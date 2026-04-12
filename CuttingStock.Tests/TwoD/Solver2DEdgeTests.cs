using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Edge cases for all three 2D solvers: rotation flags, infeasibility,
    /// extreme aspect ratios, kerf, trim, and demand boundary conditions.
    /// </summary>
    [TestFixture]
    public class Solver2DEdgeTests
    {
        private static IEnumerable<ICuttingSolver2D> AllSolvers()
        {
            yield return new ShelfGuillotineSolver();
            yield return new ColumnGeneration2DSolver();
            yield return new StagedMipGuillotineSolver();
        }

        // ----- rotation -----

        [TestCaseSource(nameof(AllSolvers))]
        public void GlobalRotationOff_NoItemRotated(ICuttingSolver2D solver)
        {
            // Item 40×100 in a 100×100 sheet — fits without rotation. With rotation off
            // the solver MUST NOT rotate it.
            var sheets = new List<Sheet> { new(100, 100, 1) };
            var orders = new List<RectOrder> { new(40, 100, 1, allowRotation: true) };
            var options = new SolverOptions2D { TimeLimitMs = 5000, AllowRotation = false };

            var r = solver.Solve(sheets, orders, options);

            r.Success.Should().BeTrue();
            r.Patterns.SelectMany(p => p.Placements).Any(p => p.Rotated).Should().BeFalse();
        }

        [TestCaseSource(nameof(AllSolvers))]
        public void PerItemRotationOff_NoItemRotatedEvenIfGlobalOn(ICuttingSolver2D solver)
        {
            // 100×40 sheet, item 40×100 — only fits rotated. AllowRotation=false at item
            // level → infeasible.
            var sheets = new List<Sheet> { new(100, 40, 1) };
            var orders = new List<RectOrder> { new(40, 100, 1, allowRotation: false) };
            var options = new SolverOptions2D { TimeLimitMs = 3000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeFalse(); // cannot place
        }

        // ----- infeasibility (item never fits) -----

        [TestCaseSource(nameof(AllSolvers))]
        public void ItemLargerThanAnySheet_ReportsFailure(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(100, 100, 5) };
            var orders = new List<RectOrder> { new(200, 200, 1, allowRotation: true) };
            var options = new SolverOptions2D { TimeLimitMs = 3000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeFalse();
            r.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        // ----- single-item sheet -----

        [TestCaseSource(nameof(AllSolvers))]
        public void SquareItem_RotationDoesNotMatter(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(100, 100, 1) };
            var orders = new List<RectOrder> { new(50, 50, 4, allowRotation: true) };
            var options = new SolverOptions2D { TimeLimitMs = 3000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            // For squares the rotated flag is meaningless and should be false (canonical).
            r.Patterns.SelectMany(p => p.Placements).Any(p => p.Rotated).Should().BeFalse();
        }

        // ----- 1×1 items (extreme density) -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Tile1x1Items_AllPlaced(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(10, 10, 1) };
            var orders = new List<RectOrder> { new(1, 1, 100) };
            var options = new SolverOptions2D { TimeLimitMs = 8000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(100);
            r.MaterialEfficiency.Should().Be(100.0);
        }

        // ----- extreme aspect ratio -----

        [TestCaseSource(nameof(AllSolvers))]
        public void LongStripItems_FitInWideSheet(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(2000, 100, 1) };
            var orders = new List<RectOrder> { new(200, 100, 10, allowRotation: false) };
            var options = new SolverOptions2D { TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(10);
            r.MaterialEfficiency.Should().Be(100.0);
        }

        // ----- exact-tile -----

        [TestCaseSource(nameof(AllSolvers))]
        public void ExactGridTile_NoWaste(ICuttingSolver2D solver)
        {
            // 3×3 grid of 100×100 in a 300×300 sheet.
            var sheets = new List<Sheet> { new(300, 300, 1) };
            var orders = new List<RectOrder> { new(100, 100, 9) };
            var options = new SolverOptions2D { TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(9);
            r.TotalWasteArea.Should().Be(0);
            r.MaterialEfficiency.Should().Be(100.0);
        }

        // ----- kerf -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Kerf_PlacementsRespectKerfBetweenAdjacent(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(1000, 1000, 1) };
            var orders = new List<RectOrder> { new(450, 450, 4) };
            var options = new SolverOptions2D { Kerf = 5, TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            // Validate kerf-aware no-overlap (every pair has at least kerf gap or touches sheet edge)
            foreach (var pat in r.Patterns)
                SolverUtils2D.HasOverlap(pat.Placements, options.Kerf).Should().BeFalse();
        }

        [TestCaseSource(nameof(AllSolvers))]
        public void Kerf_DenseTileRequiresMoreSheetsWhenKerfTooBig(ICuttingSolver2D solver)
        {
            // 100×100 sheet, items 50×50 — without kerf, 4 fit per sheet.
            // With kerf=10, each row needs 50+10+50=110 > 100 → only 1 per row, 1 per col → 1 per sheet.
            var sheets = new List<Sheet> { new(100, 100, 10) };
            var orders = new List<RectOrder> { new(50, 50, 4) };
            var options = new SolverOptions2D { Kerf = 10, TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            r.SheetsUsed.Should().BeGreaterThanOrEqualTo(4);
        }

        // ----- trim -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Trim_ReducesUsableArea_StillFeasible(ICuttingSolver2D solver)
        {
            // 110×110 sheet with trim=5 → effective 100×100. Fits 4 of 50×50.
            var sheets = new List<Sheet> { new(110, 110, 1) };
            var orders = new List<RectOrder> { new(50, 50, 4) };
            var options = new SolverOptions2D { Trim = 5, TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            // Every placement must respect the trim margin.
            foreach (var pat in r.Patterns)
                SolverUtils2D.WithinSheet(pat.Placements, pat.Sheet, options.Trim).Should().BeTrue();
        }

        [TestCaseSource(nameof(AllSolvers))]
        public void Trim_ExceedingHalfSheet_Infeasible(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(100, 100, 1) };
            var orders = new List<RectOrder> { new(10, 10, 1) };
            var options = new SolverOptions2D { Trim = 60, TimeLimitMs = 3000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeFalse();
        }

        // ----- multi-sheet selection -----

        [TestCaseSource(nameof(AllSolvers))]
        public void MultipleSheetTypes_PrefersAppropriateSheet(ICuttingSolver2D solver)
        {
            // Big and small sheet types — small items should ideally use small sheet.
            var sheets = new List<Sheet>
            {
                new(2000, 2000, 5),
                new(200, 200, 5),
            };
            var orders = new List<RectOrder> { new(100, 100, 4) };
            var options = new SolverOptions2D { TimeLimitMs = 5000, UsageOrder = CuttingStock.Core.Domain.StockUsageOrder.SmallToLarge };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            // Should not waste a 2000×2000 sheet for 4 small items when small sheets exist.
            r.Patterns.Should().NotContain(p => p.Sheet.Width == 2000);
        }

        // ----- demand exact match -----

        [TestCaseSource(nameof(AllSolvers))]
        public void NoOverproduction(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(2440, 1220, 5) };
            var orders = new List<RectOrder>
            {
                new(600, 400, 6),
                new(800, 300, 4),
                new(300, 300, 8),
            };
            var options = new SolverOptions2D { TimeLimitMs = 8000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            for (int i = 0; i < orders.Count; i++)
            {
                int placed = CountPlaced(r, i);
                placed.Should().Be(orders[i].Quantity, "solver {0} must not over- or under-produce order {1}", solver.Name, i);
            }
        }

        // ----- placements only reference valid order indices -----

        [TestCaseSource(nameof(AllSolvers))]
        public void OnlyValidOrderIndicesAppear(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(1000, 1000, 3) };
            var orders = new List<RectOrder>
            {
                new(200, 200, 4),
                new(300, 100, 6),
            };
            var options = new SolverOptions2D { TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            foreach (var pat in r.Patterns)
            foreach (var pl in pat.Placements)
            {
                pl.OrderIndex.Should().BeInRange(0, orders.Count - 1);
            }
        }

        // ----- placement dimensions match the order (rotation handled) -----

        [TestCaseSource(nameof(AllSolvers))]
        public void PlacementDimensionsMatchOrderOrRotation(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(2440, 1220, 3) };
            var orders = new List<RectOrder>
            {
                new(600, 400, 4, allowRotation: true),
                new(800, 200, 3, allowRotation: false),
            };
            var options = new SolverOptions2D { TimeLimitMs = 5000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();

            foreach (var pat in r.Patterns)
            foreach (var pl in pat.Placements)
            {
                var o = orders[pl.OrderIndex];
                bool matchAsIs   = pl.Width == o.Width && pl.Height == o.Height;
                bool matchRotate = pl.Width == o.Height && pl.Height == o.Width;
                (matchAsIs || matchRotate).Should().BeTrue(
                    "placement dims must match the order (or its rotation), got {0}×{1} for order {2} ({3}×{4})",
                    pl.Width, pl.Height, pl.OrderIndex, o.Width, o.Height);
                if (!o.AllowRotation)
                {
                    pl.Rotated.Should().BeFalse();
                    matchAsIs.Should().BeTrue();
                }
            }
        }

        // ----- helpers -----

        private static int CountPlaced(SolverResult2D r, int orderIdx)
        {
            int s = 0;
            foreach (var pat in r.Patterns)
                s += pat.Placements.Count(pl => pl.OrderIndex == orderIdx) * pat.Multiplicity;
            return s;
        }
    }
}
