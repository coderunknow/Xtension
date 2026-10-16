using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>
    /// Decides which browser, if any, owns a top-level window. The verdict comes from the process image name and
    /// nothing else: a window title, a class name or an unverified substring is never enough to treat a window as a
    /// browser. The verdict is cached per window and process id, for positive and for negative results, so the
    /// per-tick foreground check does not open a process handle on every call. Negative verdicts expire sooner,
    /// because a transient failure must heal quickly. A caller that is about to send Ctrl+W passes fresh = true and
    /// always re-queries the process.
    /// </summary>
    internal static class ProcessIdentity
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const long PositiveCacheLifetimeMs = 5000;
        private const long NegativeCacheLifetimeMs = 1000;

        private static readonly object Gate = new object();
        private static IntPtr _cachedWindow = IntPtr.Zero;
        private static BrowserKind _cachedVerdict;
        private static uint _cachedProcessId;
        private static long _cachedAtMs = Clock.Never;
        private static uint _ownProcessId;

        /// <summary>Browser that owns the window, or <see cref="BrowserKind.None"/> for anything else.</summary>
        public static BrowserKind GetBrowser(IntPtr window)
        {
            return GetBrowser(window, false);
        }

        /// <summary>
        /// Browser that owns the window, or None. With fresh = true the process is queried again even when a cached
        /// verdict exists; the close path uses that so a decision never rests on a stale identity.
        /// </summary>
        public static BrowserKind GetBrowser(IntPtr window, bool fresh)
        {
            if (window == IntPtr.Zero) return BrowserKind.None;
            uint processId;
            if (NativeMethods.GetWindowThreadProcessId(window, out processId) == 0 || processId == 0) return BrowserKind.None;

            if (!fresh)
            {
                BrowserKind cached;
                if (TryReadCache(window, processId, out cached)) return cached;
            }

            if (processId == OwnProcessId) return BrowserKind.None;
            BrowserKind verdict = Query(processId);
            lock (Gate)
            {
                _cachedWindow = window;
                _cachedProcessId = processId;
                _cachedVerdict = verdict;
                _cachedAtMs = Clock.NowMs;
            }
            return verdict;
        }

        /// <summary>
        /// True when the window belongs to a browser this build can confirm a selected tab in. Whether the user
        /// enabled that browser is a separate question, decided by the settings, so the two cannot be confused.
        /// </summary>
        public static bool IsSupportedBrowser(IntPtr window)
        {
            return BrowserCatalog.IsSupported(GetBrowser(window));
        }

        public static bool IsSupportedBrowser(IntPtr window, bool fresh)
        {
            return BrowserCatalog.IsSupported(GetBrowser(window, fresh));
        }

        /// <summary>
        /// Catalog entry for the window's browser, or null when the window is not one of the browsers this build
        /// knows. Callers use this to hand the scanner an explicit access contract instead of letting the scanner
        /// guess what kind of window it was given. With fresh = true the process is queried again.
        /// </summary>
        public static BrowserDescriptor Descriptor(IntPtr window, bool fresh)
        {
            BrowserDescriptor descriptor;
            if (!BrowserCatalog.TryGet(GetBrowser(window, fresh), out descriptor)) return null;
            return descriptor;
        }

        /// <summary>True when the window belongs to this process. Used to recognise Xtension's own windows.</summary>
        public static bool IsOwnWindow(IntPtr window)
        {
            if (window == IntPtr.Zero) return false;
            uint processId;
            if (NativeMethods.GetWindowThreadProcessId(window, out processId) == 0) return false;
            return processId == OwnProcessId;
        }

        private static uint OwnProcessId
        {
            get
            {
                if (_ownProcessId == 0)
                {
                    try
                    {
                        _ownProcessId = (uint)Process.GetCurrentProcess().Id;
                    }
                    catch (Exception ex)
                    {
                        // A failure here must not make every window look like ours or like a browser: fall back to a
                        // value that matches nothing, so identity stays fail-closed.
                        AppLog.WarnThrottled("own-pid", "The process id could not be read: " + ex.GetType().Name);
                        _ownProcessId = uint.MaxValue;
                    }
                }
                return _ownProcessId;
            }
        }

        private static bool TryReadCache(IntPtr window, uint processId, out BrowserKind verdict)
        {
            verdict = BrowserKind.None;
            lock (Gate)
            {
                if (window != _cachedWindow || processId != _cachedProcessId) return false;
                long lifetime = _cachedVerdict == BrowserKind.None ? NegativeCacheLifetimeMs : PositiveCacheLifetimeMs;
                if (Clock.HasElapsed(_cachedAtMs, lifetime)) return false;
                verdict = _cachedVerdict;
                return true;
            }
        }

        private static BrowserKind Query(uint processId)
        {
            IntPtr process = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return BrowserKind.None;
            try
            {
                StringBuilder path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;
                if (!NativeMethods.QueryFullProcessImageName(process, 0, path, ref size)) return BrowserKind.None;
                BrowserDescriptor descriptor = BrowserCatalog.ForExecutable(Path.GetFileName(path.ToString()));
                return descriptor == null ? BrowserKind.None : descriptor.Kind;
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }
    }
}
