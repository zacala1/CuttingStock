namespace CuttingStock.Core.Models
{
    /// <summary>
    /// Rebar order information
    /// </summary>
    public class Order : IEquatable<Order>
    {
        /// <summary>
        /// Rebar length (mm)
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Order quantity (pieces)
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public Order()
        {
        }

        /// <summary>
        /// Constructor to set order information
        /// </summary>
        /// <param name="length">Rebar length (mm). Must be greater than 0.</param>
        /// <param name="quantity">Order quantity (pieces). Must be greater than 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">When length or quantity is less than or equal to 0</exception>
        public Order(int length, int quantity)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "길이는 0보다 커야 합니다.");
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "수량은 0보다 커야 합니다.");

            Length = length;
            Quantity = quantity;
        }

        /// <summary>
        /// Compares two orders for equality (based on length and quantity)
        /// </summary>
        public bool Equals(Order? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Length == other.Length && Quantity == other.Quantity;
        }

        /// <summary>
        /// Compares objects for equality
        /// </summary>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Order);
        }

        /// <summary>
        /// Returns the hash code
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Length, Quantity);
        }

        /// <summary>
        /// Returns order information as a string
        /// </summary>
        public override string ToString()
        {
            return $"Order({Length}mm x {Quantity}개)";
        }

        /// <summary>
        /// Equality operator for comparing two orders
        /// </summary>
        public static bool operator ==(Order? left, Order? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator for comparing two orders
        /// </summary>
        public static bool operator !=(Order? left, Order? right)
        {
            return !(left == right);
        }
    }
}
