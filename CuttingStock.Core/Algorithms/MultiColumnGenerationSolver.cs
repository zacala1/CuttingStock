namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column generation variant that adds several improving pricing columns per
    /// RMP iteration by re-solving pricing with dominant items temporarily excluded.
    /// </summary>
    public sealed class MultiColumnGenerationSolver : ColumnGenerationSolver
    {
        public MultiColumnGenerationSolver()
            : base(ColumnGenerationProfile.MultiColumn)
        {
        }
    }
}
