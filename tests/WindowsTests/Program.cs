using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Xtension.Tests
{
    internal static class WindowsEntry
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);

        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.EnablePerMonitorDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 4 && (args[0] == "--brave" || args[0] == "--refuse-browser"))
                return BraveAcceptance.Run(args[1], args[2], args[3], args[0] == "--refuse-browser");
            // Resource measurement of a shipped EXE, used by the performance CI job. Never a functional test.
            if (args.Length >= 4 && args[0] == "--measure") return Measurement.Run(args);
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
        public static void TrayContextShutsDownOwnedResourcesWithoutRegistryWrites()
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Xtension-lifecycle-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    SettingsStore store = new SettingsStore(System.IO.Path.Combine(directory, "settings.ini"));
                    using (TrayAppContext context = new TrayAppContext(store, AppSettings.Defaults()))
                    {
                        System.Reflection.FieldInfo overlayField = typeof(TrayAppContext).GetField("_overlay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        System.Reflection.FieldInfo invokerField = typeof(TrayAppContext).GetField("_invoker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        CloseButtonOverlay overlay = (CloseButtonOverlay)overlayField.GetValue(context);
                        Control invoker = (Control)invokerField.GetValue(context);
                        Assert.IsFalse(overlay.IsDisposed, "constructed context owns live overlay");
                        context.ExitThread();
                        Assert.IsTrue(overlay.IsDisposed, "exit disposes overlay");
                        Assert.IsTrue(invoker.IsDisposed, "exit disposes marshal control");
                    }
                }
            }
            finally { System.IO.Directory.Delete(directory, true); }
        }

        [Test]
        public static void SingleInstanceRefusesDuplicateAndDeliversDashboardSignal()
        {
            using (SingleInstance primary = SingleInstance.Acquire())
            using (AutoResetEvent delivered = new AutoResetEvent(false))
            {
                Assert.IsFalse(primary.Unavailable || primary.IsSecondary, "first instance holds exclusion");
                using (ShowDashboardListener listener = new ShowDashboardListener(delegate { delivered.Set(); }))
                using (SingleInstance secondary = SingleInstance.Acquire())
                {
                    Assert.IsTrue(secondary.IsSecondary && !secondary.Unavailable, "duplicate cannot create another active application");
                    Assert.IsTrue(secondary.SignalExisting(), "secondary can signal primary");
                    Assert.IsTrue(delivered.WaitOne(5000), "primary receives dashboard signal");
                }
            }
        }

        [Test]
        public static void SelectionWatcherResubscribesAfterReturningToSameWindow()
        {
            using (Form fixture = new Form())
            using (TabControl tabs = new TabControl())
            {
                tabs.TabPages.Add("Synthetic");
                fixture.Controls.Add(tabs);
                fixture.Show();
                using (TabSelectionWatcher watcher = new TabSelectionWatcher(delegate { }))
                {
                    watcher.SetTarget(fixture.Handle);
                    PumpUntil(delegate { return watcher.Snapshot().Registered; }, "first subscription");
                    long first = watcher.Snapshot().Registrations;
                    watcher.SetTarget(IntPtr.Zero);
                    PumpUntil(delegate { return !watcher.Snapshot().Registered; }, "unsubscription");
                    watcher.SetTarget(fixture.Handle);
                    PumpUntil(delegate { return watcher.Snapshot().Registered && watcher.Snapshot().Registrations > first; }, "return to same HWND must resubscribe");
                }
            }
        }

        private static void PumpUntil(Func<bool> condition, string reason)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 10000)
            {
                Application.DoEvents();
                if (condition()) return;
                Thread.Sleep(20);
            }
            Assert.IsTrue(false, reason);
        }

        [Test]
        public static void OverlayPressCannotSurviveTargetOrGeometryChange()
        {
            using (CloseButtonOverlay overlay = new CloseButtonOverlay())
            {
                string failure;
                overlay.ShowAt(new Rectangle(100, 100, 26, 26), out failure);
                int clicks = 0;
                overlay.Clicked += delegate { clicks++; };
                System.Reflection.MethodInfo down = typeof(CloseButtonOverlay).GetMethod("OnMouseDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.MethodInfo up = typeof(CloseButtonOverlay).GetMethod("OnMouseUp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                MouseEventArgs mouse = new MouseEventArgs(MouseButtons.Left, 1, 13, 13, 0);
                down.Invoke(overlay, new object[] { mouse });
                overlay.CancelPendingClick();
                up.Invoke(overlay, new object[] { mouse });
                Assert.AreEqual(0, clicks, "identity change cancels a held press even at identical bounds");
                down.Invoke(overlay, new object[] { mouse });
                overlay.ShowAt(new Rectangle(101, 100, 26, 26), out failure);
                up.Invoke(overlay, new object[] { mouse });
                Assert.AreEqual(0, clicks, "move cancels a held press");
                down.Invoke(overlay, new object[] { mouse });
                up.Invoke(overlay, new object[] { mouse });
                Assert.AreEqual(1, clicks, "unchanged target still reports a normal click");
            }
        }

        [Test]
        public static void ControllerSettingsSnapshotMatchesAppliedValuesAndIsIsolated()
        {
            using (Control invoker = new Control())
            using (CloseButtonOverlay overlay = new CloseButtonOverlay())
            {
                invoker.CreateControl();
                AppSettings initial = AppSettings.Defaults();
                using (ActiveTabController controller = new ActiveTabController(overlay, invoker, initial))
                {
                    initial.ButtonSizeLogical = 19;
                    Assert.AreEqual(AppInfo.DefaultButtonSizeLogical, controller.Settings.ButtonSizeLogical, "constructor copies ownership");
                    AppSettings changed = AppSettings.Defaults();
                    changed.Language = AppLanguage.English;
                    changed.ButtonColor = OverlayColor.Red;
                    changed.ButtonBorder = false;
                    changed.ButtonOpacityPercent = 55;
                    changed.OverlayEnabled = false;
                    controller.ApplySettings(changed);
                    Assert.IsTrue(controller.GetSnapshot().Settings.Matches(changed), "all applied fields reach diagnostics");
                    AppSettings exposed = controller.Settings;
                    exposed.ButtonSizeLogical = 18;
                    Assert.AreEqual(AppInfo.DefaultButtonSizeLogical, controller.Settings.ButtonSizeLogical, "getter cannot mutate owner");
                }
            }
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
                    // The 15 s deadline failed once on a loaded runner and the message said nothing about where the
                    // time went. Each phase now records its own duration, so the next timeout identifies the step
                    // that was slow - and says whether it was the product scan or the operating system's UI
                    // Automation plumbing underneath it.
                    List<string> phases = new List<string>();
                    Stopwatch phaseClock = Stopwatch.StartNew();
                    Action<string> phase = delegate(string name)
                    {
                        lock (phases) phases.Add(name + "=" + phaseClock.ElapsedMilliseconds + "ms");
                        phaseClock.Restart();
                    };
                    Thread worker = new Thread(delegate()
                    {
                        try
                        {
                            phase("thread-start");
                            CacheRequest cache = new CacheRequest();
                            cache.Add(AutomationElement.ControlTypeProperty);
                            using (cache.Activate())
                            {
                                phase("cache-activate");
                                AutomationElement root = AutomationElement.FromHandle(hwnd);
                                phase("from-handle");
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
                            // The fixture is a synthetic window, not a browser: the identity gate must refuse it.
                            // That refusal is asserted first, because it is what keeps a window that merely looks
                            // like a browser from being scanned with Brave's assumptions.
                            phase("legacy-cache");
                            ScanResult refused = UiaTabScanner.Scan(hwnd, AppInfo.DefaultButtonSizeLogical,
                                AppInfo.DefaultButtonInsetLogical);
                            phase("refusal-scan");
                            Assert.AreEqual(ScanOutcome.ContractUnimplemented, refused.Outcome,
                                "a window that is not an enabled browser is refused: " + refused.Outcome);
                            Assert.AreEqual(0, refused.Tabs.Count, "a refused scan exposes no tabs");
                            Assert.IsTrue(refused.SelectedHorizontalTab == null, "a refused scan exposes no selected tab");

                            // The bounded walk itself is exercised through the contract overload with the one
                            // contract this build implements, which is what a real Brave window resolves to. This
                            // window is still synthetic: passing here is not evidence that a real browser works,
                            // which is what the isolated Brave run is for.
                            BrowserDescriptor brave;
                            Assert.IsTrue(BrowserCatalog.TryGet(BrowserKind.Brave, out brave), "the catalog has the verified browser");
                            ScanResult scan = UiaTabScanner.Scan(hwnd, brave, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                            Assert.AreEqual(ScanOutcome.Ready, scan.Outcome, "scanner on synthetic horizontal tabs: " + scan.Error + " " + scan.Message);
                            Assert.AreEqual(2, scan.Tabs.Count, "both siblings enumerated with cache");
                            Assert.IsTrue(scan.Tabs[1].Selected && !scan.Tabs[0].Selected, "cached selection pattern");
                            phase("contract-scan");
                        }
                        catch (Exception ex) { failure = ex; }
                    });
                    worker.IsBackground = true;
                    worker.SetApartmentState(ApartmentState.MTA);
                    worker.Start();
                    if (!worker.Join(15000))
                    {
                        string phasesSoFar;
                        lock (phases) phasesSoFar = string.Join(" ", phases.ToArray());
                        throw new AssertionFailure("bounded UIA test deadline exceeded after 15000 ms; phases completed so far: "
                            + (phasesSoFar.Length == 0 ? "none - the worker never reached its first mark" : phasesSoFar)
                            + " (each value is the time spent in that phase)");
                    }
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
                            form.Render(new ActiveTabSnapshot { Enabled = true, SupportedBrowserInForeground = true,
                                HiddenReason = "Quét UI Automation thất bại. Đã ẩn nút ×. " + new string('x', 160),
                                Scan = FailingScan() });
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
                                    Assert.IsTrue(control.Parent.ClientRectangle.Contains(control.Bounds),
                                        "action not clipped by flow row at " + scale + ": " + control.GetType().Name
                                        + " '" + control.Text + "' " + control.Bounds + " in " + control.Parent.GetType().Name
                                        + " " + control.Parent.ClientRectangle);
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
                ScanResult scan = FailingScan();
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
                    return ScanResult.Idle(window, ScanOutcome.NoTabs, ScanMessageKey.NoTabs);
                }))
                {
                    try
                    {
                        ScanRequest first = new ScanRequest(1, ScanLane.Overlay, 0, new IntPtr(1), Point.Empty);
                        Assert.IsTrue(worker.TrySubmit(first), "first request accepted");
                        Assert.IsTrue(entered.WaitOne(5000), "worker started");
                        Assert.IsFalse(worker.TrySubmit(first), "busy lane refuses second request");
                        release.Set();
                        Assert.IsTrue(delivered.WaitOne(5000), "error delivered");
                        Assert.AreEqual(ScanOutcome.Error, last.Result.Outcome, "exception fails closed");
                        Assert.IsFalse(last.Result.Error.Contains("PRIVATE"), "provider message redacted");
                        Assert.IsTrue(worker.TrySubmit(new ScanRequest(2, ScanLane.Overlay, 0, new IntPtr(1), Point.Empty)), "error does not wedge lane");
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
                using (ActiveTabController controller = new ActiveTabController(overlay, invoker, AppSettings.Defaults()))
                {
                    System.Reflection.MethodInfo complete = typeof(ActiveTabController).GetMethod("OnScanCompleted",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ScanRequest old = new ScanRequest(1, ScanLane.Dashboard, 0, new IntPtr(1), Point.Empty);
                    ScanResult result = ScanResult.Idle(old.Window, ScanOutcome.Ready, ScanMessageKey.Ready);
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

        /// <summary>
        /// The overlay is the only clickable surface, so a size change must move the native window, be verified
        /// again on the next placement, and never leave a stale hit-test area behind.
        /// </summary>
        [Test]
        public static void OverlayResizesToConfiguredGeometryAndStaysSelfVerifying()
        {
            using (CloseButtonOverlay overlay = new CloseButtonOverlay())
            {
                string error;
                Rectangle small = new Rectangle(140, 160, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonSizeLogical);
                Rectangle large = new Rectangle(140, 160, AppInfo.MaxButtonSizeLogical, AppInfo.MaxButtonSizeLogical);
                Assert.IsTrue(overlay.ShowAt(small, out error), "first placement: " + error);
                Assert.AreEqual(small, NativeMethods.GetWindowBounds(overlay.Handle), "first physical bounds");
                Assert.IsTrue(overlay.ShowAt(small, out error), "an unchanged placement is a verified no-op");
                Assert.AreEqual(small, NativeMethods.GetWindowBounds(overlay.Handle), "no-op keeps the bounds");
                Assert.IsTrue(overlay.ShowAt(large, out error), "resize: " + error);
                Assert.AreEqual(large, NativeMethods.GetWindowBounds(overlay.Handle), "resized window uses the configured size");
                Assert.IsTrue(overlay.Visible, "still visible after a resize");
                Assert.IsTrue(overlay.ShowAt(small, out error), "shrink back: " + error);
                Assert.AreEqual(small, NativeMethods.GetWindowBounds(overlay.Handle), "shrunk window uses the configured size");
                overlay.Hide();
                Assert.IsFalse(overlay.Visible, "hidden on demand");
                Assert.IsFalse(overlay.ShowAt(Rectangle.Empty, out error), "invalid bounds are rejected");
                Assert.IsFalse(overlay.Visible, "a rejected placement stays hidden");
                Assert.IsTrue(!string.IsNullOrEmpty(error), "a rejected placement explains itself");
            }
        }

        /// <summary>
        /// The settings window must survive the smallest supported window at high font scaling, and the language
        /// toggle must relabel every element in place (no window rebuild, no stale text).
        /// </summary>
        [Test]
        public static void SettingsWindowLayoutsAndTogglesLanguage()
        {
            foreach (float scale in new float[] { 1F, 1.25F, 1.5F, 2F })
            {
                RecordingSettingsHost host = new RecordingSettingsHost();
                using (SettingsForm form = new SettingsForm(host))
                {
                    form.PrepareForExit();
                    List<Control> controls = AllControls(form);
                    List<Font> fonts = new List<Font>();
                    try
                    {
                        foreach (Control control in controls) fonts.Add(new Font(control.Font.FontFamily, control.Font.Size * scale, control.Font.Style));
                        for (int i = 0; i < controls.Count; i++) controls[i].Font = fonts[i];
                        form.Show();
                        foreach (Size size in new Size[] { new Size(760, 720), new Size(420, 340), new Size(1100, 820) })
                        {
                            form.Size = size;
                            form.PerformLayout();
                            Application.DoEvents();
                            foreach (Control control in controls)
                            {
                                Assert.IsTrue(control.Width > 0 && control.Height > 0,
                                    "control has no size at " + scale + ": " + control.GetType().Name);
                                WrappingLabel label = control as WrappingLabel;
                                if (label != null)
                                {
                                    int needed = label.GetPreferredSize(new Size(label.Width, 0)).Height;
                                    Assert.IsTrue(label.Height >= needed, "wrapped settings text clipped at " + scale + ": " + label.Height + " < " + needed);
                                }
                                if (control is Button || control is CheckBox || control is RadioButton)
                                    Assert.IsTrue(control.Parent.ClientRectangle.Contains(control.Bounds),
                                        "settings control clipped at " + scale + ": " + control.GetType().Name
                                        + " '" + control.Text + "' " + control.Bounds + " in " + control.Parent.GetType().Name
                                        + " " + control.Parent.ClientRectangle);
                                if (control is NumericUpDown || control is TrackBar)
                                    Assert.IsTrue(control.Left >= 0 && control.Right <= control.Parent.ClientSize.Width + 2,
                                        "numeric row escapes its group at " + scale + ": " + control.Bounds + " in " + control.Parent.ClientSize);
                            }
                        }

                        Strings.Language = AppLanguage.English;
                        form.ApplyLanguage();
                        Application.DoEvents();
                        Assert.IsTrue(form.Text.Contains(Strings.Get(StringId.SettingsTitle)), "window caption follows the language");
                        Assert.IsTrue(CountText(controls, Strings.Get(StringId.SettingsTitle)) >= 1, "header follows the language");
                        Assert.IsTrue(CountText(controls, Strings.Get(StringId.SettingsEnable)) >= 1, "enable label follows the language");

                        Strings.Language = AppLanguage.Vietnamese;
                        form.ApplyLanguage();
                        Application.DoEvents();
                        Assert.IsTrue(CountText(controls, Strings.Get(StringId.SettingsTitle)) >= 1, "switching back relabels the header");
                    }
                    finally
                    {
                        Strings.Language = AppLanguage.Vietnamese;
                        form.Close();
                        foreach (Font font in fonts) font.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Opening the window must not spend its time measuring the same text over and over, and the geometry it ends
        /// up with must be exactly the geometry a second pass produces. This guards the measurement cache in
        /// <see cref="UiLayout"/>: a stale entry would silently change the layout on the next pass, and clipping
        /// assertions elsewhere would not notice a window that is simply slow to build.
        /// </summary>
        [Test]
        public static void SettingsWindowBuildsOnceAndLaysOutStably()
        {
            RecordingSettingsHost host = new RecordingSettingsHost();
            Stopwatch open = Stopwatch.StartNew();
            using (SettingsForm form = new SettingsForm(host))
            {
                form.PrepareForExit();
                form.Show();
                Application.DoEvents();
                open.Stop();
                Console.WriteLine("INFO settings window built and shown in " + open.ElapsedMilliseconds + " ms");

                List<Control> controls = AllControls(form);
                Rectangle[] before = new Rectangle[controls.Count];
                for (int index = 0; index < controls.Count; index++) before[index] = controls[index].Bounds;

                form.PerformLayout();
                Application.DoEvents();
                form.PerformLayout();
                Application.DoEvents();

                for (int index = 0; index < controls.Count; index++)
                {
                    Assert.AreEqual(before[index], controls[index].Bounds,
                        "a repeated layout pass moved " + controls[index].GetType().Name + "; the text measurement cache is stale");
                }
                form.Close();
            }
        }

        /// <summary>
        /// Round trip through the window's own controls: host values reach the controls, control values reach the
        /// host, and an out-of-range value coming from anywhere is repaired instead of thrown or shown as-is.
        /// </summary>
        [Test]
        public static void SettingsWindowReflectsHostAndClampsEveryValue()
        {
            RecordingSettingsHost host = new RecordingSettingsHost();
            using (SettingsForm form = new SettingsForm(host))
            {
                form.PrepareForExit();
                form.Show();
                List<Control> controls = AllControls(form);

                AppSettings changed = host.Settings.Copy();
                changed.VisibilityMode = OverlayVisibilityMode.HoverOnSelectedTab;
                changed.ButtonSizeLogical = AppInfo.MaxButtonSizeLogical;
                changed.ButtonInsetLogical = AppInfo.MaxButtonInsetLogical;
                changed.HoverGraceMs = 0;
                changed.ScanIntervalMs = AppInfo.MinScanIntervalMs;
                changed.Language = AppLanguage.English;
                host.Replace(changed);
                form.LoadFromHost();
                AppSettings fromControls = form.BuildSettings();
                Assert.AreEqual(OverlayVisibilityMode.HoverOnSelectedTab, fromControls.VisibilityMode, "mode shown in the window");
                Assert.AreEqual(AppInfo.MaxButtonSizeLogical, fromControls.ButtonSizeLogical, "size shown in the window");
                Assert.AreEqual(AppInfo.MaxButtonInsetLogical, fromControls.ButtonInsetLogical, "inset shown in the window");
                Assert.AreEqual(0, fromControls.HoverGraceMs, "grace shown in the window");
                Assert.AreEqual(AppInfo.MinScanIntervalMs, fromControls.ScanIntervalMs, "interval shown in the window");
                Assert.AreEqual(AppLanguage.English, fromControls.Language, "language shown in the window");

                // A hostile or corrupted value must be repaired, not trusted, and must not throw while loading.
                AppSettings hostile = AppSettings.Defaults();
                hostile.ButtonSizeLogical = 10000;
                hostile.ButtonInsetLogical = -50;
                hostile.HoverGraceMs = 999999;
                hostile.ScanIntervalMs = 1;
                host.Replace(hostile);
                form.LoadFromHost();
                AppSettings repaired = form.BuildSettings();
                Assert.IsTrue(repaired.ButtonSizeLogical >= AppInfo.MinButtonSizeLogical && repaired.ButtonSizeLogical <= AppInfo.MaxButtonSizeLogical,
                    "size clamped into the documented range: " + repaired.ButtonSizeLogical);
                Assert.IsTrue(repaired.ButtonInsetLogical >= AppInfo.MinButtonInsetLogical && repaired.ButtonInsetLogical <= AppInfo.MaxButtonInsetLogical,
                    "inset clamped into the documented range: " + repaired.ButtonInsetLogical);
                Assert.IsTrue(repaired.HoverGraceMs >= AppInfo.MinHoverGraceMs && repaired.HoverGraceMs <= AppInfo.MaxHoverGraceMs,
                    "grace clamped into the documented range: " + repaired.HoverGraceMs);
                Assert.IsTrue(repaired.ScanIntervalMs >= AppInfo.MinScanIntervalMs && repaired.ScanIntervalMs <= AppInfo.MaxScanIntervalMs,
                    "interval clamped into the documented range: " + repaired.ScanIntervalMs);

                // The enable check box asks the host and follows the effective answer, not the requested one:
                // when the host refuses (the user cancelled the warning), the box must snap back to the real state.
                CheckBox enableBox = FindLocalized<CheckBox>(controls, StringId.SettingsEnable);
                Assert.IsTrue(enableBox != null, "the enable check box must be present");
                Assert.IsTrue(enableBox.Checked, "the fixture starts enabled");
                host.RefuseDisable = true;
                enableBox.Checked = false;
                Application.DoEvents();
                Assert.AreEqual(1, host.RequestCount, "turning the button off must ask the host once");
                Assert.IsTrue(enableBox.Checked, "a refused change snaps the check box back to the effective state");

                host.RefuseDisable = false;
                enableBox.Checked = false;
                Application.DoEvents();
                Assert.AreEqual(2, host.RequestCount, "turning it off again asks the host again");
                Assert.IsFalse(enableBox.Checked, "an accepted change is shown as off");

                CheckBox startupBox = FindLocalized<CheckBox>(controls, StringId.SettingsStartWithWindows);
                Assert.IsTrue(startupBox != null, "the start-with-Windows check box must be present");
                Assert.IsFalse(startupBox.Checked, "the fixture registry entry is absent, so the box starts unchecked");
                startupBox.Checked = true;
                Application.DoEvents();
                Assert.AreEqual(1, host.StartupChanges, "the check box asks the host to write the registry entry");
                Assert.IsFalse(startupBox.Checked, "the box follows the real registry state, not the click");
                form.PrepareForExit();
            }
        }

        /// <summary>
        /// The declared INPUT layout is only meaningful if SendInput really accepts the structure on this
        /// architecture, and a delivered chord must not leave a key logically down. The chord is sent to a
        /// throwaway window owned by this process, never to a browser.
        /// </summary>
        [Test]
        public static void KeyboardInjectionDeliversTheWholeChordAndReleasesBothKeys()
        {
            Assert.AreEqual(IntPtr.Size == 8 ? 40 : 28, KeyboardInjector.InputStructureSize, "Win32 INPUT layout for this architecture");
            Assert.IsFalse(KeyboardInjector.BlockingModifierDown(), "the fixture must start with no blocking modifier held");

            using (Form target = new Form
            {
                Text = "Xtension injector fixture",
                StartPosition = FormStartPosition.Manual,
                Location = new Point(60, 60),
                Size = new Size(420, 260),
                ShowInTaskbar = true
            })
            {
                target.Show();
                SetForegroundWindow(target.Handle);
                Application.DoEvents();
                Assert.AreEqual(target.Handle, NativeMethods.GetForegroundRootWindow(),
                    "the throwaway fixture must be the foreground window before injecting");

                string failure;
                bool sent = KeyboardInjector.SendControlW(out failure);
                Assert.IsTrue(sent, "SendInput must accept every event of the chord with this structure size: " + failure);
                Assert.IsTrue(target.Visible && !target.IsDisposed, "Ctrl+W in a plain WinForms window closes nothing");
                Assert.IsFalse(IsKeyDown(0x11), "Ctrl must be released after the chord");
                Assert.IsFalse(IsKeyDown(0x57), "W must be released after the chord");
                Assert.IsFalse(KeyboardInjector.BlockingModifierDown(), "no blocking modifier may be left held");
            }
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static ScanResult FailingScan()
        {
            return ScanResult.Idle(IntPtr.Zero, ScanOutcome.Error, ScanMessageKey.ScanFailed);
        }

        private static T FindLocalized<T>(List<Control> controls, StringId id) where T : Control
        {
            foreach (Control control in controls)
            {
                T typed = control as T;
                if (typed == null) continue;
                LocalizedText text = typed.Tag as LocalizedText;
                if (text != null && text.Id == id) return typed;
            }
            return null;
        }

        /// <summary>
        /// The shipped executable embeds one icon with a frame for every size Explorer, the notification area and the
        /// file properties dialog ask for, and the tray really uses it. A build whose resource went missing must show
        /// the painted fallback instead of a blank square, which is checked too.
        /// </summary>
        [Test]
        public static void IconResourceCarriesEverySizeTheTrayAndExplorerAskFor()
        {
            Assert.AreEqual(9, AppIcon.FrameCount(), "the embedded icon is compiled in with its nine frames");
            using (Icon small = AppIcon.Small())
            using (Icon large = AppIcon.Large())
            {
                Assert.IsTrue(small != null, "the tray size is available from the embedded resource");
                Assert.IsTrue(large != null, "the window size is available from the embedded resource");
                Assert.IsTrue(small.Width >= 16 && small.Width <= 32, "the tray icon is a small frame: " + small.Width);
                Assert.IsTrue(large.Width >= 16, "the window icon has a usable frame: " + large.Width);
            }

            // Disposing what the icon class hands out must never affect anybody else. The first version of this class
            // returned one cached instance to every caller, so a single owner that disposed it destroyed the handle
            // that every window and the tray were still using, and the next window to be painted threw
            // ObjectDisposedException. This is the regression guard for that defect.
            Icon disposable = AppIcon.Small();
            Assert.IsTrue(disposable != null, "an icon is handed out");
            disposable.Dispose();
            using (Icon again = AppIcon.Small())
            {
                Assert.IsTrue(again != null, "an icon is still available after another one was disposed");
                Assert.IsTrue(again.Width >= 16, "the second icon is a real frame: " + again.Width);
                Assert.IsTrue(HasVisiblePixel(again), "the second icon can still be drawn");
            }
            using (Form window = new Form { Text = "Xtension icon fixture" })
            {
                AppIcon.Attach(window);
                Assert.IsTrue(window.Icon != null, "a window gets its own icon");
                Assert.IsTrue(HasVisiblePixel(window.Icon), "the window icon can still be drawn after the earlier disposal");
            }

            Assert.IsTrue(TrayIcon.UsesEmbedded(), "the tray icon comes from the embedded resource, not the fallback");
            using (Icon tray = TrayIcon.Create())
            {
                Assert.IsTrue(tray != null, "the tray icon is created");
                Assert.IsTrue(tray.Width >= 16, "the tray icon has a real frame: " + tray.Width);
                Assert.IsTrue(HasVisiblePixel(tray), "the tray icon can be drawn");
            }
            // The fallback must exist and be usable: an install whose resource is missing still shows something, and
            // an icon whose handle was already destroyed would fail here instead of looking fine.
            using (Icon painted = TrayIcon.CreatePainted(16))
            {
                Assert.IsTrue(painted != null, "the painted fallback icon is always available");
                Assert.AreEqual(16, painted.Width, "the fallback honours the requested size");
                Assert.IsTrue(HasVisiblePixel(painted), "the painted fallback can actually be drawn");
            }
            using (Icon painted = TrayIcon.CreatePainted(32))
            using (Bitmap decoded = painted.ToBitmap())
            {
                Assert.AreEqual(32, decoded.Width, "the fallback is decoded back at its own size");
            }
        }

        /// <summary>True when an icon can be converted to a bitmap and contains at least one visible pixel.</summary>
        private static bool HasVisiblePixel(Icon icon)
        {
            if (icon == null || icon.Width <= 0) return false;
            using (Bitmap bitmap = icon.ToBitmap())
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        if (bitmap.GetPixel(x, y).A > 0) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Notifications must never take the foreground. If showing one could activate a window, the × would have to
        /// be recalculated for a different foreground window and a notification could interrupt what the user is
        /// doing - which is also why the sound-free own window replaced the Shell balloon tip.
        /// </summary>
        [Test]
        public static void NoticeWindowNeverActivatesOrTakesTheForeground()
        {
            const int WsExNoActivate = 0x08000000;
            const int WsExToolWindow = 0x00000080;
            const int GwlExStyle = -20;
            using (Form fixture = new Form { Text = "Xtension notice foreground fixture", StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), Size = new Size(420, 260) })
            using (NoticeWindow notice = new NoticeWindow())
            {
                fixture.Show();
                // Bringing a window to the front can be refused once, so this retries with message pumping instead of
                // asserting on a single attempt: the contract below is "the notice never changes the foreground
                // window", which cannot be measured at all unless the fixture really holds it first.
                IntPtr before = IntPtr.Zero;
                for (int attempt = 0; attempt < 40 && before != fixture.Handle; attempt++)
                {
                    SetForegroundWindow(fixture.Handle);
                    Application.DoEvents();
                    Thread.Sleep(50);
                    before = NativeMethods.GetForegroundRootWindow();
                }
                Assert.AreEqual(fixture.Handle, before,
                    "the runner could not bring the fixture to the foreground, so the non-activation contract cannot be measured");

                notice.Notify("Xtension", "Synthetic notification text for the non-activation check.");
                Application.DoEvents();
                Assert.IsTrue(notice.Visible, "the notice window is shown");
                int style = GetWindowLong(notice.Handle, GwlExStyle);
                Assert.IsTrue((style & WsExNoActivate) != 0, "the notice window must carry WS_EX_NOACTIVATE");
                Assert.IsTrue((style & WsExToolWindow) != 0, "the notice window must be a tool window, never a taskbar window");
                // Two separate claims: the notice never grabs the foreground itself, and it never takes it away from
                // the window that had it.
                IntPtr duringNotice = NativeMethods.GetForegroundRootWindow();
                Assert.IsTrue(duringNotice != notice.Handle, "the notice window must never become the foreground window");
                Assert.AreEqual(before, duringNotice, "showing a notification must not change the foreground window");

                notice.Dismiss();
                Application.DoEvents();
                Assert.IsFalse(notice.Visible, "the notice window hides on dismiss");
                Assert.AreEqual(before, NativeMethods.GetForegroundRootWindow(),
                    "dismissing a notification must not change the foreground window either");
            }
        }

        /// <summary>
        /// The settings window's whole point is that a control change reaches the host immediately and that a value the
        /// user edited is never silently replaced. This walks profile selection, a hand-edited interval, appearance and
        /// the browser list through the real controls.
        /// </summary>
        [Test]
        public static void SettingsWindowAppliesProfilesAppearanceAndBrowsers()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                Strings.Language = AppLanguage.English;
                RecordingSettingsHost host = new RecordingSettingsHost();
                using (SettingsForm form = new SettingsForm(host))
                {
                    form.PrepareForExit();
                    form.Show();
                    Application.DoEvents();
                    List<Control> controls = AllControls(form);

                    // Selecting a preset writes exactly the preset's interval.
                    RadioButton fast = FindLocalized<RadioButton>(controls, StringId.ProfileFast);
                    Assert.IsTrue(fast != null, "the fast preset radio exists");
                    fast.Checked = true;
                    Application.DoEvents();
                    Assert.AreEqual(Profiles.CadenceFor(PerformanceProfile.Fast).ScanIntervalMs, host.Settings.ScanIntervalMs,
                        "choosing a preset applies its documented interval");
                    Assert.AreEqual(PerformanceProfile.Fast, host.Settings.Profile, "the chosen preset is the stored marker");

                    // Editing the interval must turn the configuration into custom, and the window must say so
                    // instead of continuing to claim the preset.
                    NumericUpDown interval = FindNumeric(controls, Profiles.CadenceFor(PerformanceProfile.Fast).ScanIntervalMs);
                    Assert.IsTrue(interval != null, "the scan interval control exists");
                    interval.Value = 900;
                    form.FlushPendingChanges();
                    Application.DoEvents();
                    Assert.AreEqual(900, host.Settings.ScanIntervalMs, "a debounced edit reaches the host");
                    Assert.AreEqual(PerformanceProfile.Custom, host.Settings.Profile,
                        "a profile-controlled value edited by the user is reported as custom, not as the old preset");
                    RadioButton custom = FindLocalized<RadioButton>(controls, StringId.ProfileCustom);
                    Assert.IsTrue(custom != null && custom.Checked, "the window shows the configuration as custom");

                    // Appearance is applied without touching geometry or the hit area.
                    int sizeBefore = host.Settings.ButtonSizeLogical;
                    // Two unrelated radio groups used to share one container, and WinForms makes radio buttons
                    // exclusive per container: choosing a colour then switched the visibility mode back to "always"
                    // without a word. The mode is chosen first and checked again after the colour, so that pairing
                    // cannot come back unnoticed.
                    RadioButton hoverMode = FindLocalized<RadioButton>(controls, StringId.SettingsModeHover);
                    Assert.IsTrue(hoverMode != null, "the visibility mode options exist");
                    hoverMode.Checked = true;
                    form.FlushPendingChanges();
                    Application.DoEvents();
                    Assert.AreEqual(OverlayVisibilityMode.HoverOnSelectedTab, host.Settings.VisibilityMode,
                        "the visibility mode reaches the host");

                    NumericUpDown opacity = FindNumeric(controls, AppInfo.DefaultButtonOpacityPercent);
                    Assert.IsTrue(opacity != null, "the opacity control exists");
                    opacity.Value = 50;
                    form.FlushPendingChanges();
                    Application.DoEvents();
                    Assert.AreEqual(50, host.Settings.ButtonOpacityPercent, "opacity reaches the host");
                    RadioButton red = FindLocalized<RadioButton>(controls, StringId.ColorRed);
                    Assert.IsTrue(red != null, "the colour options exist");
                    red.Checked = true;
                    form.FlushPendingChanges();
                    Application.DoEvents();
                    Assert.AreEqual(OverlayColor.Red, host.Settings.ButtonColor, "the colour reaches the host");
                    Assert.IsTrue(hoverMode.Checked, "choosing a colour must not clear the visibility mode");
                    Assert.AreEqual(OverlayVisibilityMode.HoverOnSelectedTab, host.Settings.VisibilityMode,
                        "choosing a colour must not change the visibility mode");
                    Assert.AreEqual(sizeBefore, host.Settings.ButtonSizeLogical, "appearance must not change the geometry");
                    Assert.AreEqual("brave", BrowserCatalog.MaskTokenList(host.Settings.Browsers),
                        "the browser list still contains only the verified browser");

                    // Clearing the last browser must not leave the product with nothing to show.
                    CheckBox browserBox = FindBrowserBox(controls);
                    Assert.IsTrue(browserBox != null, "the browser list is offered in the window");
                    browserBox.Checked = false;
                    form.FlushPendingChanges();
                    Application.DoEvents();
                    Assert.AreEqual("brave", BrowserCatalog.MaskTokenList(host.Settings.Browsers),
                        "the last browser cannot be cleared, so the × always has a browser it may appear in");

                    // Export then import: the same validated path the window's file dialogs call.
                    string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xtension-dev4-window-" + Guid.NewGuid().ToString("N"));
                    System.IO.Directory.CreateDirectory(directory);
                    try
                    {
                        string file = System.IO.Path.Combine(directory, "backup.ini");
                        string exported = host.ExportSettings(file);
                        Assert.IsTrue(exported == "exported", "the host reports a successful export: " + exported);
                        Assert.IsTrue(System.IO.File.Exists(file), "the backup file exists");
                        form.ImportBackupFile(file);
                        System.Reflection.FieldInfo statusField = typeof(SettingsForm).GetField("_status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Label status = (Label)statusField.GetValue(form);
                        Assert.IsTrue(status.Text.StartsWith("imported ", StringComparison.Ordinal), "import outcome survives values/language refresh");

                        AppSettings other = AppSettings.Defaults();
                        other.ScanIntervalMs = AppInfo.MinScanIntervalMs;
                        host.Replace(other);
                        form.LoadFromHost();
                        string imported = host.ImportSettings(file);
                        Assert.IsTrue(imported.StartsWith("imported", StringComparison.Ordinal), "the host reports a successful import: " + imported);
                        Assert.AreEqual(900, host.Settings.ScanIntervalMs, "the imported configuration is the exported one");
                        Assert.AreEqual(OverlayColor.Red, host.Settings.ButtonColor, "the imported appearance is the exported one");
                    }
                    finally
                    {
                        try { System.IO.Directory.Delete(directory, true); }
                        catch (System.IO.IOException) { }
                    }
                    form.PrepareForExit();
                }
            }
            finally
            {
                Strings.Language = previous;
            }
        }

        /// <summary>
        /// Finds the first numeric control showing a value. The settings window has no per-control names, so a value
        /// that appears exactly once (each documented default is distinct) is the stable way to address one.
        /// </summary>
        private static NumericUpDown FindNumeric(List<Control> controls, int value)
        {
            foreach (Control control in controls)
            {
                NumericUpDown numeric = control as NumericUpDown;
                if (numeric != null && numeric.Value == value) return numeric;
            }
            return null;
        }

        private static CheckBox FindBrowserBox(List<Control> controls)
        {
            foreach (Control control in controls)
            {
                CheckBox box = control as CheckBox;
                if (box == null) continue;
                if (box.Text != null && box.Text.Contains("Brave")) return box;
            }
            return null;
        }

        private static int CountText(List<Control> controls, string text)
        {
            int count = 0;
            foreach (Control control in controls) if (control.Text == text) count++;
            return count;
        }

        /// <summary>
        /// Stands in for the tray context so the settings window can be exercised without a tray icon, a browser or
        /// a real registry entry, exactly as the interface intends.
        /// </summary>
        private sealed class RecordingSettingsHost : ISettingsHost
        {
            private AppSettings _settings = AppSettings.Defaults();
            public bool RefuseDisable;
            public string ExportPath;
            public string ImportPath;
            public int ApplyCount;
            public int RequestCount;
            public int StartupChanges;

            public bool OverlayEnabled
            {
                get { return _settings.OverlayEnabled; }
            }

            public AppSettings Settings
            {
                get { return _settings.Copy(); }
            }

            public bool LaunchAtLogin
            {
                get { return false; }
            }

            public bool RequestOverlayEnabled(bool enabled)
            {
                RequestCount++;
                if (!enabled && RefuseDisable) return _settings.OverlayEnabled;
                _settings.OverlayEnabled = enabled;
                return enabled;
            }

            public void ApplySettings(AppSettings settings)
            {
                ApplyCount++;
                _settings = settings.Copy();
                _settings.Normalize();
            }

            public void SetLaunchAtLogin(bool enabled)
            {
                // The fixture never writes the registry: LaunchAtLogin stays false, which is the real state a
                // settings window must keep showing after a click.
                StartupChanges++;
            }

            /// <summary>
            /// The fixture writes through the same validated path the product uses, into a temporary file, so the
            /// settings window's export and import behaviour is exercised without touching the user's settings file.
            /// </summary>
            public string ExportSettings(string path)
            {
                ExportPath = path;
                try
                {
                    string document = SettingsBackup.Export(_settings);
                    System.IO.File.WriteAllText(path, document, new System.Text.UTF8Encoding(false));
                    return "exported";
                }
                catch (Exception ex)
                {
                    return "export failed: " + ex.GetType().Name;
                }
            }

            public string ImportSettings(string path)
            {
                ImportPath = path;
                BackupImportResult outcome = SettingsBackup.Parse(System.IO.File.ReadAllText(path));
                if (!outcome.Accepted) return "rejected";
                _settings = outcome.Settings;
                _settings.Normalize();
                return "imported " + outcome.AppliedCount;
            }

            public void ShowDiagnostics()
            {
            }

            public void OpenLog()
            {
            }

            public void Replace(AppSettings settings)
            {
                _settings = settings.Copy();
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
