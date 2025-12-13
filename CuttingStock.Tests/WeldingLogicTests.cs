using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;
using CuttingStock.Core.Domain;

namespace CuttingStock.Tests
{
    [TestFixture]
    [Category("Welding")]
    public class WeldingLogicTests
    {
        /// <summary>
        /// 용접 비활성화 시 긴 주문 처리 실패 확인
        /// </summary>
        [Test]
        public void WeldingDisabled_LongOrder_ShouldFail()
        {
            // Arrange
            var stock = new List<RebarStock>
            {
                new RebarStock(12000, 5) // 12m 재고
            };

            var orders = new List<Order>
            {
                new Order(15000, 1) // 15m 주문 (재고보다 김!)
            };

            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = false // 용접 비활성화
            };

            var optimizer = new GreedyKnapsackSolver();

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("용접 없이는 긴 주문을 처리할 수 없음");
            result.WeldCount.Should().Be(0, "용접이 비활성화되어 있음");
        }

        /// <summary>
        /// 용접 활성화 시 긴 주문 처리 성공 확인
        /// </summary>
        [Test]
        public void WeldingEnabled_LongOrder_ShouldSucceed()
        {
            // Arrange
            var stock = new List<RebarStock>
            {
                new RebarStock(12000, 5) // 12m 재고
            };

            var orders = new List<Order>
            {
                new Order(15000, 1) // 15m 주문 (재고보다 김!)
            };

            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 1000, // 용접 가능 최소 길이 1m
                EnableWelding = true // 용접 활성화
            };

            var optimizer = new GreedyKnapsackSolver();

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue("용접으로 긴 주문을 처리 가능");
            result.WeldCount.Should().BeGreaterThan(0, "용접이 발생해야 함");

            // 주문 총 길이 확인
            var totalCutLength = result.CuttingPlans.Sum(p => p.Cuts.Sum(c => c.Length));
            totalCutLength.Should().Be(15000, "15m 주문이 완전히 충족되어야 함");

            // 용접된 조각 확인
            var weldedCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .ToList();

            weldedCuts.Should().NotBeEmpty("용접된 조각이 있어야 함");
            weldedCuts.All(c => c.Length >= parameters.Delta).Should().BeTrue("모든 조각이 Delta 이상이어야 함");
        }

        /// <summary>
        /// 용접 횟수 정확성 확인
        /// </summary>
        [Test]
        public void WeldCount_ShouldBeCorrect()
        {
            // Arrange
            var stock = new List<RebarStock>
            {
                new RebarStock(6000, 10) // 6m 재고
            };

            var orders = new List<Order>
            {
                new Order(15000, 1) // 15m 주문 → 3조각 용접 (2회 용접)
            };

            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 3000, // 최소 3m 이상 조각만 용접 가능
                EnableWelding = true
            };

            var optimizer = new GreedyKnapsackSolver();

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            // 15000mm를 6000mm 조각으로 나누면: 6000 + 6000 + 3000 = 3조각
            // 용접 횟수 = 조각 수 - 1 = 2회
            result.WeldCount.Should().Be(2, "3조각을 용접하면 2회 필요");

            // WeldGroupId 확인
            var weldGroups = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .ToList();

            weldGroups.Should().HaveCount(1, "15000mm 주문 1개 = 용접 그룹 1개");
            weldGroups.First().Should().HaveCount(3, "3조각이 하나의 그룹");
        }

        // /// <summary>
        // /// FFD 알고리즘에서 용접 테스트
        // /// </summary>
        // [Test]
        // public void FFD_WeldingEnabled_ShouldWork()
        // {
        //     // Arrange
        //     var stock = new List<RebarStock>
        //     {
        //         new RebarStock(10000, 5)
        //     };
        //
        //     var orders = new List<Order>
        //     {
        //         new Order(5000, 2),   // 정상 주문
        //         new Order(18000, 1)   // 긴 주문 (용접 필요)
        //     };
        //
        //     var parameters = new SolverOptions
        //     {
        //         EnableWelding = true,
        //         Delta = 2000
        //     };
        //
        //     // var optimizer = new FirstFitDecreasingSolver();
        //
        //     // Act
        //     var result = optimizer.Solve(stock, orders, parameters);
        //
        //     // Assert
        //     result.Should().NotBeNull();
        //     result.Success.Should().BeTrue("FFD도 용접 지원");
        //     result.WeldCount.Should().BeGreaterThan(0);
        //
        //     var totalCutLength = result.CuttingPlans.Sum(p => p.Cuts.Sum(c => c.Length));
        //     totalCutLength.Should().Be(28000, "5000×2 + 18000 = 28000");
        // }

        // /// <summary>
        // /// BFD 알고리즘에서 용접 테스트
        // /// </summary>
        // [Test]
        // public void BFD_WeldingEnabled_ShouldWork()
        // {
        //     // Arrange
        //     var stock = new List<RebarStock>
        //     {
        //         new RebarStock(10000, 5)
        //     };
        //
        //     var orders = new List<Order>
        //     {
        //         new Order(5000, 2),   // 정상 주문
        //         new Order(18000, 1)   // 긴 주문 (용접 필요)
        //     };
        //
        //     var parameters = new SolverOptions
        //     {
        //         EnableWelding = true,
        //         Delta = 2000
        //     };
        //
        //     // var optimizer = new BestFitDecreasingSolver();
        //
        //     // Act
        //     var result = optimizer.Solve(stock, orders, parameters);
        //
        //     // Assert
        //     result.Should().NotBeNull();
        //     result.Success.Should().BeTrue("BFD도 용접 지원");
        //     result.WeldCount.Should().BeGreaterThan(0);
        //
        //     var totalCutLength = result.CuttingPlans.Sum(p => p.Cuts.Sum(c => c.Length));
        //     totalCutLength.Should().Be(28000, "5000×2 + 18000 = 28000");
        // }

        /// <summary>
        /// 용접 비용 계산 확인
        /// </summary>
        [Test]
        public void WeldCost_ShouldBeIncludedInTotalCost()
        {
            // Arrange
            var stock = new List<RebarStock>
            {
                new RebarStock(6000, 10)
            };

            var orders = new List<Order>
            {
                new Order(15000, 1) // 3조각 → 2회 용접
            };

            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f, // 용접 1회당 500원
                Gamma = 100,
                Delta = 3000,
                EnableWelding = true
            };

            var optimizer = new GreedyKnapsackSolver();

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            result.Should().NotBeNull();
            result.WeldCount.Should().Be(2);

            var weldCost = result.WeldCount * parameters.Beta;
            weldCost.Should().Be(1000, "2회 × 500원 = 1000원");

            result.TotalCost.Should().BeGreaterThanOrEqualTo((int)weldCost, "총 비용에 용접 비용 포함");
        }

        /// <summary>
        /// Delta 제약 위반 시 용접 실패
        /// </summary>
        [Test]
        public void WeldingWithLargeDelta_ShouldFailWhenPiecesTooSmall()
        {
            // Arrange
            var stock = new List<RebarStock>
            {
                new RebarStock(2000, 10) // 2m 재고
            };

            var orders = new List<Order>
            {
                new Order(5000, 1) // 5m 주문
            };

            var parameters = new SolverOptions
            {
                EnableWelding = true,
                Delta = 3000 // 최소 3m 이상만 용접 가능 (재고보다 큼!)
            };

            var optimizer = new GreedyKnapsackSolver();

            // Act
            var result = optimizer.Solve(stock, orders, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("2m 조각은 Delta(3m) 미만이므로 용접 불가");
        }
    }
}
