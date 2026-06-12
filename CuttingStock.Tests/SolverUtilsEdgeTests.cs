using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests
{
    /// <summary>Edge-case tests for 1D SolverUtils helpers.</summary>
    [TestFixture]
    public class SolverUtilsEdgeTests
    {
        // ----- ComputeLeftover -----

        [Test]
        public void ComputeLeftover_EmptyCuts_ReturnsFullStock()
        {
            SolverUtils.ComputeLeftover(12000, new List<Cut>(), kerf: 0).Should().Be(12000);
            SolverUtils.ComputeLeftover(12000, new List<Cut>(), kerf: 5).Should().Be(12000);
        }

        [Test]
        public void ComputeLeftover_SingleCut_NoKerf()
        {
            var cuts = new List<Cut> { new() { Length = 5000 } };
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 0).Should().Be(7000);
        }

        [Test]
        public void ComputeLeftover_SingleCut_WithKerf_NoKerfConsumed()
        {
            // Single cut: kerf only applies between cuts, so no kerf consumed.
            var cuts = new List<Cut> { new() { Length = 5000 } };
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 3).Should().Be(7000);
        }

        [Test]
        public void ComputeLeftover_TwoCuts_KerfConsumedOnce()
        {
            var cuts = new List<Cut> { new() { Length = 5000 }, new() { Length = 5000 } };
            // consumed = 10000 + (2-1)*3 = 10003
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 3).Should().Be(1997);
        }

        [Test]
        public void ComputeLeftover_ExactFit_NoKerf_ZeroLeftover()
        {
            var cuts = new List<Cut> { new() { Length = 6000 }, new() { Length = 6000 } };
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 0).Should().Be(0);
        }

        [Test]
        public void ComputeLeftover_ExceedsStock_ClampsToZero()
        {
            // If kerf pushes consumption over stock length, result should be 0 (not negative).
            var cuts = new List<Cut> { new() { Length = 6000 }, new() { Length = 6000 } };
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 10).Should().Be(0);
        }

        [Test]
        public void ComputeLeftover_ManyCuts_KerfAccumulates()
        {
            // 5 cuts of 2000mm with kerf=2: consumed = 10000 + 4*2 = 10008
            var cuts = new List<Cut>();
            for (int i = 0; i < 5; i++) cuts.Add(new Cut { Length = 2000 });
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 2).Should().Be(1992);
        }

        [Test]
        public void ComputeLeftover_ZeroKerf_SameAsNoKerf()
        {
            var cuts = new List<Cut> { new() { Length = 3000 }, new() { Length = 4000 } };
            SolverUtils.ComputeLeftover(12000, cuts, kerf: 0).Should().Be(5000);
        }

        // ----- ValidateInputs -----

        [Test]
        public void ValidateInputs_NullStock_ReturnsInvalid()
        {
            var (valid, _) = SolverUtils.ValidateInputs(null, new List<Order> { new(100, 1) });
            valid.Should().BeFalse();
        }

        [Test]
        public void ValidateInputs_EmptyStock_ReturnsInvalid()
        {
            var (valid, _) = SolverUtils.ValidateInputs(new List<RebarStock>(), new List<Order> { new(100, 1) });
            valid.Should().BeFalse();
        }

        [Test]
        public void ValidateInputs_NullOrders_ReturnsInvalid()
        {
            var (valid, _) = SolverUtils.ValidateInputs(new List<RebarStock> { new(100, 1) }, null);
            valid.Should().BeFalse();
        }

        [Test]
        public void ValidateInputs_ValidData_ReturnsValid()
        {
            var (valid, msg) = SolverUtils.ValidateInputs(
                new List<RebarStock> { new(12000, 5) },
                new List<Order> { new(5000, 3) });
            valid.Should().BeTrue();
            msg.Should().BeNull();
        }

        // ----- SortStock -----

        [Test]
        public void SortStock_SmallToLarge_SortsByLength()
        {
            var stock = new List<RebarStock> { new(12000, 1), new(6000, 1), new(9000, 1) };
            var sorted = SolverUtils.SortStock(stock, StockUsageOrder.SmallToLarge);
            sorted[0].Length.Should().Be(6000);
            sorted[1].Length.Should().Be(9000);
            sorted[2].Length.Should().Be(12000);
        }

        [Test]
        public void SortStock_LargeToSmall_ReverseOrder()
        {
            var stock = new List<RebarStock> { new(6000, 1), new(12000, 1), new(9000, 1) };
            var sorted = SolverUtils.SortStock(stock, StockUsageOrder.LargeToSmall);
            sorted[0].Length.Should().Be(12000);
        }

        // ----- SortOrdersByScarcity -----

        [Test]
        public void SortOrdersByScarcity_LowestQuantityFirst_ThenLongestFirst()
        {
            var orders = new List<Order> { new(5000, 10), new(3000, 2), new(8000, 2) };
            var sorted = SolverUtils.SortOrdersByScarcity(orders);
            sorted[0].Length.Should().Be(8000); // qty 2, longest first
            sorted[1].Length.Should().Be(3000); // qty 2
            sorted[2].Length.Should().Be(5000); // qty 10
        }

        // ----- CalculateResults -----

        [Test]
        public void CalculateResults_ClassifiesLeftovers()
        {
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new() { StockLength = 12000, Leftover = 500, Cuts = new List<Cut> { new() { Length = 11500 } } },
                    new() { StockLength = 12000, Leftover = 50,  Cuts = new List<Cut> { new() { Length = 11950 } } },
                }
            };
            var options = new SolverOptions { Alpha = 1, Beta = 500, Gamma = 100 };

            SolverUtils.CalculateResults(result, options);

            result.ReusableLeftovers.Should().BeEquivalentTo(new[] { 500 }); // 500 >= Gamma
            result.WasteLength.Should().Be(50); // 50 < Gamma
        }

        [Test]
        public void CalculateResults_WeldCountFromGroups()
        {
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new()
                    {
                        StockLength = 12000, Leftover = 0,
                        Cuts = new List<Cut>
                        {
                            new() { Length = 4000, WeldGroupId = 1 },
                            new() { Length = 4000, WeldGroupId = 1 },
                            new() { Length = 4000, WeldGroupId = 1 },
                        }
                    }
                }
            };
            var options = new SolverOptions { Alpha = 1, Beta = 100 };
            SolverUtils.CalculateResults(result, options);
            // 3 cuts in group 1 → 2 welds
            result.WeldCount.Should().Be(2);
        }

        // ----- ValidateSuccessfulResult -----

        [Test]
        public void ValidateSuccessfulResult_ValidPlan_ReturnsNull()
        {
            var stock = new List<RebarStock> { new(205, 1) };
            var orders = new List<Order> { new(100, 2) };
            var options = new SolverOptions { Kerf = 5 };
            var cuts = new List<Cut> { new() { Length = 100 }, new() { Length = 100 } };
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new() { StockLength = 205, Cuts = cuts, Leftover = 0 },
                },
            };

            SolverUtils.ValidateSuccessfulResult(stock, orders, options, result).Should().BeNull();
        }

        [Test]
        public void ValidateSuccessfulResult_OverPackedPlan_ReturnsError()
        {
            var stock = new List<RebarStock> { new(200, 1) };
            var orders = new List<Order> { new(100, 2) };
            var options = new SolverOptions { Kerf = 5 };
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new()
                    {
                        StockLength = 200,
                        Cuts = new List<Cut> { new() { Length = 100 }, new() { Length = 100 } },
                        Leftover = 0,
                    },
                },
            };

            SolverUtils.ValidateSuccessfulResult(stock, orders, options, result)
                .Should().Contain("consumes");
        }

        [Test]
        public void ValidateSuccessfulResult_OverProducesDemand_ReturnsError()
        {
            var stock = new List<RebarStock> { new(1000, 2) };
            var orders = new List<Order> { new(100, 1) };
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new()
                    {
                        StockLength = 1000,
                        Cuts = new List<Cut> { new() { Length = 100 }, new() { Length = 100 } },
                        Leftover = 800,
                    },
                },
            };

            SolverUtils.ValidateSuccessfulResult(stock, orders, new SolverOptions(), result)
                .Should().Contain("produced 2, expected 1");
        }

        [Test]
        public void ValidateSuccessfulResult_WeldGroupCountsAsOriginalOrderLength()
        {
            var stock = new List<RebarStock> { new(6000, 3) };
            var orders = new List<Order> { new(15000, 1) };
            var result = new SolverResult
            {
                CuttingPlans = new List<CuttingPlan>
                {
                    new()
                    {
                        StockLength = 6000,
                        Cuts = new List<Cut> { new() { Length = 6000, RequiresWelding = true, WeldGroupId = 1 } },
                        Leftover = 0,
                    },
                    new()
                    {
                        StockLength = 6000,
                        Cuts = new List<Cut> { new() { Length = 6000, RequiresWelding = true, WeldGroupId = 1 } },
                        Leftover = 0,
                    },
                    new()
                    {
                        StockLength = 6000,
                        Cuts = new List<Cut> { new() { Length = 3000, RequiresWelding = true, WeldGroupId = 1 } },
                        Leftover = 3000,
                    },
                },
            };

            SolverUtils.ValidateSuccessfulResult(
                stock,
                orders,
                new SolverOptions { Delta = 1000, EnableWelding = true },
                result).Should().BeNull();
        }

        // ----- UpdateOrders -----

        [Test]
        public void UpdateOrders_DeductsQuantities_RemovesZero()
        {
            var orders = new List<Order> { new(5000, 3), new(3000, 2) };
            var cuts = new List<int> { 5000, 5000, 3000, 3000 };

            SolverUtils.UpdateOrders(orders, cuts);

            orders.Should().HaveCount(1);
            orders[0].Length.Should().Be(5000);
            orders[0].Quantity.Should().Be(1);
        }

        [Test]
        public void UpdateOrders_AllConsumed_EmptyList()
        {
            var orders = new List<Order> { new(5000, 2) };
            var cuts = new List<int> { 5000, 5000 };

            SolverUtils.UpdateOrders(orders, cuts);
            orders.Should().BeEmpty();
        }

        [Test]
        public void UpdateOrders_NoCuts_NoChange()
        {
            var orders = new List<Order> { new(5000, 3) };
            SolverUtils.UpdateOrders(orders, new List<int>());
            orders.Should().HaveCount(1);
            orders[0].Quantity.Should().Be(3);
        }
    }
}
