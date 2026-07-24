using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.TwoD
{
    [TestFixture]
    [Category("Architecture")]
    public class FocusedTwoDServicesTests
    {
        [Test]
        public void InputPreprocessor_Preprocess_AggregatesDuplicateSheetDimensions()
        {
            var result = new SolverResult2D();
            var input = TwoDInputPreprocessor.Preprocess(
                new List<Sheet>
                {
                    new(1000, 500, 1),
                    new(1000, 500, 2),
                },
                new List<RectOrder> { new(100, 100, 1) },
                result);

            input.ShouldReturn.Should().BeFalse();
            input.Sheets.Should().Equal(new Sheet(1000, 500, 3));
            input.Orders.Should().Equal(new RectOrder(100, 100, 1));
        }

        [Test]
        public void InputPreprocessor_Preprocess_PreservesNormalizedInputForTrivialSuccess()
        {
            var result = new SolverResult2D();

            var input = TwoDInputPreprocessor.Preprocess(
                new List<Sheet>
                {
                    new(1000, 500, 1),
                    new(1000, 500, 2),
                },
                new List<RectOrder>(),
                result);

            input.ShouldReturn.Should().BeTrue();
            result.Success.Should().BeTrue();
            input.Sheets.Should().Equal(new Sheet(1000, 500, 3));
            input.Orders.Should().BeEmpty();
        }

        [Test]
        public void ResultFinalizer_FinalizeAndValidate_ComputesCostAfterValidation()
        {
            var result = new SolverResult2D
            {
                Patterns = new List<CuttingPattern2D>
                {
                    new()
                    {
                        Sheet = new Sheet(100, 100, 1),
                        Placements = new List<Placement>
                        {
                            new() { OrderIndex = 0, X = 0, Y = 0, Width = 50, Height = 50 },
                        },
                    },
                },
            };

            TwoDResultFinalizer.FinalizeAndValidate(
                new List<Sheet> { new(100, 100, 1) },
                new List<RectOrder> { new(50, 50, 1, allowRotation: false) },
                new SolverOptions2D { AlphaArea = 2f, AllowRotation = false },
                result);

            result.Success.Should().BeTrue();
            result.TotalWasteArea.Should().Be(7500);
            result.TotalCost.Should().Be(15000);
        }

        [Test]
        public void PlacementMath_CountPlaced_AccountsForPatternMultiplicity()
        {
            var pattern = new CuttingPattern2D
            {
                Sheet = new Sheet(100, 100, 1),
                Multiplicity = 3,
                Placements = new List<Placement>
                {
                    new() { OrderIndex = 0, Width = 50, Height = 50 },
                    new() { OrderIndex = 1, Width = 50, Height = 50 },
                },
            };

            TwoDPlacementMath.CountPlaced(new List<CuttingPattern2D> { pattern }, orderCount: 2)
                .Should().Equal(3, 3);
        }
    }
}
