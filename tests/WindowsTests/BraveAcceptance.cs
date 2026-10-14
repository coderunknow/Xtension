using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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
                    Wait(delegate { initial = UiaTabScanner.Scan(brave); return initial.Outcome == ScanOutcome.Ready && initial.Tabs.Count == 3; }, 20000,
                        "initial Brave scan did not confirm three selected-strip tabs");
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
                    ScanResult beforeClose = UiaTabScanner.Scan(brave);
                    int selectedIndex = beforeClose.Tabs.IndexOf(beforeClose.SelectedHorizontalTab);
                    Assert.IsTrue(selectedIndex >= 0, "selected tab before click");
                    Click(overlay);
                    Wait(delegate { return UiaTabScanner.Scan(brave).Tabs.Count == 2; }, 7000, "actual click did not close exactly one tab");
                    Thread.Sleep(1500);
                    Assert.AreEqual(2, UiaTabScanner.Scan(brave).Tabs.Count, "only one close after click");
                    Console.WriteLine("PASS actual overlay mouse click closed one fixture tab, no consecutive close");

                    ShowWindow(brave, 6);
                    Wait(delegate { return FindVisibleWindow(app.Id, true) == IntPtr.Zero; }, 5000, "overlay survived minimize");
                    ShowWindow(brave, 9);
                    SetForegroundWindow(brave);
                    Wait(delegate { return FindVisibleWindow(app.Id, true) != IntPtr.Zero; }, 7000, "overlay did not recover on restore");
                    Console.WriteLine("PASS minimize/restore recovery");
                }
                finally
                {
                    if (app != null) { if (!app.HasExited) { app.Kill(); app.WaitForExit(5000); } app.Dispose(); }
                    if (!browser.HasExited) { browser.Kill(); browser.WaitForExit(5000); }
                }
            }
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
