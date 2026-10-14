using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Xtension.Tests
{
    internal static class WindowsEntry
    {
        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.EnablePerMonitorDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 4 && args[0] == "--brave") return BraveAcceptance.Run(args[1], args[2], args[3]);
            if (args.Length != 0 && args[0] == "--fixture")
            {
                Form fixture = new Form { Text = "Xtension synthetic UIA fixture", Location = new Point(20, 20), StartPosition = FormStartPosition.Manual, Size = new Size(700, 300) };
                TabControl tabs = new TabControl { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(160, 40) };
                tabs.TabPages.Add("Synthetic first");
                tabs.TabPages.Add("Synthetic second");
                tabs.SelectedIndex = 1;
                fixture.Controls.Add(tabs);
                fixture.Shown += delegate
                {
                    using (EventWaitHandle ready = EventWaitHandle.OpenExisting(args[1])) ready.Set();
                };
                Application.Run(fixture);
                return 0;
            }
            return TestRunner.Run();
        }

        [Test]
        public static void ExplicitCacheNavigationAndScannerOnRealWindowsUia()
        {
            string eventName = @"Local\Xtension.TestReady." + Guid.NewGuid().ToString("N");
            using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName))
            using (Process fixture = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--fixture " + eventName) { UseShellExecute = false, CreateNoWindow = true }))
            {
                try
                {
                    Assert.IsTrue(ready.WaitOne(10000), "fixture Shown handshake");
                    fixture.Refresh();
                    IntPtr hwnd = fixture.MainWindowHandle;
                    Assert.IsTrue(hwnd != IntPtr.Zero, "fixture HWND");
                    Exception failure = null;
                    Thread worker = new Thread(delegate()
                    {
                        try
                        {
                            CacheRequest cache = new CacheRequest();
                            cache.Add(AutomationElement.ControlTypeProperty);
                            using (cache.Activate())
                            {
                                AutomationElement root = AutomationElement.FromHandle(hwnd);
                                AutomationElement naked = TreeWalker.ControlViewWalker.GetFirstChild(root);
                                Assert.IsTrue(naked != null, "fixture child exists");
                                bool uncached = false;
                                try { ControlType ignored = naked.Cached.ControlType; }
                                catch (InvalidOperationException ex)
                                {
                                    uncached = true;
                                    Console.WriteLine("Legacy cache failure: " + ScanSafety.DescribeError("Cached.ControlType", ex));
                                }
                                Assert.IsTrue(uncached, "legacy overload must reproduce missing cache");
                                AutomationElement cached = TreeWalker.ControlViewWalker.GetFirstChild(root, cache);
                                Assert.IsTrue(cached.Cached.ControlType != null, "explicit cache overload works");
                            }
                            ScanResult scan = UiaTabScanner.Scan(hwnd);
                            Assert.AreEqual(ScanOutcome.Ready, scan.Outcome, "scanner on synthetic horizontal tabs: " + scan.Error + " " + scan.Message);
                            Assert.AreEqual(2, scan.Tabs.Count, "both siblings enumerated with cache");
                            Assert.IsTrue(scan.Tabs[1].Selected && !scan.Tabs[0].Selected, "cached selection pattern");
                        }
                        catch (Exception ex) { failure = ex; }
                    });
                    worker.IsBackground = true;
                    worker.SetApartmentState(ApartmentState.MTA);
                    worker.Start();
                    Assert.IsTrue(worker.Join(15000), "bounded UIA test deadline");
                    if (failure != null) throw new AssertionFailure(failure.ToString().Replace(Environment.NewLine, " | "));
                }
                finally
                {
                    if (!fixture.HasExited) { fixture.Kill(); fixture.WaitForExit(5000); }
                }
            }
        }

        [Test]
        public static void DashboardLayoutsAtSmallSizesAndScaledFonts()
        {
            foreach (float scale in new float[] { 1F, 1.25F, 1.5F, 2F })
            {
                using (MainForm form = new MainForm(null))
                {
                    form.PrepareForExit();
                    List<Control> controls = AllControls(form);
                    List<Font> fonts = new List<Font>();
                    try
                    {
                        foreach (Control control in controls) fonts.Add(new Font(control.Font.FontFamily, control.Font.Size * scale, control.Font.Style));
                        for (int i = 0; i < controls.Count; i++) controls[i].Font = fonts[i];
                        form.Show();
                        foreach (Size size in new Size[] { new Size(640, 440), new Size(360, 280), new Size(1000, 650) })
                        {
                            form.Size = size;
                            form.Render(new ActiveTabSnapshot { Enabled = true, BraveInForeground = true,
                                HiddenReason = "Quét UI Automation thất bại. Đã ẩn nút ×. " + new string('x', 160),
                                Scan = ScanResult.Idle(IntPtr.Zero, ScanOutcome.Error, "Quét UI Automation thất bại. Đã ẩn nút ×.") });
                            form.PerformLayout();
                            Application.DoEvents();
                            foreach (Control control in controls)
                            {
                                WrappingLabel label = control as WrappingLabel;
                                if (label != null)
                                {
                                    int needed = label.GetPreferredSize(new Size(label.Width, 0)).Height;
                                    Assert.IsTrue(label.Height >= needed, "wrapped label height at " + scale + ": " + label.Height + " < " + needed);
                                }
                                if (control is Button || control is CheckBox)
                                    Assert.IsTrue(control.Parent.ClientRectangle.Contains(control.Bounds), "action not clipped by flow row at " + scale + ": " + control.GetType().Name + " " + control.Bounds + " in " + control.Parent.ClientRectangle);
                            }
                        }
                    }
                    finally { form.Close(); foreach (Font font in fonts) font.Dispose(); }
                }
            }
        }

        [Test]
        public static void DiagnosticsRefreshesErrorAndCountersWithoutOutcomeChange()
        {
            using (DiagnosticsForm form = new DiagnosticsForm())
            {
                form.Show();
                ScanResult scan = ScanResult.Idle(IntPtr.Zero, ScanOutcome.Error, "Failure");
                ActiveTabSnapshot snapshot = new ActiveTabSnapshot { Enabled = true, Scan = scan, HiddenReason = "Hidden" };
                scan.Error = "First operation";
                form.Render(snapshot);
                scan.Error = "Second operation HRESULT 0x80004005";
                scan.ElapsedMs = 72;
                form.Render(snapshot);
                bool found = false;
                foreach (Control control in AllControls(form))
                {
                    TextBox text = control as TextBox;
                    if (text != null && text.Text.Contains("Second operation"))
                    {
                        found = true;
                        Assert.IsTrue(text.Text.Contains("72"), "timing updated");
                        Assert.IsTrue(text.Multiline && text.ScrollBars == ScrollBars.Vertical && text.ReadOnly, "accessible diagnostics");
                    }
                }
                Assert.IsTrue(found, "changed error must re-render");
            }
        }

        [Test]
        public static void OverlayShowHideAndInputStructure()
        {
            Assert.AreEqual(IntPtr.Size == 8 ? 40 : 28, KeyboardInjector.InputStructureSize, "Win32 INPUT layout");
            using (CloseButtonOverlay overlay = new CloseButtonOverlay())
            {
                string error;
                Rectangle bounds = new Rectangle(100, 100, 26, 26);
                Assert.IsTrue(overlay.ShowAt(bounds, out error), "native show: " + error);
                Assert.AreEqual(bounds, NativeMethods.GetWindowBounds(overlay.Handle), "physical bounds");
                overlay.Hide();
                Assert.IsTrue(overlay.ShowAt(bounds, out error), "re-show at unchanged bounds");
                Assert.IsFalse(overlay.ShowAt(Rectangle.Empty, out error), "invalid bounds hidden");
                Assert.IsFalse(overlay.Visible, "fail closed");
            }
        }

        [Test]
        public static void WorkerBusyErrorRecoveryAndDisposalAreBounded()
        {
            using (ManualResetEvent entered = new ManualResetEvent(false))
            using (ManualResetEvent release = new ManualResetEvent(false))
            using (AutoResetEvent delivered = new AutoResetEvent(false))
            {
                int calls = 0;
                ScanCompletion last = null;
                using (ScanWorker worker = new ScanWorker("Synthetic scanner", delegate(ScanCompletion completion)
                {
                    last = completion;
                    delivered.Set();
                }, delegate(IntPtr window)
                {
                    Assert.AreEqual(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState(), "UIA worker apartment");
                    int call = Interlocked.Increment(ref calls);
                    entered.Set();
                    if (!release.WaitOne(5000)) throw new TimeoutException("fixture deadline");
                    if (call == 1) throw new InvalidOperationException("PRIVATE provider message");
                    return ScanResult.Idle(window, ScanOutcome.NoTabs, "Synthetic recovery");
                }))
                {
                    try
                    {
                        ScanRequest first = new ScanRequest(1, ScanKind.Overlay, 0, new IntPtr(1), Point.Empty);
                        Assert.IsTrue(worker.TrySubmit(first), "first request accepted");
                        Assert.IsTrue(entered.WaitOne(5000), "worker started");
                        Assert.IsFalse(worker.TrySubmit(first), "busy lane refuses second request");
                        release.Set();
                        Assert.IsTrue(delivered.WaitOne(5000), "error delivered");
                        Assert.AreEqual(ScanOutcome.Error, last.Result.Outcome, "exception fails closed");
                        Assert.IsFalse(last.Result.Error.Contains("PRIVATE"), "provider message redacted");
                        Assert.IsTrue(worker.TrySubmit(new ScanRequest(2, ScanKind.Overlay, 0, new IntPtr(1), Point.Empty)), "error does not wedge lane");
                        Assert.IsTrue(delivered.WaitOne(5000), "recovery delivered");
                        Assert.AreEqual(ScanOutcome.NoTabs, last.Result.Outcome, "recovery outcome");
                        worker.Dispose();
                        worker.Dispose();
                        Assert.IsFalse(worker.TrySubmit(first), "disposed worker refuses submissions");
                    }
                    finally { release.Set(); }
                }
            }
        }

        [Test]
        public static void ControllerRejectsOldEpochAndDashboardCannotShowOverlay()
        {
            using (Control invoker = new Control())
            using (CloseButtonOverlay overlay = new CloseButtonOverlay())
            {
                invoker.CreateControl();
                using (ActiveTabController controller = new ActiveTabController(overlay, invoker, true))
                {
                    System.Reflection.MethodInfo complete = typeof(ActiveTabController).GetMethod("OnScanCompleted",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ScanRequest old = new ScanRequest(1, ScanKind.Dashboard, 0, new IntPtr(1), Point.Empty);
                    ScanResult result = ScanResult.Idle(old.Window, ScanOutcome.Ready, "Synthetic dashboard");
                    complete.Invoke(controller, new object[] { new ScanCompletion(old, result) });
                    Assert.IsTrue(controller.GetSnapshot().Scan == result, "dashboard accepts diagnostics only");
                    Assert.IsFalse(overlay.Visible, "dashboard can never authorize visibility");
                    controller.SetEnabled(false);
                    controller.SetEnabled(true);
                    complete.Invoke(controller, new object[] { new ScanCompletion(old, result) });
                    Assert.IsTrue(controller.GetSnapshot().Scan == null, "old epoch cannot restore confirmation");
                    Assert.IsFalse(overlay.Visible, "old result remains hidden");
                }
            }
        }

        private static List<Control> AllControls(Control root)
        {
            List<Control> controls = new List<Control>();
            foreach (Control child in root.Controls)
            {
                controls.Add(child);
                controls.AddRange(AllControls(child));
            }
            return controls;
        }
    }
}
