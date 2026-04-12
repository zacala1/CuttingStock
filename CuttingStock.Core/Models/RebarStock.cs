namespace CuttingStock.Core.Models
{
    /// <summary>Available rebar stock. All lengths in mm.</summary>
    public sealed class RebarStock : IEquatable<RebarStock>
    {
        public int Length { get; set; }
        public int Quantity { get; set; }

        public RebarStock() { }

        public RebarStock(int length, int quantity)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Length = length;
            Quantity = quantity;
        }

        public bool Equals(RebarStock? other) =>
            other is not null && Length == other.Length && Quantity == other.Quantity;
        public override bool Equals(object? obj) => obj is RebarStock s && Equals(s);
        public override int GetHashCode() => HashCode.Combine(Length, Quantity);
        public override string ToString() => $"RebarStock({Length}mm x{Quantity})";
        public static bool operator ==(RebarStock? a, RebarStock? b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(RebarStock? a, RebarStock? b) => !(a == b);
    }
}
