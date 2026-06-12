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
            : base(
                name: "Column Generation (Stabilized LP)",
                description: "CG with dual-smoothed knapsack pricing and raw-dual fallback.",
                useDualStabilization: true,
                dualSmoothingFactor: 0.70,
                maxColumnsPerIteration: 1,
                useIntegerMaster: false,
                integerMasterTimeLimitMs: 0)
        {
        }
    }
}
