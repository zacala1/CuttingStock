using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Shared post-solve result validation for 2D solvers.</summary>
    public static class TwoDResultValidator
    {
        public static string? ValidateSuccessfulResult(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            SolverResult2D result)
        {
            var inventory = sheets
                .GroupBy(s => (s.Width, s.Height))
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
            var usedSheets = new Dictionary<(int Width, int Height), int>();
            var produced = new int[orders.Count];

            for (int patternIndex = 0; patternIndex < result.Patterns.Count; patternIndex++)
            {
                var pattern = result.Patterns[patternIndex];
                if (pattern.Multiplicity <= 0)
                    return $"Pattern {patternIndex + 1} has non-positive multiplicity {pattern.Multiplicity}.";

                var sheetKey = (pattern.Sheet.Width, pattern.Sheet.Height);
                if (!inventory.ContainsKey(sheetKey))
                    return $"Pattern {patternIndex + 1} uses unknown sheet {pattern.Sheet.Width}x{pattern.Sheet.Height}.";

                usedSheets.TryGetValue(sheetKey, out int used);
                usedSheets[sheetKey] = used + pattern.Multiplicity;

                if (pattern.UsedArea > pattern.Sheet.Area)
                    return $"Pattern {patternIndex + 1} used area {pattern.UsedArea} exceeds sheet area {pattern.Sheet.Area}.";

                if (!TwoDPlacementMath.WithinSheet(pattern.Placements, pattern.Sheet, options.Trim))
                    return $"Pattern {patternIndex + 1} has a placement outside the trimmed sheet.";

                if (TwoDPlacementMath.HasOverlap(pattern.Placements, options.Kerf))
                    return $"Pattern {patternIndex + 1} has overlapping placements.";

                var rects = pattern.Placements
                    .Select(p => (p.X, p.Y, p.Width, p.Height))
                    .ToList();
                if (!GuillotineValidator.IsGuillotineCompliant(
                        0, 0, pattern.Sheet.Width, pattern.Sheet.Height, rects))
                    return $"Pattern {patternIndex + 1} is not guillotine-compliant.";

                foreach (var placement in pattern.Placements)
                {
                    if (placement.OrderIndex < 0 || placement.OrderIndex >= orders.Count)
                        return $"Pattern {patternIndex + 1} references unknown order index {placement.OrderIndex}.";

                    var order = orders[placement.OrderIndex];
                    bool matchesAsIs = placement.Width == order.Width && placement.Height == order.Height;
                    bool matchesRotated = placement.Width == order.Height && placement.Height == order.Width;
                    if (!matchesAsIs && !matchesRotated)
                    {
                        return $"Pattern {patternIndex + 1} placement for order {placement.OrderIndex} has dimensions {placement.Width}x{placement.Height}, expected {order.Width}x{order.Height}.";
                    }

                    if (placement.Rotated)
                    {
                        if (!options.AllowRotation || !order.AllowRotation || !matchesRotated)
                            return $"Pattern {patternIndex + 1} illegally rotates order {placement.OrderIndex}.";
                    }
                    else if (!matchesAsIs)
                    {
                        return $"Pattern {patternIndex + 1} uses rotated dimensions for order {placement.OrderIndex} without marking it rotated.";
                    }

                    produced[placement.OrderIndex] += pattern.Multiplicity;
                }
            }

            foreach (var (sheet, used) in usedSheets)
            {
                if (used > inventory[sheet])
                    return $"Sheet {sheet.Width}x{sheet.Height} usage {used} exceeds inventory {inventory[sheet]}.";
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (produced[i] != orders[i].Quantity)
                    return $"Order {i} produced {produced[i]}, expected {orders[i].Quantity}.";
            }

            return null;
        }
    }
}
