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
            TabInfo tab = new TabInfo { Name = "t", Bounds = Tab, SelectionKnown = true, Selected = true, IsHorizontalCandidate = true };
            scan.Tabs.Add(tab);
            scan.SelectedHorizontalTab = tab;
            return scan;
        }

        private static CloseRequest ValidRequest()
        {
            ScanResult scan = ReadyScan();
            Rectangle close = TabGeometry.GetOverlayBounds(Tab, 96);
            return new CloseRequest
            {
                OverlayEnabled = true,
                EpochCurrent = true,
                ScannedWindow = Brave,
                ForegroundNow = Brave,
                ForegroundIsBrave = true,
                Minimized = false,
                BlockingModifierDown = false,
                Scan = scan,
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
            Rectangle area = TabGeometry.GetOverlayBounds(Tab, 96);
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
            request.ForegroundIsBrave = false;
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
