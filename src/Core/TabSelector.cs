using System.Collections.Generic;
using System.Drawing;

namespace Xtension
{
    /// <summary>
    /// Decides, from the complete set of tabs one scan produced, whether exactly one tab is the confirmed active
    /// horizontal tab. Every doubt maps to a non-Ready outcome: the overlay is hidden rather than guessed.
    /// The result carries a <see cref="ScanMessageKey"/>, never localized text, so the explanation is rendered in
    /// the current language whenever it is displayed.
    /// </summary>
    internal static class TabSelector
    {
        public static void Apply(ScanResult result, Rectangle window, bool incomplete, int nodeCount, long elapsedMs,
            int sizeLogical, int insetLogical)
        {
            result.NodesVisited = nodeCount;
            result.ElapsedMs = elapsedMs;
            result.SelectedHorizontalTab = null;
            result.AmbiguousCount = 0;

            foreach (TabInfo tab in result.Tabs)
            {
                tab.IsHorizontalCandidate = !tab.IsOffscreen && TabGeometry.IsHorizontalCandidate(tab.Bounds, window, result.Dpi);
            }

            if (incomplete)
            {
                // A truncated walk may have skipped a second selected tab, so no selection is trustworthy.
                result.Outcome = ScanOutcome.ScanIncomplete;
                result.Key = ScanMessageKey.Incomplete;
                return;
            }

            if (result.Tabs.Count == 0)
            {
                result.Outcome = ScanOutcome.NoTabs;
                result.Key = ScanMessageKey.NoTabs;
                return;
            }

            List<TabInfo> selected = new List<TabInfo>();
            foreach (TabInfo tab in result.Tabs)
            {
                if (tab.SelectionKnown && tab.Selected) selected.Add(tab);
            }

            if (selected.Count == 0)
            {
                result.Outcome = ScanOutcome.NoSelectedTab;
                result.Key = ScanMessageKey.NoSelectedTab;
                return;
            }

            if (selected.Count > 1)
            {
                result.Outcome = ScanOutcome.AmbiguousSelection;
                result.Key = ScanMessageKey.AmbiguousSelection;
                result.AmbiguousCount = selected.Count;
                return;
            }

            foreach (TabInfo tab in result.Tabs)
            {
                if (!tab.SelectionKnown)
                {
                    result.Outcome = ScanOutcome.NoSelectedTab;
                    result.Key = ScanMessageKey.UnknownSelection;
                    return;
                }
            }

            TabInfo only = selected[0];
            if (!only.IsHorizontalCandidate)
            {
                result.Outcome = ScanOutcome.NoSelectedTab;
                result.Key = ScanMessageKey.SelectedNotHorizontal;
                return;
            }

            // The whole button, including its inset, must fit inside the tab at the configured size.
            if (TabGeometry.GetOverlayBounds(only.Bounds, result.Dpi, sizeLogical, insetLogical).IsEmpty)
            {
                result.Outcome = ScanOutcome.NoSelectedTab;
                result.Key = ScanMessageKey.SelectedTabTooSmall;
                return;
            }

            result.Outcome = ScanOutcome.Ready;
            result.Key = ScanMessageKey.Ready;
            result.SelectedHorizontalTab = only;
        }
    }
}
