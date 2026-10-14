using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// Application lifetime: tray icon, menus, dashboard and diagnostics windows, and the single UI timer.
    /// Tab logic lives in ActiveTabController; this class only wires user actions to it.
    /// </summary>
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly Control _invoker;
        private readonly CloseButtonOverlay _overlay;
        private readonly ActiveTabController _controller;
        private readonly SettingsStore _settings;
        private readonly NotifyIcon _tray;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ToolStripMenuItem _overlayItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ShowDashboardListener _showListener;
        private MainForm _mainForm;
        private DiagnosticsForm _diagnostics;
        private bool _exiting;

        public TrayAppContext(SettingsStore settings)
        {
            _settings = settings;
            bool enabled = settings.GetBool(SettingsStore.OverlayEnabledKey, true);

            // A hidden control gives worker threads a safe way to marshal results onto the UI thread.
            _invoker = new Control();
            _invoker.CreateControl();

            _overlay = new CloseButtonOverlay();
            _controller = new ActiveTabController(_overlay, _invoker, enabled);
            _controller.Changed += delegate { RefreshViews(); };
            _controller.CloseFailed += delegate(string message) { ShowTrayTip(AppInfo.ProductName, message); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9F);
            menu.Items.Add("Mở bảng điều khiển", null, delegate { ShowDashboard(); }).Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());
            _overlayItem = new ToolStripMenuItem("Hiện nút × trên tab đang chọn") { Checked = enabled, CheckOnClick = false };
            _overlayItem.Click += delegate { SetOverlayEnabledFromUser(!_controller.GetSnapshot().Enabled); };
            menu.Items.Add(_overlayItem);
            _startupItem = new ToolStripMenuItem("Khởi chạy cùng Windows") { Checked = StartupRegistration.IsEnabled(), CheckOnClick = false };
            _startupItem.Click += delegate { SetLaunchAtLogin(!_startupItem.Checked); };
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Chẩn đoán…", null, delegate { ShowDiagnostics(); });
            menu.Items.Add("Mở cài đặt Giao diện Brave", null, delegate { OpenBraveAppearanceSettings(); });
            menu.Items.Add("Mở nhật ký", null, delegate { OpenLog(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Thoát ứng dụng", null, delegate { RequestExit(); });

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
            _timer.Interval = AppInfo.ForegroundPollMs;
            _timer.Tick += delegate { _controller.Tick(); };
            _timer.Start();

            _showListener = new ShowDashboardListener(delegate { _invoker.BeginInvoke(new Action(ShowDashboard)); });
            RefreshViews();
            AppLog.Info("Xtension " + AppInfo.DisplayVersion + " started. Overlay " + (enabled ? "enabled" : "disabled") +
                ". Win32 INPUT size=" + KeyboardInjector.InputStructureSize + " bytes, pointer size=" + IntPtr.Size + ".");
        }

        public ActiveTabSnapshot Snapshot
        {
            get { return _controller.GetSnapshot(); }
        }

        public void ToggleOverlayFromDashboard()
        {
            SetOverlayEnabledFromUser(!_controller.GetSnapshot().Enabled);
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
                MessageBox.Show("Không thể cập nhật khởi chạy cùng Windows. Hãy kiểm tra quyền ghi Registry của tài khoản hiện tại.\r\n\r\n" + ex.Message,
                    AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateStartupState();
        }

        public void RequestExit()
        {
            DialogResult answer = MessageBox.Show(
                "Thoát Xtension? Nút × tùy chỉnh sẽ ngừng hoạt động. Nếu Brave đang ẩn nút đóng gốc bằng tùy chọn của Brave, " +
                "nút đóng gốc có thể vẫn bị ẩn cho đến khi bạn đổi tùy chọn đó trong Cài đặt Brave.\r\n\r\nBạn có chắc muốn thoát không?",
                "Thoát ứng dụng", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
                MessageBox.Show("Không thể mở cài đặt tự động. Trong Brave, hãy mở:\r\nbrave://settings/appearance\r\n\r\n" + ex.Message,
                    AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void OpenLog()
        {
            try
            {
                AppPaths.EnsureDirectory();
                if (!File.Exists(AppPaths.LogFile)) File.WriteAllText(AppPaths.LogFile, AppInfo.ProductName + " log" + Environment.NewLine, Encoding.UTF8);
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + AppPaths.LogFile + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể mở nhật ký:\r\n" + ex.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ShowTrayTip(string title, string message)
        {
            if (_exiting) return;
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = message;
                _tray.ShowBalloonTip(3000);
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("tray-tip", "Tray notification could not be shown: " + ex.GetType().Name);
            }
        }

        private void SetOverlayEnabledFromUser(bool enabled)
        {
            if (!enabled)
            {
                DialogResult confirmation = MessageBox.Show(
                    "Tắt nút × tùy chỉnh? Nếu Brave đang ẩn nút đóng gốc bằng tùy chọn của Brave, nút đóng gốc có thể vẫn bị ẩn. " +
                    "Khi đó bạn cần mở Cài đặt Brave để hiện lại.\r\n\r\nTiếp tục tắt nút ×?",
                    "Tắt nút ×", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirmation != DialogResult.Yes) return;
            }

            _controller.SetEnabled(enabled);
            try
            {
                _settings.SetBool(SettingsStore.OverlayEnabledKey, enabled);
            }
            catch (Exception ex)
            {
                AppLog.Warn("The overlay setting could not be saved; it applies until Xtension restarts: " + ex.GetType().Name);
            }
            RefreshViews();
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
            string trayText = snapshot.Enabled ? AppInfo.ProductName : AppInfo.ProductName + " (nút × đang tắt)";
            if (_tray.Text != trayText) _tray.Text = trayText;

            bool mainVisible = _mainForm != null && !_mainForm.IsDisposed && _mainForm.Visible;
            bool diagnosticsVisible = _diagnostics != null && !_diagnostics.IsDisposed && _diagnostics.Visible;
            _controller.SetDashboardVisible(mainVisible || diagnosticsVisible);

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
            StopSafely(() => { if (_diagnostics != null && !_diagnostics.IsDisposed) _diagnostics.Close(); });
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
