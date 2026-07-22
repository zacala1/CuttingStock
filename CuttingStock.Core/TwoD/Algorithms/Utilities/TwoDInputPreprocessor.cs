using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    public sealed record TwoDPreprocessedInput(
        bool ShouldReturn,
        List<Sheet> Sheets,
        List<RectOrder> Orders);

    /// <summary>Shared entry preprocessing for 2D solvers.</summary>
    public static class TwoDInputPreprocessor
    {
        public static TwoDPreprocessedInput Preprocess(
            List<Sheet>? sheets,
            List<RectOrder>? orders,
            SolverResult2D result)
        {
            if (ValidateInputs(sheets, orders, result))
                return new TwoDPreprocessedInput(true, new List<Sheet>(), new List<RectOrder>());

            return new TwoDPreprocessedInput(
                false,
                AggregateByDims(sheets!),
                orders!);
        }

        public static bool ValidateInputs(
            List<Sheet>? sheets,
            List<RectOrder>? orders,
            SolverResult2D result)
        {
            if (sheets == null || sheets.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "At least one sheet must be provided.";
                return true;
            }

            if (orders == null || orders.Count == 0)
            {
                // Empty demand is trivially solved with no patterns; success stays true.
                return true;
            }

            return false;
        }

        public static List<Sheet> AggregateByDims(List<Sheet> sheets)
        {
            return sheets
                .GroupBy(s => (s.Width, s.Height))
                .Select(g => new Sheet(g.Key.Width, g.Key.Height, g.Sum(s => s.Quantity)))
                .ToList();
        }

        public static List<Sheet> OrderSheets(List<Sheet> sheets, SolverOptions2D options)
        {
            return options.UsageOrder == StockUsageOrder.SmallToLarge
                ? sheets.OrderBy(s => s.Area).ToList()
                : sheets.OrderByDescending(s => s.Area).ToList();
        }

        public static List<(int OrderIndex, int W, int H, bool Rot)> ExpandOrders(
            List<RectOrder> orders,
            bool globalAllowRotation)
        {
            var list = new List<(int, int, int, bool)>();
            for (int i = 0; i < orders.Count; i++)
            {
                var o = orders[i];
                bool rot = globalAllowRotation && o.AllowRotation;
                for (int k = 0; k < o.Quantity; k++)
                    list.Add((i, o.Width, o.Height, rot));
            }
            return list;
        }
    }
}
