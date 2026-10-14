using System;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>
    /// Decides whether a top-level window belongs to brave.exe. Caches the verdict for the last window, so the
    /// per-tick foreground check does not open a process handle on every call.
    /// </summary>
    internal static class ProcessIdentity
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const string BraveExecutable = "brave.exe";
        private const long CacheLifetimeMs = 5000;

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
            lock (Gate)
            {
                if (!fresh && _cachedVerdict && window == _cachedWindow && processId == _cachedProcessId && !Clock.HasElapsed(_cachedAtMs, CacheLifetimeMs)) return _cachedVerdict;
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
