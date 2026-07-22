using System;
using System.Diagnostics;

namespace CuttingStock.Core.TwoD.Algorithms.Utilities
{
    /// <summary>Absolute wall-clock deadline measured from a solver's start stopwatch.</summary>
    public readonly struct TwoDDeadline
    {
        private readonly Func<long> _elapsedMilliseconds;

        private TwoDDeadline(long totalMilliseconds, Func<long> elapsedMilliseconds)
        {
            if (totalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalMilliseconds));

            TotalMilliseconds = totalMilliseconds;
            _elapsedMilliseconds = elapsedMilliseconds;
        }

        public long TotalMilliseconds { get; }
        public long ElapsedMilliseconds => _elapsedMilliseconds();
        public long RemainingMilliseconds => Math.Max(0, TotalMilliseconds - ElapsedMilliseconds);
        public bool IsExpired => ElapsedMilliseconds > TotalMilliseconds;

        public static TwoDDeadline FromStopwatch(Stopwatch stopwatch, long totalMilliseconds)
        {
            return new TwoDDeadline(totalMilliseconds, () => stopwatch.ElapsedMilliseconds);
        }

        public static TwoDDeadline FromElapsedProvider(long totalMilliseconds, Func<long> elapsedMilliseconds)
        {
            return new TwoDDeadline(totalMilliseconds, elapsedMilliseconds);
        }

        public long PhaseEndMilliseconds(int numerator, int denominator)
        {
            if (denominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            if (numerator < 0 || numerator > denominator)
                throw new ArgumentOutOfRangeException(nameof(numerator));

            return TotalMilliseconds * numerator / denominator;
        }

        public bool IsPast(long absoluteMilliseconds)
        {
            return ElapsedMilliseconds > absoluteMilliseconds;
        }

        public bool HasLessThanReserve(long reserveMilliseconds)
        {
            if (reserveMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(reserveMilliseconds));

            return TotalMilliseconds - ElapsedMilliseconds < reserveMilliseconds;
        }

        public long RemainingMillisecondsWithFloor(long floorMilliseconds)
        {
            if (floorMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(floorMilliseconds));

            return Math.Max(floorMilliseconds, TotalMilliseconds - ElapsedMilliseconds);
        }
    }
}
