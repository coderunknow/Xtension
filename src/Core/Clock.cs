using System.Diagnostics;

namespace Xtension
{
    /// <summary>Monotonic millisecond clock. Immune to wall-clock changes and 24.8-day TickCount wrap.</summary>
    internal static class Clock
    {
        /// <summary>Sentinel for "no event has happened yet". Far enough from zero that subtraction cannot overflow.</summary>
        public const long Never = long.MinValue / 4;

        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        public static long NowMs
        {
            get { return Watch.ElapsedMilliseconds; }
        }

        /// <summary>True when the event at <paramref name="sinceMs"/> is at least <paramref name="durationMs"/> old, or never happened.</summary>
        public static bool HasElapsed(long sinceMs, long durationMs)
        {
            return sinceMs == Never || NowMs - sinceMs >= durationMs;
        }
    }
}
