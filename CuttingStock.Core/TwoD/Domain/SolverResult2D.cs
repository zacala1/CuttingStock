using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// Result of a 2D guillotine cutting solve. Mirrors the contract of
    /// <see cref="CuttingStock.Core.Domain.SolverResult"/> for UI/test parity.
    /// </summary>
    public sealed class SolverResult2D
    {
        /// <summary>Algorithm display name.</summary>
        public string AlgorithmName { get; set; } = "";

        /// <summary>Generated cutting patterns. Each entry corresponds to one sheet instance
        /// (multiplicity already expanded for visualization simplicity unless noted).</summary>
        public List<CuttingPattern2D> Patterns { get; set; } = new();

        /// <summary>Total waste area across all used sheets, in mm².</summary>
        public long TotalWasteArea => Patterns.Sum(p => p.WasteArea * p.Multiplicity);

        /// <summary>Total used (item) area across all sheets, in mm².</summary>
        public long TotalUsedArea => Patterns.Sum(p => p.UsedArea * p.Multiplicity);

        /// <summary>Total sheet area consumed, in mm².</summary>
        public long TotalSheetArea => Patterns.Sum(p => p.Sheet.Area * p.Multiplicity);

        /// <summary>Number of sheet instances used (sum of multiplicities).</summary>
        public int SheetsUsed => Patterns.Sum(p => p.Multiplicity);

        /// <summary>Solve wall-time in milliseconds.</summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>True if the solve produced a valid solution.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Used / sheet area as a percentage.</summary>
        public double MaterialEfficiency =>
            TotalSheetArea == 0 ? 0 : 100.0 * TotalUsedArea / TotalSheetArea;

        /// <summary>
        /// Total cost = waste area × <see cref="SolverOptions2D.AlphaArea"/>, rounded to long.
        /// </summary>
        public long TotalCost { get; set; }

        /// <summary>
        /// Generates a human-readable report mirroring 1D <c>GetDetailedReport</c>.
        /// </summary>
        public string GetDetailedReport(SolverOptions2D options)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(AlgorithmName))
                sb.AppendLine($"=== Algorithm: {AlgorithmName} ===").AppendLine();

            sb.AppendLine("=== Cutting Patterns ===");
            int idx = 1;
            foreach (var p in Patterns)
            {
                sb.AppendLine($"#{idx}: Sheet {p.Sheet.Width}×{p.Sheet.Height} ×{p.Multiplicity} | items={p.Placements.Count} | used={p.UsedArea}mm² | waste={p.WasteArea}mm² | eff={p.Efficiency:F1}%");
                foreach (var pl in p.Placements)
                {
                    sb.AppendLine($"    O{pl.OrderIndex} @({pl.X},{pl.Y}) {pl.Width}×{pl.Height}{(pl.Rotated ? " ↻" : "")}");
                }
                idx++;
            }

            sb.AppendLine()
              .AppendLine("=== Performance Metrics ===")
              .AppendLine($"Sheets Used: {SheetsUsed}")
              .AppendLine($"Total Used Area: {TotalUsedArea}mm²")
              .AppendLine($"Total Waste Area: {TotalWasteArea}mm²")
              .AppendLine($"Material Efficiency: {MaterialEfficiency:F2}%")
              .AppendLine()
              .AppendLine("=== Costs ===")
              .AppendLine($"Waste Cost: {TotalWasteArea}mm² × {options.AlphaArea}/mm² = {TotalWasteArea * options.AlphaArea:F0}")
              .AppendLine($"Total Cost: {TotalCost}")
              .Append($"Execution Time: {ExecutionTimeMs:F2}ms");

            return sb.ToString();
        }
    }
}
