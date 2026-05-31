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

        /// <summary>
        /// Validates sheets and orders for 2D solvers. Returns true if the result has
        /// been finalized (caller should return) — either due to invalid sheets or an
        /// empty order list (treated as trivially solved with no patterns).
        /// </summary>
        public static bool ValidateInputs(
            List<Sheet>? sheets, List<RectOrder>? orders, SolverResult2D result)
        {
            if (sheets == null || sheets.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "At least one sheet must be provided.";
                return true;
            }
            if (orders == null || orders.Count == 0)
            {
                // Empty demand is trivially solved with no patterns; success stays true.
                return true;
            }
            return false;
        }

        public static List<Sheet> OrderSheets(List<Sheet> sheets, SolverOptions2D options)
        {
            return options.UsageOrder == CuttingStock.Core.Domain.StockUsageOrder.SmallToLarge
                ? sheets.OrderBy(s => s.Area).ToList()
                : sheets.OrderByDescending(s => s.Area).ToList();
        }

        /// <summary>
        /// Collapse same-dimension Sheet rows into a single row with summed Quantity.
        /// Required by every solver because Sheet.Equals/GetHashCode are structural —
        /// two distinct rows with the same (Width, Height, Quantity) collide as the
        /// same key in Dictionary&lt;Sheet,_&gt;, hiding half the inventory and causing
        /// either an ArgumentException or a silently low capacity.
        /// </summary>
        public static List<Sheet> AggregateByDims(List<Sheet> sheets)
        {
            return sheets
                .GroupBy(s => (s.Width, s.Height))
                .Select(g => new Sheet(g.Key.Width, g.Key.Height, g.Sum(s => s.Quantity)))
                .ToList();
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

        public static List<CuttingPattern2D> TrimToDemand(
            List<CuttingPattern2D> patterns,
            int[] demand,
            out int[] produced)
        {
            produced = new int[demand.Length];
            var trimmed = new List<CuttingPattern2D>();

            foreach (var pattern in patterns)
            {
                for (int copy = 0; copy < pattern.Multiplicity; copy++)
                {
                    var placements = new List<Placement>();
                    foreach (var placement in pattern.Placements)
                    {
                        if (placement.OrderIndex < 0 || placement.OrderIndex >= demand.Length)
                            throw new ArgumentOutOfRangeException(nameof(patterns), "Pattern placement references an unknown order index.");

                        if (produced[placement.OrderIndex] >= demand[placement.OrderIndex])
                            continue;

                        placements.Add(ClonePlacement(placement));
                        produced[placement.OrderIndex]++;
                    }

                    if (placements.Count == 0)
                        continue;

                    trimmed.Add(new CuttingPattern2D
                    {
                        Sheet = pattern.Sheet,
                        Multiplicity = 1,
                        Placements = placements,
                    });
                }
            }

            return trimmed;
        }

        private static Placement ClonePlacement(Placement p) => new()
        {
            OrderIndex = p.OrderIndex,
            X = p.X,
            Y = p.Y,
            Width = p.Width,
            Height = p.Height,
            Rotated = p.Rotated,
        };
    }
}
