using System;
using System.Drawing;

namespace Xtension.Tests
{
    internal static class TabSelectorTests
    {
        // A 1200 px wide window at 96 DPI. Tabs sit in the top strip, 36 px tall.
        private static readonly Rectangle Window = new Rectangle(0, 0, 1200, 800);

        private static TabInfo Tab(string name, int left, bool selected)
        {
            return new TabInfo
            {
                Name = name,
                Bounds = new Rectangle(left, 8, 180, 36),
                SelectionKnown = true,
                Selected = selected
            };
        }

        private static ScanResult Result(params TabInfo[] tabs)
        {
            ScanResult result = new ScanResult();
            result.Dpi = 96;
            result.Window = IntPtr.Zero;
            result.Tabs.AddRange(tabs);
            return result;
        }

        [Test]
        public static void SingleSelectedHorizontalTabIsReady()
        {
            ScanResult result = Result(Tab("a", 0, false), Tab("b", 190, true), Tab("c", 380, false));
            TabSelector.Apply(result, Window, false, 40, 3, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.Ready, result.Outcome, "one selected tab should be Ready");
            Assert.IsTrue(result.SelectedHorizontalTab != null && result.SelectedHorizontalTab.Name == "b", "selected tab should be 'b'");
        }

        [Test]
        public static void IncompleteScanNeverReady()
        {
            ScanResult result = Result(Tab("a", 0, true));
            TabSelector.Apply(result, Window, true, 1500, 1000, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.ScanIncomplete, result.Outcome, "incomplete scan must not be Ready");
            Assert.IsTrue(result.SelectedHorizontalTab == null, "incomplete scan must not select a tab");
        }

        [Test]
        public static void TwoSelectedTabsAreAmbiguous()
        {
            ScanResult result = Result(Tab("a", 0, true), Tab("b", 190, true));
            TabSelector.Apply(result, Window, false, 10, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.AmbiguousSelection, result.Outcome, "two selected tabs must be ambiguous");
        }

        [Test]
        public static void UnknownSelectionIsNotSelected()
        {
            TabInfo unknown = Tab("a", 0, true);
            unknown.SelectionKnown = false;
            ScanResult result = Result(unknown);
            TabSelector.Apply(result, Window, false, 5, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoSelectedTab, result.Outcome, "a tab with unknown selection must not count as selected");
        }

        [Test]
        public static void NoTabsIsNoTabs()
        {
            ScanResult result = Result();
            TabSelector.Apply(result, Window, false, 3, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoTabs, result.Outcome, "empty tab list should be NoTabs");
        }

        [Test]
        public static void SelectedTabBelowStripIsRejected()
        {
            TabInfo low = Tab("a", 0, true);
            low.Bounds = new Rectangle(0, 400, 180, 36);
            ScanResult result = Result(low);
            TabSelector.Apply(result, Window, false, 5, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoSelectedTab, result.Outcome, "a selected tab outside the top strip must not be used");
        }

        [Test]
        public static void OffscreenSelectedTabIsRejected()
        {
            TabInfo hidden = Tab("a", 0, true);
            hidden.IsOffscreen = true;
            ScanResult result = Result(hidden);
            TabSelector.Apply(result, Window, false, 5, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoSelectedTab, result.Outcome, "an offscreen selected tab must not be used");
        }

        [Test]
        public static void ClearsStaleSelectionOnReapply()
        {
            ScanResult result = Result(Tab("a", 0, true));
            TabSelector.Apply(result, Window, false, 5, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.IsTrue(result.SelectedHorizontalTab != null, "first apply should select");
            result.Tabs[0].Selected = false;
            TabSelector.Apply(result, Window, false, 5, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.IsTrue(result.SelectedHorizontalTab == null, "re-apply must clear the previous selection");
        }
    }
}
