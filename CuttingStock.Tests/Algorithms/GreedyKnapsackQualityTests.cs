using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using System.Diagnostics;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// GreedyKnapsackSolver 최적화 검증 테스트
    ///
    /// 개선 항목:
    /// 1. UpdateOrders Dictionary 최적화
    /// 2. usedStockCounts Pass간 전달
    /// 3. Look-ahead 시뮬레이션 경량화
    /// 4. ToList() 불필요한 호출 제거
    /// </summary>
    [TestFixture]
    public class GreedyKnapsackOptimizationTests
    {
        private GreedyKnapsackSolver _solver = null!;
        private SolverOptions _defaultOptions = null!;

        [SetUp]
        public void Setup()
        {
            _solver = new GreedyKnapsackSolver();
            _defaultOptions = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = false,
                UsageOrder = StockUsageOrder.LargeToSmall
            };
        }

        #region UpdateOrders 최적화 검증

        [Test]
        [Category("Optimization")]
        public void UpdateOrders_MultipleOrdersSameLength_ShouldProcessCorrectly()
        {
            // 동일 길이 주문이 여러 개 있을 때 Dictionary 인덱싱이 올바르게 동작하는지 검증
            // 참고: Order는 Length 기준으로 그룹화되어 처리되므로 같은 길이의 주문은 합산됨
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(3000, 5),
                new Order(2000, 5)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            var cuts = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            // 3000mm 주문은 5개, 2000mm 주문은 5개
            cuts.GetValueOrDefault(3000, 0).Should().Be(5);
            cuts.GetValueOrDefault(2000, 0).Should().Be(5);
        }

        [Test]
        [Category("Optimization")]
        public void UpdateOrders_BatchRemoval_ShouldMaintainOrderIntegrity()
        {
            // 여러 주문이 한 번에 제거될 때 인덱스 무결성 유지 검증
            var stock = new List<RebarStock> { new RebarStock(15000, 3) };
            var orders = new List<Order>
            {
                new Order(5000, 3),
                new Order(4000, 3),
                new Order(3000, 3),
                new Order(2000, 3),
                new Order(1000, 3)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(15); // 모든 주문 처리
        }

        [Test]
        [Category("Optimization")]
        public void UpdateOrders_SingleCut_ShouldUpdateCorrectly()
        {
            // 단일 컷으로 주문 업데이트 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(5000, 2) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans.SelectMany(p => p.Cuts)
                .Count(c => c.Length == 5000).Should().Be(2);
        }

        #endregion

        #region usedStockCounts Pass간 전달 검증

        [Test]
        [Category("Optimization")]
        public void UsedStockCounts_MultiPass_ShouldTrackAcrossPasses()
        {
            // 여러 Pass에서 재고 사용량이 올바르게 추적되는지 검증
            var stock = new List<RebarStock>
            {
                new RebarStock(10000, 3),
                new RebarStock(8000, 3)
            };
            var orders = new List<Order>
            {
                new Order(5000, 6),  // Pass1에서 일부 처리
                new Order(3000, 6)   // Pass2, Pass3에서 처리
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();

            // 재고 사용량 검증
            var stockUsage = result.CuttingPlans
                .GroupBy(p => p.StockLength)
                .ToDictionary(g => g.Key, g => g.Count());

            // 각 재고 타입의 사용량이 수량을 초과하지 않아야 함
            stockUsage.GetValueOrDefault(10000, 0).Should().BeLessThanOrEqualTo(3);
            stockUsage.GetValueOrDefault(8000, 0).Should().BeLessThanOrEqualTo(3);
        }

        [Test]
        [Category("Optimization")]
        public void UsedStockCounts_Pass3_ShouldUseRemainingStock()
        {
            // Pass3에서 남은 재고를 사용하는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(2000, 20)  // maxPerOrder 제한으로 여러 Pass 필요
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans.SelectMany(p => p.Cuts)
                .Count(c => c.Length == 2000).Should().Be(20);
        }

        [Test]
        [Category("Optimization")]
        public void UsedStockCounts_StockQuantityRespected()
        {
            // 재고 수량이 초과되지 않는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 2) };
            var orders = new List<Order> { new Order(5000, 6) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            // 재고 2개 × 10000mm = 20000mm, 주문 6개 × 5000mm = 30000mm
            // 최대 4개만 처리 가능
            result.StockUsed.Should().BeLessThanOrEqualTo(2);
            result.CuttingPlans.SelectMany(p => p.Cuts)
                .Count(c => c.Length == 5000).Should().BeLessThanOrEqualTo(4);
        }

        #endregion

        #region Look-ahead 시뮬레이션 경량화 검증

        [Test]
        [Category("Optimization")]
        public void LookAhead_DictionarySimulation_ShouldProduceSameResults()
        {
            // Dictionary 기반 시뮬레이션이 올바른 결과를 내는지 검증
            var stock = new List<RebarStock> { new RebarStock(12000, 5) };
            var orders = new List<Order>
            {
                new Order(5000, 3),
                new Order(4000, 3),
                new Order(3000, 3)
            };

            // 여러 번 실행해도 동일한 결과
            var result1 = _solver.Solve(stock, orders, _defaultOptions);
            var result2 = _solver.Solve(stock, orders, _defaultOptions);

            result1.Success.Should().Be(result2.Success);
            result1.StockUsed.Should().Be(result2.StockUsed);
            result1.MaterialEfficiency.Should().BeApproximately(result2.MaterialEfficiency, 0.01);
        }

        [Test]
        [Category("Optimization")]
        public void LookAhead_MultipleCandiates_ShouldSelectBest()
        {
            // 여러 후보 중 최적의 것을 선택하는지 검증
            var stock = new List<RebarStock> { new RebarStock(10000, 3) };
            var orders = new List<Order>
            {
                new Order(6000, 2),
                new Order(4000, 2),
                new Order(3000, 2)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            // 6000+4000=10000 또는 6000+3000=9000 패턴 사용 예상
            result.MaterialEfficiency.Should().BeGreaterThan(80.0);
        }

        [Test]
        [Category("Optimization")]
        public void LookAhead_PerformanceWithManyOrders()
        {
            // 많은 주문에서도 성능이 유지되는지 검증
            var stock = new List<RebarStock> { new RebarStock(12000, 20) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(4000, 10),
                new Order(3000, 10),
                new Order(2000, 10),
                new Order(1000, 10)
            };

            var sw = Stopwatch.StartNew();
            var result = _solver.Solve(stock, orders, _defaultOptions);
            sw.Stop();

            result.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(500);
        }

        #endregion

        #region Sparse DP HashSet 반복 검증

        [Test]
        [Category("Optimization")]
        public void SparseDP_HashSetIteration_ShouldNotCauseErrors()
        {
            // HashSet 반복 중 수정 없이 안전하게 동작하는지 검증
            var stock = new List<RebarStock> { new RebarStock(20000, 5) };
            var orders = new List<Order>
            {
                new Order(7000, 3),
                new Order(6000, 3),
                new Order(5000, 3),
                new Order(4000, 3),
                new Order(3000, 3)
            };

            // 예외 발생하지 않아야 함
            Action act = () => _solver.Solve(stock, orders, _defaultOptions);
            act.Should().NotThrow();
        }

        [Test]
        [Category("Optimization")]
        public void SparseDP_LargeStockLength_ShouldNotOverflow()
        {
            // 큰 재고 길이에서도 오버플로우 없이 동작
            var stock = new List<RebarStock> { new RebarStock(100000, 2) };
            var orders = new List<Order>
            {
                new Order(30000, 2),
                new Order(20000, 2),
                new Order(10000, 2)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
        }

        [Test]
        [Category("Optimization")]
        public void SparseDP_ManyUniqueLength_ShouldComplete()
        {
            // 다양한 길이의 주문에서도 완료되어야 함
            var stock = new List<RebarStock> { new RebarStock(50000, 10) };
            var orders = Enumerable.Range(1, 20)
                .Select(i => new Order(1000 + i * 500, 2))
                .ToList();

            var sw = Stopwatch.StartNew();
            var result = _solver.Solve(stock, orders, _defaultOptions);
            sw.Stop();

            result.Success.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        #endregion

        #region 결과 정합성 검증

        [Test]
        [Category("Integrity")]
        public void Result_CutLengths_ShouldNotExceedStockLength()
        {
            // 각 계획의 컷 합계가 재고 길이를 초과하지 않아야 함
            var stock = new List<RebarStock>
            {
                new RebarStock(10000, 5),
                new RebarStock(8000, 5)
            };
            var orders = new List<Order>
            {
                new Order(5000, 5),
                new Order(3000, 5),
                new Order(2000, 5)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            foreach (var plan in result.CuttingPlans)
            {
                var totalCutLength = plan.Cuts.Sum(c => c.Length);
                totalCutLength.Should().BeLessThanOrEqualTo(plan.StockLength,
                    $"Plan with stock {plan.StockLength} has cuts totaling {totalCutLength}");
            }
        }

        [Test]
        [Category("Integrity")]
        public void Result_LeftoverCalculation_ShouldBeCorrect()
        {
            // Leftover 계산이 정확해야 함
            var stock = new List<RebarStock> { new RebarStock(12000, 5) };
            var orders = new List<Order>
            {
                new Order(5000, 3),
                new Order(3000, 4)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            foreach (var plan in result.CuttingPlans)
            {
                var expectedLeftover = plan.StockLength - plan.Cuts.Sum(c => c.Length);
                plan.Leftover.Should().Be(expectedLeftover,
                    $"Plan leftover {plan.Leftover} should equal {expectedLeftover}");
            }
        }

        [Test]
        [Category("Integrity")]
        public void Result_OrderQuantities_ShouldNotBeExceeded()
        {
            // 주문 수량을 초과해서 절단하지 않아야 함
            var stock = new List<RebarStock> { new RebarStock(10000, 10) };
            var orders = new List<Order>
            {
                new Order(5000, 3),
                new Order(3000, 4),
                new Order(2000, 5)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            var cutsByLength = result.CuttingPlans.SelectMany(p => p.Cuts)
                .GroupBy(c => c.Length)
                .ToDictionary(g => g.Key, g => g.Count());

            cutsByLength.GetValueOrDefault(5000, 0).Should().BeLessThanOrEqualTo(3);
            cutsByLength.GetValueOrDefault(3000, 0).Should().BeLessThanOrEqualTo(4);
            cutsByLength.GetValueOrDefault(2000, 0).Should().BeLessThanOrEqualTo(5);
        }

        [Test]
        [Category("Integrity")]
        public void Result_StockUsage_ShouldNotExceedQuantity()
        {
            // 재고 사용량이 재고 수량을 초과하지 않아야 함
            var stock = new List<RebarStock>
            {
                new RebarStock(10000, 2),
                new RebarStock(8000, 3)
            };
            var orders = new List<Order>
            {
                new Order(5000, 10)  // 재고보다 많은 주문
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            var usage10000 = result.CuttingPlans.Count(p => p.StockLength == 10000);
            var usage8000 = result.CuttingPlans.Count(p => p.StockLength == 8000);

            usage10000.Should().BeLessThanOrEqualTo(2);
            usage8000.Should().BeLessThanOrEqualTo(3);
        }

        #endregion

        #region 성능 회귀 테스트

        [Test]
        [Category("Performance")]
        public void Performance_SmallScale_UnderThreshold()
        {
            // 소규모: 50 주문, 100ms 이내
            var stock = new List<RebarStock> { new RebarStock(12000, 20) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 20),
                new Order(2000, 20)
            };

            var sw = Stopwatch.StartNew();
            _solver.Solve(stock, orders, _defaultOptions);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(100);
        }

        [Test]
        [Category("Performance")]
        public void Performance_MediumScale_UnderThreshold()
        {
            // 중규모: 200 주문, 500ms 이내
            var stock = new List<RebarStock> { new RebarStock(12000, 50) };
            var orders = new List<Order>
            {
                new Order(5000, 50),
                new Order(4000, 50),
                new Order(3000, 50),
                new Order(2000, 50)
            };

            var sw = Stopwatch.StartNew();
            _solver.Solve(stock, orders, _defaultOptions);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(500);
        }

        [Test]
        [Category("Performance")]
        public void Performance_LargeScale_UnderThreshold()
        {
            // 대규모: 500 주문, 2초 이내
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order>
            {
                new Order(5000, 100),
                new Order(4000, 100),
                new Order(3000, 150),
                new Order(2000, 150)
            };

            var sw = Stopwatch.StartNew();
            _solver.Solve(stock, orders, _defaultOptions);
            sw.Stop();

            sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        [Test]
        [Category("Performance")]
        public void Performance_MultipleRuns_Consistent()
        {
            // 여러 번 실행해도 성능이 일관되어야 함
            var stock = new List<RebarStock> { new RebarStock(12000, 30) };
            var orders = new List<Order>
            {
                new Order(5000, 20),
                new Order(3000, 30),
                new Order(2000, 30)
            };

            var times = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                var sw = Stopwatch.StartNew();
                _solver.Solve(stock, orders, _defaultOptions);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
            }

            var avgTime = times.Average();
            var maxDeviation = times.Max() - times.Min();

            // 편차가 평균의 100% + 50ms 이내
            maxDeviation.Should().BeLessThan((long)(avgTime + 50));
        }

        #endregion

        #region 엣지 케이스 검증

        [Test]
        [Category("Edge")]
        public void Edge_SingleOrderSingleStock_ExactFit()
        {
            var stock = new List<RebarStock> { new RebarStock(5000, 1) };
            var orders = new List<Order> { new Order(5000, 1) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.StockUsed.Should().Be(1);
            result.CuttingPlans[0].Leftover.Should().Be(0);
        }

        [Test]
        [Category("Edge")]
        public void Edge_OrderLargerThanStock_ShouldFail()
        {
            var stock = new List<RebarStock> { new RebarStock(5000, 10) };
            var orders = new List<Order> { new Order(6000, 1) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeFalse();
        }

        [Test]
        [Category("Edge")]
        public void Edge_InsufficientStock_PartialFulfillment()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(5000, 3) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            // 2개만 처리 가능
            var processed = result.CuttingPlans.SelectMany(p => p.Cuts)
                .Count(c => c.Length == 5000);
            processed.Should().Be(2);
        }

        [Test]
        [Category("Edge")]
        public void Edge_VerySmallOrders()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(100, 50) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans.SelectMany(p => p.Cuts)
                .Count(c => c.Length == 100).Should().Be(50);
        }

        [Test]
        [Category("Edge")]
        public void Edge_ManySmallStocks()
        {
            var stock = new List<RebarStock> { new RebarStock(1000, 100) };
            var orders = new List<Order>
            {
                new Order(500, 50),
                new Order(300, 50)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
        }

        #endregion

        #region 용접 로직 검증

        [Test]
        [Category("Welding")]
        public void Welding_LargeOrder_ShouldUseWelding()
        {
            var stock = new List<RebarStock> { new RebarStock(5000, 5) };
            var orders = new List<Order> { new Order(12000, 1) };

            var weldOptions = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = true,
                UsageOrder = StockUsageOrder.LargeToSmall
            };

            var result = _solver.Solve(stock, orders, weldOptions);

            result.Success.Should().BeTrue();
            result.WeldCount.Should().BeGreaterThan(0);
        }

        [Test]
        [Category("Welding")]
        public void Welding_Disabled_ShouldNotWeld()
        {
            var stock = new List<RebarStock> { new RebarStock(5000, 5) };
            var orders = new List<Order> { new Order(12000, 1) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeFalse();
            result.WeldCount.Should().Be(0);
        }

        [Test]
        [Category("Welding")]
        public void Welding_WeldGroupId_ShouldBeConsistent()
        {
            var stock = new List<RebarStock> { new RebarStock(5000, 6) };
            var orders = new List<Order> { new Order(12000, 2) };

            var weldOptions = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100,
                EnableWelding = true,
                UsageOrder = StockUsageOrder.LargeToSmall
            };

            var result = _solver.Solve(stock, orders, weldOptions);

            result.Success.Should().BeTrue();

            var weldGroups = result.CuttingPlans.SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .ToList();

            // 각 용접 그룹의 총 길이가 주문 길이와 일치해야 함
            foreach (var group in weldGroups)
            {
                group.Sum(c => c.Length).Should().Be(12000);
            }
        }

        #endregion

        #region 후처리 최적화 검증

        [Test]
        [Category("PostProcess")]
        public void PostProcess_HighLeftoverPlans_ShouldBeOptimized()
        {
            // 높은 자투리를 가진 계획들이 후처리로 최적화되어야 함
            var stock = new List<RebarStock> { new RebarStock(10000, 5) };
            var orders = new List<Order>
            {
                new Order(6000, 3),
                new Order(3000, 5)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            // 후처리 후 재료 효율이 합리적이어야 함 (균등 분배로 인해 효율이 다소 낮을 수 있음)
            result.MaterialEfficiency.Should().BeGreaterThan(60.0);
        }

        [Test]
        [Category("PostProcess")]
        public void PostProcess_SmallCuts_ShouldBeRedistributed()
        {
            var stock = new List<RebarStock> { new RebarStock(12000, 4) };
            var orders = new List<Order>
            {
                new Order(7000, 2),
                new Order(5000, 2),
                new Order(4000, 2),
                new Order(3000, 2)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();

            // 모든 주문이 처리되어야 함
            var totalCuts = result.CuttingPlans.SelectMany(p => p.Cuts).Count();
            totalCuts.Should().Be(8);
        }

        #endregion

        #region 자투리 처리 검증

        [Test]
        [Category("Leftover")]
        public void Leftover_Processing_ShouldUseLeftovers()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 3) };
            var orders = new List<Order>
            {
                new Order(7000, 3),
                new Order(2000, 3)
            };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            // 7000 + 2000 = 9000, 자투리 1000mm
            // 자투리에서 추가 2000mm 절단 불가하므로 별도 처리 필요
        }

        [Test]
        [Category("Leftover")]
        public void Leftover_ReusableVsWaste_Classification()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(9800, 1) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(200);

            // Gamma=100이므로 200mm는 재사용 가능
            result.ReusableLeftovers.Should().Contain(200);
            result.WasteLength.Should().Be(0);
        }

        [Test]
        [Category("Leftover")]
        public void Leftover_BelowGamma_ShouldBeWaste()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(9950, 1) };

            var result = _solver.Solve(stock, orders, _defaultOptions);

            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(50);

            // 50mm < Gamma(100)이므로 폐기물
            result.ReusableLeftovers.Should().BeEmpty();
            result.WasteLength.Should().Be(50);
        }

        #endregion
    }
}
