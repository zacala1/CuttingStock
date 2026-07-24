using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace CuttingStock.UI.Services
{
    /// <summary>Reads 1D length/quantity rows from supported tabular files.</summary>
    public static class ScenarioImportService
    {
        public static IReadOnlyList<LengthQuantityInput> ReadLengthQuantityRows(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".csv" => ReadCsv(path),
                ".xlsx" => ReadWorkbook(path),
                _ => Array.Empty<LengthQuantityInput>(),
            };
        }

        private static IReadOnlyList<LengthQuantityInput> ReadCsv(string path)
        {
            string[] lines = File.ReadAllLines(path);
            IEnumerable<string> data = lines.Length > 0 &&
                                       !int.TryParse(
                                           lines[0].Split(',')[0].Trim(),
                                           out _)
                ? lines.Skip(1)
                : lines;

            var rows = new List<LengthQuantityInput>();
            foreach (string line in data)
            {
                string[] columns = line.Split(',');
                if (columns.Length >= 2 &&
                    TryParsePositive(columns[0], out int length) &&
                    TryParsePositive(columns[1], out int quantity))
                {
                    rows.Add(new LengthQuantityInput(length, quantity));
                }
            }

            return rows;
        }

        private static IReadOnlyList<LengthQuantityInput> ReadWorkbook(string path)
        {
            using var stream = File.OpenRead(path);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();
            var range = worksheet.RangeUsed();
            if (range == null) return Array.Empty<LengthQuantityInput>();

            var allRows = range.RowsUsed().ToList();
            IEnumerable<IXLRangeRow> data = allRows.Count > 0 &&
                                            !int.TryParse(
                                                allRows[0].Cell(1).GetValue<string>(),
                                                out _)
                ? allRows.Skip(1)
                : allRows;

            var rows = new List<LengthQuantityInput>();
            foreach (var row in data)
            {
                if (TryParsePositive(row.Cell(1).GetValue<string>(), out int length) &&
                    TryParsePositive(row.Cell(2).GetValue<string>(), out int quantity))
                {
                    rows.Add(new LengthQuantityInput(length, quantity));
                }
            }

            return rows;
        }

        private static bool TryParsePositive(string value, out int parsed)
        {
            return int.TryParse(value.Trim(), out parsed) && parsed > 0;
        }
    }
}
