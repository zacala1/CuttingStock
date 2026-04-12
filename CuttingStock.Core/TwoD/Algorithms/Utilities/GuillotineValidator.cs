using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Guillotine compliance checker (Beasley 1985 recursive separator test).
    /// </summary>
    public static class GuillotineValidator
    {
        /// <summary>
        /// True if the rectangles can be recursively separated by edge-to-edge cuts.
        /// </summary>
        public static bool IsGuillotineCompliant(
            int outerX, int outerY, int outerW, int outerH,
            IList<(int x, int y, int w, int h)> rects)
        {
            if (rects.Count <= 1) return true;
            return TrySplit(outerX, outerY, outerW, outerH, rects);
        }

        /// <summary>Convenience: validates a pattern against its sheet dimensions.</summary>
        public static bool IsGuillotineCompliant(CuttingPattern2D pattern, int trim = 0)
        {
            var rects = new List<(int, int, int, int)>(pattern.Placements.Count);
            foreach (var p in pattern.Placements)
                rects.Add((p.X, p.Y, p.Width, p.Height));
            return IsGuillotineCompliant(
                trim, trim,
                pattern.Sheet.Width - 2 * trim,
                pattern.Sheet.Height - 2 * trim,
                rects);
        }

        /// <summary>
        /// Validates a cut tree structurally: children must exactly tile the parent
        /// along the split axis with no overlap or gap.
        /// </summary>
        public static bool IsValidTree(GuillotineNode node)
        {
            if (node.Kind == NodeKind.Leaf || node.Kind == NodeKind.Waste) return true;
            if (node.Children.Count == 0) return false;

            int sumW = 0, sumH = 0;
            foreach (var c in node.Children)
            {
                if (c.X < node.X || c.Y < node.Y || c.Right > node.Right || c.Bottom > node.Bottom)
                    return false;
                if (!IsValidTree(c)) return false;
            }

            if (node.Kind == NodeKind.HCut)
            {
                // children stacked vertically with same X/W as parent
                int prevBottom = node.Y;
                foreach (var c in node.Children)
                {
                    if (c.X != node.X || c.Width != node.Width) return false;
                    if (c.Y != prevBottom) return false;
                    prevBottom = c.Bottom;
                    sumH += c.Height;
                }
                if (prevBottom != node.Bottom) return false;
                return sumH == node.Height;
            }
            else // VCut
            {
                int prevRight = node.X;
                foreach (var c in node.Children)
                {
                    if (c.Y != node.Y || c.Height != node.Height) return false;
                    if (c.X != prevRight) return false;
                    prevRight = c.Right;
                    sumW += c.Width;
                }
                if (prevRight != node.Right) return false;
                return sumW == node.Width;
            }
        }

        private static bool TrySplit(
            int x0, int y0, int w, int h,
            IList<(int x, int y, int w, int h)> rects)
        {
            if (rects.Count <= 1) return true;
            int x1 = x0 + w, y1 = y0 + h;

            // Try every vertical guillotine line: at the right edge of any rect.
            // A line at X is valid iff no rect strictly straddles it.
            var xCandidates = new HashSet<int>();
            foreach (var r in rects)
            {
                if (r.x > x0 && r.x < x1) xCandidates.Add(r.x);
                int rx2 = r.x + r.w;
                if (rx2 > x0 && rx2 < x1) xCandidates.Add(rx2);
            }
            foreach (var xs in xCandidates)
            {
                bool ok = true;
                var left = new List<(int, int, int, int)>();
                var right = new List<(int, int, int, int)>();
                foreach (var r in rects)
                {
                    int rx2 = r.x + r.w;
                    if (rx2 <= xs) left.Add(r);
                    else if (r.x >= xs) right.Add(r);
                    else { ok = false; break; }
                }
                if (!ok) continue;
                if (left.Count == 0 || right.Count == 0) continue; // not progress
                if (TrySplit(x0, y0, xs - x0, h, left) &&
                    TrySplit(xs, y0, x1 - xs, h, right))
                    return true;
            }

            // Try horizontal lines.
            var yCandidates = new HashSet<int>();
            foreach (var r in rects)
            {
                if (r.y > y0 && r.y < y1) yCandidates.Add(r.y);
                int ry2 = r.y + r.h;
                if (ry2 > y0 && ry2 < y1) yCandidates.Add(ry2);
            }
            foreach (var ys in yCandidates)
            {
                bool ok = true;
                var top = new List<(int, int, int, int)>();
                var bot = new List<(int, int, int, int)>();
                foreach (var r in rects)
                {
                    int ry2 = r.y + r.h;
                    if (ry2 <= ys) top.Add(r);
                    else if (r.y >= ys) bot.Add(r);
                    else { ok = false; break; }
                }
                if (!ok) continue;
                if (top.Count == 0 || bot.Count == 0) continue;
                if (TrySplit(x0, y0, w, ys - y0, top) &&
                    TrySplit(x0, ys, w, y1 - ys, bot))
                    return true;
            }

            return false;
        }
    }
}
