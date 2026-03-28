using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CuttingStock.Core.Algorithms.Utilities
{
    /// <summary>
    /// Utility methods commonly used across solver algorithms.
    /// </summary>
    public static class SolverUtils
    {
        #region Constants

        /// <summary>
        /// Maximum usage per order in Pass 1.
        /// </summary>
        public const int PASS1_MAX_PER_ORDER = 2;

        /// <summary>
        /// Maximum usage per order in Pass 2.
        /// </summary>
        public const int PASS2_MAX_PER_ORDER = 5;

        /// <summary>
        /// Top ratio for post-processing (1/3).
        /// </summary>
        public const int POSTPROCESS_TOP_RATIO = 3;

        #endregion

        #region Exception Handling

        /// <summary>
        /// Handles exceptions and sets error information in the result object.
        /// </summary>
        public static void HandleException(SolverResult result, Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error during optimization: {ex.Message}";
        }

        /// <summary>
        /// Sets error message when there are remaining orders.
        /// </summary>
        public static void SetRemainingOrdersError(SolverResult result, int remainingCount)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to process {remainingCount} order(s).";
        }

        /// <summary>
        /// Sets insufficient stock error message.
        /// </summary>
        public static void SetInsufficientStockError(SolverResult result, int remainingCount)
        {
            result.Success = false;
            result.ErrorMessage = $"Insufficient stock. Failed to process {remainingCount} order(s).";
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates input data.
        /// </summary>
        public static (bool isValid, string? errorMessage) ValidateInputs(
            List<RebarStock>? stock,
            List<Order>? orders)
        {
            if (stock == null || !stock.Any())
            {
                return (false, "No stock available.");
            }

            if (orders == null || !orders.Any())
            {
                return (false, "No orders to process.");
            }

            var invalidStock = stock.FirstOrDefault(s => s.Length <= 0);
            if (invalidStock != null)
            {
                return (false, $"Stock length must be greater than 0. (Current: {invalidStock.Length}mm)");
            }

            var zeroQuantityStock = stock.FirstOrDefault(s => s.Quantity <= 0);
            if (zeroQuantityStock != null)
            {
                return (false, $"Stock quantity must be greater than 0. (Length: {zeroQuantityStock.Length}mm, Quantity: {zeroQuantityStock.Quantity})");
            }

            var invalidOrder = orders.FirstOrDefault(o => o.Length <= 0);
            if (invalidOrder != null)
            {
                return (false, $"Order length must be greater than 0. (Current: {invalidOrder.Length}mm)");
            }

            var zeroQuantityOrder = orders.FirstOrDefault(o => o.Quantity <= 0);
            if (zeroQuantityOrder != null)
            {
                return (false, $"Order quantity must be greater than 0. (Length: {zeroQuantityOrder.Length}mm, Quantity: {zeroQuantityOrder.Quantity})");
            }

            return (true, null);
        }

        #endregion

        #region Sorting

        /// <summary>
        /// Sorts stock according to usage order.
        /// </summary>
        public static List<RebarStock> SortStock(
            List<RebarStock> stock,
            StockUsageOrder usageOrder)
        {
            return usageOrder == StockUsageOrder.SmallToLarge
                ? stock.OrderBy(s => s.Length).ToList()
                : stock.OrderByDescending(s => s.Length).ToList();
        }

        /// <summary>
        /// Flattens and sorts orders in descending order by length.
        /// </summary>
        public static List<int> FlattenOrdersDescending(List<Order> orders)
        {
            return orders
                .OrderByDescending(o => o.Length)
                .SelectMany(o => Enumerable.Repeat(o.Length, o.Quantity))
                .ToList();
        }

        /// <summary>
        /// Sorts orders by scarcity (low quantity first).
        /// </summary>
        public static List<Order> SortOrdersByScarcity(List<Order> orders)
        {
            return orders
                .OrderBy(o => o.Quantity)
                .ThenByDescending(o => o.Length)
                .ToList();
        }

        #endregion

        #region Result Calculation

        /// <summary>
        /// Calculates optimization result metrics.
        /// </summary>
        public static void CalculateResults(
            SolverResult result,
            SolverOptions options)
        {
            result.ReusableLeftovers = result.CuttingPlans
                .Where(p => p.Leftover >= options.Gamma)
                .Select(p => p.Leftover)
                .ToList();

            result.WasteLength = result.CuttingPlans
                .Where(p => p.Leftover < options.Gamma)
                .Sum(p => p.Leftover);

            var weldGroups = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value);

            result.WeldCount = weldGroups.Sum(g => g.Count() - 1);

            result.TotalCost = (int)(result.WasteLength * options.Alpha +
                                    result.WeldCount * options.Beta);
        }

        #endregion

        #region Post-Processing

        /// <summary>
        /// Performs post-processing optimization.
        /// Redistributes orders from high-leftover stock to improve efficiency.
        /// </summary>
        public static void OptimizePostProcess(
            SolverResult result,
            SolverOptions options)
        {
            if (result.CuttingPlans.Count < 2)
                return;

            // Phase 1: Original redistribution
            RedistributeCuts(result, options);

            // Phase 2: Local Search (2-opt style swap)
            LocalSearchOptimize(result, options, maxIterations: 100);
        }

        /// <summary>
        /// Original redistribution logic - moves cuts from high-leftover plans to low-leftover plans.
        /// </summary>
        private static void RedistributeCuts(SolverResult result, SolverOptions options)
        {
            var sortedPlans = result.CuttingPlans
                .Select((plan, index) => (plan, index))
                .OrderByDescending(x => x.plan.Leftover)
                .ToList();

            var topCount = Math.Max(1, sortedPlans.Count / 3);

            for (int i = 0; i < topCount; i++)
            {
                var (largePlan, largeIndex) = sortedPlans[i];

                if (largePlan.Leftover < options.Gamma)
                    continue;

                var smallestCut = largePlan.Cuts.OrderBy(c => c.Length).FirstOrDefault();
                if (smallestCut == null)
                    continue;

                for (int j = sortedPlans.Count - 1; j > i; j--)
                {
                    var (smallPlan, smallIndex) = sortedPlans[j];

                    if (smallPlan.Leftover >= smallestCut.Length)
                    {
                        var newLargeLeftover = largePlan.Leftover + smallestCut.Length;
                        var newSmallLeftover = smallPlan.Leftover - smallestCut.Length;

                        var currentWaste = (largePlan.Leftover < options.Gamma ? largePlan.Leftover : 0) +
                                          (smallPlan.Leftover < options.Gamma ? smallPlan.Leftover : 0);
                        var newWaste = (newLargeLeftover < options.Gamma ? newLargeLeftover : 0) +
                                      (newSmallLeftover < options.Gamma ? newSmallLeftover : 0);

                        if (newWaste < currentWaste)
                        {
                            largePlan.Cuts.Remove(smallestCut);
                            largePlan.Leftover = newLargeLeftover;

                            var newCut = new Cut
                            {
                                Length = smallestCut.Length,
                                OrderIndex = smallestCut.OrderIndex,
                                RequiresWelding = smallestCut.RequiresWelding,
                                WeldGroupId = smallestCut.WeldGroupId
                            };
                            smallPlan.Cuts.Add(newCut);
                            smallPlan.Leftover = newSmallLeftover;

                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Local Search optimization using 2-opt style swapping.
        /// Tries to swap cuts between pairs of plans to reduce total waste.
        /// Reference: https://link.springer.com/article/10.1186/2251-712X-8-24
        /// </summary>
        private static void LocalSearchOptimize(SolverResult result, SolverOptions options, int maxIterations)
        {
            bool improved = true;
            int iteration = 0;

            while (improved && iteration < maxIterations)
            {
                improved = false;
                iteration++;

                for (int i = 0; i < result.CuttingPlans.Count - 1; i++)
                {
                    for (int j = i + 1; j < result.CuttingPlans.Count; j++)
                    {
                        var planA = result.CuttingPlans[i];
                        var planB = result.CuttingPlans[j];

                        // Skip if either plan has welded cuts (to preserve weld groups)
                        if (planA.Cuts.Any(c => c.WeldGroupId.HasValue) ||
                            planB.Cuts.Any(c => c.WeldGroupId.HasValue))
                            continue;

                        // Try swapping each cut from A with each cut from B
                        if (TrySwapCuts(planA, planB, options))
                        {
                            improved = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tries to find a beneficial swap between two plans.
        /// Returns true if a swap was made.
        /// </summary>
        private static bool TrySwapCuts(CuttingPlan planA, CuttingPlan planB, SolverOptions options)
        {
            int currentWaste = CalculateWaste(planA, options) + CalculateWaste(planB, options);

            // Pre-compute total used lengths once (O(n) instead of O(n) per iteration)
            int totalUsedA = planA.Cuts.Sum(c => c.Length);
            int totalUsedB = planB.Cuts.Sum(c => c.Length);

            foreach (var cutA in planA.Cuts.ToList())
            {
                foreach (var cutB in planB.Cuts.ToList())
                {
                    // Check if swap is feasible (doesn't exceed stock length)
                    int newAUsed = totalUsedA - cutA.Length + cutB.Length;
                    int newBUsed = totalUsedB - cutB.Length + cutA.Length;

                    if (newAUsed > planA.StockLength || newBUsed > planB.StockLength)
                        continue;

                    // Calculate new leftovers
                    int newALeftover = planA.StockLength - newAUsed;
                    int newBLeftover = planB.StockLength - newBUsed;

                    // Calculate new waste
                    int newWasteA = newALeftover < options.Gamma ? newALeftover : 0;
                    int newWasteB = newBLeftover < options.Gamma ? newBLeftover : 0;
                    int newWaste = newWasteA + newWasteB;

                    // Accept if waste is reduced
                    if (newWaste < currentWaste)
                    {
                        // Perform swap
                        planA.Cuts.Remove(cutA);
                        planB.Cuts.Remove(cutB);

                        planA.Cuts.Add(new Cut
                        {
                            Length = cutB.Length,
                            OrderIndex = cutB.OrderIndex,
                            RequiresWelding = cutB.RequiresWelding,
                            WeldGroupId = cutB.WeldGroupId
                        });

                        planB.Cuts.Add(new Cut
                        {
                            Length = cutA.Length,
                            OrderIndex = cutA.OrderIndex,
                            RequiresWelding = cutA.RequiresWelding,
                            WeldGroupId = cutA.WeldGroupId
                        });

                        planA.Leftover = newALeftover;
                        planB.Leftover = newBLeftover;

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates waste for a single plan.
        /// </summary>
        private static int CalculateWaste(CuttingPlan plan, SolverOptions options)
        {
            return plan.Leftover < options.Gamma ? plan.Leftover : 0;
        }

        #endregion

        #region Welding

        /// <summary>
        /// Processes remaining orders using welding.
        /// </summary>
        public static int ProcessWeldedOrders(
            List<int> remainingOrders,
            List<RebarStock> stock,
            int alreadyUsedStockCount,
            SolverOptions options,
            SolverResult result)
        {
            int weldGroupId = 1;

            var stockUsage = InitializeStockUsage(stock, alreadyUsedStockCount);

            for (int i = remainingOrders.Count - 1; i >= 0; i--)
            {
                var orderLength = remainingOrders[i];
                var neededLength = orderLength;
                var pieces = new List<(int length, int stockLength)>();

                for (int si = 0; si < stock.Count; si++)
                {
                    var stockItem = stock[si];
                    if (neededLength <= 0)
                        break;

                    var usedFromThisStock = stockUsage[si];
                    var availableStocks = stockItem.Quantity - usedFromThisStock;

                    while (availableStocks > 0 && neededLength > 0)
                    {
                        var pieceLength = Math.Min(stockItem.Length, neededLength);
                        if (pieceLength >= options.Delta)
                        {
                            pieces.Add((pieceLength, stockItem.Length));
                            neededLength -= pieceLength;
                            stockUsage[si]++;
                            availableStocks--;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (neededLength <= 0 && pieces.Count > 0)
                {
                    bool requiresWelding = pieces.Count > 1;

                    foreach (var (pieceLength, stockLength) in pieces)
                    {
                        var plan = new CuttingPlan
                        {
                            StockLength = stockLength,
                            Cuts = new List<Cut>(),
                            Leftover = stockLength
                        };
                        result.CuttingPlans.Add(plan);

                        var cut = new Cut
                        {
                            Length = pieceLength,
                            OrderIndex = 0,
                            RequiresWelding = requiresWelding,
                            WeldGroupId = requiresWelding ? weldGroupId : null
                        };

                        plan.Cuts.Add(cut);
                        plan.Leftover -= pieceLength;
                    }

                    if (requiresWelding)
                    {
                        weldGroupId++;
                    }
                    remainingOrders.RemoveAt(i);
                }
            }

            return weldGroupId - 1;
        }

        /// <summary>
        /// Initializes stock usage tracking using index-based array.
        /// </summary>
        private static int[] InitializeStockUsage(
            List<RebarStock> stock,
            int alreadyUsedStockCount)
        {
            var stockUsage = new int[stock.Count];
            int remainingToSkip = alreadyUsedStockCount;

            for (int i = 0; i < stock.Count; i++)
            {
                if (remainingToSkip >= stock[i].Quantity)
                {
                    stockUsage[i] = stock[i].Quantity;
                    remainingToSkip -= stock[i].Quantity;
                }
                else if (remainingToSkip > 0)
                {
                    stockUsage[i] = remainingToSkip;
                    remainingToSkip = 0;
                }
            }

            return stockUsage;
        }

        #endregion

        #region Order Management

        /// <summary>
        /// Updates order quantities after cuts are made.
        /// Performance optimized: O(1) lookup using index-based single pass.
        /// </summary>
        public static void UpdateOrders(List<Order> orders, List<int> cuts)
        {
            var cutCounts = cuts.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var indicesToRemove = new List<int>();

            foreach (var kvp in cutCounts)
            {
                var cutLength = kvp.Key;
                var remaining = kvp.Value;

                // Distribute across all orders with this length (not just the first)
                for (int i = 0; i < orders.Count && remaining > 0; i++)
                {
                    var order = orders[i];
                    if (order.Length != cutLength || order.Quantity <= 0)
                        continue;

                    var deduct = Math.Min(order.Quantity, remaining);
                    remaining -= deduct;
                    var newQuantity = order.Quantity - deduct;

                    if (newQuantity > 0)
                    {
                        orders[i] = new Order(order.Length, newQuantity);
                    }
                    else
                    {
                        indicesToRemove.Add(i);
                    }
                }
            }

            foreach (var index in indicesToRemove.OrderByDescending(i => i))
            {
                orders.RemoveAt(index);
            }
        }

        #endregion
    }
}
