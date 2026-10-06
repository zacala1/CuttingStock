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

        [Test]
        public void KerfAwareNormalSet_AllowsInteriorKerfExtents()
        {
            // Three 50mm strips fit exactly in width 160 with two 5mm kerfs:
            // 50 + 5 + 50 + 5 + 50 = 160. The normal set must include the
            // intermediate 105mm extent (50 + 5 + 50), not just raw item sums.
            var items = new List<GuillotineKnapsackDp.Item>
            {
                new() { OrderIndex = 0, W = 50, H = 100, Profit = 1 },
            };

            var res = new GuillotineKnapsackDp(160, 100, items, kerf: 5).Solve();

            res.Profit.Should().Be(3.0);
            res.Placements.Should().HaveCount(3);
            res.Placements.Select(p => p.X).Should().BeEquivalentTo(new[] { 0, 55, 110 });
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

        // ----- cooperative cancellation -----

        /// <summary>Many distinct dimensions on a sizeable sheet: thousands of DP cells.</summary>
        private static List<GuillotineKnapsackDp.Item> ManyDimensionItems()
        {
            var rng = new System.Random(7);
            var items = new List<GuillotineKnapsackDp.Item>();
            for (int i = 0; i < 30; i++)
                items.Add(new GuillotineKnapsackDp.Item
                {
                    OrderIndex = i,
                    W = 97 + rng.Next(0, 300),
                    H = 61 + rng.Next(0, 200),
                    Profit = 1000 + rng.Next(0, 5000),
                });
            return items;
        }

        [Test]
        public void TrySolve_NullCancel_MatchesSolve()
        {
            var items = ManyDimensionItems();
            var expected = new GuillotineKnapsackDp(900, 700, items, kerf: 3).Solve();

            new GuillotineKnapsackDp(900, 700, items, kerf: 3)
                .TrySolve(cancel: null, out var actual).Should().BeTrue();

            actual!.Profit.Should().Be(expected.Profit);
            actual.Placements.Should().HaveCount(expected.Placements.Count);
        }

        [Test]
        public void TrySolve_CancelAlreadyRequested_ReturnsFalseWithoutResult()
        {
            var dp = new GuillotineKnapsackDp(900, 700, ManyDimensionItems(), kerf: 3);

            bool completed = dp.TrySolve(cancel: () => true, out var result);

            completed.Should().BeFalse();
            result.Should().BeNull();
        }

        [Test]
        public void TrySolve_CancelledRun_DoesNotPoisonLaterSolve()
        {
            var items = ManyDimensionItems();
            var expected = new GuillotineKnapsackDp(900, 700, items, kerf: 3).Solve();

            // Cancel part-way through the recurrence, then re-solve on the SAME instance:
            // partial (truncated) cell values must not have been memoized.
            var dp = new GuillotineKnapsackDp(900, 700, items, kerf: 3);
            int polls = 0;
            dp.TrySolve(cancel: () => ++polls > 20, out _).Should().BeFalse();
            polls.Should().BeGreaterThan(20, "the DP should poll repeatedly rather than only once");

            dp.TrySolve(cancel: null, out var retry).Should().BeTrue();
            retry!.Profit.Should().Be(expected.Profit);
        }

        [Test]
        public void TrySolve_HeavyInstance_StopsPromptlyOnceCancelled()
        {
            // Large sheet + many distinct dimensions: an uncancelled solve takes seconds.
            var items = ManyDimensionItems();
            var dp = new GuillotineKnapsackDp(2440, 1220, items, kerf: 3);
            var clock = System.Diagnostics.Stopwatch.StartNew();

            bool completed = dp.TrySolve(cancel: () => clock.ElapsedMilliseconds >= 100, out _);

            completed.Should().BeFalse("the instance is far too large to finish in 100ms");
            clock.ElapsedMilliseconds.Should().BeLessThan(
                1000, "cancellation must interrupt the recurrence, not wait for the whole DP");
        }
    }
}
