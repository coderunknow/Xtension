using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>Dashboard window. It renders snapshots only; all decisions live in ActiveTabController.</summary>
    internal sealed class MainForm : Form
    {
        private const string GeneralNote =
            "Xtension không thay đổi cài đặt của Brave. Nếu Brave có tùy chọn ẩn nút đóng tab trong Cài đặt → Giao diện, " +
            "hãy bật tùy chọn đó để giảm nhầm với nút đóng gốc. Nếu tắt nút × hoặc thoát ứng dụng, nút đóng gốc của Brave " +
            "có thể vẫn bị ẩn cho đến khi bạn đổi lại tùy chọn đó trong Brave.";

        private readonly TrayAppContext _app;
        private readonly Label _badge;
        private readonly Label _headline;
        private readonly Label _tabName;
        private readonly Label _notice;
        private readonly Label _footer;
        private readonly Button _toggleButton;
        private readonly CheckBox _startupBox;
        private readonly Color _navy = Color.FromArgb(24, 34, 51);
        private readonly Color _blue = Color.FromArgb(37, 99, 235);
        private readonly Color _green = Color.FromArgb(5, 150, 105);
        private readonly Color _red = Color.FromArgb(185, 28, 28);
        private readonly Color _text = Color.FromArgb(31, 41, 55);
        private readonly Color _muted = Color.FromArgb(100, 116, 139);
        private string _renderSignature = string.Empty;
        private bool _suppressStartupEvent;
        private bool _allowClose;
        private bool _closeTipShown;

        public MainForm(TrayAppContext app)
        {
            _app = app;
            Text = AppInfo.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(720, 560);
            MinimumSize = new Size(660, 520);
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(243, 246, 250);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = _navy };
            Label title = new Label { Text = AppInfo.ProductName, AutoSize = true, Location = new Point(22, 13), Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = Color.White };
            Label subtitle = new Label { Text = "Chỉ hiện nút đóng trên tab Brave đang được chọn", AutoSize = true, Location = new Point(24, 48), Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(203, 213, 225) };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            Panel footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(235, 239, 245), Padding = new Padding(18, 0, 18, 0) };
            _footer = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = _muted, Text = "Phiên bản " + AppInfo.DisplayVersion + " · Đóng cửa sổ này sẽ thu nhỏ ứng dụng xuống khay hệ thống." };
            footerPanel.Controls.Add(_footer);

            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18, 16, 18, 14), BackColor = BackColor };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel statusCard = CreateCard();
            _badge = new Label { AutoSize = true, Location = new Point(18, 15), Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = _blue, Text = "●  ĐANG KHỞI ĐỘNG" };
            _headline = new Label { AutoEllipsis = true, Location = new Point(18, 42), Size = new Size(630, 27), Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = _text, Text = "Đang kiểm tra tab Brave…" };
            Label activeLabel = new Label { Text = "TAB ĐƯỢC NHẬN DIỆN", AutoSize = true, Location = new Point(19, 77), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = _muted };
            _tabName = new Label { AutoEllipsis = true, Location = new Point(19, 94), Size = new Size(630, 22), Font = new Font("Segoe UI", 9F), ForeColor = _text, Text = "Chưa có tab được xác nhận" };
            statusCard.Controls.Add(_badge);
            statusCard.Controls.Add(_headline);
            statusCard.Controls.Add(activeLabel);
            statusCard.Controls.Add(_tabName);
            statusCard.Resize += delegate
            {
                int width = Math.Max(120, statusCard.ClientSize.Width - 38);
                _headline.Width = width;
                _tabName.Width = width;
            };
            content.Controls.Add(statusCard, 0, 0);

            FlowLayoutPanel toggleRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 9, 0, 0), BackColor = BackColor };
            _toggleButton = CreateButton("Tắt nút ×", _red, Color.White, 166, 40);
            _toggleButton.Margin = new Padding(0, 0, 18, 0);
            _toggleButton.Click += delegate { _app.ToggleOverlayFromDashboard(); };
            _startupBox = new CheckBox
            {
                Text = "Khởi chạy cùng Windows",
                AutoSize = true,
                Margin = new Padding(2, 12, 0, 0),
                ForeColor = _text,
                Font = new Font("Segoe UI", 9.5F),
                UseVisualStyleBackColor = false,
                BackColor = BackColor
            };
            _startupBox.CheckedChanged += delegate
            {
                if (_suppressStartupEvent) return;
                _app.SetLaunchAtLogin(_startupBox.Checked);
            };
            toggleRow.Controls.Add(_toggleButton);
            toggleRow.Controls.Add(_startupBox);
            content.Controls.Add(toggleRow, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 10, 0, 4), BackColor = BackColor };
            Button diagnosticsButton = CreateButton("Chẩn đoán", Color.FromArgb(226, 232, 240), _text, 126, 38);
            diagnosticsButton.Click += delegate { _app.ShowDiagnostics(); };
            Button settingsButton = CreateButton("Cài đặt Brave", Color.FromArgb(226, 232, 240), _text, 126, 38);
            settingsButton.Click += delegate { _app.OpenBraveAppearanceSettings(); };
            Button logButton = CreateButton("Mở nhật ký", Color.FromArgb(226, 232, 240), _text, 126, 38);
            logButton.Click += delegate { _app.OpenLog(); };
            Button exitButton = CreateButton("Thoát ứng dụng", Color.FromArgb(254, 226, 226), _red, 132, 38);
            exitButton.Click += delegate { _app.RequestExit(); };
            diagnosticsButton.Margin = new Padding(0, 0, 8, 4);
            settingsButton.Margin = new Padding(0, 0, 8, 4);
            logButton.Margin = new Padding(0, 0, 8, 4);
            exitButton.Margin = new Padding(0, 0, 0, 4);
            actions.Controls.Add(diagnosticsButton);
            actions.Controls.Add(settingsButton);
            actions.Controls.Add(logButton);
            actions.Controls.Add(exitButton);
            content.Controls.Add(actions, 0, 2);

            Panel noticeCard = CreateCard();
            _notice = new Label { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 12, 8), ForeColor = _muted, Font = new Font("Segoe UI", 8.5F), Text = GeneralNote };
            noticeCard.Controls.Add(_notice);
            content.Controls.Add(noticeCard, 0, 3);

            Controls.Add(content);
            Controls.Add(footerPanel);
            Controls.Add(header);

            FormClosing += OnFormClosing;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                    e.Handled = true;
                }
            };
        }

        /// <summary>Redraws only when the displayed content changed, so periodic updates cost almost nothing.</summary>
        public void Render(ActiveTabSnapshot snapshot)
        {
            if (IsDisposed || snapshot == null) return;
            ScanResult scan = snapshot.Scan;
            bool ready = scan != null && scan.Outcome == ScanOutcome.Ready && scan.SelectedHorizontalTab != null;
            string tabName = ready ? scan.SelectedHorizontalTab.Name : string.Empty;
            string signature = string.Join("|", new string[]
            {
                snapshot.Enabled ? "on" : "off",
                snapshot.OverlayVisible ? "shown" : "hidden",
                snapshot.BraveInForeground ? "brave" : "other",
                snapshot.Stalled ? "stalled" : "ok",
                snapshot.IdleReason,
                snapshot.Notice,
                scan == null ? "none" : scan.Outcome.ToString(),
                scan == null ? string.Empty : scan.Message,
                tabName
            });
            if (signature == _renderSignature) return;
            _renderSignature = signature;

            string detail;
            if (!snapshot.Enabled)
            {
                _badge.Text = "●  NÚT × ĐANG TẮT";
                _badge.ForeColor = _red;
                _headline.Text = "Nút × tùy chỉnh đang tắt";
                detail = "Xtension không hiện nút × và không gửi lệnh đóng tab.";
            }
            else if (snapshot.Stalled)
            {
                _badge.Text = "●  UIA KHÔNG PHẢN HỒI";
                _badge.ForeColor = _red;
                _headline.Text = "Brave chưa phản hồi trợ năng";
                detail = "Nút × được ẩn cho đến khi Brave phản hồi. Xtension không đóng tab khi chưa xác nhận được.";
            }
            else if (snapshot.OverlayVisible)
            {
                _badge.Text = "●  ĐANG HOẠT ĐỘNG";
                _badge.ForeColor = _green;
                _headline.Text = "Nút × đang gắn với tab được chọn";
                detail = "Nút × chỉ đóng tab đang được chọn sau khi được xác nhận lại ngay trước khi nhấp.";
            }
            else if (!snapshot.BraveInForeground)
            {
                _badge.Text = "●  CHỜ BRAVE";
                _badge.ForeColor = _muted;
                _headline.Text = "Mở Brave để bắt đầu";
                detail = snapshot.IdleReason;
            }
            else if (ready)
            {
                _badge.Text = "●  SẴN SÀNG";
                _badge.ForeColor = _blue;
                _headline.Text = "Đã nhận diện tab — nút × sẽ hiện trong giây lát";
                detail = scan.Message;
            }
            else
            {
                _badge.Text = "●  CHƯA NHẬN DIỆN";
                _badge.ForeColor = _muted;
                _headline.Text = "Chưa xác nhận được tab đang chọn";
                detail = scan == null ? "Đang chờ kết quả quét đầu tiên." : scan.Message;
            }

            _tabName.Text = ready
                ? (string.IsNullOrEmpty(tabName) ? "(tab không có tiêu đề)" : tabName)
                : "Chưa có tab được xác nhận";

            string notice = detail + "\r\n" + GeneralNote;
            if (!string.IsNullOrEmpty(snapshot.Notice)) notice = snapshot.Notice + "\r\n" + notice;
            _notice.Text = notice;

            _toggleButton.Text = snapshot.Enabled ? "Tắt nút ×" : "Bật nút ×";
            _toggleButton.BackColor = snapshot.Enabled ? _red : _green;
            _toggleButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_toggleButton.BackColor, 0.08F);
            _toggleButton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(_toggleButton.BackColor, 0.06F);
        }

        public void UpdateStartupState(bool enabled)
        {
            if (IsDisposed) return;
            _suppressStartupEvent = true;
            _startupBox.Checked = enabled;
            _suppressStartupEvent = false;
        }

        /// <summary>Called by the tray context when the application exits, so the window really closes.</summary>
        public void PrepareForExit()
        {
            _allowClose = true;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowClose || e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing) return;
            e.Cancel = true;
            Hide();
            if (!_closeTipShown)
            {
                _closeTipShown = true;
                _app.ShowTrayTip("Ứng dụng vẫn đang chạy", "Xtension đã thu nhỏ xuống khay hệ thống. Nhấp biểu tượng gần đồng hồ để mở lại.");
            }
        }

        private Panel CreateCard()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0), Padding = new Padding(0) };
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(221, 228, 237)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                }
            };
            return panel;
        }

        private static Button CreateButton(string caption, Color backColor, Color foreColor, int width, int height)
        {
            Button button = new Button
            {
                Text = caption,
                Size = new Size(width, height),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.08F);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.06F);
            return button;
        }
    }
}
