using System.Diagnostics;
using CuttingStock.Core.Algorithms.Utilities;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using Google.OrTools.LinearSolver;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>
    /// Variable-stock column generation. Columns are keyed by stock length and
    /// pattern counts, then a CBC integer master chooses across all stock sizes.
    /// </summary>
    public sealed class GlobalStockColumnGenerationSolver : ICuttingSolver
    {
        private const int MaxIterations = 100;
        private const double Eps = 1e-6;
        private const long IntegerMasterTimeLimitMs = 10000;

        public string Name => "Global Stock Column Generation";
        public string Description => "Variable-stock CG with a global generated-column integer master.";
        public string TimeComplexity => "Poly/iter + MIP polish, exp worst-case";

        public SolverResult Solve(
            List<RebarStock> stock,
            List<Order> orders,
            SolverOptions options,
            IProgress<double>? progress = null)
        {
            var result = new SolverResult { AlgorithmName = Name };
            var sw = Stopwatch.StartNew();

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
                var demand = orders
                    .GroupBy(o => o.Length)
                    .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));
                var lengths = demand.Keys.OrderByDescending(k => k).ToList();
                var stockByLength = stock
                    .GroupBy(s => s.Length)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
                var stockLengths = options.UsageOrder == StockUsageOrder.LargeToSmall
                    ? stockByLength.Keys.OrderByDescending(l => l).ToList()
                    : stockByLength.Keys.OrderBy(l => l).ToList();

                if (lengths.Any(len => !stockLengths.Any(s => len <= s)))
                {
                    result.Success = false;
                    result.ErrorMessage = "At least one order is longer than every available stock length.";
                    return result;
                }

                var columns = new List<Column>();
                var signatures = new HashSet<string>();
                foreach (int stockLength in stockLengths)
                    AddInitialColumns(columns, signatures, stockLength, lengths, kerf);

                int[] demandVector = lengths.Select(len => demand[len]).ToArray();
                for (int iter = 0; iter < MaxIterations; iter++)
                {
                    if (!SolveLpMaster(columns, demandVector, out var pi))
                        break;

                    bool anyAdded = false;
                    foreach (int stockLength in stockLengths)
                    {
                        var priced = PriceColumn(stockLength, lengths, pi, kerf);
                        if (priced.Value <= stockLength + Eps) continue;
                        if (AddIfNew(columns, signatures, priced.Column))
                            anyAdded = true;
                    }

                    progress?.Report(80.0 * (iter + 1) / MaxIterations);
                    if (!anyAdded) break;
                }

                if (!TrySolveIntegerMaster(columns, demandVector, lengths, stockByLength, kerf, out var plans))
                {
                    var fallback = new ColumnGenerationSolver().Solve(stock, orders, options, progress);
                    fallback.AlgorithmName = $"{Name} (fallback)";
                    return fallback;
                }

                result.CuttingPlans.AddRange(plans);
                result.Success = true;

                SolverResultFinalizer.FinalizeAndValidate(stock, orders, options, result);
                progress?.Report(100.0);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
            finally
            {
                sw.Stop();
                result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        private static void AddInitialColumns(
            List<Column> columns,
            HashSet<string> signatures,
            int stockLength,
            List<int> lengths,
            int kerf)
        {
            for (int i = 0; i < lengths.Count; i++)
            {
                if (lengths[i] > stockLength) continue;
                var identity = new Column(stockLength, new int[lengths.Count]);
                identity.Counts[i] = 1;
                AddIfNew(columns, signatures, identity);
            }

            foreach (int startLen in lengths)
            {
                if (startLen > stockLength) continue;

                var counts = new int[lengths.Count];
                int remaining = stockLength;
                int cutsSoFar = 0;
                int startIdx = lengths.IndexOf(startLen);
                counts[startIdx]++;
                remaining -= startLen;
                cutsSoFar++;

                for (int i = 0; i < lengths.Count; i++)
                {
                    int len = lengths[i];
                    while (true)
                    {
                        int weight = len + (cutsSoFar > 0 ? kerf : 0);
                        if (weight > remaining) break;
                        counts[i]++;
                        remaining -= weight;
                        cutsSoFar++;
                    }
                }

                AddIfNew(columns, signatures, new Column(stockLength, counts));
            }
        }

        private static bool SolveLpMaster(List<Column> columns, int[] demand, out double[] pi)
        {
            pi = Array.Empty<double>();
            var solver = Solver.CreateSolver("GLOP");
            if (solver == null) return false;

            var vars = new Variable[columns.Count];
            for (int p = 0; p < columns.Count; p++)
                vars[p] = solver.MakeNumVar(0.0, double.PositiveInfinity, $"x{p}");

            var constraints = new Constraint[demand.Length];
            for (int i = 0; i < demand.Length; i++)
            {
                constraints[i] = solver.MakeConstraint(demand[i], double.PositiveInfinity, $"d{i}");
                for (int p = 0; p < columns.Count; p++)
                    if (columns[p].Counts[i] != 0)
                        constraints[i].SetCoefficient(vars[p], columns[p].Counts[i]);
            }

            var obj = solver.Objective();
            for (int p = 0; p < columns.Count; p++)
                obj.SetCoefficient(vars[p], columns[p].StockLength);
            obj.SetMinimization();

            var status = solver.Solve();
            if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE)
                return false;

            pi = constraints.Select(c => c.DualValue()).ToArray();
            return pi.All(double.IsFinite);
        }

        private static (Column Column, double Value) PriceColumn(
            int stockLength,
            List<int> lengths,
            double[] pi,
            int kerf)
        {
            int capacity = stockLength + kerf;
            var dp = new double[capacity + 1];
            var choice = new int[capacity + 1];
            Array.Fill(choice, -1);

            for (int w = 1; w <= capacity; w++)
            {
                for (int i = 0; i < lengths.Count; i++)
                {
                    if (lengths[i] > stockLength || pi[i] <= Eps) continue;
                    int weight = lengths[i] + kerf;
                    if (weight > w) continue;

                    double value = dp[w - weight] + pi[i];
                    if (value > dp[w] + 1e-9)
                    {
                        dp[w] = value;
                        choice[w] = i;
                    }
                }
            }

            var counts = new int[lengths.Count];
            int current = capacity;
            while (current > 0 && choice[current] != -1)
            {
                int item = choice[current];
                counts[item]++;
                current -= lengths[item] + kerf;
            }

            return (new Column(stockLength, counts), dp[capacity]);
        }

        private static bool TrySolveIntegerMaster(
            List<Column> columns,
            int[] demand,
            List<int> lengths,
            Dictionary<int, int> stockByLength,
            int kerf,
            out List<CuttingPlan> plans)
        {
            plans = new List<CuttingPlan>();
            var solver = Solver.CreateSolver("CBC");
            if (solver == null) return false;
            solver.SetTimeLimit(IntegerMasterTimeLimitMs);

            var vars = new Variable[columns.Count];
            for (int p = 0; p < columns.Count; p++)
            {
                int ub = stockByLength[columns[p].StockLength];
                for (int i = 0; i < demand.Length; i++)
                {
                    int count = columns[p].Counts[i];
                    if (count > 0)
                        ub = Math.Min(ub, demand[i] / count);
                }
                vars[p] = solver.MakeIntVar(0, ub, $"x{p}");
            }

            for (int i = 0; i < demand.Length; i++)
            {
                var c = solver.MakeConstraint(demand[i], demand[i], $"d{i}");
                for (int p = 0; p < columns.Count; p++)
                    if (columns[p].Counts[i] != 0)
                        c.SetCoefficient(vars[p], columns[p].Counts[i]);
            }

            foreach (var (stockLength, quantity) in stockByLength)
            {
                var c = solver.MakeConstraint(0, quantity, $"q{stockLength}");
                for (int p = 0; p < columns.Count; p++)
                    if (columns[p].StockLength == stockLength)
                        c.SetCoefficient(vars[p], 1);
            }

            var obj = solver.Objective();
            for (int p = 0; p < columns.Count; p++)
                obj.SetCoefficient(vars[p], ComputeWaste(columns[p], lengths, kerf));
            obj.SetMinimization();

            var status = solver.Solve();
            if (status != Solver.ResultStatus.OPTIMAL && status != Solver.ResultStatus.FEASIBLE)
                return false;

            var remaining = demand.ToArray();
            for (int p = 0; p < columns.Count; p++)
            {
                int count = (int)Math.Round(vars[p].SolutionValue());
                for (int use = 0; use < count; use++)
                {
                    var cuts = new List<Cut>();
                    for (int i = 0; i < lengths.Count; i++)
                    {
                        for (int k = 0; k < columns[p].Counts[i]; k++)
                        {
                            if (remaining[i] <= 0) return false;
                            cuts.Add(new Cut { Length = lengths[i] });
                            remaining[i]--;
                        }
                    }

                    var plan = new CuttingPlan
                    {
                        StockLength = columns[p].StockLength,
                        Cuts = cuts,
                        Leftover = SolverUtils.ComputeLeftover(columns[p].StockLength, cuts, kerf)
                    };
                    if (plan.Leftover < 0) return false;
                    plans.Add(plan);
                }
            }

            return remaining.All(v => v == 0);
        }

        private static long ComputeWaste(Column column, List<int> lengths, int kerf)
        {
            long cutCount = 0;
            long used = 0;
            for (int i = 0; i < lengths.Count; i++)
            {
                cutCount += column.Counts[i];
                used += (long)column.Counts[i] * lengths[i];
            }

            long kerfLoss = cutCount > 0 ? (cutCount - 1) * (long)kerf : 0;
            return column.StockLength - used - kerfLoss;
        }

        private static bool AddIfNew(List<Column> columns, HashSet<string> signatures, Column column)
        {
            if (column.Counts.Sum() == 0) return false;
            string signature = $"{column.StockLength}:{string.Join(",", column.Counts)}";
            if (!signatures.Add(signature)) return false;
            columns.Add(column);
            return true;
        }

        private sealed class Column
        {
            public Column(int stockLength, int[] counts)
            {
                StockLength = stockLength;
                Counts = counts;
            }

            public int StockLength { get; }
            public int[] Counts { get; }
        }
    }
}
