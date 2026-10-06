using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// The catalog advertises <see cref="SolverCapability.StockUsageOrder"/> to the UI, which
    /// keeps the "재고 사용 순서" selector enabled. A solver may claim it only when the option
    /// really changes which stock is consumed, and must not silently depend on it otherwise.
    /// Joint (global-pool) optimizers choose stock by material/area, so they do not claim it.
    /// </summary>
    [TestFixture]
    [Category("Architecture")]
    public class StockUsageOrderContractTests
    {
        // ----- 1D: one 5000mm order fits both stock lengths; only the usage order can decide. -----

        private static IEnumerable<TestCaseData> Catalog1D() =>
            SolverCatalog.All.Select(d => new TestCaseData(d).SetName($"{d.Key}_usage_order_contract"));

        private static List<int> UsedStockLengths(SolverDescriptor descriptor, StockUsageOrder order)
        {
            var result = descriptor.CreateSolver().Solve(
                new List<RebarStock> { new(6000, 5), new(12000, 5) },
                new List<Order> { new(5000, 1) },
                new SolverOptions { Kerf = 0, UsageOrder = order });

            result.Success.Should().BeTrue("{0}: {1}", descriptor.Key, result.ErrorMessage);
            return result.CuttingPlans.Select(p => p.StockLength).OrderBy(l => l).ToList();
        }

        [TestCaseSource(nameof(Catalog1D))]
        public void Catalog1D_UsageOrderCapability_MatchesBehavior(SolverDescriptor descriptor)
        {
            var smallFirst = UsedStockLengths(descriptor, StockUsageOrder.SmallToLarge);
            var largeFirst = UsedStockLengths(descriptor, StockUsageOrder.LargeToSmall);

            if (descriptor.Supports(SolverCapability.StockUsageOrder))
            {
                smallFirst.Should().Equal(new[] { 6000 },
                    "{0} claims StockUsageOrder, so SmallToLarge must consume the 6000mm stock", descriptor.Key);
                largeFirst.Should().Equal(new[] { 12000 },
                    "{0} claims StockUsageOrder, so LargeToSmall must consume the 12000mm stock", descriptor.Key);
            }
            else
            {
                largeFirst.Should().Equal(smallFirst,
                    "{0} does not claim StockUsageOrder, so the option must not change its result; " +
                    "declare the capability if the solver now honors it", descriptor.Key);
            }
        }

        [Test]
        public void Catalog1D_JointStockOptimizers_DoNotClaimUsageOrder()
        {
            SolverCatalog.All
                .Where(d => !d.Supports(SolverCapability.StockUsageOrder))
                .Select(d => d.Key)
                .Should().BeEquivalentTo("global-stock-column-generation", "arc-flow");
        }

        // ----- 2D: one 900x900 order fits both sheet sizes. -----

        private static IEnumerable<TestCaseData> Catalog2D() =>
            SolverCatalog2D.All.Select(d => new TestCaseData(d).SetName($"{d.Key}_usage_order_contract"));

        private static List<(int Width, int Height)> UsedSheets(SolverDescriptor2D descriptor, StockUsageOrder order)
        {
            var result = descriptor.CreateSolver().Solve(
                new List<Sheet> { new(1000, 1000, 5), new(2000, 2000, 5) },
                new List<RectOrder> { new(900, 900, 1, allowRotation: false) },
                new SolverOptions2D
                {
                    AllowRotation = false,
                    Stage = 2,
                    TimeLimitMs = 6000,
                    UsageOrder = order,
                });

            result.Success.Should().BeTrue("{0}: {1}", descriptor.Key, result.ErrorMessage);
            return result.Patterns
                .Select(p => (p.Sheet.Width, p.Sheet.Height))
                .OrderBy(s => s.Width)
                .ToList();
        }

        [TestCaseSource(nameof(Catalog2D))]
        public void Catalog2D_UsageOrderCapability_MatchesBehavior(SolverDescriptor2D descriptor)
        {
            var smallFirst = UsedSheets(descriptor, StockUsageOrder.SmallToLarge);
            var largeFirst = UsedSheets(descriptor, StockUsageOrder.LargeToSmall);

            if (descriptor.Supports(SolverCapability.StockUsageOrder))
            {
                smallFirst.Should().Equal(new[] { (1000, 1000) },
                    "{0} claims StockUsageOrder, so SmallToLarge must consume the 1000x1000 sheet", descriptor.Key);
                largeFirst.Should().Equal(new[] { (2000, 2000) },
                    "{0} claims StockUsageOrder, so LargeToSmall must consume the 2000x2000 sheet", descriptor.Key);
            }
            else
            {
                largeFirst.Should().Equal(smallFirst,
                    "{0} does not claim StockUsageOrder, so the option must not change its result; " +
                    "declare the capability if the solver now honors it", descriptor.Key);
            }
        }

        [Test]
        public void Catalog2D_JointPoolOptimizers_DoNotClaimUsageOrder()
        {
            SolverCatalog2D.All
                .Where(d => !d.Supports(SolverCapability.StockUsageOrder))
                .Select(d => d.Key)
                .Should().BeEquivalentTo("column-generation-2d", "staged-mip-guillotine");
        }
    }
}
