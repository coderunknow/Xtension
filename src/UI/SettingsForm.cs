using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// The dedicated Xtension Settings window, organised into four sections: General, Appearance, Performance and
    /// Advanced. It owns no state of its own: every change is turned into an <see cref="AppSettings"/> instance,
    /// handed to the host (which applies it to the controller and persists it) and read back from the host, so the
    /// window, the dashboard, the tray menu and the overlay cannot disagree.
    ///
    /// Discrete controls (check boxes, radios, numeric boxes) apply and save immediately. The size slider applies on a
    /// short debounce while dragging and flushes the final value on mouse-up and when the window is hidden.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly ISettingsHost _host;
        private readonly List<Font> _fonts = new List<Font>();
        private readonly List<CheckBox> _browserBoxes = new List<CheckBox>();
        private readonly List<BrowserDescriptor> _browserDescriptors = new List<BrowserDescriptor>();
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
        private NumericUpDown _opacityValue;
        private CheckBox _borderBox;
        private RadioButton _colorDark;
        private RadioButton _colorRed;
        private RadioButton _colorBlue;
        private RadioButton _colorGreen;
        private RadioButton _profileBalanced;
        private RadioButton _profileFast;
        private RadioButton _profileLow;
        private RadioButton _profileCustom;
        private Label _profileSummary;
        private RadioButton _languageVietnamese;
        private RadioButton _languageEnglish;
        private CheckBox _startupBox;
        private Label _status;
        // Four shared fonts instead of a new Font object per control: fewer GDI objects to create, compare and
        // dispose, and the window scales as one unit because every control that shares a role shares the object.
        private readonly Font _bodyFont;
        private readonly Font _buttonFont;
        private readonly Font _groupFont;
        private readonly Font _titleFont;
        private bool _loading;
        private bool _pendingApply;
        private bool _allowClose;

        public SettingsForm(ISettingsHost host)
        {
            _host = host;
            _bodyFont = OwnFont(9F, FontStyle.Regular);
            _buttonFont = OwnFont(9F, FontStyle.Bold);
            _groupFont = OwnFont(10F, FontStyle.Bold);
            _titleFont = OwnFont(15F, FontStyle.Bold);
            AutoScaleMode = AutoScaleMode.Font;
            Font = _bodyFont;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(400, 320);
            Size = new Size(780, 760);
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
            // Build the whole tree with layout suspended. WinForms lays a container out again for every row that is
            // added to it, so building a window row by row costs one full layout per row; this makes it one pass.
            SuspendLayout();
            viewport.SuspendLayout();
            content.SuspendLayout();
            try
            {
                TableLayoutPanel header = UiLayout.Stack();
                header.BackColor = _navy;
                header.Padding = new Padding(18, 12, 18, 12);
                UiLayout.Add(header, UiLayout.Localize(new WrappingLabel { Font = _titleFont, ForeColor = Color.White }, StringId.SettingsTitle));
                UiLayout.Add(header, UiLayout.Localize(new WrappingLabel { ForeColor = Color.FromArgb(203, 213, 225) }, StringId.SettingsIntro));
                UiLayout.Add(content, header);

                BuildGeneralGroup(content);
                BuildAppearanceGroup(content);
                BuildPerformanceGroup(content);
                BuildBrowsersGroup(content);
                BuildAdvancedGroup(content);

                UiLayout.Add(content, UiLayout.Localize(new WrappingLabel
                {
                    Padding = new Padding(14, 10, 12, 10), BackColor = Color.White, ForeColor = _muted, Margin = new Padding(0, 10, 0, 6)
                }, StringId.SettingsBraveNote));

                _status = new WrappingLabel { ForeColor = _muted, Padding = new Padding(0, 6, 0, 0) };
                UiLayout.Add(content, _status);

                FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
                Button resetButton = CreateButton(StringId.SettingsReset, Color.FromArgb(254, 226, 226), Color.FromArgb(185, 28, 28));
                resetButton.Click += delegate { ResetToDefaults(); };
                Button closeButton = CreateButton(StringId.SettingsClose, Color.FromArgb(226, 232, 240), _text);
                closeButton.Click += delegate { Close(); };
                actions.Controls.Add(resetButton);
                actions.Controls.Add(closeButton);
                UiLayout.Add(content, actions);
            }
            finally
            {
                content.ResumeLayout(false);
                viewport.ResumeLayout(false);
                ResumeLayout(false);
                PerformLayout();
            }

            AppIcon.Attach(this);
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

        private void BuildGeneralGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupGeneral);
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

            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, StringId.SettingsLanguage));
            _languageVietnamese = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.LanguageVietnamese);
            _languageEnglish = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.LanguageEnglish);
            _languageVietnamese.CheckedChanged += delegate { if (!_loading) LanguageChanged(); };
            _languageEnglish.CheckedChanged += delegate { if (!_loading) LanguageChanged(); };
            FlowLayoutPanel languageRow = RadioRow();
            languageRow.Controls.Add(_languageVietnamese);
            languageRow.Controls.Add(_languageEnglish);
            UiLayout.Add(group, languageRow);

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

        /// <summary>
        /// Which browsers the × may appear in. Only browsers at or above their support level are listed, so the list a
        /// user sees is the list this build can really confirm a selected tab for.
        /// </summary>
        private void BuildBrowsersGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupBrowsers);
            foreach (BrowserDescriptor descriptor in BrowserCatalog.All)
            {
                if (!BrowserCatalog.IsSupported(descriptor.Kind)) continue;
                CheckBox box = new WrappingCheckBox { AutoSize = true, ForeColor = _text };
                UiLayout.Localize(box, StringId.SettingsBrowserRow, descriptor.DisplayName, SupportLabel(descriptor.Support));
                box.CheckedChanged += delegate { if (!_loading) BrowsersChanged(); };
                _browserBoxes.Add(box);
                _browserDescriptors.Add(descriptor);
                UiLayout.Add(group, box);
            }
            UiLayout.Add(group, Help(StringId.SettingsBrowsersHelp));
        }

        private static string SupportLabel(BrowserSupport support)
        {
            if (support == BrowserSupport.Verified) return Strings.Get(StringId.SupportVerified);
            if (support == BrowserSupport.Experimental) return Strings.Get(StringId.SupportExperimental);
            return Strings.Get(StringId.SupportUnsupported);
        }

        private void BuildAppearanceGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupAppearance);

            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { Font = _groupFont, ForeColor = _muted, Padding = new Padding(0, 6, 0, 0) }, StringId.SettingsGroupVisibility));
            _modeAlways = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.SettingsModeAlways);
            _modeHover = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.SettingsModeHover);
            _modeAlways.CheckedChanged += delegate { if (!_loading) ModeChanged(); };
            _modeHover.CheckedChanged += delegate { if (!_loading) ModeChanged(); };
            FlowLayoutPanel modeRow = RadioRow();
            modeRow.Controls.Add(_modeAlways);
            modeRow.Controls.Add(_modeHover);
            UiLayout.Add(group, modeRow);
            UiLayout.Add(group, Help(StringId.SettingsModeHelp));

            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { Font = _groupFont, ForeColor = _muted, Padding = new Padding(0, 6, 0, 0) }, StringId.SettingsGroupSize));
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
            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, StringId.SettingsSizeLabel));
            UiLayout.Add(group, _sizeTrack);

            _sizeValue = new NumericUpDown();
            _sizeValue.Minimum = AppInfo.MinButtonSizeLogical;
            _sizeValue.Maximum = AppInfo.MaxButtonSizeLogical;
            _sizeValue.Width = 70;
            _sizeValue.ValueChanged += delegate { if (!_loading) SizeChanged(); };
            UiLayout.Add(group, _sizeValue);

            _insetValue = NumberRow(group, StringId.SettingsInsetLabel, AppInfo.MinButtonInsetLogical,
                AppInfo.MaxButtonInsetLogical, 1, delegate { if (!_loading) ScheduleApply(); });
            _graceValue = NumberRow(group, StringId.SettingsHoverGraceLabel, AppInfo.MinHoverGraceMs,
                AppInfo.MaxHoverGraceMs, 50, delegate { if (!_loading) ScheduleApply(); });
            UiLayout.Add(group, Help(StringId.SettingsGeometryHelp));

            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text, Padding = new Padding(0, 6, 0, 0) }, StringId.SettingsColorLabel));
            _colorDark = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ColorDark);
            _colorRed = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ColorRed);
            _colorBlue = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ColorBlue);
            _colorGreen = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ColorGreen);
            FlowLayoutPanel colourRow = RadioRow();
            foreach (RadioButton button in new RadioButton[] { _colorDark, _colorRed, _colorBlue, _colorGreen })
            {
                button.CheckedChanged += delegate { if (!_loading) ColorChanged(); };
                colourRow.Controls.Add(button);
            }
            UiLayout.Add(group, colourRow);

            _opacityValue = NumberRow(group, StringId.SettingsOpacityLabel, AppInfo.MinButtonOpacityPercent,
                AppInfo.MaxButtonOpacityPercent, 5, delegate { if (!_loading) ScheduleApply(); });
            _borderBox = UiLayout.Localize(new WrappingCheckBox { AutoSize = true, ForeColor = _text }, StringId.SettingsBorderLabel);
            _borderBox.CheckedChanged += delegate { if (!_loading) ScheduleApply(); };
            UiLayout.Add(group, _borderBox);
            UiLayout.Add(group, Help(StringId.SettingsAppearanceHelp));
        }

        /// <summary>
        /// Performance profiles. A preset only writes profile-controlled values (the scan interval); the store keeps
        /// the individual values authoritative, so editing the interval afterwards is shown as "Custom" instead of
        /// being reset to the preset on the next start.
        /// </summary>
        private void BuildPerformanceGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupPerformance);
            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { ForeColor = _text }, StringId.SettingsProfileLabel));
            _profileBalanced = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ProfileBalanced);
            _profileFast = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ProfileFast);
            _profileLow = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ProfileLowResource);
            _profileCustom = UiLayout.Localize(new RadioButton { AutoSize = true, ForeColor = _text }, StringId.ProfileCustom);
            FlowLayoutPanel profileRow = RadioRow();
            foreach (RadioButton button in new RadioButton[] { _profileBalanced, _profileFast, _profileLow, _profileCustom })
            {
                button.CheckedChanged += delegate { if (!_loading) ProfileChanged(); };
                profileRow.Controls.Add(button);
            }
            UiLayout.Add(group, profileRow);
            _intervalValue = NumberRow(group, StringId.SettingsScanIntervalLabel, AppInfo.MinScanIntervalMs,
                AppInfo.MaxScanIntervalMs, 50, delegate { if (!_loading) ScheduleApply(); });
            _profileSummary = new WrappingLabel { ForeColor = _muted, Padding = new Padding(0, 4, 0, 0) };
            UiLayout.Add(group, _profileSummary);
            UiLayout.Add(group, Help(StringId.SettingsProfileHelp));
        }

        private void BuildAdvancedGroup(TableLayoutPanel content)
        {
            TableLayoutPanel group = Group(content, StringId.SettingsGroupAdvanced);
            FlowLayoutPanel row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 2, 0, 2) };
            Button diagnosticsButton = CreateButton(StringId.SettingsButtonDiagnostics, Color.FromArgb(226, 232, 240), _text);
            diagnosticsButton.Click += delegate { if (_host != null) _host.ShowDiagnostics(); };
            Button logButton = CreateButton(StringId.SettingsButtonLog, Color.FromArgb(226, 232, 240), _text);
            logButton.Click += delegate { if (_host != null) _host.OpenLog(); };
            Button exportButton = CreateButton(StringId.SettingsBackupExport, Color.FromArgb(226, 232, 240), _text);
            exportButton.Click += delegate { ExportBackup(); };
            Button importButton = CreateButton(StringId.SettingsBackupImport, Color.FromArgb(226, 232, 240), _text);
            importButton.Click += delegate { ImportBackup(); };
            row.Controls.Add(diagnosticsButton);
            row.Controls.Add(logButton);
            row.Controls.Add(exportButton);
            row.Controls.Add(importButton);
            UiLayout.Add(group, row);
            UiLayout.Add(group, Help(StringId.SettingsBackupHelp));
            UiLayout.Add(group, Help(StringId.SettingsAdvancedHelp));
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
                _opacityValue.Value = settings.ButtonOpacityPercent;
                _borderBox.Checked = settings.ButtonBorder;
                _colorDark.Checked = settings.ButtonColor == OverlayColor.Dark;
                _colorRed.Checked = settings.ButtonColor == OverlayColor.Red;
                _colorBlue.Checked = settings.ButtonColor == OverlayColor.Blue;
                _colorGreen.Checked = settings.ButtonColor == OverlayColor.Green;
                _profileBalanced.Checked = settings.Profile == PerformanceProfile.Balanced;
                _profileFast.Checked = settings.Profile == PerformanceProfile.Fast;
                _profileLow.Checked = settings.Profile == PerformanceProfile.LowResource;
                _profileCustom.Checked = settings.Profile == PerformanceProfile.Custom;
                _languageVietnamese.Checked = settings.Language == AppLanguage.Vietnamese;
                _languageEnglish.Checked = settings.Language == AppLanguage.English;
                _startupBox.Checked = _host != null && _host.LaunchAtLogin;
                for (int index = 0; index < _browserBoxes.Count; index++)
                {
                    _browserBoxes[index].Checked = BrowserCatalog.IsEnabled(settings.Browsers, _browserDescriptors[index].Kind);
                }
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

        private void ColorChanged()
        {
            if (_colorDark.Checked || _colorRed.Checked || _colorBlue.Checked || _colorGreen.Checked) { }
            else
            {
                // Keep exactly one colour selected.
                _loading = true;
                _colorDark.Checked = true;
                _loading = false;
            }
            ScheduleApply();
            RefreshStatus();
        }

        /// <summary>
        /// Applies a selected preset through the host, then reads the result back. Selecting a preset never touches
        /// the visibility mode, the geometry, the appearance, the language or the browser list.
        /// </summary>
        private void ProfileChanged()
        {
            if (_loading) return;
            PerformanceProfile chosen = _profileFast.Checked ? PerformanceProfile.Fast
                : _profileLow.Checked ? PerformanceProfile.LowResource
                : _profileCustom.Checked ? PerformanceProfile.Custom
                : PerformanceProfile.Balanced;
            if (Profiles.IsPreset(chosen))
            {
                AppSettings settings = BuildSettings();
                Profiles.ApplyTo(chosen, settings);
                _loading = true;
                try
                {
                    // Show the value the preset really selected, then read everything back from the host.
                    _intervalValue.Value = settings.ScanIntervalMs;
                }
                finally
                {
                    _loading = false;
                }
                if (_host != null) _host.ApplySettings(settings);
                LoadFromHost();
                return;
            }
            ScheduleApply();
            RefreshStatus();
        }

        private void BrowsersChanged()
        {
            int mask = BrowserCatalog.NoBrowsers;
            for (int index = 0; index < _browserBoxes.Count; index++)
            {
                if (_browserBoxes[index].Checked) mask |= BrowserCatalog.Mask(_browserDescriptors[index].Kind);
            }
            if (mask == BrowserCatalog.NoBrowsers)
            {
                // At least one browser must stay selected, or the × could never appear at all.
                _loading = true;
                if (_browserBoxes.Count > 0) _browserBoxes[0].Checked = true;
                _loading = false;
                ScheduleApply();
                RefreshStatus();
                return;
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
            settings.ButtonOpacityPercent = (int)_opacityValue.Value;
            settings.ButtonBorder = _borderBox.Checked;
            settings.ButtonColor = _colorRed.Checked ? OverlayColor.Red
                : _colorBlue.Checked ? OverlayColor.Blue
                : _colorGreen.Checked ? OverlayColor.Green
                : OverlayColor.Dark;
            int mask = BrowserCatalog.NoBrowsers;
            for (int index = 0; index < _browserBoxes.Count; index++)
            {
                if (_browserBoxes[index].Checked) mask |= BrowserCatalog.Mask(_browserDescriptors[index].Kind);
            }
            if (mask != BrowserCatalog.NoBrowsers) settings.Browsers = mask;
            // The profile marker is derived, never trusted: a preset whose value is no longer the stored one is custom.
            PerformanceProfile marker = _profileFast.Checked ? PerformanceProfile.Fast
                : _profileLow.Checked ? PerformanceProfile.LowResource
                : _profileCustom.Checked ? PerformanceProfile.Custom
                : PerformanceProfile.Balanced;
            settings.Profile = Profiles.Resolve(marker, settings.ScanIntervalMs);
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
            AppSettings applied = BuildSettings();
            _host.ApplySettings(applied);
            // Editing a profile-controlled value turns the configuration into Custom. Showing that immediately keeps
            // the radios from claiming a preset the stored values no longer match.
            if (applied.Profile == PerformanceProfile.Custom && !_profileCustom.Checked)
            {
                _loading = true;
                try { _profileCustom.Checked = true; }
                finally { _loading = false; }
            }
            RefreshStatus();
        }

        private void ExportBackup()
        {
            if (_host == null) return;
            FlushPendingChanges();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = Strings.Get(StringId.BackupExportTitle);
                dialog.Filter = Strings.Get(StringId.BackupFileFilter);
                dialog.FileName = "xtension-settings.ini";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                // The result is shown in the window's own status line: no modal dialog is opened for a routine
                // outcome, so a settings action can never steal focus from what the user is doing.
                _status.Text = _host.ExportSettings(dialog.FileName);
            }
        }

        private void ImportBackup()
        {
            if (_host == null) return;
            FlushPendingChanges();
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = Strings.Get(StringId.BackupImportTitle);
                dialog.Filter = Strings.Get(StringId.BackupFileFilter);
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ImportBackupFile(dialog.FileName);
            }
        }

        /// <summary>Refresh values and language before showing the import result, so refresh cannot erase it.</summary>
        internal void ImportBackupFile(string path)
        {
            if (_host == null) return;
            string result = _host.ImportSettings(path);
            LoadFromHost();
            ApplyLanguage();
            _status.Text = result;
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
            // The line under the profile radios states which profile is really in force and at which cadence, so a
            // value the user edited is reported as Custom instead of being presented as the preset they once picked.
            if (_profileSummary != null)
            {
                _profileSummary.Text = Strings.Get(StringId.ProfileSummaryLine, ProfileText(settings.Profile),
                    settings.ScanIntervalMs, Profiles.CadenceFor(settings.Profile).ForegroundPollMs);
            }
            _status.Text = Strings.Get(settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab
                ? StringId.SettingsModeHover : StringId.SettingsModeAlways);
        }

        private static string ProfileText(PerformanceProfile profile)
        {
            if (profile == PerformanceProfile.Fast) return Strings.Get(StringId.ProfileFast);
            if (profile == PerformanceProfile.LowResource) return Strings.Get(StringId.ProfileLowResource);
            if (profile == PerformanceProfile.Custom) return Strings.Get(StringId.ProfileCustom);
            return Strings.Get(StringId.ProfileBalanced);
        }

        /// <summary>Re-applies every localized text and the live values. Used after a language change.</summary>
        public void ApplyLanguage()
        {
            UiLayout.ApplyLanguage(this);
            Text = AppInfo.ProductName + " - " + Strings.Get(StringId.SettingsTitle);
            for (int index = 0; index < _browserBoxes.Count; index++)
            {
                LocalizedText text = _browserBoxes[index].Tag as LocalizedText;
                if (text == null) continue;
                _browserBoxes[index].Tag = new LocalizedText(StringId.SettingsBrowserRow, new object[]
                {
                    _browserDescriptors[index].DisplayName, SupportLabel(_browserDescriptors[index].Support)
                });
                UiLayout.ApplyLocalizedText(_browserBoxes[index]);
            }
            RefreshStatus();
        }

        /// <summary>
        /// One row that holds exactly one logical radio group. WinForms makes radio buttons mutually exclusive by
        /// their parent container, so two unrelated groups that share a container uncheck each other: with the
        /// visibility-mode radios and the colour radios in the same group, choosing a colour switched the mode back to
        /// "always" without telling anybody. Each logical group therefore gets its own row.
        ///
        /// The row wraps, because the options must survive a narrow window and a large font. Without wrapping the row
        /// is exactly as wide as the column allows and any option past that width is silently clipped - which is what
        /// the harness caught at 100% and what the smallest supported window at 200% would have made much worse.
        /// </summary>
        private static FlowLayoutPanel RadioRow()
        {
            return new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 2),
                Padding = new Padding(0)
            };
        }

        private TableLayoutPanel Group(TableLayoutPanel parent, StringId titleId)
        {
            TableLayoutPanel group = UiLayout.Stack();
            group.BackColor = Color.White;
            group.Padding = new Padding(14, 10, 14, 12);
            group.Margin = new Padding(0, 6, 0, 6);
            UiLayout.Add(group, UiLayout.Localize(new WrappingLabel { Font = _groupFont, ForeColor = _navy }, titleId));
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
                Font = _buttonFont,
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
