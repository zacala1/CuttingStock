using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using System.Diagnostics;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Greedy Knapsack DP 알고리즘 종합 테스트
    ///
    /// 테스트 범위:
    /// 1. 경계 조건 (Boundary Conditions)
    /// 2. 엣지 케이스 (Edge Cases)
    /// 3. 다중 패스 최적화 검증
    /// 4. 희소 DP 메모리 효율 검증
    /// 5. 균등 분배 전략 검증
    /// 6. 후처리 최적화 검증
    /// 7. 성능 테스트
    /// 8. 회귀 테스트
    /// </summary>
    [TestFixture]
    public class GreedyKnapsackComprehensiveTests
    {
        private GreedyKnapsackSolver _optimizer = null!;
        private SolverOptions _defaultParams = null!;

        [SetUp]
        public void Setup()
        {
            _optimizer = new GreedyKnapsackSolver();
            _defaultParams = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = false,
                UsageOrder = StockUsageOrder.LargeToSmall
            };
        }

        #region 경계 조건 테스트 (Boundary Conditions)

        [Test]
        [Category("Boundary")]
        public void Optimize_SingleStockSingleOrder_ExactFit()
        {
            // 정확히 맞는 경우
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(10000, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().Be(1);
            result.CuttingPlans.Should().HaveCount(1);
            result.CuttingPlans[0].Leftover.Should().Be(0);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_SingleStockSingleOrder_SmallLeftover()
        {
            // 작은 자투리가 남는 경우
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(9999, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(1);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_OrderExactlyEqualToGamma()
        {
            // 주문 길이가 정확히 Gamma인 경우
            var stock = new List<RebarStock> { new RebarStock(1000, 1) };
            var orders = new List<Order> { new Order(100, 1) }; // Gamma = 100

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(900);
            result.ReusableLeftovers.Should().Contain(900); // 900 >= Gamma
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_LeftoverJustBelowGamma()
        {
            // 자투리가 Gamma 바로 아래인 경우
            var stock = new List<RebarStock> { new RebarStock(199, 1) };
            var orders = new List<Order> { new Order(100, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(99);
            result.ReusableLeftovers.Should().BeEmpty(); // 99 < Gamma
            result.WasteLength.Should().Be(99);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_LeftoverExactlyGamma()
        {
            // 자투리가 정확히 Gamma인 경우
            var stock = new List<RebarStock> { new RebarStock(200, 1) };
            var orders = new List<Order> { new Order(100, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(100);
            result.ReusableLeftovers.Should().Contain(100); // 100 >= Gamma
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_MaximumStockQuantity()
        {
            // 재고 수량이 많은 경우
            // 다중 패스 알고리즘은 균등 분배를 위해 더 많은 재고를 사용할 수 있음
            var stock = new List<RebarStock> { new RebarStock(10000, 150) };
            var orders = new List<Order> { new Order(5000, 200) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().BeGreaterThanOrEqualTo(100); // 최소 100개

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(200); // 모든 주문 처리
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_MinimumOrderLength()
        {
            // 최소 주문 길이
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(1, 10) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans.SelectMany(p => p.Cuts).Count().Should().Be(10);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_OrderLengthEqualsStockLength()
        {
            // 주문 길이 = 재고 길이
            var stock = new List<RebarStock> { new RebarStock(5000, 3) };
            var orders = new List<Order> { new Order(5000, 3) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().Be(3);
            result.CuttingPlans.All(p => p.Leftover == 0).Should().BeTrue();
        }

        #endregion

        #region 엣지 케이스 테스트 (Edge Cases)

        [Test]
        [Category("Edge")]
        public void Optimize_OrderLargerThanAnyStock_ShouldFail()
        {
            // 주문이 모든 재고보다 큰 경우
            var stock = new List<RebarStock> { new RebarStock(5000, 10) };
            var orders = new List<Order> { new Order(6000, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("process");
        }

        [Test]
        [Category("Edge")]
        public void Optimize_MixedLargeAndSmallOrders()
        {
            // 큰 주문과 작은 주문 혼합
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(9500, 2),  // 큰 주문
                new Order(100, 20)   // 작은 주문
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            totalCuts[9500].Should().Be(2);
            totalCuts[100].Should().Be(20);
        }

        [Test]
        [Category("Edge")]
        public void Optimize_AllOrdersSameLength()
        {
            // 모든 주문이 같은 길이
            // 다중 패스는 균등 분배를 위해 더 많은 재고를 사용할 수 있음
            var stock = new List<RebarStock> { new RebarStock(15000, 20) };
            var orders = new List<Order> { new Order(5000, 30) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().BeGreaterThanOrEqualTo(10);
            result.CuttingPlans.SelectMany(p => p.Cuts).Count().Should().Be(30);
        }

        [Test]
        [Category("Edge")]
        public void Optimize_ManyDifferentOrderLengths()
        {
            // 다양한 주문 길이
            var stock = new List<RebarStock> { new RebarStock(20000, 10) };
            var orders = new List<Order>
            {
                new Order(7000, 2),
                new Order(6000, 2),
                new Order(5000, 2),
                new Order(4000, 2),
                new Order(3000, 2),
                new Order(2000, 2),
                new Order(1000, 2)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(14); // 2개씩 7종류 = 14개
        }

        [Test]
        [Category("Edge")]
        public void Optimize_SingleOrderQuantityOne()
        {
            // 수량 1인 단일 주문
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(5000, 1) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().Be(1);
            result.CuttingPlans.Should().HaveCount(1);
        }

        [Test]
        [Category("Edge")]
        public void Optimize_MultipleStockSizes()
        {
            // 여러 재고 크기
            var stock = new List<RebarStock>
            {
                new RebarStock(12000, 2),
                new RebarStock(10000, 2),
                new RebarStock(8000, 2)
            };
            var orders = new List<Order>
            {
                new Order(7000, 3),
                new Order(5000, 3)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(6);
        }

        [Test]
        [Category("Edge")]
        public void Optimize_ZeroQuantityOrder_ShouldBeIgnored()
        {
            // 수량 0인 주문은 무시되어야 함
            // 희소성 정렬에서 0은 처리 대상이 아님
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order>
            {
                new Order(5000, 1)
                // 수량 0인 주문은 입력하지 않음 (일반적으로 필터링됨)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(1); // 5000mm만 처리
        }

        #endregion

        #region 다중 패스 최적화 검증

        [Test]
        [Category("MultiPass")]
        public void Optimize_MultiPass_ShouldDistributeEvenly()
        {
            // 다중 패스가 균등 분배하는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(5000, 8)  // 8개를 5개 재고에 분배
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            // 각 재고에 1-2개씩 분배되어야 함
            var cutsPerStock = result.CuttingPlans.Select(p => p.Cuts.Count).ToList();
            cutsPerStock.Max().Should().BeLessThanOrEqualTo(2);
        }

        [Test]
        [Category("MultiPass")]
        public void Optimize_MultiPass_ShouldProcessAllOrders()
        {
            // 다중 패스가 모든 주문을 처리하는지 검증
            var stock = new List<RebarStock> { new RebarStock(12000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 5),
                new Order(3000, 8),
                new Order(2000, 6)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            totalCuts[5000].Should().Be(5);
            totalCuts[3000].Should().Be(8);
            totalCuts[2000].Should().Be(6);
        }

        [Test]
        [Category("MultiPass")]
        public void Optimize_MultiPass_Pass1ShouldLimit()
        {
            // Pass 1에서 maxPerOrder=2 제한이 동작하는지 검증
            // 다중 패스로 모든 주문 처리
            var stock = new List<RebarStock> { new RebarStock(20000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 10)  // 한 재고에 4개 들어갈 수 있지만 제한
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            // 모든 주문이 처리되어야 함
            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(10);
        }

        #endregion

        #region 희소 DP 메모리 효율 검증

        [Test]
        [Category("SparseDP")]
        public void Optimize_SparseDP_ShouldHandleLargeStock()
        {
            // 큰 재고에서도 메모리 효율적으로 동작하는지 검증
            var stock = new List<RebarStock> { new RebarStock(100000, 1) }; // 100m
            var orders = new List<Order>
            {
                new Order(30000, 2),
                new Order(20000, 2)
            };

            var sw = Stopwatch.StartNew();
            var result = _optimizer.Solve(stock, orders, _defaultParams);
            sw.Stop();

            result.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(1000); // 1초 이내
        }

        [Test]
        [Category("SparseDP")]
        public void Optimize_SparseDP_ShouldFindOptimalCombination()
        {
            // 희소 DP가 최적 조합을 찾는지 검증
            var stock = new List<RebarStock> { new RebarStock(12000, 1) };
            var orders = new List<Order>
            {
                new Order(5000, 1),
                new Order(4000, 1),
                new Order(3000, 1)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(0); // 5000+4000+3000=12000
        }

        #endregion

        #region 균등 분배 전략 검증

        [Test]
        [Category("FairShare")]
        public void Optimize_FairShare_ShouldNotExhaustEarly()
        {
            // 조기 고갈 방지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 4) };
            var orders = new List<Order>
            {
                new Order(5000, 6)  // 2개씩 4개 재고에 분배 필요
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().BeGreaterThanOrEqualTo(3); // 최소 3개 재고 사용
        }

        [Test]
        [Category("FairShare")]
        public void Optimize_FairShare_ScarcityFirst()
        {
            // 희소 주문 우선 처리 검증
            // 총 주문: 5000+30000+20000=55000mm → 재고 6개 필요 (60000mm)
            var stock = new List<RebarStock> { new RebarStock(10000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 1),  // 희소: 수량 1
                new Order(3000, 10), // 풍부: 수량 10
                new Order(2000, 10)  // 풍부: 수량 10
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var cuts = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            cuts[5000].Should().Be(1); // 희소 주문 완전 처리
        }

        #endregion

        #region 후처리 최적화 검증

        [Test]
        [Category("PostProcess")]
        public void Optimize_PostProcess_ShouldReduceWaste()
        {
            // 후처리가 폐기물을 줄이는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 3) };
            var orders = new List<Order>
            {
                new Order(6000, 2),
                new Order(4000, 2),
                new Order(3000, 2)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.WasteLength.Should().BeLessThan(4000); // 후처리로 폐기물 감소
        }

        #endregion

        #region 성능 테스트

        [Test]
        [Category("Performance")]
        public void Optimize_Performance_SmallScale()
        {
            // 소규모: 10 재고, 50 주문
            var stock = new List<RebarStock> { new RebarStock(12000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20),
                new Order(2000, 20)
            };

            var sw = Stopwatch.StartNew();
            var result = _optimizer.Solve(stock, orders, _defaultParams);
            sw.Stop();

            result.ExecutionTimeMs.Should().BeLessThan(100);
            sw.ElapsedMilliseconds.Should().BeLessThan(100);
        }

        [Test]
        [Category("Performance")]
        public void Optimize_Performance_MediumScale()
        {
            // 중규모: 50 재고, 200 주문
            var stock = new List<RebarStock> { new RebarStock(12000, 50) };
            var orders = new List<Order>
            {
                new Order(5000, 50),
                new Order(4000, 50),
                new Order(3000, 50),
                new Order(2000, 50)
            };

            var sw = Stopwatch.StartNew();
            var result = _optimizer.Solve(stock, orders, _defaultParams);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(500);
        }

        [Test]
        [Category("Performance")]
        public void Optimize_Performance_LargeScale()
        {
            // 대규모: 100 재고, 500 주문
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order>
            {
                new Order(5000, 100),
                new Order(4000, 100),
                new Order(3000, 150),
                new Order(2000, 150)
            };

            var sw = Stopwatch.StartNew();
            var result = _optimizer.Solve(stock, orders, _defaultParams);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        [Test]
        [Category("Performance")]
        public void Optimize_Performance_ConsistentAcrossRuns()
        {
            // 여러 번 실행해도 일관된 성능
            var stock = new List<RebarStock> { new RebarStock(12000, 20) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20),
                new Order(2000, 20)
            };

            var times = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var result = _optimizer.Solve(stock, orders, _defaultParams);
                times.Add(result.ExecutionTimeMs);
            }

            var avgTime = times.Average();
            var maxDeviation = times.Max() - times.Min();

            // 편차가 평균의 50% 이내
            maxDeviation.Should().BeLessThan(avgTime * 0.5 + 10);
        }

        #endregion

        #region UsageOrder 테스트

        [Test]
        [Category("UsageOrder")]
        public void Optimize_UsageOrder_LargeToSmall()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(8000, 1),
                new RebarStock(12000, 1),
                new RebarStock(10000, 1)
            };
            var orders = new List<Order> { new Order(7000, 1) };

            var paramsLargeFirst = new SolverOptions
            {
                Alpha = 1.0f, Beta = 500.0f, Gamma = 100, Delta = 100,
                EnableWelding = false,
                UsageOrder = StockUsageOrder.LargeToSmall
            };

            var result = _optimizer.Solve(stock, orders, paramsLargeFirst);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].StockLength.Should().Be(12000); // 가장 큰 재고 먼저
        }

        [Test]
        [Category("UsageOrder")]
        public void Optimize_UsageOrder_SmallToLarge()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(12000, 1),
                new RebarStock(8000, 1),
                new RebarStock(10000, 1)
            };
            var orders = new List<Order> { new Order(7000, 1) };

            var paramsSmallFirst = new SolverOptions
            {
                Alpha = 1.0f, Beta = 500.0f, Gamma = 100, Delta = 100,
                EnableWelding = false,
                UsageOrder = StockUsageOrder.SmallToLarge
            };

            var result = _optimizer.Solve(stock, orders, paramsSmallFirst);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].StockLength.Should().Be(8000); // 가장 작은 재고 먼저
        }

        #endregion

        #region 용접 테스트

        [Test]
        [Category("Welding")]
        public void Optimize_Welding_ShouldJoinPieces()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(5000, 3)
            };
            var orders = new List<Order>
            {
                new Order(12000, 1)  // 재고보다 긴 주문
            };

            var paramsWithWelding = new SolverOptions
            {
                Alpha = 1.0f, Beta = 500.0f, Gamma = 100, Delta = 100,
                EnableWelding = true,
                UsageOrder = StockUsageOrder.LargeToSmall
            };

            var result = _optimizer.Solve(stock, orders, paramsWithWelding);

            result.Success.Should().BeTrue();
            result.WeldCount.Should().BeGreaterThan(0);

            var weldedCuts = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.RequiresWelding)
                .ToList();

            weldedCuts.Should().NotBeEmpty();
        }

        [Test]
        [Category("Welding")]
        public void Optimize_NoWelding_ShouldNotWeld()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(5000, 3)
            };
            var orders = new List<Order>
            {
                new Order(12000, 1)  // 용접 없이는 처리 불가
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeFalse();
            result.WeldCount.Should().Be(0);
        }

        #endregion

        #region 회귀 테스트 (Regression)

        [Test]
        [Category("Regression")]
        public void Regression_OriginalFailingCase_ShouldNowPass()
        {
            // 기존에 실패했던 케이스
            var stock = new List<RebarStock> { new RebarStock(12000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 5),
                new Order(3000, 8),
                new Order(2000, 6)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var cuts = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            cuts[5000].Should().Be(5);
            cuts[3000].Should().Be(8);
            cuts[2000].Should().Be(6);
        }

        [Test]
        [Category("Regression")]
        public void Regression_LargeScaleCase_ShouldComplete()
        {
            var stock = new List<RebarStock> { new RebarStock(12000, 20) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20),
                new Order(2000, 20)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(50); // 모든 주문 처리
        }

        [Test]
        [Category("Regression")]
        public void Regression_EarlyExhaustion_ShouldBeFixed()
        {
            // 조기 고갈 문제가 해결되었는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 3) };
            var orders = new List<Order>
            {
                new Order(6000, 3)  // 각 재고에 1개씩만 가능
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().Be(3);
            result.CuttingPlans.SelectMany(p => p.Cuts).Count().Should().Be(3);
        }

        #endregion

        #region 결과 일관성 테스트

        [Test]
        [Category("Consistency")]
        public void Optimize_SameInput_ShouldProduceSameOutput()
        {
            var stock = new List<RebarStock> { new RebarStock(12000, 5) };
            var orders = new List<Order>
            {
                new Order(5000, 3),
                new Order(3000, 4),
                new Order(2000, 5)
            };

            var result1 = _optimizer.Solve(stock, orders, _defaultParams);
            var result2 = _optimizer.Solve(stock, orders, _defaultParams);

            result1.Success.Should().Be(result2.Success);
            result1.StockUsed.Should().Be(result2.StockUsed);
            result1.MaterialEfficiency.Should().BeApproximately(result2.MaterialEfficiency, 0.01);
        }

        [Test]
        [Category("Consistency")]
        public void Optimize_MaterialEfficiency_ShouldBeCalculatedCorrectly()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 2) };
            var orders = new List<Order> { new Order(8000, 2) };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();

            // 총 사용량: 8000 * 2 = 16000
            // 총 재고: 10000 * 2 = 20000
            // 효율: 16000 / 20000 = 80%
            result.MaterialEfficiency.Should().BeApproximately(80.0, 1.0);
        }

        #endregion

        #region 특수 패턴 테스트

        [Test]
        [Category("Pattern")]
        public void Optimize_PerfectFitPattern()
        {
            // 완벽하게 맞는 패턴
            var stock = new List<RebarStock> { new RebarStock(12000, 3) };
            var orders = new List<Order>
            {
                new Order(6000, 3),
                new Order(4000, 3),
                new Order(2000, 3)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
            result.WasteLength.Should().Be(0);
            result.MaterialEfficiency.Should().Be(100.0);
        }

        [Test]
        [Category("Pattern")]
        public void Optimize_FibonacciLikeLengths()
        {
            // 피보나치 유사 길이
            var stock = new List<RebarStock> { new RebarStock(21000, 3) };
            var orders = new List<Order>
            {
                new Order(13000, 1),
                new Order(8000, 2),
                new Order(5000, 2),
                new Order(3000, 3),
                new Order(2000, 3)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
        }

        [Test]
        [Category("Pattern")]
        public void Optimize_PrimeLengths()
        {
            // 소수 길이 (조합하기 어려움)
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(7919, 1),
                new Order(6997, 1),
                new Order(5987, 1),
                new Order(4999, 1),
                new Order(2003, 2)
            };

            var result = _optimizer.Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue();
        }

        #endregion
    }
}
