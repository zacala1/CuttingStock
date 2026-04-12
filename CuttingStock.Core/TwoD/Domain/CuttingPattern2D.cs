using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>One cutting pattern: a sheet plus the items placed on it.</summary>
    public sealed class CuttingPattern2D
    {
        public Sheet Sheet { get; init; } = null!;
        public int Multiplicity { get; init; } = 1;
        public List<Placement> Placements { get; init; } = new();

        /// <summary>
        /// Optional guillotine cut tree. Null when the solver only outputs flat placements;
        /// <c>PatternBuilder</c> can reconstruct it from <see cref="Placements"/>.
        /// </summary>
        public GuillotineNode? Root { get; init; }

        public long UsedArea => Placements.Sum(p => p.Area);
        public long WasteArea => Sheet.Area - UsedArea;
        public double Efficiency => Sheet.Area == 0 ? 0 : 100.0 * UsedArea / Sheet.Area;
    }
}
