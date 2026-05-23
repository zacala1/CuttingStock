using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Wall-clock performance budgets for the three 2D solvers. Counterpart to
    /// <c>PerformanceTests1D</c>. Generators draw items strictly inside the
    /// effective sheet (after trim) so success is guaranteed and we measure the
    /// solver, not the problem.
    ///
    /// Tagged [Category("Performance")] for selective exclusion.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class PerformanceTests2D
    {
        // Budgets in milliseconds — Release build.
        private const int ShelfSmallMs   = 500;
        private const int ShelfMediumMs  = 3_000;
        private const int ShelfLargeMs   = 15_000;
        private const int Cg2DSmallMs    = 5_000;
        private const int Cg2DMediumMs   = 35_000;
        private const int MipSmallMs     = 35_000;
        private const int MipMediumMs    = 35_000;

        // ─── Shelf Guillotine ─────────────────────────────────────────

        [Test]
        public void Shelf_25Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 11, items: 25);
            var (ms, r) = TimedSolve(new ShelfGuillotineSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(ShelfSmallMs);
        }

        [Test]
        public void Shelf_100Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 22, items: 100);
            var (ms, r) = TimedSolve(new ShelfGuillotineSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(ShelfMediumMs);
        }

        [Test]
        public void Shelf_400Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 33, items: 400);
            var (ms, r) = TimedSolve(new ShelfGuillotineSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(ShelfLargeMs);
        }

        // ─── Column Generation 2D ─────────────────────────────────────

        [Test]
        public void Cg2D_25Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 44, items: 25);
            var (ms, r) = TimedSolve(new ColumnGeneration2DSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(Cg2DSmallMs);
        }

        [Test]
        public void Cg2D_100Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 55, items: 100);
            var (ms, r) = TimedSolve(new ColumnGeneration2DSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(Cg2DMediumMs);
        }

        // ─── Staged MIP Guillotine ────────────────────────────────────

        [Test]
        public void Mip_25Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 66, items: 25);
            var (ms, r) = TimedSolve(new StagedMipGuillotineSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(MipSmallMs);
        }

        [Test]
        public void Mip_60Rects_FinishesWithinBudget()
        {
            var (sheets, orders) = Generate(seed: 77, items: 60);
            var (ms, r) = TimedSolve(new StagedMipGuillotineSolver(), sheets, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(MipMediumMs);
        }

        // ─── Relative speed sanity ────────────────────────────────────

        [Test]
        public void Shelf_IsFastest_Among_2D()
        {
            // Shelf is the heuristic — should be at least 3× faster than MIP on
            // small inputs. Catches a perf regression that closes the gap.
            var (sheets, orders) = Generate(seed: 88, items: 30);

            var (shelfMs, sr) = TimedSolve(new ShelfGuillotineSolver(), sheets, orders);
            var (mipMs, mr)   = TimedSolve(new StagedMipGuillotineSolver(), sheets, orders);

            sr.Success.Should().BeTrue();
            mr.Success.Should().BeTrue();
            // Shelf typically << 100ms, MIP includes CBC setup so several hundred ms.
            (shelfMs * 3).Should().BeLessThan(Math.Max(mipMs, 100),
                $"Shelf ({shelfMs}ms) should be ≥3× faster than MIP ({mipMs}ms)");
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private static (long ms, SolverResult2D r) TimedSolve(
            ICuttingSolver2D solver, List<Sheet> sheets, List<RectOrder> orders)
        {
            var opt = new SolverOptions2D { TimeLimitMs = 30_000 };
            var sw = Stopwatch.StartNew();
            var r = solver.Solve(sheets, orders, opt);
            sw.Stop();
            return (sw.ElapsedMilliseconds, r);
        }

        /// <summary>
        /// Feasibility-guaranteed generator. Sheet ≥ 2× largest item, total item
        /// area ≤ 30% of stock area. Items drawn from an 8-shape palette so
        /// MIP / CG patterns can share columns across many rects of the same
        /// dimensions.
        /// </summary>
        private static (List<Sheet> sheets, List<RectOrder> orders) Generate(int seed, int items)
        {
            var rng = new Random(seed);
            var sheets = new List<Sheet> { new(2440, 1220, items * 2) };
            (int W, int H)[] palette =
            {
                (200, 150), (300, 200), (400, 300), (250, 250),
                (500, 200), (350, 350), (600, 300), (400, 400),
            };
            var counts = new Dictionary<(int, int), int>();
            for (int i = 0; i < items; i++)
            {
                var (w, h) = palette[rng.Next(palette.Length)];
                counts.TryGetValue((w, h), out var c);
                counts[(w, h)] = c + 1;
            }
            var orders = new List<RectOrder>();
            foreach (var kv in counts)
                orders.Add(new RectOrder(kv.Key.Item1, kv.Key.Item2, kv.Value, allowRotation: true));
            return (sheets, orders);
        }
    }
}
