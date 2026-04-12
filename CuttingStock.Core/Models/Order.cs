namespace CuttingStock.Core.Models
{
    /// <summary>1D rebar order. All lengths in mm.</summary>
    public class Order : IEquatable<Order>
    {
        public int Length { get; set; }
        public int Quantity { get; set; }

        public Order() { }

        public Order(int length, int quantity)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Length = length;
            Quantity = quantity;
        }

        public bool Equals(Order? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Length == other.Length && Quantity == other.Quantity;
        }

        public override bool Equals(object? obj) => Equals(obj as Order);
        public override int GetHashCode() => HashCode.Combine(Length, Quantity);
        public override string ToString() => $"Order({Length}mm x{Quantity})";

        public static bool operator ==(Order? left, Order? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }
        public static bool operator !=(Order? left, Order? right) => !(left == right);
    }
}
