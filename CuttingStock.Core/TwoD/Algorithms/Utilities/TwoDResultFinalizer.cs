using System;
using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Shared post-solve finalization for 2D solver results.</summary>
    public static class TwoDResultFinalizer
    {
        public static void FinalizeResult(SolverResult2D result, SolverOptions2D options)
        {
            result.TotalCost = (long)Math.Round(result.TotalWasteArea * (double)options.AlphaArea);
        }

        public static void FinalizeAndValidate(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            SolverResult2D result)
        {
            if (result.Success &&
                TwoDResultValidator.ValidateSuccessfulResult(sheets, orders, options, result) is { } validationError)
            {
                result.Success = false;
                result.ErrorMessage = validationError;
            }

            FinalizeResult(result, options);
        }

        public static List<CuttingPattern2D> TrimToDemand(
            List<CuttingPattern2D> patterns,
            int[] demand,
            out int[] produced)
        {
            produced = new int[demand.Length];
            var trimmed = new List<CuttingPattern2D>();

            foreach (var pattern in patterns)
            {
                for (int copy = 0; copy < pattern.Multiplicity; copy++)
                {
                    var placements = new List<Placement>();
                    foreach (var placement in pattern.Placements)
                    {
                        if (placement.OrderIndex < 0 || placement.OrderIndex >= demand.Length)
                            throw new ArgumentOutOfRangeException(nameof(patterns), "Pattern placement references an unknown order index.");

                        if (produced[placement.OrderIndex] >= demand[placement.OrderIndex])
                            continue;

                        placements.Add(ClonePlacement(placement));
                        produced[placement.OrderIndex]++;
                    }

                    if (placements.Count == 0)
                        continue;

                    trimmed.Add(new CuttingPattern2D
                    {
                        Sheet = pattern.Sheet,
                        Multiplicity = 1,
                        Placements = placements,
                    });
                }
            }

            return trimmed;
        }

        private static Placement ClonePlacement(Placement p) => new()
        {
            OrderIndex = p.OrderIndex,
            X = p.X,
            Y = p.Y,
            Width = p.Width,
            Height = p.Height,
            Rotated = p.Rotated,
        };
    }
}
