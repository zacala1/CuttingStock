using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// One public 2D cutting pattern for a sheet.
    /// <see cref="Placements"/> is the canonical solver output; consumers should
    /// treat <see cref="Root"/> as optional derived data.
    /// </summary>
    public sealed class CuttingPattern2D
    {
        public Sheet Sheet { get; init; } = null!;
        public int Multiplicity { get; init; } = 1;
        /// <summary>
        /// Canonical public placement list. UI, export, validation, and solver
        /// contracts consume this flat geometry even when <see cref="Root"/> is null.
        /// </summary>
        public List<Placement> Placements { get; init; } = new();

        /// <summary>
        /// Optional guillotine cut tree derived from <see cref="Placements"/>.
        /// Solvers may leave this null; callers that need a tree can reconstruct one
        /// with <c>PatternBuilder</c> after validating guillotine compliance.
        /// </summary>
        public GuillotineNode? Root { get; init; }

        public long UsedArea => Placements.Sum(p => p.Area);
        public long WasteArea => Sheet.Area - UsedArea;
        public double Efficiency => Sheet.Area == 0 ? 0 : 100.0 * UsedArea / Sheet.Area;
    }
}
