using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CuttingStock.Core.Models;

namespace CuttingStock.Core.Domain
{
    /// <summary>Configuration for 1D cutting solvers.</summary>
    public class SolverOptions
    {
        private float _alpha = 1.0f;
        private float _beta = 500.0f;
        private int _gamma = 100;
        private int _delta = 100;
        private int _kerf = 0;

        /// <summary>Cost per mm of waste.</summary>
        public float Alpha
        {
            get => _alpha;
            set => _alpha = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Alpha));
        }

        /// <summary>Cost per weld operation.</summary>
        public float Beta
        {
            get => _beta;
            set => _beta = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Beta));
        }

        /// <summary>Minimum reusable leftover length (mm).</summary>
        public int Gamma
        {
            get => _gamma;
            set => _gamma = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Gamma));
        }

        /// <summary>Minimum weldable piece length (mm).</summary>
        public int Delta
        {
            get => _delta;
            set => _delta = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(Delta));
        }

        /// <summary>Blade kerf (mm). Consumed between adjacent cuts: total = sum(cuts) + (n-1)*kerf.</summary>
        public int Kerf
        {
            get => _kerf;
            set => _kerf = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Kerf));
        }

        public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.SmallToLarge;
        public bool EnableWelding { get; set; } = false;
    }

    public enum StockUsageOrder
    {
        SmallToLarge,
        LargeToSmall
    }

    /// <summary>Result of a 1D cutting optimization.</summary>
    public class SolverResult
    {
        public string AlgorithmName { get; set; } = "";
        public List<CuttingPlan> CuttingPlans { get; set; } = new();
        public List<int> ReusableLeftovers { get; set; } = new();
        public long WasteLength { get; set; }
        public int WeldCount { get; set; }
        public long TotalCost { get; set; }
        public double ExecutionTimeMs { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public int StockUsed => CuttingPlans.Count;

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

        public string GetDetailedReport(SolverOptions options)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(AlgorithmName))
                sb.AppendLine($"=== Algorithm: {AlgorithmName} ===").AppendLine();

            sb.AppendLine("=== Cutting Results ===");
            var planNumber = 1;
            foreach (var plan in CuttingPlans)
            {
                var cutsDisplay = plan.Cuts.Select(c =>
                    c.WeldGroupId.HasValue
                        ? $"{c.Length}mm*G{c.WeldGroupId.Value}"
                        : $"{c.Length}mm");
                sb.AppendLine($"#{planNumber}: Stock {plan.StockLength}mm -> [{string.Join(", ", cutsDisplay)}] (Rem: {plan.Leftover}mm)");
                planNumber++;
            }

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
              .AppendLine("=== Metrics ===")
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
              .Append($"Time: {ExecutionTimeMs:F2}ms");

            return sb.ToString();
        }
    }

    /// <summary>One stock bar and how it's cut.</summary>
    public sealed class CuttingPlan
    {
        public int StockLength { get; init; }
        public List<Cut> Cuts { get; init; } = new();
        // Mutable: post-processing (relocate/swap) recomputes this after Cuts changes.
        public int Leftover { get; set; }
    }

    /// <summary>A single cut piece from a stock bar.</summary>
    public sealed class Cut
    {
        public int Length { get; init; }
        public int OrderIndex { get; init; }
        public bool RequiresWelding { get; init; }
        public int? WeldGroupId { get; init; }
    }
}
