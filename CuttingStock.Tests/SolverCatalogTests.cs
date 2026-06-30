using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests
{
    [TestFixture]
    public class SolverCatalogTests
    {
        [Test]
        public void OneDimensionalCatalog_ShouldExposeStableSolverOrder()
        {
            SolverCatalog.All.Select(d => d.Key).Should().Equal(
                "greedy-knapsack",
                "column-generation",
                "column-generation-stabilized",
                "column-generation-multicolumn",
                "column-generation-integer-master",
                "global-stock-column-generation",
                "arc-flow");
        }

        [Test]
        public void OneDimensionalCatalog_ShouldMarkOnlyGreedyAsWeldingCapable()
        {
            var weldingSolvers = SolverCatalog.All
                .Where(d => d.Supports(SolverCapability.Welding))
                .Select(d => d.Key);

            weldingSolvers.Should().Equal("greedy-knapsack");
        }

        [Test]
        public void OneDimensionalCatalog_ShouldExposeCommonDescriptorContract()
        {
            SolverCatalog.All.Should().OnlyContain(
                d => d is ISolverDescriptor<ICuttingSolver, SolverOptions>);
        }

        [Test]
        public void OneDimensionalDescriptor_ShouldRejectUnsupportedWeldingOption()
        {
            var options = new SolverOptions { EnableWelding = true };

            SolverCatalog.GetByIndex(1).GetUnsupportedReason(options)
                .Should().Be("용접 옵션을 지원하지 않습니다.");
        }

        [Test]
        public void OneDimensionalCatalog_ShouldCreateStabilizedColumnGenerationSolver()
        {
            var descriptor = SolverCatalog.All.Single(d => d.Key == "column-generation-stabilized");

            descriptor.CreateSolver().Should().BeOfType<StabilizedColumnGenerationSolver>();
        }

        [Test]
        public void OneDimensionalCatalog_ShouldCreateMultiColumnGenerationSolver()
        {
            var descriptor = SolverCatalog.All.Single(d => d.Key == "column-generation-multicolumn");

            descriptor.CreateSolver().Should().BeOfType<MultiColumnGenerationSolver>();
        }

        [Test]
        public void OneDimensionalCatalog_ShouldCreateIntegerMasterColumnGenerationSolver()
        {
            var descriptor = SolverCatalog.All.Single(d => d.Key == "column-generation-integer-master");

            descriptor.CreateSolver().Should().BeOfType<IntegerMasterColumnGenerationSolver>();
        }

        [Test]
        public void OneDimensionalCatalog_ShouldCreateGlobalStockColumnGenerationSolver()
        {
            var descriptor = SolverCatalog.All.Single(d => d.Key == "global-stock-column-generation");

            descriptor.CreateSolver().Should().BeOfType<GlobalStockColumnGenerationSolver>();
        }

        [Test]
        public void TwoDimensionalCatalog_ShouldExposeStableSolverOrder()
        {
            SolverCatalog2D.All.Select(d => d.Key).Should().Equal(
                "shelf-guillotine",
                "two-stage-shelf-guillotine",
                "column-generation-2d",
                "staged-mip-guillotine");
        }

        [Test]
        public void TwoDimensionalCatalog_ShouldOnlyClaimStageEnforcementForTwoStageShelfSolver()
        {
            var enforced = SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.EnforcedStage))
                .Select(d => d.Key);

            enforced.Should().Equal("two-stage-shelf-guillotine");
        }

        [Test]
        public void TwoDimensionalCatalog_ShouldLimitTimeLimitCapabilityToCgAndMip()
        {
            var timedSolvers = SolverCatalog2D.All
                .Where(d => d.Supports(SolverCapability.TimeLimit))
                .Select(d => d.Key);

            timedSolvers.Should().Equal("column-generation-2d", "staged-mip-guillotine");
        }

        [Test]
        public void TwoDimensionalCatalog_ShouldExposeCommonDescriptorContract()
        {
            SolverCatalog2D.All.Should().OnlyContain(
                d => d is ISolverDescriptor<ICuttingSolver2D, SolverOptions2D>);
        }

        [Test]
        public void TwoDimensionalDescriptor_ShouldRejectUnsupportedEnforcedStage()
        {
            var descriptor = SolverCatalog2D.All.Single(d => d.Key == "two-stage-shelf-guillotine");
            var options = new SolverOptions2D { Stage = 3 };

            descriptor.GetUnsupportedReason(options).Should().Be("3-stage 옵션을 지원하지 않습니다.");
        }
    }
}
