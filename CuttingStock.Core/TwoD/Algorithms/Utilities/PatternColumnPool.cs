using System.Collections.Generic;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Identity and deduplication policy for generated pattern columns.</summary>
    internal static class PatternColumnPool
    {
        /// <summary>
        /// FNV-1a fingerprint of sheet dimensions and item counts. Sheet quantity and
        /// placements are intentionally excluded because the master sees only this identity.
        /// </summary>
        public static long Signature(PatternColumn column)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                const long prime = 1099511628211L;
                hash = (hash ^ column.Sheet.Width) * prime;
                hash = (hash ^ column.Sheet.Height) * prime;
                for (int i = 0; i < column.Counts.Length; i++)
                    hash = (hash ^ column.Counts[i]) * prime;
                return hash;
            }
        }

        public static bool AddIfNew(
            List<PatternColumn> columns,
            HashSet<long> signatures,
            PatternColumn candidate)
        {
            long signature = Signature(candidate);
            if (!signatures.Add(signature)) return false;

            columns.Add(candidate);
            return true;
        }
    }
}
