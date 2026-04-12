using System;
using System.Collections.Generic;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>Common interface for 2D guillotine cutting-stock solvers.</summary>
    public interface ICuttingSolver2D
    {
        string Name { get; }
        string Description { get; }
        string TimeComplexity { get; }

        SolverResult2D Solve(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            IProgress<double>? progress = null);
    }
}
