namespace CuttingStock.Core.Models
{
    /// <summary>Available rebar stock. All lengths in mm.</summary>
    public class RebarStock : IEquatable<RebarStock>
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

        public bool Equals(RebarStock? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Length == other.Length && Quantity == other.Quantity;
        }

        public override bool Equals(object? obj) => Equals(obj as RebarStock);
        public override int GetHashCode() => HashCode.Combine(Length, Quantity);
        public override string ToString() => $"RebarStock({Length}mm x{Quantity})";

        public static bool operator ==(RebarStock? left, RebarStock? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }
        public static bool operator !=(RebarStock? left, RebarStock? right) => !(left == right);
    }
}
