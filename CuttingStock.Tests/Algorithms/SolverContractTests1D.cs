using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.Algorithms
{
    [TestFixture]
    [Category("Architecture")]
    public class SolverContractTests1D
    {
        private static IEnumerable<TestCaseData> CatalogSolvers()
        {
            return SolverCatalog.All.Select(d =>
                new TestCaseData(d).SetName($"{d.Key}_common_contracts"));
        }

        private static IEnumerable<TestCaseData> WeldingCapableCatalogSolvers()
        {
            return SolverCatalog.All
                .Where(d => d.Supports(SolverCapability.Welding))
                .Select(d => new TestCaseData(d).SetName($"{d.Key}_welding_contract"));
        }

        [TestCaseSource(nameof(CatalogSolvers))]
        public void CatalogSolver_OnDeterministicFeasibleInput_SatisfiesCommonResultContract(SolverDescriptor descriptor)
        {
            var stock = new List<RebarStock>
            {
                new(12000, 2),
                new(9000, 2),
            };
            var orders = new List<Order>
            {
                new(6000, 1),
                new(4000, 2),
                new(2500, 2),
                new(1500, 1),
            };
            var options = new SolverOptions
            {
                Alpha = 1.25f,
                Beta = 450f,
                Gamma = 300,
                Delta = 500,
                Kerf = 5,
                UsageOrder = StockUsageOrder.SmallToLarge,
                EnableWelding = false,
            };

            descriptor.GetUnsupportedReason(options).Should().BeNull();

            var result = descriptor.CreateSolver().Solve(
                CloneStock(stock),
                CloneOrders(orders),
                options);

            AssertSuccessfulContract(descriptor, stock, orders, options, result);
            result.CuttingPlans.SelectMany(p => p.Cuts)
                .Should().OnlyContain(c => !c.WeldGroupId.HasValue && !c.RequiresWelding);
        }

        [TestCaseSource(nameof(CatalogSolvers))]
        public void Descriptor_WhenWeldingRequested_ReportsSupportAccurately(SolverDescriptor descriptor)
        {
            var options = new SolverOptions { EnableWelding = true };

            var unsupportedReason = descriptor.GetUnsupportedReason(options);

            if (descriptor.Supports(SolverCapability.Welding))
            {
                unsupportedReason.Should().BeNull();
            }
            else
            {
                unsupportedReason.Should().NotBeNull();
                unsupportedReason.Should().Contain("용접");
            }
        }

        [TestCaseSource(nameof(WeldingCapableCatalogSolvers))]
        public void WeldingCapableCatalogSolver_OnLongOrder_SatisfiesStructuralWeldContract(SolverDescriptor descriptor)
        {
            var stock = new List<RebarStock> { new(6000, 10) };
            var orders = new List<Order> { new(15000, 1) };
            var options = new SolverOptions
            {
                Alpha = 1f,
                Beta = 500f,
                Gamma = 100,
                Delta = 1000,
                Kerf = 0,
                EnableWelding = true,
            };

            var result = descriptor.CreateSolver().Solve(
                CloneStock(stock),
                CloneOrders(orders),
                options);

            AssertSuccessfulContract(descriptor, stock, orders, options, result);

            var weldedGroups = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .ToList();

            weldedGroups.Should().NotBeEmpty();
            weldedGroups.Should().OnlyContain(g => g.Count() >= 2);
            weldedGroups.Should().OnlyContain(g => g.Sum(c => c.Length) == 15000);
            result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.RequiresWelding)
                .Should().OnlyContain(c => c.WeldGroupId.HasValue);
            result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .Should().OnlyContain(c => c.RequiresWelding && c.Length >= options.Delta);
        }

        [Test]
        public void InvalidSuccessfulResult_WithWrongLeftover_ReturnsClearContractError()
        {
            var stock = new List<RebarStock> { new(12000, 1) };
            var orders = new List<Order> { new(4000, 2) };
            var options = new SolverOptions
            {
                Gamma = 100,
                Kerf = 5,
            };
            var result = new SolverResult
            {
                CuttingPlans =
                [
                    new CuttingPlan
                    {
                        StockLength = 12000,
                        Cuts =
                        [
                            new Cut { Length = 4000 },
                            new Cut { Length = 4000 },
                        ],
                        Leftover = 4000,
                    },
                ],
            };

            SolverUtils.ValidateSuccessfulResult(stock, orders, options, result)
                .Should().Contain("leftover is 4000mm, expected 3995mm");
        }

        private static void AssertSuccessfulContract(
            SolverDescriptor descriptor,
            List<RebarStock> stock,
            List<Order> orders,
            SolverOptions options,
            SolverResult result)
        {
            result.Success.Should().BeTrue(
                "{0} should solve the deterministic architecture fixture. Error: {1}",
                descriptor.Key,
                result.ErrorMessage);
            result.ErrorMessage.Should().BeNullOrEmpty();

            SolverUtils.ValidateSuccessfulResult(stock, orders, options, result)
                .Should().BeNull("{0} should satisfy the shared 1D result validator", descriptor.Key);

            var stockLengths = stock.Select(s => s.Length).ToHashSet();
            result.CuttingPlans.Should().NotBeEmpty();
            result.CuttingPlans.Should().OnlyContain(p => stockLengths.Contains(p.StockLength));

            foreach (var plan in result.CuttingPlans)
            {
                plan.Leftover.Should().Be(
                    SolverUtils.ComputeLeftover(plan.StockLength, plan.Cuts, options.Kerf),
                    "{0} must use the canonical kerf-aware leftover formula",
                    descriptor.Key);
                plan.Leftover.Should().BeGreaterThanOrEqualTo(0);
            }

            var expectedReusable = result.CuttingPlans
                .Where(p => p.Leftover >= options.Gamma)
                .Select(p => p.Leftover)
                .OrderBy(x => x);
            result.ReusableLeftovers.OrderBy(x => x).Should().Equal(expectedReusable);

            var expectedWaste = result.CuttingPlans
                .Where(p => p.Leftover < options.Gamma)
                .Sum(p => (long)p.Leftover);
            result.WasteLength.Should().Be(expectedWaste);

            var expectedCost = (long)Math.Round(
                result.WasteLength * (double)options.Alpha +
                result.WeldCount * (double)options.Beta);
            result.TotalCost.Should().Be(expectedCost);
            result.StockUsed.Should().Be(result.CuttingPlans.Count);
            result.MaterialEfficiency.Should().BeInRange(0d, 100d);
        }

        private static List<RebarStock> CloneStock(IEnumerable<RebarStock> stock)
        {
            return stock.Select(s => new RebarStock(s.Length, s.Quantity)).ToList();
        }

        private static List<Order> CloneOrders(IEnumerable<Order> orders)
        {
            return orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
        }
    }
}
