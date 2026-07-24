using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Models;
using CuttingStock.Core.Domain;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Multi-pass greedy knapsack DP. Pass1 balanced, Pass2 residual, Pass3 fill,
    /// then 2-opt/relocate post-processing. Sparse DP for memory efficiency.
    /// </summary>
    public class GreedyKnapsackSolver : ICuttingSolver
    {
        // Multi-pass limits: Pass1 = balanced (2 cuts/order max), Pass2 = residual (5), Pass3 = fill all.
        private const int Pass1MaxPerOrder = 2;
        private const int Pass2MaxPerOrder = 5;

        public string Name => "Greedy Knapsack DP";
        public string Description => "Multi-pass sparse DP with post-processing.";
        public string TimeComplexity => "O(N * L * Passes)";

        /// <inheritdoc/>
        public SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null)
        {
            var result = new SolverResult();
            var stopwatch = Stopwatch.StartNew();
            result.AlgorithmName = this.Name;

            try
            {
                var (isValid, errorMessage) = SolverUtils.ValidateInputs(stock, orders);
                if (!isValid)
                {
                    result.Success = false;
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                var normalizedStock = SolverUtils.AggregateStockByLength(stock);
                var normalizedOrders = SolverUtils.AggregateOrdersByLength(orders);
                var sortedStock = SolverUtils.SortStock(normalizedStock, options.UsageOrder);
                var sortedOrders = SolverUtils.SortOrdersByScarcity(normalizedOrders);

                var totalStockCount = sortedStock.Sum(s => s.Quantity);
                var allLeftovers = new List<int>();

                long initialTotalOrderQuantity = sortedOrders.Sum(o => (long)o.Quantity);

                ProcessMultiPass(sortedStock, sortedOrders, options, result, allLeftovers, totalStockCount, progress, initialTotalOrderQuantity);

                if (sortedOrders.Any() && allLeftovers.Any())
                {
                    ProcessLeftovers(sortedOrders, allLeftovers, options, result);
                }

                if (options.EnableWelding && sortedOrders.Any())
                {
                    ProcessWeldedOrders(sortedOrders, allLeftovers, sortedStock, options, result);
                }

                SolverUtils.OptimizePostProcess(result, options);
                result.Success = !sortedOrders.Any();
                if (!result.Success)
                {
                    SolverUtils.SetRemainingOrdersError(result, sortedOrders.Count);
                }

                SolverResultFinalizer.FinalizeAndValidate(stock, orders, options, result);
            }
            catch (Exception ex)
            {
                SolverUtils.HandleException(result, ex);
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private void ProcessMultiPass(
            List<RebarStock> sortedStock,
            List<Order> sortedOrders,
            SolverOptions options,
            SolverResult result,
            List<int> allLeftovers,
            int totalStockCount,
            IProgress<double>? progress,
            long initialTotalOrderQuantity)
        {
            // Initialize usedStockCounts by index to avoid mutable object as Dictionary key
            var usedStockCounts = new int[sortedStock.Count];

            ProcessPass(sortedStock, sortedOrders, options, result, allLeftovers,
                        totalStockCount, Pass1MaxPerOrder, "Pass1", progress, initialTotalOrderQuantity, usedStockCounts);

            if (sortedOrders.Any())
            {
                ProcessPass(sortedStock, sortedOrders, options, result, allLeftovers,
                            totalStockCount, Pass2MaxPerOrder, "Pass2", progress, initialTotalOrderQuantity, usedStockCounts);
            }

            if (sortedOrders.Any())
            {
                ProcessPass(sortedStock, sortedOrders, options, result, allLeftovers,
                            totalStockCount, int.MaxValue, "Pass3", progress, initialTotalOrderQuantity, usedStockCounts);
            }
        }

        private void ProcessPass(
            List<RebarStock> sortedStock,
            List<Order> sortedOrders,
            SolverOptions options,
            SolverResult result,
            List<int> allLeftovers,
            int totalStockCount,
            int maxPerOrder,
            string passName,
            IProgress<double>? progress,
            long initialTotalOrderQuantity,
            int[] usedStockCounts)
        {
            for (int stockIdx = 0; stockIdx < sortedStock.Count; stockIdx++)
            {
                var stockItem = sortedStock[stockIdx];
                var availableCount = stockItem.Quantity - usedStockCounts[stockIdx];

                for (int i = 0; i < availableCount; i++)
                {
                    if (!sortedOrders.Any())
                        break;

                    var candidates = FindTopKCutsSparse(stockItem.Length, sortedOrders, totalStockCount, maxPerOrder, options.Kerf, k: 3);

                    List<int> bestCuts = new List<int>();

                    if (candidates.Any())
                    {
                        if (candidates.Count == 1)
                        {
                            bestCuts = candidates[0].Cuts;
                        }
                        else
                        {
                            var bestCandidate = candidates[0];
                            double bestFutureScore = double.MaxValue;

                            // Mutable working buffer keyed by order length — much cheaper than
                            // copying a Dictionary per candidate. Each candidate applies its
                            // cuts, scores future waste, then reverts via the appliedCuts log;
                            // the buffer is reused across candidates.
                            var workingQty = new Dictionary<int, int>(sortedOrders.Count);
                            foreach (var o in sortedOrders)
                            {
                                workingQty[o.Length] = o.Quantity;
                            }
                            var appliedCuts = new List<int>();

                            foreach (var candidate in candidates)
                            {
                                appliedCuts.Clear();
                                foreach (var cut in candidate.Cuts)
                                {
                                    if (workingQty.TryGetValue(cut, out int qty) && qty > 0)
                                    {
                                        workingQty[cut] = qty - 1;
                                        appliedCuts.Add(cut);
                                    }
                                }

                                double futureWaste = EstimateFutureWasteMFFDFromDict(workingQty, stockItem.Length);
                                double totalScore = candidate.Waste + futureWaste;

                                // Revert by replaying the applied log — the only cuts that
                                // actually consumed from workingQty.
                                foreach (var cut in appliedCuts)
                                {
                                    workingQty[cut]++;
                                }

                                if (totalScore < bestFutureScore)
                                {
                                    bestFutureScore = totalScore;
                                    bestCandidate = candidate;
                                }
                            }

                            bestCuts = bestCandidate.Cuts;
                        }
                    }

                    if (bestCuts != null && bestCuts.Any())
                    {
                        var cuts = bestCuts.Select(len => new Cut { Length = len }).ToList();
                        var plan = new CuttingPlan
                        {
                            StockLength = stockItem.Length,
                            Cuts = cuts,
                            Leftover = SolverUtils.ComputeLeftover(stockItem.Length, cuts, options.Kerf)
                        };

                        result.CuttingPlans.Add(plan);
                        usedStockCounts[stockIdx]++;

                        if (plan.Leftover >= options.Gamma)
                        {
                            allLeftovers.Add(plan.Leftover);
                        }

                        UpdateOrders(sortedOrders, bestCuts);
                    }
                    else
                    {
                        var smallestFittingOrder = sortedOrders
                            .OrderBy(o => o.Length)
                            .FirstOrDefault(o => o.Length <= stockItem.Length && o.Quantity > 0);

                        if (smallestFittingOrder != null)
                        {
                            var singleCut = new List<int> { smallestFittingOrder.Length };
                            var singleCuts = singleCut.Select(len => new Cut { Length = len }).ToList();
                            var plan = new CuttingPlan
                            {
                                StockLength = stockItem.Length,
                                Cuts = singleCuts,
                                Leftover = SolverUtils.ComputeLeftover(stockItem.Length, singleCuts, options.Kerf)
                            };

                            result.CuttingPlans.Add(plan);
                            usedStockCounts[stockIdx]++;

                            if (plan.Leftover >= options.Gamma)
                            {
                                allLeftovers.Add(plan.Leftover);
                            }

                            UpdateOrders(sortedOrders, singleCut);
                        }
                        else if (stockItem.Length >= options.Gamma)
                        {
                            allLeftovers.Add(stockItem.Length);
                        }
                    }
                }

                if (!sortedOrders.Any())
                    break;

                if (progress != null && initialTotalOrderQuantity > 0)
                {
                    long currentQuantity = sortedOrders.Sum(o => (long)o.Quantity);
                    double percent = (1.0 - (double)currentQuantity / initialTotalOrderQuantity) * 100.0;
                    progress.Report(percent);
                }
            }
        }

        private class CandidateCut
        {
            public List<int> Cuts { get; set; } = new List<int>();
            public int Waste { get; set; }
            public int CutCount { get; set; }
        }

        private List<int> FindBestCutsSparse(int stockLength, List<Order> orders, int totalStockCount, int maxPerOrder, int kerf = 0)
        {
            var candidates = FindTopKCutsSparse(stockLength, orders, totalStockCount, maxPerOrder, kerf, k: 1);
            return candidates.FirstOrDefault()?.Cuts ?? new List<int>();
        }

        private List<CandidateCut> FindTopKCutsSparse(int stockLength, List<Order> orders, int totalStockCount, int maxPerOrder, int kerf, int k)
        {
            var dp = new Dictionary<int, List<int>>
            {
                [0] = new List<int>()
            };

            var maxUsagePerOrder = new Dictionary<int, int>();
            foreach (var order in orders)
            {
                var fairShare = Math.Max(1, (int)Math.Ceiling((double)order.Quantity / Math.Max(1, totalStockCount / 2)));
                maxUsagePerOrder[order.Length] = Math.Min(Math.Min(order.Quantity, fairShare), maxPerOrder);
            }

            foreach (var order in orders.Where(o => o.Quantity > 0))
            {
                // Dedup by key, keeping the cut list with the fewest entries. A list
                // was used here previously, but later overwrites by insertion order
                // could let a worse candidate overwrite a better one for the same key.
                var newEntries = new Dictionary<int, List<int>>();
                var maxUsage = maxUsagePerOrder.GetValueOrDefault(order.Length, 1);

                foreach (var kvp in dp)
                {
                    var currentLength = kvp.Key;
                    var currentCuts = kvp.Value;

                    // Build frequency count for this order's length in currentCuts
                    int alreadyUsed = 0;
                    foreach (var c in currentCuts)
                    {
                        if (c == order.Length) alreadyUsed++;
                    }

                    for (int count = 1; count <= maxUsage; count++)
                    {
                        var newLength = currentLength + order.Length * count;
                        if (newLength > stockLength)
                            break;

                        if (alreadyUsed + count > order.Quantity)
                            continue;

                        if (alreadyUsed + count > maxUsage)
                            continue;

                        int newCutCount = currentCuts.Count + count;

                        // Skip if neither dp nor newEntries holds an inferior entry
                        // for this key — avoids allocating a list we'd throw away.
                        if (dp.TryGetValue(newLength, out var existingDp) && newCutCount >= existingDp.Count)
                            continue;
                        if (newEntries.TryGetValue(newLength, out var existingNew) && newCutCount >= existingNew.Count)
                            continue;

                        var newCuts = new List<int>(newCutCount);
                        newCuts.AddRange(currentCuts);
                        for (int j = 0; j < count; j++) newCuts.Add(order.Length);
                        newEntries[newLength] = newCuts;
                    }
                }

                foreach (var entry in newEntries)
                {
                    dp[entry.Key] = entry.Value;
                }
            }

            return dp.Select(kvp =>
            {
                int kerfLoss = Math.Max(0, kvp.Value.Count - 1) * kerf;
                return new CandidateCut
                {
                    Cuts = kvp.Value,
                    Waste = stockLength - kvp.Key - kerfLoss,
                    CutCount = kvp.Value.Count
                };
            })
                .Where(c => c.Cuts.Any() && c.Waste >= 0) // kerf may make some combos infeasible
                .OrderBy(c => c.Waste)
                .ThenBy(c => c.CutCount)
                .Take(k)
                .ToList();
        }

        private void UpdateOrders(List<Order> orders, List<int> cuts)
        {
            SolverUtils.UpdateOrders(orders, cuts);
        }

        private void ProcessLeftovers(
            List<Order> remainingOrders,
            List<int> leftovers,
            SolverOptions options,
            SolverResult result)
        {
            leftovers.Sort(options.UsageOrder == StockUsageOrder.SmallToLarge
                ? (a, b) => a.CompareTo(b)
                : (a, b) => b.CompareTo(a));

            var processedIndices = new HashSet<int>();

            for (int i = 0; i < leftovers.Count; i++)
            {
                if (!remainingOrders.Any())
                    break;

                var leftover = leftovers[i];
                var bestCuts = FindBestCutsSparse(leftover, remainingOrders, 1, int.MaxValue, options.Kerf);

                if (bestCuts.Any())
                {
                    var cuts = bestCuts.Select(len => new Cut { Length = len }).ToList();
                    var plan = new CuttingPlan
                    {
                        StockLength = leftover,
                        Cuts = cuts,
                        Leftover = SolverUtils.ComputeLeftover(leftover, cuts, options.Kerf)
                    };

                    result.CuttingPlans.Add(plan);
                    processedIndices.Add(i);

                    UpdateOrders(remainingOrders, bestCuts);
                }
            }

            for (int i = leftovers.Count - 1; i >= 0; i--)
            {
                if (processedIndices.Contains(i))
                {
                    leftovers.RemoveAt(i);
                }
            }
        }

        private void ProcessWeldedOrders(
            List<Order> remainingOrders,
            List<int> leftovers,
            List<RebarStock> sortedStock,
            SolverOptions options,
            SolverResult result)
        {
            int weldGroupId = 1;
            var stockUsage = new int[sortedStock.Count];

            foreach (var plan in result.CuttingPlans)
            {
                for (int si = 0; si < sortedStock.Count; si++)
                {
                    if (sortedStock[si].Length == plan.StockLength)
                    {
                        stockUsage[si]++;
                        break;
                    }
                }
            }

            while (remainingOrders.Any())
            {
                var order = remainingOrders.First();
                var neededLength = order.Length;
                var pieces = new List<(int length, int stockLength)>();

                for (int si = 0; si < sortedStock.Count; si++)
                {
                    var stockItem = sortedStock[si];
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
                        else if (neededLength < options.Delta &&
                                 TryAppendAdjustedWeldTail(pieces, neededLength, stockItem.Length, options.Delta))
                        {
                            neededLength = 0;
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
                        var cut = new Cut
                        {
                            Length = pieceLength,
                            OrderIndex = 0,
                            RequiresWelding = requiresWelding,
                            WeldGroupId = requiresWelding ? weldGroupId : null
                        };

                        // Partial-bar pieces (pieceLength < stockLength) may fit into an
                        // existing non-welded plan's leftover. Doing so avoids burning a
                        // fresh bar when a tail piece can ride along on an already-cut bar.
                        // Full-bar pieces must take a fresh bar; their bar usage was
                        // already reserved above via stockUsage.
                        CuttingPlan? hostPlan = null;
                        if (pieceLength < stockLength)
                        {
                            hostPlan = FindHostPlanForWeld(result.CuttingPlans, pieceLength, options);
                        }

                        if (hostPlan != null)
                        {
                            hostPlan.Cuts.Add(cut);
                            hostPlan.Leftover = SolverUtils.ComputeLeftover(hostPlan.StockLength, hostPlan.Cuts, options.Kerf);

                            // Free the bar we had reserved for this piece — it wasn't needed.
                            for (int si = sortedStock.Count - 1; si >= 0; si--)
                            {
                                if (sortedStock[si].Length == stockLength && stockUsage[si] > 0)
                                {
                                    stockUsage[si]--;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var weldCuts = new List<Cut> { cut };
                            var plan = new CuttingPlan
                            {
                                StockLength = stockLength,
                                Cuts = weldCuts,
                                Leftover = SolverUtils.ComputeLeftover(stockLength, weldCuts, options.Kerf)
                            };
                            result.CuttingPlans.Add(plan);
                        }
                    }

                    if (requiresWelding)
                    {
                        weldGroupId++;
                    }

                    if (order.Quantity > 1)
                    {
                        remainingOrders[0] = new Order(order.Length, order.Quantity - 1);
                    }
                    else
                    {
                        remainingOrders.RemoveAt(0);
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static bool TryAppendAdjustedWeldTail(
            List<(int length, int stockLength)> pieces,
            int shortTailLength,
            int tailStockLength,
            int delta)
        {
            if (shortTailLength <= 0 || shortTailLength >= delta || tailStockLength < delta)
                return false;

            int reduction = delta - shortTailLength;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var prior = pieces[i];
                int adjustedPriorLength = prior.length - reduction;
                if (adjustedPriorLength < delta)
                    continue;

                pieces[i] = (adjustedPriorLength, prior.stockLength);
                pieces.Add((delta, tailStockLength));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Locates an existing non-welded plan whose leftover (after a fresh kerf) can
        /// accommodate a welded piece. Picks the smallest viable leftover to keep larger
        /// scraps available for later. Returns null when no such plan exists.
        /// </summary>
        private static CuttingPlan? FindHostPlanForWeld(List<CuttingPlan> plans, int pieceLength, SolverOptions options)
        {
            CuttingPlan? best = null;
            int bestLeftover = int.MaxValue;

            foreach (var plan in plans)
            {
                // Welded plans are structurally 1 cut per bar — adding to them breaks that
                // invariant and the local-search/redistribute guards that rely on it.
                bool isWeldedPlan = false;
                foreach (var c in plan.Cuts)
                {
                    if (c.WeldGroupId.HasValue) { isWeldedPlan = true; break; }
                }
                if (isWeldedPlan) continue;

                int needed = pieceLength + (plan.Cuts.Count > 0 ? options.Kerf : 0);
                if (plan.Leftover >= needed && plan.Leftover < bestLeftover)
                {
                    best = plan;
                    bestLeftover = plan.Leftover;
                }
            }
            return best;
        }

        private double EstimateFutureWasteMFFDFromDict(Dictionary<int, int> orderDict, int stockLength)
        {
            var items = new List<int>();
            foreach (var kvp in orderDict)
            {
                for (int i = 0; i < kvp.Value; i++) items.Add(kvp.Key);
            }

            return EstimateFutureWasteMFFD(items, stockLength);
        }

        /// <summary>
        /// Modified First-Fit Decreasing (MFFD) with Best-Fit selection.
        /// Classifies items into size categories for better packing.
        /// Reference: https://en.wikipedia.org/wiki/First-fit-decreasing_bin_packing
        /// </summary>
        private double EstimateFutureWasteMFFD(List<int> items, int stockLength)
        {
            if (items.Count == 0) return 0;

            // MFFD: Classify items by size relative to bin capacity
            // Large: > 1/2, Medium: > 1/3, Small: > 1/6, Tiny: <= 1/6
            var large = new List<int>();   // > stockLength / 2
            var medium = new List<int>();  // > stockLength / 3
            var small = new List<int>();   // > stockLength / 6
            var tiny = new List<int>();    // <= stockLength / 6

            foreach (var item in items)
            {
                if (item > stockLength / 2)
                    large.Add(item);
                else if (item > stockLength / 3)
                    medium.Add(item);
                else if (item > stockLength / 6)
                    small.Add(item);
                else
                    tiny.Add(item);
            }

            // Sort each category in descending order
            large.Sort((a, b) => b.CompareTo(a));
            medium.Sort((a, b) => b.CompareTo(a));
            small.Sort((a, b) => b.CompareTo(a));
            tiny.Sort((a, b) => b.CompareTo(a));

            // Combine in priority order: Large -> Medium -> Small -> Tiny
            var sortedItems = new List<int>();
            sortedItems.AddRange(large);
            sortedItems.AddRange(medium);
            sortedItems.AddRange(small);
            sortedItems.AddRange(tiny);

            // Use Best-Fit Decreasing (BFD) instead of First-Fit
            return EstimateFutureWasteBFD(sortedItems, stockLength);
        }

        /// <summary>
        /// Best-Fit Decreasing (BFD) algorithm.
        /// Places each item in the bin with smallest remaining space that fits.
        /// Achieves tighter packing than FFD in practice (94.8% vs 94.7% optimal).
        /// </summary>
        private double EstimateFutureWasteBFD(List<int> sortedItems, int stockLength)
        {
            var bins = new List<int>(); // Remaining capacity of each bin

            foreach (var item in sortedItems)
            {
                int bestBinIndex = -1;
                int bestRemainingSpace = int.MaxValue;

                // Find the bin with smallest remaining space that can fit the item
                for (int i = 0; i < bins.Count; i++)
                {
                    if (bins[i] >= item && bins[i] - item < bestRemainingSpace)
                    {
                        bestRemainingSpace = bins[i] - item;
                        bestBinIndex = i;
                    }
                }

                if (bestBinIndex >= 0)
                {
                    bins[bestBinIndex] -= item;
                }
                else
                {
                    bins.Add(stockLength - item);
                }
            }

            return bins.Sum();
        }
    }
}
