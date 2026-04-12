using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Shared utilities for 2D guillotine solvers: input expansion, sheet ordering,
    /// solution finalization (cost &amp; metrics), and overlap/coverage checks.
    /// </summary>
    public static class SolverUtils2D
    {
        /// <summary>
        /// Expands an order list into one entry per required item, preserving the
        /// original index. Returned tuples are (originalOrderIndex, width, height, allowRotation).
        /// </summary>
        public static List<(int OrderIndex, int W, int H, bool Rot)> ExpandOrders(
            List<RectOrder> orders, bool globalAllowRotation)
        {
            var list = new List<(int, int, int, bool)>();
            for (int i = 0; i < orders.Count; i++)
            {
                var o = orders[i];
                bool rot = globalAllowRotation && o.AllowRotation;
                for (int k = 0; k < o.Quantity; k++)
                    list.Add((i, o.Width, o.Height, rot));
            }
            return list;
        }

        /// <summary>
        /// Returns sheets sorted by area according to <see cref="SolverOptions2D.UsageOrder"/>.
        /// </summary>
        public static List<Sheet> OrderSheets(List<Sheet> sheets, SolverOptions2D options)
        {
            return options.UsageOrder == CuttingStock.Core.Domain.StockUsageOrder.SmallToLarge
                ? sheets.OrderBy(s => s.Area).ToList()
                : sheets.OrderByDescending(s => s.Area).ToList();
        }

        /// <summary>
        /// Computes cost and writes summary fields onto a result.
        /// </summary>
        public static void Finalize(SolverResult2D result, SolverOptions2D options)
        {
            result.TotalCost = (long)Math.Round(result.TotalWasteArea * (double)options.AlphaArea);
        }

        /// <summary>
        /// Checks that no two placements overlap (kerf-aware: every placement is inflated
        /// by half a kerf on each side before the test, except along the sheet boundary).
        /// </summary>
        public static bool HasOverlap(List<Placement> placements, int kerf)
        {
            // O(n²) is plenty for the sizes we care about (≤ a few thousand items per sheet).
            for (int i = 0; i < placements.Count; i++)
            {
                var a = placements[i];
                for (int j = i + 1; j < placements.Count; j++)
                {
                    var b = placements[j];
                    int ax2 = a.Right, ay2 = a.Bottom;
                    int bx2 = b.Right, by2 = b.Bottom;
                    bool sepX = ax2 + kerf <= b.X || bx2 + kerf <= a.X;
                    bool sepY = ay2 + kerf <= b.Y || by2 + kerf <= a.Y;
                    if (!sepX && !sepY) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Verifies every placement lies within the given sheet (after trim).
        /// </summary>
        public static bool WithinSheet(List<Placement> placements, Sheet sheet, int trim)
        {
            int x0 = trim, y0 = trim;
            int x1 = sheet.Width - trim, y1 = sheet.Height - trim;
            foreach (var p in placements)
            {
                if (p.X < x0 || p.Y < y0 || p.Right > x1 || p.Bottom > y1) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the placed quantity per original order index across all patterns.
        /// </summary>
        public static int[] CountPlaced(List<CuttingPattern2D> patterns, int orderCount)
        {
            var counts = new int[orderCount];
            foreach (var pat in patterns)
            foreach (var p in pat.Placements)
                counts[p.OrderIndex] += pat.Multiplicity;
            return counts;
        }
    }
}
