namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column generation variant that solves a small integer master over the
    /// generated columns before falling back to LP floor/residual rounding.
    /// </summary>
    public sealed class IntegerMasterColumnGenerationSolver : ColumnGenerationSolver
    {
        public IntegerMasterColumnGenerationSolver()
            : base(
                name: "Column Generation (Integer Master)",
                description: "CG with a generated-column CBC integer master polish.",
                useDualStabilization: false,
                dualSmoothingFactor: 1.0,
                maxColumnsPerIteration: 1,
                useIntegerMaster: true,
                integerMasterTimeLimitMs: 5000)
        {
        }
    }
}
