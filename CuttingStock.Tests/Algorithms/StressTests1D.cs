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
    /// Heavy-load stress tests for the 1D solvers. These probe the algorithms
    /// well past their typical scenarios (5000+ items, sparse multi-stock, many
    /// distinct lengths) to catch:
    ///   - quadratic-or-worse regressions in DP / post-processing,
    ///   - state explosion in the Greedy DP dedup table,
    ///   - LP feasibility failures with extreme demand sparsity,
    ///   - Arc Flow MIP scaling cliff (it's exact, so stress with care).
    ///
    /// Marked [Category("Stress")] so they can be excluded from fast feedback
    /// runs (`--filter "TestCategory!=Stress"`) but still run on demand.
    /// </summary>
    [TestFixture]
    [Category("Stress")]
    public class StressTests1D
    {
        // ─── Greedy ────────────────────────────────────────────────────

        [Test]
        public void Greedy_2000DistinctOrders_CompletesAndCovers()
        {
            // Greedy's DP iterates per-order with full cut-list cloning. At 5000
            // distinct lengths the state space exceeds practical memory bounds
            // on this DP design; 2000 is the realistic upper end for the
            // heuristic and still well above what real construction loads ever
            // hit (palette-aggregated input).
            var (stocks, orders) = GenerateDistinct(seed: 101, orderCount: 2000);
            int demand = orders.Sum(o => o.Quantity);

            var sw = Stopwatch.StartNew();
            var r = new GreedyKnapsackSolver().Solve(stocks, orders, new SolverOptions());
            sw.Stop();

            r.Success.Should().BeTrue($"Greedy failed: {r.ErrorMessage}");
            sw.ElapsedMilliseconds.Should().BeLessThan(120_000,
                $"Greedy on 2000 distinct orders should finish under 120s (was {sw.ElapsedMilliseconds}ms)");
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(demand);
        }

        [Test]
        public void Greedy_HeavyDuplicates_HandlesSameLengthThousands()
        {
            // 5000 identical orders — exercises the DP dedup path. Optimum is
            // 5000 / 6 = 834 bars (six 2000mm cuts per 12000mm bar). Greedy's
            // Pass1 cap of 2 cuts per bar means it opens roughly 2x-3x that many
            // bars before post-processing consolidates; we assert ≤ 3000 to
            // confirm it doesn't blow up to one-cut-per-bar (5000 bars).
            var stocks = new List<RebarStock> { new(12000, 5000) };
            var orders = new List<Order> { new(2000, 5000) };

            var sw = Stopwatch.StartNew();
            var r = new GreedyKnapsackSolver().Solve(stocks, orders, new SolverOptions());
            sw.Stop();

            r.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(30_000);
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(5000);
            r.StockUsed.Should().BeLessThanOrEqualTo(3000,
                "Greedy with Pass1 max-2 cap should still consolidate to ≤ 3000 bars (optimum 834)");
        }

        [Test]
        public void Greedy_MixedMultiStock_BalancesAcrossPool()
        {
            // 3 distinct stock lengths × 50 each, 800 mixed orders.
            var stocks = new List<RebarStock>
            {
                new(6000, 200),
                new(9000, 200),
                new(12000, 200),
            };
            var orders = GenerateDistinct(seed: 202, orderCount: 800).orders
                .Where(o => o.Length <= 6000)  // ensure feasibility across all stocks
                .ToList();

            var sw = Stopwatch.StartNew();
            var r = new GreedyKnapsackSolver().Solve(stocks, orders, new SolverOptions());
            sw.Stop();

            r.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(30_000);
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(orders.Sum(o => o.Quantity));
        }

        // ─── Column Generation ────────────────────────────────────────

        [Test]
        public void CG_1000DistinctOrders_FeasibleAndCovers()
        {
            var (stocks, orders) = GenerateDistinct(seed: 303, orderCount: 1000);
            int demand = orders.Sum(o => o.Quantity);

            var sw = Stopwatch.StartNew();
            var r = new ColumnGenerationSolver().Solve(stocks, orders, new SolverOptions());
            sw.Stop();

            r.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(60_000);
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(demand);
        }

        // ─── Arc Flow ─────────────────────────────────────────────────

        [Test]
        public void ArcFlow_DensePalette2000_HitsTimeBudget()
        {
            // Realistic construction load: 2000 items, 12-length palette.
            var (stocks, orders) = GenerateDensePalette(seed: 404, itemCount: 2000);

            var sw = Stopwatch.StartNew();
            var r = new ArcFlowSolver().Solve(stocks, orders, new SolverOptions());
            sw.Stop();

            r.Success.Should().BeTrue();
            // ArcFlow caps internally at 30s; we accept up to 60s wall-clock for setup + extract.
            sw.ElapsedMilliseconds.Should().BeLessThan(60_000);
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(orders.Sum(o => o.Quantity));
        }

        // ─── Generators ───────────────────────────────────────────────

        private static (List<RebarStock> stocks, List<Order> orders) GenerateDistinct(int seed, int orderCount)
        {
            var rng = new Random(seed);
            var stocks = new List<RebarStock> { new(12000, Math.Max(orderCount, 2000)) };
            var orders = new List<Order>(orderCount);
            var seen = new HashSet<int>();
            while (orders.Count < orderCount)
            {
                int len = 500 + rng.Next(0, 6501);
                if (seen.Add(len)) orders.Add(new Order(len, 1));
            }
            return (stocks, orders);
        }

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
