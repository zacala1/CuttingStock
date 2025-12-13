using System.Collections.Generic;
using CuttingStock.Core.Models;

namespace CuttingStock.Core.Domain
{
    /// <summary>
    /// Common Interface for Cutting Stock Solvers.
    /// </summary>
    public interface ICuttingSolver
    {
        /// <summary>
        /// Gets the name of the algorithm.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the algorithm.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the time complexity of the algorithm.
        /// </summary>
        string TimeComplexity { get; }

        /// <summary>
        /// Solves the Cutting Stock Problem.
        /// </summary>
        /// <param name="stock">Available stock inventory.</param>
        /// <param name="orders">Orders to fulfill.</param>
        /// <param name="options">Solver configuration options.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>Result of the optimization.</returns>
        SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null);
    }
}
