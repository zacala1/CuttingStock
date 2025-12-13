namespace CuttingStock.Core.Models
{
    /// <summary>
    /// Represents a rebar stock item
    /// </summary>
    public class RebarStock : IEquatable<RebarStock>
    {
        /// <summary>
        /// Rebar length (mm)
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Stock quantity (pieces)
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public RebarStock()
        {
        }

        /// <summary>
        /// Constructor to set stock information
        /// </summary>
        /// <param name="length">Rebar length (mm). Must be greater than 0.</param>
        /// <param name="quantity">Stock quantity (pieces). Must be greater than 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">When length or quantity is less than or equal to 0</exception>
        public RebarStock(int length, int quantity)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "길이는 0보다 커야 합니다.");
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "수량은 0보다 커야 합니다.");

            Length = length;
            Quantity = quantity;
        }

        /// <summary>
        /// Compares two stocks for equality (based on length and quantity)
        /// </summary>
        public bool Equals(RebarStock? other)
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
            return Equals(obj as RebarStock);
        }

        /// <summary>
        /// Returns the hash code
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Length, Quantity);
        }

        /// <summary>
        /// Returns stock information as a string
        /// </summary>
        public override string ToString()
        {
            return $"RebarStock({Length}mm x {Quantity}개)";
        }

        /// <summary>
        /// Equality operator for comparing two stocks
        /// </summary>
        public static bool operator ==(RebarStock? left, RebarStock? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator for comparing two stocks
        /// </summary>
        public static bool operator !=(RebarStock? left, RebarStock? right)
        {
            return !(left == right);
        }
    }
}
