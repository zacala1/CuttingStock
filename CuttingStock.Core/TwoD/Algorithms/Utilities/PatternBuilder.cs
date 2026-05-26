using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Builds a <see cref="GuillotineNode"/> tree from flat placements via recursive splitting.
    /// Returns null if the layout is not guillotine-decomposable.
    /// </summary>
    public static class PatternBuilder
    {
        public static GuillotineNode? BuildTree(int sheetWidth, int sheetHeight, List<Placement> placements)
        {
            // We pass placements as records carrying the original index for leaf reconstruction.
            var rects = placements
                .Select((p, idx) => (p.X, p.Y, p.Width, p.Height, OrigIdx: idx))
                .ToList();
            return BuildRec(0, 0, sheetWidth, sheetHeight, rects, placements);
        }

        private static GuillotineNode? BuildRec(
            int x0, int y0, int w, int h,
            List<(int x, int y, int w, int h, int OrigIdx)> rects,
            List<Placement> placements)
        {
            if (rects.Count == 0)
            {
                return new GuillotineNode { Kind = NodeKind.Waste, X = x0, Y = y0, Width = w, Height = h };
            }
            if (rects.Count == 1)
            {
                var r = rects[0];
                // Place the leaf in its exact position; surround with waste if rectangles don't match.
                if (r.x == x0 && r.y == y0 && r.w == w && r.h == h)
                {
                    var pl = placements[r.OrigIdx];
                    return new GuillotineNode
                    {
                        Kind = NodeKind.Leaf,
                        X = x0, Y = y0, Width = w, Height = h,
                        OrderIndex = pl.OrderIndex,
                        Rotated = pl.Rotated
                    };
                }
                // Single rect doesn't fill region: peel waste off one edge at a time.
                // TrySplit can't help here because TryPartition requires both sides non-empty.
                return SplitOffSingleRect(x0, y0, w, h, r, placements);
            }
            return TrySplit(x0, y0, w, h, rects, placements);
        }

        /// <summary>
        /// Decompose a region containing exactly one rectangle by peeling off waste
        /// from each side that doesn't touch the rect. At most 4 cuts.
        /// </summary>
        private static GuillotineNode? SplitOffSingleRect(
            int x0, int y0, int w, int h,
            (int x, int y, int w, int h, int OrigIdx) r,
            List<Placement> placements)
        {
            int x1 = x0 + w, y1 = y0 + h;

            // Peel left waste strip.
            if (r.x > x0)
            {
                var left  = new GuillotineNode { Kind = NodeKind.Waste, X = x0,  Y = y0, Width = r.x - x0, Height = h };
                var right = BuildRec(r.x, y0, x1 - r.x, h, new() { r }, placements);
                if (right == null) return null;
                return new GuillotineNode { Kind = NodeKind.VCut, X = x0, Y = y0, Width = w, Height = h, Children = new[] { left, right } };
            }
            // Peel right waste strip.
            int rx2 = r.x + r.w;
            if (rx2 < x1)
            {
                var left  = BuildRec(x0, y0, rx2 - x0, h, new() { r }, placements);
                if (left == null) return null;
                var right = new GuillotineNode { Kind = NodeKind.Waste, X = rx2, Y = y0, Width = x1 - rx2, Height = h };
                return new GuillotineNode { Kind = NodeKind.VCut, X = x0, Y = y0, Width = w, Height = h, Children = new[] { left, right } };
            }
            // Peel top waste strip.
            if (r.y > y0)
            {
                var top = new GuillotineNode { Kind = NodeKind.Waste, X = x0, Y = y0, Width = w, Height = r.y - y0 };
                var bot = BuildRec(x0, r.y, w, y1 - r.y, new() { r }, placements);
                if (bot == null) return null;
                return new GuillotineNode { Kind = NodeKind.HCut, X = x0, Y = y0, Width = w, Height = h, Children = new[] { top, bot } };
            }
            // Peel bottom waste strip.
            int ry2 = r.y + r.h;
            if (ry2 < y1)
            {
                var top = BuildRec(x0, y0, w, ry2 - y0, new() { r }, placements);
                if (top == null) return null;
                var bot = new GuillotineNode { Kind = NodeKind.Waste, X = x0, Y = ry2, Width = w, Height = y1 - ry2 };
                return new GuillotineNode { Kind = NodeKind.HCut, X = x0, Y = y0, Width = w, Height = h, Children = new[] { top, bot } };
            }
            // Rect fills the region exactly — already handled by the caller.
            return null;
        }

        private static GuillotineNode? TrySplit(
            int x0, int y0, int w, int h,
            List<(int x, int y, int w, int h, int OrigIdx)> rects,
            List<Placement> placements)
        {
            int x1 = x0 + w, y1 = y0 + h;

            // Vertical lines.
            var xs = new SortedSet<int>();
            foreach (var r in rects)
            {
                if (r.x > x0 && r.x < x1) xs.Add(r.x);
                int rx2 = r.x + r.w;
                if (rx2 > x0 && rx2 < x1) xs.Add(rx2);
            }
            foreach (var xc in xs)
            {
                if (TryPartitionV(rects, xc, out var left, out var right))
                {
                    var l = BuildRec(x0, y0, xc - x0, h, left, placements);
                    var rN = BuildRec(xc, y0, x1 - xc, h, right, placements);
                    if (l != null && rN != null)
                        return new GuillotineNode
                        {
                            Kind = NodeKind.VCut,
                            X = x0, Y = y0, Width = w, Height = h,
                            Children = new[] { l, rN }
                        };
                }
            }

            // Horizontal lines.
            var ys = new SortedSet<int>();
            foreach (var r in rects)
            {
                if (r.y > y0 && r.y < y1) ys.Add(r.y);
                int ry2 = r.y + r.h;
                if (ry2 > y0 && ry2 < y1) ys.Add(ry2);
            }
            foreach (var yc in ys)
            {
                if (TryPartitionH(rects, yc, out var top, out var bot))
                {
                    var tN = BuildRec(x0, y0, w, yc - y0, top, placements);
                    var bN = BuildRec(x0, yc, w, y1 - yc, bot, placements);
                    if (tN != null && bN != null)
                        return new GuillotineNode
                        {
                            Kind = NodeKind.HCut,
                            X = x0, Y = y0, Width = w, Height = h,
                            Children = new[] { tN, bN }
                        };
                }
            }

            // Couldn't split — non-guillotine arrangement.
            return null;
        }

        private static bool TryPartitionV(
            List<(int x, int y, int w, int h, int OrigIdx)> rects, int xc,
            out List<(int x, int y, int w, int h, int OrigIdx)> left,
            out List<(int x, int y, int w, int h, int OrigIdx)> right)
        {
            left = new(); right = new();
            foreach (var r in rects)
            {
                int rx2 = r.x + r.w;
                if (rx2 <= xc) left.Add(r);
                else if (r.x >= xc) right.Add(r);
                else return false;
            }
            return left.Count > 0 && right.Count > 0;
        }

        private static bool TryPartitionH(
            List<(int x, int y, int w, int h, int OrigIdx)> rects, int yc,
            out List<(int x, int y, int w, int h, int OrigIdx)> top,
            out List<(int x, int y, int w, int h, int OrigIdx)> bot)
        {
            top = new(); bot = new();
            foreach (var r in rects)
            {
                int ry2 = r.y + r.h;
                if (ry2 <= yc) top.Add(r);
                else if (r.y >= yc) bot.Add(r);
                else return false;
            }
            return top.Count > 0 && bot.Count > 0;
        }
    }
}
