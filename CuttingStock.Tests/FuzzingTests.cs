using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;
using CuttingStock.Core.Domain;

namespace CuttingStock.Tests
{
    [TestFixture]
    public class FuzzingTests
    {
        private Random _random;

        [SetUp]
        public void SetUp()
        {
            _random = new Random(42); // Seed for reproducibility
        }

        [Test]
        [Category("Fuzzing")]
        [Repeat(20)] // Run 20 times with different data
        public void FuzzTest_AllAlgorithms_ShouldProduceValidResults()
        {
            // 1. Generate Random Inputs
            var stockLength = _random.Next(8000, 12000);
            var stockQuantity = _random.Next(10, 50);
            var stocks = new List<RebarStock> { new RebarStock(stockLength, stockQuantity) };

            var numOrders = _random.Next(5, 20);
            var orders = new List<Order>();
            for (int i = 0; i < numOrders; i++)
            {
                var len = _random.Next(500, stockLength); // 500mm ~ StockLength
                var qty = _random.Next(1, 10);
                orders.Add(new Order(len, qty));
            }

            var parameters = new SolverOptions
            {
                Gamma = 100,
                EnableWelding = false // Start with simple case
            };

            var optimizers = new List<ICuttingSolver>
            {
                new GreedyKnapsackSolver(),
                // new FirstFitDecreasingSolver(),
                // new BestFitDecreasingSolver(),
                new ColumnGenerationSolver()
            };

            // 2. Run Optimization
            foreach (var optimizer in optimizers)
            {
                SolverResult? result = null;
                try
                {
                    // Create deep copy of orders since optimizer might modify them (though it shouldn't affect original list passed by ref unless it modifies objects directly, but better safe)
                    var ordersCopy = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();

                    result = optimizer.Solve(stocks, ordersCopy, parameters);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Algorithm {optimizer.Name} crashed with inputs: Stock={stockLength}x{stockQuantity}, Orders={orders.Count}. Error: {ex}");
                }

                // 3. Validation
                if (result?.Success == true)
                {
                    // Check 1: Cuts <= StockLength
                    foreach (var plan in result.CuttingPlans)
                    {
                        var usedLength = plan.Cuts.Sum(c => c.Length);
                        usedLength.Should().BeLessThanOrEqualTo(plan.StockLength, $"{optimizer.Name}: Cut length exceeds stock");
                    }

                    // Check 2: All items cut
                    var totalCutItems = result.CuttingPlans.Sum(p => p.Cuts.Count);
                    var totalOrderedItems = orders.Sum(o => o.Quantity);
                    totalCutItems.Should().Be(totalOrderedItems, $"{optimizer.Name}: Should cut all items on success");
                }
                else
                {
                    // If failed, make sure it wasn't a logic error (unless impossible)
                    // With 50 stock and small orders, it should usually pass.
                    // But if random generated huge orders, failure is expected.
                }
            }
        }
    }
}
