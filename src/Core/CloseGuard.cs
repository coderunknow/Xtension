using System;
using System.Drawing;

namespace Xtension
{
    /// <summary>Everything the close decision depends on, captured on the UI thread immediately before the keystroke.</summary>
    internal sealed class CloseRequest
    {
        public bool OverlayEnabled;
        /// <summary>False when any state change (foreground, enable, previous send) happened after the click.</summary>
        public bool EpochCurrent;
        /// <summary>The Brave window the validation scan examined.</summary>
        public IntPtr ScannedWindow;
        /// <summary>Foreground root window read again at decision time.</summary>
        public IntPtr ForegroundNow;
        public bool ForegroundIsBrave;
        public bool Minimized;
        /// <summary>True when Shift, Alt or Win is physically held. Ctrl+Shift+W would close the whole window.</summary>
        public bool BlockingModifierDown;
        public ScanResult Scan;
        /// <summary>Configured × size in logical pixels, so the validated area matches the button on screen.</summary>
        public int OverlaySizeLogical = AppInfo.DefaultButtonSizeLogical;
        /// <summary>Configured inset from the tab's right edge, in logical pixels.</summary>
        public int OverlayInsetLogical = AppInfo.DefaultButtonInsetLogical;
        /// <summary>Screen coordinates of the physical click on the overlay.</summary>
        public Point Click;
        public long NowMs;
        public long StartedMs = Clock.Never;
        /// <summary>Clock time of the last Ctrl+W, or the sentinel value that means "never".</summary>
        public long LastSentMs;
    }

    internal sealed class CloseDecision
    {
        public bool Allowed;
        public string Reason = string.Empty;

        public static CloseDecision Allow()
        {
            return new CloseDecision { Allowed = true, Reason = "confirmed" };
        }

        public static CloseDecision Reject(string reason)
        {
            return new CloseDecision { Allowed = false, Reason = reason };
        }
    }

    /// <summary>
    /// Final gate before Ctrl+W. Pure and conservative: the click must land on the × that a fresh scan
    /// places on the one confirmed selected horizontal tab, in the same foreground Brave window.
    /// </summary>
    internal static class CloseGuard
    {
        public static CloseDecision Evaluate(CloseRequest request)
        {
            if (!ScanSafety.IsFresh(request.StartedMs, request.NowMs, AppInfo.CloseValidationTimeoutMs))
                return CloseDecision.Reject("validation expired before delivery");
            if (!request.OverlayEnabled) return CloseDecision.Reject("overlay disabled");
            if (!request.EpochCurrent) return CloseDecision.Reject("window state changed during validation");
            if (request.LastSentMs != Clock.Never && request.NowMs - request.LastSentMs < AppInfo.CloseCooldownMs)
                return CloseDecision.Reject("cooldown after previous close");
            if (request.BlockingModifierDown) return CloseDecision.Reject("Shift, Alt or Win is held; Ctrl+W would not be safe");
            if (request.ForegroundNow == IntPtr.Zero) return CloseDecision.Reject("no foreground window");
            if (request.ForegroundNow != request.ScannedWindow) return CloseDecision.Reject("foreground window changed");
            if (!request.ForegroundIsBrave) return CloseDecision.Reject("foreground window is not Brave");
            if (request.Minimized) return CloseDecision.Reject("Brave is minimized");

            ScanResult scan = request.Scan;
            if (scan == null) return CloseDecision.Reject("no validation scan");
            if (scan.Window != request.ScannedWindow) return CloseDecision.Reject("scan belongs to another window");
            if (scan.Outcome != ScanOutcome.Ready || scan.SelectedHorizontalTab == null)
                return CloseDecision.Reject("active tab not confirmed (" + scan.Outcome + ")");

            Rectangle tab = scan.SelectedHorizontalTab.Bounds;
            Rectangle closeArea = TabGeometry.GetOverlayBounds(tab, scan.Dpi, request.OverlaySizeLogical, request.OverlayInsetLogical);
            if (closeArea.IsEmpty) return CloseDecision.Reject("confirmed tab too small for the close button");
            if (!closeArea.Contains(request.Click))
                return CloseDecision.Reject("click was outside the × of the confirmed active tab (stale position)");
            if (!tab.Contains(request.Click)) return CloseDecision.Reject("click was outside the confirmed active tab");

            return CloseDecision.Allow();
        }
    }
}
