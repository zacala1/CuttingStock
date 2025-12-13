using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;

namespace CuttingStock.Tests
{
    /// <summary>
    /// OptimizationModels 테스트
    /// </summary>
    [TestFixture]
    public class OptimizationModelsTests
    {
        #region SolverOptions Tests

        [Test]
        [Category("Models")]
        public void SolverOptions_DefaultValues_ShouldBeReasonable()
        {
            // Arrange & Act
            var parameters = new SolverOptions();

            // Assert
            parameters.Alpha.Should().Be(1.0f);
            parameters.Beta.Should().Be(500.0f);
            parameters.Gamma.Should().Be(100);
            parameters.Delta.Should().Be(100);
            parameters.UsageOrder.Should().Be(StockUsageOrder.SmallToLarge);
        }

        [Test]
        [Category("Models")]
        public void SolverOptions_CustomValues_ShouldBeSettable()
        {
            // Arrange & Act
            var parameters = new SolverOptions
            {
                Alpha = 2.0f,
                Beta = 1000.0f,
                Gamma = 500,
                Delta = 200,
                UsageOrder = StockUsageOrder.LargeToSmall
            };

            // Assert
            parameters.Alpha.Should().Be(2.0f);
            parameters.Beta.Should().Be(1000.0f);
            parameters.Gamma.Should().Be(500);
            parameters.Delta.Should().Be(200);
            parameters.UsageOrder.Should().Be(StockUsageOrder.LargeToSmall);
        }

        #endregion

        #region SolverResult Tests

        [Test]
        [Category("Models")]
        public void SolverResult_MaterialEfficiency_ZeroStock_ShouldReturnZero()
        {
            // Arrange
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>()
            };

            // Act
            var efficiency = result.MaterialEfficiency;

            // Assert
            efficiency.Should().Be(0.0);
        }

        [Test]
        [Category("Models")]
        public void SolverResult_MaterialEfficiency_PerfectMatch_ShouldBe100()
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

            // Act
            var efficiency = result.MaterialEfficiency;

            // Assert
            efficiency.Should().Be(100.0);
        }

        [Test]
        [Category("Models")]
        public void SolverResult_MaterialEfficiency_WithWaste_ShouldBeCorrect()
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
                            new Cut { Length = 4000 },
                            new Cut { Length = 4000 }
                        },
                        Leftover = 2000
                    }
                }
            };

            // Act
            var efficiency = result.MaterialEfficiency;

            // Assert
            efficiency.Should().Be(80.0); // 8000 / 10000 = 80%
        }

        [Test]
        [Category("Models")]
        public void SolverResult_StockUsed_ShouldReturnPlanCount()
        {
            // Arrange
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new CuttingPlan { StockLength = 10000 },
                    new CuttingPlan { StockLength = 10000 },
                    new CuttingPlan { StockLength = 10000 }
                }
            };

            // Act
            var stockUsed = result.StockUsed;

            // Assert
            stockUsed.Should().Be(3);
        }

        [Test]
        [Category("Models")]
        public void SolverResult_GetDetailedReport_ShouldContainAllSections()
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
                            new Cut { Length = 3000 }
                        },
                        Leftover = 2000
                    }
                },
                ReusableLeftovers = new List<int> { 2000 },
                WasteLength = 0,
                WeldCount = 0,
                TotalCost = 0,
                ExecutionTimeMs = 12.34,
                Success = true
            };

            var parameters = new SolverOptions();

            // Act
            var report = result.GetDetailedReport(parameters);

            // Assert
            report.Should().Contain("=== Cutting Results ===");
            report.Should().Contain("=== Performance Metrics ===");
            report.Should().Contain("=== Costs ===");
            report.Should().Contain("Stock 10000mm");
            report.Should().Contain("5000mm, 3000mm");
            report.Should().Contain("Rem: 2000mm");
            report.Should().Contain("Stock Used: 1");
            report.Should().Contain("Reusable Leftovers: [2000]");
            report.Should().Contain("Efficiency: 80.0%");
            report.Should().Contain("Execution Time: 12.34ms");
        }

        [Test]
        [Category("Models")]
        public void SolverResult_GetDetailedReport_WithCost_ShouldCalculateCorrectly()
        {
            // Arrange
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new CuttingPlan
                    {
                        StockLength = 10000,
                        Cuts = new List<Cut> { new Cut { Length = 9000 } },
                        Leftover = 1000
                    }
                },
                WasteLength = 1000,
                WeldCount = 2,
                TotalCost = 2000, // 1000 × 1 + 2 × 500
                Success = true
            };

            var parameters = new SolverOptions
            {
                Alpha = 1.0f,
                Beta = 500.0f
            };

            // Act
            var report = result.GetDetailedReport(parameters);

            // Assert
            report.Should().Contain("Waste Cost: 1000mm x 1/mm = 1000");
            report.Should().Contain("Weld Cost: 2 x 500/weld = 1000");
            report.Should().Contain("Total Cost: 2000");
        }

        #endregion

        #region CuttingPlan Tests

        [Test]
        [Category("Models")]
        public void CuttingPlan_Properties_ShouldBeSettable()
        {
            // Arrange & Act
            var plan = new CuttingPlan
            {
                StockLength = 10000,
                Cuts = new List<Cut>
                {
                    new Cut { Length = 5000, OrderIndex = 0 },
                    new Cut { Length = 3000, OrderIndex = 1 }
                },
                Leftover = 2000
            };

            // Assert
            plan.StockLength.Should().Be(10000);
            plan.Cuts.Should().HaveCount(2);
            plan.Cuts[0].Length.Should().Be(5000);
            plan.Cuts[1].Length.Should().Be(3000);
            plan.Leftover.Should().Be(2000);
        }

        #endregion

        #region Cut Tests

        [Test]
        [Category("Models")]
        public void Cut_Properties_ShouldBeSettable()
        {
            // Arrange & Act
            var cut = new Cut
            {
                Length = 5000,
                OrderIndex = 3,
                RequiresWelding = true
            };

            // Assert
            cut.Length.Should().Be(5000);
            cut.OrderIndex.Should().Be(3);
            cut.RequiresWelding.Should().BeTrue();
        }

        [Test]
        [Category("Models")]
        public void Cut_DefaultRequiresWelding_ShouldBeFalse()
        {
            // Arrange & Act
            var cut = new Cut
            {
                Length = 5000,
                OrderIndex = 0
            };

            // Assert
            cut.RequiresWelding.Should().BeFalse();
        }

        #endregion

        #region StockUsageOrder Tests

        [Test]
        [Category("Models")]
        public void StockUsageOrder_ShouldHaveTwoValues()
        {
            // Act
            var values = Enum.GetValues<StockUsageOrder>();

            // Assert
            values.Should().HaveCount(2);
            values.Should().Contain(StockUsageOrder.SmallToLarge);
            values.Should().Contain(StockUsageOrder.LargeToSmall);
        }

        #endregion
    }
}
