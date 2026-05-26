using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;

namespace CuttingStock.Benchmarks
{
    /// <summary>
    /// 알고리즘 품질 비교 벤치마크
    ///
    /// 성능뿐만 아니라 최적화 품질(비용, 효율)도 측정
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class QualityBenchmarks
    {
        private List<RebarStock> _testStock = null!;
        private List<Order> _testOrders = null!;
        private SolverOptions _defaultParams = null!;

        // 결과 저장용
        public long LastTotalCost { get; private set; }
        public long LastWasteLength { get; private set; }
        public int LastStockUsed { get; private set; }
        public double LastMaterialEfficiency { get; private set; }

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

            // 현실적인 테스트 케이스 (TC-007 기반)
            _testStock = new List<RebarStock>
            {
                new RebarStock(12000, 20)
            };
            _testOrders = new List<Order>
            {
                new Order(5000, 10),
                new Order(4000, 15),
                new Order(3000, 12),
                new Order(2000, 8)
            };
        }

        [Benchmark(Baseline = true, Description = "Greedy Knapsack")]
        public SolverResult GreedyKnapsack()
        {
            var optimizer = new GreedyKnapsackSolver();
            var result = optimizer.Solve(_testStock, _testOrders, _defaultParams);

            // 결과 저장
            LastTotalCost = result.TotalCost;
            LastWasteLength = result.WasteLength;
            LastStockUsed = result.StockUsed;
            LastMaterialEfficiency = result.MaterialEfficiency;

            return result;
        }

        // [Benchmark(Description = "First Fit Decreasing")]
        // public SolverResult FirstFitDecreasing()
        // {
        //     var optimizer = new FirstFitDecreasingSolver();
        //     var result = optimizer.Solve(_testStock, _testOrders, _defaultParams);
        //
        //     LastTotalCost = result.TotalCost;
        //     LastWasteLength = result.WasteLength;
        //     LastStockUsed = result.StockUsed;
        //     LastMaterialEfficiency = result.MaterialEfficiency;
        //
        //     return result;
        // }

        // [Benchmark(Description = "Best Fit Decreasing")]
        // public SolverResult BestFitDecreasing()
        // {
        //     var optimizer = new BestFitDecreasingSolver();
        //     var result = optimizer.Solve(_testStock, _testOrders, _defaultParams);
        //
        //     LastTotalCost = result.TotalCost;
        //     LastWasteLength = result.WasteLength;
        //     LastStockUsed = result.StockUsed;
        //     LastMaterialEfficiency = result.MaterialEfficiency;
        //
        //     return result;
        // }
    }

    /// <summary>
    /// 품질 지표를 출력하는 상세 벤치마크
    /// </summary>
    public class DetailedQualityComparison
    {
        public static void Run()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  알고리즘 품질 상세 비교");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            var stock = new List<RebarStock>
            {
                new RebarStock(12000, 20)
            };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(4000, 15),
                new Order(3000, 12),
                new Order(2000, 8)
            };
            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                UsageOrder = StockUsageOrder.SmallToLarge
            };

            var optimizers = new List<ICuttingSolver>
            {
                new GreedyKnapsackSolver(),
                // new FirstFitDecreasingSolver(),
                // new BestFitDecreasingSolver()
            };

            Console.WriteLine($"{"알고리즘",-30} {"비용",8} {"낭비",8} {"재고",6} {"효율",8} {"시간(ms)",10}");
            Console.WriteLine(new string('-', 80));

            foreach (var optimizer in optimizers)
            {
                var result = optimizer.Solve(stock, orders, parameters);

                Console.WriteLine(
                    $"{optimizer.Name,-30} " +
                    $"{result.TotalCost,8:N0} " +
                    $"{result.WasteLength,8:N0} " +
                    $"{result.StockUsed,6} " +
                    $"{result.MaterialEfficiency,7:F2}% " +
                    $"{result.ExecutionTimeMs,10:F3}"
                );
            }

            Console.WriteLine();
            Console.WriteLine("총 주문: {0}개 (5000×10, 4000×15, 3000×12, 2000×8)",
                orders.Sum(o => o.Quantity));
            Console.WriteLine("총 필요 길이: {0:N0}mm",
                orders.Sum(o => o.Length * o.Quantity));
            Console.WriteLine();
        }
    }
}
