using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;

namespace CuttingStock.Benchmarks
{
    /// <summary>
    /// 알고리즘 성능 벤치마크
    ///
    /// 실행 방법:
    /// dotnet run -c Release --project CuttingStock.Benchmarks
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class AlgorithmBenchmarks
    {
        private List<RebarStock> _smallStock = null!;
        private List<Order> _smallOrders = null!;

        private List<RebarStock> _mediumStock = null!;
        private List<Order> _mediumOrders = null!;

        private List<RebarStock> _largeStock = null!;
        private List<Order> _largeOrders = null!;

        private SolverOptions _defaultParams = null!;

        [GlobalSetup]
        public void Setup()
        {
            _defaultParams = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                UsageOrder = StockUsageOrder.SmallToLarge
            };

            // Small Scale (소규모): 재고 10개, 주문 30개
            _smallStock = new List<RebarStock>
            {
                new RebarStock(12000, 10)
            };
            _smallOrders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20)
            };

            // Medium Scale (중규모): 재고 50개, 주문 80개
            _mediumStock = new List<RebarStock>
            {
                new RebarStock(12000, 50)
            };
            _mediumOrders = new List<Order>
            {
                new Order(5000, 20),
                new Order(4000, 20),
                new Order(3000, 20),
                new Order(2000, 20)
            };

            // Large Scale (대규모): 재고 100개, 주문 200개
            _largeStock = new List<RebarStock>
            {
                new RebarStock(12000, 100)
            };
            _largeOrders = new List<Order>
            {
                new Order(5000, 50),
                new Order(4000, 50),
                new Order(3000, 50),
                new Order(2000, 50)
            };
        }

        #region Small Scale Benchmarks

        [Benchmark(Description = "Greedy (Small)")]
        public SolverResult GreedyKnapsack_Small()
        {
            var optimizer = new GreedyKnapsackSolver();
            return optimizer.Solve(_smallStock, _smallOrders, _defaultParams);
        }

        // [Benchmark(Description = "FFD (Small)")]
        // public SolverResult FirstFitDecreasing_Small()
        // {
        //     var optimizer = new FirstFitDecreasingSolver();
        //     return optimizer.Solve(_smallStock, _smallOrders, _defaultParams);
        // }

        // [Benchmark(Description = "BFD (Small)")]
        // public SolverResult BestFitDecreasing_Small()
        // {
        //     var optimizer = new BestFitDecreasingSolver();
        //     return optimizer.Solve(_smallStock, _smallOrders, _defaultParams);
        // }

        #endregion

        #region Medium Scale Benchmarks

        [Benchmark(Description = "Greedy (Medium)")]
        public SolverResult GreedyKnapsack_Medium()
        {
            var optimizer = new GreedyKnapsackSolver();
            return optimizer.Solve(_mediumStock, _mediumOrders, _defaultParams);
        }

        // [Benchmark(Description = "FFD (Medium)")]
        // public SolverResult FirstFitDecreasing_Medium()
        // {
        //     var optimizer = new FirstFitDecreasingSolver();
        //     return optimizer.Solve(_mediumStock, _mediumOrders, _defaultParams);
        // }

        // [Benchmark(Description = "BFD (Medium)")]
        // public SolverResult BestFitDecreasing_Medium()
        // {
        //     var optimizer = new BestFitDecreasingSolver();
        //     return optimizer.Solve(_mediumStock, _mediumOrders, _defaultParams);
        // }

        #endregion

        #region Large Scale Benchmarks

        [Benchmark(Description = "Greedy (Large)")]
        public SolverResult GreedyKnapsack_Large()
        {
            var optimizer = new GreedyKnapsackSolver();
            return optimizer.Solve(_largeStock, _largeOrders, _defaultParams);
        }

        // [Benchmark(Description = "FFD (Large)")]
        // public SolverResult FirstFitDecreasing_Large()
        // {
        //     var optimizer = new FirstFitDecreasingSolver();
        //     return optimizer.Solve(_largeStock, _largeOrders, _defaultParams);
        // }

        // [Benchmark(Description = "BFD (Large)")]
        // public SolverResult BestFitDecreasing_Large()
        // {
        //     var optimizer = new BestFitDecreasingSolver();
        //     return optimizer.Solve(_largeStock, _largeOrders, _defaultParams);
        // }

        #endregion
    }
}
