using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;
using CuttingStock.Core.Domain;

namespace CuttingStock.Tests
{
    [TestFixture]
    public class LargeScaleBenchmarks
    {
        [Test]
        [Category("Performance")]
        [Explicit("Long running benchmark")]
        public void Benchmark_LargeScale_1000_Orders()
        {
            // Arrange
            var stocks = new List<RebarStock> { new RebarStock(12000, 1000) };
            var orders = new List<Order>();
            var rnd = new Random(12345);
            
            for(int i=0; i<1000; i++)
            {
                orders.Add(new Order(rnd.Next(1000, 8000), 1));
            }

            var parameters = new SolverOptions();
            
            var optimizers = new List<ICuttingSolver>
            {
                // new FirstFitDecreasingSolver(),
                // new BestFitDecreasingSolver(),
                new GreedyKnapsackSolver()
            };

            Console.WriteLine("Algorithm | Time (ms) | Efficiency (%) | Cost");
            Console.WriteLine("---|---|---|---");

            foreach(var optimizer in optimizers)
            {
                var inputOrders = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                
                var sw = Stopwatch.StartNew();
                var result = optimizer.Solve(stocks, inputOrders, parameters);
                sw.Stop();

                Console.WriteLine($"{optimizer.Name} | {sw.ElapsedMilliseconds} | {result.MaterialEfficiency:F2} | {result.TotalCost}");

                result.Success.Should().BeTrue();
                sw.ElapsedMilliseconds.Should().BeLessThan(60000, $"{optimizer.Name} should finish within 60s for 1000 items");
            }
        }
    }
}
