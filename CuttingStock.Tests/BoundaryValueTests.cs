using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests
{
    /// <summary>
    /// 경계값 및 엣지 케이스 테스트
    /// 입력 검증, 파라미터 검증, 극단적 케이스 테스트
    /// </summary>
    [TestFixture]
    public class BoundaryValueTests
    {
        private GreedyKnapsackSolver _greedyOptimizer = null!;
        private SolverOptions _defaultParams = null!;

        [SetUp]
        public void SetUp()
        {
            _greedyOptimizer = new GreedyKnapsackSolver();
            _defaultParams = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100
            };
        }

        #region 입력 검증 테스트

        [Test]
        [Category("Validation")]
        public void Optimize_NullStock_ShouldReturnError()
        {
            // Arrange
            List<RebarStock>? stock = null;
            var orders = new List<Order> { new Order(1000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock!, orders, _defaultParams);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("stock");
        }

        [Test]
        [Category("Validation")]
        public void Optimize_EmptyStock_ShouldReturnError()
        {
            // Arrange
            var stock = new List<RebarStock>();
            var orders = new List<Order> { new Order(1000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("stock");
        }

        [Test]
        [Category("Validation")]
        public void Optimize_NullOrders_ShouldReturnError()
        {
            // Arrange
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            List<Order>? orders = null;

            // Act
            var result = _greedyOptimizer.Solve(stock, orders!, _defaultParams);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("order");
        }

        [Test]
        [Category("Validation")]
        public void Optimize_EmptyOrders_ShouldReturnError()
        {
            // Arrange
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order>();

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("order");
        }

        [Test]
        [Category("Validation")]
        public void RebarStock_ZeroLength_ShouldThrowException()
        {
            // Act & Assert - 생성자에서 검증
            var action = () => new RebarStock(0, 1);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*length*");
        }

        [Test]
        [Category("Validation")]
        public void RebarStock_ZeroQuantity_ShouldThrowException()
        {
            // Act & Assert - 생성자에서 검증
            var action = () => new RebarStock(10000, 0);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*quantity*");
        }

        [Test]
        [Category("Validation")]
        public void Order_ZeroLength_ShouldThrowException()
        {
            // Act & Assert - 생성자에서 검증
            var action = () => new Order(0, 1);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*length*");
        }

        [Test]
        [Category("Validation")]
        public void Order_ZeroQuantity_ShouldThrowException()
        {
            // Act & Assert - 생성자에서 검증
            var action = () => new Order(1000, 0);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*quantity*");
        }

        [Test]
        [Category("Validation")]
        public void RebarStock_NegativeLength_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new RebarStock(-1000, 1);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*length*");
        }

        [Test]
        [Category("Validation")]
        public void Order_NegativeQuantity_ShouldThrowException()
        {
            // Act & Assert
            var action = () => new Order(1000, -5);
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*quantity*");
        }

        #endregion

        #region 파라미터 검증 테스트

        [Test]
        [Category("Parameter")]
        public void SolverOptions_NegativeAlpha_ShouldThrow()
        {
            // Act & Assert
            var action = () => new SolverOptions { Alpha = -1.0f };
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*Alpha*");
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_NegativeBeta_ShouldThrow()
        {
            // Act & Assert
            var action = () => new SolverOptions { Beta = -500.0f };
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*Beta*");
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_NegativeGamma_ShouldThrow()
        {
            // Act & Assert
            var action = () => new SolverOptions { Gamma = -100 };
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*Gamma*");
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_ZeroDelta_ShouldThrow()
        {
            // Act & Assert
            var action = () => new SolverOptions { Delta = 0 };
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*Delta*");
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_ZeroAlpha_ShouldSucceed()
        {
            // Alpha = 0 is valid (자투리 비용 무시)
            var param = new SolverOptions { Alpha = 0.0f };
            param.Alpha.Should().Be(0.0f);
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_ZeroBeta_ShouldSucceed()
        {
            // Beta = 0 is valid (용접 비용 무시)
            var param = new SolverOptions { Beta = 0.0f };
            param.Beta.Should().Be(0.0f);
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_ZeroGamma_ShouldSucceed()
        {
            // Gamma = 0 is valid (모든 자투리 재사용 가능)
            var param = new SolverOptions { Gamma = 0 };
            param.Gamma.Should().Be(0);
        }

        [Test]
        [Category("Parameter")]
        public void SolverOptions_ValidParams_ShouldNotThrow()
        {
            // Setter validation covers all constraints;
            // valid values should not throw
            var param = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f,
                Gamma = 100,
                Delta = 100
            };

            param.Alpha.Should().Be(1.0f);
            param.Beta.Should().Be(500.0f);
            param.Gamma.Should().Be(100);
            param.Delta.Should().Be(100);
        }

        #endregion

        #region 경계 조건 테스트

        [Test]
        [Category("Boundary")]
        public void Optimize_ExactFit_ShouldHaveZeroWaste()
        {
            // Arrange - 정확히 맞아떨어지는 경우
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(5000, 2) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeTrue();
            result.WasteLength.Should().Be(0);
            result.CuttingPlans[0].Leftover.Should().Be(0);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_SingleItemSingleStock_ShouldWork()
        {
            // Arrange - 최소 단위 테스트
            var stock = new List<RebarStock> { new RebarStock(1000, 1) };
            var orders = new List<Order> { new Order(500, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans.Should().HaveCount(1);
            result.CuttingPlans[0].Cuts.Should().HaveCount(1);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_OrderExactlyMatchesStock_ShouldHaveZeroWaste()
        {
            // Arrange - 주문 = 재고 길이
            var stock = new List<RebarStock> { new RebarStock(10000, 1) };
            var orders = new List<Order> { new Order(10000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeTrue();
            result.WasteLength.Should().Be(0);
            result.CuttingPlans[0].Leftover.Should().Be(0);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_MinimumLeftover_BelowGamma_ShouldBeWaste()
        {
            // Arrange - 자투리가 정확히 Gamma 미만인 경우
            var param = new SolverOptions { Gamma = 100 };
            var stock = new List<RebarStock> { new RebarStock(10099, 1) };
            var orders = new List<Order> { new Order(10000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(99);
            result.WasteLength.Should().Be(99);
            result.ReusableLeftovers.Should().BeEmpty();
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_MinimumLeftover_ExactlyGamma_ShouldBeReusable()
        {
            // Arrange - 자투리가 정확히 Gamma인 경우
            var param = new SolverOptions { Gamma = 100 };
            var stock = new List<RebarStock> { new RebarStock(10100, 1) };
            var orders = new List<Order> { new Order(10000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans[0].Leftover.Should().Be(100);
            result.WasteLength.Should().Be(0);
            result.ReusableLeftovers.Should().Contain(100);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_OrderLargerThanAnyStock_WithWelding_ShouldSucceed()
        {
            // Arrange - 주문이 모든 재고보다 큰 경우 (용접 필요)
            var param = new SolverOptions { EnableWelding = true, Delta = 100 };
            var stock = new List<RebarStock> { new RebarStock(5000, 4) };
            var orders = new List<Order> { new Order(15000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeTrue();
            result.WeldCount.Should().BeGreaterThan(0);
        }

        [Test]
        [Category("Boundary")]
        public void Optimize_OrderLargerThanAnyStock_WithoutWelding_ShouldFail()
        {
            // Arrange - 주문이 모든 재고보다 큰 경우 (용접 비활성화)
            var param = new SolverOptions { EnableWelding = false };
            var stock = new List<RebarStock> { new RebarStock(5000, 4) };
            var orders = new List<Order> { new Order(15000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("process");
        }

        #endregion

        #region 대규모 테스트

        [Test]
        [Category("Scale")]
        public void Optimize_ManySmallOrders_ShouldComplete()
        {
            // Arrange - 다수의 작은 주문
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order> { new Order(100, 1000) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Should().NotBeNull();
            // 실패해도 OK (재고 부족일 수 있음)
        }

        [Test]
        [Category("Scale")]
        public void Optimize_ManyDifferentStocks_ShouldComplete()
        {
            // Arrange - 다양한 재고
            var stock = new List<RebarStock>();
            for (int i = 1; i <= 100; i++)
            {
                stock.Add(new RebarStock(1000 + i * 100, 1));
            }
            var orders = new List<Order>
            {
                new Order(500, 50),
                new Order(800, 30),
                new Order(1000, 20)
            };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Should().NotBeNull();
            result.ExecutionTimeMs.Should().BeLessThan(5000);
        }

        #endregion

        #region 정렬 순서 테스트

        [Test]
        [Category("Sorting")]
        public void Optimize_SmallToLarge_ShouldUseSmallStockFirst()
        {
            // Arrange
            var param = new SolverOptions { UsageOrder = StockUsageOrder.SmallToLarge };
            var stock = new List<RebarStock>
            {
                new RebarStock(10000, 1),
                new RebarStock(8000, 1),
                new RebarStock(6000, 1)
            };
            var orders = new List<Order> { new Order(5000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans[0].StockLength.Should().Be(6000);
        }

        [Test]
        [Category("Sorting")]
        public void Optimize_LargeToSmall_ShouldUseLargeStockFirst()
        {
            // Arrange
            var param = new SolverOptions { UsageOrder = StockUsageOrder.LargeToSmall };
            var stock = new List<RebarStock>
            {
                new RebarStock(6000, 1),
                new RebarStock(8000, 1),
                new RebarStock(10000, 1)
            };
            var orders = new List<Order> { new Order(5000, 1) };

            // Act
            var result = _greedyOptimizer.Solve(stock, orders, param);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans[0].StockLength.Should().Be(10000);
        }

        #endregion

        #region 리포트 생성 테스트

        [Test]
        [Category("Report")]
        public void GetDetailedReport_EmptyResult_ShouldNotThrow()
        {
            // Arrange
            var result = new SolverResult();

            // Act
            var report = result.GetDetailedReport(_defaultParams);

            // Assert
            report.Should().NotBeNullOrEmpty();
            report.Should().Contain("Metrics");
        }

        [Test]
        [Category("Report")]
        public void GetDetailedReport_WithWeldGroups_ShouldIncludeWeldInfo()
        {
            // Arrange
            var result = new SolverResult
            {
                AlgorithmName = "Test",
                CuttingPlans = new List<CuttingPlan>
                {
                    new CuttingPlan
                    {
                        StockLength = 5000,
                        Cuts = new List<Cut>
                        {
                            new Cut { Length = 3000, WeldGroupId = 1 },
                            new Cut { Length = 2000 }
                        },
                        Leftover = 0
                    },
                    new CuttingPlan
                    {
                        StockLength = 5000,
                        Cuts = new List<Cut>
                        {
                            new Cut { Length = 4000, WeldGroupId = 1 }
                        },
                        Leftover = 1000
                    }
                }
            };

            // Act
            var report = result.GetDetailedReport(_defaultParams);

            // Assert
            report.Should().Contain("Weld Groups");
            report.Should().Contain("G1");
        }

        [Test]
        [Category("Report")]
        public void MaterialEfficiency_NoPlans_ShouldBeZero()
        {
            // Arrange
            var result = new SolverResult();

            // Assert
            result.MaterialEfficiency.Should().Be(0);
        }

        [Test]
        [Category("Report")]
        public void MaterialEfficiency_PerfectFit_ShouldBe100()
        {
            // Arrange
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new CuttingPlan
                    {
                        StockLength = 10000,
                        Cuts = new List<Cut>
                        {
                            new Cut { Length = 5000 },
                            new Cut { Length = 5000 }
                        },
                        Leftover = 0
                    }
                }
            };

            // Assert
            result.MaterialEfficiency.Should().Be(100.0);
        }

        #endregion
    }
}
