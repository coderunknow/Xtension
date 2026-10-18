using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Automation;

namespace Xtension
{
    /// <summary>
    /// Bounded, read-only UI Automation walk of one browser window. Must run on a worker thread, never on the UI
    /// thread.
    ///
    /// The walk is only entered through an explicit <see cref="TabAccessContract"/>: the caller passes the catalog
    /// entry for the window it verified, and a browser whose contract this scanner does not implement is refused
    /// outright. That is what stops a Chromium window that merely looks like Brave from being scanned with Brave's
    /// assumptions, and it is what a new browser has to implement instead of inheriting.
    /// It reads TabItem elements and never descends into a tab, page content (Document) or text input (Edit).
    /// Properties are prefetched through an explicit CacheRequest on every navigation operation; the request is
    /// created and released inside the scan (only the activation returned by Activate() is disposable in
    /// .NET Framework 4.8) so repeated scans cannot accumulate automation objects.
    /// </summary>
    internal static class UiaTabScanner
    {
        private static readonly TreeWalker Walker = TreeWalker.ControlViewWalker;

        private sealed class WalkState
        {
            public readonly Stopwatch Watch;
            public readonly ScanResult Result;
            public int Nodes;
            public bool Incomplete;
            public string Operation = "BuildCacheRequest";
            public CacheRequest Cache;

            public WalkState(ScanResult result, Stopwatch watch)
            {
                Result = result;
                Watch = watch;
            }
        }

        /// <summary>
        /// Scans a window after resolving its identity and contract from the process image name. Used by callers that
        /// only hold a window handle; the resolution never looks at a title or a window class.
        /// </summary>
        public static ScanResult Scan(IntPtr window, int sizeLogical, int insetLogical)
        {
            return Scan(window, ProcessIdentity.Descriptor(window, false), sizeLogical, insetLogical);
        }

        /// <summary>
        /// Scans a window whose browser entry the caller already verified, and refuses every contract this build does
        /// not implement. Refusing returns no tabs and no selected tab, so nothing downstream can authorise anything.
        /// </summary>
        public static ScanResult Scan(IntPtr window, BrowserDescriptor descriptor, int sizeLogical, int insetLogical)
        {
            if (descriptor == null || !descriptor.ScannerImplemented || !BrowserCatalog.IsImplemented(descriptor.Contract))
            {
                ScanResult refused = ScanResult.Idle(window, ScanOutcome.ContractUnimplemented,
                    ScanMessageKey.BrowserUnimplemented);
                refused.Dpi = NativeMethods.GetWindowDpi(window);
                return refused;
            }
            return ScanWindow(window, sizeLogical, insetLogical);
        }

        private static ScanResult ScanWindow(IntPtr window, int sizeLogical, int insetLogical)
        {
            ScanResult result = new ScanResult();
            result.Window = window;
            result.Dpi = NativeMethods.GetWindowDpi(window);

            Stopwatch total = Stopwatch.StartNew();
            Rectangle bounds = NativeMethods.GetWindowBounds(window);
            result.WindowBounds = bounds;
            if (bounds.IsEmpty)
            {
                result.Outcome = ScanOutcome.WindowUnavailable;
                result.Key = ScanMessageKey.WindowBoundsUnavailable;
                result.ElapsedMs = total.ElapsedMilliseconds;
                return result;
            }

            WalkState state = new WalkState(result, total);
            try
            {
                state.Cache = BuildCacheRequest();
                state.Operation = "CacheRequest.Activate";
                using (state.Cache.Activate())
                {
                    state.Operation = "AutomationElement.FromHandle";
                    AutomationElement root = AutomationElement.FromHandle(window);
                    if (root == null)
                    {
                        result.Outcome = ScanOutcome.WindowUnavailable;
                        result.Key = ScanMessageKey.WindowRootUnavailable;
                        result.ElapsedMs = total.ElapsedMilliseconds;
                        return result;
                    }
                    Visit(root, 0, null, state);
                }
            }
            catch (ElementNotAvailableException ex)
            {
                state.Incomplete = true;
                result.Error = ScanSafety.DescribeError(state.Operation, ex);
            }
            catch (Exception ex)
            {
                result.Outcome = ScanOutcome.Error;
                result.Key = ScanMessageKey.ScanFailed;
                result.Error = ScanSafety.DescribeError(state.Operation, ex);
                result.NodesVisited = state.Nodes;
                result.ElapsedMs = total.ElapsedMilliseconds;
                return result;
            }
            finally
            {
                // Only the activation returned by Activate() is disposable in .NET Framework, and the using block
                // above already releases it. CacheRequest itself exposes no release method (verified by the compiler:
                // System.Windows.Automation.CacheRequest has no Dispose), so the request object is collected normally.
                state.Cache = null;
            }

            // A final provider call can exceed the budget even if no further node is visited.
            if (total.ElapsedMilliseconds >= AppInfo.ScanTimeBudgetMs) MarkIncomplete(state, "TimeBudget");
            TabSelector.Apply(result, bounds, state.Incomplete, state.Nodes, total.ElapsedMilliseconds, sizeLogical, insetLogical);
            return result;
        }

        private static CacheRequest BuildCacheRequest()
        {
            CacheRequest cache = new CacheRequest();
            cache.TreeScope = TreeScope.Element;
            cache.Add(AutomationElement.ControlTypeProperty);
            cache.Add(AutomationElement.NameProperty);
            cache.Add(AutomationElement.BoundingRectangleProperty);
            cache.Add(AutomationElement.IsOffscreenProperty);
            cache.Add(SelectionItemPattern.Pattern);
            cache.Add(SelectionItemPattern.IsSelectedProperty);
            return cache;
        }

        private static void Visit(AutomationElement element, int depth, ControlType parentType, WalkState state)
        {
            if (state.Incomplete) return;
            if (state.Nodes >= AppInfo.ScanMaxNodes || state.Watch.ElapsedMilliseconds > AppInfo.ScanTimeBudgetMs)
            {
                MarkIncomplete(state, state.Nodes >= AppInfo.ScanMaxNodes ? "NodeBudget" : "TimeBudget");
                return;
            }
            state.Nodes++;

            state.Operation = "Cached.ControlType";
            ControlType type = element.Cached.ControlType;
            if (type == ControlType.TabItem)
            {
                // Chromium exposes strip tabs as direct children of a UIA Tab (ARIA tablist). A TabItem elsewhere
                // is not evidence of a browser tab. Do not guess if a provider changes this parent contract.
                if (parentType != ControlType.Tab) MarkIncomplete(state, "TabItemParent="
                    + (parentType == null ? "null" : parentType.ProgrammaticName) + ";depth=" + depth);
                state.Result.Tabs.Add(ReadTab(element, depth, state));
                return;
            }
            if (IsLeafContent(type)) return;

            state.Operation = "TreeWalker.GetFirstChild(cache)";
            AutomationElement child = Walker.GetFirstChild(element, state.Cache);
            if (child != null && depth >= AppInfo.ScanMaxDepth)
            {
                MarkIncomplete(state, "DepthBudget");
                return;
            }
            int siblings = 0;
            while (child != null)
            {
                if (siblings >= AppInfo.ScanMaxSiblings)
                {
                    MarkIncomplete(state, "SiblingBudget");
                    return;
                }
                Visit(child, depth + 1, type, state);
                if (state.Incomplete) return;
                siblings++;
                state.Operation = "TreeWalker.GetNextSibling(cache)";
                child = Walker.GetNextSibling(child, state.Cache);
            }
        }

        private static void MarkIncomplete(WalkState state, string reason)
        {
            state.Incomplete = true;
            // Tokens and counts only. Never add provider Name, message, path or runtime identity to this metadata.
            if (string.IsNullOrEmpty(state.Result.Error)) state.Result.Error = "Incomplete: " + reason;
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

        private static TabInfo ReadTab(AutomationElement element, int depth, WalkState state)
        {
            TabInfo tab = new TabInfo();
            state.Operation = "Cached.Name";
            tab.Name = element.Cached.Name ?? string.Empty;
            state.Operation = "GetRuntimeId";
            tab.RuntimeId = element.GetRuntimeId();
            if (tab.RuntimeId == null || tab.RuntimeId.Length == 0) MarkIncomplete(state, "MissingRuntimeId");
            state.Operation = "Cached.BoundingRectangle";
            tab.Bounds = ToRectangle(element.Cached.BoundingRectangle);
            state.Operation = "Cached.IsOffscreen";
            tab.IsOffscreen = element.Cached.IsOffscreen;
            tab.Depth = depth;

            state.Operation = "TryGetCachedPattern(SelectionItem)";
            object pattern;
            if (element.TryGetCachedPattern(SelectionItemPattern.Pattern, out pattern))
            {
                state.Operation = "SelectionItem.Cached.IsSelected";
                tab.SelectionKnown = true;
                tab.Selected = ((SelectionItemPattern)pattern).Cached.IsSelected;
            }
            return tab;
        }

        private static Rectangle ToRectangle(System.Windows.Rect rect)
        {
            if (double.IsNaN(rect.Left) || double.IsNaN(rect.Top) || double.IsNaN(rect.Right) || double.IsNaN(rect.Bottom)) return Rectangle.Empty;
            if (double.IsInfinity(rect.Left) || double.IsInfinity(rect.Top) || double.IsInfinity(rect.Right) || double.IsInfinity(rect.Bottom)) return Rectangle.Empty;

            if (rect.Left < int.MinValue || rect.Top < int.MinValue || rect.Right > int.MaxValue || rect.Bottom > int.MaxValue
                || rect.Width > int.MaxValue || rect.Height > int.MaxValue) return Rectangle.Empty;

            int left = (int)Math.Round(rect.Left);
            int top = (int)Math.Round(rect.Top);
            int right = (int)Math.Round(rect.Right);
            int bottom = (int)Math.Round(rect.Bottom);
            if (right <= left || bottom <= top) return Rectangle.Empty;
            return Rectangle.FromLTRB(left, top, right, bottom);
        }
    }
}
