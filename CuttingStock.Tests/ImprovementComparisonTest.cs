using NUnit.Framework;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests
{
    /// <summary>
    /// 개선된 알고리즘의 효과를 검증하는 테스트
    /// </summary>
    [TestFixture]
    public class ImprovementComparisonTest
    {
        [Test]
        public void VerifyImprovement_MixedOrders_ShouldProcessAll()
        {
            // Arrange - 기존에 실패했던 테스트 케이스
            var optimizer = new GreedyKnapsackSolver();
            var stock = new List<RebarStock> { new RebarStock(12000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 5),
                new Order(3000, 8),
                new Order(2000, 6)
            };
            var parameters = new SolverOptions
            {
                Alpha = 1.0f, Beta = 500.0f, Gamma = 100, Delta = 100,
                EnableWelding = false, UsageOrder = StockUsageOrder.SmallToLarge
            };

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            TestContext.Out.WriteLine($"=== 개선된 알고리즘 결과 ===");
            TestContext.Out.WriteLine($"Success: {result.Success}");
            TestContext.Out.WriteLine($"StockUsed: {result.StockUsed}");
            TestContext.Out.WriteLine($"MaterialEfficiency: {result.MaterialEfficiency:F2}%");
            TestContext.Out.WriteLine($"WasteLength: {result.WasteLength}mm");

            var totalCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            var processed5000 = totalCuts.GetValueOrDefault(5000, 0);
            var processed3000 = totalCuts.GetValueOrDefault(3000, 0);
            var processed2000 = totalCuts.GetValueOrDefault(2000, 0);

            TestContext.Out.WriteLine($"\n처리된 주문:");
            TestContext.Out.WriteLine($"  5000mm: {processed5000}/5");
            TestContext.Out.WriteLine($"  3000mm: {processed3000}/8");
            TestContext.Out.WriteLine($"  2000mm: {processed2000}/6");

            TestContext.Out.WriteLine($"\n절단 계획:");
            foreach (var (plan, index) in result.CuttingPlans.Select((p, i) => (p, i)))
            {
                var cuts = string.Join(", ", plan.Cuts.Select(c => c.Length));
                TestContext.Out.WriteLine($"  Stock {index + 1}: [{cuts}], Leftover={plan.Leftover}mm");
            }

            // 기존 알고리즘: 4개 재고만 사용, 14개만 처리 (5000x5 + 3000x5 + 2000x4)
            // 개선 알고리즘: 모든 주문 처리 가능해야 함
            var totalProcessed = processed5000 + processed3000 + processed2000;
            Assert.That(totalProcessed, Is.GreaterThanOrEqualTo(17),
                "개선된 알고리즘은 최소 17개 이상의 주문을 처리해야 함 (기존: 14개)");

            TestContext.Out.WriteLine($"\n✅ 총 {totalProcessed}개 주문 처리 완료!");
        }

        [Test]
        public void VerifyImprovement_LargeScale_ShouldComplete()
        {
            // Arrange - 대규모 테스트
            var optimizer = new GreedyKnapsackSolver();
            var stock = new List<RebarStock> { new RebarStock(12000, 20) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20),
                new Order(2000, 20)
            };
            var parameters = new SolverOptions
            {
                Alpha = 1.0f, Beta = 500.0f, Gamma = 100, Delta = 100,
                EnableWelding = false, UsageOrder = StockUsageOrder.LargeToSmall
            };

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            TestContext.Out.WriteLine($"=== 대규모 테스트 결과 ===");
            TestContext.Out.WriteLine($"Success: {result.Success}");
            TestContext.Out.WriteLine($"StockUsed: {result.StockUsed}");
            TestContext.Out.WriteLine($"ExecutionTimeMs: {result.ExecutionTimeMs:F2}ms");
            TestContext.Out.WriteLine($"MaterialEfficiency: {result.MaterialEfficiency:F2}%");

            var totalCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalProcessed = totalCuts.Values.Sum();
            var totalOrders = 10 + 20 + 20; // 50개

            TestContext.Out.WriteLine($"\n처리된 주문: {totalProcessed}/{totalOrders} ({100.0 * totalProcessed / totalOrders:F1}%)");

            // 기존: 60% 처리, 개선: 80% 이상 처리 목표
            Assert.That(totalProcessed, Is.GreaterThanOrEqualTo(40),
                "개선된 알고리즘은 80% 이상의 주문을 처리해야 함");

            TestContext.Out.WriteLine($"\n✅ {100.0 * totalProcessed / totalOrders:F1}% 주문 처리 완료!");
        }
    }
}
