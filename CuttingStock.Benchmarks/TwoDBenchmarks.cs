using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Benchmarks
{
    /// <summary>
    /// 2D guillotine cutting-stock solver benchmarks.
    /// Three scenarios (Small / Medium / Large) × three solvers.
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class TwoDBenchmarks
    {
        // ---- inputs ----
        private List<Sheet> _smallSheets = null!;
        private List<RectOrder> _smallOrders = null!;
        private List<Sheet> _medSheets = null!;
        private List<RectOrder> _medOrders = null!;
        private List<Sheet> _largeSheets = null!;
        private List<RectOrder> _largeOrders = null!;
        private SolverOptions2D _opts = null!;

        [GlobalSetup]
        public void Setup()
        {
            _opts = new SolverOptions2D { Kerf = 3, Trim = 5, AllowRotation = true, TimeLimitMs = 10000 };

            // Small: 1 sheet type, 3 order types, ~10 items
            _smallSheets = new() { new Sheet(2440, 1220, 3) };
            _smallOrders = new()
            {
                new RectOrder(600, 400, 3),
                new RectOrder(800, 300, 2),
                new RectOrder(300, 300, 5),
            };

            // Medium: 2 sheet types, 6 order types, ~30 items
            _medSheets = new()
            {
                new Sheet(2440, 1220, 6),
                new Sheet(1220, 1220, 6),
            };
            _medOrders = new()
            {
                new RectOrder(600, 400, 6),
                new RectOrder(800, 300, 4),
                new RectOrder(300, 300, 8),
                new RectOrder(1200, 500, 2),
                new RectOrder(400, 200, 5),
                new RectOrder(500, 500, 3),
            };

            // Large: 2 sheet types, 10 order types, ~80 items
            _largeSheets = new()
            {
                new Sheet(3000, 1500, 15),
                new Sheet(2440, 1220, 15),
            };
            _largeOrders = new()
            {
                new RectOrder(600, 400, 10),
                new RectOrder(800, 300, 8),
                new RectOrder(300, 300, 12),
                new RectOrder(1200, 500, 4),
                new RectOrder(400, 200, 8),
                new RectOrder(500, 500, 6),
                new RectOrder(700, 350, 5),
                new RectOrder(250, 250, 10),
                new RectOrder(1000, 400, 3),
                new RectOrder(450, 150, 8),
            };
        }

        // ---- Shelf ----
        [Benchmark(Description = "Shelf (Small)")]
        public SolverResult2D Shelf_Small() => new ShelfGuillotineSolver().Solve(_smallSheets, _smallOrders, _opts);

        [Benchmark(Description = "Shelf (Medium)")]
        public SolverResult2D Shelf_Medium() => new ShelfGuillotineSolver().Solve(_medSheets, _medOrders, _opts);

        [Benchmark(Description = "Shelf (Large)")]
        public SolverResult2D Shelf_Large() => new ShelfGuillotineSolver().Solve(_largeSheets, _largeOrders, _opts);

        // ---- CG2D ----
        [Benchmark(Description = "CG2D (Small)")]
        public SolverResult2D CG_Small() => new ColumnGeneration2DSolver().Solve(_smallSheets, _smallOrders, _opts);

        [Benchmark(Description = "CG2D (Medium)")]
        public SolverResult2D CG_Medium() => new ColumnGeneration2DSolver().Solve(_medSheets, _medOrders, _opts);

        [Benchmark(Description = "CG2D (Large)")]
        public SolverResult2D CG_Large() => new ColumnGeneration2DSolver().Solve(_largeSheets, _largeOrders, _opts);

        // ---- MIP ----
        [Benchmark(Description = "MIP (Small)")]
        public SolverResult2D MIP_Small() => new StagedMipGuillotineSolver().Solve(_smallSheets, _smallOrders, _opts);

        [Benchmark(Description = "MIP (Medium)")]
        public SolverResult2D MIP_Medium() => new StagedMipGuillotineSolver().Solve(_medSheets, _medOrders, _opts);

        [Benchmark(Description = "MIP (Large)")]
        public SolverResult2D MIP_Large() => new StagedMipGuillotineSolver().Solve(_largeSheets, _largeOrders, _opts);
    }
}
