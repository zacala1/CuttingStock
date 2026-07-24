using System;
using System.Collections.Generic;

namespace CuttingStock.UI.Services
{
    /// <summary>Parses tabular clipboard text into plain input rows.</summary>
    public static class ClipboardRowParser
    {
        private static readonly char[] RowSeparators = ['\r', '\n'];
        private static readonly char[] ColumnSeparators = ['\t', ',', ';'];

        public static IReadOnlyList<LengthQuantityInput> ParseLengthQuantityRows(string? text)
        {
            var result = new List<LengthQuantityInput>();
            foreach (string[] columns in SplitRows(text))
            {
                if (columns.Length < 2 ||
                    !TryParsePositive(columns[0], out int length) ||
                    !TryParsePositive(columns[1], out int quantity))
                {
                    continue;
                }

                result.Add(new LengthQuantityInput(length, quantity));
            }

            return result;
        }

        public static IReadOnlyList<SheetInput> ParseSheetRows(string? text)
        {
            var result = new List<SheetInput>();
            foreach (string[] columns in SplitRows(text))
            {
                if (columns.Length < 3 ||
                    !TryParsePositive(columns[0], out int width) ||
                    !TryParsePositive(columns[1], out int height) ||
                    !TryParsePositive(columns[2], out int quantity))
                {
                    continue;
                }

                result.Add(new SheetInput(width, height, quantity));
            }

            return result;
        }

        public static IReadOnlyList<RectOrderInput> ParseRectOrderRows(string? text)
        {
            var result = new List<RectOrderInput>();
            foreach (string[] columns in SplitRows(text))
            {
                if (columns.Length < 3 ||
                    !TryParsePositive(columns[0], out int width) ||
                    !TryParsePositive(columns[1], out int height) ||
                    !TryParsePositive(columns[2], out int quantity))
                {
                    continue;
                }

                bool allowRotation = columns.Length < 4 ||
                                     ParseBoolean(columns[3], defaultValue: true);
                result.Add(new RectOrderInput(width, height, quantity, allowRotation));
            }

            return result;
        }

        private static IEnumerable<string[]> SplitRows(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            foreach (string row in text.Split(
                         RowSeparators,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                yield return row.Split(
                    ColumnSeparators,
                    StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private static bool TryParsePositive(string value, out int parsed)
        {
            return int.TryParse(value.Trim(), out parsed) && parsed > 0;
        }

        private static bool ParseBoolean(string value, bool defaultValue)
        {
            string normalized = value.Trim();
            if (bool.TryParse(normalized, out bool parsed)) return parsed;
            if (normalized == "1" ||
                normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized == "0" ||
                normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }
    }
}
