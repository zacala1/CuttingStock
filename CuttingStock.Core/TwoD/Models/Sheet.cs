using System;

namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>
    /// Represents a 2D rectangular stock sheet (e.g. plywood, glass, metal plate).
    /// 2D analogue of <see cref="CuttingStock.Core.Models.RebarStock"/>.
    /// </summary>
    public sealed class Sheet : IEquatable<Sheet>
    {
        /// <summary>Sheet width (mm). Strictly positive.</summary>
        public int Width { get; }

        /// <summary>Sheet height (mm). Strictly positive.</summary>
        public int Height { get; }

        /// <summary>Number of identical sheets available. Strictly positive.</summary>
        public int Quantity { get; }

        /// <summary>Total area of one sheet in mm².</summary>
        public long Area => (long)Width * Height;

        /// <summary>
        /// Creates a sheet definition.
        /// </summary>
        /// <param name="width">Sheet width in mm (must be &gt; 0).</param>
        /// <param name="height">Sheet height in mm (must be &gt; 0).</param>
        /// <param name="quantity">Available quantity (must be &gt; 0).</param>
        public Sheet(int width, int height, int quantity)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be > 0.");
            Width = width;
            Height = height;
            Quantity = quantity;
        }

        /// <inheritdoc />
        public bool Equals(Sheet? other) =>
            other is not null && Width == other.Width && Height == other.Height && Quantity == other.Quantity;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Sheet s && Equals(s);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Width, Height, Quantity);

        /// <inheritdoc />
        public override string ToString() => $"Sheet({Width}x{Height} x{Quantity})";

        /// <summary>Equality operator.</summary>
        public static bool operator ==(Sheet? a, Sheet? b) => Equals(a, b);
        /// <summary>Inequality operator.</summary>
        public static bool operator !=(Sheet? a, Sheet? b) => !Equals(a, b);
    }
}
