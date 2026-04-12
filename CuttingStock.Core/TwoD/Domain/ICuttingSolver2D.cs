using System;
using System.Collections.Generic;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// Common interface for 2D guillotine cutting-stock solvers.
    /// Mirrors <see cref="CuttingStock.Core.Domain.ICuttingSolver"/> for API parity.
    /// </summary>
    public interface ICuttingSolver2D
    {
        /// <summary>Algorithm display name.</summary>
        string Name { get; }

        /// <summary>Short human-readable description.</summary>
        string Description { get; }

        /// <summary>Worst-case time complexity (informational).</summary>
        string TimeComplexity { get; }

        /// <summary>
        /// Solve a 2D guillotine cutting-stock instance.
        /// </summary>
        /// <param name="sheets">Available sheet inventory (one entry per distinct size).</param>
        /// <param name="orders">Rectangular items to be produced.</param>
        /// <param name="options">Solver configuration.</param>
        /// <param name="progress">Optional progress reporter (0..1).</param>
        SolverResult2D Solve(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            IProgress<double>? progress = null);
    }
}
