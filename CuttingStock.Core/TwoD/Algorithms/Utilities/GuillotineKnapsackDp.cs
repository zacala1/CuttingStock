using System;
using System.Collections.Generic;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>
    /// Unbounded 2D guillotine knapsack on a normal-cut grid (Beasley 1985).
    /// Used as the CG pricing sub-problem. DP on |X| x |Y| normal sets with
    /// NaN-sentinel memoization and full placement reconstruction.
    /// </summary>
    public sealed class GuillotineKnapsackDp
    {
        public sealed class Item
        {
            public int OrderIndex;
            public int W, H;
            public bool Rotated;
            /// <summary>Dual price (CG) or synthetic weight (diversification).</summary>
            public double Profit;
        }

        public sealed class Result
        {
            public double Profit;
            public List<Placement> Placements = new();
        }

        private readonly int _W, _H;
        private readonly List<Item> _items;
        private readonly int _kerf;

        private readonly int[] _xs, _ys;
        private readonly Dictionary<int, int> _xi = new(), _yi = new();

        // NaN = not yet computed. Tag: 0=base, 1=item, 2=vcut, 3=hcut. Data: item idx or cut pos.
        private readonly double[,] _memo;
        private readonly byte[,] _tag;
        private readonly int[,] _data;

        /// <summary>Construct a solver for one rectangle / item set.</summary>
        public GuillotineKnapsackDp(int width, int height, List<Item> items, int kerf = 0)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
            _W = width;
            _H = height;
            _items = items;
            _kerf = kerf;

            _xs = BuildNormalSet(width, items, axis: 0);
            _ys = BuildNormalSet(height, items, axis: 1);
            for (int i = 0; i < _xs.Length; i++) _xi[_xs[i]] = i;
            for (int i = 0; i < _ys.Length; i++) _yi[_ys[i]] = i;

            _memo = new double[_xs.Length, _ys.Length];
            _tag  = new byte[_xs.Length, _ys.Length];
            _data = new int[_xs.Length, _ys.Length];
            // Initialize memo to NaN sentinel.
            for (int a = 0; a < _xs.Length; a++)
                for (int b = 0; b < _ys.Length; b++)
                    _memo[a, b] = double.NaN;
        }

        /// <summary>Solve the unbounded 2D guillotine knapsack.</summary>
        public Result Solve()
        {
            int xiMax = _xs.Length - 1;
            int yiMax = _ys.Length - 1;
            double best = F(xiMax, yiMax);

            var result = new Result { Profit = best };
            Reconstruct(0, 0, xiMax, yiMax, result.Placements);
            return result;
        }

        // ---------- recurrence ----------

        private double F(int xi, int yi)
        {
            double cached = _memo[xi, yi];
            if (!double.IsNaN(cached)) return cached;
            int W = _xs[xi];
            int H = _ys[yi];

            double best = 0.0;
            byte bestTag = 0;
            int bestData = 0;

            // (a) Single item.
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.W <= W && it.H <= H && it.Profit > best)
                {
                    best = it.Profit;
                    bestTag = 1;
                    bestData = i;
                }
            }

            // Vertical cuts. Only x <= W/2 — F(a,H)+F(b,H) == F(b,H)+F(a,H).
            for (int k = 1; k < xi; k++)
            {
                int x = _xs[k];
                if (x > W / 2) break;
                int rest = W - x - _kerf;
                if (rest <= 0) continue;
                int restIdx = LowerBoundIndex(_xs, rest);
                if (restIdx < 0) continue;
                double val = F(k, yi) + F(restIdx, yi);
                if (val > best)
                {
                    best = val;
                    bestTag = 2;
                    bestData = x;
                }
            }

            // Horizontal cuts. Same symmetry.
            for (int k = 1; k < yi; k++)
            {
                int y = _ys[k];
                if (y > H / 2) break;
                int rest = H - y - _kerf;
                if (rest <= 0) continue;
                int restIdx = LowerBoundIndex(_ys, rest);
                if (restIdx < 0) continue;
                double val = F(xi, k) + F(xi, restIdx);
                if (val > best)
                {
                    best = val;
                    bestTag = 3;
                    bestData = y;
                }
            }

            _memo[xi, yi] = best;
            _tag[xi, yi]  = bestTag;
            _data[xi, yi] = bestData;
            return best;
        }

        // ---------- reconstruction ----------

        private void Reconstruct(int x0, int y0, int xi, int yi, List<Placement> outList)
        {
            // Make sure F has been computed.
            if (double.IsNaN(_memo[xi, yi])) F(xi, yi);
            byte tag = _tag[xi, yi];
            if (tag == 0) return;

            int W = _xs[xi];
            int H = _ys[yi];

            if (tag == 1)
            {
                int idx = _data[xi, yi];
                var it = _items[idx];
                outList.Add(new Placement
                {
                    OrderIndex = it.OrderIndex,
                    X = x0,
                    Y = y0,
                    Width = it.W,
                    Height = it.H,
                    Rotated = it.Rotated,
                });
                return;
            }

            if (tag == 2) // vertical cut at x
            {
                int x = _data[xi, yi];
                int leftIdx = _xi[x];
                int rest = W - x - _kerf;
                int restIdx = LowerBoundIndex(_xs, rest);
                Reconstruct(x0, y0, leftIdx, yi, outList);
                if (restIdx >= 0) Reconstruct(x0 + x + _kerf, y0, restIdx, yi, outList);
                return;
            }

            // tag == 3 — horizontal cut at y
            {
                int y = _data[xi, yi];
                int topIdx = _yi[y];
                int rest = H - y - _kerf;
                int restIdx = LowerBoundIndex(_ys, rest);
                Reconstruct(x0, y0, xi, topIdx, outList);
                if (restIdx >= 0) Reconstruct(x0, y0 + y + _kerf, xi, restIdx, outList);
            }
        }

        // ---------- normal sets ----------

        /// <summary>
        /// Build the normal set along axis 0 (widths) or axis 1 (heights):
        /// all sums of item widths/heights not exceeding the bound, plus 0 and the bound
        /// itself. Normal sets reduce the DP from O(W·H) to O(|X|·|Y|) without loss
        /// of optimality (Christofides &amp; Whitlock 1977; Beasley 1985).
        /// </summary>
        private static int[] BuildNormalSet(int bound, List<Item> items, int axis)
        {
            // Standard subset-sum closure: start with {0} and, for each item dimension s,
            // extend the set by all reachable s, 2s, 3s, ... not exceeding `bound`.
            // Final set is union over all such closures, capped by `bound`.
            var reachable = new HashSet<int> { 0, bound };
            foreach (var it in items)
            {
                int s = axis == 0 ? it.W : it.H;
                if (s <= 0 || s > bound) continue;
                var snapshot = reachable.ToArray();   // freeze prior values
                foreach (var v in snapshot)
                {
                    int nv = v + s;
                    while (nv <= bound)
                    {
                        reachable.Add(nv);            // duplicates are silently ignored
                        nv += s;
                    }
                }
            }
            var arr = new int[reachable.Count];
            reachable.CopyTo(arr);
            Array.Sort(arr);
            return arr;
        }

        /// <summary>Index of the largest array entry ≤ value, or -1.</summary>
        private static int LowerBoundIndex(int[] arr, int value)
        {
            int lo = 0, hi = arr.Length - 1, ans = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (arr[mid] <= value) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans;
        }
    }
}
