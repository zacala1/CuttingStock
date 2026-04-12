using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Sanity tests for the recursive guillotine separator test (Beasley 1985).
    /// </summary>
    [TestFixture]
    public class GuillotineValidatorTests
    {
        [Test]
        public void EmptyAndSingleAreCompliant()
        {
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, new List<(int, int, int, int)>()).Should().BeTrue();
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, new List<(int, int, int, int)> { (0, 0, 50, 50) }).Should().BeTrue();
        }

        [Test]
        public void TwoSideBySideRectanglesAreCompliant()
        {
            var rects = new List<(int, int, int, int)>
            {
                (0, 0, 50, 100),
                (50, 0, 50, 100),
            };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        [Test]
        public void ShelfPatternIsCompliant()
        {
            // Two shelves of two items each — shelf cut horizontal, then vertical cuts in each shelf.
            var rects = new List<(int, int, int, int)>
            {
                (0, 0, 60, 50),  (60, 0, 40, 50),
                (0, 50, 30, 50), (30, 50, 70, 50),
            };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 100, 100, rects).Should().BeTrue();
        }

        [Test]
        public void PinwheelPatternIsNotCompliant()
        {
            // Classic pinwheel: four rectangles around a missing center — no edge-to-edge cut exists.
            // Sheet 4×4, items 3×1 and 1×3 arranged around a 1×1 center hole.
            var rects = new List<(int, int, int, int)>
            {
                (0, 0, 3, 1),   // top
                (3, 0, 1, 3),   // right
                (1, 3, 3, 1),   // bottom
                (0, 1, 1, 3),   // left
            };
            GuillotineValidator.IsGuillotineCompliant(0, 0, 4, 4, rects).Should().BeFalse();
        }
    }
}
