using System;

namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>
    /// Represents a 2D rectangular order to be cut from sheets.
    /// 2D analogue of <see cref="CuttingStock.Core.Models.Order"/>.
    /// </summary>
    public sealed class RectOrder : IEquatable<RectOrder>
    {
        /// <summary>Required width in mm.</summary>
        public int Width { get; }

        /// <summary>Required height in mm.</summary>
        public int Height { get; }

        /// <summary>Number of pieces required.</summary>
        public int Quantity { get; }

        /// <summary>
        /// If true, this item may be placed rotated by 90°.
        /// Solver-level <see cref="Domain.SolverOptions2D.AllowRotation"/> serves as the global default
        /// — when global rotation is disabled, this flag has no effect.
        /// </summary>
        public bool AllowRotation { get; }

        /// <summary>Area of one piece in mm².</summary>
        public long Area => (long)Width * Height;

        /// <summary>
        /// Creates an order.
        /// </summary>
        /// <param name="width">Width (must be &gt; 0).</param>
        /// <param name="height">Height (must be &gt; 0).</param>
        /// <param name="quantity">Quantity (must be &gt; 0).</param>
        /// <param name="allowRotation">Whether 90° rotation is permitted (default: true).</param>
        public RectOrder(int width, int height, int quantity, bool allowRotation = true)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be > 0.");
            Width = width;
            Height = height;
            Quantity = quantity;
            AllowRotation = allowRotation;
        }

        /// <inheritdoc />
        public bool Equals(RectOrder? other) =>
            other is not null && Width == other.Width && Height == other.Height
                              && Quantity == other.Quantity && AllowRotation == other.AllowRotation;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is RectOrder o && Equals(o);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Width, Height, Quantity, AllowRotation);

        /// <inheritdoc />
        public override string ToString() => $"RectOrder({Width}x{Height} x{Quantity}{(AllowRotation ? " ↻" : "")})";

        /// <summary>Equality operator.</summary>
        public static bool operator ==(RectOrder? a, RectOrder? b) => Equals(a, b);
        /// <summary>Inequality operator.</summary>
        public static bool operator !=(RectOrder? a, RectOrder? b) => !Equals(a, b);
    }
}
