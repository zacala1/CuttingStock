using CuttingStock.Core.Domain;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column generation variant that prices with a smoothed dual vector while
    /// accepting columns only when they improve under the current RMP dual.
    /// </summary>
    public sealed class StabilizedColumnGenerationSolver : ColumnGenerationSolver
    {
        public StabilizedColumnGenerationSolver()
            : base(ColumnGenerationProfile.Stabilized)
        {
        }
    }
}
