using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Conversions between public 2D patterns, DP results, and master columns.</summary>
    internal static class PatternMaterializer
    {
        public static PatternColumn FromPattern(CuttingPattern2D pattern, int orderCount)
        {
            var column = new PatternColumn
            {
                Sheet = pattern.Sheet,
                Counts = new int[orderCount],
            };

            foreach (var placement in pattern.Placements)
            {
                column.Counts[placement.OrderIndex]++;
                column.Placements.Add(ClonePlacement(placement));
            }

            return column;
        }

        public static PatternColumn FromDpResult(
            Sheet sheet,
            GuillotineKnapsackDp.Result result,
            int orderCount,
            int trim)
        {
            var column = new PatternColumn
            {
                Sheet = sheet,
                Counts = new int[orderCount],
            };

            foreach (var placement in result.Placements)
            {
                column.Counts[placement.OrderIndex]++;
                column.Placements.Add(new Placement
                {
                    OrderIndex = placement.OrderIndex,
                    X = placement.X + trim,
                    Y = placement.Y + trim,
                    Width = placement.Width,
                    Height = placement.Height,
                    Rotated = placement.Rotated,
                });
            }

            return column;
        }

        public static CuttingPattern2D ToPattern(PatternColumn column, int multiplicity)
        {
            return new CuttingPattern2D
            {
                Sheet = column.Sheet,
                Multiplicity = multiplicity,
                Placements = column.Placements.ConvertAll(ClonePlacement),
            };
        }

        public static List<CuttingPattern2D> ToPatterns(
            IReadOnlyList<PatternColumn> columns,
            IReadOnlyList<int> multiplicities)
        {
            var patterns = new List<CuttingPattern2D>();
            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                int multiplicity = multiplicities[columnIndex];
                if (multiplicity > 0)
                    patterns.Add(ToPattern(columns[columnIndex], multiplicity));
            }

            return patterns;
        }

        public static Placement ClonePlacement(Placement placement)
        {
            return new Placement
            {
                OrderIndex = placement.OrderIndex,
                X = placement.X,
                Y = placement.Y,
                Width = placement.Width,
                Height = placement.Height,
                Rotated = placement.Rotated,
            };
        }
    }
}
