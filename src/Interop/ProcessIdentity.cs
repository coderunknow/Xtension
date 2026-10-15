using System;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>
    /// Decides whether a top-level window belongs to brave.exe. The verdict is cached per window and process id,
    /// for positive results and for negative results, so the per-tick foreground check does not open a process
    /// handle on every call. Negative verdicts expire sooner, because a transient failure must heal quickly.
    /// A caller that is about to send Ctrl+W passes fresh = true and always re-queries the process.
    /// </summary>
    internal static class ProcessIdentity
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const string BraveExecutable = "brave.exe";
        private const long PositiveCacheLifetimeMs = 5000;
        private const long NegativeCacheLifetimeMs = 1000;

        private static readonly object Gate = new object();
        private static IntPtr _cachedWindow = IntPtr.Zero;
        private static bool _cachedVerdict;
        private static uint _cachedProcessId;
        private static long _cachedAtMs = Clock.Never;

        public static bool IsBraveWindow(IntPtr window)
        {
            return IsBraveWindow(window, false);
        }

        public static bool IsBraveWindow(IntPtr window, bool fresh)
        {
            if (window == IntPtr.Zero) return false;
            uint processId;
            if (NativeMethods.GetWindowThreadProcessId(window, out processId) == 0 || processId == 0) return false;

            if (!fresh)
            {
                bool cached;
                if (TryReadCache(window, processId, out cached)) return cached;
            }

            bool verdict = Query(processId);
            lock (Gate)
            {
                _cachedWindow = window;
                _cachedProcessId = processId;
                _cachedVerdict = verdict;
                _cachedAtMs = Clock.NowMs;
            }
            return verdict;
        }

        private static bool TryReadCache(IntPtr window, uint processId, out bool verdict)
        {
            verdict = false;
            lock (Gate)
            {
                if (window != _cachedWindow || processId != _cachedProcessId) return false;
                long lifetime = _cachedVerdict ? PositiveCacheLifetimeMs : NegativeCacheLifetimeMs;
                if (Clock.HasElapsed(_cachedAtMs, lifetime)) return false;
                verdict = _cachedVerdict;
                return true;
            }
        }

        private static bool Query(uint processId)
        {
            IntPtr process = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return false;
            try
            {
                StringBuilder path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;
                if (!NativeMethods.QueryFullProcessImageName(process, 0, path, ref size)) return false;
                return string.Equals(Path.GetFileName(path.ToString()), BraveExecutable, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }
    }
}
