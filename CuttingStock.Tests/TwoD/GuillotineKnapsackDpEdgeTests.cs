using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Edge-case &amp; round-trip tests for the 2D guillotine knapsack DP.
    /// </summary>
    [TestFixture]
    public class GuillotineKnapsackDpEdgeTests
    {
        // ----- correctness on small known instances -----

        [Test]
        public void HighProfitItemPreferred_OverManySmallOnes()
        {
            // 100×100 sheet. One big item profit 1000 fills the sheet, or 16 small items
            // profit 50 each = 800. DP must pick the big one.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 100, H = 100, Profit = 1000 },
                new() { OrderIndex = 1, W = 25,  H = 25,  Profit = 50   },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(1000.0);
            res.Placements.Should().HaveCount(1);
            res.Placements[0].OrderIndex.Should().Be(0);
        }

        [Test]
        public void DensityBeatsAbsolute_WhenManyFit()
        {
            // 100×100 sheet. Big item: 100×100 profit 100. Small items: 25×25 profit 10
            // → 16 fit → 160. DP must pick the small ones.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 100, H = 100, Profit = 100 },
                new() { OrderIndex = 1, W = 25,  H = 25,  Profit = 10  },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(160.0);
            res.Placements.Should().HaveCount(16);
            res.Placements.Should().OnlyContain(p => p.OrderIndex == 1);
        }

        [Test]
        public void EmptyItemList_ReturnsZero()
        {
            var dp = new GuillotineKnapsackDp(100, 100, new List<GuillotineKnapsackDp.Item>());
            var res = dp.Solve();
            res.Profit.Should().Be(0.0);
            res.Placements.Should().BeEmpty();
        }

        [Test]
        public void ItemsLargerThanSheet_NoPlacement()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 200, H = 50, Profit = 100 },
                new() { OrderIndex = 1, W = 50, H = 200, Profit = 100 },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            res.Profit.Should().Be(0.0);
        }

        [Test]
        public void OneByOneItem_Fits10000Times()
        {
            // 100×100 sheet, 1×1 item profit 1 — should be 10000 (full coverage).
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 1, H = 1, Profit = 1 },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            // Note: Beasley DP with normal sets cannot guarantee tile-everywhere reconstruction
            // for 1×1 — but profit must be ≥ 100×100 = 10000 (lower bounded by full tiling).
            res.Profit.Should().BeGreaterThanOrEqualTo(10000.0);
        }

        [Test]
        public void ExtremeAspectRatio_LongStrip()
        {
            // 1000×10 sheet, item 100×10 — should fit 10.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 100, H = 10, Profit = 1 },
            };
            var dp = new GuillotineKnapsackDp(1000, 10, items);
            var res = dp.Solve();
            res.Profit.Should().Be(10.0);
            res.Placements.Should().HaveCount(10);
        }

        [Test]
        public void RotatedAndOriginal_AreDistinctItems()
        {
            // 100×40 sheet. Item A 80×30 profit 5 (fits). Same item rotated 30×80 doesn't fit.
            // Item B 40×40 profit 4 fits. DP should prefer A.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 80, H = 30, Profit = 5, Rotated = false },
                new() { OrderIndex = 0, W = 30, H = 80, Profit = 5, Rotated = true  },
                new() { OrderIndex = 1, W = 40, H = 40, Profit = 4 },
            };
            var dp = new GuillotineKnapsackDp(100, 40, items);
            var res = dp.Solve();
            res.Profit.Should().BeGreaterThanOrEqualTo(5.0);
            // Verify the rotated variant is not used (height > sheet height).
            res.Placements.Should().NotContain(p => p.OrderIndex == 0 && p.Rotated && p.Height == 80);
        }

        [Test]
        public void Kerf_ReducesCapacity()
        {
            // 100×100 sheet, item 50×100. Without kerf: 2 fit. With kerf=1: still 2 fit
            // because cuts consume material (2 items take 100 + 0 kerf interior — still OK).
            // With item 51×100 + kerf=0: 1 fits (51+51 > 100). Use to verify kerf is respected.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 50, H = 100, Profit = 1 },
            };
            var noKerf = new GuillotineKnapsackDp(100, 100, items, kerf: 0).Solve();
            noKerf.Profit.Should().Be(2.0);

            var withKerf = new GuillotineKnapsackDp(100, 100, items, kerf: 5).Solve();
            // With 5mm kerf between two items: 50+5+50 = 105 > 100 → only one fits.
            withKerf.Profit.Should().Be(1.0);
        }

        // ----- reconstruction round-trip -----

        [Test]
        public void Reconstruction_AllPlacementsWithinSheet()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 30, H = 20, Profit = 1 },
                new() { OrderIndex = 1, W = 50, H = 40, Profit = 2 },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();
            foreach (var p in res.Placements)
            {
                p.X.Should().BeGreaterThanOrEqualTo(0);
                p.Y.Should().BeGreaterThanOrEqualTo(0);
                p.Right.Should().BeLessThanOrEqualTo(100);
                p.Bottom.Should().BeLessThanOrEqualTo(100);
            }
        }

        [Test]
        public void Reconstruction_NoOverlap()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 30, H = 20, Profit = 1 },
                new() { OrderIndex = 1, W = 50, H = 40, Profit = 2 },
                new() { OrderIndex = 2, W = 25, H = 25, Profit = 1 },
            };
            var dp = new GuillotineKnapsackDp(120, 120, items);
            var res = dp.Solve();

            for (int i = 0; i < res.Placements.Count; i++)
            for (int j = i + 1; j < res.Placements.Count; j++)
            {
                var a = res.Placements[i];
                var b = res.Placements[j];
                bool sepX = a.Right <= b.X || b.Right <= a.X;
                bool sepY = a.Bottom <= b.Y || b.Bottom <= a.Y;
                (sepX || sepY).Should().BeTrue("placements {0} and {1} overlap", i, j);
            }
        }

        [Test]
        public void Reconstruction_PlacementResultIsGuillotineCompliant()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 30, H = 25, Profit = 1 },
                new() { OrderIndex = 1, W = 60, H = 40, Profit = 2 },
                new() { OrderIndex = 2, W = 20, H = 20, Profit = 1 },
            };
            var dp = new GuillotineKnapsackDp(120, 120, items);
            var res = dp.Solve();

            var rects = res.Placements.Select(p => (p.X, p.Y, p.Width, p.Height)).ToList();
            GuillotineValidator.IsGuillotineCompliant(0, 0, 120, 120, rects).Should().BeTrue();
        }

        [Test]
        public void ProfitMatchesSumOfPlacedProfits()
        {
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 50, H = 50, Profit = 7.0 },
                new() { OrderIndex = 1, W = 25, H = 25, Profit = 1.5 },
            };
            var dp = new GuillotineKnapsackDp(100, 100, items);
            var res = dp.Solve();

            // Sum profits over placed items by orientation match.
            double placedProfit = 0;
            foreach (var pl in res.Placements)
            {
                var item = items.First(it =>
                    it.OrderIndex == pl.OrderIndex && it.Rotated == pl.Rotated &&
                    it.W == pl.Width && it.H == pl.Height);
                placedProfit += item.Profit;
            }
            placedProfit.Should().BeApproximately(res.Profit, 1e-9);
        }
    }
}
