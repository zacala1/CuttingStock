using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Models;
using CuttingStock.Core.Domain;
using System.Collections.Generic;
using System.Linq;

namespace CuttingStock.Tests
{
    [TestFixture]
    public class SolverUtilsTests
    {
        [Test]
        public void ValidateInputs_WithNullStock_ShouldFail()
        {
            var (isValid, message) = SolverUtils.ValidateInputs(null, new List<Order>());
            isValid.Should().BeFalse();
            message.Should().Contain("stock");
        }

        [Test]
        public void ValidateInputs_WithEmptyOrders_ShouldFail()
        {
            var (isValid, message) = SolverUtils.ValidateInputs(new List<RebarStock>(), new List<Order>());
            isValid.Should().BeFalse();
            message.Should().Contain("stock");
        }

        [Test]
        public void FlattenOrdersDescending_ShouldSortAndFlatten()
        {
            var orders = new List<Order>
            {
                new Order(100, 2),
                new Order(200, 1),
                new Order(50, 3)
            };

            var result = SolverUtils.FlattenOrdersDescending(orders);

            result.Should().HaveCount(6); // 2 + 1 + 3
            result[0].Should().Be(200);
            result[1].Should().Be(100);
            result[2].Should().Be(100);
            result[3].Should().Be(50);
        }

        [Test]
        public void SortStock_SmallToLarge_ShouldSortAscending()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(3000, 1),
                new RebarStock(1000, 1),
                new RebarStock(2000, 1)
            };

            var result = SolverUtils.SortStock(stock, StockUsageOrder.SmallToLarge);

            result[0].Length.Should().Be(1000);
            result[1].Length.Should().Be(2000);
            result[2].Length.Should().Be(3000);
        }

        [Test]
        public void SortStock_LargeToSmall_ShouldSortDescending()
        {
            var stock = new List<RebarStock>
            {
                new RebarStock(3000, 1),
                new RebarStock(1000, 1),
                new RebarStock(2000, 1)
            };

            var result = SolverUtils.SortStock(stock, StockUsageOrder.LargeToSmall);

            result[0].Length.Should().Be(3000);
            result[1].Length.Should().Be(2000);
            result[2].Length.Should().Be(1000);
        }
    }
}
