using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Constructs a <see cref="GuillotineNode"/> tree from a flat placement list,
    /// using recursive sweep splitting (Beasley 1985 separator test).
    /// Returns <c>null</c> if the placements do not admit a guillotine decomposition.
    /// </summary>
    public static class PatternBuilder
    {
        /// <summary>
        /// Build a guillotine cut tree for a sheet with placements (rotation already
        /// resolved into <see cref="Placement.Width"/>/<see cref="Placement.Height"/>).
        /// </summary>
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
                // Need to split off the rectangle from waste — try a single guillotine cut.
                return TrySplit(x0, y0, w, h, rects, placements);
            }
            return TrySplit(x0, y0, w, h, rects, placements);
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
                            Children = { l, rN }
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
                            Children = { tN, bN }
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
