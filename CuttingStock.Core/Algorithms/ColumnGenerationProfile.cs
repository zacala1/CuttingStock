using System;

namespace CuttingStock.Core.Algorithms
{
    internal sealed class ColumnGenerationProfile
    {
        internal static ColumnGenerationProfile Standard { get; } = new(
            name: "Column Generation (LP)",
            description: "CG with Simplex master + knapsack DP pricing.",
            useDualStabilization: false,
            dualSmoothingFactor: 1.0,
            maxColumnsPerIteration: 1,
            useIntegerMaster: false,
            integerMasterTimeLimitMs: 0);

        internal static ColumnGenerationProfile Stabilized { get; } = new(
            name: "Column Generation (Stabilized LP)",
            description: "CG with dual-smoothed knapsack pricing and raw-dual fallback.",
            useDualStabilization: true,
            dualSmoothingFactor: 0.70,
            maxColumnsPerIteration: 1,
            useIntegerMaster: false,
            integerMasterTimeLimitMs: 0);

        internal static ColumnGenerationProfile MultiColumn { get; } = new(
            name: "Column Generation (Multi-column LP)",
            description: "CG that adds multiple improving knapsack pricing columns per iteration.",
            useDualStabilization: false,
            dualSmoothingFactor: 1.0,
            maxColumnsPerIteration: 4,
            useIntegerMaster: false,
            integerMasterTimeLimitMs: 0);

        internal static ColumnGenerationProfile IntegerMaster { get; } = new(
            name: "Column Generation (Integer Master)",
            description: "CG with a generated-column CBC integer master polish.",
            useDualStabilization: false,
            dualSmoothingFactor: 1.0,
            maxColumnsPerIteration: 1,
            useIntegerMaster: true,
            integerMasterTimeLimitMs: 5000);

        internal ColumnGenerationProfile(
            string name,
            string description,
            bool useDualStabilization,
            double dualSmoothingFactor,
            int maxColumnsPerIteration,
            bool useIntegerMaster,
            long integerMasterTimeLimitMs)
        {
            if (dualSmoothingFactor <= 0.0 || dualSmoothingFactor > 1.0)
                throw new ArgumentOutOfRangeException(nameof(dualSmoothingFactor));
            if (maxColumnsPerIteration <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxColumnsPerIteration));
            if (integerMasterTimeLimitMs < 0)
                throw new ArgumentOutOfRangeException(nameof(integerMasterTimeLimitMs));

            Name = name;
            Description = description;
            UseDualStabilization = useDualStabilization;
            DualSmoothingFactor = dualSmoothingFactor;
            MaxColumnsPerIteration = maxColumnsPerIteration;
            UseIntegerMaster = useIntegerMaster;
            IntegerMasterTimeLimitMs = integerMasterTimeLimitMs;
        }

        public string Name { get; }
        public string Description { get; }
        public bool UseDualStabilization { get; }
        public double DualSmoothingFactor { get; }
        public int MaxColumnsPerIteration { get; }
        public bool UseIntegerMaster { get; }
        public long IntegerMasterTimeLimitMs { get; }
    }
}
