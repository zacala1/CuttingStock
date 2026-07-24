using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.TwoD
{
    [TestFixture]
    [Category("Architecture")]
    public class SolverContractTests2D
    {
        private static IEnumerable<TestCaseData> CatalogSolvers()
        {
            return SolverCatalog2D.All.Select(d =>
                new TestCaseData(d).SetName($"{d.Key}_duplicate_sheet_dims_contract"));
        }

        private static IEnumerable<TestCaseData> TimedCatalogSolvers()
        {
            return SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.TimeLimit))
                .Select(d => new TestCaseData(d).SetName($"{d.Key}_absolute_deadline_contract"));
        }

        [TestCaseSource(nameof(CatalogSolvers))]
        public void CatalogSolver_DuplicateSheetDims_UsesAggregatedInventory(SolverDescriptor2D descriptor)
        {
            var sheets = new List<Sheet>
            {
                new(1000, 1000, 1),
                new(1000, 1000, 1),
            };
            var orders = new List<RectOrder>
            {
                new(600, 600, 2, allowRotation: false),
            };
            var options = new SolverOptions2D
            {
                AllowRotation = false,
                Stage = 2,
                TimeLimitMs = 6000,
            };

            descriptor.GetUnsupportedReason(options).Should().BeNull();

            var result = descriptor.CreateSolver().Solve(
                CloneSheets(sheets),
                CloneOrders(orders),
                options);

            result.Success.Should().BeTrue(
                "{0} should aggregate duplicate-dimension sheet inventory. Error: {1}",
                descriptor.Key,
                result.ErrorMessage);
            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, options, result)
                .Should().BeNull("{0} should satisfy the shared 2D result validator", descriptor.Key);

            CountPlaced(result, orderIndex: 0).Should().Be(2);
            result.SheetsUsed.Should().BeLessThanOrEqualTo(2);
            result.TotalCost.Should().Be((long)Math.Round(result.TotalWasteArea * (double)options.AlphaArea));
            result.MaterialEfficiency.Should().BeInRange(0d, 100d);
        }

        [Test]
        public void CatalogDescriptors_StagePolicy_DistinguishesAdvisoryFromEnforced()
        {
            SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.EnforcedStage))
                .Select(d => d.Key)
                .Should().Equal("two-stage-shelf-guillotine");
            SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.AdvisoryStage))
                .Select(d => d.Key)
                .Should().Equal("shelf-guillotine", "column-generation-2d", "staged-mip-guillotine");
            SolverCatalog2D.All.Should().OnlyContain(
                d => !(d.Supports(SolverCapability.AdvisoryStage) &&
                       d.Supports(SolverCapability.EnforcedStage)));

            var requestedThreeStage = new SolverOptions2D { Stage = 3 };
            foreach (var descriptor in SolverCatalog2D.All)
            {
                if (descriptor.Supports(SolverCapability.EnforcedStage))
                {
                    descriptor.GetUnsupportedReason(requestedThreeStage)
                        .Should().Be("3-stage 옵션을 지원하지 않습니다.");
                    descriptor.SupportedStages.Should().Equal(2);
                }
                else
                {
                    descriptor.GetUnsupportedReason(requestedThreeStage).Should().BeNull();
                    descriptor.SupportedStages.Should().Contain(3);
                }
            }
        }

        [Test]
        public void CatalogDescriptors_TimeLimitCapability_IsLimitedToCgAndMipSolvers()
        {
            var timedDescriptors = SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.TimeLimit))
                .ToList();

            timedDescriptors.Select(d => d.Key)
                .Should().Equal("column-generation-2d", "staged-mip-guillotine");
            timedDescriptors.Select(d => d.CreateSolver())
                .Should().AllSatisfy(solver =>
                    solver.Should().BeAssignableTo<ICuttingSolver2D>());
            timedDescriptors.Select(d => d.CreateSolver().GetType())
                .Should().Equal(typeof(ColumnGeneration2DSolver), typeof(StagedMipGuillotineSolver));

            SolverCatalog2D.All
                .Where(d => !d.Supports(SolverCapability.TimeLimit))
                .Select(d => d.Key)
                .Should().Equal("shelf-guillotine", "two-stage-shelf-guillotine");
        }

        [TestCaseSource(nameof(TimedCatalogSolvers))]
        public void CatalogSolver_TinyAbsoluteDeadline_DoesNotReceiveFreeSecondPhaseBudget(
            SolverDescriptor2D descriptor)
        {
            var sheets = new List<Sheet> { new(2440, 1220, 10) };
            var orders = new List<RectOrder>
            {
                new(500, 300, 8),
                new(400, 250, 10),
                new(300, 200, 20),
            };
            var options = new SolverOptions2D
            {
                TimeLimitMs = 1,
                Kerf = 3,
                Trim = 5,
            };

            var result = descriptor.CreateSolver().Solve(
                CloneSheets(sheets),
                CloneOrders(orders),
                options);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.ExecutionTimeMs.Should().BeLessThan(
                1000,
                "{0} must not grant a new one-second budget after its warm start consumes the absolute deadline",
                descriptor.Key);
            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, options, result)
                .Should().BeNull();
        }

        private static int CountPlaced(SolverResult2D result, int orderIndex)
        {
            return result.Patterns.Sum(pattern =>
                pattern.Placements.Count(p => p.OrderIndex == orderIndex) * pattern.Multiplicity);
        }

        private static List<Sheet> CloneSheets(IEnumerable<Sheet> sheets)
        {
            return sheets.Select(s => new Sheet(s.Width, s.Height, s.Quantity)).ToList();
        }

        private static List<RectOrder> CloneOrders(IEnumerable<RectOrder> orders)
        {
            return orders
                .Select(o => new RectOrder(o.Width, o.Height, o.Quantity, o.AllowRotation))
                .ToList();
        }
    }
}
