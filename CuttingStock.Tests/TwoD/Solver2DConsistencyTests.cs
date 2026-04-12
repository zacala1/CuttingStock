using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Cross-solver consistency: the column-generation and integer-master solvers must
    /// never produce a strictly worse solution than the shelf heuristic on the same input,
    /// because both warm-start from the heuristic. The integer master should also tie
    /// or beat the LP-rounded CG solver in expectation.
    /// </summary>
    [TestFixture]
    public class Solver2DConsistencyTests
    {
        // Allow tiny numerical slack — both CG and MIP have warm-start = heuristic, so
        // their cost should be ≤ heuristic cost. We allow at most 0 mm² regression.
        private const long MaxRegression = 0;

        [TestCase("compact",
            new[] { 1000, 1000, 5 },
            new[] { 200, 200, 4,  300, 100, 6,  150, 150, 8 })]
        [TestCase("medium",
            new[] { 2440, 1220, 8 },
            new[] { 600, 400, 6,  800, 300, 4,  300, 300, 8,  1200, 500, 2 })]
        [TestCase("dense",
            new[] { 600, 600, 6 },
            new[] { 200, 100, 5,  150, 150, 6,  300, 200, 3 })]
        public void CG_NotWorseThanShelf(string name, int[] sheetSpec, int[] orderSpec)
        {
            var (sheets, orders, options) = Build(sheetSpec, orderSpec);

            var shelf = new ShelfGuillotineSolver().Solve(sheets, orders, options);
            var cg = new ColumnGeneration2DSolver().Solve(sheets, orders, options);

            shelf.Success.Should().BeTrue();
            cg.Success.Should().BeTrue();
            (cg.TotalWasteArea - shelf.TotalWasteArea).Should()
                .BeLessThanOrEqualTo(MaxRegression, "{0}: CG should not regress vs shelf", name);
        }

        [TestCase("compact",
            new[] { 1000, 1000, 5 },
            new[] { 200, 200, 4,  300, 100, 6,  150, 150, 8 })]
        [TestCase("medium",
            new[] { 2440, 1220, 8 },
            new[] { 600, 400, 6,  800, 300, 4,  300, 300, 8,  1200, 500, 2 })]
        [TestCase("dense",
            new[] { 600, 600, 6 },
            new[] { 200, 100, 5,  150, 150, 6,  300, 200, 3 })]
        public void MIP_NotWorseThanShelf(string name, int[] sheetSpec, int[] orderSpec)
        {
            var (sheets, orders, options) = Build(sheetSpec, orderSpec);

            var shelf = new ShelfGuillotineSolver().Solve(sheets, orders, options);
            var mip = new StagedMipGuillotineSolver().Solve(sheets, orders, options);

            shelf.Success.Should().BeTrue();
            mip.Success.Should().BeTrue();
            (mip.TotalWasteArea - shelf.TotalWasteArea).Should()
                .BeLessThanOrEqualTo(MaxRegression, "{0}: MIP should not regress vs shelf", name);
        }

        [Test]
        public void AllSolvers_AgreeOnTotalAreaProduced()
        {
            // Same input → all 3 solvers must produce exactly the demanded area
            // (since they exactly cover demand). Total used area is invariant.
            var sheets = new List<Sheet> { new(2440, 1220, 5) };
            var orders = new List<RectOrder>
            {
                new(600, 400, 6),
                new(800, 300, 4),
                new(300, 300, 8),
            };
            var options = new SolverOptions2D { TimeLimitMs = 8000 };

            long expected = 0;
            foreach (var o in orders) expected += o.Area * o.Quantity;

            foreach (ICuttingSolver2D s in new ICuttingSolver2D[]
            {
                new ShelfGuillotineSolver(),
                new ColumnGeneration2DSolver(),
                new StagedMipGuillotineSolver(),
            })
            {
                var r = s.Solve(sheets, orders, options);
                r.Success.Should().BeTrue();
                r.TotalUsedArea.Should().Be(expected, "{0}: total placed area must equal demand area", s.Name);
            }
        }

        [Test]
        public void Determinism_SameInputProducesSameResult()
        {
            // Heuristic + CG (no random elements) must be deterministic. MIP uses a fixed
            // RNG seed for diversification + a deterministic CBC, so it is also deterministic
            // for the same time budget.
            var sheets = new List<Sheet> { new(1000, 1000, 4) };
            var orders = new List<RectOrder> { new(300, 200, 5), new(150, 150, 8) };
            var options = new SolverOptions2D { TimeLimitMs = 5000 };

            foreach (ICuttingSolver2D s in new ICuttingSolver2D[]
            {
                new ShelfGuillotineSolver(),
                new ColumnGeneration2DSolver(),
                // MIP uses wall-clock time limit so its determinism depends on the host;
                // we exclude it from the strict determinism test.
            })
            {
                var r1 = s.Solve(sheets, orders, options);
                var r2 = s.Solve(sheets, orders, options);

                r1.Success.Should().BeTrue();
                r2.Success.Should().BeTrue();
                r1.TotalWasteArea.Should().Be(r2.TotalWasteArea, "{0} should be deterministic", s.Name);
                r1.SheetsUsed.Should().Be(r2.SheetsUsed);
            }
        }

        [Test]
        public void EmptyOrders_AllSolversReturnEmptySuccess()
        {
            var sheets = new List<Sheet> { new(1000, 1000, 1) };
            var orders = new List<RectOrder>();
            var options = new SolverOptions2D { TimeLimitMs = 1000 };

            foreach (ICuttingSolver2D s in new ICuttingSolver2D[]
            {
                new ShelfGuillotineSolver(),
                new ColumnGeneration2DSolver(),
                new StagedMipGuillotineSolver(),
            })
            {
                var r = s.Solve(sheets, orders, options);
                r.Success.Should().BeTrue();
                r.Patterns.Should().BeEmpty();
                r.SheetsUsed.Should().Be(0);
                r.TotalWasteArea.Should().Be(0);
                r.TotalCost.Should().Be(0);
            }
        }

        // ----- helpers -----

        private static (List<Sheet>, List<RectOrder>, SolverOptions2D) Build(int[] sheetSpec, int[] orderSpec)
        {
            var sheets = new List<Sheet>();
            for (int i = 0; i + 2 < sheetSpec.Length; i += 3)
                sheets.Add(new Sheet(sheetSpec[i], sheetSpec[i + 1], sheetSpec[i + 2]));

            var orders = new List<RectOrder>();
            for (int i = 0; i + 2 < orderSpec.Length; i += 3)
                orders.Add(new RectOrder(orderSpec[i], orderSpec[i + 1], orderSpec[i + 2]));

            var options = new SolverOptions2D { TimeLimitMs = 8000, AllowRotation = true };
            return (sheets, orders, options);
        }
    }
}
