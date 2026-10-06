using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Regression tests for the integer-master column-generation variants.
    ///
    /// The integer master used equality demand rows over the generated column pool and
    /// was accepted whenever it was feasible. On pools without an exact cover it fell back
    /// on single-cut "identity" columns, so one instance with a 6-bar lower bound produced
    /// 26 bars (the LP-rounded baseline needed 9). These tests pin the invariant that
    /// an integer-master variant never consumes more stock than the baseline CG solution.
    /// </summary>
    [TestFixture]
    [Category("Quality")]
    public class IntegerMasterQualityTests
    {
        private const string Baseline = "column-generation";
        private const string IntegerMaster = "column-generation-integer-master";
        private const string GlobalStock = "global-stock-column-generation";

        private static readonly int[] Palette = { 900, 1200, 1500, 1800, 2400, 3000, 3600, 4500, 5400 };

        private static SolverResult Solve(string key, List<RebarStock> stock, List<Order> orders, SolverOptions options)
        {
            var descriptor = SolverCatalog.All.First(d => d.Key == key);
            return descriptor.CreateSolver().Solve(
                stock.Select(s => new RebarStock(s.Length, s.Quantity)).ToList(),
                orders.Select(o => new Order(o.Length, o.Quantity)).ToList(),
                options);
        }

        private static long Material(SolverResult result) =>
            result.CuttingPlans.Where(p => !p.UsesReusableLeftover).Sum(p => (long)p.StockLength);

        private static List<Order> RandomOrders(Random rng)
        {
            int items = 8 + rng.Next(25);
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < items; i++)
            {
                int length = Palette[rng.Next(Palette.Length)];
                counts[length] = counts.GetValueOrDefault(length) + 1;
            }
            return counts.Select(kv => new Order(kv.Key, kv.Value)).ToList();
        }

        private static IEnumerable<TestCaseData> PathologicalInstanceVariants()
        {
            yield return new TestCaseData(IntegerMaster).SetName("IntegerMaster_pathological_instance");
            yield return new TestCaseData(GlobalStock).SetName("GlobalStock_pathological_instance");
        }

        [TestCaseSource(nameof(PathologicalInstanceVariants))]
        public void PathologicalInstance_IsNotWorseThanBaseline(string key)
        {
            // Total ordered length 71400 -> lower bound 6 bars on 12000mm stock.
            // Before the fix: integer-master = 26 bars, global-stock = 26 bars, baseline CG = 9.
            var stock = new List<RebarStock> { new(12000, 80) };
            var orders = new List<Order>
            {
                new(5400, 3), new(4500, 2), new(3600, 1), new(3000, 3), new(2400, 4),
                new(1800, 3), new(1500, 4), new(1200, 6), new(900, 6),
            };
            var options = new SolverOptions { Kerf = 0, Gamma = 400 };

            var baseline = Solve(Baseline, stock, orders, options);
            var result = Solve(key, stock, orders, options);

            baseline.Success.Should().BeTrue(baseline.ErrorMessage);
            result.Success.Should().BeTrue(result.ErrorMessage);
            result.StockUsed.Should().BeLessThanOrEqualTo(baseline.StockUsed,
                "{0} must never be worse than the baseline CG solution", key);
            result.StockUsed.Should().BeLessThanOrEqualTo(10,
                "the equality-master pathology used 26 single-cut bars");
        }

        private static IEnumerable<TestCaseData> RandomSingleStockSeeds()
        {
            foreach (var key in new[] { IntegerMaster, GlobalStock })
                for (int seed = 0; seed < 25; seed++)
                    yield return new TestCaseData(key, seed).SetName($"{key}_single_stock_seed{seed}");
        }

        [TestCaseSource(nameof(RandomSingleStockSeeds))]
        public void RandomSingleStock_NeverUsesMoreMaterialThanBaseline(string key, int seed)
        {
            var rng = new Random(seed * 7919 + 5);
            var stock = new List<RebarStock> { new(12000, 80) };
            var orders = RandomOrders(rng);
            var options = new SolverOptions { Kerf = seed % 4 == 0 ? 3 : 0, Gamma = 400 };

            var baseline = Solve(Baseline, stock, orders, options);
            var result = Solve(key, stock, orders, options);

            baseline.Success.Should().BeTrue(baseline.ErrorMessage);
            result.Success.Should().BeTrue(result.ErrorMessage);
            Material(result).Should().BeLessThanOrEqualTo(Material(baseline),
                "{0} (seed {1}) must not consume more stock than the baseline CG", key, seed);
        }

        private static IEnumerable<TestCaseData> RandomMultiStockSeeds()
        {
            foreach (var key in new[] { IntegerMaster, GlobalStock })
                for (int seed = 0; seed < 20; seed++)
                    yield return new TestCaseData(key, seed).SetName($"{key}_multi_stock_seed{seed}");
        }

        [TestCaseSource(nameof(RandomMultiStockSeeds))]
        public void RandomMultiStock_NeverUsesMoreMaterialThanBaseline(string key, int seed)
        {
            var rng = new Random(seed * 104729 + 17);
            var stock = new List<RebarStock> { new(6000, 40), new(9000, 40), new(12000, 40) };
            var orders = RandomOrders(rng);
            var options = new SolverOptions
            {
                Kerf = 0,
                Gamma = 400,
                UsageOrder = seed % 2 == 0 ? StockUsageOrder.SmallToLarge : StockUsageOrder.LargeToSmall,
            };

            var baseline = Solve(Baseline, stock, orders, options);
            var result = Solve(key, stock, orders, options);

            baseline.Success.Should().BeTrue(baseline.ErrorMessage);
            result.Success.Should().BeTrue(result.ErrorMessage);
            Material(result).Should().BeLessThanOrEqualTo(Material(baseline),
                "{0} (seed {1}) must not consume more stock than the baseline CG", key, seed);
        }
    }
}
