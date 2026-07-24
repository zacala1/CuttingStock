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
        private static PatternColumn MakeCol(int w, int h, int q, int[] counts) =>
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
            PatternColumnPool.Signature(a).Should().Be(PatternColumnPool.Signature(b));
        }

        [Test]
        public void Signature_KnownColumn_PreservesFnvFingerprint()
        {
            var column = MakeCol(100, 100, 1, new[] { 2, 0, 3 });

            PatternColumnPool.Signature(column).Should().Be(-3064470450015899442L);
        }

        [Test]
        public void Signature_DifferentCountsDifferentHash()
        {
            var a = MakeCol(100, 100, 1, new[] { 2, 0, 3 });
            var b = MakeCol(100, 100, 1, new[] { 2, 1, 3 });
            PatternColumnPool.Signature(a).Should().NotBe(PatternColumnPool.Signature(b));
        }

        [Test]
        public void Signature_DifferentSheetDifferentHash()
        {
            var a = MakeCol(100, 100, 1, new[] { 2, 0 });
            var b = MakeCol(200, 100, 1, new[] { 2, 0 });
            PatternColumnPool.Signature(a).Should().NotBe(PatternColumnPool.Signature(b));
        }

        [Test]
        public void Signature_IgnoresSheetQuantity()
        {
            // Two columns for the same sheet *size* should collapse even if the sheet
            // objects report different Quantity — the master sees only sheet dimensions.
            var a = MakeCol(100, 100, 1, new[] { 2 });
            var b = MakeCol(100, 100, 9, new[] { 2 });
            PatternColumnPool.Signature(a).Should().Be(PatternColumnPool.Signature(b));
        }

        // ----- AddIfNew -----

        [Test]
        public void AddIfNew_PreventsDuplicates()
        {
            var list = new List<PatternColumn>();
            var sigs = new HashSet<long>();

            PatternColumnPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 1, 2 })).Should().BeTrue();
            PatternColumnPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 1, 2 })).Should().BeFalse();
            PatternColumnPool.AddIfNew(list, sigs, MakeCol(100, 100, 1, new[] { 2, 1 })).Should().BeTrue();

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

            var columns = new List<PatternColumn>(
                PatternPricing.PriceImprovingColumns(sheets, orders, pi, options, orderCount: 2));

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

            var columns = new List<PatternColumn>(
                PatternPricing.PriceImprovingColumns(sheets, orders, pi, options, orderCount: 1));
            columns.Should().BeEmpty();
        }

        [Test]
        public void BuildDpItems_FiltersNonFiniteAndSubEpsilonDuals()
        {
            var orders = new List<RectOrder>
            {
                new(10, 20, 1),
                new(20, 30, 1),
                new(30, 40, 1),
                new(40, 50, 1),
                new(50, 60, 1),
            };
            double[] duals =
            [
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                1e-6,
                1e-5,
            ];

            var items = PatternPricing.BuildDpItems(
                orders,
                duals,
                new SolverOptions2D { AllowRotation = false });

            items.Should().ContainSingle();
            items[0].OrderIndex.Should().Be(4);
            items[0].Profit.Should().Be(1e-5);
        }

        [Test]
        public void BuildDpItems_RequiresGlobalAndPerOrderRotationPermission()
        {
            var orders = new List<RectOrder>
            {
                new(10, 20, 1, allowRotation: true),
                new(30, 40, 1, allowRotation: false),
                new(50, 50, 1, allowRotation: true),
            };
            double[] duals = [1.0, 1.0, 1.0];

            var rotationEnabled = PatternPricing.BuildDpItems(
                orders,
                duals,
                new SolverOptions2D { AllowRotation = true });
            var rotationDisabled = PatternPricing.BuildDpItems(
                orders,
                duals,
                new SolverOptions2D { AllowRotation = false });

            rotationEnabled.Should().HaveCount(4);
            rotationEnabled.Should().ContainSingle(item => item.Rotated);
            rotationEnabled.Single(item => item.Rotated).OrderIndex.Should().Be(0);
            rotationDisabled.Should().HaveCount(3);
            rotationDisabled.Should().OnlyContain(item => !item.Rotated);
        }

        [Test]
        public void PriceImprovingColumns_CancellationStopsBeforePricing()
        {
            int cancellationChecks = 0;

            var columns = PatternPricing.PriceImprovingColumns(
                    [new Sheet(200, 200, 1)],
                    [new RectOrder(20, 20, 1)],
                    [1e6],
                    new SolverOptions2D(),
                    orderCount: 1,
                    cancel: () =>
                    {
                        cancellationChecks++;
                        return true;
                    })
                .ToList();

            columns.Should().BeEmpty();
            cancellationChecks.Should().Be(1);
        }

        // ----- LP master -----

        [Test]
        public void SolveLpMaster_BasicTwoColumnInstance()
        {
            // Two identical columns (each covers one of two orders), demand = 3 each.
            // Optimum: x1 = 3, x2 = 3 (total 6 sheets × area).
            var columns = new List<PatternColumn>
            {
                new() { Sheet = new Sheet(100, 100, 10), Counts = new[] { 1, 0 } },
                new() { Sheet = new Sheet(100, 100, 10), Counts = new[] { 0, 1 } },
            };
            var demand = new[] { 3, 3 };

            PatternMasterLp.Solve(columns, demand, out var x, out var pi).Should().BeTrue();
            x.Should().HaveCount(2);
            x[0].Should().BeApproximately(3.0, 1e-6);
            x[1].Should().BeApproximately(3.0, 1e-6);
            // Dual of each demand constraint equals the per-unit sheet cost.
            pi[0].Should().BeApproximately(100 * 100, 1e-3);
            pi[1].Should().BeApproximately(100 * 100, 1e-3);
        }

        [Test]
        public void SolveLpMaster_InfeasibleDemand_ReturnsFalseAndEmptyOutputs()
        {
            var columns = new List<PatternColumn>
            {
                new() { Sheet = new Sheet(100, 100, 1), Counts = new[] { 1, 0 } },
            };

            bool solved = PatternMasterLp.Solve(
                columns,
                demand: [1, 1],
                out double[] multiplicities,
                out double[] duals);

            solved.Should().BeFalse();
            multiplicities.Should().BeEmpty();
            duals.Should().BeEmpty();
        }

        [Test]
        public void Materializer_FromPatternAndToPattern_ClonePlacements()
        {
            var sourcePlacement = new Placement
            {
                OrderIndex = 1,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40,
                Rotated = true,
            };
            var source = new CuttingPattern2D
            {
                Sheet = new Sheet(100, 100, 3),
                Multiplicity = 1,
                Placements = new List<Placement> { sourcePlacement },
            };

            PatternColumn column = PatternMaterializer.FromPattern(source, orderCount: 2);
            CuttingPattern2D materialized = PatternMaterializer.ToPattern(column, multiplicity: 2);

            column.Counts.Should().Equal(0, 1);
            column.Placements[0].Should().NotBeSameAs(sourcePlacement);
            materialized.Sheet.Should().BeSameAs(column.Sheet);
            materialized.Multiplicity.Should().Be(2);
            materialized.Placements[0].Should().NotBeSameAs(column.Placements[0]);
            materialized.Placements[0].Should().BeEquivalentTo(sourcePlacement);
        }

        [Test]
        public void Materializer_FromDpResult_AppliesTrimOffset()
        {
            var dpResult = new GuillotineKnapsackDp.Result
            {
                Placements = new List<Placement>
                {
                    new()
                    {
                        OrderIndex = 0,
                        X = 1,
                        Y = 2,
                        Width = 30,
                        Height = 40,
                    },
                },
            };

            PatternColumn column = PatternMaterializer.FromDpResult(
                new Sheet(100, 100, 1),
                dpResult,
                orderCount: 1,
                trim: 5);

            column.Counts.Should().Equal(1);
            column.Placements.Should().ContainSingle();
            column.Placements[0].X.Should().Be(6);
            column.Placements[0].Y.Should().Be(7);
        }

        [Test]
        public void Materializer_ToPatterns_SkipsZeroAndDeepClonesPlacements()
        {
            var skipped = MakeCol(100, 100, 1, new[] { 1 });
            var sourcePlacement = new Placement
            {
                OrderIndex = 0,
                Width = 10,
                Height = 20,
            };
            var included = MakeCol(200, 100, 1, new[] { 1 });
            included.Placements.Add(sourcePlacement);

            List<CuttingPattern2D> patterns = PatternMaterializer.ToPatterns(
                [skipped, included],
                [0, 2]);

            patterns.Should().ContainSingle();
            patterns[0].Sheet.Should().BeSameAs(included.Sheet);
            patterns[0].Multiplicity.Should().Be(2);
            patterns[0].Placements.Should().ContainSingle();
            patterns[0].Placements[0].Should().NotBeSameAs(sourcePlacement);
            patterns[0].Placements[0].Should().BeEquivalentTo(sourcePlacement);
        }
    }
}
