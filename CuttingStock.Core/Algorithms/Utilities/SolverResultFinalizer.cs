using System;
using System.Collections.Generic;
using System.Linq;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Core.Algorithms.Utilities
{
    /// <summary>Centralized finalization and validation for 1D solver results.</summary>
    public static class SolverResultFinalizer
    {
        public static void FinalizeResult(SolverResult result, SolverOptions options)
        {
            RecomputeLeftovers(result, options);

            result.ReusableLeftovers = ComputeAvailableReusableLeftovers(
                result.CuttingPlans,
                options.Gamma);

            result.WasteLength = result.CuttingPlans
                .Where(p => p.Leftover < options.Gamma)
                .Sum(p => (long)p.Leftover);

            var weldGroups = result.CuttingPlans
                .SelectMany(p => p.Cuts)
                .Where(c => c.WeldGroupId.HasValue)
                .GroupBy(c => c.WeldGroupId!.Value);

            result.WeldCount = weldGroups.Sum(g => g.Count() - 1);

            result.TotalCost = (long)Math.Round(
                result.WasteLength * (double)options.Alpha +
                result.WeldCount * (double)options.Beta);
        }

        public static void FinalizeAndValidate(
            List<RebarStock> stock,
            List<Order> orders,
            SolverOptions options,
            SolverResult result)
        {
            FinalizeResult(result, options);

            if (!result.Success)
                return;

            if (ValidateSuccessfulResult(stock, orders, options, result) is { } validationError)
            {
                result.Success = false;
                result.ErrorMessage = validationError;
            }
        }

        public static string? ValidateSuccessfulResult(
            List<RebarStock> stock,
            List<Order> orders,
            SolverOptions options,
            SolverResult result)
        {
            var stockByLength = stock
                .GroupBy(s => s.Length)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
            var demandByLength = orders
                .GroupBy(o => o.Length)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.Quantity));
            var producedByLength = new Dictionary<int, int>();
            var usedStockByLength = new Dictionary<int, int>();
            var consumedSourcePlanIndexes = new HashSet<int>();

            for (int planIndex = 0; planIndex < result.CuttingPlans.Count; planIndex++)
            {
                var plan = result.CuttingPlans[planIndex];
                if (plan.ReusableLeftoverSourcePlanIndex is int sourcePlanIndex)
                {
                    if (sourcePlanIndex < 0 || sourcePlanIndex >= planIndex)
                        return $"Plan {planIndex + 1} has invalid reusable-leftover source plan {sourcePlanIndex + 1}.";
                    if (!consumedSourcePlanIndexes.Add(sourcePlanIndex))
                        return $"Plan {sourcePlanIndex + 1} leftover is reused more than once.";

                    var sourcePlan = result.CuttingPlans[sourcePlanIndex];
                    if (sourcePlan.Leftover < options.Gamma)
                        return $"Plan {sourcePlanIndex + 1} leftover {sourcePlan.Leftover}mm is below Gamma {options.Gamma}mm.";
                    if (sourcePlan.Leftover != plan.StockLength)
                        return $"Plan {planIndex + 1} reuses {plan.StockLength}mm but source plan {sourcePlanIndex + 1} provides {sourcePlan.Leftover}mm.";
                }
                else
                {
                    if (!stockByLength.ContainsKey(plan.StockLength))
                        return $"Plan {planIndex + 1} uses unknown stock length {plan.StockLength}mm.";

                    usedStockByLength.TryGetValue(plan.StockLength, out int used);
                    used++;
                    if (used > stockByLength[plan.StockLength])
                        return $"Stock {plan.StockLength}mm usage {used} exceeds inventory {stockByLength[plan.StockLength]}.";

                    usedStockByLength[plan.StockLength] = used;
                }

                int consumed = plan.Cuts.Sum(c => c.Length) + Math.Max(0, plan.Cuts.Count - 1) * options.Kerf;
                if (consumed > plan.StockLength)
                    return $"Plan {planIndex + 1} consumes {consumed}mm from {plan.StockLength}mm stock.";

                int expectedLeftover = plan.StockLength - consumed;
                if (plan.Leftover != expectedLeftover)
                    return $"Plan {planIndex + 1} leftover is {plan.Leftover}mm, expected {expectedLeftover}mm.";

                foreach (var cut in plan.Cuts)
                {
                    if (cut.WeldGroupId.HasValue)
                        continue;

                    if (cut.RequiresWelding)
                        return $"Plan {planIndex + 1} has a welding cut without a weld group.";

                    if (!demandByLength.ContainsKey(cut.Length))
                        return $"Plan {planIndex + 1} produces unknown cut length {cut.Length}mm.";

                    producedByLength.TryGetValue(cut.Length, out int produced);
                    producedByLength[cut.Length] = produced + 1;
                }
            }

            foreach (var group in result.CuttingPlans
                         .SelectMany(p => p.Cuts)
                         .Where(c => c.WeldGroupId.HasValue)
                         .GroupBy(c => c.WeldGroupId!.Value))
            {
                if (group.Count() < 2)
                    return $"Weld group {group.Key} has fewer than two pieces.";

                if (group.Any(c => c.Length < options.Delta))
                    return $"Weld group {group.Key} contains a piece shorter than Delta {options.Delta}mm.";

                int weldedLength = group.Sum(c => c.Length);
                if (!demandByLength.ContainsKey(weldedLength))
                    return $"Weld group {group.Key} produces unknown order length {weldedLength}mm.";

                producedByLength.TryGetValue(weldedLength, out int produced);
                producedByLength[weldedLength] = produced + 1;
            }

            foreach (var (length, demand) in demandByLength)
            {
                producedByLength.TryGetValue(length, out int produced);
                if (produced != demand)
                    return $"Cut length {length}mm produced {produced}, expected {demand}.";
            }

            foreach (var (length, produced) in producedByLength)
            {
                if (!demandByLength.ContainsKey(length))
                    return $"Cut length {length}mm produced {produced} but is not in demand.";
            }

            return null;
        }

        private static List<int> ComputeAvailableReusableLeftovers(
            List<CuttingPlan> plans,
            int gamma)
        {
            var consumedSourcePlanIndexes = plans
                .Where(plan => plan.ReusableLeftoverSourcePlanIndex.HasValue)
                .Select(plan => plan.ReusableLeftoverSourcePlanIndex!.Value)
                .ToHashSet();

            return plans
                .Select((plan, index) => (plan, index))
                .Where(entry =>
                    entry.plan.Leftover >= gamma &&
                    !consumedSourcePlanIndexes.Contains(entry.index))
                .Select(entry => entry.plan.Leftover)
                .ToList();
        }

        private static void RecomputeLeftovers(SolverResult result, SolverOptions options)
        {
            foreach (var plan in result.CuttingPlans)
            {
                plan.Leftover = SolverUtils.ComputeLeftover(plan.StockLength, plan.Cuts, options.Kerf);
            }
        }
    }
}
