using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Edge cases for the recursive guillotine separator test (Beasley 1985).
    /// </summary>
    [TestFixture]
    public class GuillotineValidatorEdgeTests
    {
        [Test]
        public void OverlappingRectangles_TreatedAsCompliantOrNot_DoesNotThrow()
        {
            // Even if rectangles overlap (which shouldn't happen in valid solver output),
            // the validator must terminate without throwing.
            var rects = new List<(int, int, int, int)>
            {
                (0, 0, 60, 100),
                (40, 0, 60, 100),
            };
            // We don't assert truth value — just that it terminates.
            _ = GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects);
            Assert.Pass();
        }

        [Test]
        public void ThreeShelves_VerticalStacking_IsCompliant()
        {
            var rects = new List<(int, int, int, int)>
            {
                (0,  0, 100, 30),
                (0, 30, 60,  40), (60, 30, 40, 40),
                (0, 70, 100, 30),
            };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        [Test]
        public void NestedGuillotineSplits_DeepRecursion_IsCompliant()
        {
            // 4×4 grid of 25×25 items in 100×100 sheet.
            var rects = new List<(int, int, int, int)>();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    rects.Add((i * 25, j * 25, 25, 25));
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        [Test]
        public void SinglePlacementInLargerSheet_AllowsWaste()
        {
            // A single rectangle inside a larger sheet — guillotine cuts can simply
            // separate it from the surrounding waste.
            var rects = new List<(int, int, int, int)> { (0, 0, 50, 50) };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        [Test]
        public void TwoBlocks_StackedVerticallyWithGap_IsCompliant()
        {
            var rects = new List<(int, int, int, int)>
            {
                (10, 10, 80, 30),
                (10, 60, 80, 30),
            };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        // ----- IsValidTree -----

        [Test]
        public void IsValidTree_ChildrenOutsideParent_Rejected()
        {
            var node = new GuillotineNode
            {
                Kind = NodeKind.HCut,
                X = 0, Y = 0, Width = 100, Height = 100,
                Children =
                {
                    new() { Kind = NodeKind.Leaf, X = 0, Y = 0,  Width = 100, Height = 60 },
                    new() { Kind = NodeKind.Leaf, X = 0, Y = 60, Width = 100, Height = 50 },  // overflows
                },
            };
            GuillotineValidator.IsValidTree(node).Should().BeFalse();
        }

        [Test]
        public void IsValidTree_HCutChildrenWithGap_Rejected()
        {
            var node = new GuillotineNode
            {
                Kind = NodeKind.HCut,
                X = 0, Y = 0, Width = 100, Height = 100,
                Children =
                {
                    new() { Kind = NodeKind.Leaf, X = 0, Y = 0,  Width = 100, Height = 30 },
                    new() { Kind = NodeKind.Leaf, X = 0, Y = 50, Width = 100, Height = 50 },  // gap of 20
                },
            };
            GuillotineValidator.IsValidTree(node).Should().BeFalse();
        }

        [Test]
        public void IsValidTree_VCutChildrenInWrongOrder_Rejected()
        {
            // Children are not adjacent left-to-right.
            var node = new GuillotineNode
            {
                Kind = NodeKind.VCut,
                X = 0, Y = 0, Width = 100, Height = 100,
                Children =
                {
                    new() { Kind = NodeKind.Leaf, X = 50, Y = 0, Width = 50, Height = 100 },
                    new() { Kind = NodeKind.Leaf, X = 0,  Y = 0, Width = 50, Height = 100 },
                },
            };
            GuillotineValidator.IsValidTree(node).Should().BeFalse();
        }

        [Test]
        public void IsValidTree_WellFormed_HCutThenVCut_Accepted()
        {
            var node = new GuillotineNode
            {
                Kind = NodeKind.HCut,
                X = 0, Y = 0, Width = 100, Height = 100,
                Children =
                {
                    new()
                    {
                        Kind = NodeKind.VCut,
                        X = 0, Y = 0, Width = 100, Height = 50,
                        Children =
                        {
                            new() { Kind = NodeKind.Leaf,  X = 0,  Y = 0, Width = 60, Height = 50 },
                            new() { Kind = NodeKind.Waste, X = 60, Y = 0, Width = 40, Height = 50 },
                        },
                    },
                    new() { Kind = NodeKind.Leaf,  X = 0, Y = 50, Width = 100, Height = 50 },
                },
            };
            GuillotineValidator.IsValidTree(node).Should().BeTrue();
        }

        [Test]
        public void IsValidTree_InternalNodeWithNoChildren_Rejected()
        {
            var node = new GuillotineNode
            {
                Kind = NodeKind.HCut,
                X = 0, Y = 0, Width = 100, Height = 100,
            };
            GuillotineValidator.IsValidTree(node).Should().BeFalse();
        }
    }
}
