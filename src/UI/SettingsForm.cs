using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// The dedicated Xtension Settings window. It owns no state of its own: every change is turned into an
    /// <see cref="AppSettings"/> instance, handed to the host (which applies it to the controller and persists it),
    /// and read back from the host, so the window, the dashboard, the tray menu and the overlay cannot disagree.
    ///
    /// Discrete controls (check boxes, mode, numeric boxes) apply and save immediately. The size slider applies on a
    /// short debounce while dragging and flushes the final value on mouse-up and when the window is hidden.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly ISettingsHost _host;
        private readonly List<Font> _fonts = new List<Font>();
        private readonly Color _navy = Color.FromArgb(24, 34, 51);
        private readonly Color _text = Color.FromArgb(31, 41, 55);
        private readonly Color _muted = Color.FromArgb(100, 116, 139);
        private readonly Timer _applyTimer;

        private CheckBox _enableBox;
        private RadioButton _modeAlways;
        private RadioButton _modeHover;
        private TrackBar _sizeTrack;
        private NumericUpDown _sizeValue;
        private NumericUpDown _insetValue;
        private NumericUpDown _graceValue;
        private NumericUpDown _intervalValue;
        private RadioButton _languageVietnamese;
        private RadioButton _languageEnglish;
        private CheckBox _startupBox;
        private Label _status;
        private bool _loading;
        private bool _pendingApply;
        private bool _allowClose;

        public SettingsForm(ISettingsHost host)
        {
            _host = host;
            AutoScaleMode = AutoScaleMode.Font;
            Font = OwnFont(9F, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(400, 320);
            Size = new Size(760, 720);
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = true;
            BackColor = Color.FromArgb(243, 246, 250);
            KeyPreview = true;

            _applyTimer = new Timer();
            _applyTimer.Interval = 150;
            _applyTimer.Tick += delegate { FlushPendingChanges(); };

            Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            TableLayoutPanel content = UiLayout.Stack();
            content.Padding = new Padding(18, 16, 18, 14);
            viewport.Controls.Add(content);
            Controls.Add(viewport);

            TableLayoutPanel header = UiLayout.Stack();
            header.BackColor = _navy;
            header.Padding = new Padding(18, 12, 18, 12);
            UiLayout.Add(header, UiLayout.Localize(new WrappingLabel { Font = OwnFont(15F, FontStyle.Bold), ForeColor = Color.White }, StringId.SettingsTitle));
            UiLayout.Add(header, UiLayout.Localize(new WrappingLabel { ForeColor = Color.FromArgb(203, 213, 225) }, StringId.SettingsIntro));
            UiLayout.Add(content, header);

            BuildButtonGroup(content);
            BuildVisibilityGroup(content);
            BuildGeometryGroup(content);
            BuildAdvancedGroup(content);
            BuildGeneralGroup(content);

            UiLayout.Add(content, UiLayout.Localize(new WrappingLabel
            {
                Padding = new Padding(14, 10, 12, 10), BackColor = Color.White, ForeColor = _muted, Margin = new Padding(0, 10, 0, 6)
            }, StringId.SettingsBraveNote));

            _status = new WrappingLabel { ForeColor = _muted, Padding = new Padding(0, 6, 0, 0) };
            UiLayout.Add(content, _status);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
            Button resetButton = CreateButton(StringId.SettingsReset, Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));
            resetButton.Click += delegate { ResetToDefaults(); };
            Button diagnosticsButton = CreateButton(StringId.SettingsButtonDiagnostics, Color.FromArgb(226, 232, 240), _text);
            diagnosticsButton.Click += delegate { if (_host != null) _host.ShowDiagnostics(); };
            Button logButton = CreateButton(StringId.SettingsButtonLog, Color.FromArgb(226, 232, 240), _text);
            logButton.Click += delegate { if (_host != null) _host.OpenLog(); };
            Button closeButton = CreateButton(StringId.SettingsClose, Color.FromArgb(226, 232, 240), _text);
            closeButton.Click += delegate { Close(); };
            actions.Controls.Add(resetButton);
            actions.Controls.Add(diagnosticsButton);
            actions.Controls.Add(logButton);
            actions.Controls.Add(closeButton);
            UiLayout.Add(content, actions);

            LoadFromHost();
            ApplyLanguage();
            Shown += delegate { UiLayout.FitToWorkingArea(this); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_allowClose) return;
                // Keep the window alive so the tray menu and the dashboard can bring the same instance back.
                e.Cancel = true;
                FlushPendingChanges();
                Hide();
            };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };
        }

        private void BuildButtonGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupButton);
            _enableBox = UiLayout.Localize(new WrappingCheckBox { AutoSize = true, ForeColor = _text }, StringId.SettingsEnable);
            _enableBox.CheckedChanged += delegate
            {
                if (_loading) return;
                bool requested = _enableBox.Checked;
                bool effective = _host == null ? requested : _host.RequestOverlayEnabled(requested);
                if (effective != requested)
                {
                    // The host refused the change (the user cancelled the warning): show the real state again.
                    _loading = true;
                    _enableBox.Checked = effective;
                    _loading = false;
                }
                RefreshStatus();
            };
            UiLayout.Add(group, _enableBox);
            UiLayout.Add(group, Help(StringId.SettingsEnableHelp));
        }

        private void BuildVisibilityGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupVisibility);
            _modeAlways = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.SettingsModeAlways);
            _modeHover = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.SettingsModeHover);
            _modeAlways.CheckedChanged += delegate { if (!_loading) ModeChanged(); };
            _modeHover.CheckedChanged += delegate { if (!_loading) ModeChanged(); };
            UiLayout.Add(group, _modeAlways);
            UiLayout.Add(group, _modeHover);
            UiLayout.Add(group, Help(StringId.SettingsModeHelp));
        }

        private void BuildGeometryGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupSize);

            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, StringId.SettingsSizeLabel));
            _sizeTrack = new TrackBar();
            _sizeTrack.Minimum = AppInfo.MinButtonSizeLogical;
            _sizeTrack.Maximum = AppInfo.MaxButtonSizeLogical;
            _sizeTrack.TickFrequency = 2;
            _sizeTrack.SmallChange = 1;
            _sizeTrack.LargeChange = 2;
            _sizeTrack.AutoSize = false;
            _sizeTrack.Width = 240;
            _sizeTrack.Height = 34;
            _sizeTrack.Margin = new Padding(0, 0, 0, 0);
            _sizeTrack.ValueChanged += delegate { if (!_loading) SizeChanged(); };
            // A drag applies on a short debounce; releasing the slider flushes the final value at once.
            _sizeTrack.MouseUp += delegate { FlushPendingChanges(); };
            UiLayout.Add(group, _sizeTrack);

            _sizeValue = new NumericUpDown();
            _sizeValue.Minimum = AppInfo.MinButtonSizeLogical;
            _sizeValue.Maximum = AppInfo.MaxButtonSizeLogical;
            _sizeValue.Width = 70;
            _sizeValue.ValueChanged += delegate { if (!_loading) SizeChanged(); };
            UiLayout.Add(group, _sizeValue);

            _insetValue = NumberRow(group, StringId.SettingsInsetLabel, AppInfo.MinButtonInsetLogical,
                AppInfo.MaxButtonInsetLogical, 1, delegate { if (!_loading) ScheduleApply(); });
            UiLayout.Add(group, Help(StringId.SettingsGeometryHelp));
        }

        private void BuildAdvancedGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupAdvanced);
            _graceValue = NumberRow(group, StringId.SettingsHoverGraceLabel, AppInfo.MinHoverGraceMs,
                AppInfo.MaxHoverGraceMs, 50, delegate { if (!_loading) ScheduleApply(); });
            _intervalValue = NumberRow(group, StringId.SettingsScanIntervalLabel, AppInfo.MinScanIntervalMs,
                AppInfo.MaxScanIntervalMs, 50, delegate { if (!_loading) ScheduleApply(); });
            UiLayout.Add(group, Help(StringId.SettingsAdvancedHelp));
        }

        private void BuildGeneralGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupGeneral);
            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, StringId.SettingsLanguage));
            _languageVietnamese = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.LanguageVietnamese);
            _languageEnglish = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.LanguageEnglish);
            _languageVietnamese.CheckedChanged += delegate { if (!_loading) LanguageChanged(); };
            _languageEnglish.CheckedChanged += delegate { if (!_loading) LanguageChanged(); };
            UiLayout.Add(group, _languageVietnamese);
            UiLayout.Add(group, _languageEnglish);

            _startupBox = UiLayout.Localize(new WrappingCheckBox { AutoSize = true, ForeColor = _text, Margin = new Padding(0, 6, 0, 0) }, StringId.SettingsStartWithWindows);
            _startupBox.CheckedChanged += delegate
            {
                if (_loading || _host == null) return;
                _host.SetLaunchAtLogin(_startupBox.Checked);
                _loading = true;
                _startupBox.Checked = _host.LaunchAtLogin;
                _loading = false;
            };
            UiLayout.Add(group, _startupBox);
            UiLayout.Add(group, Help(StringId.SettingsStartupHelp));
        }

        /// <summary>Reads the live values back from the host and shows them without triggering change handling.</summary>
        public void LoadFromHost()
        {
            AppSettings settings = _host == null ? AppSettings.Defaults() : _host.Settings.Copy();
            settings.Normalize();
            _loading = true;
            try
            {
                _enableBox.Checked = _host == null ? settings.OverlayEnabled : _host.OverlayEnabled;
                _modeAlways.Checked = settings.VisibilityMode == OverlayVisibilityMode.AlwaysWhenSafe;
                _modeHover.Checked = settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab;
                _sizeValue.Value = settings.ButtonSizeLogical;
                _sizeTrack.Value = settings.ButtonSizeLogical;
                _insetValue.Value = settings.ButtonInsetLogical;
                _graceValue.Value = settings.HoverGraceMs;
                _intervalValue.Value = settings.ScanIntervalMs;
                _languageVietnamese.Checked = settings.Language == AppLanguage.Vietnamese;
                _languageEnglish.Checked = settings.Language == AppLanguage.English;
                _startupBox.Checked = _host != null && _host.LaunchAtLogin;
            }
            finally
            {
                _loading = false;
            }
            RefreshStatus();
        }

        private void ModeChanged()
        {
            if (_modeAlways.Checked == _modeHover.Checked)
            {
                // Keep exactly one mode selected.
                _loading = true;
                _modeAlways.Checked = !_modeHover.Checked;
                _loading = false;
            }
            ScheduleApply();
            RefreshStatus();
        }

        private void SizeChanged()
        {
            if (_sizeValue.Value != _sizeTrack.Value)
            {
                _loading = true;
                if (_sizeTrack.Focused) _sizeValue.Value = _sizeTrack.Value;
                else _sizeTrack.Value = (int)_sizeValue.Value;
                _loading = false;
            }
            ScheduleApply();
            RefreshStatus();
        }

        private void LanguageChanged()
        {
            if (_languageVietnamese.Checked == _languageEnglish.Checked)
            {
                _loading = true;
                _languageVietnamese.Checked = !_languageEnglish.Checked;
                _loading = false;
            }
            // A language change is visible immediately, so it is applied and saved without waiting for the debounce.
            ScheduleApply();
            FlushPendingChanges();
        }

        /// <summary>Builds a validated settings instance from the controls. Every value is inside its documented range.</summary>
        public AppSettings BuildSettings()
        {
            AppSettings settings = _host == null ? AppSettings.Defaults() : _host.Settings.Copy();
            settings.OverlayEnabled = _host == null ? _enableBox.Checked : _host.OverlayEnabled;
            settings.VisibilityMode = _modeHover.Checked ? OverlayVisibilityMode.HoverOnSelectedTab : OverlayVisibilityMode.AlwaysWhenSafe;
            settings.ButtonSizeLogical = (int)_sizeValue.Value;
            settings.ButtonInsetLogical = (int)_insetValue.Value;
            settings.HoverGraceMs = (int)_graceValue.Value;
            settings.ScanIntervalMs = (int)_intervalValue.Value;
            settings.Language = _languageEnglish.Checked ? AppLanguage.English : AppLanguage.Vietnamese;
            settings.Normalize();
            return settings;
        }

        private void ScheduleApply()
        {
            _pendingApply = true;
            _applyTimer.Stop();
            _applyTimer.Start();
        }

        /// <summary>Applies any pending change now. Called by the debounce timer, on mouse-up and before hiding.</summary>
        public void FlushPendingChanges()
        {
            _applyTimer.Stop();
            if (!_pendingApply) return;
            _pendingApply = false;
            if (_host == null) return;
            _host.ApplySettings(BuildSettings());
            RefreshStatus();
        }

        private void ResetToDefaults()
        {
            DialogResult answer = MessageBox.Show(Strings.Get(StringId.SettingsResetConfirm), Strings.Get(StringId.SettingsTitle),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            _pendingApply = false;
            _applyTimer.Stop();
            if (_host != null)
            {
                AppSettings defaults = AppSettings.Defaults();
                // The start-with-Windows entry lives in the registry and is deliberately not part of the reset.
                defaults.OverlayEnabled = _host.RequestOverlayEnabled(true);
                _host.ApplySettings(defaults);
            }
            LoadFromHost();
            UiLayout.ApplyLanguage(this);
            _status.Text = Strings.Get(StringId.SettingsResetDone);
        }

        private void RefreshStatus()
        {
            if (_status == null) return;
            AppSettings settings = BuildSettings();
            _status.Text = Strings.Get(settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab
                ? StringId.SettingsModeHover : StringId.SettingsModeAlways);
        }

        /// <summary>Re-applies every localized text and the live values. Used after a language change.</summary>
        public void ApplyLanguage()
        {
            UiLayout.ApplyLanguage(this);
            Text = AppInfo.ProductName + " - " + Strings.Get(StringId.SettingsTitle);
            RefreshStatus();
        }

        private TableLayoutPanel Group(TableLayoutPanel parent, StringId titleId)
        {
            TableLayoutPanel group = UiLayout.Stack();
            group.BackColor = Color.White;
            group.Padding = new Padding(14, 10, 14, 12);
            group.Margin = new Padding(0, 6, 0, 6);
            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { Font = OwnFont(10F, FontStyle.Bold), ForeColor = _navy }, titleId));
            UiLayout.Add(parent, group);
            return group;
        }

        private WrappingLabel Help(StringId id)
        {
            return UiLayout.Localize(new WrappingLabel { ForeColor = _muted, Margin = new Padding(0, 4, 0, 0) }, id);
        }

        private NumericUpDown NumberRow(TableLayoutPanel parent, StringId labelId, int minimum, int maximum, int increment,
            EventHandler changed)
        {
            UiLayout.Add(parent, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, labelId));
            NumericUpDown numeric = new NumericUpDown();
            numeric.Minimum = minimum;
            numeric.Maximum = maximum;
            numeric.Increment = increment;
            numeric.Width = 90;
            if (changed != null) numeric.ValueChanged += changed;
            UiLayout.Add(parent, numeric);
            return numeric;
        }

        private Font OwnFont(float size, FontStyle style)
        {
            Font font = new Font("Segoe UI", size, style);
            _fonts.Add(font);
            return font;
        }

        private Button CreateButton(StringId id, Color backColor, Color foreColor)
        {
            Button button = new Button
            {
                MinimumSize = new Size(150, 38),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = OwnFont(9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 8, 4),
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.08F);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.06F);
            return UiLayout.Localize(button, id);
        }

        /// <summary>Lets the tray context really close the window during application shutdown.</summary>
        public void PrepareForExit()
        {
            _allowClose = true;
            _applyTimer.Stop();
            _pendingApply = false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _applyTimer.Dispose();
                foreach (Font font in _fonts) font.Dispose();
                _fonts.Clear();
            }
        }
    }
}
