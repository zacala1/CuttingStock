using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using Google.OrTools.LinearSolver;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Arc flow network model solved by OR-Tools SCIP. DAG nodes = positions,
    /// item arcs = cuts (width = length + kerf). Provably optimal.
    /// Ref: Valerio de Carvalho 1999.
    /// </summary>
    public class ArcFlowSolver : ICuttingSolver
    {
        private const int MipTimeLimitMs = 30000;

        public string Name => "Arc Flow MIP (OR-Tools)";
        public string Description => "Exact arc flow network + SCIP MIP.";
        public string TimeComplexity => "Exact (MIP, 30s limit)";

        /// <inheritdoc />
        public SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null)
        {
            var result = new SolverResult { AlgorithmName = this.Name };
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var (isValid, errorMessage) = SolverUtils.ValidateInputs(stock, orders);
                if (!isValid)
                {
                    result.Success = false;
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                int kerf = options.Kerf;

                // Aggregate demand
                var demand = orders.GroupBy(o => o.Length)
                                   .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));

                // Aggregate stock by length
                var stockByLength = stock.GroupBy(s => s.Length)
                                         .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

                var sortedStockLengths = options.UsageOrder == StockUsageOrder.LargeToSmall
                    ? stockByLength.Keys.OrderByDescending(l => l).ToList()
                    : stockByLength.Keys.OrderBy(l => l).ToList();

                var itemLengths = demand.Keys.OrderByDescending(k => k).ToList();

                // GCD optimization: reduce node count
                var gcdValues = itemLengths.Concat(sortedStockLengths).ToList();
                if (kerf > 0) gcdValues.Add(kerf);
                int gcd = ComputeGCD(gcdValues);
                if (gcd <= 0) gcd = 1;

                progress?.Report(10);

                // Build and solve the model
                SolveArcFlow(result, stockByLength, sortedStockLengths, demand, itemLengths, kerf, gcd, options, progress);

                // Verify fulfillment
                var remainingDemand = new Dictionary<int, int>(demand);
                foreach (var plan in result.CuttingPlans)
                {
                    foreach (var cut in plan.Cuts)
                    {
                        if (remainingDemand.TryGetValue(cut.Length, out int qty) && qty > 0)
                            remainingDemand[cut.Length] = qty - 1;
                    }
                }

                int unfulfilled = remainingDemand.Values.Where(v => v > 0).Sum();
                result.Success = unfulfilled == 0;
                if (!result.Success)
                    result.ErrorMessage = $"Failed to process {unfulfilled} order(s). MIP solver could not find a feasible solution.";

                SolverResultFinalizer.FinalizeAndValidate(stock, orders, options, result);

                progress?.Report(100);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Arc Flow solver error: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private void SolveArcFlow(
            SolverResult result,
            Dictionary<int, int> stockByLength,
            List<int> stockLengths,
            Dictionary<int, int> demand,
            List<int> itemLengths,
            int kerf,
            int gcd,
            SolverOptions options,
            IProgress<double>? progress)
        {
            var solver = Solver.CreateSolver("SCIP");
            if (solver == null)
            {
                throw new InvalidOperationException("SCIP solver not available. Ensure Google.OrTools is correctly installed.");
            }

            // Set time limit (30 seconds)
            solver.SetTimeLimit(MipTimeLimitMs);

            // For each stock length, build an arc flow sub-graph
            // z[s] = number of bars of stock length s used
            // x[s,i,u] = flow of item i starting at scaled position u in stock s
            // w[s,u] = waste flow at scaled position u in stock s

            var zVars = new Dictionary<int, Variable>();       // z[stockLen]
            var itemFlows = new Dictionary<(int stockLen, int itemLen, int u), Variable>();
            var wasteFlows = new Dictionary<(int stockLen, int u), Variable>();

            foreach (int sLen in stockLengths)
            {
                int maxBars = stockByLength[sLen];
                // Item arcs carry (length + kerf). Expanding the stock capacity by
                // one kerf makes a path with n cuts consume sum(lengths) + n*kerf
                // <= stock + kerf, which is equivalent to kerf only between cuts.
                int capacity = (sLen + kerf) / gcd; // scaled expanded capacity

                zVars[sLen] = solver.MakeIntVar(0, maxBars, $"z_{sLen}");

                // Compute reachable nodes via BFS
                var reachable = ComputeReachable(capacity, itemLengths, kerf, gcd);

                // Item arcs
                foreach (int itemLen in itemLengths)
                {
                    if (itemLen > sLen) continue;
                    int arcLen = (itemLen + kerf) / gcd; // scaled arc length

                    foreach (int u in reachable)
                    {
                        int v = u + arcLen;
                        if (v <= capacity && reachable.Contains(v))
                        {
                            var x = solver.MakeIntVar(0, double.PositiveInfinity, $"x_{sLen}_{itemLen}_{u}");
                            itemFlows[(sLen, itemLen, u)] = x;
                        }
                    }
                }

                // Waste arcs (unit steps for loss)
                foreach (int u in reachable)
                {
                    if (u + 1 <= capacity && reachable.Contains(u + 1))
                    {
                        wasteFlows[(sLen, u)] = solver.MakeIntVar(0, double.PositiveInfinity, $"w_{sLen}_{u}");
                    }
                }

                // Flow conservation constraints
                foreach (int u in reachable)
                {
                    if (u == 0 || u == capacity) continue;

                    // Inflow at u
                    var inflow = new LinearExpr();
                    foreach (int itemLen in itemLengths)
                    {
                        int arcLen = (itemLen + kerf) / gcd;
                        int fromU = u - arcLen;
                        if (fromU >= 0 && itemFlows.TryGetValue((sLen, itemLen, fromU), out var xIn))
                            inflow += xIn;
                    }
                    if (wasteFlows.TryGetValue((sLen, u - 1), out var wIn))
                        inflow += wIn;

                    // Outflow at u
                    var outflow = new LinearExpr();
                    foreach (int itemLen in itemLengths)
                    {
                        int arcLen = (itemLen + kerf) / gcd;
                        if (itemFlows.TryGetValue((sLen, itemLen, u), out var xOut))
                            outflow += xOut;
                    }
                    if (wasteFlows.TryGetValue((sLen, u), out var wOut))
                        outflow += wOut;

                    solver.Add(inflow == outflow);
                }

                // Source constraint: outflow at node 0 = z[s]
                {
                    var outflow0 = new LinearExpr();
                    foreach (int itemLen in itemLengths)
                    {
                        if (itemFlows.TryGetValue((sLen, itemLen, 0), out var x))
                            outflow0 += x;
                    }
                    if (wasteFlows.TryGetValue((sLen, 0), out var w0))
                        outflow0 += w0;
                    solver.Add(outflow0 == zVars[sLen]);
                }

                // Sink constraint: inflow at node capacity = z[s]
                {
                    var inflowC = new LinearExpr();
                    foreach (int itemLen in itemLengths)
                    {
                        int arcLen = (itemLen + kerf) / gcd;
                        int fromU = capacity - arcLen;
                        if (fromU >= 0 && itemFlows.TryGetValue((sLen, itemLen, fromU), out var xIn))
                            inflowC += xIn;
                    }
                    if (wasteFlows.TryGetValue((sLen, capacity - 1), out var wIn))
                        inflowC += wIn;
                    solver.Add(inflowC == zVars[sLen]);
                }
            }

            // Demand constraints: for each item, total flow across all stocks >= demand
            // Group item flows by itemLen for O(1) lookup
            var flowsByItem = itemFlows
                .GroupBy(kvp => kvp.Key.itemLen)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToList());

            foreach (int itemLen in itemLengths)
            {
                var totalFlow = new LinearExpr();
                if (flowsByItem.TryGetValue(itemLen, out var flows))
                {
                    foreach (var flow in flows)
                        totalFlow += flow;
                }
                solver.Add(totalFlow >= demand[itemLen]);
            }

            progress?.Report(50);

            // Objective: minimize total bars used
            var objective = new LinearExpr();
            foreach (var z in zVars.Values) objective += z;
            solver.Minimize(objective);

            // Solve
            var status = solver.Solve();

            progress?.Report(80);

            if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE)
            {
                throw new InvalidOperationException($"MIP solver returned status: {status}. No feasible solution found.");
            }

            // Extract solution: convert flows to CuttingPlans, trim excess cuts
            ExtractPlans(result, solver, stockLengths, itemLengths, itemFlows, zVars, demand, kerf, gcd, options);
        }

        private void ExtractPlans(
            SolverResult result,
            Solver solver,
            List<int> stockLengths,
            List<int> itemLengths,
            Dictionary<(int stockLen, int itemLen, int u), Variable> itemFlows,
            Dictionary<int, Variable> zVars,
            Dictionary<int, int> demand,
            int kerf,
            int gcd,
            SolverOptions options)
        {
            // Track remaining demand to trim excess cuts
            var remainingDemand = new Dictionary<int, int>(demand);

            foreach (int sLen in stockLengths)
            {
                int barsUsed = (int)Math.Round(zVars[sLen].SolutionValue());
                if (barsUsed == 0) continue;

                int capacity = (sLen + kerf) / gcd;

                var flowValues = new Dictionary<(int itemLen, int u), int>();
                foreach (int itemLen in itemLengths)
                {
                    int arcLen = (itemLen + kerf) / gcd;
                    for (int u = 0; u + arcLen <= capacity; u++)
                    {
                        if (itemFlows.TryGetValue((sLen, itemLen, u), out var x))
                        {
                            int val = (int)Math.Round(x.SolutionValue());
                            if (val > 0)
                                flowValues[(itemLen, u)] = val;
                        }
                    }
                }

                var patterns = DecomposeFlowToPatterns(flowValues, itemLengths, capacity, kerf, gcd, barsUsed);

                foreach (var pattern in patterns)
                {
                    // Trim excess cuts: only include cuts that fulfill remaining demand
                    var trimmedCuts = new List<Cut>();
                    foreach (int len in pattern)
                    {
                        if (remainingDemand.TryGetValue(len, out int needed) && needed > 0)
                        {
                            trimmedCuts.Add(new Cut { Length = len });
                            remainingDemand[len] = needed - 1;
                        }
                    }

                    if (trimmedCuts.Count > 0)
                    {
                        var plan = new CuttingPlan
                        {
                            StockLength = sLen,
                            Cuts = trimmedCuts,
                            Leftover = SolverUtils.ComputeLeftover(sLen, trimmedCuts, kerf)
                        };
                        result.CuttingPlans.Add(plan);
                    }
                }
            }
        }

        /// <summary>
        /// Decompose aggregate flow into individual cutting patterns (paths).
        /// Uses greedy path tracing from node 0 to capacity.
        /// </summary>
        private List<List<int>> DecomposeFlowToPatterns(
            Dictionary<(int itemLen, int u), int> flowValues,
            List<int> itemLengths,
            int capacity,
            int kerf,
            int gcd,
            int expectedPaths)
        {
            var patterns = new List<List<int>>();
            var remaining = new Dictionary<(int itemLen, int u), int>(flowValues);

            for (int p = 0; p < expectedPaths; p++)
            {
                var cuts = new List<int>();
                int pos = 0;
                // Guard against degenerate flows where neither an item nor a waste arc
                // exists from the current position — without a cap the outer for would
                // never advance past pos == capacity, but an inner infinite loop is
                // still possible if every step lands on a dead-end node.
                int safety = capacity + 1;

                while (pos < capacity && safety-- > 0)
                {
                    bool moved = false;

                    // Try item arcs first (prefer items over waste)
                    foreach (int itemLen in itemLengths)
                    {
                        int arcLen = (itemLen + kerf) / gcd;
                        var key = (itemLen, pos);
                        if (remaining.TryGetValue(key, out int val) && val > 0)
                        {
                            cuts.Add(itemLen);
                            remaining[key] = val - 1;
                            pos += arcLen;
                            moved = true;
                            break;
                        }
                    }

                    if (!moved)
                    {
                        // Waste arc: advance by 1
                        pos++;
                    }
                }

                if (cuts.Count > 0)
                    patterns.Add(cuts);
            }

            return patterns;
        }

        private static HashSet<int> ComputeReachable(int capacity, List<int> itemLengths, int kerf, int gcd)
        {
            var reachable = new HashSet<int> { 0, capacity };
            var queue = new Queue<int>();
            queue.Enqueue(0);

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();

                // Item arcs
                foreach (int itemLen in itemLengths)
                {
                    int arcLen = (itemLen + kerf) / gcd;
                    int v = u + arcLen;
                    if (v <= capacity && reachable.Add(v))
                        queue.Enqueue(v);
                }

                // Waste arc (step by 1)
                if (u + 1 <= capacity && reachable.Add(u + 1))
                    queue.Enqueue(u + 1);
            }

            // Also compute backward reachable from capacity
            var backReachable = new HashSet<int> { capacity };
            var backQueue = new Queue<int>();
            backQueue.Enqueue(capacity);

            while (backQueue.Count > 0)
            {
                int v = backQueue.Dequeue();

                foreach (int itemLen in itemLengths)
                {
                    int arcLen = (itemLen + kerf) / gcd;
                    int u = v - arcLen;
                    if (u >= 0 && backReachable.Add(u))
                        backQueue.Enqueue(u);
                }

                if (v - 1 >= 0 && backReachable.Add(v - 1))
                    backQueue.Enqueue(v - 1);
            }

            // Intersection: nodes reachable from 0 AND can reach capacity
            reachable.IntersectWith(backReachable);
            return reachable;
        }

        private static int ComputeGCD(List<int> values)
        {
            if (values.Count == 0) return 1;
            int result = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                result = GCD(result, values[i]);
                if (result == 1) return 1;
            }
            return Math.Max(1, result);
        }

        private static int GCD(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0) { int t = b; b = a % b; a = t; }
            return a;
        }
    }
}
