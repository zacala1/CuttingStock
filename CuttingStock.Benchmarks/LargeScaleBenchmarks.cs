using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Benchmarks
{
    /// <summary>
    /// Long-running throughput benchmark kept outside the correctness test suite.
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class LargeScaleBenchmarks
    {
        private List<RebarStock> _stocks = null!;
        private List<Order> _orders = null!;
        private SolverOptions _options = null!;

        [GlobalSetup]
        public void Setup()
        {
            _stocks = [new RebarStock(12000, 1000)];

            var random = new Random(12345);
            _orders = Enumerable.Range(0, 1000)
                .Select(_ => new Order(random.Next(1000, 8000), 1))
                .ToList();

            _options = new SolverOptions();

            var validationResult = new GreedyKnapsackSolver().Solve(_stocks, _orders, _options);
            if (!validationResult.Success)
            {
                throw new InvalidOperationException(
                    $"Large-scale benchmark input must be solvable: {validationResult.ErrorMessage}");
            }
        }

        [Benchmark(Description = "Greedy (1000 random orders)")]
        public SolverResult Greedy_1000Orders()
        {
            var solver = new GreedyKnapsackSolver();
            return solver.Solve(_stocks, _orders, _options);
        }
    }
}
