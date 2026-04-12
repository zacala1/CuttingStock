using System;
using CuttingStock.Core.Domain;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>
    /// Configuration for 2D guillotine cutting solvers.
    /// </summary>
    public class SolverOptions2D
    {
        private int _kerf;
        private int _trim;
        private float _alphaArea = 1f;
        private int _stage = 2;
        private int _timeLimitMs = 30000;

        /// <summary>
        /// Saw-blade kerf in mm. Each guillotine cut consumes this much material on the cut side.
        /// Must be non-negative.
        /// </summary>
        public int Kerf
        {
            get => _kerf;
            set => _kerf = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Kerf), "Kerf must be non-negative.");
        }

        /// <summary>
        /// Edge-trim removed from each side of every sheet before any cut (mm).
        /// Effective usable area becomes (Width-2·Trim) × (Height-2·Trim). Must be non-negative.
        /// </summary>
        public int Trim
        {
            get => _trim;
            set => _trim = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Trim), "Trim must be non-negative.");
        }

        /// <summary>
        /// Cost per 1 mm² of waste area. Used by <see cref="SolverResult2D.TotalCost"/>.
        /// </summary>
        public float AlphaArea
        {
            get => _alphaArea;
            set => _alphaArea = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(AlphaArea), "AlphaArea must be non-negative.");
        }

        /// <summary>
        /// Global default for whether items may be rotated 90°.
        /// Per-item <see cref="Models.RectOrder.AllowRotation"/> must also be true for an item to be rotated.
        /// </summary>
        public bool AllowRotation { get; set; } = true;

        /// <summary>
        /// Number of guillotine stages to enforce. 2-stage = strip-then-item;
        /// 3-stage = strip-then-item-then-trim. Industry panel saws are usually 2- or 3-stage.
        /// Allowed: 2 or 3.
        /// </summary>
        public int Stage
        {
            get => _stage;
            set
            {
                if (value != 2 && value != 3) throw new ArgumentOutOfRangeException(nameof(Stage), "Stage must be 2 or 3.");
                _stage = value;
            }
        }

        /// <summary>
        /// Time limit for exact / column-generation solvers in milliseconds.
        /// </summary>
        public int TimeLimitMs
        {
            get => _timeLimitMs;
            set => _timeLimitMs = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(TimeLimitMs), "TimeLimitMs must be > 0.");
        }

        /// <summary>
        /// Order in which sheet sizes are consumed (small-first vs large-first).
        /// Reuses the 1D enum for consistency.
        /// </summary>
        public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.LargeToSmall;
    }
}
