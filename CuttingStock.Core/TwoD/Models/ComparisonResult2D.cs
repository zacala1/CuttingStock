namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>
    /// Per-algorithm summary used by the UI to compare 2D solvers side-by-side.
    /// 2D analogue of <c>CuttingStock.Core.Models.ComparisonResult</c>.
    /// </summary>
    public sealed class ComparisonResult2D
    {
        /// <summary>Algorithm display name.</summary>
        public string AlgorithmName { get; init; } = "";

        /// <summary>Total cost.</summary>
        public long TotalCost { get; init; }

        /// <summary>Total waste area in mm².</summary>
        public long WasteArea { get; init; }

        /// <summary>Number of sheets used.</summary>
        public int SheetsUsed { get; init; }

        /// <summary>Material efficiency as a percentage.</summary>
        public double MaterialEfficiency { get; init; }

        /// <summary>Wall-clock execution time in milliseconds.</summary>
        public double ExecutionTimeMs { get; init; }

        /// <summary>Whether the solve succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Rank when several algorithms are compared (1 = best).</summary>
        public int Rank { get; set; }
    }
}
