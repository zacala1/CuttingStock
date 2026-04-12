using System;
using CuttingStock.Core.Domain;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>Configuration for 2D guillotine cutting solvers.</summary>
    public class SolverOptions2D
    {
        private int _kerf;
        private int _trim;
        private float _alphaArea = 1f;
        private int _stage = 2;
        private int _timeLimitMs = 30000;

        /// <summary>Blade kerf (mm). Each guillotine cut consumes this much material.</summary>
        public int Kerf
        {
            get => _kerf;
            set => _kerf = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Kerf));
        }

        /// <summary>Edge trim (mm) removed from each side of every sheet before cutting.</summary>
        public int Trim
        {
            get => _trim;
            set => _trim = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Trim));
        }

        /// <summary>Cost per mm² of waste. Drives <see cref="SolverResult2D.TotalCost"/>.</summary>
        public float AlphaArea
        {
            get => _alphaArea;
            set => _alphaArea = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(AlphaArea));
        }

        /// <summary>Global 90° rotation toggle. Per-item flag must also be true.</summary>
        public bool AllowRotation { get; set; } = true;

        /// <summary>Guillotine stage count (2 or 3). Industry panel saws are typically 2-stage.</summary>
        public int Stage
        {
            get => _stage;
            set
            {
                if (value != 2 && value != 3) throw new ArgumentOutOfRangeException(nameof(Stage));
                _stage = value;
            }
        }

        /// <summary>Wall-clock time limit for CG / MIP solvers (ms).</summary>
        public int TimeLimitMs
        {
            get => _timeLimitMs;
            set => _timeLimitMs = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(TimeLimitMs));
        }

        public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.LargeToSmall;
    }
}
