using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Property-based / fuzzing invariants for the three 1D solvers. Mirrors
    /// <c>Solver2DInvariantTests</c>: 15 random seeds × 3 solvers = 45 runs per
    /// fixture method, every universal invariant checked on every successful
    /// solve.
    ///
    /// Seeds are calibrated to stay inside the Arc Flow MIP's 30 s ceiling:
    ///   - kerf locked to 0 so the GCD reduction stays large (capacity = 120
    ///     nodes instead of 2400 when kerf = 5),
    ///   - small palette (6 lengths) and modest item counts (10..25).
    ///
    /// Invariants every solver must satisfy on a feasible instance:
    ///   (a) every order is fully covered — sum of cuts at length L equals demand,
    ///   (b) no plan's consumed length exceeds its stock (kerf-aware),
    ///   (c) Leftover == stockLength − sum(cuts) − (cuts.Count − 1) * kerf,
    ///   (d) stock-usage of any RebarStock instance does not exceed its Quantity,
    ///   (e) waste length (∑ leftover &lt; γ) is consistent with reported WasteLength,
    ///   (f) TotalCost == round(WasteLength × α + WeldCount × β),
    ///   (g) StockUsed == CuttingPlans.Count,
    ///   (h) MaterialEfficiency is in [0, 100].
    /// </summary>
    [TestFixture]
    public class InvariantTests1D
    {
        private const int Seeds = 15;
        private static readonly string[] SolverNames = { "Greedy", "CG", "ArcFlow" };

        private static IEnumerable<TestCaseData> AllSolversAllSeeds()
        {
            for (int i = 0; i < 3; i++)
                for (int s = 0; s < Seeds; s++)
                    yield return new TestCaseData(i, s).SetName($"{SolverNames[i]}_seed{s}");
        }

        [TestCaseSource(nameof(AllSolversAllSeeds))]
        public void RandomFeasibleInstance_AllInvariantsHold(int solverIdx, int seed)
        {
            var (stocks, orders, options) = Generate(seed);
            var solver = MakeSolver(solverIdx);

            var r = solver.Solve(stocks, orders, options);

            r.Success.Should().BeTrue("{0} must succeed on seed {1}", solver.Name, seed);

            // (a) exact demand coverage
            var demand = orders.GroupBy(o => o.Length)
                               .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));
            var produced = new Dictionary<int, int>();
            foreach (var p in r.CuttingPlans)
                foreach (var c in p.Cuts)
                {
                    produced.TryGetValue(c.Length, out var cur);
                    produced[c.Length] = cur + 1;
                }
            foreach (var kv in demand)
            {
                produced.TryGetValue(kv.Key, out var prod);
                prod.Should().Be(kv.Value,
                    "{0}: must produce exactly demand for length {1}", solver.Name, kv.Key);
            }
            // No fabricated lengths (every cut traces to a real order).
            foreach (var len in produced.Keys)
                demand.ContainsKey(len).Should().BeTrue(
                    "{0}: produced length {1} not in demand", solver.Name, len);

            // (b) stock-length respect (kerf-aware) AND (c) Leftover consistency
            foreach (var p in r.CuttingPlans)
            {
                int consumed = p.Cuts.Sum(c => c.Length)
                             + Math.Max(0, p.Cuts.Count - 1) * options.Kerf;
                consumed.Should().BeLessThanOrEqualTo(p.StockLength,
                    "{0}: consumed ({1}) must not exceed stock ({2})",
                    solver.Name, consumed, p.StockLength);

                int expectedLeftover = p.StockLength - consumed;
                expectedLeftover.Should().BeGreaterThanOrEqualTo(0);
                p.Leftover.Should().Be(expectedLeftover,
                    "{0}: leftover formula mismatch on plan with stock={1}",
                    solver.Name, p.StockLength);
            }

            // (d) stock inventory not exceeded
            var stockUsage = r.CuttingPlans.GroupBy(p => p.StockLength)
                                            .ToDictionary(g => g.Key, g => g.Count());
            foreach (var s in stocks)
            {
                stockUsage.TryGetValue(s.Length, out var used);
                used.Should().BeLessThanOrEqualTo(s.Quantity,
                    "{0}: stock {1}mm usage ({2}) must not exceed inventory ({3})",
                    solver.Name, s.Length, used, s.Quantity);
            }

            // (e) waste is sum of leftovers < gamma
            long expectedWaste = r.CuttingPlans
                .Where(p => p.Leftover < options.Gamma)
                .Sum(p => (long)p.Leftover);
            ((long)r.WasteLength).Should().Be(expectedWaste,
                "{0}: reported waste must match leftovers below gamma", solver.Name);

            // (f) cost formula
            long expectedCost = (long)Math.Round(
                r.WasteLength * (double)options.Alpha + r.WeldCount * (double)options.Beta);
            r.TotalCost.Should().Be(expectedCost,
                "{0}: cost formula mismatch", solver.Name);

            // (g) StockUsed
            r.StockUsed.Should().Be(r.CuttingPlans.Count);

            // (h) efficiency range
            r.MaterialEfficiency.Should().BeInRange(0, 100.001);
        }

        // ─── Generator ─────────────────────────────────────────────

        /// <summary>
        /// MIP-friendly instance. Kerf locked to 0 so the Arc Flow GCD stays
        /// large; small palette so demand vector is dense; generous stock.
        /// </summary>
        private static (List<RebarStock> stocks, List<Order> orders, SolverOptions options) Generate(int seed)
        {
            var rng = new Random(seed * 7919 + 31);

            int[] palette = { 1000, 1500, 2000, 3000, 4000, 6000 };  // 6 lengths
            int items = 10 + rng.Next(0, 16);                         // 10..25 items
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < items; i++)
            {
                int len = palette[rng.Next(palette.Length)];
                counts.TryGetValue(len, out var c);
                counts[len] = c + 1;
            }
            var orders = counts.Select(kv => new Order(kv.Key, kv.Value)).ToList();

            var stocks = new List<RebarStock> { new(12000, items * 2 + 10) };

            int gamma = 50 + rng.Next(0, 200);
            float alpha = (float)(0.5 + rng.NextDouble() * 1.5);
            float beta = 100 + (float)(rng.NextDouble() * 1000);

            var options = new SolverOptions
            {
                Alpha = alpha,
                Beta = beta,
                Gamma = gamma,
                Delta = 100,
                Kerf = 0,                       // see note above
                UsageOrder = rng.Next(0, 2) == 0
                    ? StockUsageOrder.SmallToLarge
                    : StockUsageOrder.LargeToSmall,
                EnableWelding = false,
            };

            return (stocks, orders, options);
        }

        private static ICuttingSolver MakeSolver(int idx) => idx switch
        {
            0 => new GreedyKnapsackSolver(),
            1 => new ColumnGenerationSolver(),
            2 => new ArcFlowSolver(),
            _ => new GreedyKnapsackSolver(),
        };
    }
}
