using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// A single cutting pattern: one sheet plus the items placed on it (and the guillotine
    /// cut tree that produced them). 2D analogue of <see cref="CuttingStock.Core.Domain.CuttingPlan"/>.
    /// </summary>
    public sealed class CuttingPattern2D
    {
        /// <summary>The sheet this pattern is cut from.</summary>
        public Sheet Sheet { get; init; } = null!;

        /// <summary>How many copies of this pattern are produced (×k identical sheets).</summary>
        public int Multiplicity { get; init; } = 1;

        /// <summary>Flat list of placed items in absolute sheet coordinates.</summary>
        public List<Placement> Placements { get; init; } = new();

        /// <summary>
        /// Optional guillotine cut tree. May be null for solvers that only output placements;
        /// in that case <c>CuttingStock.Core.TwoD.Algorithms.Utilities.PatternBuilder</c> can
        /// construct it from <see cref="Placements"/> via recursive guillotine inference.
        /// </summary>
        public GuillotineNode? Root { get; init; }

        /// <summary>Total area of all placed items in mm².</summary>
        public long UsedArea => Placements.Sum(p => p.Area);

        /// <summary>Sheet area minus used area, in mm².</summary>
        public long WasteArea => Sheet.Area - UsedArea;

        /// <summary>Used area divided by sheet area, as a percentage.</summary>
        public double Efficiency => Sheet.Area == 0 ? 0 : 100.0 * UsedArea / Sheet.Area;
    }
}
