using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Automation;

namespace Xtension.Tests
{
    // Isolated runner profile only. No personal browser, no browsing titles/URLs in output.
    internal static class BraveAcceptance
    {
        private delegate bool WindowCallback(IntPtr hwnd, IntPtr state);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        public static int Run(string bravePath, string candidatePath, string profile)
        {
            Exception failure = null;
            Thread worker = new Thread(delegate()
            {
                try
                {
                    Exercise(bravePath, candidatePath, profile);
                    // Second phase: the same shipped binary, started from a settings file that selects the new
                    // hover mode and a non-default button size. It runs after the first app process is gone, because
                    // Xtension is single-instance by design.
                    ConfiguredBehaviour(bravePath, candidatePath, profile);
                }
                catch (Exception ex) { failure = ex; }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
            if (!worker.Join(300000)) { Console.WriteLine("FAIL Brave acceptance exceeded 300 seconds."); return 1; }
            if (failure != null)
            {
                Console.WriteLine("FAIL Brave acceptance: " + failure.GetType().Name + ": " + failure.Message);
                return 1;
            }
            Console.WriteLine("PASS isolated real-Brave smoke; not a full desktop/DPI acceptance matrix.");
            return 0;
        }

        private static void Exercise(string bravePath, string candidatePath, string profile)
        {
            string version = FileVersionInfo.GetVersionInfo(bravePath).ProductVersion;
            Console.WriteLine("Brave binary version=" + version + "; OS=" + Environment.OSVersion.Version + "; test pointer bytes=" + IntPtr.Size);
            using (Process browser = Process.Start(new ProcessStartInfo(bravePath,
                "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-background-networking --disable-component-update --disable-sync --new-window about:blank about:blank about:blank") { UseShellExecute = false }))
            {
                Process app = null;
                try
                {
                    IntPtr brave = IntPtr.Zero;
                    Wait(delegate { brave = FindVisibleWindow(browser.Id, false); return brave != IntPtr.Zero; }, 20000, "Brave window did not appear");
                    ShowWindow(brave, 3);
                    SetForegroundWindow(brave);
                    Wait(delegate { return NativeMethods.GetForegroundRootWindow() == brave; }, 5000, "runner could not foreground Brave");
                    Assert.IsTrue(ProcessIdentity.IsBraveWindow(brave, true), "real Brave identity");
                    ScanResult initial = null;
                    try
                    {
                        Wait(delegate { initial = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical); return initial.Outcome == ScanOutcome.Ready && initial.Tabs.Count == 3; }, 20000,
                            "initial Brave scan did not confirm three selected-strip tabs");
                    }
                    catch (AssertionFailure)
                    {
                        throw new AssertionFailure("Initial Brave scan: " + (initial == null ? "no result" : initial.Outcome + "; " + initial.Error + "; nodes=" + initial.NodesVisited + "; tabs=" + initial.Tabs.Count));
                    }
                    Console.WriteLine("PASS Brave already running: selected horizontal tab confirmed; nodes=" + initial.NodesVisited + "; dpi=" + initial.Dpi);
                    app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                    SetForegroundWindow(brave);
                    IntPtr overlay = IntPtr.Zero;
                    Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 10000, "candidate overlay did not appear");
                    Assert.AreEqual(TabGeometry.GetOverlayBounds(initial.SelectedHorizontalTab.Bounds, initial.Dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical), NativeMethods.GetWindowBounds(overlay), "real overlay position");
                    Console.WriteLine("PASS shipped candidate EXE displays overlay without taking Brave foreground");

                    for (int i = 0; i < 3; i++)
                    {
                        Rectangle previous = NativeMethods.GetWindowBounds(overlay);
                        Key(0x11, false); Key(0x09, false); Key(0x09, true); Key(0x11, true);
                        Wait(delegate
                        {
                            ScanResult current = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                            overlay = FindVisibleWindow(app.Id, true);
                            return current.Outcome == ScanOutcome.Ready && overlay != IntPtr.Zero
                                && NativeMethods.GetWindowBounds(overlay) != previous
                                && NativeMethods.GetWindowBounds(overlay) == TabGeometry.GetOverlayBounds(current.SelectedHorizontalTab.Bounds, current.Dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                        }, 7000, "overlay did not follow Ctrl+Tab");
                    }
                    Console.WriteLine("PASS three tab switches followed by actual overlay");

                    // A second browser window must not inherit the first window's confirmation.
                    Key(0x11, false); Key(0x4E, false); Key(0x4E, true); Key(0x11, true);
                    IntPtr second = IntPtr.Zero;
                    Wait(delegate
                    {
                        second = NativeMethods.GetForegroundRootWindow();
                        return second != brave && ProcessIdentity.IsBraveWindow(second, true);
                    }, 7000, "second Brave window did not become foreground");
                    WaitForOverlay(app.Id, second, 7000);
                    SetForegroundWindow(brave);
                    WaitForOverlay(app.Id, brave, 7000);
                    Console.WriteLine("PASS overlay follows two Brave windows");
                    // Later minimize acceptance expects NO eligible browser foreground. Leaving this second
                    // window open/normal lets Windows legitimately foreground it and show its own overlay.
                    ShowWindow(second, 6);
                    Wait(delegate { return NativeMethods.IsIconic(second); }, 5000, "secondary window did not minimize");
                    SetForegroundWindow(brave);
                    WaitForOverlay(app.Id, brave, 7000);

                    string readyName = @"Local\Xtension.FocusFixture." + Guid.NewGuid().ToString("N");
                    using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, readyName))
                    using (Process other = Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,
                        "--fixture " + readyName) { UseShellExecute = false, CreateNoWindow = true }))
                    {
                        try
                        {
                            Assert.IsTrue(ready.WaitOne(10000), "focus fixture ready");
                            IntPtr otherWindow = FindVisibleWindow(other.Id, false);
                            SetForegroundWindow(otherWindow);
                            Wait(delegate { return NativeMethods.GetForegroundRootWindow() == otherWindow && FindVisibleWindow(app.Id, true) == IntPtr.Zero; }, 5000, "overlay did not hide for other application");
                        }
                        finally { if (!other.HasExited) { other.Kill(); other.WaitForExit(5000); } }
                    }
                    SetForegroundWindow(brave);
                    WaitForOverlay(app.Id, brave, 7000);
                    Console.WriteLine("PASS focus away hides overlay; focus return recovers");

                    foreach (byte modifier in new byte[] { 0x10, 0x12, 0x5B })
                    {
                        SetForegroundWindow(brave);
                        Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 5000, "overlay before modifier");
                        Key(modifier, false);
                        try { Click(overlay); Thread.Sleep(1500); }
                        finally { Key(modifier, true); }
                        ScanResult scan = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                        Assert.AreEqual(3, scan.Tabs.Count, "modifier click must not close tab/window");
                        // Dismiss any OS menu opened by releasing Alt/Win, then restore Brave.
                        Key(0x1B, false); Key(0x1B, true);
                        SetForegroundWindow(brave);
                    }
                    Console.WriteLine("PASS Shift/Alt/Win clicks left all three fixture tabs intact");

                    Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 5000, "overlay before close");
                    string selectedId;
                    List<string> beforeClose = CaptureTabIds(brave, out selectedId);
                    Assert.IsTrue(selectedId != null, "unique selected identity before click");
                    Click(overlay);
                    Wait(delegate { return UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).Tabs.Count == 2; }, 7000, "actual click did not close exactly one tab");
                    Thread.Sleep(1500);
                    Assert.AreEqual(2, UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).Tabs.Count, "only one close after click");
                    string afterSelected;
                    List<string> afterClose = CaptureTabIds(brave, out afterSelected);
                    Assert.IsFalse(afterClose.Contains(selectedId), "selected identity was removed");
                    foreach (string id in beforeClose)
                        if (id != selectedId) Assert.IsTrue(afterClose.Contains(id), "unselected identity survived");
                    Console.WriteLine("PASS actual mouse close removed only selected runtime identity, no consecutive close");

                    ShowWindow(brave, 6);
                    try
                    {
                        Wait(delegate { return NativeMethods.IsIconic(brave) && NativeMethods.IsIconic(second)
                            && FindVisibleWindow(app.Id, true) == IntPtr.Zero; }, 5000, "overlay survived minimize with both browser windows minimized");
                    }
                    catch (AssertionFailure)
                    {
                        throw new AssertionFailure("Minimize state: " + DescribeMinimize(brave, second, overlay, app.Id));
                    }
                    Console.WriteLine("PASS minimized-window visibility: " + DescribeMinimize(brave, second, overlay, app.Id));
                    ShowWindow(brave, 9);
                    SetForegroundWindow(brave);
                    Wait(delegate { return FindVisibleWindow(app.Id, true) != IntPtr.Zero; }, 7000, "overlay did not recover on restore");
                    Console.WriteLine("PASS minimize/restore recovery");
                    ShowWindow(brave, 1);
                    Assert.IsTrue(NativeMethods.SetWindowPos(brave, IntPtr.Zero, 70, 80, 850, 600, NativeMethods.SWP_NOACTIVATE), "move/resize browser");
                    SetForegroundWindow(brave);
                    WaitForOverlay(app.Id, brave, 7000);
                    ShowWindow(brave, 3);
                    WaitForOverlay(app.Id, brave, 7000);
                    Console.WriteLine("PASS move/resize/maximize overlay geometry");

                    for (int i = 0; i < 10; i++)
                    {
                        Key(0x11, false); Key(0x54, false); Key(0x54, true); Key(0x11, true);
                        Thread.Sleep(100);
                    }
                    Wait(delegate { return UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical).Tabs.Count == 12; }, 10000, "many-tab fixture not ready");
                    WaitForOverlay(app.Id, brave, 10000);
                    Console.WriteLine("PASS selected horizontal tab with twelve tabs");

                    string longPage = Path.Combine(profile, "synthetic-long-title.html");
                    Directory.CreateDirectory(profile);
                    File.WriteAllText(longPage, "<html><head><title>" + new string('X', 300) + "</title></head><body>Fixture</body></html>");
                    using (Process launch = Process.Start(new ProcessStartInfo(bravePath,
                        "--user-data-dir=\"" + profile + "\" \"" + longPage + "\"") { UseShellExecute = false }))
                    {
                        Wait(delegate
                        {
                            ScanResult scan = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                            return scan.Outcome == ScanOutcome.Ready && scan.SelectedHorizontalTab.Name.Length >= 300;
                        }, 15000, "long-title fixture not selected");
                        WaitForOverlay(app.Id, brave, 7000);
                    }
                    Console.WriteLine("PASS long synthetic title does not prevent selected-tab overlay");

                }
                finally
                {
                    if (app != null) { if (!app.HasExited) { app.Kill(); app.WaitForExit(5000); } app.Dispose(); }
                    if (!browser.HasExited) { browser.Kill(); browser.WaitForExit(5000); }
                }
            }
        }

        /// <summary>
        /// Real-Brave acceptance for the dev3 settings: hover visibility, a configured × size, persistence across a
        /// restart while Brave stays open, and recovery from a corrupted settings file. The button is only ever
        /// clicked after a hover showed it, so this also proves that hovering cannot authorise a close by itself.
        /// </summary>
        private static void ConfiguredBehaviour(string bravePath, string candidatePath, string profile)
        {
            const int configuredInset = 5;
            int configuredSize = AppInfo.DefaultButtonSizeLogical;
            string settingsPath = AppPaths.SettingsFile;
            string original = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            AppPaths.EnsureDirectory();

            Process browser = null;
            Process app = null;
            try
            {
                browser = Process.Start(new ProcessStartInfo(bravePath,
                    "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-background-networking " +
                    "--disable-component-update --disable-sync --new-window about:blank about:blank about:blank") { UseShellExecute = false });
                IntPtr brave = IntPtr.Zero;
                Wait(delegate { brave = FindVisibleWindow(browser.Id, false); return brave != IntPtr.Zero; }, 20000, "configured-phase Brave window did not appear");
                ShowWindow(brave, 3);
                SetForegroundWindow(brave);
                Wait(delegate { return NativeMethods.GetForegroundRootWindow() == brave; }, 5000, "runner could not foreground Brave for the configured phase");

                // The real tab decides which configured sizes exist. A size it cannot contain is refused by the
                // product instead of being drawn outside the tab, so the phase configures the largest size that fits
                // and checks the refusal separately: asserting a visible button for an impossible size would be a
                // test of the fixture, not of the product.
                ScanResult probe = null;
                Wait(delegate
                {
                    probe = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                    return probe.Outcome == ScanOutcome.Ready && probe.Tabs.Count == 3;
                }, 20000, "default-size probe scan did not confirm three tabs");
                int tabHeight = probe.SelectedHorizontalTab.Bounds.Height;
                int largest = LargestFittingSize(probe.SelectedHorizontalTab.Bounds, probe.Dpi, configuredInset);
                if (largest > AppInfo.DefaultButtonSizeLogical) configuredSize = largest;
                Console.WriteLine("INFO the real selected tab is " + tabHeight + " px tall at dpi " + probe.Dpi
                    + "; the configured phase uses " + configuredSize + " px with inset " + configuredInset);

                if (TabGeometry.GetOverlayBounds(probe.SelectedHorizontalTab.Bounds, probe.Dpi, AppInfo.MaxButtonSizeLogical, configuredInset).IsEmpty)
                {
                    ScanResult refused = UiaTabScanner.Scan(brave, AppInfo.MaxButtonSizeLogical, configuredInset);
                    Assert.IsTrue(refused.Outcome == ScanOutcome.NoSelectedTab && refused.Key == ScanMessageKey.SelectedTabTooSmall,
                        "a " + AppInfo.MaxButtonSizeLogical + " px button cannot fit a " + tabHeight + " px tab, so the scan must fail closed with "
                        + ScanMessageKey.SelectedTabTooSmall + " (got " + refused.Outcome + "/" + refused.Key + ")");
                    Console.WriteLine("PASS a " + AppInfo.MaxButtonSizeLogical + " px button is refused on the real " + tabHeight
                        + " px tab (fail closed, " + refused.Key + ")");
                }

                File.WriteAllText(settingsPath, "overlayEnabled=true\r\nvisibilityMode=hover\r\nbuttonSize=" + configuredSize
                    + "\r\nbuttonInset=" + configuredInset + "\r\nhoverGraceMs=250\r\nscanIntervalMs=500\r\nlanguage=vi\r\n");

                ScanResult scan = null;
                Wait(delegate
                {
                    scan = UiaTabScanner.Scan(brave, configuredSize, configuredInset);
                    return scan.Outcome == ScanOutcome.Ready && scan.Tabs.Count == 3;
                }, 20000, "configured-phase scan did not confirm three tabs");
                Rectangle configured = TabGeometry.GetOverlayBounds(scan.SelectedHorizontalTab.Bounds, scan.Dpi, configuredSize, configuredInset);
                Assert.IsFalse(configured.IsEmpty, "the configured button must fit the real tab");
                Assert.IsTrue(configured.Width == TabGeometry.OverlaySize(scan.Dpi, configuredSize), "configured width follows the DPI-scaled size");
                TabInfo inactive = FirstInactive(scan);
                Assert.IsTrue(inactive != null, "the fixture needs at least one inactive tab");

                Rectangle braveBounds = NativeMethods.GetWindowBounds(brave);
                Point page = new Point(braveBounds.Left + braveBounds.Width / 2, braveBounds.Top + braveBounds.Height - 60);
                SetCursorPos(page.X, page.Y);

                app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                SetForegroundWindow(brave);
                Thread.Sleep(2500);
                Assert.IsTrue(FindVisibleWindow(app.Id, true) == IntPtr.Zero,
                    "hover mode must not show the button while the pointer is outside the selected tab");

                CursorTo(scan.SelectedHorizontalTab.Bounds);
                IntPtr overlay = IntPtr.Zero;
                try
                {
                    Wait(delegate
                    {
                        overlay = FindVisibleWindow(app.Id, true);
                        return overlay != IntPtr.Zero && NativeMethods.GetWindowBounds(overlay) == configured;
                    }, 8000, "hover did not show the configured button");
                }
                catch (AssertionFailure)
                {
                    throw new AssertionFailure("Hover show: " + DescribeHover(app.Id, brave, configured));
                }
                Console.WriteLine("PASS hover mode shows the configured " + configured.Width + "x" + configured.Height + " px button over the selected tab");

                CursorTo(configured);
                Thread.Sleep(700);
                Assert.IsTrue(FindVisibleWindow(app.Id, true) != IntPtr.Zero, "moving from the tab onto the button must keep it visible");
                Console.WriteLine("PASS moving the pointer from the tab onto the button keeps it visible");

                CursorTo(inactive.Bounds);
                Wait(delegate { return FindVisibleWindow(app.Id, true) == IntPtr.Zero; }, 5000,
                    "an inactive tab must not satisfy the hover rule");
                Console.WriteLine("PASS an inactive tab and the title bar never satisfy the hover rule");

                CursorTo(scan.SelectedHorizontalTab.Bounds);
                Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 8000, "hover did not re-show the button");
                Rectangle shown = NativeMethods.GetWindowBounds(overlay);
                Assert.AreEqual(configured, shown, "the hover-shown button uses the configured geometry");
                Click(overlay);
                Wait(delegate { return UiaTabScanner.Scan(brave, configuredSize, configuredInset).Tabs.Count == 2; }, 8000,
                    "a click on a hover-shown button must still close exactly one tab");
                Console.WriteLine("PASS hover click closes exactly one tab through the unchanged validation path");

                // Restart the application while Brave stays open: the persisted settings must be applied again.
                Kill(app);
                Thread.Sleep(500);
                app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                SetForegroundWindow(brave);
                ScanResult restarted = null;
                Wait(delegate
                {
                    restarted = UiaTabScanner.Scan(brave, configuredSize, configuredInset);
                    return restarted.Outcome == ScanOutcome.Ready && restarted.Tabs.Count == 2;
                }, 20000, "post-restart scan did not confirm the two remaining tabs");
                Rectangle afterRestart = TabGeometry.GetOverlayBounds(restarted.SelectedHorizontalTab.Bounds, restarted.Dpi, configuredSize, configuredInset);
                CursorTo(restarted.SelectedHorizontalTab.Bounds);
                Wait(delegate
                {
                    IntPtr window = FindVisibleWindow(app.Id, true);
                    return window != IntPtr.Zero && NativeMethods.GetWindowBounds(window) == afterRestart;
                }, 8000, "restarted application did not apply the persisted hover mode and size");
                Console.WriteLine("PASS persisted hover mode and " + configuredSize + " px size survive a restart with Brave still open");

                // A corrupted settings file must fall back to the documented defaults instead of failing to start.
                Kill(app);
                Thread.Sleep(500);
                File.WriteAllText(settingsPath, "overlayEnabled=maybe\r\nvisibilityMode=sideways\r\nbuttonSize=not-a-number\r\nhoverGraceMs=999999\r\n");
                app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                SetForegroundWindow(brave);
                ScanResult fallback = null;
                Wait(delegate
                {
                    fallback = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                    return fallback.Outcome == ScanOutcome.Ready;
                }, 20000, "fallback scan did not confirm a tab");
                Rectangle defaultGeometry = TabGeometry.GetOverlayBounds(fallback.SelectedHorizontalTab.Bounds, fallback.Dpi,
                    AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                // Defaults are always-show, so the button must appear with the pointer parked away from the tab strip.
                SetCursorPos(page.X, page.Y);
                Wait(delegate
                {
                    IntPtr window = FindVisibleWindow(app.Id, true);
                    return window != IntPtr.Zero && NativeMethods.GetWindowBounds(window) == defaultGeometry;
                }, 8000, "a corrupted settings file did not fall back to always-show with the default size");
                Console.WriteLine("PASS a corrupted settings file falls back to documented defaults (always-show, " + AppInfo.DefaultButtonSizeLogical + " px)");
            }
            finally
            {
                Kill(app);
                Kill(browser);
                try
                {
                    if (original == null) File.Delete(settingsPath);
                    else File.WriteAllText(settingsPath, original);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>
        /// Largest configured × size the real tab can contain at this DPI, so the acceptance phase measures a size
        /// Brave has room for instead of assuming the maximum always fits a 100 % browser tab.
        /// </summary>
        private static int LargestFittingSize(Rectangle tab, int dpi, int insetLogical)
        {
            for (int size = AppInfo.MaxButtonSizeLogical; size > AppInfo.MinButtonSizeLogical; size--)
            {
                if (!TabGeometry.GetOverlayBounds(tab, dpi, size, insetLogical).IsEmpty) return size;
            }
            return AppInfo.MinButtonSizeLogical;
        }

        private static void CursorTo(Rectangle bounds)
        {
            SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        }

        private static TabInfo FirstInactive(ScanResult scan)
        {
            foreach (TabInfo tab in scan.Tabs)
            {
                if (tab != scan.SelectedHorizontalTab && tab.IsHorizontalCandidate && !tab.IsOffscreen) return tab;
            }
            return null;
        }

        private static string DescribeHover(int appId, IntPtr brave, Rectangle expected)
        {
            IntPtr overlay = FindVisibleWindow(appId, true);
            return "expected=" + expected + "; overlay=" + overlay.ToInt64()
                + "; overlayVisible=" + (overlay != IntPtr.Zero && NativeMethods.IsWindowVisible(overlay))
                + "; overlayBounds=" + (overlay == IntPtr.Zero ? "none" : NativeMethods.GetWindowBounds(overlay).ToString())
                + "; foregroundIsBrave=" + (NativeMethods.GetForegroundRootWindow() == brave)
                + "; cursor=" + System.Windows.Forms.Cursor.Position;
        }

        private static void Kill(Process process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        private static string DescribeMinimize(IntPtr primary, IntPtr secondary, IntPtr priorOverlay, int appId)
        {
            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            IntPtr visible = FindVisibleWindow(appId, true);
            return "primaryIconic=" + NativeMethods.IsIconic(primary) + "; secondaryIconic=" + NativeMethods.IsIconic(secondary)
                + "; foreground=" + foreground.ToInt64() + "; foregroundIsPrimary=" + (foreground == primary)
                + "; foregroundIsSecondary=" + (foreground == secondary) + "; foregroundIconic=" + NativeMethods.IsIconic(foreground)
                + "; foregroundBrave=" + ProcessIdentity.IsBraveWindow(foreground, true)
                + "; priorOverlay=" + priorOverlay.ToInt64() + "; priorOverlayVisible=" + NativeMethods.IsWindowVisible(priorOverlay)
                + "; visibleSmallWindow=" + visible.ToInt64();
        }

        private static void WaitForOverlay(int appId, IntPtr window, int milliseconds)
        {
            Wait(delegate
            {
                ScanResult scan = UiaTabScanner.Scan(window, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                IntPtr overlay = FindVisibleWindow(appId, true);
                return NativeMethods.GetForegroundRootWindow() == window && scan.Outcome == ScanOutcome.Ready
                    && overlay != IntPtr.Zero && NativeMethods.GetWindowBounds(overlay) == TabGeometry.GetOverlayBounds(scan.SelectedHorizontalTab.Bounds, scan.Dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
            }, milliseconds, "foreground overlay did not match current scan");
        }

        private static List<string> CaptureTabIds(IntPtr window, out string selectedId)
        {
            // The fixture has exactly three blank pages, no arbitrary page-content accessibility tree.
            AutomationElementCollection tabs = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
            List<string> ids = new List<string>();
            selectedId = null;
            foreach (AutomationElement tab in tabs)
            {
                string id = string.Join(",", tab.GetRuntimeId());
                ids.Add(id);
                object pattern;
                Assert.IsTrue(tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern), "tab selection pattern");
                if (((SelectionItemPattern)pattern).Current.IsSelected)
                {
                    Assert.IsTrue(selectedId == null, "one selected identity");
                    selectedId = id;
                }
            }
            return ids;
        }

        private static void Key(byte key, bool up) { keybd_event(key, 0, up ? 2u : 0u, UIntPtr.Zero); }
        private static void Click(IntPtr overlay)
        {
            Rectangle bounds = NativeMethods.GetWindowBounds(overlay);
            Assert.IsFalse(bounds.IsEmpty, "click requires a visible overlay");
            SetCursorPos(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }
        private static IntPtr FindVisibleWindow(int processId, bool small)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr state)
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                Rectangle bounds = NativeMethods.GetWindowBounds(hwnd);
                if (pid == processId && NativeMethods.IsWindowVisible(hwnd) && !NativeMethods.IsIconic(hwnd)
                    && bounds.Width > 0 && bounds.Height > 0 && (!small || (bounds.Width <= 100 && bounds.Height <= 100)))
                { found = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        private static void Wait(Func<bool> condition, int milliseconds, string failure)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                if (condition()) return;
                Thread.Sleep(150);
            }
            throw new AssertionFailure(failure);
        }
    }
}
