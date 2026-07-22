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

            result.ReusableLeftovers = result.CuttingPlans
                .Where(p => p.Leftover >= options.Gamma)
                .Select(p => p.Leftover)
                .ToList();

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

            for (int planIndex = 0; planIndex < result.CuttingPlans.Count; planIndex++)
            {
                var plan = result.CuttingPlans[planIndex];
                if (stockByLength.ContainsKey(plan.StockLength))
                {
                    usedStockByLength.TryGetValue(plan.StockLength, out int used);
                    usedStockByLength[plan.StockLength] = used + 1;
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

            foreach (var (length, used) in usedStockByLength)
            {
                if (used > stockByLength[length])
                    return $"Stock {length}mm usage {used} exceeds inventory {stockByLength[length]}.";
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

        private static void RecomputeLeftovers(SolverResult result, SolverOptions options)
        {
            foreach (var plan in result.CuttingPlans)
            {
                plan.Leftover = SolverUtils.ComputeLeftover(plan.StockLength, plan.Cuts, options.Kerf);
            }
        }
    }
}
