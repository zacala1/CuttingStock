namespace CuttingStock.Core.Models
{
    /// <summary>
    /// Model class for algorithm comparison results
    ///
    /// Bound to UI DataGrid to display comparison results.
    /// </summary>
    public class ComparisonResult
    {
        /// <summary>
        /// Algorithm name
        /// Example: "Greedy Knapsack DP", "First Fit Decreasing (FFD)", "Best Fit Decreasing (BFD)"
        /// </summary>
        public string AlgorithmName { get; set; } = string.Empty;

        /// <summary>
        /// Total cost (currency)
        /// Waste cost + welding cost
        /// </summary>
        public int TotalCost { get; set; }

        /// <summary>
        /// Total waste leftover length (mm)
        /// Sum of leftovers below Gamma threshold
        /// </summary>
        public int WasteLength { get; set; }

        /// <summary>
        /// Number of stocks used
        /// Lower is better
        /// </summary>
        public int StockUsed { get; set; }

        /// <summary>
        /// Material efficiency (%)
        /// = (used length / total stock length) × 100
        /// Higher is better (max 100%)
        /// </summary>
        public double MaterialEfficiency { get; set; }

        /// <summary>
        /// Execution time (milliseconds)
        /// Algorithm performance metric
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// Optimization success status
        /// False indicates errors such as insufficient stock
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Rank (1st, 2nd, 3rd)
        /// Sorted by total cost
        /// </summary>
        public int Rank { get; set; }
    }
}
