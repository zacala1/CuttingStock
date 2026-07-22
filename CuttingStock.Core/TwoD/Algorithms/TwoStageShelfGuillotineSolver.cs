using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms
{
    /// <summary>
    /// Explicit 2-stage guillotine solver. It reuses the shelf heuristic, then
    /// verifies that every produced pattern is representable as horizontal shelf
    /// strips followed by vertical item cuts inside each strip.
    /// </summary>
    public sealed class TwoStageShelfGuillotineSolver : ICuttingSolver2D
    {
        private readonly ShelfGuillotineSolver _inner = new();

        public string Name => "Two-Stage Shelf Guillotine";
        public string Description => "Shelf heuristic locked to two-stage guillotine patterns.";
        public string TimeComplexity => _inner.TimeComplexity;

        public SolverResult2D Solve(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            IProgress<double>? progress = null)
        {
            var forced = new SolverOptions2D
            {
                Kerf = options.Kerf,
                Trim = options.Trim,
                AlphaArea = options.AlphaArea,
                AllowRotation = options.AllowRotation,
                Stage = 2,
                TimeLimitMs = options.TimeLimitMs,
                UsageOrder = options.UsageOrder,
            };

            var result = _inner.Solve(sheets, orders, forced, progress);
            result.AlgorithmName = Name;
            if (!result.Success) return result;

            for (int i = 0; i < result.Patterns.Count; i++)
            {
                if (IsTwoStageShelfPattern(result.Patterns[i], options.Kerf)) continue;
                result.Success = false;
                result.ErrorMessage = $"Pattern {i + 1} is not a two-stage shelf guillotine pattern.";
                return result;
            }

            TwoDResultFinalizer.FinalizeAndValidate(sheets, orders, forced, result);

            return result;
        }

        private static bool IsTwoStageShelfPattern(CuttingPattern2D pattern, int kerf)
        {
            var placements = pattern.Placements;
            for (int i = 0; i < placements.Count; i++)
            {
                for (int j = i + 1; j < placements.Count; j++)
                {
                    var a = placements[i];
                    var b = placements[j];
                    if (a.Y == b.Y) continue;

                    int aBottom = a.Y + a.Height;
                    int bBottom = b.Y + b.Height;
                    bool separatedByShelfCut = aBottom + kerf <= b.Y || bBottom + kerf <= a.Y;
                    if (!separatedByShelfCut) return false;
                }
            }

            foreach (var shelf in placements.GroupBy(p => p.Y))
            {
                var ordered = shelf.OrderBy(p => p.X).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    int previousRight = ordered[i - 1].X + ordered[i - 1].Width;
                    if (previousRight + kerf > ordered[i].X) return false;
                }
            }

            return true;
        }
    }
}
