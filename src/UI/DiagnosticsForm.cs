using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// Shows the current state, the last scan in detail and the bounded statistics behind the ×.
    ///
    /// Two rules are visible in the layout on purpose: current state is always separate from the last scan's result,
    /// and a result that is older than the staleness window is labelled as stale instead of being presented as the
    /// current confirmation. Tab titles appear in the list below only; the copied report (and every log line) leaves
    /// them out.
    ///
    /// Every displayed sentence is produced on demand from the current language, and the render cache is dropped on a
    /// language change so nothing on screen keeps the previous translation.
    /// </summary>
    internal sealed class DiagnosticsForm : Form
    {
        private readonly Font _ownedFont;
        private readonly TextBox _status;
        private readonly TextBox _summary;
        private readonly ListView _list;
        private readonly Label _copyStatus;
        private readonly Button _copyButton;
        private ActiveTabSnapshot _lastSnapshot;
        private string _renderSignature = string.Empty;

        public DiagnosticsForm()
        {
            MinimumSize = new Size(360, 280);
            Size = new Size(980, 620);
            StartPosition = FormStartPosition.CenterScreen;
            _ownedFont = new Font("Segoe UI", 9F);
            Font = _ownedFont;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);

            _status = ReadableText();
            _status.Text = Strings.Get(StringId.DiagWaiting);
            _summary = ReadableText();
            _summary.BackColor = Color.FromArgb(235, 239, 245);

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = true;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.ShowItemToolTips = true;
            _list.HideSelection = false;

            TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _copyButton = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(150, 32),
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Color.FromArgb(226, 232, 240),
                Cursor = Cursors.Hand
            };
            _copyButton.FlatAppearance.BorderSize = 0;
            _copyButton.Click += delegate { CopyReport(); };
            UiLayout.Localize(_copyButton, StringId.DiagCopyReport);
            _copyStatus = new WrappingLabel { ForeColor = Color.FromArgb(100, 116, 139), Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft };
            actions.Controls.Add(_copyButton, 0, 0);
            actions.Controls.Add(_copyStatus, 1, 0);

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(_status, 0, 0);
            layout.Controls.Add(_list, 0, 1);
            layout.Controls.Add(_summary, 0, 2);
            layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);
            ApplyLanguage();
            Shown += delegate
            {
                UiLayout.FitToWorkingArea(this);
                foreach (ColumnHeader column in _list.Columns) column.Width = Math.Max(column.Width, TextRenderer.MeasureText(column.Text, _list.Font).Width + 24);
            };
        }

        /// <summary>Re-applies the window title and column headers and forces the next render to redraw.</summary>
        public void ApplyLanguage()
        {
            Text = AppInfo.ProductName + " - " + Strings.Get(StringId.DiagTitle) + " " + AppInfo.DisplayVersion;
            string[] headers = new string[]
            {
                Strings.Get(StringId.DiagColumnTab),
                Strings.Get(StringId.DiagColumnSelected),
                Strings.Get(StringId.DiagColumnHorizontal),
                Strings.Get(StringId.DiagColumnBounds),
                Strings.Get(StringId.DiagColumnOffscreen)
            };
            if (_list.Columns.Count != headers.Length)
            {
                _list.Columns.Clear();
                foreach (string header in headers) _list.Columns.Add(header, 150);
            }
            else
            {
                for (int index = 0; index < headers.Length; index++) _list.Columns[index].Text = headers[index];
            }
            UiLayout.ApplyLanguage(this);
            _status.Text = Strings.Get(StringId.DiagWaiting);
            _summary.Text = string.Empty;
            _renderSignature = string.Empty;
            if (_lastSnapshot != null) Render(_lastSnapshot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _ownedFont != null) _ownedFont.Dispose();
        }

        private static TextBox ReadableText()
        {
            return new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = true,
                ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        }

        /// <summary>
        /// The sanitized report the copy button puts on the clipboard. Exposed so a test can assert that a tab title
        /// present in the list view never reaches the report.
        /// </summary>
        public string BuildReportText()
        {
            return DiagnosticsReport.Build(_lastSnapshot, _lastSnapshot == null ? null : _lastSnapshot.Settings);
        }

        private void CopyReport()
        {
            try
            {
                Clipboard.SetText(BuildReportText());
                _copyStatus.Text = Strings.Get(StringId.DiagCopiedBody);
            }
            catch (Exception ex)
            {
                _copyStatus.Text = Strings.Get(StringId.DiagCopyFailed, ex.GetType().Name);
            }
        }

        public void Render(ActiveTabSnapshot snapshot)
        {
            if (IsDisposed || snapshot == null) return;
            _lastSnapshot = snapshot;
            ScanResult result = snapshot.Scan;
            string signature = BuildSignature(snapshot, result);
            if (signature == _renderSignature) return;
            _renderSignature = signature;

            if (result == null)
            {
                _status.Text = CurrentStateText(snapshot);
                _list.Items.Clear();
                _summary.Text = BuildSummary(snapshot, null);
                return;
            }

            _status.Text = CurrentStateText(snapshot) + Strings.Get(StringId.DiagLastScanLine, result.Message, result.Outcome);
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (TabInfo tab in result.Tabs)
            {
                ListViewItem item = new ListViewItem(string.IsNullOrEmpty(tab.Name) ? Strings.Get(StringId.DiagUntitledTab) : tab.Name);
                item.ToolTipText = tab.Name;
                item.SubItems.Add(tab.SelectionKnown ? (tab.Selected ? Strings.Get(StringId.Yes) : Strings.Get(StringId.No)) : Strings.Get(StringId.Unknown));
                item.SubItems.Add(tab.IsHorizontalCandidate ? Strings.Get(StringId.Yes) : Strings.Get(StringId.No));
                item.SubItems.Add(FormatRect(tab.Bounds));
                item.SubItems.Add(tab.IsOffscreen ? Strings.Get(StringId.Yes) : Strings.Get(StringId.No));
                if (tab.Selected && tab.IsHorizontalCandidate) item.BackColor = Color.FromArgb(220, 245, 220);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _summary.Text = BuildSummary(snapshot, result);
        }

        private static string CurrentStateText(ActiveTabSnapshot snapshot)
        {
            return snapshot.OverlayVisible ? Strings.Get(StringId.DiagVisibleNow) : snapshot.HiddenReason;
        }

        private static string BuildSummary(ActiveTabSnapshot snapshot, ScanResult result)
        {
            StringBuilder summary = new StringBuilder();
            if (result == null)
            {
                summary.Append(Strings.Get(StringId.DiagNoScanYet));
            }
            else
            {
                summary.Append(Strings.Get(StringId.DiagSummary, result.NodesVisited, result.ElapsedMs, result.Dpi));
                summary.Append(snapshot.OverlayVisible ? Strings.Get(StringId.DiagSummaryVisible) : Strings.Get(StringId.DiagSummaryHidden));
                if (!string.IsNullOrEmpty(result.Error)) summary.Append(Strings.Get(StringId.DiagErrorLabel, result.Error));
            }

            summary.Append(Strings.Get(StringId.DiagForegroundLine, ForegroundText(snapshot)));
            summary.Append(Strings.Get(StringId.DiagProfileLine, ProfileText(snapshot.Profile)));
            if (snapshot.Metrics != null) summary.Append(Strings.Get(StringId.DiagMetricsLine, snapshot.Metrics.Describe()));
            summary.Append(Strings.Get(StringId.DiagWorkerLine, Describe(snapshot.OverlayWorker), Describe(snapshot.CloseWorker),
                Describe(snapshot.DashboardWorker)));
            if (snapshot.Watcher != null) summary.Append(Strings.Get(StringId.DiagWatcherLine, snapshot.Watcher.Describe()));
            if (snapshot.LastScanAgeMs >= 0)
            {
                summary.Append(snapshot.ScanStale
                    ? Strings.Get(StringId.DiagFreshnessStale, snapshot.LastScanAgeMs, AppInfo.ScanStaleMs)
                    : Strings.Get(StringId.DiagFreshnessCurrent, snapshot.LastScanAgeMs));
            }
            if (!string.IsNullOrEmpty(snapshot.EffectiveSettings))
                summary.Append(Strings.Get(StringId.DiagEffectiveLine, snapshot.EffectiveSettings));
            return summary.ToString();
        }

        private static string ForegroundText(ActiveTabSnapshot snapshot)
        {
            BrowserDescriptor descriptor;
            if (snapshot.SupportedBrowserInForeground && BrowserCatalog.TryGet(snapshot.Browser, out descriptor))
                return Strings.Get(StringId.SettingsBrowserRow, descriptor.DisplayName, SupportText(descriptor.Support));
            if (snapshot.ForegroundBrowserDisabled && BrowserCatalog.TryGet(snapshot.Browser, out descriptor))
                return Strings.Get(StringId.SettingsBrowserRow, descriptor.DisplayName, SupportText(descriptor.Support));
            if (snapshot.OwnWindowInForeground) return Strings.Get(StringId.DiagOwnWindowReason);
            return Strings.Get(StringId.IdleBrowserNotForeground);
        }

        private static string SupportText(BrowserSupport support)
        {
            if (support == BrowserSupport.Verified) return Strings.Get(StringId.SupportVerified);
            if (support == BrowserSupport.Experimental) return Strings.Get(StringId.SupportExperimental);
            return Strings.Get(StringId.SupportUnsupported);
        }

        private static string ProfileText(PerformanceProfile profile)
        {
            if (profile == PerformanceProfile.Fast) return Strings.Get(StringId.ProfileFast);
            if (profile == PerformanceProfile.LowResource) return Strings.Get(StringId.ProfileLowResource);
            if (profile == PerformanceProfile.Custom) return Strings.Get(StringId.ProfileCustom);
            return Strings.Get(StringId.ProfileBalanced);
        }

        private static string Describe(WorkerSnapshot worker)
        {
            return worker == null ? "n/a" : worker.Describe();
        }

        private static string BuildSignature(ActiveTabSnapshot snapshot, ScanResult result)
        {
            StringBuilder signature = new StringBuilder(160);
            signature.Append(snapshot.OverlayVisible).Append('|').Append(snapshot.HiddenReason).Append('|')
                .Append(snapshot.IdleReason).Append('|').Append(snapshot.Notice).Append('|')
                .Append(snapshot.Profile).Append('|').Append(snapshot.Browser).Append('|')
                .Append(snapshot.SupportedBrowserInForeground).Append('|').Append(snapshot.OwnWindowInForeground).Append('|')
                .Append(snapshot.ScanStale).Append('|').Append(snapshot.EffectiveSettings).Append('|');
            if (snapshot.Metrics != null) signature.Append(snapshot.Metrics.Describe()).Append('|');
            if (result == null) return signature.ToString();
            signature.Append(result.Outcome).Append('|').Append(result.Message).Append('|').Append(result.Error).Append('|')
                .Append(result.NodesVisited).Append('|').Append(result.ElapsedMs).Append('|').Append(result.Dpi);
            foreach (TabInfo tab in result.Tabs)
            {
                signature.Append('|').Append(tab.Name).Append(':').Append(tab.Selected).Append(':').Append(tab.Bounds)
                    .Append(':').Append(tab.SelectionKnown).Append(':').Append(tab.IsOffscreen).Append(':').Append(tab.IsHorizontalCandidate);
            }
            return signature.ToString();
        }

        private static string FormatRect(Rectangle r)
        {
            if (r == Rectangle.Empty) return Strings.Get(StringId.DiagNoRect);
            return "L" + r.Left + " T" + r.Top + " R" + r.Right + " B" + r.Bottom;
        }
    }
}
