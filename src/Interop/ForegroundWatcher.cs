using System;
using System.Runtime.InteropServices;

namespace Xtension
{
    /// <summary>
    /// Reports the three window events that can change which window is eligible for the ×, so the controller can scan
    /// immediately instead of waiting out the rest of a poll interval:
    /// the system foreground event, and the start and end of a minimize.
    ///
    /// This is a targeted, documented Windows notification for a state the controller already polls. It installs no
    /// input hook, injects no input, and its callback does nothing but raise an event on the thread that owns the
    /// message loop, so a slow handler cannot delay other applications' windows. Every failure is contained: if the
    /// hook cannot be installed, polling continues unchanged, which can only delay the × and never authorise it.
    /// </summary>
    internal sealed class ForegroundWatcher : IDisposable
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint EventSystemMinimizeStart = 0x0016;
        private const uint EventSystemMinimizeEnd = 0x0017;
        private const uint WineventOutOfContext = 0x0000;
        private const uint WineventSkipOwnProcess = 0x0002;

        private delegate void WinEventDelegate(IntPtr hook, uint eventId, IntPtr hwnd, int objectId, int childId,
            uint threadId, uint time);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventDelegate callback,
            uint processId, uint threadId, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        // Held in a field: the native hook keeps calling this delegate, so collecting it would crash the process.
        private readonly WinEventDelegate _callback;
        private IntPtr _hook = IntPtr.Zero;
        private volatile bool _disposed;
        private string _failure = string.Empty;

        /// <summary>Raised on the thread that installed the hook. The handler must be trivial.</summary>
        public event Action Changed;

        public ForegroundWatcher()
        {
            _callback = OnWinEvent;
            try
            {
                _hook = SetWinEventHook(EventSystemForeground, EventSystemMinimizeEnd, IntPtr.Zero, _callback, 0, 0,
                    WineventOutOfContext | WineventSkipOwnProcess);
                if (_hook == IntPtr.Zero)
                {
                    _failure = "SetWinEventHook returned no hook (Win32 error "
                        + Marshal.GetLastWin32Error() + "); polling is used instead";
                    AppLog.WarnThrottled("winevent", "Foreground notifications are unavailable; Xtension polls instead. " + _failure);
                }
            }
            catch (Exception ex)
            {
                _failure = ScanSafety.DescribeError("SetWinEventHook", ex);
                AppLog.WarnThrottled("winevent", "Foreground notifications are unavailable: " + _failure);
            }
        }

        public bool Registered
        {
            get { return _hook != IntPtr.Zero; }
        }

        public string Failure
        {
            get { return _failure; }
        }

        private void OnWinEvent(IntPtr hook, uint eventId, IntPtr hwnd, int objectId, int childId, uint threadId, uint time)
        {
            if (_disposed) return;
            if (eventId != EventSystemForeground && eventId != EventSystemMinimizeStart && eventId != EventSystemMinimizeEnd) return;
            if (eventId == EventSystemForeground && hwnd == IntPtr.Zero) return;
            Action handler = Changed;
            if (handler == null) return;
            try
            {
                handler();
            }
            catch (Exception)
            {
                // A notification must never escape into the window manager's callback path.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_hook != IntPtr.Zero)
            {
                try
                {
                    UnhookWinEvent(_hook);
                }
                catch (Exception ex)
                {
                    AppLog.WarnThrottled("winevent", "The foreground notification hook could not be removed: " + ex.GetType().Name);
                }
                _hook = IntPtr.Zero;
            }
        }
    }
}
