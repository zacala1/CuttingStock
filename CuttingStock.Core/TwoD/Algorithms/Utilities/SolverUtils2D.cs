using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Compatibility facade for shared 2D solver helpers.</summary>
    public static class SolverUtils2D
    {
        public static List<(int OrderIndex, int W, int H, bool Rot)> ExpandOrders(
            List<RectOrder> orders,
            bool globalAllowRotation)
        {
            return TwoDInputPreprocessor.ExpandOrders(orders, globalAllowRotation);
        }

        public static bool ValidateInputs(
            List<Sheet>? sheets,
            List<RectOrder>? orders,
            SolverResult2D result)
        {
            return TwoDInputPreprocessor.ValidateInputs(sheets, orders, result);
        }

        public static List<Sheet> OrderSheets(List<Sheet> sheets, SolverOptions2D options)
        {
            return TwoDInputPreprocessor.OrderSheets(sheets, options);
        }

        public static List<Sheet> AggregateByDims(List<Sheet> sheets)
        {
            return TwoDInputPreprocessor.AggregateByDims(sheets);
        }

        public static void Finalize(SolverResult2D result, SolverOptions2D options)
        {
            TwoDResultFinalizer.FinalizeResult(result, options);
        }

        public static string? ValidateSuccessfulResult(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            SolverResult2D result)
        {
            return TwoDResultValidator.ValidateSuccessfulResult(sheets, orders, options, result);
        }

        public static bool HasOverlap(List<Placement> placements, int kerf)
        {
            return TwoDPlacementMath.HasOverlap(placements, kerf);
        }

        public static bool WithinSheet(List<Placement> placements, Sheet sheet, int trim)
        {
            return TwoDPlacementMath.WithinSheet(placements, sheet, trim);
        }

        public static int[] CountPlaced(List<CuttingPattern2D> patterns, int orderCount)
        {
            return TwoDPlacementMath.CountPlaced(patterns, orderCount);
        }

        public static List<CuttingPattern2D> TrimToDemand(
            List<CuttingPattern2D> patterns,
            int[] demand,
            out int[] produced)
        {
            return TwoDResultFinalizer.TrimToDemand(patterns, demand, out produced);
        }
    }
}
