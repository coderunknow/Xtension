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
        /// <summary>No supported browser is the foreground window, so nothing is scanned for the overlay.</summary>
        BrowserNotForeground,
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
        Stalled,
        /// <summary>
        /// The window belongs to a browser whose accessibility contract this build does not implement, so its tab
        /// strip is never read and its selected tab is never guessed.
        /// </summary>
        ContractUnimplemented
    }

    /// <summary>
    /// Which localized sentence describes a scan result. The result stores this key, never translated text, so a
    /// language change is visible immediately and cached state can never hold a stale translation.
    /// </summary>
    internal enum ScanMessageKey
    {
        /// <summary>No description has been assigned yet.</summary>
        None,
        Ready,
        Incomplete,
        NoTabs,
        NoSelectedTab,
        AmbiguousSelection,
        UnknownSelection,
        SelectedNotHorizontal,
        SelectedTabTooSmall,
        WindowBoundsUnavailable,
        WindowRootUnavailable,
        ScanFailed,
        WorkerFailed,
        BrowserUnimplemented
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

    /// <summary>Result of one bounded scan of one browser window.</summary>
    internal sealed class ScanResult
    {
        public ScanOutcome Outcome = ScanOutcome.Idle;
        /// <summary>Localized sentence describing this result. Derived on demand from <see cref="Key"/>.</summary>
        public ScanMessageKey Key = ScanMessageKey.None;
        /// <summary>Top-level browser window this scan examined.</summary>
        public IntPtr Window = IntPtr.Zero;
        public int Dpi = TabGeometry.BaseDpi;
        public Rectangle WindowBounds;
        public readonly List<TabInfo> Tabs = new List<TabInfo>();
        public TabInfo SelectedHorizontalTab;
        public int NodesVisited;
        public long ElapsedMs;
        public string Error;
        /// <summary>Number of tabs that reported themselves as selected when the outcome was ambiguous.</summary>
        public int AmbiguousCount;
        private string _messageOverride;

        /// <summary>
        /// Localized description. Normally derived from <see cref="Key"/> and the counters; test fixtures may set
        /// an explicit text, which is also what the dashboard and diagnostics show for such a result.
        /// </summary>
        public string Message
        {
            get { return _messageOverride != null ? _messageOverride : Strings.Describe(this); }
            set { _messageOverride = value; }
        }

        public static ScanResult Idle(IntPtr window, ScanOutcome outcome, ScanMessageKey key)
        {
            ScanResult result = new ScanResult();
            result.Window = window;
            result.Outcome = outcome;
            result.Key = key;
            return result;
        }
    }
}
