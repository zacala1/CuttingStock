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
    /// End-to-end tests for the 2D solvers: ShelfGuillotine, TwoStageShelf,
    /// ColumnGeneration2D, and StagedMipGuillotine. Verifies coverage, no-overlap, sheet bounds, guillotine
    /// validity, and basic efficiency.
    /// </summary>
    [TestFixture]
    public class Solver2DTests
    {
        private static IEnumerable<ICuttingSolver2D> AllSolvers()
        {
            yield return new ShelfGuillotineSolver();
            yield return new TwoStageShelfGuillotineSolver();
            yield return new ColumnGeneration2DSolver();
            yield return new StagedMipGuillotineSolver();
        }

        // ----- 시나리오: 단일 시트, 정확히 채워지는 케이스 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_SingleSheet_ExactFill(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(100, 100, 1) };
            var orders = new List<RectOrder> { new(50, 50, 4) };
            var options = new SolverOptions2D { TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);

            r.Success.Should().BeTrue();
            r.Patterns.Should().NotBeEmpty();
            CountPlaced(r, 0).Should().Be(4);
            EveryPlacementWithinAndNoOverlap(r).Should().BeTrue();
            r.MaterialEfficiency.Should().Be(100.0);
        }

        // ----- 시나리오: 회전 필요 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_RotationAllowed_PacksMore(ICuttingSolver2D solver)
        {
            // 100×40 sheet, item 40×100 → 회전 필요.
            var sheets = new List<Sheet> { new(100, 40, 1) };
            var orders = new List<RectOrder> { new(40, 100, 1, allowRotation: true) };
            var options = new SolverOptions2D { TimeLimitMs = 5000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);

            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(1);
            r.Patterns.Sum(p => p.Placements.Count(pl => pl.Rotated)).Should().BeGreaterThan(0);
        }

        // ----- 시나리오: 멀티 시트, 멀티 아이템 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_MultiSheetMultiItem_AllOrdersPlaced(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet>
            {
                new(2440, 1220, 5),
                new(1220, 1220, 5),
            };
            var orders = new List<RectOrder>
            {
                new(600, 400, 6),
                new(800, 300, 4),
                new(300, 300, 8),
                new(1200, 500, 2),
            };
            var options = new SolverOptions2D { TimeLimitMs = 8000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);

            r.Success.Should().BeTrue();
            for (int i = 0; i < orders.Count; i++)
                CountPlaced(r, i).Should().Be(orders[i].Quantity, "order {0} fully placed", i);
            EveryPlacementWithinAndNoOverlap(r).Should().BeTrue();
            r.MaterialEfficiency.Should().BeGreaterThan(40.0);
        }

        // ----- 시나리오: kerf 적용 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_WithKerf_RespectsKerf(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(1000, 1000, 1) };
            var orders = new List<RectOrder> { new(450, 450, 4) };  // exact fit at kerf=0; needs kerf-aware to still cover
            var options = new SolverOptions2D { Kerf = 5, TimeLimitMs = 5000 };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            CountPlaced(r, 0).Should().Be(4);
            EveryPlacementWithinAndNoOverlap(r, options.Kerf).Should().BeTrue();
        }

        // ----- 시나리오: guillotine validity 보장 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_OutputIsGuillotineCompliant(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(1200, 800, 3) };
            var orders = new List<RectOrder>
            {
                new(400, 300, 4),
                new(500, 200, 3),
                new(200, 200, 6),
            };
            var options = new SolverOptions2D { TimeLimitMs = 6000, AllowRotation = true };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue();
            foreach (var pat in r.Patterns)
            {
                var rects = pat.Placements
                    .Select(p => (p.X, p.Y, p.Width, p.Height))
                    .ToList();
                GuillotineValidator
                    .IsGuillotineCompliant(0, 0, pat.Sheet.Width, pat.Sheet.Height, rects)
                    .Should().BeTrue("solver {0} must produce guillotine patterns", solver.Name);
            }
        }

        // ----- 시나리오: 빈 입력 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_NoOrders_ReturnsEmptySuccess(ICuttingSolver2D solver)
        {
            var sheets = new List<Sheet> { new(1000, 1000, 1) };
            var orders = new List<RectOrder>();
            var r = solver.Solve(sheets, orders, new SolverOptions2D { TimeLimitMs = 1000 });
            r.Success.Should().BeTrue();
            r.Patterns.Should().BeEmpty();
        }

        // ----- F5-B1 회귀: 같은 dim 시트가 여러 행에 분산되어도 인벤토리 합산됨 -----

        [TestCaseSource(nameof(AllSolvers))]
        public void Solver_DuplicateSheetDims_AggregatesQuantity(ICuttingSolver2D solver)
        {
            // 같은 1000×1000 시트가 두 행으로 입력 (qty 1 + qty 1 = 합계 2).
            // 1000×1000 시트에 600×600 + 400×400 까지만 들어가므로 한 시트당 최대 2개 배치.
            // 600×600 4개를 배치하려면 시트 2개가 필요 → 합산이 되어야 성공.
            var sheets = new List<Sheet>
            {
                new(1000, 1000, 1),
                new(1000, 1000, 1),
            };
            var orders = new List<RectOrder> { new(600, 600, 2) };
            var options = new SolverOptions2D { TimeLimitMs = 6000, AllowRotation = false };

            var r = solver.Solve(sheets, orders, options);
            r.Success.Should().BeTrue($"{solver.Name} should aggregate same-dim sheet inventories");
            CountPlaced(r, 0).Should().Be(2, $"{solver.Name} must cover full demand using both bars");
            r.SheetsUsed.Should().BeLessThanOrEqualTo(2);
        }

        // ----- helpers -----

        private static int CountPlaced(SolverResult2D r, int orderIdx)
        {
            int s = 0;
            foreach (var pat in r.Patterns)
                s += pat.Placements.Count(pl => pl.OrderIndex == orderIdx) * pat.Multiplicity;
            return s;
        }

        private static bool EveryPlacementWithinAndNoOverlap(SolverResult2D r, int kerf = 0)
        {
            foreach (var pat in r.Patterns)
            {
                if (!SolverUtils2D.WithinSheet(pat.Placements, pat.Sheet, 0)) return false;
                if (SolverUtils2D.HasOverlap(pat.Placements, kerf)) return false;
            }
            return true;
        }
    }
}
