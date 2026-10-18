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
        private static BrowserKind _kind;
        private delegate bool WindowCallback(IntPtr hwnd, IntPtr state);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        public static int Run(string bravePath, string candidatePath, string profile, bool requireRefusal = false)
        {
            BrowserDescriptor descriptor = BrowserCatalog.ForExecutable(Path.GetFileName(bravePath));
            if (descriptor == null) return 2;
            _kind = descriptor.Kind;
            Console.WriteLine("PASS browser fixture token=" + descriptor.Token);
            Exception failure = null;
            Thread worker = new Thread(delegate()
            {
                try
                {
                    if (requireRefusal)
                    {
                        ExerciseRefusal(bravePath, candidatePath, profile);
                        return;
                    }
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
            Console.WriteLine(requireRefusal ? "PASS unsupported browser refusal only; NOT support acceptance."
                : "PASS isolated real-browser smoke; not a full desktop/DPI acceptance matrix.");
            return 0;
        }

        private static void ExerciseRefusal(string browserPath, string candidatePath, string profile)
        {
            BrowserDescriptor descriptor = BrowserCatalog.ForExecutable(Path.GetFileName(browserPath));
            Assert.IsTrue(descriptor != null && !descriptor.ScannerImplemented
                && descriptor.Support == BrowserSupport.Unsupported, "explicit quarantine, not an acceptance shortcut");
            AppPaths.EnsureDirectory();
            string original = File.Exists(AppPaths.SettingsFile) ? File.ReadAllText(AppPaths.SettingsFile) : null;
            Process app = null;
            try
            {
                using (Process browser = Process.Start(new ProcessStartInfo(browserPath,
                    "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-background-networking --new-window about:blank about:blank") { UseShellExecute = false }))
                {
                    try
                    {
                        IntPtr window = IntPtr.Zero;
                        Wait(delegate { window = FindVisibleWindow(browser.Id, false); return window != IntPtr.Zero; }, 20000, "unsupported browser fixture window");
                        ShowWindow(window, 3);
                        SetForegroundWindow(window);
                        Wait(delegate { return NativeMethods.GetForegroundRootWindow() == window; }, 5000, "foreground unsupported fixture");
                        Assert.AreEqual(descriptor.Kind, ProcessIdentity.GetBrowser(window, true), "exact process identified without granting support");
                        Assert.IsFalse(ProcessIdentity.IsSupportedBrowser(window, true), "quarantined process is not supported");
                        ScanResult refused = UiaTabScanner.Scan(window, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                        Assert.AreEqual(ScanOutcome.ContractUnimplemented, refused.Outcome, "scanner refuses before traversal");
                        Assert.AreEqual(0, refused.NodesVisited, "no UIA tree walked for unsupported browser");
                        Assert.IsTrue(refused.SelectedHorizontalTab == null, "no selectable close target");
                        File.WriteAllText(AppPaths.SettingsFile, "browsers=" + descriptor.Token + "\r\n");
                        app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                        SetForegroundWindow(window);
                        Stopwatch observe = Stopwatch.StartNew();
                        while (observe.ElapsedMilliseconds < 3500)
                        {
                            Assert.IsFalse(app.HasExited, "packaged application remains healthy");
                            Assert.AreEqual(IntPtr.Zero, FindVisibleWindow(app.Id, true), "no clickable overlay despite forced unsupported token");
                            Thread.Sleep(50);
                        }
                        Assert.IsTrue(LastStartupLine() != null, "packaged application initialized");
                        Console.WriteLine("PASS actual packaged EXE refuses unsupported " + descriptor.Token
                            + "; exact detection only; no scan or overlay even with forced settings; OS=" + Environment.OSVersion.Version + "; pointerBytes=" + IntPtr.Size);
                    }
                    finally { Kill(app); if (!browser.HasExited) { browser.Kill(); browser.WaitForExit(5000); } }
                }
            }
            finally
            {
                if (original == null) File.Delete(AppPaths.SettingsFile);
                else File.WriteAllText(AppPaths.SettingsFile, original);
            }
        }

        private static void Exercise(string bravePath, string candidatePath, string profile)
        {
            string version = FileVersionInfo.GetVersionInfo(bravePath).ProductVersion;
            Console.WriteLine("Browser binary version=" + version + "; OS=" + Environment.OSVersion.Version + "; test pointer bytes=" + IntPtr.Size);
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
                    Assert.IsTrue(ProcessIdentity.IsSupportedBrowser(brave, true), "real Brave identity");
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
                    AppPaths.EnsureDirectory();
                    if (_kind != BrowserKind.Brave)
                    {
                        File.WriteAllText(AppPaths.SettingsFile, "browsers=brave\r\n");
                        app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                        SetForegroundWindow(brave);
                        Thread.Sleep(2500);
                        Assert.IsTrue(FindVisibleWindow(app.Id, true) == IntPtr.Zero, "disabled browser has no overlay");
                        Kill(app);
                        Thread.Sleep(500);
                        Console.WriteLine("PASS Brave-only settings refuse the new browser before opt in");
                    }
                    File.WriteAllText(AppPaths.SettingsFile, "browsers=" + BrowserCatalog.Token(_kind) + "\r\n");
                    app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                    SetForegroundWindow(brave);
                    IntPtr overlay = IntPtr.Zero;
                    Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 10000, "candidate overlay did not appear");
                    Assert.AreEqual(TabGeometry.GetOverlayBounds(initial.SelectedHorizontalTab.Bounds, initial.Dpi, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical), NativeMethods.GetWindowBounds(overlay), "real overlay position");
                    Console.WriteLine("PASS shipped candidate EXE displays overlay without taking Brave foreground");
                    // The icon evidence comes from the running shipped EXE, not from the sources: its startup line
                    // states how many frames the embedded resource has and where the tray icon came from.
                    string started = LastStartupLine();
                    Assert.IsTrue(started != null, "the shipped EXE wrote its startup line");
                    Assert.IsTrue(started.Contains("Icon: frames=9"), "the shipped EXE carries the nine-frame icon resource: " + started);
                    Assert.IsTrue(started.Contains("tray source=embedded"), "the shipped EXE uses the embedded icon for the tray: " + started);
                    // The expected token comes from the product's own formatter, so the assertion cannot pass on a
                    // typo in the log line and cannot break when the line is reworded. It fails if the shipped
                    // default ever stops being the verified browser.
                    string expectedBrowsers = "browsers " + BrowserCatalog.MaskTokenList(BrowserCatalog.Mask(_kind));
                    Assert.IsTrue(started.Contains(expectedBrowsers),
                        "the shipped EXE starts with the verified browser enabled (expected '" + expectedBrowsers + "'): " + started);
                    Console.WriteLine("INFO shipped EXE icon and tray source: " + started);

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

                    string beforeStressSelected;
                    List<string> beforeStress = CaptureTabIds(brave, out beforeStressSelected);
                    for (int index = 0; index < 24; index++)
                    {
                        Key(0x11, false); Key(0x09, false); Key(0x09, true); Key(0x11, true);
                        Thread.Sleep(25);
                    }
                    WaitForOverlay(app.Id, brave, 7000);
                    string afterStressSelected;
                    List<string> afterStress = CaptureTabIds(brave, out afterStressSelected);
                    Assert.AreEqual(beforeStress.Count, afterStress.Count, "rapid switching never closes a tab");
                    foreach (string id in beforeStress) Assert.IsTrue(afterStress.Contains(id), "every original tab survives switching");
                    Console.WriteLine("PASS 24 rapid selection switches preserve every tab and converge to validated overlay");

                    // A second browser window must not inherit the first window's confirmation.
                    Key(0x11, false); Key(0x4E, false); Key(0x4E, true); Key(0x11, true);
                    IntPtr second = IntPtr.Zero;
                    Wait(delegate
                    {
                        second = NativeMethods.GetForegroundRootWindow();
                        return second != brave && ProcessIdentity.IsSupportedBrowser(second, true);
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
            // A second Brave launch must not inherit the first phase's session: a profile that was killed mid-run can
            // restore extra tabs, and this phase reads the tab count as its fixture. A private directory that is
            // emptied before launch keeps the fixture exactly the three tabs the command line asks for.
            string browserProfile = profile + "-configured";
            try
            {
                if (Directory.Exists(browserProfile)) Directory.Delete(browserProfile, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            string original = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            AppPaths.EnsureDirectory();

            Process browser = null;
            Process app = null;
            try
            {
                browser = Process.Start(new ProcessStartInfo(bravePath,
                    "--user-data-dir=\"" + browserProfile + "\" --no-first-run --no-default-browser-check --disable-background-networking " +
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
                try
                {
                    Wait(delegate
                    {
                        probe = UiaTabScanner.Scan(brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                        return probe.Outcome == ScanOutcome.Ready && probe.Tabs.Count >= 2;
                    }, 20000, "default-size probe scan did not confirm a usable tab strip");
                }
                catch (AssertionFailure)
                {
                    throw new AssertionFailure("Configured-phase probe scan: " + DescribeScan(probe));
                }
                int fixtureTabs = probe.Tabs.Count;
                Console.WriteLine("INFO the configured-phase fixture has " + fixtureTabs + " tabs at dpi " + probe.Dpi);
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
                    + "\r\nbuttonInset=" + configuredInset + "\r\nhoverGraceMs=250\r\nscanIntervalMs=500\r\nlanguage=vi\r\nbrowsers=" + BrowserCatalog.Token(_kind) + "\r\n");

                ScanResult scan = null;
                try
                {
                    Wait(delegate
                    {
                        scan = UiaTabScanner.Scan(brave, configuredSize, configuredInset);
                        return scan.Outcome == ScanOutcome.Ready && scan.Tabs.Count == fixtureTabs;
                    }, 20000, "configured-size scan did not confirm the fixture tabs");
                }
                catch (AssertionFailure)
                {
                    throw new AssertionFailure("Configured-size scan at " + configuredSize + " px: " + DescribeScan(scan));
                }
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
                    Stopwatch hoverShow = Stopwatch.StartNew();
                    Wait(delegate
                    {
                        overlay = FindVisibleWindow(app.Id, true);
                        return overlay != IntPtr.Zero && NativeMethods.GetWindowBounds(overlay) == configured;
                    }, 8000, "hover did not show the configured button");
                    hoverShow.Stop();
                    Console.WriteLine("INFO latency hover-first-show pointerOutsideToVisibleMs=" + hoverShow.ElapsedMilliseconds
                        + " (includes the harness moving the pointer and the confirmation scan)");
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

                // Pointer-entry-to-visible, measured on the real browser: park the pointer outside the tab strip
                // until the × is gone, then time how long the shipped EXE takes to show it again after the pointer
                // enters the confirmed tab. This is the number the dev4 requirement asks for; it is reported whether
                // it is good or bad, and a regression to "never appears again" still fails the run.
                MeasurePointerEntryLatency(app.Id, page, scan.SelectedHorizontalTab.Bounds, "hover-reentry");
                ReportScanLatency(brave, configuredSize, configuredInset, "real-browser-uia");

                CursorTo(scan.SelectedHorizontalTab.Bounds);
                Wait(delegate { overlay = FindVisibleWindow(app.Id, true); return overlay != IntPtr.Zero; }, 8000, "hover did not re-show the button");
                Rectangle shown = NativeMethods.GetWindowBounds(overlay);
                Assert.AreEqual(configured, shown, "the hover-shown button uses the configured geometry");
                Click(overlay);
                Wait(delegate { return UiaTabScanner.Scan(brave, configuredSize, configuredInset).Tabs.Count == fixtureTabs - 1; }, 8000,
                    "a click on a hover-shown button must still close exactly one tab");
                Console.WriteLine("PASS hover click closes exactly one tab through the unchanged validation path");

                // Restart the application while Brave stays open: the persisted settings must be applied again.
                Kill(app);
                Thread.Sleep(500);
                app = Process.Start(new ProcessStartInfo(candidatePath) { UseShellExecute = false });
                SetForegroundWindow(brave);
                ScanResult restarted = null;
                try
                {
                    Wait(delegate
                    {
                        restarted = UiaTabScanner.Scan(brave, configuredSize, configuredInset);
                        return restarted.Outcome == ScanOutcome.Ready && restarted.Tabs.Count == fixtureTabs - 1;
                    }, 20000, "post-restart scan did not confirm the remaining tabs");
                }
                catch (AssertionFailure)
                {
                    throw new AssertionFailure("Post-restart scan: " + DescribeScan(restarted));
                }
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
                File.WriteAllText(settingsPath, "overlayEnabled=maybe\r\nvisibilityMode=sideways\r\nbuttonSize=not-a-number\r\nhoverGraceMs=999999\r\nbrowsers=" + BrowserCatalog.Token(_kind) + "\r\n");
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
        /// The most recent startup line of the real application, which states the version, the effective settings, the
        /// icon frame count and the tray icon source. Counts and settings only; no title or URL is involved.
        /// </summary>
        private static string LastStartupLine()
        {
            try
            {
                if (!File.Exists(AppPaths.LogFile)) return null;
                string[] lines = File.ReadAllLines(AppPaths.LogFile);
                for (int index = lines.Length - 1; index >= 0; index--)
                {
                    if (lines[index].Contains(" started. Overlay ")) return lines[index];
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return null;
        }

        /// <summary>
        /// Measures pointer-entry-to-visible on a real browser: parks the pointer away until the × is gone, then times
        /// how long the shipped EXE needs to show it again after the pointer enters the confirmed selected tab. The
        /// number is printed as evidence; a ceiling assertion keeps a regression to "never appears" from passing.
        /// </summary>
        private static void MeasurePointerEntryLatency(int appId, Point away, Rectangle tab, string label)
        {
            SetCursorPos(away.X, away.Y);
            try
            {
                Wait(delegate { return FindVisibleWindow(appId, true) == IntPtr.Zero; }, 5000,
                    "the × did not hide after the pointer left the tab strip");
            }
            catch (AssertionFailure)
            {
                throw new AssertionFailure("Latency measurement requires the hover button to hide with the pointer away");
            }
            Stopwatch watch = Stopwatch.StartNew();
            SetCursorPos(tab.Left + tab.Width / 2, tab.Top + tab.Height / 2);
            Wait(delegate { return FindVisibleWindow(appId, true) != IntPtr.Zero; }, 5000,
                "the × did not appear after the pointer entered the confirmed tab strip");
            watch.Stop();
            long milliseconds = watch.ElapsedMilliseconds;
            Console.WriteLine("INFO latency " + label + " pointerToVisibleMs=" + milliseconds);
            Assert.IsTrue(milliseconds < 3000, "pointer entry to a visible × must stay under 3000 ms (measured "
                + milliseconds + " ms)");
        }

        /// <summary>Ten harness-side UI Automation calls against the real browser, reported as p50/p95/max.</summary>
        private static void ReportScanLatency(IntPtr brave, int size, int inset, string label)
        {
            List<long> samples = new List<long>();
            for (int index = 0; index < 10; index++)
            {
                Stopwatch watch = Stopwatch.StartNew();
                ScanResult result = UiaTabScanner.Scan(brave, size, inset);
                watch.Stop();
                Assert.AreEqual(ScanOutcome.Ready, result.Outcome, "every latency sample must confirm a selected tab");
                samples.Add(watch.ElapsedMilliseconds);
            }
            samples.Sort();
            long p50 = samples[samples.Count / 2];
            long p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
            Console.WriteLine("INFO latency " + label + " samples=" + samples.Count + " p50Ms=" + p50
                + " p95Ms=" + p95 + " maxMs=" + samples[samples.Count - 1] + " (harness-side scan of a real browser)");
        }

        /// <summary>One line explaining what a scan produced, so a timed-out wait is diagnosable from the annotation.</summary>
        private static string DescribeScan(ScanResult scan)
        {
            if (scan == null) return "no result";
            return scan.Outcome + "; key=" + scan.Key + "; tabs=" + scan.Tabs.Count + "; nodes=" + scan.NodesVisited
                + "; elapsedMs=" + scan.ElapsedMs + (scan.Error == null ? string.Empty : "; error=" + scan.Error);
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
                + "; foregroundBrave=" + ProcessIdentity.IsSupportedBrowser(foreground, true)
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
