using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Tests for shared column-generation infrastructure: signature / dedup / multi-pricing.
    /// </summary>
    [TestFixture]
    public class PatternPoolTests
    {
        private static PatternPool.Column MakeCol(int w, int h, int q, int[] counts) =>
            new()
            {
                Sheet = new Sheet(w, h, q),
                Counts = counts,
                Placements = new List<Placement>(),
            };

        // ----- Signature -----

        [Test]
        public void Signature_SameContentSameHash()
        {
            var a = MakeCol(100, 100, 1, new[] { 2, 0, 3 });
            var b = MakeCol(100, 100, 1, new[] { 2, 0, 3 });
            PatternPool.Signature(a).Should().Be(PatternPool.Signature(b));
        }

        [Test]
        public void Signature_DifferentCountsDifferentHash()
        {
            var a = MakeCol(100, 100, 1, new[] { 2, 0, 3 });
            var b = MakeCol(100, 100, 1, new[] { 2, 1, 3 });
            PatternPool.Signature(a).Should().NotBe(PatternPool.Signature(b));
        }

        [Test]
        public void Signature_DifferentSheetDifferentHash()
        {
            var a = MakeCol(100, 100, 1, new[] { 2, 0 });
            var b = MakeCol(200, 100, 1, new[] { 2, 0 });
            PatternPool.Signature(a).Should().NotBe(PatternPool.Signature(b));
        }

        [Test]
        public void Signature_IgnoresSheetQuantity()
        {
            // Two columns for the same sheet *size* should collapse even if the sheet
            // objects report different Quantity — the master sees only sheet dimensions.
            var a = MakeCol(100, 100, 1, new[] { 2 });
            var b = MakeCol(100, 100, 9, new[] { 2 });
            PatternPool.Signature(a).Should().Be(PatternPool.Signature(b));
        }

        // ----- AddIfNew -----

        [Test]
        public void AddIfNew_PreventsDuplicates()
        {
            var list = new List<PatternPool.Column>();
            var sigs = new HashSet<long>();

            PatternPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 1, 2 })).Should().BeTrue();
            PatternPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 1, 2 })).Should().BeFalse();
            PatternPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 2, 1 })).Should().BeTrue();

            list.Should().HaveCount(2);
            sigs.Should().HaveCount(2);
        }

        // ----- Multi-pricing -----

        [Test]
        public void PriceImprovingColumns_YieldsOneColumnPerImprovableSheetType()
        {
            // Two sheet types; items that fit in both. Both should price into the pool
            // when duals are large enough to make either reduced cost negative.
            var sheets = new List<Sheet>
            {
                new(200, 200, 5),
                new(300, 200, 5),
            };
            var orders = new List<RectOrder>
            {
                new(80, 80, 4),
                new(100, 50, 4),
            };
            var options = new SolverOptions2D { TimeLimitMs = 3000 };
            var pi = new double[] { 1e6, 1e6 }; // huge duals → every column is improving

            var columns = new List<PatternPool.Column>(
                PatternPool.PriceImprovingColumns(sheets, orders, pi, options, orderCount: 2));

            columns.Should().HaveCountGreaterThanOrEqualTo(2);
            // One column per sheet type.
            columns.Should().Contain(c => c.Sheet.Width == 200);
            columns.Should().Contain(c => c.Sheet.Width == 300);
        }

        [Test]
        public void PriceImprovingColumns_EmptyWhenNoImprovement()
        {
            var sheets = new List<Sheet> { new(200, 200, 5) };
            var orders = new List<RectOrder> { new(80, 80, 4) };
            var options = new SolverOptions2D { TimeLimitMs = 3000 };
            var pi = new double[] { 0.0 };  // no dual → no improvement

            var columns = new List<PatternPool.Column>(
                PatternPool.PriceImprovingColumns(sheets, orders, pi, options, orderCount: 1));
            columns.Should().BeEmpty();
        }

        // ----- LP master -----

        [Test]
        public void SolveLpMaster_BasicTwoColumnInstance()
        {
            // Two identical columns (each covers one of two orders), demand = 3 each.
            // Optimum: x1 = 3, x2 = 3 (total 6 sheets × area).
            var columns = new List<PatternPool.Column>
            {
                new() { Sheet = new Sheet(100, 100, 10), Counts = new[] { 1, 0 } },
                new() { Sheet = new Sheet(100, 100, 10), Counts = new[] { 0, 1 } },
            };
            var demand = new[] { 3, 3 };

            PatternPool.SolveLpMaster(columns, demand, out var x, out var pi).Should().BeTrue();
            x.Should().HaveCount(2);
            x[0].Should().BeApproximately(3.0, 1e-6);
            x[1].Should().BeApproximately(3.0, 1e-6);
            // Dual of each demand constraint equals the per-unit sheet cost.
            pi[0].Should().BeApproximately(100 * 100, 1e-3);
            pi[1].Should().BeApproximately(100 * 100, 1e-3);
        }
    }
}
