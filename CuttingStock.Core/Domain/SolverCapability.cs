using System;

namespace CuttingStock.Core.Domain
{
    /// <summary>Options and behavioral traits a solver actually honors.</summary>
    [Flags]
    public enum SolverCapability
    {
        None = 0,
        Kerf = 1 << 0,
        StockUsageOrder = 1 << 1,
        Welding = 1 << 2,
        Trim = 1 << 3,
        Rotation = 1 << 4,
        TimeLimit = 1 << 5,
        AdvisoryStage = 1 << 6,
        EnforcedStage = 1 << 7,
        Heuristic = 1 << 8,
        LinearRelaxation = 1 << 9,
        IntegerProgramming = 1 << 10,
    }
}
