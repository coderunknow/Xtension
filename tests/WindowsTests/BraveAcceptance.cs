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
                try { Exercise(bravePath, candidatePath, profile); }
                catch (Exception ex) { failure = ex; }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
            if (!worker.Join(180000)) { Console.WriteLine("FAIL Brave acceptance exceeded 180 seconds."); return 1; }
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
                        Wait(delegate { initial = UiaTabScanner.Scan(brave); return initial.Outcome == ScanOutcome.Ready && initial.Tabs.Count == 3; }, 20000,
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
                    Assert.AreEqual(TabGeometry.GetOverlayBounds(initial.SelectedHorizontalTab.Bounds, initial.Dpi), NativeMethods.GetWindowBounds(overlay), "real overlay position");
                    Console.WriteLine("PASS shipped candidate EXE displays overlay without taking Brave foreground");

                    for (int i = 0; i < 3; i++)
                    {
                        Rectangle previous = NativeMethods.GetWindowBounds(overlay);
                        Key(0x11, false); Key(0x09, false); Key(0x09, true); Key(0x11, true);
                        Wait(delegate
                        {
                            ScanResult current = UiaTabScanner.Scan(brave);
                            overlay = FindVisibleWindow(app.Id, true);
                            return current.Outcome == ScanOutcome.Ready && overlay != IntPtr.Zero
                                && NativeMethods.GetWindowBounds(overlay) != previous
                                && NativeMethods.GetWindowBounds(overlay) == TabGeometry.GetOverlayBounds(current.SelectedHorizontalTab.Bounds, current.Dpi);
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
                        ScanResult scan = UiaTabScanner.Scan(brave);
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
                    Wait(delegate { return UiaTabScanner.Scan(brave).Tabs.Count == 2; }, 7000, "actual click did not close exactly one tab");
                    Thread.Sleep(1500);
                    Assert.AreEqual(2, UiaTabScanner.Scan(brave).Tabs.Count, "only one close after click");
                    string afterSelected;
                    List<string> afterClose = CaptureTabIds(brave, out afterSelected);
                    Assert.IsFalse(afterClose.Contains(selectedId), "selected identity was removed");
                    foreach (string id in beforeClose)
                        if (id != selectedId) Assert.IsTrue(afterClose.Contains(id), "unselected identity survived");
                    Console.WriteLine("PASS actual mouse close removed only selected runtime identity, no consecutive close");

                    ShowWindow(brave, 6);
                    Wait(delegate { return FindVisibleWindow(app.Id, true) == IntPtr.Zero; }, 5000, "overlay survived minimize");
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
                    Wait(delegate { return UiaTabScanner.Scan(brave).Tabs.Count == 12; }, 10000, "many-tab fixture not ready");
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
                            ScanResult scan = UiaTabScanner.Scan(brave);
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

        private static void WaitForOverlay(int appId, IntPtr window, int milliseconds)
        {
            Wait(delegate
            {
                ScanResult scan = UiaTabScanner.Scan(window);
                IntPtr overlay = FindVisibleWindow(appId, true);
                return NativeMethods.GetForegroundRootWindow() == window && scan.Outcome == ScanOutcome.Ready
                    && overlay != IntPtr.Zero && NativeMethods.GetWindowBounds(overlay) == TabGeometry.GetOverlayBounds(scan.SelectedHorizontalTab.Bounds, scan.Dpi);
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
