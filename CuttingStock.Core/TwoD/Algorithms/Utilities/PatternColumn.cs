using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>A generated 2D pattern as seen by the master problem.</summary>
    internal sealed class PatternColumn
    {
        public Sheet Sheet { get; init; } = null!;
        public int[] Counts { get; init; } = null!;
        public List<Placement> Placements { get; init; } = new();
    }
}
