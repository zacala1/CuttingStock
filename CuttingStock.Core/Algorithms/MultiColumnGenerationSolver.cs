namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column generation variant that adds several improving pricing columns per
    /// RMP iteration by re-solving pricing with dominant items temporarily excluded.
    /// </summary>
    public sealed class MultiColumnGenerationSolver : ColumnGenerationSolver
    {
        public MultiColumnGenerationSolver()
            : base(
                name: "Column Generation (Multi-column LP)",
                description: "CG that adds multiple improving knapsack pricing columns per iteration.",
                useDualStabilization: false,
                dualSmoothingFactor: 1.0,
                maxColumnsPerIteration: 4,
                useIntegerMaster: false,
                integerMasterTimeLimitMs: 0)
        {
        }
    }
}
