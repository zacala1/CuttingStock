using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Arc Flow used to throw when SCIP found no incumbent inside its 30 s limit (reported as a
    /// failed solve) and returned time-limited incumbents as if they were proven optimal, even
    /// when they were far worse than a heuristic (kerf 3: 11 bars vs 6 for greedy). The solver
    /// now falls back to the best heuristic and labels time-limited answers. The MIP time limit
    /// is shortened through the internal constructor so these tests stay fast.
    /// </summary>
    [TestFixture]
    [Category("Quality")]
    public class ArcFlowFallbackTests
    {
        private const string PlainName = "Arc Flow MIP (OR-Tools)";
        private static readonly int[] Palette = { 900, 1200, 1500, 1800, 2400, 3000, 3600, 4500, 5400 };

        private static List<Order> RandomOrders(int seed)
        {
            var rng = new Random(seed * 7919 + 5);
            int items = 8 + rng.Next(25);
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < items; i++)
            {
                int length = Palette[rng.Next(Palette.Length)];
                counts[length] = counts.GetValueOrDefault(length) + 1;
            }
            return counts.Select(kv => new Order(kv.Key, kv.Value)).ToList();
        }

        private static List<RebarStock> Single() => new() { new(12000, 80) };
        private static List<RebarStock> Mixed() => new() { new(6000, 40), new(9000, 40), new(12000, 40) };

        private static SolverResult Run(ICuttingSolver solver, List<RebarStock> stock, List<Order> orders, SolverOptions options) =>
            solver.Solve(
                stock.Select(s => new RebarStock(s.Length, s.Quantity)).ToList(),
                orders.Select(o => new Order(o.Length, o.Quantity)).ToList(),
                options);

        private static IEnumerable<TestCaseData> KerfInstances()
        {
            for (int seed = 0; seed < 4; seed++)
            {
                yield return new TestCaseData(seed, false).SetName($"kerf_single_stock_seed{seed}");
                yield return new TestCaseData(seed, true).SetName($"kerf_mixed_stock_seed{seed}");
            }
        }

        [TestCaseSource(nameof(KerfInstances))]
        public void TimeLimitedKerfInstance_SucceedsAndNeverBeatsHeuristicsInMaterial(int seed, bool mixed)
        {
            var stock = mixed ? Mixed() : Single();
            var orders = RandomOrders(seed);
            var options = new SolverOptions { Kerf = 3, Gamma = 400 };

            // 1 ms: SCIP cannot prove optimality on the kerf graph, and may find no incumbent at all.
            var result = Run(new ArcFlowSolver(mipTimeLimitMs: 1), stock, orders, options);

            TestContext.WriteLine($"{result.AlgorithmName} | stock={result.StockUsed} material={result.StockMaterial}");
            result.Success.Should().BeTrue(result.ErrorMessage);
            result.AlgorithmName.Should().StartWith(PlainName)
                .And.NotBe(PlainName, "an unproven result must say so instead of posing as optimal");

            long heuristicBest = new ICuttingSolver[] { new ColumnGenerationSolver(), new GreedyKnapsackSolver() }
                .Select(s => Run(s, stock, orders, options))
                .Where(r => r.Success)
                .Min(r => r.StockMaterial);
            result.StockMaterial.Should().BeLessThanOrEqualTo(heuristicBest,
                "a time-limited MIP incumbent must never be worse than the heuristic fallback");
        }

        [Test]
        public void ProvenOptimalSolve_KeepsPlainAlgorithmName()
        {
            var stock = Single();
            var orders = new List<Order> { new(3000, 4), new(1800, 3), new(1200, 6), new(900, 5) };

            var result = Run(new ArcFlowSolver(), stock, orders, new SolverOptions { Kerf = 0 });

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.AlgorithmName.Should().Be(PlainName);
        }

        [Test]
        public void InvalidMipTimeLimit_IsRejected()
        {
            var act = () => new ArcFlowSolver(mipTimeLimitMs: 0);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void StockMaterial_SumsOnlyStockPlans_NotReusableLeftoverPlans()
        {
            var result = new SolverResult();
            result.CuttingPlans.Add(new CuttingPlan { StockLength = 12000, Cuts = new List<Cut>() });
            result.CuttingPlans.Add(new CuttingPlan { StockLength = 6000, Cuts = new List<Cut>() });
            result.CuttingPlans.Add(new CuttingPlan
            {
                StockLength = 1500,
                Cuts = new List<Cut>(),
                ReusableLeftoverSourcePlanIndex = 0,
            });

            result.StockMaterial.Should().Be(18000);
            result.StockUsed.Should().Be(2);
        }
    }
}
