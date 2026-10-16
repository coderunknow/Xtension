using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// Xtension's own notification surface: a small, non-activating, sound-free card in the corner of the working
    /// area. It replaces the Shell's balloon tip for every message Xtension shows on its own, and that replacement is
    /// deliberate:
    ///
    /// - It never takes the foreground (WS_EX_NOACTIVATE, WM_MOUSEACTIVATE answered with MA_NOACTIVATE and
    ///   ShowWithoutActivation), so showing a notification cannot change which window is in front, cannot hide the ×
    ///   through a foreground change and cannot make the browser rescan because focus returned.
    /// - It plays no sound and uses no Shell toast machinery, so it cannot play the system notification sound over
    ///   whatever is playing or create a notification-host window above a video.
    /// - One instance is created and reused, so repeated notifications do not create and destroy windows.
    ///
    /// It is still an ordinary window above the page, exactly like the × itself, and it is dismissed by a click, by
    /// Escape, or by its own timeout.
    /// </summary>
    internal sealed class NoticeWindow : Form
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int WmMouseActivate = 0x0021;
        private const int WmNcHitTest = 0x0084;
        private const int MaNoActivate = 3;
        private const int HtCaption = 2;

        private readonly Label _title;
        private readonly Label _message;
        private readonly Timer _timer;
        private readonly Font _titleFont;
        private readonly Font _messageFont;

        public NoticeWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            // Deliberately not TopMost in the constructor: a topmost window is brought forward when it is shown, and
            // the card must never take the foreground. Topmost is applied with SetWindowPos and SWP_NOACTIVATE while
            // showing instead, which is the same non-activating placement the overlay uses.
            BackColor = Color.FromArgb(24, 34, 51);
            KeyPreview = true;
            MinimumSize = new Size(120, 40);
            // The inset lives in Padding so the docked labels are laid out by WinForms, not by hand.
            Padding = new Padding(14, 10, 14, 10);

            _titleFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _messageFont = new Font("Segoe UI", 9F, FontStyle.Regular);
            _title = new Label { AutoSize = false, Height = 20, Dock = DockStyle.Top, ForeColor = Color.White,
                BackColor = Color.Transparent, Font = _titleFont, UseMnemonic = false };
            _message = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(226, 232, 240),
                BackColor = Color.Transparent, Font = _messageFont, UseMnemonic = false };
            Controls.Add(_message);
            Controls.Add(_title);

            _timer = new Timer();
            _timer.Tick += delegate { Dismiss(); };

            _title.Click += delegate { Dismiss(); };
            _message.Click += delegate { Dismiss(); };
            Click += delegate { Dismiss(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Escape) return;
                e.Handled = true;
                Dismiss();
            };
            ApplyLanguage();
        }

        /// <summary>Refreshes the accessible text. The visible text is supplied with every notification.</summary>
        public void ApplyLanguage()
        {
            AccessibleName = Strings.Get(StringId.NoticeAccessibleName);
            AccessibleDescription = Strings.Get(StringId.NoticeAccessibleName);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
                return parameters;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseActivate)
            {
                m.Result = new IntPtr(MaNoActivate);
                return;
            }
            if (m.Msg == WmNcHitTest)
            {
                // The whole card behaves like a button, not like a caption to drag.
                m.Result = new IntPtr(HtCaption);
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// Shows one message without activating the window or playing a sound. Any failure is contained: a
        /// notification that cannot be shown must never take the application down or block the caller.
        /// </summary>
        public void Notify(string title, string message)
        {
            try
            {
                _title.Text = title == null ? AppInfo.ProductName : title;
                _message.Text = message == null ? string.Empty : message;
                LayoutCard();
                IntPtr foregroundBefore = NativeMethods.GetForegroundRootWindow();
                if (!Visible) Show();
                NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost, Left, Top, Width, Height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                // The card is allowed to be visible and topmost, never to hold the foreground. If it ever did, that
                // would be a defect worth knowing about, so it is recorded instead of being assumed away.
                if (NativeMethods.GetForegroundRootWindow() == Handle && foregroundBefore != Handle)
                {
                    AppLog.WarnThrottled("notice-window", "The notification took the foreground window; it must not.");
                }
                _timer.Stop();
                _timer.Interval = AppInfo.NoticeAutoCloseMs;
                _timer.Start();
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("notice-window", "A notification could not be shown: " + ex.GetType().Name);
            }
        }

        public void Dismiss()
        {
            _timer.Stop();
            if (Visible) Hide();
        }

        private void LayoutCard()
        {
            IntPtr handle = Handle;
            int dpi = NativeMethods.GetWindowDpi(handle);
            double scale = TabGeometry.Scale(dpi);
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int margin = (int)Math.Round(AppInfo.NoticeMarginLogical * scale);
            int chrome = Padding.Horizontal + 8;
            int width = (int)Math.Round(AppInfo.NoticeWidthLogical * scale);
            int maximumWidth = Math.Max(160, area.Width - margin * 2);
            if (width > maximumWidth) width = maximumWidth;
            if (width < 160) width = Math.Min(160, Math.Max(80, maximumWidth));

            _title.Height = Math.Max(18, _titleFont.Height + 4);
            int textWidth = Math.Max(40, width - chrome);
            Size titleSize = TextRenderer.MeasureText(_title.Text, _title.Font, new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            Size messageSize = TextRenderer.MeasureText(_message.Text, _message.Font, new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int minimum = (int)Math.Round(AppInfo.NoticeMinHeightLogical * scale);
            int height = Math.Max(minimum, titleSize.Height + messageSize.Height + 26);

            // The docked labels fill the padded client area, so only the card's own size has to be computed.
            SetBounds(area.Right - margin - width, area.Bottom - margin - height, width, height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                if (_titleFont != null) _titleFont.Dispose();
                if (_messageFont != null) _messageFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
