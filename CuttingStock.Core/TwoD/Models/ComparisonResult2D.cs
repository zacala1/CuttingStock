namespace CuttingStock.Core.TwoD.Models
{
    /// <summary>Per-algorithm summary row for side-by-side comparison in the UI.</summary>
    public sealed class ComparisonResult2D
    {
        public string AlgorithmName { get; init; } = "";
        public long TotalCost { get; init; }
        public long WasteArea { get; init; }
        public int SheetsUsed { get; init; }
        public double MaterialEfficiency { get; init; }
        public double ExecutionTimeMs { get; init; }
        public bool Success { get; init; }
        public int Rank { get; set; }
    }
}
