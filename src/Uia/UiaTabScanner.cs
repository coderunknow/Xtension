using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Automation;

namespace Xtension
{
    /// <summary>
    /// Bounded, read-only UI Automation walk of one Brave top-level window. Must run on a worker thread, never on the UI thread.
    /// It reads TabItem elements and never descends into a tab, page content (Document) or text input (Edit).
    /// Properties are fetched with one CacheRequest, so each element costs one cross-process round trip, not one per property.
    /// </summary>
    internal static class UiaTabScanner
    {
        private static readonly TreeWalker Walker = TreeWalker.ControlViewWalker;

        private sealed class WalkState
        {
            public readonly Stopwatch Watch = Stopwatch.StartNew();
            public readonly ScanResult Result;
            public int Nodes;
            public bool Incomplete;

            public WalkState(ScanResult result)
            {
                Result = result;
            }
        }

        public static ScanResult Scan(IntPtr window)
        {
            ScanResult result = new ScanResult();
            result.Window = window;
            result.Dpi = NativeMethods.GetWindowDpi(window);
            result.WindowTitle = NativeMethods.GetWindowTitle(window);

            Stopwatch total = Stopwatch.StartNew();
            Rectangle bounds = NativeMethods.GetWindowBounds(window);
            if (bounds.IsEmpty)
            {
                result.Outcome = ScanOutcome.WindowUnavailable;
                result.Message = "Không đọc được vị trí cửa sổ Brave. Đã ẩn nút ×.";
                result.ElapsedMs = total.ElapsedMilliseconds;
                return result;
            }

            WalkState state = new WalkState(result);
            try
            {
                CacheRequest cache = BuildCacheRequest();
                using (cache.Activate())
                {
                    AutomationElement root = AutomationElement.FromHandle(window);
                    if (root == null)
                    {
                        result.Outcome = ScanOutcome.WindowUnavailable;
                        result.Message = "UI Automation không trả về phần tử gốc của cửa sổ Brave. Đã ẩn nút ×.";
                        result.ElapsedMs = total.ElapsedMilliseconds;
                        return result;
                    }
                    Visit(root, 0, state);
                }
            }
            catch (Exception ex)
            {
                result.Outcome = ScanOutcome.Error;
                result.Error = ex.GetType().Name + ": " + ex.Message;
                result.Message = "Quét UI Automation thất bại. Đã ẩn nút ×.";
                result.ElapsedMs = total.ElapsedMilliseconds;
                return result;
            }

            TabSelector.Apply(result, bounds, state.Incomplete, state.Nodes, total.ElapsedMilliseconds);
            return result;
        }

        private static CacheRequest BuildCacheRequest()
        {
            CacheRequest cache = new CacheRequest();
            cache.Add(AutomationElement.ControlTypeProperty);
            cache.Add(AutomationElement.NameProperty);
            cache.Add(AutomationElement.BoundingRectangleProperty);
            cache.Add(AutomationElement.IsOffscreenProperty);
            cache.Add(SelectionItemPattern.Pattern);
            cache.Add(SelectionItemPattern.IsSelectedProperty);
            return cache;
        }

        private static void Visit(AutomationElement element, int depth, WalkState state)
        {
            if (state.Incomplete) return;
            if (state.Nodes >= AppInfo.ScanMaxNodes || state.Watch.ElapsedMilliseconds > AppInfo.ScanTimeBudgetMs)
            {
                state.Incomplete = true;
                return;
            }
            state.Nodes++;

            ControlType type;
            try
            {
                type = element.Cached.ControlType;
            }
            catch (ElementNotAvailableException)
            {
                state.Incomplete = true;
                return;
            }

            if (type == ControlType.TabItem)
            {
                state.Result.Tabs.Add(ReadTab(element, depth));
                return;
            }

            if (IsLeafContent(type) || depth >= AppInfo.ScanMaxDepth) return;

            AutomationElement child;
            try
            {
                child = Walker.GetFirstChild(element);
            }
            catch (ElementNotAvailableException)
            {
                state.Incomplete = true;
                return;
            }

            int siblings = 0;
            while (child != null)
            {
                if (siblings >= AppInfo.ScanMaxSiblings)
                {
                    state.Incomplete = true;
                    return;
                }

                Visit(child, depth + 1, state);
                if (state.Incomplete) return;
                siblings++;

                try
                {
                    child = Walker.GetNextSibling(child);
                }
                catch (ElementNotAvailableException)
                {
                    state.Incomplete = true;
                    return;
                }
            }
        }

        /// <summary>Page content, text and buttons never contain the tab strip, so skipping them saves most of the work.</summary>
        private static bool IsLeafContent(ControlType type)
        {
            return type == ControlType.Document
                || type == ControlType.Edit
                || type == ControlType.Text
                || type == ControlType.Image
                || type == ControlType.Button
                || type == ControlType.ScrollBar;
        }

        private static TabInfo ReadTab(AutomationElement element, int depth)
        {
            TabInfo tab = new TabInfo();
            tab.Name = element.Cached.Name ?? string.Empty;
            tab.Bounds = ToRectangle(element.Cached.BoundingRectangle);
            tab.IsOffscreen = element.Cached.IsOffscreen;
            tab.Depth = depth;

            object pattern;
            if (element.TryGetCachedPattern(SelectionItemPattern.Pattern, out pattern))
            {
                tab.SelectionKnown = true;
                tab.Selected = ((SelectionItemPattern)pattern).Cached.IsSelected;
            }
            return tab;
        }

        private static Rectangle ToRectangle(System.Windows.Rect rect)
        {
            if (double.IsNaN(rect.Left) || double.IsNaN(rect.Top) || double.IsNaN(rect.Right) || double.IsNaN(rect.Bottom)) return Rectangle.Empty;
            if (double.IsInfinity(rect.Left) || double.IsInfinity(rect.Top) || double.IsInfinity(rect.Right) || double.IsInfinity(rect.Bottom)) return Rectangle.Empty;

            int left = (int)Math.Round(rect.Left);
            int top = (int)Math.Round(rect.Top);
            int right = (int)Math.Round(rect.Right);
            int bottom = (int)Math.Round(rect.Bottom);
            if (right <= left || bottom <= top) return Rectangle.Empty;
            return Rectangle.FromLTRB(left, top, right, bottom);
        }
    }
}
