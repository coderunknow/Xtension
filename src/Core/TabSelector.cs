using System.Collections.Generic;
using System.Drawing;

namespace Xtension
{
    /// <summary>
    /// Decides, from the complete set of tabs one scan produced, whether exactly one tab is the confirmed active
    /// horizontal tab. Every doubt maps to a non-Ready outcome: the overlay is hidden rather than guessed.
    /// </summary>
    internal static class TabSelector
    {
        public static void Apply(ScanResult result, Rectangle window, bool incomplete, int nodeCount, long elapsedMs)
        {
            result.NodesVisited = nodeCount;
            result.ElapsedMs = elapsedMs;
            result.SelectedHorizontalTab = null;

            foreach (TabInfo tab in result.Tabs)
            {
                tab.IsHorizontalCandidate = !tab.IsOffscreen && TabGeometry.IsHorizontalCandidate(tab.Bounds, window, result.Dpi);
            }

            if (incomplete)
            {
                // A truncated walk may have skipped a second selected tab, so no selection is trustworthy.
                result.Outcome = ScanOutcome.ScanIncomplete;
                result.Message = "Quét UI Automation chưa đầy đủ (đạt giới hạn phần tử hoặc thời gian). Đã ẩn nút × để tránh đóng nhầm.";
                return;
            }

            if (result.Tabs.Count == 0)
            {
                result.Outcome = ScanOutcome.NoTabs;
                result.Message = "Không tìm thấy tab nào trong " + nodeCount + " phần tử UIA. Đã ẩn nút ×.";
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
                result.Message = "Đã thấy " + result.Tabs.Count + " tab nhưng không xác nhận được tab đang chọn. Đã ẩn nút ×.";
                return;
            }

            if (selected.Count > 1)
            {
                result.Outcome = ScanOutcome.AmbiguousSelection;
                result.Message = "Có " + selected.Count + " tab cùng được đánh dấu đang chọn. Đã ẩn nút × để tránh đóng nhầm.";
                return;
            }

            TabInfo only = selected[0];
            if (!only.IsHorizontalCandidate)
            {
                result.Outcome = ScanOutcome.NoSelectedTab;
                result.Message = "Tab đang chọn không nằm trong thanh tab ngang hợp lệ (có thể bị cuộn ra ngoài hoặc là tab ghim/dọc). Đã ẩn nút ×.";
                return;
            }

            result.Outcome = ScanOutcome.Ready;
            result.SelectedHorizontalTab = only;
            result.Message = "Đã xác nhận tab đang chọn. Tìm thấy " + result.Tabs.Count + " tab.";
        }
    }
}
