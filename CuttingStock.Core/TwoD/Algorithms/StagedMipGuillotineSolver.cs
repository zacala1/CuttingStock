using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using Google.OrTools.LinearSolver;

namespace CuttingStock.Core.TwoD.Algorithms
{
    /// <summary>
    /// Pattern-pool + integer master MIP for 2D guillotine cutting.
    /// Pool bootstrapped by shelf heuristic, enriched via CG + diversified pricing,
    /// then solved as an integer program by CBC under a hard time limit.
    /// Ref: Vance et al. 1994; Belov &amp; Scheithauer 2006; Furini et al. 2016.
    /// </summary>
    public sealed class StagedMipGuillotineSolver : ICuttingSolver2D
    {
        public string Name => "Staged Guillotine MIP (CBC)";
        public string Description => "CG-enriched pattern pool + CBC integer master.";
        public string TimeComplexity => "NP-hard, bounded by TimeLimitMs";

        private const int MaxCgIterations = 200;
        private const int DiversificationRounds = 6;
        private const int RngSeed = 13;

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

                // 1) Bootstrap pool with shelf heuristic.
                var heur = new ShelfGuillotineSolver().Solve(sheets, orders, options);
                if (!heur.Success || heur.Patterns.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Bootstrap heuristic failed: " + (heur.ErrorMessage ?? "infeasible");
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }

                var columns = new List<PatternPool.Column>();
                var signatures = new HashSet<long>();
                foreach (var p in heur.Patterns)
                    PatternPool.AddIfNew(columns, signatures, PatternPool.FromPattern(p, n));

                // 2) Enrich pool with multi-pricing column generation. Half the time budget
                //    goes to CG, the other half to the integer master. Every iteration adds
                //    one improving column per sheet type.
                var deadline = TwoDDeadline.FromStopwatch(sw, options.TimeLimitMs);
                long pricingEnd = deadline.PhaseEndMilliseconds(1, 2);

                for (int iter = 0; iter < MaxCgIterations; iter++)
                {
                    if (deadline.IsPast(pricingEnd)) break;
                    if (!PatternPool.SolveLpMaster(columns, demand, out _, out var pi)) break;

                    bool anyAdded = false;
                    foreach (var newCol in PatternPool.PriceImprovingColumns(
                                 sheets, orders, pi, options, n,
                                 cancel: () => deadline.IsPast(pricingEnd)))
                    {
                        if (PatternPool.AddIfNew(columns, signatures, newCol))
                            anyAdded = true;
                    }
                    if (!anyAdded) break;
                    progress?.Report(0.5 * iter / MaxCgIterations);
                }

                // 3) Diversification: a few rounds of perturbed pricing to enrich the pool.
                AddDiversifiedColumns(columns, signatures, sheets, orders, demand, options, deadline, n);

                // 4) Integer master MIP with hard time limit on remaining budget.
                long remaining = deadline.RemainingMillisecondsWithFloor(1000);
                bool ipSolved = SolveIntegerMaster(columns, demand, sheets, remaining, out var xInt);

                List<CuttingPattern2D> outPatterns;
                if (ipSolved && xInt != null)
                {
                    outPatterns = MaterializeMipSolution(columns, xInt);

                    // Sanity: every order must be covered.
                    var produced = new int[n];
                    foreach (var pat in outPatterns)
                    foreach (var pl in pat.Placements)
                        produced[pl.OrderIndex] += pat.Multiplicity;
                    bool covered = true;
                    for (int i = 0; i < n; i++) if (produced[i] < demand[i]) { covered = false; break; }
                    if (!covered) outPatterns = heur.Patterns;
                }
                else
                {
                    outPatterns = heur.Patterns;
                }

                outPatterns = SolverUtils2D.TrimToDemand(outPatterns, demand, out var finalProduced);
                if (!finalProduced.SequenceEqual(demand))
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to cover all demand exactly.";
                }

                result.Patterns = outPatterns;
                TwoDResultFinalizer.FinalizeAndValidate(sheets, orders, options, result);
                progress?.Report(1.0);
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

        // ---- diversification ----

        private static void AddDiversifiedColumns(
            List<PatternPool.Column> columns,
            HashSet<long> signatures,
            List<Sheet> sheets,
            List<RectOrder> orders,
            int[] demand,
            SolverOptions2D options,
            TwoDDeadline deadline,
            int n)
        {
            var rng = new Random(RngSeed);
            var pi = new double[n];

            for (int r = 0; r < DiversificationRounds; r++)
            {
                if (deadline.HasLessThanReserve(1000)) break;

                // Synthetic duals: demand * area gives a base priority proportional to
                // how much total material the order needs; jitter (0.7..1.3x) produces
                // different pricing optima per round to diversify the column pool.
                for (int i = 0; i < n; i++)
                {
                    double baseP = demand[i] * (double)orders[i].Area;
                    pi[i] = baseP * (0.7 + 0.6 * rng.NextDouble());
                }

                // We want one pattern per sheet type per round, so we inline the pricing loop
                // rather than going through PriceImprovingColumns (which only keeps reduced-cost
                // negative columns).
                foreach (var sheet in sheets)
                {
                    if (deadline.HasLessThanReserve(1000)) break;
                    var dpItems = PatternPool.BuildDpItems(orders, pi, options);
                    if (dpItems.Count == 0) continue;

                    int Wu = sheet.Width  - 2 * options.Trim;
                    int Hu = sheet.Height - 2 * options.Trim;
                    if (Wu <= 0 || Hu <= 0) continue;

                    var dp = new GuillotineKnapsackDp(Wu, Hu, dpItems, options.Kerf);
                    var dpRes = dp.Solve();
                    var col = PatternPool.FromDpResult(sheet, dpRes, n, options.Trim);
                    if (col.Counts.Sum() == 0) continue;
                    PatternPool.AddIfNew(columns, signatures, col);
                }
            }
        }

        // ---- integer master MIP via CBC ----

        private static bool SolveIntegerMaster(
            List<PatternPool.Column> columns, int[] demand, List<Sheet> sheets, long timeLimitMs, out int[]? xInt)
        {
            xInt = null;
            var solver = Solver.CreateSolver("CBC");
            if (solver == null) return false;
            solver.SetTimeLimit(timeLimitMs);

            int n = demand.Length;
            int m = columns.Count;

            // Per-column upper bound: x_p can never exceed the smallest d_i / a_pi ratio
            // over the orders that column p actually contains (otherwise it would produce
            // more of order i than demanded, which is pointless under exact-cover). This is
            // much tighter than a global demand.Max() * 2 bound and dramatically shrinks
            // the CBC branching tree. Additionally capped by the sheet's stock Q_s.
            var vars = new Variable[m];
            for (int p = 0; p < m; p++)
            {
                int ub = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (columns[p].Counts[i] > 0)
                    {
                        // Allow small slack above exact demand so the LP-rounded IP has
                        // room to overcover a few items when needed — capped by 2× demand
                        // per order, consistent with the old global bound.
                        int localUb = (2 * demand[i] + columns[p].Counts[i] - 1) / columns[p].Counts[i];
                        if (localUb < ub) ub = localUb;
                    }
                }
                if (ub == int.MaxValue) ub = 1;
                ub = Math.Min(ub, columns[p].Sheet.Quantity);
                vars[p] = solver.MakeIntVar(0, ub, $"x{p}");
            }

            // Overproduction slack o_i ≥ 0 — allows feasibility when no exact-cover combination
            // exists in the column pool. Heavily penalized in the objective.
            var over = new Variable[n];
            for (int i = 0; i < n; i++)
                over[i] = solver.MakeIntVar(0, double.PositiveInfinity, $"o{i}");

            // Exact demand: Σ a_pi · x_p − o_i = d_i
            for (int i = 0; i < n; i++)
            {
                var c = solver.MakeConstraint(demand[i], demand[i], $"d{i}");
                for (int p = 0; p < m; p++)
                    if (columns[p].Counts[i] != 0)
                        c.SetCoefficient(vars[p], columns[p].Counts[i]);
                c.SetCoefficient(over[i], -1);
            }

            // Sheet inventory: Σ_{p: sheet dims = (w,h)} x_p ≤ Σ Q_s for that (w,h).
            // Aggregating by (Width, Height) is critical when the caller hands us
            // multiple Sheet rows with identical dimensions — without the group-sum,
            // every per-row constraint clamps to its individual Quantity, and the
            // smallest one dominates (e.g. two rows of 2440×1220 with qty 3 and 5
            // would cap the IP at 3 sheets total instead of 8).
            var sheetTotals = sheets
                .GroupBy(s => (s.Width, s.Height))
                .Select(g => (Dims: g.Key, Total: g.Sum(s => s.Quantity)))
                .ToList();
            foreach (var (dims, total) in sheetTotals)
            {
                var c = solver.MakeConstraint(0, total, $"q_{dims.Width}x{dims.Height}");
                for (int p = 0; p < m; p++)
                    if (columns[p].Sheet.Width == dims.Width && columns[p].Sheet.Height == dims.Height)
                        c.SetCoefficient(vars[p], 1);
            }

            // Objective: minimize Σ s_p · x_p + bigM · Σ o_i. bigM dwarfs any single sheet
            // so the IP only resorts to overproduction when there is no exact-cover.
            double bigM = sheets.Max(s => (double)s.Area) * 1000.0 + 1.0;
            var obj = solver.Objective();
            for (int p = 0; p < m; p++) obj.SetCoefficient(vars[p], columns[p].Sheet.Area);
            for (int i = 0; i < n; i++) obj.SetCoefficient(over[i], bigM);
            obj.SetMinimization();

            var status = solver.Solve();
            if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE) return false;

            xInt = new int[m];
            for (int p = 0; p < m; p++) xInt[p] = (int)Math.Round(vars[p].SolutionValue());
            return true;
        }

        private static List<CuttingPattern2D> MaterializeMipSolution(List<PatternPool.Column> columns, int[] xInt)
        {
            var patterns = new List<CuttingPattern2D>();
            for (int p = 0; p < columns.Count; p++)
            {
                int k = xInt[p];
                if (k <= 0) continue;
                var col = columns[p];
                patterns.Add(new CuttingPattern2D
                {
                    Sheet = col.Sheet,
                    Multiplicity = k,
                    Placements = col.Placements.Select(PatternPool.ClonePlacement).ToList(),
                });
            }
            return patterns;
        }
    }
}
