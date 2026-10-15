using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Xtension.Tests
{
    internal static class ScanSafetyTests
    {
        [Test]
        public static void FreshnessIncludesDeliveryDelayAndExactDeadline()
        {
            Assert.IsTrue(ScanSafety.IsFresh(10, 1009, 1000), "before deadline");
            Assert.IsFalse(ScanSafety.IsFresh(10, 1010, 1000), "at deadline");
            Assert.IsFalse(ScanSafety.IsFresh(Clock.Never, 10, 1000), "no timestamp");
            Assert.IsFalse(ScanSafety.IsFresh(20, 10, 1000), "future timestamp");
            Assert.IsFalse(ScanSafety.IsFresh(10, 9000, 1000), "late queued result");
        }

        [Test]
        public static void ErrorMetadataNeverIncludesProviderMessageOrData()
        {
            Exception error = new COMException("PRIVATE-TAB https://private.invalid/", unchecked((int)0x80040201));
            error.Data["private"] = "PRIVATE-TAB";
            string detail = ScanSafety.DescribeError("Cached.ControlType", error);
            Assert.IsTrue(detail.Contains("Cached.ControlType") && detail.Contains("COMException") && detail.Contains("80040201"), "actionable metadata");
            Assert.IsFalse(detail.Contains("PRIVATE") || detail.Contains("https"), "no arbitrary exception text");
        }

        [Test]
        public static void KnownSelectionBesideUnknownTabIsNotUnique()
        {
            ScanResult scan = new ScanResult();
            scan.Tabs.Add(new TabInfo { Bounds = new Rectangle(0, 8, 180, 36), Selected = true, SelectionKnown = true });
            scan.Tabs.Add(new TabInfo { Bounds = new Rectangle(180, 8, 180, 36) });
            TabSelector.Apply(scan, new Rectangle(0, 0, 1200, 800), false, 3, 10, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoSelectedTab, scan.Outcome, "unknown second tab may also be selected");
            Assert.IsTrue(scan.SelectedHorizontalTab == null, "never authorise close");
        }

        [Test]
        public static void OverlayNeverProtrudesFromShortTabAtCommonDpi()
        {
            foreach (int dpi in new int[] { 96, 120, 144, 192 })
            {
                int size = TabGeometry.OverlaySize(dpi, AppInfo.DefaultButtonSizeLogical);
                Rectangle shortTab = new Rectangle(-800, 8, 400, size - 1);
                Assert.IsTrue(TabGeometry.GetOverlayBounds(shortTab, dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).IsEmpty, "short tab at " + dpi);
                Rectangle tab = new Rectangle(-800, 8, 400, size);
                Rectangle overlay = TabGeometry.GetOverlayBounds(tab, dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                Assert.IsFalse(overlay.IsEmpty, "exact fit");
                Assert.IsTrue(tab.Contains(overlay), "negative monitor coordinates and exact fit");
            }
        }

        [Test]
        public static void ShortSelectedTabCannotBeReady()
        {
            ScanResult scan = new ScanResult();
            scan.Tabs.Add(new TabInfo { Bounds = new Rectangle(0, 8, 180, 16), Selected = true, SelectionKnown = true });
            TabSelector.Apply(scan, new Rectangle(0, 0, 1200, 800), false, 3, 1, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.AreEqual(ScanOutcome.NoSelectedTab, scan.Outcome, "recognition must agree with overlay fit");
        }
    }
}
