using System.Drawing;

namespace Xtension.Tests
{
    internal static class TabGeometryTests
    {
        [Test]
        public static void OverlaySizeScalesWithDpiAtTheDefaultSize()
        {
            Assert.AreEqual(26, TabGeometry.OverlaySize(96, AppInfo.DefaultButtonSizeLogical), "96 DPI overlay size");
            Assert.AreEqual(52, TabGeometry.OverlaySize(192, AppInfo.DefaultButtonSizeLogical), "192 DPI overlay size");
        }

        [Test]
        public static void EveryConfiguredSizeScalesAndStaysInRange()
        {
            foreach (int dpi in new int[] { 96, 120, 144, 192, 240 })
            {
                foreach (int logical in new int[] { AppInfo.MinButtonSizeLogical, AppInfo.DefaultButtonSizeLogical, 33, AppInfo.MaxButtonSizeLogical })
                {
                    int physical = TabGeometry.OverlaySize(dpi, logical);
                    int expected = (int)System.Math.Round(logical * TabGeometry.Scale(dpi));
                    Assert.AreEqual(expected, physical, "physical size for " + logical + " at " + dpi + " DPI");
                    Assert.IsTrue(physical >= logical, "a scaled size is never smaller than the logical value");
                }
            }
            Assert.IsTrue(TabGeometry.OverlaySize(96, 1000) <= TabGeometry.OverlaySize(96, AppInfo.MaxButtonSizeLogical),
                "an out-of-range size is clamped before it reaches the window");
            Assert.IsTrue(TabGeometry.OverlaySize(96, 0) >= TabGeometry.OverlaySize(96, AppInfo.MinButtonSizeLogical),
                "a zero size is clamped up, never trusted");
        }

        [Test]
        public static void OverlayIsInsideTabAtRightEdge()
        {
            Rectangle tab = new Rectangle(100, 8, 180, 36);
            Rectangle close = TabGeometry.GetOverlayBounds(tab, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.IsTrue(tab.Contains(close), "overlay must lie inside the tab");
            Assert.AreEqual(tab.Right - AppInfo.DefaultButtonInsetLogical, close.Right, "overlay right edge is inset from the tab edge");
            Assert.AreEqual(tab.Top + (tab.Height - close.Height) / 2, close.Top, "overlay is vertically centred");
        }

        [Test]
        public static void InsetMovesTheButtonInwardAndIsClamped()
        {
            Rectangle tab = new Rectangle(100, 8, 240, 44);
            Rectangle flush = TabGeometry.GetOverlayBounds(tab, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.MinButtonInsetLogical);
            Rectangle inset = TabGeometry.GetOverlayBounds(tab, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.MaxButtonInsetLogical);
            Assert.AreEqual(tab.Right, flush.Right, "inset 0 sits flush with the tab's right edge");
            Assert.AreEqual(tab.Right - AppInfo.MaxButtonInsetLogical, inset.Right, "the configured inset is applied");
            Assert.IsTrue(inset.Left < flush.Left, "a larger inset moves the button left");
            Rectangle clamped = TabGeometry.GetOverlayBounds(tab, 96, AppInfo.DefaultButtonSizeLogical, 999);
            Assert.AreEqual(inset, clamped, "an out-of-range inset is clamped to the maximum, not trusted");
        }

        [Test]
        public static void SmallTabHasNoOverlay()
        {
            Assert.IsTrue(TabGeometry.GetOverlayBounds(new Rectangle(0, 0, 40, 30), 96, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).IsEmpty,
                "a tab narrower than the button needs gets no button");
        }

        [Test]
        public static void LargerConfiguredButtonNeedsAWiderTab()
        {
            // 30 + 12 = 42 physical px are needed at the maximum size, versus 29 at the default size.
            Rectangle narrow = new Rectangle(100, 8, 32, 40);
            Assert.IsFalse(TabGeometry.GetOverlayBounds(narrow, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).IsEmpty,
                "the default button fits a 32 px tab");
            Assert.IsTrue(TabGeometry.GetOverlayBounds(narrow, 96, AppInfo.MaxButtonSizeLogical, AppInfo.MaxButtonInsetLogical).IsEmpty,
                "the largest button with the largest inset does not fit the same tab");
        }

        [Test]
        public static void ScaledTabAtTwoHundredPercentIsCandidate()
        {
            // At 200 % a 180 px wide tab is 360 physical px. The old fixed thresholds rejected it.
            Rectangle window = new Rectangle(0, 0, 2400, 1600);
            Rectangle tab = new Rectangle(200, 16, 360, 72);
            Assert.IsTrue(TabGeometry.IsHorizontalCandidate(tab, window, 192), "a 200% tab must be recognised as horizontal");
        }

        [Test]
        public static void TabOutsideWindowIsNotCandidate()
        {
            Rectangle window = new Rectangle(0, 0, 1200, 800);
            Rectangle tab = new Rectangle(5000, 8, 180, 36);
            Assert.IsFalse(TabGeometry.IsHorizontalCandidate(tab, window, 96), "a tab far outside the window is not a candidate");
        }

        [Test]
        public static void ButtonSizeAndInsetClampsMatchTheDocumentedRanges()
        {
            Assert.AreEqual(AppInfo.MinButtonSizeLogical, TabGeometry.ClampButtonSize(int.MinValue), "size below the minimum");
            Assert.AreEqual(AppInfo.MaxButtonSizeLogical, TabGeometry.ClampButtonSize(int.MaxValue), "size above the maximum");
            Assert.AreEqual(30, TabGeometry.ClampButtonSize(30), "a value inside the range is kept");
            Assert.AreEqual(AppInfo.MinButtonInsetLogical, TabGeometry.ClampButtonInset(-1), "inset below the minimum");
            Assert.AreEqual(AppInfo.MaxButtonInsetLogical, TabGeometry.ClampButtonInset(int.MaxValue), "inset above the maximum");
        }

        [Test]
        public static void TabStripRegionCoversTheStripAndNothingBelowIt()
        {
            Rectangle window = new Rectangle(0, 0, 1200, 800);
            Rectangle strip = TabGeometry.GetTabStripRegion(window, 96);
            Assert.IsFalse(strip.IsEmpty, "a normal window has a tab strip region");
            Assert.AreEqual(window.Left, strip.Left, "the strip starts at the window's left edge");
            Assert.AreEqual(window.Right, strip.Right, "the strip spans the window's width");
            Assert.AreEqual(window.Top, strip.Top, "the strip starts at the window's top edge");
            Assert.IsTrue(strip.Height <= window.Height / 4, "the strip stays a thin band at the top");
            Assert.IsTrue(TabGeometry.GetTabStripRegion(Rectangle.Empty, 96).IsEmpty, "no window bounds means no strip");
        }
    }
}
