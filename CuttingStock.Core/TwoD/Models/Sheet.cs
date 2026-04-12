using System;

namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>
    /// 2D rectangular stock sheet (plywood, glass, metal plate, etc.).
    /// All dimensions in mm. Immutable value type with structural equality.
    /// </summary>
    public sealed class Sheet : IEquatable<Sheet>
    {
        public int Width { get; }
        public int Height { get; }
        public int Quantity { get; }
        public long Area => (long)Width * Height;

        public Sheet(int width, int height, int quantity)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Width = width;
            Height = height;
            Quantity = quantity;
        }

        public bool Equals(Sheet? other) =>
            other is not null && Width == other.Width && Height == other.Height && Quantity == other.Quantity;
        public override bool Equals(object? obj) => obj is Sheet s && Equals(s);
        public override int GetHashCode() => HashCode.Combine(Width, Height, Quantity);
        public override string ToString() => $"Sheet({Width}x{Height} x{Quantity})";
        public static bool operator ==(Sheet? a, Sheet? b) => Equals(a, b);
        public static bool operator !=(Sheet? a, Sheet? b) => !Equals(a, b);
    }
}
