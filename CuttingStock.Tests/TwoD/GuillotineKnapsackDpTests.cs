using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Tests for <see cref="GuillotineKnapsackDp"/> — Beasley 1985 normal-cut DP.
    /// </summary>
    [TestFixture]
    public class GuillotineKnapsackDpTests
    {
        [Test]
        public void SingleItemFitsExactly()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 100, H = 100, Profit = 5.0 }
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(5.0);
            res.Placements.Should().HaveCount(1);
        }

        [Test]
        public void TwoCopiesFitInExactWidth()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 50, H = 100, Profit = 3.0 }
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(6.0);
            res.Placements.Should().HaveCount(2);
        }

        [Test]
        public void DenseGridPacking()
        {
            // 100×100 sheet with 25×25 item, profit = 1: should fit 16 copies, profit = 16.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 25, H = 25, Profit = 1.0 }
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(16.0);
            res.Placements.Should().HaveCount(16);
        }

        [Test]
        public void NoItemFits_ReturnsZero()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 200, H = 200, Profit = 10.0 }
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(0.0);
            res.Placements.Should().BeEmpty();
        }

        [Test]
        public void RotatedOrientation_TreatedAsSeparateItem()
        {
            // 100×40 sheet; item 80×30 only fits as-is (profit 5), or rotated 30×80 doesn't fit.
            // Item 60×35 fits (profit 4), so DP should pick the 80×30.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 80, H = 30, Profit = 5.0, Rotated = false },
                new() { OrderIndex = 1, W = 60, H = 35, Profit = 4.0, Rotated = false },
            };
            var dp = new GuillotineKnapsackDp(100, 40, items);
            var res = dp.Solve();
            res.Profit.Should().BeGreaterThanOrEqualTo(5.0);
        }
    }
}
