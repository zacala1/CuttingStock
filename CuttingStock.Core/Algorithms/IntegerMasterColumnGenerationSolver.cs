namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column generation variant that solves a small integer master over the
    /// generated columns before falling back to LP floor/residual rounding.
    /// </summary>
    public sealed class IntegerMasterColumnGenerationSolver : ColumnGenerationSolver
    {
        public IntegerMasterColumnGenerationSolver()
            : base(ColumnGenerationProfile.IntegerMaster)
        {
        }
    }
}
