using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>DP item construction and reduced-cost pricing for 2D pattern columns.</summary>
    internal static class PatternPricing
    {
        public static List<GuillotineKnapsackDp.Item> BuildDpItems(
            List<RectOrder> orders,
            double[] duals,
            SolverOptions2D options,
            double epsilon = 1e-6)
        {
            var items = new List<GuillotineKnapsackDp.Item>(orders.Count * 2);
            for (int orderIndex = 0; orderIndex < orders.Count; orderIndex++)
            {
                if (!double.IsFinite(duals[orderIndex]) || duals[orderIndex] <= epsilon)
                    continue;

                var order = orders[orderIndex];
                items.Add(new GuillotineKnapsackDp.Item
                {
                    OrderIndex = orderIndex,
                    W = order.Width,
                    H = order.Height,
                    Rotated = false,
                    Profit = duals[orderIndex],
                });

                if (options.AllowRotation &&
                    order.AllowRotation &&
                    order.Width != order.Height)
                {
                    items.Add(new GuillotineKnapsackDp.Item
                    {
                        OrderIndex = orderIndex,
                        W = order.Height,
                        H = order.Width,
                        Rotated = true,
                        Profit = duals[orderIndex],
                    });
                }
            }

            return items;
        }

        public static PatternColumn? PriceBestColumn(
            List<Sheet> sheets,
            List<RectOrder> orders,
            double[] duals,
            SolverOptions2D options,
            int orderCount,
            double epsilon = 1e-6,
            Func<bool>? cancel = null)
        {
            PatternColumn? best = null;
            double bestReducedCost = -epsilon;

            foreach (var column in PriceImprovingColumns(
                         sheets,
                         orders,
                         duals,
                         options,
                         orderCount,
                         epsilon,
                         cancel))
            {
                double reducedCost = column.Sheet.Area;
                for (int orderIndex = 0; orderIndex < column.Counts.Length; orderIndex++)
                    reducedCost -= duals[orderIndex] * column.Counts[orderIndex];

                if (reducedCost < bestReducedCost)
                {
                    bestReducedCost = reducedCost;
                    best = column;
                }
            }

            return best;
        }

        public static IEnumerable<PatternColumn> PriceImprovingColumns(
            List<Sheet> sheets,
            List<RectOrder> orders,
            double[] duals,
            SolverOptions2D options,
            int orderCount,
            double epsilon = 1e-6,
            Func<bool>? cancel = null)
        {
            foreach (var sheet in sheets)
            {
                if (cancel?.Invoke() == true) yield break;

                var items = BuildDpItems(orders, duals, options, epsilon);
                if (items.Count == 0) continue;

                int usableWidth = sheet.Width - 2 * options.Trim;
                int usableHeight = sheet.Height - 2 * options.Trim;
                if (usableWidth <= 0 || usableHeight <= 0) continue;

                var result = new GuillotineKnapsackDp(
                    usableWidth,
                    usableHeight,
                    items,
                    options.Kerf).Solve();

                double reducedCost = sheet.Area - result.Profit;
                if (reducedCost >= -epsilon) continue;

                var column = PatternMaterializer.FromDpResult(
                    sheet,
                    result,
                    orderCount,
                    options.Trim);
                if (column.Counts.Sum() != 0)
                    yield return column;
            }
        }
    }
}
