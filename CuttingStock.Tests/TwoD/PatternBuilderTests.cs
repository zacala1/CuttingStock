using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;

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
        public void PinwheelArrangement_ReturnsNull()
        {
            // Same pinwheel that GuillotineValidator rejects — PatternBuilder should not be
            // able to construct a tree.
            var pl = new List<Placement>
            {
                P(0, 0, 0, 3, 1), P(1, 3, 0, 1, 3),
                P(2, 1, 3, 3, 1), P(3, 0, 1, 1, 3),
            };
            var tree = PatternBuilder.BuildTree(4, 4, pl);
            tree.Should().BeNull();
        }
    }
}
