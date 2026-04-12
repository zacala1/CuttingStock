using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests
{
    /// <summary>
    /// Model 클래스들의 Equals, GetHashCode, ToString, 연산자 테스트
    /// </summary>
    [TestFixture]
    public class ModelTests
    {
        #region Order Tests

        [Test]
        public void Order_Equals_SameValues_ShouldBeEqual()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(5000, 3);

            // Act & Assert
            order1.Equals(order2).Should().BeTrue();
            order2.Equals(order1).Should().BeTrue();
        }

        [Test]
        public void Order_Equals_DifferentLength_ShouldNotBeEqual()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(3000, 3);

            // Act & Assert
            order1.Equals(order2).Should().BeFalse();
        }

        [Test]
        public void Order_Equals_DifferentQuantity_ShouldNotBeEqual()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(5000, 5);

            // Act & Assert
            order1.Equals(order2).Should().BeFalse();
        }

        [Test]
        public void Order_Equals_Null_ShouldReturnFalse()
        {
            // Arrange
            var order = new Order(5000, 3);

            // Act & Assert
            order.Equals(null).Should().BeFalse();
        }

        [Test]
        public void Order_Equals_SameReference_ShouldBeEqual()
        {
            // Arrange
            var order = new Order(5000, 3);

            // Act & Assert
            order.Equals(order).Should().BeTrue();
        }

        [Test]
        public void Order_Equals_ObjectOverload_ShouldWork()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            object order2 = new Order(5000, 3);

            // Act & Assert
            order1.Equals(order2).Should().BeTrue();
        }

        [Test]
        public void Order_Equals_DifferentType_ShouldReturnFalse()
        {
            // Arrange
            var order = new Order(5000, 3);
            var notAnOrder = "not an order";

            // Act & Assert
            order.Equals(notAnOrder).Should().BeFalse();
        }

        [Test]
        public void Order_GetHashCode_EqualObjects_ShouldHaveSameHashCode()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(5000, 3);

            // Act & Assert
            order1.GetHashCode().Should().Be(order2.GetHashCode());
        }

        [Test]
        public void Order_GetHashCode_DifferentObjects_ShouldLikelyDiffer()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(3000, 5);

            // Act & Assert (다른 값은 일반적으로 다른 해시코드를 가짐)
            order1.GetHashCode().Should().NotBe(order2.GetHashCode());
        }

        [Test]
        public void Order_ToString_ShouldReturnExpectedFormat()
        {
            // Arrange
            var order = new Order(5000, 3);

            // Act
            var result = order.ToString();

            // Assert
            result.Should().Be("Order(5000mm x3)");
        }

        [Test]
        public void Order_OperatorEquals_SameValues_ShouldBeTrue()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(5000, 3);

            // Act & Assert
            (order1 == order2).Should().BeTrue();
        }

        [Test]
        public void Order_OperatorEquals_DifferentValues_ShouldBeFalse()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(3000, 3);

            // Act & Assert
            (order1 == order2).Should().BeFalse();
        }

        [Test]
        public void Order_OperatorEquals_BothNull_ShouldBeTrue()
        {
            // Arrange
            Order? order1 = null;
            Order? order2 = null;

            // Act & Assert
            (order1 == order2).Should().BeTrue();
        }

        [Test]
        public void Order_OperatorEquals_OneNull_ShouldBeFalse()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            Order? order2 = null;

            // Act & Assert
            (order1 == order2).Should().BeFalse();
            (order2 == order1).Should().BeFalse();
        }

        [Test]
        public void Order_OperatorNotEquals_DifferentValues_ShouldBeTrue()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(3000, 3);

            // Act & Assert
            (order1 != order2).Should().BeTrue();
        }

        [Test]
        public void Order_OperatorNotEquals_SameValues_ShouldBeFalse()
        {
            // Arrange
            var order1 = new Order(5000, 3);
            var order2 = new Order(5000, 3);

            // Act & Assert
            (order1 != order2).Should().BeFalse();
        }

        [Test]
        public void Order_DefaultConstructor_ShouldCreateWithDefaultValues()
        {
            // Arrange & Act
            var order = new Order();

            // Assert
            order.Length.Should().Be(0);
            order.Quantity.Should().Be(0);
        }

        [Test]
        public void Order_Constructor_ValidValues_ShouldSetProperties()
        {
            // Arrange & Act
            var order = new Order(5000, 3);

            // Assert
            order.Length.Should().Be(5000);
            order.Quantity.Should().Be(3);
        }

        [Test]
        public void Order_Constructor_ZeroLength_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new Order(0, 3);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("length");
        }

        [Test]
        public void Order_Constructor_NegativeLength_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new Order(-100, 3);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("length");
        }

        [Test]
        public void Order_Constructor_ZeroQuantity_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new Order(5000, 0);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("quantity");
        }

        [Test]
        public void Order_Constructor_NegativeQuantity_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new Order(5000, -5);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("quantity");
        }

        #endregion

        #region RebarStock Tests

        [Test]
        public void RebarStock_Equals_SameValues_ShouldBeEqual()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(12000, 10);

            // Act & Assert
            stock1.Equals(stock2).Should().BeTrue();
            stock2.Equals(stock1).Should().BeTrue();
        }

        [Test]
        public void RebarStock_Equals_DifferentLength_ShouldNotBeEqual()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(6000, 10);

            // Act & Assert
            stock1.Equals(stock2).Should().BeFalse();
        }

        [Test]
        public void RebarStock_Equals_DifferentQuantity_ShouldNotBeEqual()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(12000, 20);

            // Act & Assert
            stock1.Equals(stock2).Should().BeFalse();
        }

        [Test]
        public void RebarStock_Equals_Null_ShouldReturnFalse()
        {
            // Arrange
            var stock = new RebarStock(12000, 10);

            // Act & Assert
            stock.Equals(null).Should().BeFalse();
        }

        [Test]
        public void RebarStock_Equals_SameReference_ShouldBeEqual()
        {
            // Arrange
            var stock = new RebarStock(12000, 10);

            // Act & Assert
            stock.Equals(stock).Should().BeTrue();
        }

        [Test]
        public void RebarStock_Equals_ObjectOverload_ShouldWork()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            object stock2 = new RebarStock(12000, 10);

            // Act & Assert
            stock1.Equals(stock2).Should().BeTrue();
        }

        [Test]
        public void RebarStock_Equals_DifferentType_ShouldReturnFalse()
        {
            // Arrange
            var stock = new RebarStock(12000, 10);
            var notAStock = "not a stock";

            // Act & Assert
            stock.Equals(notAStock).Should().BeFalse();
        }

        [Test]
        public void RebarStock_GetHashCode_EqualObjects_ShouldHaveSameHashCode()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(12000, 10);

            // Act & Assert
            stock1.GetHashCode().Should().Be(stock2.GetHashCode());
        }

        [Test]
        public void RebarStock_GetHashCode_DifferentObjects_ShouldLikelyDiffer()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(6000, 20);

            // Act & Assert
            stock1.GetHashCode().Should().NotBe(stock2.GetHashCode());
        }

        [Test]
        public void RebarStock_ToString_ShouldReturnExpectedFormat()
        {
            // Arrange
            var stock = new RebarStock(12000, 10);

            // Act
            var result = stock.ToString();

            // Assert
            result.Should().Be("RebarStock(12000mm x10)");
        }

        [Test]
        public void RebarStock_OperatorEquals_SameValues_ShouldBeTrue()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(12000, 10);

            // Act & Assert
            (stock1 == stock2).Should().BeTrue();
        }

        [Test]
        public void RebarStock_OperatorEquals_DifferentValues_ShouldBeFalse()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(6000, 10);

            // Act & Assert
            (stock1 == stock2).Should().BeFalse();
        }

        [Test]
        public void RebarStock_OperatorEquals_BothNull_ShouldBeTrue()
        {
            // Arrange
            RebarStock? stock1 = null;
            RebarStock? stock2 = null;

            // Act & Assert
            (stock1 == stock2).Should().BeTrue();
        }

        [Test]
        public void RebarStock_OperatorEquals_OneNull_ShouldBeFalse()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            RebarStock? stock2 = null;

            // Act & Assert
            (stock1 == stock2).Should().BeFalse();
            (stock2 == stock1).Should().BeFalse();
        }

        [Test]
        public void RebarStock_OperatorNotEquals_DifferentValues_ShouldBeTrue()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(6000, 10);

            // Act & Assert
            (stock1 != stock2).Should().BeTrue();
        }

        [Test]
        public void RebarStock_OperatorNotEquals_SameValues_ShouldBeFalse()
        {
            // Arrange
            var stock1 = new RebarStock(12000, 10);
            var stock2 = new RebarStock(12000, 10);

            // Act & Assert
            (stock1 != stock2).Should().BeFalse();
        }

        [Test]
        public void RebarStock_DefaultConstructor_ShouldCreateWithDefaultValues()
        {
            // Arrange & Act
            var stock = new RebarStock();

            // Assert
            stock.Length.Should().Be(0);
            stock.Quantity.Should().Be(0);
        }

        [Test]
        public void RebarStock_Constructor_ValidValues_ShouldSetProperties()
        {
            // Arrange & Act
            var stock = new RebarStock(12000, 10);

            // Assert
            stock.Length.Should().Be(12000);
            stock.Quantity.Should().Be(10);
        }

        [Test]
        public void RebarStock_Constructor_ZeroLength_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new RebarStock(0, 10);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("length");
        }

        [Test]
        public void RebarStock_Constructor_NegativeLength_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new RebarStock(-100, 10);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("length");
        }

        [Test]
        public void RebarStock_Constructor_ZeroQuantity_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new RebarStock(12000, 0);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("quantity");
        }

        [Test]
        public void RebarStock_Constructor_NegativeQuantity_ShouldThrow()
        {
            // Arrange & Act
            var action = () => new RebarStock(12000, -5);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("quantity");
        }

        #endregion

        #region Dictionary/HashSet Usage Tests

        [Test]
        public void Order_InHashSet_ShouldHandleDuplicatesCorrectly()
        {
            // Arrange
            var set = new HashSet<Order>
            {
                new Order(5000, 3),
                new Order(5000, 3),  // 동일한 값 - 중복
                new Order(3000, 5)
            };

            // Assert
            set.Count.Should().Be(2);
            set.Should().Contain(new Order(5000, 3));
            set.Should().Contain(new Order(3000, 5));
        }

        [Test]
        public void RebarStock_InHashSet_ShouldHandleDuplicatesCorrectly()
        {
            // Arrange
            var set = new HashSet<RebarStock>
            {
                new RebarStock(12000, 10),
                new RebarStock(12000, 10),  // 동일한 값 - 중복
                new RebarStock(6000, 20)
            };

            // Assert
            set.Count.Should().Be(2);
            set.Should().Contain(new RebarStock(12000, 10));
            set.Should().Contain(new RebarStock(6000, 20));
        }

        [Test]
        public void Order_AsDictionaryKey_ShouldWorkCorrectly()
        {
            // Arrange
            var dict = new Dictionary<Order, string>
            {
                { new Order(5000, 3), "First" },
                { new Order(3000, 5), "Second" }
            };

            // Act & Assert
            dict[new Order(5000, 3)].Should().Be("First");
            dict[new Order(3000, 5)].Should().Be("Second");
            dict.ContainsKey(new Order(5000, 3)).Should().BeTrue();
        }

        [Test]
        public void RebarStock_AsDictionaryKey_ShouldWorkCorrectly()
        {
            // Arrange
            var dict = new Dictionary<RebarStock, string>
            {
                { new RebarStock(12000, 10), "Large" },
                { new RebarStock(6000, 20), "Small" }
            };

            // Act & Assert
            dict[new RebarStock(12000, 10)].Should().Be("Large");
            dict[new RebarStock(6000, 20)].Should().Be("Small");
            dict.ContainsKey(new RebarStock(12000, 10)).Should().BeTrue();
        }

        #endregion
    }
}
