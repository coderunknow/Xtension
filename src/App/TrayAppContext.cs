using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// Application lifetime: tray icon, menus, dashboard, diagnostics and settings windows, and the single UI timer.
    /// Tab logic lives in ActiveTabController; this class only wires user actions to it and owns the settings file.
    /// Every entry point (tray menu, dashboard, settings window) goes through the same methods, so the saved values,
    /// the controller and the visible state cannot drift apart.
    /// </summary>
    internal sealed class TrayAppContext : ApplicationContext, ISettingsHost
    {
        private readonly Control _invoker;
        private readonly CloseButtonOverlay _overlay;
        private readonly ActiveTabController _controller;
        private readonly SettingsStore _settingsStore;
        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ToolStripMenuItem _dashboardItem;
        private readonly ToolStripMenuItem _overlayItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _diagnosticsItem;
        private readonly ToolStripMenuItem _settingsItem;
        private readonly ToolStripMenuItem _braveSettingsItem;
        private readonly ToolStripMenuItem _logItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly ShowDashboardListener _showListener;
        private NoticeWindow _notice;
        private AppSettings _settings;
        private MainForm _mainForm;
        private DiagnosticsForm _diagnostics;
        private SettingsForm _settingsForm;
        private bool _exiting;

        public TrayAppContext(SettingsStore settingsStore, AppSettings settings)
        {
            _settingsStore = settingsStore ?? SettingsStore.Default;
            _settings = settings == null ? AppSettings.Load(_settingsStore) : settings.Copy();
            _settings.Normalize();
            Strings.Language = _settings.Language;

            // A hidden control gives worker threads a safe way to marshal results onto the UI thread.
            _invoker = new Control();
            _invoker.CreateControl();

            _overlay = new CloseButtonOverlay();
            _controller = new ActiveTabController(_overlay, _invoker, _settings);
            _controller.Changed += delegate { RefreshViews(); };
            _controller.CloseFailed += delegate(string message) { ShowTrayTip(AppInfo.ProductName, message); };

            ContextMenuStrip menu = new ContextMenuStrip();
            Font menuFont = new Font("Segoe UI", 9F);
            menu.Font = menuFont;
            _dashboardItem = new ToolStripMenuItem();
            _dashboardItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dashboardItem.Click += delegate { ShowDashboard(); };
            menu.Items.Add(_dashboardItem);
            menu.Items.Add(new ToolStripSeparator());
            _overlayItem = new ToolStripMenuItem { CheckOnClick = false };
            _overlayItem.Click += delegate { RequestOverlayEnabled(!_controller.GetSnapshot().Enabled); };
            menu.Items.Add(_overlayItem);
            _startupItem = new ToolStripMenuItem { CheckOnClick = false };
            _startupItem.Click += delegate { SetLaunchAtLogin(!_startupItem.Checked); };
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            _diagnosticsItem = new ToolStripMenuItem();
            _diagnosticsItem.Click += delegate { ShowDiagnostics(); };
            menu.Items.Add(_diagnosticsItem);
            _settingsItem = new ToolStripMenuItem();
            _settingsItem.Click += delegate { ShowSettings(); };
            menu.Items.Add(_settingsItem);
            _braveSettingsItem = new ToolStripMenuItem();
            _braveSettingsItem.Click += delegate { OpenBraveAppearanceSettings(); };
            menu.Items.Add(_braveSettingsItem);
            _logItem = new ToolStripMenuItem();
            _logItem.Click += delegate { OpenLog(); };
            menu.Items.Add(_logItem);
            menu.Items.Add(new ToolStripSeparator());
            _exitItem = new ToolStripMenuItem();
            _exitItem.Click += delegate { RequestExit(); };
            menu.Items.Add(_exitItem);

            _tray = new NotifyIcon();
            _tray.Icon = TrayIcon.Create();
            _tray.Text = AppInfo.ProductName;
            _tray.ContextMenuStrip = menu;
            _tray.Visible = true;
            _tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ShowDashboard();
            };

            _timer = new System.Windows.Forms.Timer();
            _timer.Tick += delegate { _controller.Tick(); };
            UpdateTimerInterval();
            _timer.Start();

            _showListener = new ShowDashboardListener(delegate { _invoker.BeginInvoke(new Action(ShowDashboard)); });
            ApplyLanguage();
            RefreshViews();
            AppLog.Info("Xtension " + AppInfo.DisplayVersion + " started. Overlay " + (_settings.OverlayEnabled ? "enabled" : "disabled")
                + ", mode " + AppSettings.VisibilityToken(_settings.VisibilityMode)
                + ", button " + _settings.ButtonSizeLogical + "/" + _settings.ButtonInsetLogical
                + ", scan " + _settings.ScanIntervalMs + " ms"
                + ", profile " + Profiles.Token(_settings.Profile)
                + ", browsers " + BrowserCatalog.MaskTokenList(_settings.Browsers) + "."
                + " Icon: frames=" + AppIcon.FrameCount() + ", tray source=" + (TrayIcon.UsesEmbedded() ? "embedded" : "painted")
                + ". Win32 INPUT size=" + KeyboardInjector.InputStructureSize + " bytes, pointer size=" + IntPtr.Size + ".");
        }

        public ActiveTabSnapshot Snapshot
        {
            get { return _controller.GetSnapshot(); }
        }

        /// <summary>Live settings copy for the settings window and for tests.</summary>
        public AppSettings Settings
        {
            get { return _settings.Copy(); }
        }

        public bool OverlayEnabled
        {
            get { return _controller.GetSnapshot().Enabled; }
        }

        public bool LaunchAtLogin
        {
            get { return StartupRegistration.IsEnabled(); }
        }

        public void ToggleOverlayFromDashboard()
        {
            RequestOverlayEnabled(!_controller.GetSnapshot().Enabled);
        }

        /// <summary>
        /// Single path for every enable/disable action. Turning the × off warns that Brave's own close buttons may
        /// stay hidden, and a cancelled warning changes nothing. Returns the effective state.
        /// </summary>
        public bool RequestOverlayEnabled(bool enabled)
        {
            if (!enabled)
            {
                DialogResult confirmation = MessageBox.Show(
                    Strings.Get(StringId.ConfirmDisableBody), Strings.Get(StringId.ConfirmDisableTitle),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmation != DialogResult.Yes) return _controller.GetSnapshot().Enabled;
            }

            _controller.SetEnabled(enabled);
            _settings.OverlayEnabled = enabled;
            PersistSettings();
            RefreshViews();
            return enabled;
        }

        /// <summary>
        /// Applies a validated settings set immediately and saves it. Everything visible – controller, overlay,
        /// tray menu, dashboards – is refreshed in the same call, so no interface shows a stale value.
        /// </summary>
        public void ApplySettings(AppSettings settings)
        {
            if (settings == null) return;
            settings.Normalize();
            bool languageChanged = settings.Language != _settings.Language;
            _settings = settings.Copy();
            Strings.Language = _settings.Language;
            _controller.ApplySettings(_settings);
            UpdateTimerInterval();
            PersistSettings();
            if (languageChanged) ApplyLanguage();
            RefreshViews();
        }

        private void PersistSettings()
        {
            try
            {
                _settings.Save(_settingsStore);
            }
            catch (Exception ex)
            {
                AppLog.Warn("The settings could not be saved; the change applies until Xtension restarts: " + ex.GetType().Name);
                ShowTrayTip(AppInfo.ProductName, Strings.Get(StringId.SettingsSaveFailed, ex.GetType().Name));
            }
        }

        private void UpdateTimerInterval()
        {
            // The controller owns the cadence because it depends on the performance profile, not only on the mode.
            int interval = _controller.PollIntervalMs();
            if (interval <= 0) interval = AppInfo.ForegroundPollMs;
            if (_timer.Interval != interval) _timer.Interval = interval;
        }

        /// <summary>Refreshes every localized element after a language change without rebuilding any window.</summary>
        private void ApplyLanguage()
        {
            _dashboardItem.Text = Strings.Get(StringId.MenuOpenDashboard);
            _overlayItem.Text = Strings.Get(StringId.MenuOverlay);
            _startupItem.Text = Strings.Get(StringId.MenuStartup);
            _diagnosticsItem.Text = Strings.Get(StringId.MenuDiagnostics);
            _settingsItem.Text = Strings.Get(StringId.MenuXtensionSettings);
            _braveSettingsItem.Text = Strings.Get(StringId.MenuBraveSettings);
            _logItem.Text = Strings.Get(StringId.MenuLog);
            _exitItem.Text = Strings.Get(StringId.MenuExit);
            _overlay.ApplyLanguage();
            if (_mainForm != null && !_mainForm.IsDisposed) _mainForm.ResetRenderCache();
            if (_diagnostics != null && !_diagnostics.IsDisposed) _diagnostics.ApplyLanguage();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.ApplyLanguage();
            if (_notice != null && !_notice.IsDisposed) _notice.ApplyLanguage();
            if (_diagnostics != null && !_diagnostics.IsDisposed) _diagnostics.ApplyLanguage();
        }

        public void ShowDashboard()
        {
            if (_exiting) return;
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(this);
                _mainForm.VisibleChanged += delegate { RefreshViews(); };
            }
            if (!_mainForm.Visible) _mainForm.Show();
            if (_mainForm.WindowState == FormWindowState.Minimized) _mainForm.WindowState = FormWindowState.Normal;
            _mainForm.UpdateStartupState(_startupItem.Checked);
            RefreshViews();
            _mainForm.BringToFront();
            _mainForm.Activate();
        }

        public void ShowSettings()
        {
            if (_exiting) return;
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(this);
                _settingsForm.VisibleChanged += delegate { RefreshViews(); };
                _settingsForm.FormClosed += delegate { _settingsForm = null; };
            }
            _settingsForm.LoadFromHost();
            if (!_settingsForm.Visible) _settingsForm.Show();
            _settingsForm.WindowState = FormWindowState.Normal;
            RefreshViews();
            _settingsForm.BringToFront();
            _settingsForm.Activate();
        }

        public void ShowDiagnostics()
        {
            if (_exiting) return;
            if (_diagnostics == null || _diagnostics.IsDisposed)
            {
                _diagnostics = new DiagnosticsForm();
                _diagnostics.VisibleChanged += delegate { RefreshViews(); };
                _diagnostics.FormClosed += delegate { _diagnostics = null; };
            }
            if (!_diagnostics.Visible) _diagnostics.Show();
            _diagnostics.WindowState = FormWindowState.Normal;
            RefreshViews();
            _diagnostics.BringToFront();
            _diagnostics.Activate();
        }

        public void SetLaunchAtLogin(bool enabled)
        {
            try
            {
                StartupRegistration.SetEnabled(enabled);
                AppLog.Info(enabled ? "Launch at Windows sign-in enabled." : "Launch at Windows sign-in disabled.");
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not change launch-at-sign-in setting: " + ex.GetType().Name + ": " + ex.Message);
                MessageBox.Show(Strings.Get(StringId.StartupRegistrationFailedBody, ex.Message),
                    Strings.Get(StringId.StartupRegistrationFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateStartupState();
        }

        public void RequestExit()
        {
            DialogResult answer = MessageBox.Show(
                Strings.Get(StringId.ConfirmExitBody), Strings.Get(StringId.ConfirmExitTitle),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes) ExitApplication();
        }

        public void OpenBraveAppearanceSettings()
        {
            try
            {
                // The brave:// scheme is registered by the Brave installer and opens the page in the running browser.
                Process.Start(new ProcessStartInfo("brave://settings/appearance") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLog.Warn("Brave settings page could not be opened: " + ex.GetType().Name);
                MessageBox.Show(Strings.Get(StringId.BraveSettingsFailedBody, ex.Message),
                    Strings.Get(StringId.BraveSettingsFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void OpenLog()
        {
            try
            {
                AppPaths.EnsureDirectory();
                if (!File.Exists(AppPaths.LogFile)) File.WriteAllText(AppPaths.LogFile, Strings.Get(StringId.LogFileHeader) + Environment.NewLine, Encoding.UTF8);
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + AppPaths.LogFile + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Get(StringId.LogOpenFailedBody, ex.Message),
                    Strings.Get(StringId.LogOpenFailedTitle), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Shows a message in Xtension's own notification window. That window never activates, never takes the
        /// foreground and plays no sound, so a notification cannot change which window is in front, cannot hide the
        /// × through a foreground change and cannot play the Shell notification sound over other audio.
        /// </summary>
        public void ShowTrayTip(string title, string message)
        {
            if (_exiting) return;
            try
            {
                if (_notice == null || _notice.IsDisposed) _notice = new NoticeWindow();
                _notice.Notify(title, message);
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("notice-window", "The notification could not be shown: " + ex.GetType().Name);
            }
        }

        /// <summary>Writes the current settings to a backup file and returns the sentence shown to the user.</summary>
        public string ExportSettings(string path)
        {
            try
            {
                string document = SettingsBackup.Export(_settings);
                File.WriteAllText(path, document, new UTF8Encoding(false));
                AppLog.Info("Settings exported to a user-chosen file.");
                return Strings.Get(StringId.BackupExportDone, path);
            }
            catch (Exception ex)
            {
                AppLog.Warn("The settings could not be exported: " + ex.GetType().Name);
                return Strings.Get(StringId.BackupExportFailed, ex.GetType().Name);
            }
        }

        /// <summary>
        /// Validates a backup file, writes it through the one settings path and applies it. Any failure leaves the
        /// current configuration untouched, and the returned sentence says exactly what was applied, rejected,
        /// clamped or preserved.
        /// </summary>
        public string ImportSettings(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists) return Strings.Get(StringId.BackupImportFailed, "file not found");
                // A backup is a settings file: the same size ceiling applies, checked before reading it.
                if (info.Length > 4L * AppInfo.MaxSettingsBackupBytes) return Strings.Get(StringId.BackupImportTooLarge);

                BackupImportResult outcome = SettingsBackup.Parse(File.ReadAllText(path));
                if (!outcome.Accepted)
                {
                    AppLog.Warn("A settings import was rejected: " + outcome.Failure);
                    return ImportFailureText(outcome);
                }

                string failure;
                if (!SettingsBackup.ApplyToStore(_settingsStore, outcome, out failure))
                {
                    AppLog.Warn("A settings import could not be written: " + failure);
                    return Strings.Get(StringId.BackupImportFailed, failure);
                }
                ApplySettings(outcome.Settings);
                AppLog.Info("Settings imported: " + outcome.AppliedCount + " applied, " + outcome.RejectedKeys.Count
                    + " rejected, " + outcome.ClampedKeys.Count + " clamped, " + outcome.PreservedKeys.Count + " preserved.");

                StringBuilder text = new StringBuilder();
                text.Append(Strings.Get(StringId.BackupImportDone, outcome.AppliedCount, outcome.RejectedKeys.Count,
                    outcome.PreservedKeys.Count));
                AppendKeyList(text, StringId.BackupImportRejectedKeys, outcome.RejectedKeys);
                AppendKeyList(text, StringId.BackupImportClampedKeys, outcome.ClampedKeys);
                AppendKeyList(text, StringId.BackupImportPreservedKeys, outcome.PreservedKeys);
                return text.ToString();
            }
            catch (Exception ex)
            {
                AppLog.Warn("A settings import failed: " + ex.GetType().Name);
                return Strings.Get(StringId.BackupImportFailed, ex.GetType().Name);
            }
        }

        private static void AppendKeyList(StringBuilder text, StringId id, System.Collections.Generic.List<string> keys)
        {
            if (keys == null || keys.Count == 0) return;
            text.Append(Environment.NewLine);
            text.Append(Strings.Get(id, string.Join(", ", keys.ToArray())));
        }

        private static string ImportFailureText(BackupImportResult outcome)
        {
            if (outcome.Message == StringId.BackupImportUnsupportedSchema)
                return Strings.Get(StringId.BackupImportUnsupportedSchema, outcome.Schema, AppInfo.SettingsBackupSchema);
            if (outcome.Message != StringId.None) return Strings.Get(outcome.Message);
            return Strings.Get(StringId.BackupImportFailed, outcome.Failure);
        }

        private void UpdateStartupState()
        {
            bool enabled = StartupRegistration.IsEnabled();
            _startupItem.Checked = enabled;
            if (_mainForm != null && !_mainForm.IsDisposed) _mainForm.UpdateStartupState(enabled);
        }

        /// <summary>Pushes the latest snapshot to every visible window and keeps the tray menu in sync.</summary>
        private void RefreshViews()
        {
            if (_exiting) return;
            ActiveTabSnapshot snapshot = _controller.GetSnapshot();
            _overlayItem.Checked = snapshot.Enabled;
            string trayText = snapshot.Enabled ? AppInfo.ProductName : AppInfo.ProductName + Strings.Get(StringId.TrayDisabledSuffix);
            if (_tray.Text != trayText) _tray.Text = trayText;

            bool mainVisible = _mainForm != null && !_mainForm.IsDisposed && _mainForm.Visible;
            bool diagnosticsVisible = _diagnostics != null && !_diagnostics.IsDisposed && _diagnostics.Visible;
            bool settingsVisible = _settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.Visible;
            _controller.SetDashboardVisible(mainVisible || diagnosticsVisible || settingsVisible);

            if (mainVisible) _mainForm.Render(snapshot);
            if (diagnosticsVisible) _diagnostics.Render(snapshot);
        }

        private void ExitApplication()
        {
            if (_exiting) return;
            _exiting = true;
            AppLog.Info("Application exit requested by user.");
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            _exiting = true;
            StopSafely(() => _timer.Stop());
            StopSafely(() => _timer.Dispose());
            StopSafely(() => _controller.Dispose());
            StopSafely(() => _showListener.Dispose());
            StopSafely(() => { _overlay.Hide(); _overlay.Dispose(); });
            StopSafely(() =>
            {
                if (_notice != null && !_notice.IsDisposed)
                {
                    _notice.Dismiss();
                    _notice.Dispose();
                }
            });
            StopSafely(() => { if (_diagnostics != null && !_diagnostics.IsDisposed) _diagnostics.Close(); });
            StopSafely(() =>
            {
                if (_settingsForm != null && !_settingsForm.IsDisposed)
                {
                    _settingsForm.PrepareForExit();
                    _settingsForm.Close();
                }
            });
            StopSafely(() =>
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.PrepareForExit();
                    _mainForm.Close();
                }
            });
            StopSafely(() =>
            {
                _tray.Visible = false;
                Icon icon = _tray.Icon;
                ContextMenuStrip menu = _tray.ContextMenuStrip;
                _tray.Dispose();
                if (icon != null) icon.Dispose();
                if (menu != null)
                {
                    foreach (ToolStripItem item in menu.Items)
                        if (item.Font != menu.Font) item.Font.Dispose();
                    Font font = menu.Font;
                    menu.Dispose();
                    font.Dispose();
                }
            });
            StopSafely(() => _invoker.Dispose());
            AppLog.Info("Xtension stopped.");
            base.ExitThreadCore();
        }

        private static void StopSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("shutdown", "A shutdown step failed: " + ex.GetType().Name);
            }
        }
    }
}
