using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CuttingStock.Core.Models;

namespace CuttingStock.Core.Domain
{
    /// <summary>
    /// Solver Configuration Options
    /// </summary>
    public class SolverOptions
    {
        private float _alpha = 1.0f;
        private float _beta = 500.0f;
        private int _gamma = 100;
        private int _delta = 100;

        /// <summary>
        /// Cost per 1mm of waste/leftover. Must be non-negative.
        /// </summary>
        public float Alpha
        {
            get => _alpha;
            set => _alpha = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Alpha), "Alpha must be non-negative.");
        }

        /// <summary>
        /// Cost per weld operation. Must be non-negative.
        /// </summary>
        public float Beta
        {
            get => _beta;
            set => _beta = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Beta), "Beta must be non-negative.");
        }

        /// <summary>
        /// Minimum reusable leftover length (mm). Must be non-negative.
        /// </summary>
        public int Gamma
        {
            get => _gamma;
            set => _gamma = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Gamma), "Gamma must be non-negative.");
        }

        /// <summary>
        /// Minimum weldable piece length (mm). Must be greater than 0.
        /// </summary>
        public int Delta
        {
            get => _delta;
            set => _delta = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(Delta), "Delta must be greater than 0.");
        }

        /// <summary>
        /// Order in which stock lengths are used.
        /// </summary>
        public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.SmallToLarge;

        /// <summary>
        /// Enable welding logic.
        /// </summary>
        public bool EnableWelding { get; set; } = false;

        /// <summary>
        /// Enable pattern reduction to minimize setup changes.
        /// When enabled, the solver tries to use fewer unique cutting patterns.
        /// Reference: https://journals.sagepub.com/doi/10.1243/09544054JEM966
        /// </summary>
        public bool EnablePatternReduction { get; set; } = false;

        /// <summary>
        /// Maximum number of unique patterns to use (0 = unlimited).
        /// Only effective when EnablePatternReduction is true.
        /// </summary>
        public int MaxPatternCount { get; set; } = 0;

    }

    /// <summary>
    /// Order in which stock lengths are used.
    /// </summary>
    public enum StockUsageOrder
    {
        /// <summary>
        /// Use smaller stock lengths first.
        /// </summary>
        SmallToLarge,

        /// <summary>
        /// Use larger stock lengths first.
        /// </summary>
        LargeToSmall
    }

    /// <summary>
    /// Result of the cutting optimization process.
    /// </summary>
    public class SolverResult
    {
        /// <summary>
        /// Name of the algorithm used.
        /// </summary>
        public string AlgorithmName { get; set; } = "";

        /// <summary>
        /// List of generated cutting plans.
        /// </summary>
        public List<CuttingPlan> CuttingPlans { get; set; } = new();

        /// <summary>
        /// List of reusable leftovers (mm).
        /// </summary>
        public List<int> ReusableLeftovers { get; set; } = new();

        /// <summary>
        /// Total length of waste (non-reusable leftovers) (mm).
        /// </summary>
        public int WasteLength { get; set; }

        /// <summary>
        /// Number of welds performed.
        /// </summary>
        public int WeldCount { get; set; }

        /// <summary>
        /// Total cost (currency).
        /// </summary>
        public int TotalCost { get; set; }

        /// <summary>
        /// Execution time in milliseconds.
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// Indicates if the optimization was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if optimization failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of stock items used.
        /// </summary>
        public int StockUsed => CuttingPlans.Count;

        /// <summary>
        /// Material Efficiency (%).
        /// </summary>
        public double MaterialEfficiency
        {
            get
            {
                long totalStockLength = CuttingPlans.Sum(p => (long)p.StockLength);
                if (totalStockLength == 0) return 0;
                long totalUsedLength = CuttingPlans.Sum(p => p.Cuts.Sum(c => (long)c.Length));
                return 100.0 * totalUsedLength / totalStockLength;
            }
        }

        /// <summary>
        /// Generates a detailed text report.
        /// </summary>
        public string GetDetailedReport(SolverOptions options)
        {
            var sb = new StringBuilder();

            // Algorithm Info
            if (!string.IsNullOrEmpty(AlgorithmName))
            {
                sb.AppendLine($"=== Algorithm: {AlgorithmName} ===").AppendLine();
            }

            sb.AppendLine("=== Cutting Results ===");
            var planNumber = 1;
            foreach (var plan in CuttingPlans)
            {
                var cutsDisplay = plan.Cuts.Select(c =>
                    c.WeldGroupId.HasValue
                        ? $"{c.Length}mm★G{c.WeldGroupId.Value}"
                        : $"{c.Length}mm");
                var cuts = string.Join(", ", cutsDisplay);
                sb.AppendLine($"#{planNumber}: Stock {plan.StockLength}mm -> [{cuts}] (Rem: {plan.Leftover}mm)");
                planNumber++;
            }

            // Weld Groups
            var weldGroups = CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value)
                .OrderBy(g => g.Key)
                .ToList();

            if (weldGroups.Any())
            {
                sb.AppendLine().AppendLine("=== Weld Groups ===");
                foreach (var group in weldGroups)
                {
                    var pieces = string.Join(" + ", group.Select(c => $"{c.Length}mm"));
                    var total = group.Sum(c => c.Length);
                    var weldCount = group.Count() - 1;
                    sb.AppendLine($"Group G{group.Key}: [{pieces}] = {total}mm ({weldCount} welds)");
                }
            }

            sb.AppendLine()
              .AppendLine("=== Performance Metrics ===")
              .AppendLine($"Stock Used: {StockUsed}")
              .AppendLine($"Reusable Leftovers: [{string.Join(", ", ReusableLeftovers)}] (Total {ReusableLeftovers.Sum()}mm)")
              .AppendLine($"Waste: {WasteLength}mm")
              .AppendLine($"Welds: {WeldCount}")
              .AppendLine($"Efficiency: {MaterialEfficiency:F1}%");

            var wasteCost = WasteLength * options.Alpha;
            var weldCost = WeldCount * options.Beta;
            sb.AppendLine()
              .AppendLine("=== Costs ===")
              .AppendLine($"Waste Cost: {WasteLength}mm x {options.Alpha}/mm = {wasteCost}")
              .AppendLine($"Weld Cost: {WeldCount} x {options.Beta}/weld = {weldCost}")
              .AppendLine($"Total Cost: {TotalCost}")
              .Append($"Execution Time: {ExecutionTimeMs:F2}ms");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a single cutting plan for one stock item.
    /// </summary>
    public class CuttingPlan
    {
        /// <summary>
        /// Length of the stock item (mm).
        /// </summary>
        public int StockLength { get; set; }

        /// <summary>
        /// List of cuts made from this stock.
        /// </summary>
        public List<Cut> Cuts { get; set; } = new();

        /// <summary>
        /// Remaining leftover length (mm).
        /// </summary>
        public int Leftover { get; set; }
    }

    /// <summary>
    /// Represents a single cut piece.
    /// </summary>
    public class Cut
    {
        /// <summary>
        /// Length of the cut piece (mm).
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Index of the order this cut belongs to.
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// Indicates if this piece requires welding.
        /// </summary>
        public bool RequiresWelding { get; set; }

        /// <summary>
        /// ID of the weld group this cut belongs to (if welded).
        /// </summary>
        public int? WeldGroupId { get; set; }
    }
}
