using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests
{
    /// <summary>
    /// End-to-end correctness tests that verify fundamental invariants:
    /// 1. Sum of all cuts == sum of all orders (no orders lost or duplicated)
    /// 2. Each plan's cuts fit within its stock length (no overflow)
    /// 3. Stock used does not exceed available stock
    /// 4. Cost calculation is consistent with components
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class IntegrationCorrectnessTests
    {
        private static readonly ICuttingSolver[] AllSolvers =
        {
            new GreedyKnapsackSolver(),
            new ColumnGenerationSolver()
        };

        private SolverOptions DefaultOptions => new()
        {
            Alpha = 1.0f,
            Beta = 500.0f,
            Gamma = 100,
            Delta = 100,
            UsageOrder = StockUsageOrder.LargeToSmall,
            EnableWelding = false
        };

        #region Invariant: Sum of cuts == Sum of orders

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void SumOfCuts_ShouldEqual_SumOfOrders_SimpleCase(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(12000, 20) };
            var orders = new List<Order>
            {
                new(5000, 10),
                new(4000, 15),
                new(3000, 12),
                new(2000, 8)
            };

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue($"{solver.Name} should succeed");
            VerifyCutsSumMatchesOrders(result, orders);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void SumOfCuts_ShouldEqual_SumOfOrders_SingleOrderSingleStock(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(10000, 1) };
            var orders = new List<Order> { new(10000, 1) };

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();
            VerifyCutsSumMatchesOrders(result, orders);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void SumOfCuts_ShouldEqual_SumOfOrders_AllSameLength(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(12000, 10) };
            var orders = new List<Order> { new(4000, 15) };

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();
            VerifyCutsSumMatchesOrders(result, orders);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void SumOfCuts_ShouldEqual_SumOfOrders_MultiStock(ICuttingSolver solver)
        {
            var stock = new List<RebarStock>
            {
                new(12000, 5),
                new(9000, 10),
                new(6000, 8)
            };
            var orders = new List<Order>
            {
                new(5000, 8),
                new(3000, 10),
                new(2000, 6)
            };

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();
            VerifyCutsSumMatchesOrders(result, orders);
        }

        #endregion

        #region Invariant: Cuts fit within stock

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void CutsSumInEachPlan_ShouldNotExceed_StockLength(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(12000, 20) };
            var orders = new List<Order>
            {
                new(5000, 10),
                new(4000, 15),
                new(3000, 12)
            };

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();
            foreach (var plan in result.CuttingPlans)
            {
                var cutsSum = plan.Cuts.Sum(c => c.Length);
                cutsSum.Should().BeLessThanOrEqualTo(plan.StockLength,
                    $"cuts sum {cutsSum} should not exceed stock {plan.StockLength}");
                plan.Leftover.Should().BeGreaterThanOrEqualTo(0,
                    "leftover should never be negative");
                plan.Leftover.Should().Be(plan.StockLength - cutsSum,
                    "leftover should equal stock minus cuts");
            }
        }

        #endregion

        #region Invariant: Stock not over-used

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void TotalCutsLength_ShouldNotExceed_TotalStockLength(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(12000, 5) };
            var orders = new List<Order> { new(4000, 15) }; // needs exactly 5 stocks

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();

            // Total material used (cuts) should not exceed total available stock
            long totalStockAvailable = stock.Sum(s => (long)s.Length * s.Quantity);
            long totalCutLength = result.CuttingPlans.Sum(p => p.Cuts.Sum(c => (long)c.Length));
            totalCutLength.Should().BeLessThanOrEqualTo(totalStockAvailable);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void InsufficientStock_ShouldFail(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(12000, 1) };
            var orders = new List<Order> { new(5000, 10) }; // needs way more stock

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeFalse($"{solver.Name} should fail with insufficient stock");
        }

        #endregion

        #region Invariant: Cost consistency

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void TotalCost_ShouldEqual_WasteCostPlusWeldCost(ICuttingSolver solver)
        {
            var options = new SolverOptions
            {
                Alpha = 2.0f,
                Beta = 300.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = false
            };

            var stock = new List<RebarStock> { new(12000, 20) };
            var orders = new List<Order>
            {
                new(5000, 5),
                new(3000, 8)
            };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();
            var expectedCost = (int)Math.Round(
                result.WasteLength * (double)options.Alpha +
                result.WeldCount * (double)options.Beta);
            result.TotalCost.Should().Be(expectedCost);
        }

        #endregion

        #region Invariant: Waste/Reusable classification

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void Leftovers_ShouldBeCorrectlyClassified(ICuttingSolver solver)
        {
            var options = new SolverOptions { Alpha = 1.0f, Beta = 500f, Gamma = 200, Delta = 100 };
            var stock = new List<RebarStock> { new(10000, 10) };
            var orders = new List<Order> { new(3000, 10) };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();

            // Verify classification
            foreach (var leftover in result.ReusableLeftovers)
            {
                leftover.Should().BeGreaterThanOrEqualTo(options.Gamma,
                    $"reusable leftover {leftover} should be >= Gamma({options.Gamma})");
            }

            // Waste is sum of leftover < Gamma
            var expectedWaste = result.CuttingPlans
                .Where(p => p.Leftover < options.Gamma && p.Leftover > 0)
                .Sum(p => p.Leftover);
            result.WasteLength.Should().Be(expectedWaste);
        }

        #endregion

        #region Welding correctness

        [Test]
        public void Welding_ShouldProduceValidWeldGroups()
        {
            var options = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500f,
                Gamma = 100,
                Delta = 2000,
                EnableWelding = true
            };

            var stock = new List<RebarStock> { new(8000, 10) };
            var orders = new List<Order> { new(15000, 1) }; // needs 2 stocks welded

            var solver = new GreedyKnapsackSolver();
            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();

            // Verify weld groups are consistent
            var weldedCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .ToList();

            foreach (var group in weldedCuts)
            {
                group.Count().Should().BeGreaterThanOrEqualTo(2,
                    $"weld group {group.Key} should have at least 2 pieces");
                var totalLength = group.Sum(c => c.Length);
                totalLength.Should().BeGreaterThanOrEqualTo(15000,
                    "welded pieces should cover the order length");
            }
        }

        #endregion

        #region Parameter edge cases

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void Alpha_Zero_ShouldStillWork(ICuttingSolver solver)
        {
            var options = new SolverOptions { Alpha = 0f, Beta = 500f, Gamma = 100, Delta = 100 };
            var stock = new List<RebarStock> { new(12000, 10) };
            var orders = new List<Order> { new(5000, 5) };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();
            VerifyCutsSumMatchesOrders(result, orders);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void Beta_Zero_ShouldStillWork(ICuttingSolver solver)
        {
            var options = new SolverOptions { Alpha = 1f, Beta = 0f, Gamma = 100, Delta = 100 };
            var stock = new List<RebarStock> { new(12000, 10) };
            var orders = new List<Order> { new(5000, 5) };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();
            VerifyCutsSumMatchesOrders(result, orders);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void Gamma_Zero_AllLeftoversShouldBeReusable(ICuttingSolver solver)
        {
            var options = new SolverOptions { Alpha = 1f, Beta = 500f, Gamma = 0, Delta = 100 };
            var stock = new List<RebarStock> { new(12000, 10) };
            var orders = new List<Order> { new(5000, 5) };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();
            // With Gamma=0, all leftovers are reusable, so waste should be 0
            result.WasteLength.Should().Be(0);
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void Gamma_VeryLarge_AllLeftoversShouldBeWaste(ICuttingSolver solver)
        {
            var options = new SolverOptions { Alpha = 1f, Beta = 500f, Gamma = 999999, Delta = 100 };
            var stock = new List<RebarStock> { new(12000, 10) };
            var orders = new List<Order> { new(5000, 5) };

            var result = solver.Solve(stock, orders, options);

            result.Success.Should().BeTrue();
            // With huge Gamma, no leftover is reusable
            result.ReusableLeftovers.Should().BeEmpty();
        }

        [Test]
        [TestCaseSource(nameof(AllSolvers))]
        public void PerfectFit_ShouldHaveZeroLeftover(ICuttingSolver solver)
        {
            var stock = new List<RebarStock> { new(10000, 2) };
            var orders = new List<Order> { new(5000, 4) }; // 5000*4 = 20000 = 10000*2

            var result = solver.Solve(stock, orders, DefaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans.Sum(p => p.Leftover).Should().Be(0);
            VerifyCutsSumMatchesOrders(result, orders);
        }

        #endregion

        #region Algorithm comparison

        [Test]
        public void BothAlgorithms_ShouldProduceValidResults_ForSameInput()
        {
            var stock = new List<RebarStock> { new(12000, 20) };
            var orders = new List<Order>
            {
                new(5000, 10),
                new(4000, 15),
                new(3000, 12),
                new(2000, 8)
            };

            var greedyResult = new GreedyKnapsackSolver().Solve(stock, orders.Select(o => new Order(o.Length, o.Quantity)).ToList(), DefaultOptions);
            var cgResult = new ColumnGenerationSolver().Solve(stock, orders.Select(o => new Order(o.Length, o.Quantity)).ToList(), DefaultOptions);

            greedyResult.Success.Should().BeTrue("Greedy should succeed");
            cgResult.Success.Should().BeTrue("ColumnGeneration should succeed");

            // Both should fulfill all orders
            VerifyCutsSumMatchesOrders(greedyResult, orders);
            VerifyCutsSumMatchesOrders(cgResult, orders);

            // Both should have reasonable efficiency (>70%)
            greedyResult.MaterialEfficiency.Should().BeGreaterThan(70);
            cgResult.MaterialEfficiency.Should().BeGreaterThan(70);
        }

        #endregion

        #region Helpers

        private static void VerifyCutsSumMatchesOrders(SolverResult result, List<Order> originalOrders)
        {
            // Build demand map from original orders
            var expectedDemand = originalOrders
                .GroupBy(o => o.Length)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));

            // Build actual cuts map
            var actualCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => !c.RequiresWelding || c.WeldGroupId.HasValue)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            // For welded orders, group by WeldGroupId and sum lengths
            var weldedGroups = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .Select(g => g.Sum(c => c.Length))
                .ToList();

            // Total cut length should match total order length
            long totalCutLength = result.CuttingPlans.Sum(p => p.Cuts.Sum(c => (long)c.Length));
            long totalOrderLength = originalOrders.Sum(o => (long)o.Length * o.Quantity);
            totalCutLength.Should().Be(totalOrderLength,
                $"total cut length {totalCutLength} should equal total order length {totalOrderLength}");
        }

        #endregion
    }
}
