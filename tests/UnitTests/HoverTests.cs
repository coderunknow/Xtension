using System.Drawing;

namespace Xtension.Tests
{
    /// <summary>
    /// Hover mode is the newest behaviour that can hide or show a clickable button, so its transitions are tested
    /// as a pure state machine: eligibility, entry, the bounded grace window, and the resets that must prevent a
    /// stale hover from surviving a tab switch, a geometry change or a failed scan.
    /// </summary>
    internal static class HoverTests
    {
        private const int Grace = AppInfo.DefaultHoverGraceMs;

        [Test]
        public static void RegionIsTheSelectedTabPlusTheButton()
        {
            Rectangle tab = new Rectangle(100, 8, 180, 36);
            Rectangle overlay = new Rectangle(255, 13, 26, 26);
            Assert.AreEqual(Rectangle.Union(tab, overlay), HoverRegion.ForSelectedTab(tab, overlay), "region is the union");
            Assert.AreEqual(tab, HoverRegion.ForSelectedTab(tab, Rectangle.Empty), "no button bounds still yields the tab");
            Assert.IsTrue(HoverRegion.ForSelectedTab(Rectangle.Empty, overlay).IsEmpty, "no confirmed tab has no region");
        }

        [Test]
        public static void InactiveTabAndTitleBarAreNotInsideTheRegion()
        {
            Rectangle selected = new Rectangle(280, 8, 180, 36);
            Rectangle region = HoverRegion.ForSelectedTab(selected, Rectangle.Empty);
            Assert.IsTrue(HoverRegion.Contains(region, new Point(selected.Left + 5, selected.Top + 5)), "inside the selected tab");
            Assert.IsFalse(HoverRegion.Contains(region, new Point(selected.Left - 5, selected.Top + 5)), "the tab to its left is inactive");
            Assert.IsFalse(HoverRegion.Contains(region, new Point(selected.Right + 5, selected.Top + 5)), "the tab to its right is inactive");
            Assert.IsFalse(HoverRegion.Contains(region, new Point(selected.Left + 5, selected.Bottom + 5)), "Brave content below the strip");
            Assert.IsFalse(HoverRegion.Contains(region, new Point(selected.Left + 5, selected.Top - 5)), "above the strip");
            Assert.IsFalse(HoverRegion.Contains(Rectangle.Empty, new Point(0, 0)), "an empty region contains nothing");
        }

        [Test]
        public static void PointerEntryShowsImmediately()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, Grace), "pointer inside the region is visible at once");
            Assert.IsTrue(tracker.Visible, "tracker reports visible");
            Assert.IsTrue(tracker.PointerInside, "pointer state recorded");
        }

        [Test]
        public static void LeavingKeepsTheButtonForExactlyTheGracePeriod()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, Grace), "enter");
            Assert.IsTrue(tracker.Update(true, false, 1100, Grace), "just left, still inside the grace window");
            Assert.IsTrue(tracker.Update(true, false, 1100 + Grace - 1, Grace), "one millisecond before the deadline");
            Assert.IsFalse(tracker.Update(true, false, 1100 + Grace, Grace), "at the deadline the button is hidden");
            Assert.IsFalse(tracker.Visible, "state follows the decision");
        }

        [Test]
        public static void ZeroGraceHidesImmediately()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, 0), "enter");
            Assert.IsFalse(tracker.Update(true, false, 1000, 0), "grace 0 hides as soon as the pointer leaves");
        }

        [Test]
        public static void ReenteringWithinGraceKeepsItVisibleWithoutFlicker()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, Grace), "enter");
            Assert.IsTrue(tracker.Update(true, false, 1050, Grace), "leaves the tab, moving towards the button");
            Assert.IsTrue(tracker.Update(true, true, 1100, Grace), "reaches the button before the deadline");
            Assert.IsTrue(tracker.Update(true, false, 1200, Grace), "grace restarts from the second exit");
            Assert.IsFalse(tracker.Update(true, false, 1200 + Grace, Grace), "and ends on time");
        }

        [Test]
        public static void IneligibleStateHidesImmediatelyWithoutGrace()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, Grace), "enter");
            // Not eligible covers: overlay disabled, Brave not in front, stalled scan, cooldown, stale confirmation.
            Assert.IsFalse(tracker.Update(false, true, 1010, Grace), "ineligible hides at once even with the pointer inside");
            Assert.IsFalse(tracker.PointerInside, "hover state is cleared, not merely hidden");
            Assert.IsFalse(tracker.Visible, "and stays hidden");
        }

        [Test]
        public static void ResetClearsAHoverWaitingInsideItsGraceWindow()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 1000, Grace), "enter");
            Assert.IsTrue(tracker.Update(true, false, 1010, Grace), "left, inside grace");
            tracker.Reset();
            Assert.IsFalse(tracker.Visible, "a tab switch or scan failure must not leave a stale hover alive");
            Assert.IsFalse(tracker.PointerInside, "pointer state also cleared");
        }

        [Test]
        public static void OutOfOrderClockNeverKeepsAStaleButton()
        {
            HoverTracker tracker = new HoverTracker();
            Assert.IsTrue(tracker.Update(true, true, 5000, Grace), "enter");
            Assert.IsTrue(tracker.Update(true, false, 5001, Grace), "left");
            // A clock that appears to move backwards must not extend the grace window indefinitely.
            Assert.IsFalse(tracker.Update(true, false, 4000, Grace), "an impossible earlier timestamp hides the button");
        }
    }
}
