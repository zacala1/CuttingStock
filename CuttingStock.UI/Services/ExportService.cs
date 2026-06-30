namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Static helpers that export optimisation results to CSV / Excel.
    /// Split by dimensionality so each file owns one export surface.
    /// </summary>
    public static partial class ExportService
    {
        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
