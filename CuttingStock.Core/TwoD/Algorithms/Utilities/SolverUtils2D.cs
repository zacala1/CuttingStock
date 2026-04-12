using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Shared helpers: input expansion, ordering, validation, finalization.</summary>
    public static class SolverUtils2D
    {
        /// <summary>Expands orders by quantity into flat list, preserving original index.</summary>
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

        public static List<Sheet> OrderSheets(List<Sheet> sheets, SolverOptions2D options)
        {
            return options.UsageOrder == CuttingStock.Core.Domain.StockUsageOrder.SmallToLarge
                ? sheets.OrderBy(s => s.Area).ToList()
                : sheets.OrderByDescending(s => s.Area).ToList();
        }

        public static void Finalize(SolverResult2D result, SolverOptions2D options)
        {
            result.TotalCost = (long)Math.Round(result.TotalWasteArea * (double)options.AlphaArea);
        }

        /// <summary>True if any two placements are closer than kerf apart on both axes.</summary>
        public static bool HasOverlap(List<Placement> placements, int kerf)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                var a = placements[i];
                for (int j = i + 1; j < placements.Count; j++)
                {
                    var b = placements[j];
                    bool sepX = a.Right + kerf <= b.X || b.Right + kerf <= a.X;
                    bool sepY = a.Bottom + kerf <= b.Y || b.Bottom + kerf <= a.Y;
                    if (!sepX && !sepY) return true;
                }
            }
            return false;
        }

        public static bool WithinSheet(List<Placement> placements, Sheet sheet, int trim)
        {
            int x0 = trim, y0 = trim;
            int x1 = sheet.Width - trim, y1 = sheet.Height - trim;
            foreach (var p in placements)
                if (p.X < x0 || p.Y < y0 || p.Right > x1 || p.Bottom > y1) return false;
            return true;
        }

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
