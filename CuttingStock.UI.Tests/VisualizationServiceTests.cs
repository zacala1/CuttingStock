using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.Tests
{
    /// <summary>
    /// Unit tests for <see cref="VisualizationService"/> — pure data builder
    /// turning a SolverResult into the bar-chart rows the View binds against.
    /// </summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class VisualizationServiceTests
    {
        [Test]
        public void Build_EmptyResult_ReturnsNoRowsButTwoLegendDefaults()
        {
            var result = new SolverResult();
            var build = VisualizationService.Build(result, gamma: 100);

            build.Rows.Should().BeEmpty();
            // Even with no cuts the legend appends the "재사용" / "낭비" defaults.
            build.Legend.Should().HaveCount(2);
            build.Legend[0].Label.Should().Contain("재사용");
            build.Legend[1].Label.Should().Contain("낭비");
        }

        [Test]
        public void Build_SinglePlan_HasOneRowAndOneCutLegend()
        {
            var result = new SolverResult
            {
                CuttingPlans =
                {
                    new CuttingPlan
                    {
                        StockLength = 12000,
                        Cuts = new List<Cut> { new() { Length = 5000 } },
                        Leftover = 7000,
                    },
                },
            };

            var build = VisualizationService.Build(result, gamma: 100);

            build.Rows.Should().HaveCount(1);
            build.Rows[0].Blocks.Should().HaveCount(2, "1 cut + 1 leftover block");
            build.Legend.Should().Contain(l => l.Label == "5000mm");
        }

        [Test]
        public void Build_IdenticalPatterns_GroupedAsOneRowWithCountLabel()
        {
            var plan = () => new CuttingPlan
            {
                StockLength = 12000,
                Cuts = new List<Cut> { new() { Length = 4000 }, new() { Length = 4000 } },
                Leftover = 4000,
            };
            var result = new SolverResult { CuttingPlans = { plan(), plan(), plan() } };

            var build = VisualizationService.Build(result, gamma: 100);

            build.Rows.Should().HaveCount(1, "three identical plans collapse to one row");
            build.Rows[0].InfoText.Should().Contain("x 3");
        }

        [Test]
        public void Build_LeftoverBelowGamma_ColorsAsWaste()
        {
            var result = new SolverResult
            {
                CuttingPlans =
                {
                    new CuttingPlan
                    {
                        StockLength = 12000,
                        Cuts = new List<Cut> { new() { Length = 11950 } },
                        Leftover = 50,  // < gamma=100, so this is waste
                    },
                },
            };

            var build = VisualizationService.Build(result, gamma: 100);

            // The leftover block is the last block; its ToolTip should label it 낭비.
            build.Rows[0].Blocks.Last().ToolTip.Should().Contain("낭비");
        }

        [Test]
        public void Build_LeftoverAboveGamma_ColorsAsReusable()
        {
            var result = new SolverResult
            {
                CuttingPlans =
                {
                    new CuttingPlan
                    {
                        StockLength = 12000,
                        Cuts = new List<Cut> { new() { Length = 8000 } },
                        Leftover = 4000,  // >= gamma=100
                    },
                },
            };

            var build = VisualizationService.Build(result, gamma: 100);

            build.Rows[0].Blocks.Last().ToolTip.Should().Contain("재사용");
        }

        [Test]
        public void Build_LegendSortedByLengthAscending()
        {
            var result = new SolverResult
            {
                CuttingPlans =
                {
                    new CuttingPlan { StockLength = 12000, Cuts = new List<Cut> { new() { Length = 7000 } }, Leftover = 5000 },
                    new CuttingPlan { StockLength = 12000, Cuts = new List<Cut> { new() { Length = 1000 } }, Leftover = 11000 },
                    new CuttingPlan { StockLength = 12000, Cuts = new List<Cut> { new() { Length = 3000 } }, Leftover = 9000 },
                },
            };

            var build = VisualizationService.Build(result, gamma: 100);

            // Filter out the "재사용"/"낭비" legend tail; what remains must be sorted.
            var lengthLegend = build.Legend
                .Where(l => l.Label.EndsWith("mm"))
                .Select(l => int.Parse(l.Label.Replace("mm", "")))
                .ToList();
            lengthLegend.Should().BeInAscendingOrder();
        }
    }
}
