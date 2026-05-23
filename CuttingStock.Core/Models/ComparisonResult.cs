namespace CuttingStock.Core.Models
{
    /// <summary>Per-algorithm summary row for UI comparison grid.</summary>
    public class ComparisonResult
    {
        public string AlgorithmName { get; set; } = string.Empty;
        public long TotalCost { get; set; }
        public int WasteLength { get; set; }
        public int StockUsed { get; set; }
        public double MaterialEfficiency { get; set; }
        public double ExecutionTimeMs { get; set; }
        public bool Success { get; set; }
        public int Rank { get; set; }
    }
}
