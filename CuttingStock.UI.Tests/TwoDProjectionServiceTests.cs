using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class TwoDProjectionServiceTests
    {
        [Test]
        public void BuildRender_FlattensPatternAndPlacementData()
        {
            var placement = new Placement
            {
                OrderIndex = 2,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40,
                Rotated = true,
            };
            var result = new SolverResult2D
            {
                Patterns =
                {
                    new CuttingPattern2D
                    {
                        Sheet = new Sheet(100, 80, 1),
                        Multiplicity = 3,
                        Placements = [placement],
                    },
                },
            };

            TwoDRenderProjection projection = TwoDProjectionService.BuildRender(
                "Shelf",
                result,
                new SolverOptions2D { Trim = 5 });

            projection.AlgorithmName.Should().Be("Shelf");
            projection.Trim.Should().Be(5);
            projection.Patterns.Should().ContainSingle();
            projection.Patterns[0].Should().BeEquivalentTo(new
            {
                SheetWidth = 100,
                SheetHeight = 80,
                Multiplicity = 3,
                Efficiency = 15.0,
            });
            projection.Patterns[0].Placements.Should().Equal(
                new TwoDPlacementProjection(2, 10, 20, 30, 40, true));
        }

        [Test]
        public void BuildChart_FiltersFailedRowsAndProjectsPlainSeries()
        {
            ComparisonResult2D[] rows =
            [
                new()
                {
                    AlgorithmName = "Shelf (Fast)",
                    SheetsUsed = 3,
                    MaterialEfficiency = 88.5,
                    ExecutionTimeMs = 1.25,
                    Success = true,
                },
                new()
                {
                    AlgorithmName = "Failed",
                    Success = false,
                },
            ];

            TwoDChartProjection projection = TwoDProjectionService.BuildChart(rows);

            projection.Labels.Should().Equal($"Shelf{Environment.NewLine}(Fast)");
            projection.SheetsUsed.Should().Equal(3d);
            projection.MaterialEfficiency.Should().Equal(88.5);
            projection.ExecutionTimeMs.Should().Equal(1.25);
        }

        [Test]
        public void BuildChart_NoSuccessfulRows_ReturnsEmptyProjection()
        {
            TwoDProjectionService.BuildChart(
                    [new ComparisonResult2D { AlgorithmName = "Failed", Success = false }])
                .Should().Be(TwoDChartProjection.Empty);
        }

        [Test]
        public void SelectBestRow_UsesCostBeforeSheetCount()
        {
            var fewerSheets = new ComparisonResult2D
            {
                AlgorithmName = "Fewer sheets",
                TotalCost = 100,
                SheetsUsed = 1,
                Success = true,
            };
            var lowerCost = new ComparisonResult2D
            {
                AlgorithmName = "Lower cost",
                TotalCost = 50,
                SheetsUsed = 2,
                Success = true,
            };

            TwoDProjectionService.SelectBestRow([fewerSheets, lowerCost])
                .Should().BeSameAs(lowerCost);
        }
    }
}
