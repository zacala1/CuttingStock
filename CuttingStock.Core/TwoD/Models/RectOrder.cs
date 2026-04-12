using System;

namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>
    /// Rectangular item to cut from sheets. All dimensions in mm.
    /// <see cref="AllowRotation"/> is a per-item flag; the solver's global flag must also
    /// be true for rotation to take effect.
    /// </summary>
    public sealed class RectOrder : IEquatable<RectOrder>
    {
        public int Width { get; }
        public int Height { get; }
        public int Quantity { get; }
        public bool AllowRotation { get; }
        public long Area => (long)Width * Height;

        public RectOrder(int width, int height, int quantity, bool allowRotation = true)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            Width = width;
            Height = height;
            Quantity = quantity;
            AllowRotation = allowRotation;
        }

        public bool Equals(RectOrder? other) =>
            other is not null && Width == other.Width && Height == other.Height
                              && Quantity == other.Quantity && AllowRotation == other.AllowRotation;
        public override bool Equals(object? obj) => obj is RectOrder o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Width, Height, Quantity, AllowRotation);
        public override string ToString() => $"RectOrder({Width}x{Height} x{Quantity}{(AllowRotation ? " R" : "")})";
        public static bool operator ==(RectOrder? a, RectOrder? b) => Equals(a, b);
        public static bool operator !=(RectOrder? a, RectOrder? b) => !Equals(a, b);
    }
}
