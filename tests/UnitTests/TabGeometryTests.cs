using System.Drawing;

namespace Xtension.Tests
{
    internal static class TabGeometryTests
    {
        [Test]
        public static void OverlaySizeScalesWithDpi()
        {
            Assert.AreEqual(26, TabGeometry.OverlaySize(96), "96 DPI overlay size");
            Assert.AreEqual(52, TabGeometry.OverlaySize(192), "192 DPI overlay size");
        }

        [Test]
        public static void OverlayIsInsideTabAtRightEdge()
        {
            Rectangle tab = new Rectangle(100, 8, 180, 36);
            Rectangle close = TabGeometry.GetOverlayBounds(tab, 96);
            Assert.IsTrue(tab.Contains(close), "overlay must lie inside the tab");
            Assert.AreEqual(tab.Right - 3, close.Right, "overlay right edge is inset 3 px from the tab edge");
            Assert.AreEqual(tab.Top + (tab.Height - close.Height) / 2, close.Top, "overlay is vertically centred");
        }

        [Test]
        public static void SmallTabHasNoOverlay()
        {
            Assert.IsTrue(TabGeometry.GetOverlayBounds(new Rectangle(0, 0, 40, 30), 96).IsEmpty, "a tab narrower than 55 px gets no button");
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
    }
}
