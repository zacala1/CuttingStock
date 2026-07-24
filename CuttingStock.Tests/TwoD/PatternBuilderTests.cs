using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Tests for <see cref="PatternBuilder"/> — placement list → guillotine tree.
    /// </summary>
    [TestFixture]
    public class PatternBuilderTests
    {
        private static Placement P(int orderIndex, int x, int y, int w, int h, bool rotated = false) =>
            new() { OrderIndex = orderIndex, X = x, Y = y, Width = w, Height = h, Rotated = rotated };

        [Test]
        public void EmptySheet_ReturnsWasteLeaf()
        {
            var tree = PatternBuilder.BuildTree(100, 100, new List<Placement>());
            tree.Should().NotBeNull();
            tree!.Kind.Should().Be(NodeKind.Waste);
            tree.Width.Should().Be(100);
            tree.Height.Should().Be(100);
        }

        [Test]
        public void SingleItem_ExactFill_BecomesLeaf()
        {
            var pl = new List<Placement> { P(0, 0, 0, 100, 100) };
            var tree = PatternBuilder.BuildTree(100, 100, pl);
            tree.Should().NotBeNull();
            tree!.Kind.Should().Be(NodeKind.Leaf);
            tree.OrderIndex.Should().Be(0);
            GuillotineValidator.IsValidTree(tree).Should().BeTrue();
        }

        [Test]
        public void TwoSideBySide_BecomesVCut()
        {
            var pl = new List<Placement>
            {
                P(0, 0,  0, 50, 100),
                P(1, 50, 0, 50, 100),
            };
            var tree = PatternBuilder.BuildTree(100, 100, pl);
            tree.Should().NotBeNull();
            tree!.Kind.Should().Be(NodeKind.VCut);
            tree.Children.Should().HaveCount(2);
            GuillotineValidator.IsValidTree(tree).Should().BeTrue();
        }

        [Test]
        public void ShelfPattern_4Items_BuildsValidTree()
        {
            // Two shelves of two items each.
            var pl = new List<Placement>
            {
                P(0, 0,  0, 60, 50), P(1, 60, 0, 40, 50),
                P(2, 0, 50, 30, 50), P(3, 30, 50, 70, 50),
            };
            var tree = PatternBuilder.BuildTree(100, 100, pl);
            tree.Should().NotBeNull();
            GuillotineValidator.IsValidTree(tree!).Should().BeTrue();
        }

        [Test]
        public void FlatPlacementPattern_WithNullRoot_RemainsCanonicalAndReconstructable()
        {
            var pattern = new CuttingPattern2D
            {
                Sheet = new Sheet(100, 100, 1),
                Placements = new List<Placement>
                {
                    P(0, 0,  0, 50, 50),
                    P(1, 50, 0, 50, 50),
                    P(2, 0, 50, 50, 50),
                    P(3, 50, 50, 50, 50),
                },
            };

            pattern.Root.Should().BeNull();
            GuillotineValidator.IsGuillotineCompliant(pattern).Should().BeTrue();

            var derived = PatternBuilder.BuildTree(pattern.Sheet.Width, pattern.Sheet.Height, pattern.Placements);
            derived.Should().NotBeNull();
            GuillotineValidator.IsValidTree(derived!).Should().BeTrue();
            CountKind(derived!, NodeKind.Leaf).Should().Be(4);
        }

        [Test]
        public void PinwheelArrangement_ReturnsNull()
        {
            // Same pinwheel that GuillotineValidator rejects — PatternBuilder should not be
            // able to construct a tree.
            var pl = new List<Placement>
            {
                P(0, 0, 0, 3, 1), P(1, 3, 0, 1, 3),
                P(2, 1, 3, 3, 1), P(3, 0, 1, 1, 3),
            };
            GuillotineValidator.IsGuillotineCompliant(
                0,
                0,
                4,
                4,
                new List<(int, int, int, int)>
                {
                    (0, 0, 3, 1),
                    (3, 0, 1, 3),
                    (1, 3, 3, 1),
                    (0, 1, 1, 3),
                }).Should().BeFalse();
            var tree = PatternBuilder.BuildTree(4, 4, pl);
            tree.Should().BeNull();
        }

        [Test]
        public void SingleItem_TopLeftCorner_DecomposesWithWaste()
        {
            // Rect flush with top-left corner: needs 2 cuts (right strip + bottom strip).
            var pl = new List<Placement> { P(0, 0, 0, 40, 30) };
            var tree = PatternBuilder.BuildTree(100, 100, pl);
            tree.Should().NotBeNull();
            GuillotineValidator.IsValidTree(tree!).Should().BeTrue();
            CountKind(tree!, NodeKind.Leaf).Should().Be(1);
            CountKind(tree!, NodeKind.Waste).Should().BeGreaterThan(0);
        }

        [Test]
        public void SingleItem_Interior_DecomposesWith4Cuts()
        {
            // Rect floating in the middle: previously returned null. Should now decompose
            // by peeling waste off each side recursively (4 cuts → 4 waste leaves + 1 leaf).
            var pl = new List<Placement> { P(0, 10, 10, 50, 50) };
            var tree = PatternBuilder.BuildTree(100, 100, pl);
            tree.Should().NotBeNull();
            GuillotineValidator.IsValidTree(tree!).Should().BeTrue();
            CountKind(tree!, NodeKind.Leaf).Should().Be(1);
            CountKind(tree!, NodeKind.Waste).Should().Be(4);
        }

        private static int CountKind(GuillotineNode node, NodeKind kind)
        {
            int c = node.Kind == kind ? 1 : 0;
            foreach (var ch in node.Children) c += CountKind(ch, kind);
            return c;
        }
    }
}
