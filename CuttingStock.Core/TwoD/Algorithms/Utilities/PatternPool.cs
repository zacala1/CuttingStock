using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using Google.OrTools.LinearSolver;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Shared CG infrastructure: column type, LP master, pricing, dedup.
    /// Used by both CG2D and StagedMip solvers.
    /// </summary>
    internal static class PatternPool
    {
        public sealed class Column
        {
            public Sheet Sheet = null!;
            public int[] Counts = null!;   // items per order index
            public List<Placement> Placements = new();
        }

        /// <summary>Build a column from a heuristic pattern.</summary>
        public static Column FromPattern(CuttingPattern2D p, int orderCount)
        {
            var col = new Column { Sheet = p.Sheet, Counts = new int[orderCount] };
            foreach (var pl in p.Placements)
            {
                col.Counts[pl.OrderIndex]++;
                col.Placements.Add(ClonePlacement(pl));
            }
            return col;
        }

        /// <summary>Build a column from a DP solve, offsetting placements by trim.</summary>
        public static Column FromDpResult(Sheet sheet, GuillotineKnapsackDp.Result dp, int orderCount, int trim)
        {
            var col = new Column { Sheet = sheet, Counts = new int[orderCount] };
            foreach (var pl in dp.Placements)
            {
                col.Counts[pl.OrderIndex]++;
                col.Placements.Add(new Placement
                {
                    OrderIndex = pl.OrderIndex,
                    X = pl.X + trim,
                    Y = pl.Y + trim,
                    Width = pl.Width,
                    Height = pl.Height,
                    Rotated = pl.Rotated,
                });
            }
            return col;
        }

        /// <summary>Clone a placement.</summary>
        public static Placement ClonePlacement(Placement p) => new Placement
        {
            OrderIndex = p.OrderIndex,
            X = p.X,
            Y = p.Y,
            Width = p.Width,
            Height = p.Height,
            Rotated = p.Rotated,
        };

        /// <summary>
        /// Build DP items from duals. Skips zero-profit orders; emits rotated variant
        /// only when both global and per-item rotation flags allow it.
        /// </summary>
        public static List<GuillotineKnapsackDp.Item> BuildDpItems(
            List<RectOrder> orders, double[] pi, SolverOptions2D options, double eps = 1e-6)
        {
            var list = new List<GuillotineKnapsackDp.Item>(capacity: orders.Count * 2);
            for (int i = 0; i < orders.Count; i++)
            {
                if (pi[i] <= eps) continue;
                var o = orders[i];
                list.Add(new GuillotineKnapsackDp.Item
                {
                    OrderIndex = i, W = o.Width, H = o.Height, Rotated = false, Profit = pi[i]
                });
                if (options.AllowRotation && o.AllowRotation && o.Width != o.Height)
                {
                    list.Add(new GuillotineKnapsackDp.Item
                    {
                        OrderIndex = i, W = o.Height, H = o.Width, Rotated = true, Profit = pi[i]
                    });
                }
            }
            return list;
        }

        /// <summary>Solve the continuous master LP (GLOP). Returns primal x and dual pi.</summary>
        public static bool SolveLpMaster(List<Column> columns, int[] demand, out double[] x, out double[] pi)
        {
            x = Array.Empty<double>();
            pi = Array.Empty<double>();

            var solver = Solver.CreateSolver("GLOP");
            if (solver == null) return false;

            int n = demand.Length;
            int m = columns.Count;

            var vars = new Variable[m];
            for (int p = 0; p < m; p++)
                vars[p] = solver.MakeNumVar(0.0, double.PositiveInfinity, $"x{p}");

            var cons = new Constraint[n];
            for (int i = 0; i < n; i++)
            {
                cons[i] = solver.MakeConstraint(demand[i], double.PositiveInfinity, $"d{i}");
                for (int p = 0; p < m; p++)
                    if (columns[p].Counts[i] != 0)
                        cons[i].SetCoefficient(vars[p], columns[p].Counts[i]);
            }

            var obj = solver.Objective();
            for (int p = 0; p < m; p++) obj.SetCoefficient(vars[p], columns[p].Sheet.Area);
            obj.SetMinimization();

            var status = solver.Solve();
            if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE)
                return false;

            x = vars.Select(v => v.SolutionValue()).ToArray();
            pi = cons.Select(c => c.DualValue()).ToArray();
            return true;
        }

        /// <summary>Price best single column across all sheet types (most negative reduced cost).</summary>
        public static Column? PriceBestColumn(
            List<Sheet> sheets,
            List<RectOrder> orders,
            double[] pi,
            SolverOptions2D options,
            int orderCount,
            double eps = 1e-6,
            Func<bool>? cancel = null)
        {
            Column? best = null;
            double bestRc = -eps;
            foreach (var col in PriceImprovingColumns(sheets, orders, pi, options, orderCount, eps, cancel))
            {
                double rc = col.Sheet.Area;
                for (int i = 0; i < col.Counts.Length; i++) rc -= pi[i] * col.Counts[i];
                if (rc < bestRc)
                {
                    bestRc = rc;
                    best = col;
                }
            }
            return best;
        }

        /// <summary>Yields one improving column per sheet type (multi-pricing).</summary>
        public static IEnumerable<Column> PriceImprovingColumns(
            List<Sheet> sheets,
            List<RectOrder> orders,
            double[] pi,
            SolverOptions2D options,
            int orderCount,
            double eps = 1e-6,
            Func<bool>? cancel = null)
        {
            foreach (var sheet in sheets)
            {
                if (cancel?.Invoke() == true) yield break;

                var dpItems = BuildDpItems(orders, pi, options, eps);
                if (dpItems.Count == 0) continue;

                int Wu = sheet.Width  - 2 * options.Trim;
                int Hu = sheet.Height - 2 * options.Trim;
                if (Wu <= 0 || Hu <= 0) continue;

                var dp = new GuillotineKnapsackDp(Wu, Hu, dpItems, options.Kerf);
                var dpRes = dp.Solve();

                double rc = sheet.Area - dpRes.Profit;
                if (rc >= -eps) continue;      // not improving for this sheet type

                var col = FromDpResult(sheet, dpRes, orderCount, options.Trim);
                if (col.Counts.Sum() == 0) continue;
                yield return col;
            }
        }

        /// <summary>FNV-1a fingerprint of (sheet dims, counts). Master LP only sees counts, so
        /// columns with the same signature are interchangeable — safe to dedup.</summary>
        public static long Signature(Column c)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                const long prime = 1099511628211L;
                h = (h ^ c.Sheet.Width) * prime;
                h = (h ^ c.Sheet.Height) * prime;
                for (int i = 0; i < c.Counts.Length; i++)
                    h = (h ^ c.Counts[i]) * prime;
                return h;
            }
        }

        /// <summary>Add column if its signature is new. Returns true if added.</summary>
        public static bool AddIfNew(List<Column> columns, HashSet<long> signatures, Column newCol)
        {
            long sig = Signature(newCol);
            if (!signatures.Add(sig)) return false;
            columns.Add(newCol);
            return true;
        }
    }
}
