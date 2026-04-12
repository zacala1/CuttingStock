namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// A rectangle placed inside a sheet, with post-rotation effective dimensions.
    /// </summary>
    public sealed class Placement
    {
        public int OrderIndex { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public bool Rotated { get; init; }

        public int Right => X + Width;
        public int Bottom => Y + Height;
        public long Area => (long)Width * Height;

        public override string ToString() =>
            $"Place(O{OrderIndex} @({X},{Y}) {Width}x{Height}{(Rotated ? " R" : "")})";
    }
}
