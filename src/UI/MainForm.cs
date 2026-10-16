using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>Dashboard window. It renders snapshots only; all decisions live in ActiveTabController.</summary>
    internal sealed class MainForm : Form
    {
        private readonly System.Collections.Generic.List<Font> _fonts = new System.Collections.Generic.List<Font>();
        private readonly TrayAppContext _app;
        private readonly Label _badge;
        private readonly Label _headline;
        private readonly TextBox _tabName;
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
        // Shared fonts instead of a new Font object per control, for the same reason as the settings window: fewer
        // GDI objects to create and dispose, and one object per role, so the window scales as a unit.
        private readonly Font _bodyFont;
        private readonly Font _badgeFont;
        private readonly Font _headlineFont;
        private readonly Font _titleFont;
        private readonly Font _buttonFont;
        private bool _suppressStartupEvent;
        private bool _allowClose;
        private bool _closeTipShown;

        // Last rendered state. Comparing these fields costs nothing and replaces building a signature string
        // for every notification, which is the common case on a timer tick.
        private bool _haveRendered;
        private bool _lastEnabled;
        private bool _lastVisible;
        private bool _lastBrowser;
        private bool _lastStalled;
        private OverlayVisibilityMode _lastMode;
        private ScanOutcome _lastOutcome;
        private string _lastIdle = string.Empty;
        private string _lastNotice = string.Empty;
        private string _lastHidden = string.Empty;
        private string _lastScanMessage = string.Empty;
        private string _lastTabName = string.Empty;

        public MainForm(TrayAppContext app)
        {
            _app = app;
            Text = AppInfo.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(720, 560);
            MinimumSize = new Size(360, 280);
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(243, 246, 250);
            _bodyFont = OwnFont(9F, FontStyle.Regular);
            _badgeFont = OwnFont(9F, FontStyle.Bold);
            _headlineFont = OwnFont(13F, FontStyle.Bold);
            _titleFont = OwnFont(17F, FontStyle.Bold);
            _buttonFont = OwnFont(9F, FontStyle.Bold);
            Font = _bodyFont;
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;

            Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel content = UiLayout.Stack();
            content.Padding = new Padding(18, 16, 18, 14);
            viewport.Controls.Add(content);
            Controls.Add(viewport);
            // Build the tree with layout suspended and lay it out once, so the window does not run a layout pass
            // for every row that is added to it (see SettingsForm, which does the same).
            SuspendLayout();
            viewport.SuspendLayout();
            content.SuspendLayout();
            try
            {

                TableLayoutPanel header = UiLayout.Stack();
                header.BackColor = _navy;
                header.Padding = new Padding(18, 12, 18, 12);
                UiLayout.Add(header, new WrappingLabel { Text = AppInfo.ProductName, Font = _titleFont, ForeColor = Color.White });
                UiLayout.Add(header, UiLayout.Localize(new WrappingLabel { ForeColor = Color.FromArgb(203, 213, 225) }, StringId.DashboardTagline));
                UiLayout.Add(content, header);

                TableLayoutPanel statusCard = UiLayout.Stack();
                statusCard.BackColor = Color.White;
                statusCard.Padding = new Padding(14);
                _badge = new WrappingLabel { Font = _badgeFont, ForeColor = _blue, Text = Strings.Get(StringId.BadgeStarting) };
                _headline = new WrappingLabel { Font = _headlineFont, ForeColor = _text, Text = Strings.Get(StringId.HeadlineStarting) };
                Label activeLabel = UiLayout.Localize(new WrappingLabel { ForeColor = _muted }, StringId.LabelActiveTab);
                // Selectable and horizontally scrollable: a long title is never silently truncated.
                _tabName = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = _text, Text = Strings.Get(StringId.NoConfirmedTab) };
                UiLayout.Add(statusCard, _badge);
                UiLayout.Add(statusCard, _headline);
                UiLayout.Add(statusCard, activeLabel);
                UiLayout.Add(statusCard, _tabName);
                UiLayout.Add(content, statusCard);

                FlowLayoutPanel toggleRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 9, 0, 4) };
                _toggleButton = CreateButton(Strings.Get(StringId.ToggleTurnOff), _red, Color.White, 166, 40);
                _toggleButton.Margin = new Padding(0, 0, 18, 4);
                _toggleButton.Click += delegate { _app.ToggleOverlayFromDashboard(); };
                _startupBox = UiLayout.Localize(new WrappingCheckBox { AutoSize = true, Margin = new Padding(2, 12, 0, 4), ForeColor = _text }, StringId.MenuStartup);
                _startupBox.CheckedChanged += delegate
                {
                    if (!_suppressStartupEvent) _app.SetLaunchAtLogin(_startupBox.Checked);
                };
                toggleRow.Controls.Add(_toggleButton);
                toggleRow.Controls.Add(_startupBox);
                UiLayout.Add(content, toggleRow);

                FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 6, 0, 4) };
                Button settingsButton = CreateButton(Strings.Get(StringId.ButtonXtensionSettings), _blue, Color.White, 152, 38);
                settingsButton.Click += delegate { _app.ShowSettings(); };
                Button diagnosticsButton = CreateButton(Strings.Get(StringId.ButtonDiagnostics), Color.FromArgb(226, 232, 240), _text, 126, 38);
                diagnosticsButton.Click += delegate { _app.ShowDiagnostics(); };
                Button braveSettingsButton = CreateButton(Strings.Get(StringId.ButtonBraveSettings), Color.FromArgb(226, 232, 240), _text, 126, 38);
                braveSettingsButton.Click += delegate { _app.OpenBraveAppearanceSettings(); };
                Button logButton = CreateButton(Strings.Get(StringId.ButtonOpenLog), Color.FromArgb(226, 232, 240), _text, 126, 38);
                logButton.Click += delegate { _app.OpenLog(); };
                Button exitButton = CreateButton(Strings.Get(StringId.ButtonExit), Color.FromArgb(254, 226, 226), _red, 132, 38);
                exitButton.Click += delegate { _app.RequestExit(); };
                UiLayout.Localize(settingsButton, StringId.ButtonXtensionSettings);
                UiLayout.Localize(diagnosticsButton, StringId.ButtonDiagnostics);
                UiLayout.Localize(braveSettingsButton, StringId.ButtonBraveSettings);
                UiLayout.Localize(logButton, StringId.ButtonOpenLog);
                UiLayout.Localize(exitButton, StringId.ButtonExit);
                foreach (Button button in new Button[] { settingsButton, diagnosticsButton, braveSettingsButton, logButton, exitButton })
                {
                    button.Margin = new Padding(0, 0, 8, 4);
                    actions.Controls.Add(button);
                }
                UiLayout.Add(content, actions);
            }
            finally
            {
                content.ResumeLayout(false);
                viewport.ResumeLayout(false);
                ResumeLayout(false);
                PerformLayout();
            }

            _notice = new WrappingLabel { Padding = new Padding(14, 10, 12, 8), BackColor = Color.White, ForeColor = _muted, Text = Strings.Get(StringId.GeneralNote) };
            UiLayout.Add(content, _notice);
            _footer = UiLayout.Localize(new WrappingLabel { Padding = new Padding(0, 10, 0, 0), ForeColor = _muted }, StringId.Footer, AppInfo.DisplayVersion);
            UiLayout.Add(content, _footer);
            AppIcon.Attach(this);
            Shown += delegate { UiLayout.FitToWorkingArea(this); };

            FormClosing += OnFormClosing;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    Hide();
                }
            };
        }

        /// <summary>Drops the render cache so the next snapshot is drawn again. Used after a language change.</summary>
        public void ResetRenderCache()
        {
            _haveRendered = false;
            _badge.Text = Strings.Get(StringId.BadgeStarting);
            _headline.Text = Strings.Get(StringId.HeadlineStarting);
            _tabName.Text = Strings.Get(StringId.NoConfirmedTab);
            _notice.Text = Strings.Get(StringId.GeneralNote);
            _toggleButton.Text = Strings.Get(StringId.ToggleTurnOff);
            UiLayout.ApplyLanguage(this);
        }

        /// <summary>Redraws only when the displayed content changed, so periodic updates cost almost nothing.</summary>
        public void Render(ActiveTabSnapshot snapshot)
        {
            if (IsDisposed || snapshot == null) return;
            ScanResult scan = snapshot.Scan;
            bool ready = scan != null && scan.Outcome == ScanOutcome.Ready && scan.SelectedHorizontalTab != null;
            string tabName = ready ? scan.SelectedHorizontalTab.Name : string.Empty;
            string scanMessage = scan == null ? string.Empty : scan.Message;

            if (_haveRendered
                && _lastEnabled == snapshot.Enabled
                && _lastVisible == snapshot.OverlayVisible
                && _lastBrowser == snapshot.SupportedBrowserInForeground
                && _lastStalled == snapshot.Stalled
                && _lastMode == snapshot.VisibilityMode
                && _lastOutcome == (scan == null ? ScanOutcome.Idle : scan.Outcome)
                && _lastIdle == snapshot.IdleReason
                && _lastNotice == snapshot.Notice
                && _lastHidden == snapshot.HiddenReason
                && _lastScanMessage == scanMessage
                && _lastTabName == tabName) return;

            _haveRendered = true;
            _lastEnabled = snapshot.Enabled;
            _lastVisible = snapshot.OverlayVisible;
            _lastBrowser = snapshot.SupportedBrowserInForeground;
            _lastStalled = snapshot.Stalled;
            _lastMode = snapshot.VisibilityMode;
            _lastOutcome = scan == null ? ScanOutcome.Idle : scan.Outcome;
            _lastIdle = snapshot.IdleReason;
            _lastNotice = snapshot.Notice;
            _lastHidden = snapshot.HiddenReason;
            _lastScanMessage = scanMessage;
            _lastTabName = tabName;

            string detail;
            if (!snapshot.Enabled)
            {
                _badge.Text = Strings.Get(StringId.BadgeDisabled);
                _badge.ForeColor = _red;
                _headline.Text = Strings.Get(StringId.HeadlineDisabled);
                detail = Strings.Get(StringId.DetailDisabled);
            }
            else if (snapshot.Stalled)
            {
                _badge.Text = Strings.Get(StringId.BadgeStalled);
                _badge.ForeColor = _red;
                _headline.Text = Strings.Get(StringId.HeadlineStalled);
                detail = Strings.Get(StringId.DetailStalled);
            }
            else if (snapshot.OverlayVisible)
            {
                _badge.Text = Strings.Get(StringId.BadgeActive);
                _badge.ForeColor = _green;
                _headline.Text = Strings.Get(StringId.HeadlineActive);
                detail = Strings.Get(StringId.DetailActive);
            }
            else if (!snapshot.SupportedBrowserInForeground)
            {
                _badge.Text = Strings.Get(StringId.BadgeWaitingBrowser);
                _badge.ForeColor = _muted;
                _headline.Text = Strings.Get(StringId.HeadlineWaitingBrowser);
                detail = snapshot.IdleReason;
            }
            else if (ready)
            {
                _badge.Text = Strings.Get(StringId.BadgeReady);
                _badge.ForeColor = _blue;
                _headline.Text = Strings.Get(StringId.HeadlineReady);
                detail = snapshot.HiddenReason;
            }
            else
            {
                _badge.Text = Strings.Get(StringId.BadgeUnknown);
                _badge.ForeColor = _muted;
                _headline.Text = Strings.Get(StringId.HeadlineUnknown);
                detail = scan == null ? Strings.Get(StringId.DetailWaitingFirstScan) : scan.Message;
            }

            _tabName.Text = ready
                ? (string.IsNullOrEmpty(tabName) ? Strings.Get(StringId.UntitledTab) : tabName)
                : Strings.Get(StringId.NoConfirmedTab);

            if (!snapshot.OverlayVisible && !string.IsNullOrEmpty(snapshot.HiddenReason)) detail = snapshot.HiddenReason;
            string notice = detail + "\r\n" + Strings.Get(StringId.GeneralNote);
            if (!string.IsNullOrEmpty(snapshot.Notice)) notice = snapshot.Notice + "\r\n" + notice;
            _notice.Text = notice;

            _toggleButton.Text = snapshot.Enabled ? Strings.Get(StringId.ToggleTurnOff) : Strings.Get(StringId.ToggleTurnOn);
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
                _app.ShowTrayTip(Strings.Get(StringId.TrayTipMinimizedTitle), Strings.Get(StringId.TrayTipMinimizedBody));
            }
        }

        private Font OwnFont(float size, FontStyle style)
        {
            Font font = new Font("Segoe UI", size, style);
            _fonts.Add(font);
            return font;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { foreach (Font font in _fonts) font.Dispose(); _fonts.Clear(); }
        }

        private Button CreateButton(string caption, Color backColor, Color foreColor, int width, int height)
        {
            Button button = new Button
            {
                Text = caption,
                MinimumSize = new Size(width, height),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = _buttonFont,
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
