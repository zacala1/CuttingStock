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
    /// Cross-solver smoke tests. They don't prove optimality — Arc Flow runs
    /// with a 30 s wall-clock limit and may return a FEASIBLE-but-not-OPTIMAL
    /// answer that's worse than Greedy on hard instances — but they catch
    /// regressions where a solver suddenly produces wildly invalid output
    /// (NaN cost, 0% efficiency, negative bars used, etc.).
    ///
    /// What we lock down:
    ///   1. All three solvers succeed on the same well-shaped input.
    ///   2. Every solver reports a finite, non-negative cost and an efficiency
    ///      in [0, 100].
    ///   3. No solver opens more than 3× the bar count that Greedy finds —
    ///      that's a generous bound that still catches pathological output
    ///      (e.g. a solver that puts every cut in its own bar).
    ///   4. On a simple instance, every solver reaches ≥70% efficiency.
    ///      That's the threshold below which "the algorithm is broken" rather
    ///      than "this input is hard". 70% leaves room for adversarial seeds.
    /// </summary>
    [TestFixture]
    [Category("Quality")]
    public class QualityComparisonTests
    {
        private static IEnumerable<TestCaseData> Scenarios() => new[]
        {
            new TestCaseData(11, "small palette, no kerf").SetName("scenario_smallNoKerf"),
            new TestCaseData(33, "wide palette, no kerf").SetName("scenario_widePaletteNoKerf"),
        };

        [TestCaseSource(nameof(Scenarios))]
        public void AllSolvers_ProduceFiniteValidOutput(int seed, string label)
        {
            var (stocks, orders, options) = Generate(seed);

            foreach (var solver in MakeSolvers())
            {
                var r = solver.Solve(stocks, Clone(orders), options);
                r.Success.Should().BeTrue("{0} should succeed on {1}", solver.Name, label);
                r.TotalCost.Should().BeGreaterThanOrEqualTo(0, "{0}: cost must be non-negative", solver.Name);
                r.StockUsed.Should().BeGreaterThan(0, "{0}: must use at least one bar", solver.Name);
                double.IsFinite(r.MaterialEfficiency).Should().BeTrue("{0}: efficiency NaN/∞", solver.Name);
                r.MaterialEfficiency.Should().BeInRange(0, 100.001, "{0}: efficiency out of range", solver.Name);
            }
        }

        [TestCaseSource(nameof(Scenarios))]
        public void NoSolver_OpensMoreThan3xGreedyBars(int seed, string label)
        {
            var (stocks, orders, options) = Generate(seed);
            var greedy = new GreedyKnapsackSolver().Solve(stocks, Clone(orders), options);
            greedy.Success.Should().BeTrue();
            int bound = greedy.StockUsed * 3;

            foreach (var solver in MakeSolvers())
            {
                var r = solver.Solve(stocks, Clone(orders), options);
                r.Success.Should().BeTrue("{0}: should succeed on {1}", solver.Name, label);
                r.StockUsed.Should().BeLessThanOrEqualTo(bound,
                    "{0}: {1} bars vs Greedy {2} — should not be > 3× ({3})",
                    solver.Name, r.StockUsed, greedy.StockUsed, bound);
            }
        }

        [Test]
        public void AllSolvers_OnTrivialInstance_AtLeast70PercentEfficient()
        {
            // Trivial: 5000mm orders into 10000mm stock — at most 2 cuts per bar
            // with 0 waste. Every solver should clear this trivially.
            var stocks = new List<RebarStock> { new(10000, 20) };
            var orders = new List<Order> { new(5000, 30) };
            var options = new SolverOptions();

            foreach (var solver in MakeSolvers())
            {
                var r = solver.Solve(stocks, Clone(orders), options);
                r.Success.Should().BeTrue("{0} should solve trivial instance", solver.Name);
                r.MaterialEfficiency.Should().BeGreaterThanOrEqualTo(70,
                    "{0}: trivial instance efficiency {1:F1}% should be ≥70%",
                    solver.Name, r.MaterialEfficiency);
            }
        }

        // ─── Generators ───────────────────────────────────────────────

        /// <summary>Standard construction-style scenario.</summary>
        private static (List<RebarStock>, List<Order>, SolverOptions) Generate(int seed)
        {
            var rng = new Random(seed);
            int[] palette = { 1500, 2000, 2500, 3000, 4000, 5000, 6000 };
            var stocks = new List<RebarStock> { new(12000, 200) };

            var counts = new Dictionary<int, int>();
            int items = 30 + rng.Next(0, 20);
            for (int i = 0; i < items; i++)
            {
                int len = palette[rng.Next(palette.Length)];
                counts.TryGetValue(len, out var c);
                counts[len] = c + 1;
            }
            var orders = counts.Select(kv => new Order(kv.Key, kv.Value)).ToList();

            var options = new SolverOptions
            {
                Alpha = 1f, Beta = 500f, Gamma = 100, Delta = 100,
                Kerf = 0,
                UsageOrder = StockUsageOrder.LargeToSmall,
                EnableWelding = false,
            };
            return (stocks, orders, options);
        }

        private static IEnumerable<ICuttingSolver> MakeSolvers()
        {
            yield return new GreedyKnapsackSolver();
            yield return new ColumnGenerationSolver();
            yield return new ArcFlowSolver();
        }

        private static List<Order> Clone(List<Order> src) =>
            src.Select(o => new Order(o.Length, o.Quantity)).ToList();
    }
}
