using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>Result of a 2D guillotine cutting solve.</summary>
    public sealed class SolverResult2D
    {
        public string AlgorithmName { get; set; } = "";
        public List<CuttingPattern2D> Patterns { get; set; } = new();

        public long TotalWasteArea => Patterns.Sum(p => p.WasteArea * p.Multiplicity);
        public long TotalUsedArea => Patterns.Sum(p => p.UsedArea * p.Multiplicity);
        public long TotalSheetArea => Patterns.Sum(p => p.Sheet.Area * p.Multiplicity);
        public int SheetsUsed => Patterns.Sum(p => p.Multiplicity);

        public double ExecutionTimeMs { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }

        public double MaterialEfficiency =>
            TotalSheetArea == 0 ? 0 : 100.0 * TotalUsedArea / TotalSheetArea;

        /// <summary>Waste area * AlphaArea, rounded.</summary>
        public long TotalCost { get; set; }

        public string GetDetailedReport(SolverOptions2D options)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(AlgorithmName))
                sb.AppendLine($"=== Algorithm: {AlgorithmName} ===").AppendLine();

            sb.AppendLine("=== Cutting Patterns ===");
            int idx = 1;
            foreach (var p in Patterns)
            {
                sb.AppendLine($"#{idx}: Sheet {p.Sheet.Width}x{p.Sheet.Height} x{p.Multiplicity} | items={p.Placements.Count} | used={p.UsedArea}mm2 | waste={p.WasteArea}mm2 | eff={p.Efficiency:F1}%");
                foreach (var pl in p.Placements)
                    sb.AppendLine($"    O{pl.OrderIndex} @({pl.X},{pl.Y}) {pl.Width}x{pl.Height}{(pl.Rotated ? " R" : "")}");
                idx++;
            }

            sb.AppendLine()
              .AppendLine("=== Metrics ===")
              .AppendLine($"Sheets Used: {SheetsUsed}")
              .AppendLine($"Used Area: {TotalUsedArea}mm2")
              .AppendLine($"Waste Area: {TotalWasteArea}mm2")
              .AppendLine($"Efficiency: {MaterialEfficiency:F2}%")
              .AppendLine($"Cost: {TotalCost} (waste {TotalWasteArea} x {options.AlphaArea})")
              .Append($"Time: {ExecutionTimeMs:F2}ms");

            return sb.ToString();
        }
    }
}
