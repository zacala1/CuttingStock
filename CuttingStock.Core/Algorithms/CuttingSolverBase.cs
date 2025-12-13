using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using System.Collections.Generic;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Abstract Base Class for Cutting Stock Solvers.
    /// Implements common logic like validation, timing, and error handling.
    /// </summary>
    public abstract class CuttingSolverBase : ICuttingSolver
    {
        /// <summary>
        /// Name of the solver algorithm.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of the solver algorithm.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Time complexity of the algorithm.
        /// </summary>
        public abstract string TimeComplexity { get; }

        /// <inheritdoc/>
        public SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null)
        {
            var result = new SolverResult { AlgorithmName = Name };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Note: Assuming SolverUtils is available in CuttingStock.Core.Algorithms.Utilities
                // We will add 'using CuttingStock.Core.Algorithms.Utilities;' if needed, 
                // but since we are in the same assembly, we need to ensure namespace visibility.

                // Validate Inputs
                // Since SolverUtils is not yet created in this atomic step (it's next), 
                // we assume it will be there. Or we can inline validation here if we want to be safe, 
                // but delegation is better.
                // For now, I will comment this out until SolverUtils is ready, or just assume it compiles later.
                // Actually, I will write SolverUtils in the same turn so it's fine.

                // Let's assume Utilities namespace is imported.
                // For now, simple validation to avoid dependency error in this specific file content string:
                if (stock == null || orders == null || options == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Inputs cannot be null.";
                    return result;
                }

                SolveInternal(result, stock, orders, options, progress);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error during optimization: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Internal implementation of the solver algorithm.
        /// Classes inheriting from this must implement the core logic here.
        /// </summary>
        protected abstract void SolveInternal(SolverResult result, List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress);
    }
}
