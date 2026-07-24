using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Core.TwoD.Algorithms
{
    /// <summary>
    /// Shelf-based heuristic: NFDH/FFDH/BFDH x 5 sort orders, best-of-15 by waste.
    /// Produces shelf-shaped 2-stage layouts, while the Stage option remains advisory;
    /// TwoStageShelfGuillotineSolver owns explicit stage-contract enforcement.
    /// Ref: Coffman et al. 1980; Berkey &amp; Wang 1987.
    /// </summary>
    public sealed class ShelfGuillotineSolver : ICuttingSolver2D
    {
        public string Name => "Shelf Guillotine (NFDH/FFDH/BFDH)";
        public string Description => "Best-of-15 shelf heuristic with rotation.";
        public string TimeComplexity => "O(K * N log N)";

        /// <inheritdoc />
        public SolverResult2D Solve(
            List<Sheet> sheets,
            List<RectOrder> orders,
            SolverOptions2D options,
            IProgress<double>? progress = null)
        {
            var sw = Stopwatch.StartNew();
            var result = new SolverResult2D { AlgorithmName = Name };

            try
            {
                var input = TwoDInputPreprocessor.Preprocess(sheets, orders, result);
                if (input.ShouldReturn)
                {
                    TwoDResultFinalizer.FinalizeAndValidate(input.Sheets, input.Orders, options, result);
                    sw.Stop();
                    result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                    return result;
                }

                var orderedSheets = TwoDInputPreprocessor.OrderSheets(input.Sheets, options);
                var items = TwoDInputPreprocessor.ExpandOrders(input.Orders, options.AllowRotation);

                // Try every (order rule × shelf strategy) combo and keep the best.
                var orderingRules = new (string Name, Func<List<Item>, List<Item>> Sort)[]
                {
                    ("DecH",    xs => xs.OrderByDescending(i => i.H).ThenByDescending(i => i.W).ToList()),
                    ("DecW",    xs => xs.OrderByDescending(i => i.W).ThenByDescending(i => i.H).ToList()),
                    ("DecArea", xs => xs.OrderByDescending(i => (long)i.W * i.H).ToList()),
                    ("DecPeri", xs => xs.OrderByDescending(i => i.W + i.H).ToList()),
                    ("DecLong", xs => xs.OrderByDescending(i => Math.Max(i.W, i.H)).ToList()),
                };
                var strategies = new (string Name, ShelfStrategy S)[]
                {
                    ("NFDH", ShelfStrategy.NextFit),
                    ("FFDH", ShelfStrategy.FirstFit),
                    ("BFDH", ShelfStrategy.BestFit),
                };

                List<CuttingPattern2D>? bestPatterns = null;
                long bestWaste = long.MaxValue;
                int totalCombos = orderingRules.Length * strategies.Length;
                int comboIdx = 0;

                foreach (var rule in orderingRules)
                {
                    foreach (var strat in strategies)
                    {
                        var rotated = NormalizeOrientation(items);
                        var sorted  = rule.Sort(rotated);
                        var patterns = PackAll(sorted, orderedSheets, options, strat.S);
                        if (patterns == null) { comboIdx++; continue; }

                        long waste = patterns.Sum(p => p.WasteArea * p.Multiplicity);
                        if (waste < bestWaste)
                        {
                            bestWaste = waste;
                            bestPatterns = patterns;
                        }
                        comboIdx++;
                        progress?.Report((double)comboIdx / totalCombos);
                    }
                }

                if (bestPatterns == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "No item fits in any provided sheet.";
                }
                else
                {
                    result.Patterns = bestPatterns;
                    TwoDResultFinalizer.FinalizeAndValidate(input.Sheets, input.Orders, options, result);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            TwoDResultFinalizer.FinalizeResult(result, options);
            return result;
        }

        // ---------- internal model ----------

        private enum ShelfStrategy { NextFit, FirstFit, BestFit }

        private sealed class Item
        {
            public int OrderIndex;
            public int W, H;
            public bool AllowRot;
            public bool Rotated;
        }

        private static List<Item> NormalizeOrientation(
            List<(int OrderIndex, int W, int H, bool Rot)> raw)
        {
            // Orient tall-side-up: shorter shelves → more shelves per sheet → less wasted height.
            var list = new List<Item>(raw.Count);
            foreach (var t in raw)
            {
                int w = t.W, h = t.H; bool rotated = false;
                if (t.Rot && w > h) { (w, h) = (h, w); rotated = true; }
                list.Add(new Item { OrderIndex = t.OrderIndex, W = w, H = h, AllowRot = t.Rot, Rotated = rotated });
            }
            return list;
        }

        private static List<CuttingPattern2D>? PackAll(
            List<Item> items,
            List<Sheet> orderedSheets,
            SolverOptions2D options,
            ShelfStrategy strategy)
        {
            var remaining = new LinkedList<Item>(items);
            var patterns = new List<CuttingPattern2D>();
            var stockLeft = orderedSheets.ToDictionary(s => s, s => s.Quantity);

            while (remaining.Count > 0)
            {
                Sheet? chosen = null;
                foreach (var s in orderedSheets)
                {
                    if (stockLeft[s] > 0)
                    {
                        // Quick feasibility: at least one remaining item must fit in this sheet.
                        if (AnyFits(remaining, s, options))
                        {
                            chosen = s;
                            break;
                        }
                    }
                }
                if (chosen == null)
                {
                    // No remaining item fits in any sheet — infeasible from here.
                    return null;
                }

                var pattern = PackOneSheet(chosen, remaining, options, strategy);
                if (pattern.Placements.Count == 0)
                {
                    // Should not happen because of AnyFits, but guard.
                    return null;
                }
                patterns.Add(pattern);
                stockLeft[chosen]--;
            }

            return patterns;
        }

        private static bool AnyFits(LinkedList<Item> items, Sheet sheet, SolverOptions2D options)
        {
            int W = sheet.Width  - 2 * options.Trim;
            int H = sheet.Height - 2 * options.Trim;
            if (W <= 0 || H <= 0) return false;
            foreach (var it in items)
            {
                if (it.W <= W && it.H <= H) return true;
                if (it.AllowRot && it.H <= W && it.W <= H) return true;
            }
            return false;
        }

        private static CuttingPattern2D PackOneSheet(
            Sheet sheet, LinkedList<Item> remaining, SolverOptions2D options, ShelfStrategy strategy)
        {
            int trim = options.Trim;
            int kerf = options.Kerf;
            int sheetW = sheet.Width  - 2 * trim;
            int sheetH = sheet.Height - 2 * trim;

            var shelves = new List<Shelf>();
            var placements = new List<Placement>();

            // For first-fit / best-fit we iterate items and try to drop each into existing shelves.
            // For next-fit we only try the most recent shelf. New shelves stack vertically.
            var node = remaining.First;
            while (node != null)
            {
                var next = node.Next;
                var it = node.Value;

                if (TryPlace(it, shelves, sheetW, sheetH, kerf, strategy, trim, out var pl))
                {
                    placements.Add(pl);
                    remaining.Remove(node);
                }

                node = next;
            }

            return new CuttingPattern2D
            {
                Sheet = sheet,
                Multiplicity = 1,
                Placements = placements,
            };
        }

        private static bool TryPlace(
            Item it, List<Shelf> shelves, int sheetW, int sheetH,
            int kerf, ShelfStrategy strategy, int trim,
            out Placement placement)
        {
            placement = null!;

            // Helper to compute current total height of opened shelves with kerfs between them.
            int UsedHeight()
            {
                if (shelves.Count == 0) return 0;
                int sum = 0;
                for (int i = 0; i < shelves.Count; i++)
                {
                    sum += shelves[i].Height;
                    if (i > 0) sum += kerf;
                }
                return sum;
            }

            // Try to place item into each shelf according to strategy.
            int? bestShelfIdx = null;
            int bestLeftover = int.MaxValue;
            int chosenW = it.W, chosenH = it.H; bool chosenRot = it.Rotated;

            // Score = wasted vertical gap + wasted horizontal gap.
            // Minimizing this combined metric prefers tight-fitting shelves.
            void Consider(int shelfIdx, int w, int h, bool rotApplied)
            {
                var sh = shelves[shelfIdx];
                int avail = sheetW - sh.UsedWidth - (sh.Items > 0 ? kerf : 0);
                if (w > avail || h > sh.Height) return;
                int leftover = (sh.Height - h) + (avail - w);
                if (leftover < bestLeftover)
                {
                    bestLeftover = leftover;
                    bestShelfIdx = shelfIdx;
                    chosenW = w; chosenH = h; chosenRot = rotApplied;
                }
            }

            for (int i = 0; i < shelves.Count; i++)
            {
                if (strategy == ShelfStrategy.NextFit && i != shelves.Count - 1) continue;
                Consider(i, it.W, it.H, it.Rotated);
                if (it.AllowRot && it.W != it.H) Consider(i, it.H, it.W, !it.Rotated);

                if (strategy == ShelfStrategy.FirstFit && bestShelfIdx.HasValue) break;
                if (strategy == ShelfStrategy.NextFit  && bestShelfIdx.HasValue) break;
            }

            if (bestShelfIdx.HasValue)
            {
                var sh = shelves[bestShelfIdx.Value];
                int xLocal = sh.UsedWidth + (sh.Items > 0 ? kerf : 0);
                placement = new Placement
                {
                    OrderIndex = it.OrderIndex,
                    X = trim + xLocal,
                    Y = trim + sh.Y,
                    Width = chosenW,
                    Height = chosenH,
                    Rotated = chosenRot,
                };
                sh.UsedWidth = xLocal + chosenW;
                sh.Items++;
                return true;
            }

            // Open a new shelf. Try both orientations (if rotation is allowed) and pick the
            // one that yields the smallest shelf height — this minimizes wasted vertical
            // space in subsequent shelves. If an orientation doesn't fit, discard it.
            int newShelfY = UsedHeight() + (shelves.Count > 0 ? kerf : 0);
            int availH = sheetH - newShelfY;
            if (availH <= 0) return false;

            int orientW = it.W, orientH = it.H;
            bool orientRot = it.Rotated;
            bool asIsFits    = it.W <= sheetW && it.H <= availH;
            bool rotatedFits = it.AllowRot && it.W != it.H && it.H <= sheetW && it.W <= availH;

            if (!asIsFits && !rotatedFits) return false;
            if (asIsFits && rotatedFits)
            {
                // Prefer the shorter shelf height (= smaller H after rotation choice).
                if (it.W < it.H)
                {
                    orientW = it.H; orientH = it.W; orientRot = !it.Rotated;
                }
            }
            else if (rotatedFits)
            {
                orientW = it.H; orientH = it.W; orientRot = !it.Rotated;
            }

            var newShelf = new Shelf { Y = newShelfY, Height = orientH, UsedWidth = orientW, Items = 1 };
            shelves.Add(newShelf);
            placement = new Placement
            {
                OrderIndex = it.OrderIndex,
                X = trim + 0,
                Y = trim + newShelfY,
                Width = orientW,
                Height = orientH,
                Rotated = orientRot,
            };
            return true;
        }

        private sealed class Shelf
        {
            public int Y;
            public int Height;
            public int UsedWidth;
            public int Items;
        }
    }
}
