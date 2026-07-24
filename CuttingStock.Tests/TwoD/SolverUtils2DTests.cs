using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Tests for the shared 2D solver utility functions.
    /// </summary>
    [TestFixture]
    public class SolverUtils2DTests
    {
        // ----- ExpandOrders -----

        [Test]
        public void ExpandOrders_PreservesOrderIndex_AndQuantity()
        {
            var orders = new List<RectOrder>
            {
                new(100, 50, 3, allowRotation: true),
                new(200, 80, 2, allowRotation: false),
            };
            var expanded = SolverUtils2D.ExpandOrders(orders, globalAllowRotation: true);

            expanded.Should().HaveCount(5);
            expanded.Count(x => x.OrderIndex == 0).Should().Be(3);
            expanded.Count(x => x.OrderIndex == 1).Should().Be(2);
            expanded.Where(x => x.OrderIndex == 0).Should().OnlyContain(x => x.Rot);
            expanded.Where(x => x.OrderIndex == 1).Should().OnlyContain(x => !x.Rot);
        }

        [Test]
        public void ExpandOrders_GlobalRotationOff_DisablesAll()
        {
            var orders = new List<RectOrder> { new(100, 50, 2, allowRotation: true) };
            var expanded = SolverUtils2D.ExpandOrders(orders, globalAllowRotation: false);
            expanded.Should().OnlyContain(x => !x.Rot);
        }

        [Test]
        public void ExpandOrders_EmptyInput_ReturnsEmpty()
        {
            SolverUtils2D.ExpandOrders(new List<RectOrder>(), true).Should().BeEmpty();
        }

        [Test]
        public void ValidateInputs_NoSheets_MarksResultFailedAndReturnsTrue()
        {
            var result = new SolverResult2D();

            SolverUtils2D.ValidateInputs(new List<Sheet>(), new List<RectOrder> { new(100, 100, 1) }, result)
                .Should().BeTrue();

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Be("At least one sheet must be provided.");
        }

        [Test]
        public void ValidateInputs_EmptyOrders_ReturnsTrueWithoutFailingResult()
        {
            var result = new SolverResult2D();

            SolverUtils2D.ValidateInputs(new List<Sheet> { new(100, 100, 1) }, new List<RectOrder>(), result)
                .Should().BeTrue();

            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.Patterns.Should().BeEmpty();
        }

        [Test]
        public void AggregateByDims_MergesQuantitiesByDimensions()
        {
            var sheets = new List<Sheet>
            {
                new(100, 200, 1),
                new(100, 200, 3),
                new(200, 100, 2),
            };

            var aggregated = SolverUtils2D.AggregateByDims(sheets);

            aggregated.Should().BeEquivalentTo(new[]
            {
                new Sheet(100, 200, 4),
                new Sheet(200, 100, 2),
            });
        }

        // ----- OrderSheets -----

        [Test]
        public void OrderSheets_LargeToSmall_Sorts()
        {
            var sheets = new List<Sheet>
            {
                new(100, 100, 1),
                new(500, 500, 1),
                new(300, 300, 1),
            };
            var ordered = SolverUtils2D.OrderSheets(sheets, new SolverOptions2D { UsageOrder = StockUsageOrder.LargeToSmall });
            ordered.Select(s => s.Width).Should().Equal(500, 300, 100);
        }

        [Test]
        public void OrderSheets_SmallToLarge_Sorts()
        {
            var sheets = new List<Sheet>
            {
                new(100, 100, 1),
                new(500, 500, 1),
                new(300, 300, 1),
            };
            var ordered = SolverUtils2D.OrderSheets(sheets, new SolverOptions2D { UsageOrder = StockUsageOrder.SmallToLarge });
            ordered.Select(s => s.Width).Should().Equal(100, 300, 500);
        }

        // ----- HasOverlap -----

        [Test]
        public void HasOverlap_AdjacentRectangles_NoOverlap_KerfZero()
        {
            var pl = new List<Placement>
            {
                Make(0, 0, 0, 50, 100),
                Make(1, 50, 0, 50, 100),
            };
            SolverUtils2D.HasOverlap(pl, kerf: 0).Should().BeFalse();
        }

        [Test]
        public void HasOverlap_OverlappingRectangles_DetectsOverlap()
        {
            var pl = new List<Placement>
            {
                Make(0, 0, 0, 60, 100),
                Make(1, 40, 0, 60, 100),
            };
            SolverUtils2D.HasOverlap(pl, kerf: 0).Should().BeTrue();
        }

        [Test]
        public void HasOverlap_AdjacentWithoutKerf_FailsWhenKerfRequired()
        {
            var pl = new List<Placement>
            {
                Make(0, 0, 0, 50, 100),
                Make(1, 50, 0, 50, 100),
            };
            // Items touch — adding a kerf > 0 makes them violate the gap requirement.
            SolverUtils2D.HasOverlap(pl, kerf: 5).Should().BeTrue();
        }

        [Test]
        public void HasOverlap_AdjacentWithKerfGap_PassesKerfCheck()
        {
            var pl = new List<Placement>
            {
                Make(0, 0, 0, 50, 100),
                Make(1, 55, 0, 50, 100),
            };
            SolverUtils2D.HasOverlap(pl, kerf: 5).Should().BeFalse();
        }

        [Test]
        public void HasOverlap_DiagonalAdjacency_NoOverlap()
        {
            var pl = new List<Placement>
            {
                Make(0, 0, 0, 50, 50),
                Make(1, 50, 50, 50, 50),
            };
            SolverUtils2D.HasOverlap(pl, kerf: 0).Should().BeFalse();
        }

        // ----- WithinSheet -----

        [Test]
        public void WithinSheet_AllInside_ReturnsTrue()
        {
            var sheet = new Sheet(100, 100, 1);
            var pl = new List<Placement> { Make(0, 0, 0, 50, 50), Make(1, 50, 50, 50, 50) };
            SolverUtils2D.WithinSheet(pl, sheet, trim: 0).Should().BeTrue();
        }

        [Test]
        public void WithinSheet_NegativeCoordinate_ReturnsFalse()
        {
            var sheet = new Sheet(100, 100, 1);
            var pl = new List<Placement> { Make(0, -1, 0, 50, 50) };
            SolverUtils2D.WithinSheet(pl, sheet, trim: 0).Should().BeFalse();
        }

        [Test]
        public void WithinSheet_ExceedsRightEdge_ReturnsFalse()
        {
            var sheet = new Sheet(100, 100, 1);
            var pl = new List<Placement> { Make(0, 60, 0, 50, 50) };
            SolverUtils2D.WithinSheet(pl, sheet, trim: 0).Should().BeFalse();
        }

        [Test]
        public void WithinSheet_TrimReducesUsableArea()
        {
            var sheet = new Sheet(100, 100, 1);
            var pl = new List<Placement> { Make(0, 0, 0, 100, 100) };  // touches edge
            SolverUtils2D.WithinSheet(pl, sheet, trim: 5).Should().BeFalse();
        }

        // ----- CountPlaced -----

        [Test]
        public void CountPlaced_AggregatesAcrossPatternsAndMultiplicity()
        {
            var sheet = new Sheet(100, 100, 5);
            var p1 = new CuttingPattern2D
            {
                Sheet = sheet,
                Multiplicity = 3,
                Placements = new List<Placement> { Make(0, 0, 0, 50, 50), Make(1, 50, 0, 50, 50) },
            };
            var p2 = new CuttingPattern2D
            {
                Sheet = sheet,
                Multiplicity = 2,
                Placements = new List<Placement> { Make(0, 0, 0, 100, 100) },
            };
            var counts = SolverUtils2D.CountPlaced(new List<CuttingPattern2D> { p1, p2 }, orderCount: 2);
            counts[0].Should().Be(3 + 2);   // 3 (from p1×3) + 2 (from p2×2) — wait p1 has 1 of order 0, ×3 = 3
            counts[1].Should().Be(3);
        }

        [Test]
        public void TrimToDemand_RemovesOverproductionAcrossMultiplicity()
        {
            var sheet = new Sheet(100, 100, 5);
            var pattern = new CuttingPattern2D
            {
                Sheet = sheet,
                Multiplicity = 2,
                Placements = new List<Placement>
                {
                    Make(0, 0, 0, 20, 20),
                    Make(1, 20, 0, 20, 20),
                },
            };

            var trimmed = SolverUtils2D.TrimToDemand(
                new List<CuttingPattern2D> { pattern },
                new[] { 1, 2 },
                out var produced);

            produced.Should().Equal(1, 2);
            SolverUtils2D.CountPlaced(trimmed, orderCount: 2).Should().Equal(1, 2);
            trimmed.Should().HaveCount(2);
            trimmed.Should().OnlyContain(p => p.Multiplicity == 1);
            trimmed[0].Placements.Select(p => p.OrderIndex).Should().Equal(0, 1);
            trimmed[1].Placements.Select(p => p.OrderIndex).Should().Equal(1);
        }

        [Test]
        public void Finalize_ComputesCostFromWasteArea()
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
                            Make(0, 0, 0, 40, 50),
                        },
                    },
                },
            };

            SolverUtils2D.Finalize(result, new SolverOptions2D { AlphaArea = 1.5f });

            result.TotalWasteArea.Should().Be(8000);
            result.TotalCost.Should().Be(12000);
        }

        [Test]
        public void ValidateSuccessfulResult_ValidPattern_ReturnsNull()
        {
            var sheets = new List<Sheet> { new(105, 100, 1) };
            var orders = new List<RectOrder> { new(50, 100, 2) };
            var options = new SolverOptions2D { Kerf = 5 };
            var result = new SolverResult2D
            {
                Patterns = new List<CuttingPattern2D>
                {
                    new()
                    {
                        Sheet = sheets[0],
                        Placements = new List<Placement>
                        {
                            Make(0, 0, 0, 50, 100),
                            Make(0, 55, 0, 50, 100),
                        },
                    },
                },
            };

            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, options, result).Should().BeNull();
        }

        [Test]
        public void ValidateSuccessfulResult_Overlap_ReturnsError()
        {
            var sheets = new List<Sheet> { new(120, 100, 1) };
            var orders = new List<RectOrder> { new(60, 100, 2) };
            var result = new SolverResult2D
            {
                Patterns = new List<CuttingPattern2D>
                {
                    new()
                    {
                        Sheet = sheets[0],
                        Placements = new List<Placement>
                        {
                            Make(0, 0, 0, 60, 100),
                            Make(0, 40, 0, 60, 100),
                        },
                    },
                },
            };

            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, new SolverOptions2D(), result)
                .Should().Contain("overlapping");
        }

        [Test]
        public void ValidateSuccessfulResult_IllegalRotation_ReturnsError()
        {
            var sheets = new List<Sheet> { new(100, 200, 1) };
            var orders = new List<RectOrder> { new(100, 50, 1, allowRotation: false) };
            var result = new SolverResult2D
            {
                Patterns = new List<CuttingPattern2D>
                {
                    new()
                    {
                        Sheet = sheets[0],
                        Placements = new List<Placement>
                        {
                            new() { OrderIndex = 0, X = 0, Y = 0, Width = 50, Height = 100, Rotated = true },
                        },
                    },
                },
            };

            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, new SolverOptions2D(), result)
                .Should().Contain("illegally rotates");
        }

        // ----- helpers -----

        private static Placement Make(int oi, int x, int y, int w, int h) =>
            new() { OrderIndex = oi, X = x, Y = y, Width = w, Height = h };
    }
}
