using System;
using System.Collections.Generic;
using System.Drawing;

namespace Xtension
{
    /// <summary>Why a scan did or did not yield a confirmed active tab. Drives UI text and logging, never string matching.</summary>
    internal enum ScanOutcome
    {
        /// <summary>No scan has run for the current state.</summary>
        Idle,
        /// <summary>Brave is not the foreground window, so nothing is scanned for the overlay.</summary>
        BraveNotForeground,
        /// <summary>The window has no usable bounds or UI Automation returned no root element.</summary>
        WindowUnavailable,
        /// <summary>The scan hit a budget or lost an element mid-walk, so the tab set is incomplete.</summary>
        ScanIncomplete,
        /// <summary>UI Automation threw an unexpected error.</summary>
        Error,
        /// <summary>The scan completed but the tab strip contains no TabItem elements.</summary>
        NoTabs,
        /// <summary>Tabs exist, but no single selected tab is confirmed in the horizontal strip.</summary>
        NoSelectedTab,
        /// <summary>More than one tab reports itself as selected.</summary>
        AmbiguousSelection,
        /// <summary>Exactly one selected horizontal tab was confirmed. The only outcome that may show the overlay.</summary>
        Ready,
        /// <summary>A UI Automation call has not returned for longer than the stall threshold.</summary>
        Stalled
    }

    /// <summary>One TabItem read from UI Automation. Name is for on-screen diagnostics only.</summary>
    internal sealed class TabInfo
    {
        public string Name = string.Empty;
        public Rectangle Bounds = Rectangle.Empty;
        public bool SelectionKnown;
        public bool Selected;
        public bool IsOffscreen;
        public bool IsHorizontalCandidate;
        public int Depth;
    }

    /// <summary>Result of one bounded scan of a Brave window.</summary>
    internal sealed class ScanResult
    {
        public ScanOutcome Outcome = ScanOutcome.Idle;
        public string Message = string.Empty;
        public IntPtr Window = IntPtr.Zero;
        public int Dpi = TabGeometry.BaseDpi;
        public string WindowTitle = string.Empty;
        public readonly List<TabInfo> Tabs = new List<TabInfo>();
        public TabInfo SelectedHorizontalTab;
        public int NodesVisited;
        public long ElapsedMs;
        public string Error;

        public static ScanResult Idle(IntPtr window, ScanOutcome outcome, string message)
        {
            ScanResult result = new ScanResult();
            result.Window = window;
            result.Outcome = outcome;
            result.Message = message;
            return result;
        }
    }
}
