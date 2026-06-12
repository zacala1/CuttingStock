using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace CuttingStock.Tests.Algorithms
{
    [TestFixture]
    public class ColumnGenerationSolverTests
    {
        private ColumnGenerationSolver _optimizer = null!;
        private SolverOptions _defaultParams = null!;

        [SetUp]
        public void SetUp()
        {
            _optimizer = new ColumnGenerationSolver();
            _defaultParams = new SolverOptions
            {
                Alpha = 1.0f, // 재고 비용 가중치
                Beta = 500.0f, // 용접 비용 (사용 안함)
                Gamma = 2000, // 자투리 기준 (2000 미만은 폐기)
                Delta = 100, // 잉여 기준
                UsageOrder = StockUsageOrder.SmallToLarge
            };
        }

        [Test]
        [Category("Basic")]
        public void Optimize_SimpleCase_ShouldFindOptimalSolution()
        {
            // Arrange
            // 재고: 10000mm
            // 주문: 3000mm x 3개
            // 기대: 1개의 재고에서 3000*3 = 9000 사용, 1000 남음. (최적)
            var stock = new List<RebarStock> { new RebarStock(10000, 10) };
            var orders = new List<Order> { new Order(3000, 3) };

            // Act
            var result = _optimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeTrue();
            result.CuttingPlans.Should().HaveCount(1);
            result.CuttingPlans[0].Cuts.Should().HaveCount(3);
            result.CuttingPlans[0].Leftover.Should().Be(1000);
            result.WasteLength.Should().Be(1000);
        }

        [Test]
        [Category("Basic")]
        public void Optimize_FractionalSolutionPossible_ShouldReturnIntegerSolution()
        {
            // Column Generation의 LP 해가 소수로 나올 수 있는 상황
            // 예: 재고 100, 주문 30 (x5), 70 (x2)
            // 패턴 A: [30, 30, 30] (10 남음)
            // 패턴 B: [70, 30] (0 남음)
            // 패턴 C: [70] (30 남음)

            // Arrange
            var stock = new List<RebarStock> { new RebarStock(10000, 100) };
            var orders = new List<Order>
            {
                new Order(3000, 7), // A 패턴(3개) x 2 + B 패턴(1개) x 1 ??
                new Order(7000, 3)
            };
            // 3000x3 (9000) -> 1000 남음
            // 7000+3000 (10000) -> 0 남음 (Best)

            // 최적해: [7000, 3000] 패턴 3번 사용 -> 3000 3개, 7000 3개 해결.
            // 남은 3000 4개 -> [3000, 3000, 3000] 패턴 1번 -> 3000 1개 남음
            // 남은 3000 1개 -> [3000] 패턴 1번.
            // 총 재고 사용: 3 + 1 + 1 = 5개.

            // Act
            var result = _optimizer.Solve(stock, orders, _defaultParams);

            // Assert
            result.Success.Should().BeTrue();
            var totalUsed = result.CuttingPlans.Count;
            // 5개 이하로 막아야 함 (Greedy라면 6개 쓸 수도 있음)
            // 7000x3은 무조건 새 재고 3개 필요. (각 3000씩 붙임). -> 3000 3개 처리됨.
            // 남은 3000 4개. -> 3000x3 한 재고에. -> 1개 남음.
            // 남은 3000 1개. -> 새 재고.
            // 합계: 3 + 1 + 1 = 5개.
            totalUsed.Should().BeLessThanOrEqualTo(5);
        }

        [Test]
        [Category("Algorithm")]
        public void Optimize_DifferentLengths_ShouldSelectEfficientPatterns()
        {
            // 다양한 길이가 섞여 있을 때 효율적인 패턴을 찾는지 확인
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order>
            {
                new Order(5000, 10),
                new Order(3000, 10),
                new Order(2000, 10)
            };
            // 이상적 패턴: 5000+5000+2000=12000 (Perfect)
            // 3000x4=12000 (Perfect)
            // 2000x6=12000 (Perfect)
            // 이렇게 딱 떨어지게 조합 가능함.
            // 5000(10개) -> 5000+5000+2000 패턴 5번 사용. (2000도 5개 처리됨)
            // 남은 2000(5개), 3000(10개).
            // 3000x4 패턴 2번 -> 3000 8개 처리. 2000 5개 남음. 3000 2개 남음.
            // ...

            // Act
            var result = _optimizer.Solve(stock, orders, _defaultParams);

            // Assert
            // 재료 효율이 매우 높아야 함 (거의 100%에 가까운 조합이 존재하므로)
            result.MaterialEfficiency.Should().BeGreaterThan(90.0);
        }

        [Test]
        [Category("Properties")]
        public void OptimizerProperties_ShouldBeCorrect()
        {
            _optimizer.Name.Should().Contain("Column Generation");
            _optimizer.TimeComplexity.Should().Contain("exp");
        }

        [Test]
        [Category("Algorithm")]
        public void StabilizedVariant_SimpleCase_ShouldReturnValidSolution()
        {
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order>
            {
                new Order(5000, 8),
                new Order(3000, 10),
                new Order(2000, 6),
            };

            var result = new StabilizedColumnGenerationSolver().Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.CuttingPlans.Should().NotBeEmpty();
            result.MaterialEfficiency.Should().BeGreaterThan(80.0);
        }

        [Test]
        [Category("Properties")]
        public void StabilizedVariant_Properties_ShouldIdentifyVariant()
        {
            var solver = new StabilizedColumnGenerationSolver();

            solver.Name.Should().Be("Column Generation (Stabilized LP)");
            solver.Description.Should().Contain("dual");
        }

        [Test]
        [Category("Algorithm")]
        public void MultiColumnVariant_SimpleCase_ShouldReturnValidSolution()
        {
            var stock = new List<RebarStock> { new RebarStock(12000, 100) };
            var orders = new List<Order>
            {
                new Order(5000, 8),
                new Order(3000, 10),
                new Order(2000, 6),
            };

            var result = new MultiColumnGenerationSolver().Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.CuttingPlans.Should().NotBeEmpty();
            result.MaterialEfficiency.Should().BeGreaterThan(80.0);
        }

        [Test]
        [Category("Properties")]
        public void MultiColumnVariant_Properties_ShouldIdentifyVariant()
        {
            var solver = new MultiColumnGenerationSolver();

            solver.Name.Should().Be("Column Generation (Multi-column LP)");
            solver.Description.Should().Contain("multiple");
        }

        [Test]
        [Category("Algorithm")]
        public void IntegerMasterVariant_SimpleCase_ShouldReturnValidSolution()
        {
            var stock = new List<RebarStock> { new RebarStock(10000, 100) };
            var orders = new List<Order>
            {
                new Order(3000, 7),
                new Order(7000, 3)
            };

            var result = new IntegerMasterColumnGenerationSolver().Solve(stock, orders, _defaultParams);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.CuttingPlans.Should().HaveCountLessThanOrEqualTo(5);
        }

        [Test]
        [Category("Properties")]
        public void IntegerMasterVariant_Properties_ShouldIdentifyVariant()
        {
            var solver = new IntegerMasterColumnGenerationSolver();

            solver.Name.Should().Be("Column Generation (Integer Master)");
            solver.Description.Should().Contain("integer master");
        }

        [Test]
        [Category("Algorithm")]
        public void GlobalStockVariant_MultipleStockLengths_ShouldChooseGlobalBestStock()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(10000, 10),
                new RebarStock(6000, 10),
            };
            var orders = new List<Order> { new Order(3000, 2) };
            var options = new SolverOptions
            {
                UsageOrder = StockUsageOrder.LargeToSmall,
                Kerf = 0,
            };

            var result = new GlobalStockColumnGenerationSolver().Solve(stock, orders, options);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.CuttingPlans.Should().ContainSingle();
            result.CuttingPlans[0].StockLength.Should().Be(6000);
            result.WasteLength.Should().Be(0);
        }

        [Test]
        [Category("Properties")]
        public void GlobalStockVariant_Properties_ShouldIdentifyVariant()
        {
            var solver = new GlobalStockColumnGenerationSolver();

            solver.Name.Should().Be("Global Stock Column Generation");
            solver.Description.Should().Contain("Variable-stock");
        }
    }
}
