using System.Collections.Generic;
using CuttingStock.Core.Models;

namespace CuttingStock.Core.Domain
{
    /// <summary>Common interface for 1D cutting stock solvers.</summary>
    public interface ICuttingSolver
    {
        string Name { get; }
        string Description { get; }
        string TimeComplexity { get; }

        SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null);
    }
}
