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

        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.EnablePerMonitorDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 4 && args[0] == "--brave") return BraveAcceptance.Run(args[1], args[2], args[3]);
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
                            ScanResult scan = UiaTabScanner.Scan(hwnd, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
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
                using (ActiveTabController controller = new ActiveTabController(overlay, invoker, AppSettings.Defaults()))
                {
                    System.Reflection.MethodInfo complete = typeof(ActiveTabController).GetMethod("OnScanCompleted",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ScanRequest old = new ScanRequest(1, ScanKind.Dashboard, 0, new IntPtr(1), Point.Empty);
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
                                        "settings control clipped at " + scale + ": " + control.GetType().Name + " " + control.Bounds + " in " + control.Parent.ClientRectangle);
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
