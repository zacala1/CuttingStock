using System.Collections.Generic;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// Kind of a node in a guillotine cutting tree.
    /// </summary>
    public enum NodeKind
    {
        /// <summary>An item placed at this rectangle (leaf).</summary>
        Leaf,

        /// <summary>An empty waste area (leaf).</summary>
        Waste,

        /// <summary>Horizontal cut: the rectangle is split into a top and bottom child.</summary>
        HCut,

        /// <summary>Vertical cut: the rectangle is split into a left and right child.</summary>
        VCut
    }

    /// <summary>
    /// A node in a guillotine cutting tree.
    /// Internal nodes (HCut/VCut) represent edge-to-edge straight cuts and have children;
    /// leaf nodes are either items (<see cref="OrderIndex"/> set) or waste.
    /// Coordinates are in absolute sheet space.
    /// </summary>
    public sealed class GuillotineNode
    {
        /// <summary>Node kind.</summary>
        public NodeKind Kind { get; init; }

        /// <summary>Top-left X coordinate (mm) within the sheet.</summary>
        public int X { get; init; }

        /// <summary>Top-left Y coordinate (mm) within the sheet.</summary>
        public int Y { get; init; }

        /// <summary>Width of the rectangle (mm).</summary>
        public int Width { get; init; }

        /// <summary>Height of the rectangle (mm).</summary>
        public int Height { get; init; }

        /// <summary>Order index of the placed item (leaf with <see cref="NodeKind.Leaf"/> only).</summary>
        public int? OrderIndex { get; init; }

        /// <summary>True if the placed item is rotated 90°.</summary>
        public bool Rotated { get; init; }

        /// <summary>Children of an internal node. Empty for leaves.</summary>
        public List<GuillotineNode> Children { get; init; } = new();

        /// <summary>Right edge X (exclusive).</summary>
        public int Right => X + Width;

        /// <summary>Bottom edge Y (exclusive).</summary>
        public int Bottom => Y + Height;

        /// <summary>Area of this node's rectangle in mm².</summary>
        public long Area => (long)Width * Height;
    }
}
