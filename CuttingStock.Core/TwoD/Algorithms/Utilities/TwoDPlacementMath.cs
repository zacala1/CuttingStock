using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Placement geometry helpers shared by 2D validation and tests.</summary>
    public static class TwoDPlacementMath
    {
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
            foreach (var pattern in patterns)
            foreach (var placement in pattern.Placements)
                counts[placement.OrderIndex] += pattern.Multiplicity;
            return counts;
        }
    }
}
