using System;
using System.Drawing;

namespace Xtension.Tests
{
    internal static class CloseGuardTests
    {
        private static readonly IntPtr Brave = new IntPtr(1001);
        private static readonly Rectangle Tab = new Rectangle(100, 8, 180, 36);

        // Scan result whose selected tab is Tab, at 96 DPI.
        private static ScanResult ReadyScan()
        {
            ScanResult scan = new ScanResult();
            scan.Window = Brave;
            scan.Dpi = 96;
            scan.Outcome = ScanOutcome.Ready;
            TabInfo tab = new TabInfo { RuntimeId = new int[] { 42, 7 }, Name = "t", Bounds = Tab, SelectionKnown = true, Selected = true, IsHorizontalCandidate = true };
            scan.Tabs.Add(tab);
            scan.SelectedHorizontalTab = tab;
            return scan;
        }


        [Test]
        public static void DifferentTabAtIdenticalGeometryRejects()
        {
            CloseRequest request = ValidRequest();
            request.Scan.SelectedHorizontalTab.RuntimeId = new int[] { 42, 8 };
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "same coordinates are not the intended tab");
        }

        [Test]
        public static void MissingOrPartialIdentityRejects()
        {
            CloseRequest request = ValidRequest();
            request.ExpectedTabRuntimeId = null;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "missing displayed identity refuses");
            request = ValidRequest();
            request.Scan.SelectedHorizontalTab.RuntimeId = new int[0];
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "missing fresh identity refuses");
            request.Scan.SelectedHorizontalTab.RuntimeId = new int[] { 42 };
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "partial identity refuses");
        }

        [Test]
        public static void ClickAreaFollowsConfiguredButtonSize()
        {
            // A taller tab so both the minimum and the maximum button fit inside it.
            Rectangle tallTab = new Rectangle(100, 8, 220, 44);
            Rectangle small = TabGeometry.GetOverlayBounds(tallTab, 96, AppInfo.MinButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Rectangle large = TabGeometry.GetOverlayBounds(tallTab, 96, AppInfo.MaxButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            Assert.IsFalse(small.IsEmpty, "minimum size fits the fixture tab");
            Assert.IsFalse(large.IsEmpty, "maximum size fits the fixture tab");
            Assert.IsTrue(large.Left < small.Left, "a larger button starts further left");
            Point largeOnly = new Point(large.Left + 1, large.Top + large.Height / 2);
            Assert.IsFalse(small.Contains(largeOnly), "fixture point is outside the minimum-size button");

            CloseRequest defaultSize = ValidRequest();
            defaultSize.Scan.SelectedHorizontalTab.Bounds = tallTab;
            defaultSize.Scan.Tabs[0].Bounds = tallTab;
            defaultSize.Click = largeOnly;
            Assert.IsFalse(CloseGuard.Evaluate(defaultSize).Allowed, "click outside the configured minimum-size button must reject");

            CloseRequest maximumSize = ValidRequest();
            maximumSize.Scan.SelectedHorizontalTab.Bounds = tallTab;
            maximumSize.Scan.Tabs[0].Bounds = tallTab;
            maximumSize.OverlaySizeLogical = AppInfo.MaxButtonSizeLogical;
            maximumSize.Click = largeOnly;
            Assert.IsTrue(CloseGuard.Evaluate(maximumSize).Allowed, "the same click must be accepted with the configured maximum size");
        }

        [Test]
        public static void OutOfRangeConfiguredSizeIsClampedNotTrusted()
        {
            Rectangle tallTab = new Rectangle(100, 8, 220, 44);
            CloseRequest request = ValidRequest();
            request.Scan.SelectedHorizontalTab.Bounds = tallTab;
            request.Scan.Tabs[0].Bounds = tallTab;
            request.OverlaySizeLogical = 10000;
            request.OverlayInsetLogical = -5;
            Rectangle clamped = TabGeometry.GetOverlayBounds(tallTab, 96, request.OverlaySizeLogical, request.OverlayInsetLogical);
            Assert.IsFalse(clamped.IsEmpty, "clamped geometry must still fit a normal tab");
            request.Click = new Point(clamped.Left + clamped.Width / 2, clamped.Top + clamped.Height / 2);
            Assert.IsTrue(CloseGuard.Evaluate(request).Allowed, "clamped geometry is used consistently for validation");
        }

        private static CloseRequest ValidRequest()
        {
            ScanResult scan = ReadyScan();
            Rectangle close = TabGeometry.GetOverlayBounds(Tab, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            return new CloseRequest
            {
                OverlayEnabled = true,
                EpochCurrent = true,
                ScannedWindow = Brave,
                ForegroundNow = Brave,
                ForegroundIsSupportedBrowser = true,
                Minimized = false,
                BlockingModifierDown = false,
                Scan = scan,
                ExpectedTabRuntimeId = new int[] { 42, 7 },
                Click = new Point(close.X + close.Width / 2, close.Y + close.Height / 2),
                NowMs = 10000,
                StartedMs = 9990,
                LastSentMs = Clock.Never
            };
        }

        [Test]
        public static void LateDeliveryRejectsEvenWithoutTimerTick()
        {
            CloseRequest request = ValidRequest();
            request.StartedMs = request.NowMs - AppInfo.CloseValidationTimeoutMs;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "message loop timeout cannot authorise close");
        }

        [Test]
        public static void ScanErrorAndWrongWindowReject()
        {
            CloseRequest request = ValidRequest();
            request.Scan.Outcome = ScanOutcome.Error;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "UIA failure must not close");
            request = ValidRequest();
            request.Scan.Window = new IntPtr(2002);
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "scan must match clicked window");
        }

        [Test]
        public static void RightAndBottomEdgesAreOutsideCloseArea()
        {
            CloseRequest request = ValidRequest();
            Rectangle area = TabGeometry.GetOverlayBounds(Tab, 96, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            request.Click = new Point(area.Right, area.Top);
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "right edge excluded");
            request.Click = new Point(area.Left, area.Bottom);
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "bottom edge excluded");
        }

        [Test]
        public static void ValidClickOnConfirmedCloseButtonIsAllowed()
        {
            CloseDecision decision = CloseGuard.Evaluate(ValidRequest());
            Assert.IsTrue(decision.Allowed, "valid click should be allowed: " + decision.Reason);
        }

        [Test]
        public static void DisabledOverlayRejects()
        {
            CloseRequest request = ValidRequest();
            request.OverlayEnabled = false;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "disabled overlay must never close");
        }

        [Test]
        public static void EpochChangeRejects()
        {
            CloseRequest request = ValidRequest();
            request.EpochCurrent = false;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "state change during validation must reject");
        }

        [Test]
        public static void ModifierHeldRejects()
        {
            CloseRequest request = ValidRequest();
            request.BlockingModifierDown = true;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "Shift/Alt/Win held must reject, Ctrl+Shift+W would close the window");
        }

        [Test]
        public static void ForegroundChangedRejects()
        {
            CloseRequest request = ValidRequest();
            request.ForegroundNow = new IntPtr(2002);
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "foreground change must reject");
        }

        [Test]
        public static void NonBraveForegroundRejects()
        {
            CloseRequest request = ValidRequest();
            request.ForegroundIsSupportedBrowser = false;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "non-Brave foreground must reject");
        }

        [Test]
        public static void MinimizedRejects()
        {
            CloseRequest request = ValidRequest();
            request.Minimized = true;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "minimized Brave must reject");
        }

        [Test]
        public static void NotReadyScanRejects()
        {
            CloseRequest request = ValidRequest();
            request.Scan.Outcome = ScanOutcome.ScanIncomplete;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "an incomplete scan must reject");
        }

        [Test]
        public static void ClickOutsideCloseButtonRejects()
        {
            CloseRequest request = ValidRequest();
            request.Click = new Point(Tab.Left + 10, Tab.Top + 10);
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "click on the tab body must reject (only the × may close)");
        }

        [Test]
        public static void StaleClickPositionRejects()
        {
            CloseRequest request = ValidRequest();
            // The tab moved after the overlay was placed: the click is on the old × position, now outside the tab.
            request.Scan.SelectedHorizontalTab.Bounds = new Rectangle(400, 8, 180, 36);
            request.Scan.Tabs[0].Bounds = request.Scan.SelectedHorizontalTab.Bounds;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "a click on a stale position must reject");
        }

        [Test]
        public static void CooldownRejects()
        {
            CloseRequest request = ValidRequest();
            request.LastSentMs = request.NowMs - 100;
            Assert.IsFalse(CloseGuard.Evaluate(request).Allowed, "a second close within the cooldown must reject");
        }

        [Test]
        public static void CooldownExpiresAllowsClose()
        {
            CloseRequest request = ValidRequest();
            request.LastSentMs = request.NowMs - AppInfo.CloseCooldownMs - 1;
            Assert.IsTrue(CloseGuard.Evaluate(request).Allowed, "a close after the cooldown should be allowed");
        }
    }
}
