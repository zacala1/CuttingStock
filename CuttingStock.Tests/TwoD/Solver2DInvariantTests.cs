using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Property-based / fuzzing tests for the 2D solver suite. For each randomly-generated
    /// feasible instance, we verify the universal invariants every solver must satisfy:
    ///
    ///   (a) every order's quantity is exactly produced (no over- and no under-production),
    ///   (b) every placement lies within its sheet (after trim),
    ///   (c) no two placements on the same sheet overlap (kerf-aware),
    ///   (d) every placement is a guillotine cut decomposition (Beasley separator test),
    ///   (e) placement dimensions match the order or its rotated counterpart,
    ///   (f) no item that disallows rotation is rotated,
    ///   (g) the cost reported is consistent with the waste area and AlphaArea,
    ///   (h) sheet usage does not exceed sheet inventory.
    ///
    /// 30 random seeds × 3 solvers = 90 fuzzing runs per fixture method.
    /// </summary>
    [TestFixture]
    public class Solver2DInvariantTests
    {
        private const int Seeds = 30;

        private static IEnumerable<TestCaseData> AllSolversAllSeeds()
        {
            string[] names = { "Shelf", "CG2D", "MIP" };
            for (int i = 0; i < 3; i++)
                for (int s = 0; s < Seeds; s++)
                    yield return new TestCaseData(i, s).SetName($"{names[i]}_seed{s}");
        }

        [TestCaseSource(nameof(AllSolversAllSeeds))]
        public void RandomFeasibleInstance_AllInvariantsHold(int solverIdx, int seed)
        {
            var (sheets, orders, options) = GenerateFeasibleInstance(seed);
            var solver = MakeSolver(solverIdx);

            var r = solver.Solve(sheets, orders, options);

            r.Success.Should().BeTrue("solver {0} must succeed on seed {1}", solver.Name, seed);

            // (a) exact demand
            var produced = SolverUtils2D.CountPlaced(r.Patterns, orders.Count);
            for (int i = 0; i < orders.Count; i++)
                produced[i].Should().Be(orders[i].Quantity, "order {0} demand must match exactly", i);

            // (h) sheet inventory
            var sheetUsage = new Dictionary<(int w, int h), int>();
            foreach (var pat in r.Patterns)
            {
                var key = (pat.Sheet.Width, pat.Sheet.Height);
                sheetUsage.TryGetValue(key, out var cur);
                sheetUsage[key] = cur + pat.Multiplicity;
            }
            foreach (var s in sheets)
            {
                sheetUsage.TryGetValue((s.Width, s.Height), out var used);
                used.Should().BeLessThanOrEqualTo(s.Quantity, "sheet {0}×{1} usage must respect inventory", s.Width, s.Height);
            }

            // (g) cost consistency
            long expectedCost = (long)Math.Round(r.TotalWasteArea * (double)options.AlphaArea);
            r.TotalCost.Should().Be(expectedCost);

            foreach (var pat in r.Patterns)
            {
                // (b) within sheet (after trim)
                SolverUtils2D.WithinSheet(pat.Placements, pat.Sheet, options.Trim)
                    .Should().BeTrue("placements must lie inside the trimmed sheet");

                // (c) no overlap (kerf-aware)
                SolverUtils2D.HasOverlap(pat.Placements, options.Kerf)
                    .Should().BeFalse("placements must not overlap (kerf={0})", options.Kerf);

                // (d) guillotine compliance
                var rects = pat.Placements.Select(p => (p.X, p.Y, p.Width, p.Height)).ToList();
                GuillotineValidator.IsGuillotineCompliant(0, 0, pat.Sheet.Width, pat.Sheet.Height, rects)
                    .Should().BeTrue("pattern must admit a guillotine cut decomposition");

                // (e) dimension match + (f) rotation flag respected
                foreach (var pl in pat.Placements)
                {
                    var o = orders[pl.OrderIndex];
                    bool matchAsIs = pl.Width == o.Width && pl.Height == o.Height;
                    bool matchRot  = pl.Width == o.Height && pl.Height == o.Width;
                    (matchAsIs || matchRot).Should().BeTrue(
                        "dims must match order (got {0}×{1}, expected {2}×{3} or rotated)",
                        pl.Width, pl.Height, o.Width, o.Height);
                    if (!o.AllowRotation || !options.AllowRotation)
                    {
                        pl.Rotated.Should().BeFalse("rotation must respect order/global flag");
                        matchAsIs.Should().BeTrue();
                    }
                }
            }
        }

        // ----- generator -----

        /// <summary>
        /// Generate a feasibility-guaranteed random instance. Strategy:
        ///  1) draw sheet dimensions and stock,
        ///  2) draw items strictly bounded by the *effective* sheet (post trim + kerf),
        ///  3) draw a total demand whose area is at most 30% of total stock area,
        ///     leaving plenty of headroom for any heuristic packer to succeed.
        /// </summary>
        private static (List<Sheet> sheets, List<RectOrder> orders, SolverOptions2D options)
            GenerateFeasibleInstance(int seed)
        {
            var rng = new Random(seed * 7919 + 31);

            int kerf = rng.Next(0, 4);                  // 0..3
            int trim = rng.Next(0, 3);                  // 0..2

            int sheetW = 400 + rng.Next(0, 20) * 50;    // 400..1350
            int sheetH = 400 + rng.Next(0, 20) * 50;
            int sheetQty = 8 + rng.Next(0, 8);          // 8..15

            var sheets = new List<Sheet> { new(sheetW, sheetH, sheetQty) };
            if (rng.Next(0, 2) == 0)
            {
                int sw2 = sheetW + 100 + rng.Next(0, 4) * 50;
                int sh2 = sheetH + 100 + rng.Next(0, 4) * 50;
                sheets.Add(new Sheet(sw2, sh2, sheetQty));
            }

            // Effective dimensions of the smallest sheet, after trim + one kerf safety margin.
            int effW = sheetW - 2 * trim - kerf;
            int effH = sheetH - 2 * trim - kerf;
            int maxItemW = Math.Max(20, effW / 3);
            int maxItemH = Math.Max(20, effH / 3);

            long stockArea = sheets.Sum(s => s.Area * s.Quantity);
            long areaBudget = stockArea / 4;            // use at most 25% of total stock

            int orderTypes = 2 + rng.Next(0, 4);        // 2..5 distinct shapes
            var orders = new List<RectOrder>(orderTypes);
            long usedArea = 0;
            for (int i = 0; i < orderTypes; i++)
            {
                int w = 20 + rng.Next(0, maxItemW - 19);
                int h = 20 + rng.Next(0, maxItemH - 19);
                long itemArea = (long)w * h;
                int maxQty = (int)Math.Max(1, Math.Min(5, (areaBudget - usedArea) / Math.Max(1, itemArea)));
                int qty = 1 + rng.Next(0, maxQty);
                if (qty <= 0) qty = 1;
                bool rot = rng.Next(0, 2) == 0;
                orders.Add(new RectOrder(w, h, qty, rot));
                usedArea += itemArea * qty;
                if (usedArea >= areaBudget) break;
            }

            bool globalRot = rng.Next(0, 2) == 0;
            float alpha = (float)(0.5 + rng.NextDouble());

            var options = new SolverOptions2D
            {
                Kerf = kerf,
                Trim = trim,
                AllowRotation = globalRot,
                AlphaArea = alpha,
                TimeLimitMs = 6000,
                UsageOrder = rng.Next(0, 2) == 0
                    ? CuttingStock.Core.Domain.StockUsageOrder.SmallToLarge
                    : CuttingStock.Core.Domain.StockUsageOrder.LargeToSmall,
            };

            return (sheets, orders, options);
        }

        private static ICuttingSolver2D MakeSolver(int idx) => idx switch
        {
            0 => new ShelfGuillotineSolver(),
            1 => new ColumnGeneration2DSolver(),
            2 => new StagedMipGuillotineSolver(),
            _ => new ShelfGuillotineSolver(),
        };
    }
}
