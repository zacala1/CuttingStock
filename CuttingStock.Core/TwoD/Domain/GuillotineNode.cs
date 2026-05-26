using System;
using System.Collections.Generic;

namespace CuttingStock.Core.TwoD.Domain
{
    public enum NodeKind
    {
        Leaf,   // placed item
        Waste,  // empty scrap
        HCut,   // horizontal split (children stacked vertically)
        VCut,   // vertical split (children stacked horizontally)
    }

    /// <summary>
    /// Node in a guillotine cut tree. Internal nodes (HCut/VCut) represent edge-to-edge
    /// straight cuts; leaves are items or waste. Coordinates are absolute within the sheet.
    /// Children is exposed as <see cref="IReadOnlyList{T}"/> so consumers can't mutate
    /// the tree after construction; PatternBuilder builds a concrete list and assigns it.
    /// </summary>
    public sealed class GuillotineNode
    {
        public NodeKind Kind { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        /// <summary>Set only on <see cref="NodeKind.Leaf"/> nodes.</summary>
        public int? OrderIndex { get; init; }
        public bool Rotated { get; init; }

        public IReadOnlyList<GuillotineNode> Children { get; init; } = Array.Empty<GuillotineNode>();

        public int Right => X + Width;
        public int Bottom => Y + Height;
        public long Area => (long)Width * Height;
    }
}
