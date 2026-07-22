using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms
{
    /// <summary>
    /// Gilmore-Gomory column generation for 2D guillotine cutting stock.
    /// Master LP (GLOP) + Beasley 1985 guillotine knapsack DP pricing.
    /// LP solution is floored, residual covered by shelf heuristic.
    /// Ref: Gilmore &amp; Gomory 1965; Cintra et al. 2008; Beasley 1985.
    /// </summary>
    public sealed class ColumnGeneration2DSolver : ICuttingSolver2D
    {
        public string Name => "Column Generation 2D (Gilmore-Gomory)";
        public string Description => "CG with GLOP master + Beasley DP pricing, LP-rounded.";
        public string TimeComplexity => "Poly/iter, exp worst-case";

        private const int MaxCgIterations = 200;

        /// <inheritdoc />
        public SolverResult2D Solve(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            IProgress<double>? progress = null)
        {
            var sw = Stopwatch.StartNew();
            var result = new SolverResult2D { AlgorithmName = Name };
            try
            {
                var input = TwoDInputPreprocessor.Preprocess(sheets, orders, result);
                if (input.ShouldReturn)
                {
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }

                sheets = input.Sheets;
                orders = input.Orders;
                int n = orders.Count;
                int[] demand = orders.Select(o => o.Quantity).ToArray();

                // 1) Warm start with the shelf heuristic.
                var warm = new ShelfGuillotineSolver().Solve(sheets, orders, options);
                if (!warm.Success || warm.Patterns.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Warm start failed: " + (warm.ErrorMessage ?? "infeasible");
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }

                var columns = new List<PatternPool.Column>();
                var signatures = new HashSet<long>();
                foreach (var p in warm.Patterns)
                {
                    var col = PatternPool.FromPattern(p, n);
                    PatternPool.AddIfNew(columns, signatures, col);
                }

                // 2) Column generation loop with multi-pricing: every iteration picks ALL
                //    improving columns (one per sheet type), not just the best one. Empirically
                //    this shrinks the iteration count by 2–4× on multi-sheet inputs.
                // TimeLimitMs is the total wall-clock budget; warm start already consumed
                // some of it, so the deadline is from session start, not from "now".
                long deadline = options.TimeLimitMs;
                for (int iter = 0; iter < MaxCgIterations; iter++)
                {
                    if (sw.ElapsedMilliseconds > deadline) break;

                    if (!PatternPool.SolveLpMaster(columns, demand, out _, out var pi))
                    {
                    // LP infeasible — fall back to warm start.
                    result.Patterns = warm.Patterns;
                    TwoDResultFinalizer.FinalizeAndValidate(sheets, orders, options, result);
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                    }

                    bool anyAdded = false;
                    foreach (var newCol in PatternPool.PriceImprovingColumns(
                                 sheets, orders, pi, options, n,
                                 cancel: () => sw.ElapsedMilliseconds > deadline))
                    {
                        if (PatternPool.AddIfNew(columns, signatures, newCol))
                            anyAdded = true;
                    }

                    if (!anyAdded) break;   // optimum reached or no new columns
                    progress?.Report(Math.Min(1.0, (double)iter / MaxCgIterations));
                }

                // 3) Final LP, then floor to integer multiplicities and mop up the residual.
                if (!PatternPool.SolveLpMaster(columns, demand, out var xFinal, out _))
                {
                    result.Patterns = warm.Patterns;
                    TwoDResultFinalizer.FinalizeAndValidate(sheets, orders, options, result);
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }

                var integerPatterns = RoundDownAndMaterialize(
                    columns, xFinal, sheets, n, demand, out var produced, out var sheetUsage);

                CoverResidualWithHeuristic(
                    integerPatterns, sheets, sheetUsage, orders, demand, produced, options, result);

                if (integerPatterns.Count == 0)
                    integerPatterns = warm.Patterns;

                integerPatterns = SolverUtils2D.TrimToDemand(integerPatterns, demand, out var finalProduced);
                if (!finalProduced.SequenceEqual(demand) && result.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to cover all residual demand exactly.";
                }

                result.Patterns = integerPatterns;
                TwoDResultFinalizer.FinalizeAndValidate(sheets, orders, options, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            TwoDResultFinalizer.FinalizeResult(result, options);
            return result;
        }

        // ---- helpers ----

        /// <summary>
        /// Floor the LP solution to integer multiplicities while respecting per-sheet stock,
        /// and materialize the corresponding patterns. Outputs the per-order produced count
        /// and per-sheet usage to drive the residual mop-up.
        /// </summary>
        private static List<CuttingPattern2D> RoundDownAndMaterialize(
            List<PatternPool.Column> columns,
            double[] xFinal,
            List<Sheet> sheets,
            int n,
            int[] demand,
            out int[] produced,
            out int[] sheetUsage)
        {
            produced = new int[n];
            sheetUsage = new int[sheets.Count];
            var sheetIdx = new Dictionary<Sheet, int>(sheets.Count);
            for (int i = 0; i < sheets.Count; i++) sheetIdx[sheets[i]] = i;

            var patterns = new List<CuttingPattern2D>();
            for (int p = 0; p < columns.Count; p++)
            {
                int k = (int)Math.Floor(xFinal[p] + 1e-9);
                if (k <= 0) continue;
                var col = columns[p];

                // Respect sheet inventory.
                int idx = sheetIdx[col.Sheet];
                int avail = col.Sheet.Quantity - sheetUsage[idx];
                if (k > avail) k = avail;
                if (k <= 0) continue;
                sheetUsage[idx] += k;

                patterns.Add(new CuttingPattern2D
                {
                    Sheet = col.Sheet,
                    Multiplicity = k,
                    Placements = col.Placements.Select(PatternPool.ClonePlacement).ToList(),
                });
                for (int i = 0; i < n; i++) produced[i] += k * col.Counts[i];
            }
            return patterns;
        }

        /// <summary>
        /// Build a residual <see cref="RectOrder"/> list from unmet demand and run the
        /// shelf heuristic on the leftover stock; append its patterns to <paramref name="patterns"/>.
        /// Re-maps order indices back to the original order list.
        /// </summary>
        private static void CoverResidualWithHeuristic(
            List<CuttingPattern2D> patterns,
            List<Sheet> sheets,
            int[] sheetUsage,
            List<RectOrder> orders,
            int[] demand,
            int[] produced,
            SolverOptions2D options,
            SolverResult2D result)
        {
            int n = orders.Count;
            var residualOriginalIndex = new List<int>();
            var residual = new List<RectOrder>();
            for (int i = 0; i < n; i++)
            {
                int need = demand[i] - produced[i];
                if (need <= 0) continue;
                var o = orders[i];
                residual.Add(new RectOrder(o.Width, o.Height, need, o.AllowRotation));
                residualOriginalIndex.Add(i);
            }
            if (residual.Count == 0) return;

            var remainSheets = new List<Sheet>();
            for (int i = 0; i < sheets.Count; i++)
            {
                int left = sheets[i].Quantity - sheetUsage[i];
                if (left > 0) remainSheets.Add(new Sheet(sheets[i].Width, sheets[i].Height, left));
            }
            if (remainSheets.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Out of sheet stock during residual coverage.";
                return;
            }

            var mop = new ShelfGuillotineSolver().Solve(remainSheets, residual, options);
            if (!mop.Success)
            {
                result.Success = false;
                result.ErrorMessage = "Residual coverage failed: " + mop.ErrorMessage;
                return;
            }

            foreach (var pat in mop.Patterns)
            {
                patterns.Add(new CuttingPattern2D
                {
                    Sheet = pat.Sheet,
                    Multiplicity = pat.Multiplicity,
                    Placements = pat.Placements.Select(pl => new Placement
                    {
                        OrderIndex = residualOriginalIndex[pl.OrderIndex],
                        X = pl.X, Y = pl.Y,
                        Width = pl.Width, Height = pl.Height,
                        Rotated = pl.Rotated,
                    }).ToList(),
                });
            }
        }
    }
}
