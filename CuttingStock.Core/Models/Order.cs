namespace CuttingStock.Core.Models
{
    /// <summary>1D rebar order. All lengths in mm.</summary>
    public sealed class Order : IEquatable<Order>
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

        public bool Equals(Order? other) =>
            other is not null && Length == other.Length && Quantity == other.Quantity;
        public override bool Equals(object? obj) => obj is Order o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Length, Quantity);
        public override string ToString() => $"Order({Length}mm x{Quantity})";
        public static bool operator ==(Order? a, Order? b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(Order? a, Order? b) => !(a == b);
    }
}
