using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.Core.Algorithms.Utilities;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Column Generation Optimizer (Enhanced with Multi-Stock Support)
    ///
    /// Solves the Cutting Stock Problem using the Column Generation technique.
    /// Uses a custom Simplex solver for the Restricted Master Problem (RMP).
    /// Uses the Knapsack DP for the Pricing Problem (Sub-problem).
    ///
    /// Enhancements (v2.0):
    /// - Multiple stock length support
    /// - Per-stock pricing problem
    /// - Improved integer solution generation
    ///
    /// References:
    /// - Gilmore and Gomory (1961): "A Linear Programming Approach to the Cutting-Stock Problem"
    /// - https://jump.dev/JuMP.jl/stable/tutorials/algorithms/cutting_stock_column_generation/
    /// </summary>
    public class ColumnGenerationSolver : ICuttingSolver
    {
        /// <inheritdoc/>
        public string Name => "Column Generation (LP)";

        /// <inheritdoc/>
        public string Description => "Global Optimization using Linear Programming and Column Generation with Multi-Stock Support";

        /// <inheritdoc/>
        public string TimeComplexity => "Exponential (NP-Hard)";

        /// <inheritdoc/>
        public SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null)
        {
            var result = new SolverResult { AlgorithmName = this.Name };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Validate inputs
                var (isValid, errorMessage) = SolverUtils.ValidateInputs(stock, orders);
                if (!isValid)
                {
                    result.Success = false;
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                // Track remaining demand to verify all orders are fulfilled
                var remainingDemand = orders.GroupBy(o => o.Length)
                                            .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));

                // Check if we should use multi-stock or single-stock approach
                var distinctStockLengths = stock.Select(s => s.Length).Distinct().OrderByDescending(l => l).ToList();

                if (distinctStockLengths.Count == 1)
                {
                    SolveSingleStock(result, stock, orders, options, progress);
                }
                else
                {
                    SolveMultiStock(result, stock, orders, options, progress);
                }

                // Verify all orders were fulfilled
                foreach (var plan in result.CuttingPlans)
                {
                    foreach (var cut in plan.Cuts)
                    {
                        if (remainingDemand.TryGetValue(cut.Length, out int qty) && qty > 0)
                        {
                            remainingDemand[cut.Length] = qty - 1;
                        }
                    }
                }

                int unfulfilledCount = remainingDemand.Values.Sum();
                if (unfulfilledCount > 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to process {unfulfilledCount} order(s). Insufficient stock or patterns.";
                }
                else
                {
                    result.Success = true;
                }

                SolverUtils.CalculateResults(result, options);
                progress?.Report(100.0);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Solves CSP with multiple stock lengths.
        /// Runs column generation for each stock length and combines results.
        /// </summary>
        private void SolveMultiStock(SolverResult result, List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress)
        {
            var currentDemand = orders.GroupBy(o => o.Length)
                                      .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));

            var stockByLength = stock.GroupBy(s => s.Length)
                                     .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            var sortedStockLengths = options.UsageOrder == StockUsageOrder.LargeToSmall
                ? stockByLength.Keys.OrderByDescending(l => l).ToList()
                : stockByLength.Keys.OrderBy(l => l).ToList();

            int totalProgress = 0;
            int progressPerStock = 80 / Math.Max(1, sortedStockLengths.Count);

            foreach (var stockLength in sortedStockLengths)
            {
                if (!currentDemand.Any(kv => kv.Value > 0))
                    break;

                int availableStock = stockByLength[stockLength];
                if (availableStock <= 0)
                    continue;

                // Filter demands that can fit in this stock
                var feasibleDemand = currentDemand
                    .Where(kv => kv.Key <= stockLength && kv.Value > 0)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                if (!feasibleDemand.Any())
                    continue;

                // Run column generation for this stock length
                var subResult = new SolverResult();
                SolveSingleStockInternal(subResult, stockLength, availableStock, feasibleDemand, progress);

                // Update demand based on what was cut
                foreach (var plan in subResult.CuttingPlans)
                {
                    foreach (var cut in plan.Cuts)
                    {
                        if (currentDemand.ContainsKey(cut.Length) && currentDemand[cut.Length] > 0)
                        {
                            currentDemand[cut.Length]--;
                        }
                    }
                    result.CuttingPlans.Add(plan);
                }

                // Update available stock
                stockByLength[stockLength] -= subResult.CuttingPlans.Count;

                totalProgress += progressPerStock;
                progress?.Report(totalProgress);
            }
        }

        /// <summary>
        /// Original single-stock algorithm wrapper.
        /// </summary>
        private void SolveSingleStock(SolverResult result, List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress)
        {
            var primaryStock = stock.OrderByDescending(s => s.Length).First();
            int stockLength = primaryStock.Length;
            int availableStock = stock.Where(s => s.Length == stockLength).Sum(s => s.Quantity);

            var demand = orders.GroupBy(o => o.Length)
                              .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));

            SolveSingleStockInternal(result, stockLength, availableStock, demand, progress);
        }

        /// <summary>
        /// Internal single-stock column generation implementation.
        /// </summary>
        private void SolveSingleStockInternal(SolverResult result, int stockLength, int maxStockCount, Dictionary<int, int> demand, IProgress<double>? progress)
        {
            var distinctLengths = demand.Keys.OrderByDescending(k => k).ToList();
            int numConstraints = distinctLengths.Count;

            if (numConstraints == 0)
                return;

            // 2. Initial Columns Generation
            // Add Identity Patterns (one cut per length) to form the Initial Basis for Simplex.
            var patterns = new List<CuttingPatternColumn>();

            // Mandatory Identity Patterns
            for (int i = 0; i < numConstraints; i++)
            {
                var col = new CuttingPatternColumn(numConstraints);
                col.Counts[i] = 1;
                patterns.Add(col);
            }

            // Add Greedy Patterns to improve convergence and integer solution quality
            foreach (var startLen in distinctLengths)
            {
                var col = new CuttingPatternColumn(numConstraints);
                int currentRem = stockLength;

                int startIdx = distinctLengths.IndexOf(startLen);
                if (startLen <= currentRem)
                {
                    col.Counts[startIdx]++;
                    currentRem -= startLen;
                }

                // Fill remaining space with largest possible items
                for (int i = 0; i < numConstraints; i++)
                {
                    int len = distinctLengths[i];
                    while (len <= currentRem)
                    {
                        col.Counts[i]++;
                        currentRem -= len;
                    }
                }

                if (!patterns.Any(p => p.Equals(col)))
                {
                    patterns.Add(col);
                }
            }

            bool improved = true;
            int maxIterations = 100; // Prevent infinite loop
            int iter = 0;

            while (improved && iter < maxIterations)
            {
                iter++;
                // Report progress (Approximate)
                progress?.Report((double)iter / maxIterations * 80.0);

                // 3. Solve RMP (Restricted Master Problem)
                var solver = new SimplexSolver();
                var dualValues = solver.SolveRelaxed(patterns, demand, distinctLengths);

                // 4. Solve Pricing Problem (Knapsack)
                // Find a pattern with Reduced Cost < 0 (Maximization: Reduced Cost > 0 if formulated as max)
                // Knapsack Value = Sum(dual_i * a_i) > 1

                var knapsackItems = new List<KnapsackItem>();
                for (int i = 0; i < numConstraints; i++)
                {
                    knapsackItems.Add(new KnapsackItem
                    {
                        Length = distinctLengths[i],
                        Value = dualValues[i],
                        Index = i
                    });
                }

                var newPattern = SolveKnapsack(knapsackItems, stockLength);

                // Check if new column improves the solution
                if (newPattern.TotalValue > 1.00001)
                {
                    var col = new CuttingPatternColumn(numConstraints);
                    foreach (var item in newPattern.Items)
                    {
                        col.Counts[item.Index]++;
                    }

                    if (!patterns.Any(p => p.Equals(col)))
                    {
                        patterns.Add(col);
                    }
                    else
                    {
                        improved = false;
                    }
                }
                else
                {
                    improved = false;
                }
            }

            // 5. Generate Integer Solution (respecting stock limit)
            GenerateSolutionHybrid(result, patterns, demand, distinctLengths, stockLength, maxStockCount);
        }

        private KnapsackResult SolveKnapsack(List<KnapsackItem> items, int capacity)
        {
            // Unbounded Knapsack Problem (DP)
            var dp = new double[capacity + 1];
            var itemIdx = new int[capacity + 1];

            Array.Fill(itemIdx, -1);

            for (int w = 1; w <= capacity; w++)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Length <= w)
                    {
                        double val = dp[w - items[i].Length] + items[i].Value;
                        if (val > dp[w] + 1e-9)
                        {
                            dp[w] = val;
                            itemIdx[w] = i;
                        }
                    }
                }
            }

            var res = new KnapsackResult { TotalValue = dp[capacity] };
            int curr = capacity;

            while (curr > 0 && itemIdx[curr] != -1)
            {
                int idx = itemIdx[curr];
                res.Items.Add(items[idx]);
                curr -= items[idx].Length;
            }

            return res;
        }

        private void GenerateSolutionHybrid(
            SolverResult result,
            List<CuttingPatternColumn> patterns,
            Dictionary<int, int> demand,
            List<int> lengths,
            int stockLength,
            int maxStockCount)
        {
            var currentDemand = new Dictionary<int, int>(demand);
            int usedStockCount = 0;

            // Refine LP solution to Integer Solution greedily
            // Priority: Minimize Waste > Maximize Satisfaction Count
            while (currentDemand.Any(kv => kv.Value > 0) && usedStockCount < maxStockCount)
            {
                var bestPatternIndex = -1;
                double bestScore = -double.MaxValue;
                int bestMaxApply = 1;

                for (int p = 0; p < patterns.Count; p++)
                {
                    var pattern = patterns[p];
                    long satisfyCount = 0;
                    long realUsedLen = 0;
                    int maxApply = int.MaxValue;
                    bool isUseful = false;

                    for (int i = 0; i < lengths.Count; i++)
                    {
                        if (pattern.Counts[i] > 0)
                        {
                            int needed = currentDemand[lengths[i]];
                            if (needed > 0)
                            {
                                isUseful = true;
                                int applyCount = needed / pattern.Counts[i];
                                if (applyCount < maxApply) maxApply = applyCount;
                            }
                            else
                            {
                                // If any part of the pattern is not needed, we can't apply it efficiently without waste.
                                // We set maxApply to 0, implying we can't perfectly apply it multiple times.
                                maxApply = 0;
                            }

                            // Calculate metrics for a SINGLE application
                            long countInSingleRun = Math.Min(pattern.Counts[i], needed);
                            satisfyCount += countInSingleRun;
                            realUsedLen += countInSingleRun * lengths[i];
                        }
                    }

                    if (!isUseful) continue;

                    // Score Calculation
                    // Primary: Minimize Effective Waste (StockLength - RealUsedLength)
                    // Secondary: Maximize Satisfied Item Count (Tie-breaker)
                    long effectiveWaste = stockLength - realUsedLen;

                    // We use negative waste so higher is better (0 waste is best)
                    double score = -effectiveWaste + (satisfyCount * 0.001);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPatternIndex = p;
                        // Determine how many times to apply.
                        // If perfectly applicable (maxApply > 0), use that.
                        // If not (maxApply == 0 due to partial mismatch), apply at least once to make progress.
                        bestMaxApply = (maxApply > 0) ? maxApply : 1;
                    }
                }

                if (bestPatternIndex == -1)
                {
                    // No existing pattern fits (should be rare with fallback logic). Break to avoid infinite loop.
                    break;
                }

                var selected = patterns[bestPatternIndex];

                // Apply the selected pattern 'bestMaxApply' times (respecting stock limit)
                int actualApplyCount = Math.Min(bestMaxApply, maxStockCount - usedStockCount);
                for (int applyIter = 0; applyIter < actualApplyCount; applyIter++)
                {
                    var plan = new CuttingPlan { StockLength = stockLength, Cuts = new List<Cut>() };

                    for (int i = 0; i < lengths.Count; i++)
                    {
                        int count = selected.Counts[i];
                        int len = lengths[i];
                        for (int k = 0; k < count; k++)
                        {
                            if (currentDemand[len] > 0)
                            {
                                plan.Cuts.Add(new Cut { Length = len });
                                currentDemand[len]--;
                            }
                        }
                    }

                    // Calculate leftover
                    plan.Leftover = stockLength - plan.Cuts.Sum(c => c.Length);
                    result.CuttingPlans.Add(plan);
                    usedStockCount++;

                    // Check if everything is done or stock exhausted
                    if (!currentDemand.Any(kv => kv.Value > 0) || usedStockCount >= maxStockCount) break;
                }
            }

            // Handle any remaining demand with simple cuts (One-Cut per Stock, respecting limit)
            foreach (var kv in currentDemand.Where(kv => kv.Value > 0).ToList())
            {
                if (usedStockCount >= maxStockCount) break;

                int len = kv.Key;
                int qty = kv.Value;
                int canUse = Math.Min(qty, maxStockCount - usedStockCount);

                for (int i = 0; i < canUse; i++)
                {
                    var plan = new CuttingPlan
                    {
                        StockLength = stockLength,
                        Cuts = new List<Cut> { new Cut { Length = len } },
                        Leftover = stockLength - len
                    };
                    result.CuttingPlans.Add(plan);
                    usedStockCount++;
                }
            }
        }

        /// <summary>
        /// Tableau-based Simplex Solver for Column Generation (RMP)
        /// Objective: Minimize Sum(x_j) (Cost = 1 each)
        /// Constraints: A * x = d
        /// </summary>
        private class SimplexSolver
        {
            public List<double> SolveRelaxed(List<CuttingPatternColumn> patterns, Dictionary<int, int> demand, List<int> lengths)
            {
                int m = lengths.Count; // Constraints (Rows)
                int n = patterns.Count; // Variables (Columns)

                // Tableau Dimensions: (m + 1) x (n + 1)
                // Row 0..m-1: Constraints
                // Row m: Reduced Costs (Objective)
                // Col 0..n-1: Variables
                // Col n: RHS

                double[,] tableau = new double[m + 1, n + 1];

                // 1. Initialize Tableau
                for (int i = 0; i < m; i++)
                {
                    // A matrix part
                    for (int j = 0; j < n; j++)
                    {
                        tableau[i, j] = patterns[j].Counts[i];
                    }

                    // RHS
                    tableau[i, n] = demand[lengths[i]];
                }

                // Objective: Minimize Z = Sum(c_j * x_j) with c_j = 1
                for (int j = 0; j < n; j++)
                {
                    double sumAij = 0;
                    for (int i = 0; i < m; i++)
                    {
                        sumAij += tableau[i, j];
                    }
                    // Reduced Cost for Minimization
                    // r_j = c_j - z_j = 1 - sum(a_ij)
                    tableau[m, j] = 1.0 - sumAij;
                }

                // Initial RHS of Objective (Current Cost)
                double initialCost = 0;
                for (int i = 0; i < m; i++) initialCost += demand[lengths[i]];
                tableau[m, n] = -initialCost;

                // 2. Simplex Iterations
                int maxIter = 1000;
                const double epsilon = 1e-9;

                for (int simplexIter = 0; simplexIter < maxIter; simplexIter++)
                {
                    // Find Entering Variable (Most negative reduced cost)
                    int enteringCol = -1;
                    double minReducedCost = -epsilon;

                    for (int j = 0; j < n; j++)
                    {
                        if (tableau[m, j] < minReducedCost)
                        {
                            minReducedCost = tableau[m, j];
                            enteringCol = j;
                        }
                    }

                    if (enteringCol == -1) break; // Optimality reached

                    // Find Leaving Variable (Min Ratio Test)
                    int leavingRow = -1;
                    double minRatio = double.MaxValue;

                    for (int i = 0; i < m; i++)
                    {
                        double val = tableau[i, enteringCol];
                        if (val > epsilon)
                        {
                            double ratio = tableau[i, n] / val;
                            if (ratio < minRatio)
                            {
                                minRatio = ratio;
                                leavingRow = i;
                            }
                        }
                    }

                    if (leavingRow == -1) break; // Unbounded (should not happen in CSP)

                    // Pivot
                    Pivot(tableau, m, n, leavingRow, enteringCol);
                }

                // 3. Extract Dual Values
                var duals = new List<double>();
                for (int i = 0; i < m; i++)
                {
                    double r_i = tableau[m, i];
                    double y_i = 1.0 - r_i;
                    duals.Add(y_i);
                }

                return duals;
            }

            private void Pivot(double[,] tableau, int m, int n, int pivotRow, int pivotCol)
            {
                double pivotVal = tableau[pivotRow, pivotCol];

                // Guard against near-zero pivot to prevent numerical instability
                if (Math.Abs(pivotVal) < 1e-12)
                    return;

                // 1. Normalize Pivot Row
                for (int j = 0; j <= n; j++)
                {
                    tableau[pivotRow, j] /= pivotVal;
                }

                // 2. Eliminate other rows
                for (int i = 0; i <= m; i++) // Include objective row
                {
                    if (i != pivotRow)
                    {
                        double factor = tableau[i, pivotCol];
                        if (Math.Abs(factor) > 1e-10)
                        {
                            for (int j = 0; j <= n; j++)
                            {
                                tableau[i, j] -= factor * tableau[pivotRow, j];
                            }
                        }
                    }
                }
            }
        }

        private class CuttingPatternColumn : IEquatable<CuttingPatternColumn>
        {
            public int[] Counts { get; }
            public CuttingPatternColumn(int size) { Counts = new int[size]; }

            public bool Equals(CuttingPatternColumn? other)
            {
                if (other is null) return false;
                return Counts.SequenceEqual(other.Counts);
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as CuttingPatternColumn);
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                foreach (var c in Counts) hash.Add(c);
                return hash.ToHashCode();
            }
        }

        private class KnapsackItem
        {
            public int Length { get; set; }
            public double Value { get; set; }
            public int Index { get; set; }
        }

        private class KnapsackResult
        {
            public List<KnapsackItem> Items { get; } = new List<KnapsackItem>();
            public double TotalValue { get; set; }
        }
    }
}
