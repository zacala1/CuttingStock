using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Wall-clock performance budgets for the three 1D solvers across input sizes.
    /// These complement the informational benchmark workloads by failing the build
    /// when a regression blows past the budget. Budgets are generous (3-5× observed
    /// runtimes on an i5-14600KF Release build) so transient CI noise doesn't
    /// flake them, but a genuine algorithmic regression (e.g. accidental O(N²)
    /// in the DP) gets caught.
    ///
    /// The two solver families have different sensitivity profiles, so they get
    /// different generators:
    ///   - Greedy / CG: cost grows with the number of *distinct* order lengths
    ///     (DP state space). We test with all-distinct inputs.
    ///   - Arc Flow MIP: cost grows with distinct lengths × stock arc graph. We
    ///     cap distinct lengths via a palette to keep the MIP tractable; real
    ///     construction work also has this profile (a few standard lengths,
    ///     thousands of orders).
    ///
    /// All tests are tagged [Category("Performance")] so they can be excluded
    /// from fast feedback runs (`--filter "TestCategory!=Performance"`).
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class PerformanceTests1D
    {
        // Budgets in milliseconds — Release build, single-thread.
        private const int GreedySmallMs   = 500;
        private const int GreedyMediumMs  = 5_000;
        private const int GreedyLargeMs   = 30_000;
        private const int CgSmallMs       = 2_000;
        private const int CgMediumMs      = 15_000;
        private const int ArcFlowSmallMs  = 35_000;
        private const int ArcFlowMediumMs = 35_000;

        // ─── Greedy Knapsack ──────────────────────────────────────────

        [Test]
        public void Greedy_50DistinctOrders_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDistinct(seed: 11, orderCount: 50);
            var (ms, r) = TimedSolve(new GreedyKnapsackSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(GreedySmallMs,
                $"Greedy on 50 distinct orders should finish under {GreedySmallMs}ms (was {ms}ms)");
        }

        [Test]
        public void Greedy_250DistinctOrders_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDistinct(seed: 22, orderCount: 250);
            var (ms, r) = TimedSolve(new GreedyKnapsackSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(GreedyMediumMs,
                $"Greedy on 250 distinct orders should finish under {GreedyMediumMs}ms (was {ms}ms)");
        }

        [Test]
        public void Greedy_1000DistinctOrders_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDistinct(seed: 33, orderCount: 1000);
            var (ms, r) = TimedSolve(new GreedyKnapsackSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(GreedyLargeMs,
                $"Greedy on 1000 distinct orders should finish under {GreedyLargeMs}ms (was {ms}ms)");
        }

        // ─── Column Generation ────────────────────────────────────────

        [Test]
        public void ColumnGeneration_50DistinctOrders_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDistinct(seed: 44, orderCount: 50);
            var (ms, r) = TimedSolve(new ColumnGenerationSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(CgSmallMs,
                $"CG on 50 distinct orders should finish under {CgSmallMs}ms (was {ms}ms)");
        }

        [Test]
        public void ColumnGeneration_250DistinctOrders_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDistinct(seed: 55, orderCount: 250);
            var (ms, r) = TimedSolve(new ColumnGenerationSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(CgMediumMs,
                $"CG on 250 distinct orders should finish under {CgMediumMs}ms (was {ms}ms)");
        }

        // ─── Arc Flow MIP ─────────────────────────────────────────────
        // Bounded by the solver's own 30 s internal time limit; the test budget
        // tolerates that ceiling without flagging it.

        [Test]
        public void ArcFlow_DensePalette200_FinishesWithinBudget()
        {
            // 200 items drawn from a 12-length palette — dense demand vector,
            // small constraint count → MIP-friendly.
            var (stocks, orders) = GenerateDensePalette(seed: 66, itemCount: 200);
            var (ms, r) = TimedSolve(new ArcFlowSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(ArcFlowSmallMs,
                $"ArcFlow on dense 200-item palette should finish under {ArcFlowSmallMs}ms (was {ms}ms)");
        }

        [Test]
        public void ArcFlow_DensePalette600_FinishesWithinBudget()
        {
            var (stocks, orders) = GenerateDensePalette(seed: 77, itemCount: 600);
            var (ms, r) = TimedSolve(new ArcFlowSolver(), stocks, orders);
            r.Success.Should().BeTrue();
            ms.Should().BeLessThan(ArcFlowMediumMs,
                $"ArcFlow on dense 600-item palette should finish under {ArcFlowMediumMs}ms (was {ms}ms)");
        }

        // ─── Relative speed sanity ────────────────────────────────────

        [Test]
        public void Greedy_BeatsArcFlow_OnDenseInput()
        {
            // On a dense-palette input that ArcFlow can actually solve, Greedy
            // should still be at least 5× faster — that's the heuristic's value
            // proposition. Catches a Greedy perf regression that closes the gap.
            var (stocks, orders) = GenerateDensePalette(seed: 88, itemCount: 200);
            var ordersA = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
            var ordersB = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();

            var (greedyMs, gr) = TimedSolve(new GreedyKnapsackSolver(), stocks, ordersA);
            var (afMs, afr)    = TimedSolve(new ArcFlowSolver(), stocks, ordersB);

            gr.Success.Should().BeTrue();
            afr.Success.Should().BeTrue();
            (greedyMs * 5).Should().BeLessThan(Math.Max(afMs, 50),
                $"Greedy ({greedyMs}ms) should be ≥5× faster than ArcFlow ({afMs}ms)");
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private static (long ms, SolverResult result) TimedSolve(
            ICuttingSolver solver, List<RebarStock> stocks, List<Order> orders)
        {
            // No JIT warm-up: ArcFlow's warm-up call would itself hit the 30 s
            // internal MIP limit. Steady-state isn't worth the cost at these
            // coarse budgets.
            var opt = new SolverOptions();
            var sw = Stopwatch.StartNew();
            var r = solver.Solve(stocks, orders, opt);
            sw.Stop();
            return (sw.ElapsedMilliseconds, r);
        }

        /// <summary>
        /// All-distinct lengths, every order has Quantity=1. Stresses the per-order
        /// dimension of the Greedy DP and the CG pricing pool.
        /// </summary>
        private static (List<RebarStock> stocks, List<Order> orders) GenerateDistinct(int seed, int orderCount)
        {
            var rng = new Random(seed);
            var stocks = new List<RebarStock> { new(12000, orderCount * 2) };
            var orders = new List<Order>(orderCount);
            var seen = new HashSet<int>();
            while (orders.Count < orderCount)
            {
                int len = 500 + rng.Next(0, 6501);  // 500..7000
                if (seen.Add(len)) orders.Add(new Order(len, 1));
            }
            return (stocks, orders);
        }

        /// <summary>
        /// Items drawn from a fixed 12-length palette and aggregated. Mirrors real
        /// construction work (handful of standard bar lengths, repeated). Keeps
        /// the Arc Flow MIP's constraint count manageable.
        /// </summary>
        private static (List<RebarStock> stocks, List<Order> orders) GenerateDensePalette(int seed, int itemCount)
        {
            var rng = new Random(seed);
            int[] palette = { 600, 900, 1200, 1500, 1800, 2400, 3000, 3600, 4200, 5000, 6000, 7200 };
            var stocks = new List<RebarStock> { new(12000, itemCount * 2) };
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < itemCount; i++)
            {
                int len = palette[rng.Next(palette.Length)];
                counts.TryGetValue(len, out var c);
                counts[len] = c + 1;
            }
            return (stocks, counts.Select(kv => new Order(kv.Key, kv.Value)).ToList());
        }
    }
}
