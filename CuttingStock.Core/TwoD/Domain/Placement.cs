namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// A placed rectangle inside a sheet, with its post-rotation dimensions.
    /// 2D analogue of <see cref="CuttingStock.Core.Domain.Cut"/>.
    /// </summary>
    public sealed class Placement
    {
        /// <summary>Index into the input order list.</summary>
        public int OrderIndex { get; init; }

        /// <summary>Top-left X coordinate (mm).</summary>
        public int X { get; init; }

        /// <summary>Top-left Y coordinate (mm).</summary>
        public int Y { get; init; }

        /// <summary>Effective width after any rotation (mm).</summary>
        public int Width { get; init; }

        /// <summary>Effective height after any rotation (mm).</summary>
        public int Height { get; init; }

        /// <summary>True when the item was placed rotated 90°.</summary>
        public bool Rotated { get; init; }

        /// <summary>Right edge (exclusive).</summary>
        public int Right => X + Width;

        /// <summary>Bottom edge (exclusive).</summary>
        public int Bottom => Y + Height;

        /// <summary>Area of the placed rectangle (mm²).</summary>
        public long Area => (long)Width * Height;

        /// <inheritdoc />
        public override string ToString() =>
            $"Place(O{OrderIndex} @({X},{Y}) {Width}x{Height}{(Rotated ? " ↻" : "")})";
    }
}
