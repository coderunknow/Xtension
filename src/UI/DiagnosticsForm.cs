using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>Shows the latest scan in detail. Tab titles appear here only; they are never written to the log.</summary>
    internal sealed class DiagnosticsForm : Form
    {
        private readonly Font _ownedFont;
        private readonly TextBox _status;
        private readonly TextBox _summary;
        private readonly ListView _list;
        private string _renderSignature = string.Empty;

        public DiagnosticsForm()
        {
            Text = AppInfo.ProductName + " — Chẩn đoán " + AppInfo.DisplayVersion;
            MinimumSize = new Size(360, 280);
            Size = new Size(980, 560);
            StartPosition = FormStartPosition.CenterScreen;
            _ownedFont = new Font("Segoe UI", 9F);
            Font = _ownedFont;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);

            _status = ReadableText();
            _status.Text = "Đang chờ kết quả quét…";
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
            _list.Columns.Add("Tab / tên truy cập", 300);
            _list.Columns.Add("Đang chọn", 90);
            _list.Columns.Add("Trong thanh ngang", 130);
            _list.Columns.Add("Vị trí màn hình (px)", 200);
            _list.Columns.Add("Ngoài màn hình", 120);

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            layout.Controls.Add(_status, 0, 0);
            layout.Controls.Add(_list, 0, 1);
            layout.Controls.Add(_summary, 0, 2);
            Controls.Add(layout);
            Shown += delegate
            {
                UiLayout.FitToWorkingArea(this);
                foreach (ColumnHeader column in _list.Columns) column.Width = Math.Max(column.Width, TextRenderer.MeasureText(column.Text, _list.Font).Width + 24);
            };
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

        public void Render(ActiveTabSnapshot snapshot)
        {
            if (IsDisposed || snapshot == null) return;
            ScanResult result = snapshot.Scan;
            string signature = BuildSignature(snapshot, result);
            if (signature == _renderSignature) return;
            _renderSignature = signature;

            if (result == null)
            {
                _status.Text = snapshot.HiddenReason;
                _list.Items.Clear();
                _summary.Text = "Chưa có kết quả quét. Nút × được ẩn cho đến khi Xtension xác nhận được tab đang chọn.";
                return;
            }

            _status.Text = (snapshot.OverlayVisible ? "Nút × đang hiển thị." : snapshot.HiddenReason) + "\r\nQuét gần nhất: " + result.Message + "   [" + result.Outcome + "]";
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (TabInfo tab in result.Tabs)
            {
                ListViewItem item = new ListViewItem(string.IsNullOrEmpty(tab.Name) ? "(TabItem không có tên)" : tab.Name);
                item.ToolTipText = tab.Name;
                item.SubItems.Add(tab.SelectionKnown ? (tab.Selected ? "CÓ" : "không") : "không rõ");
                item.SubItems.Add(tab.IsHorizontalCandidate ? "có" : "không");
                item.SubItems.Add(FormatRect(tab.Bounds));
                item.SubItems.Add(tab.IsOffscreen ? "có" : "không");
                if (tab.Selected && tab.IsHorizontalCandidate) item.BackColor = Color.FromArgb(220, 245, 220);
                _list.Items.Add(item);
            }
            _list.EndUpdate();

            StringBuilder summary = new StringBuilder();
            summary.Append("Quét ").Append(result.NodesVisited).Append(" phần tử UIA trong ").Append(result.ElapsedMs).Append(" ms; DPI cửa sổ ").Append(result.Dpi).Append(".");
            if (snapshot.OverlayVisible)
            {
                summary.Append(" Nút × nằm sát mép phải tab được xác nhận.");
            }
            else
            {
                summary.Append(" Nút × được ẩn để tránh đóng nhầm.");
            }
            if (!string.IsNullOrEmpty(result.Error)) summary.Append(" Lỗi: ").Append(result.Error);
            _summary.Text = summary.ToString();
        }

        private static string BuildSignature(ActiveTabSnapshot snapshot, ScanResult result)
        {
            StringBuilder signature = new StringBuilder(128);
            signature.Append(snapshot.OverlayVisible).Append('|').Append(snapshot.HiddenReason).Append('|');
            if (result == null) return signature.ToString();
            signature.Append(result.Outcome).Append('|').Append(result.Message).Append('|').Append(result.Error).Append('|').Append(result.NodesVisited).Append('|').Append(result.ElapsedMs).Append('|').Append(result.Dpi);
            foreach (TabInfo tab in result.Tabs)
            {
                signature.Append('|').Append(tab.Name).Append(':').Append(tab.Selected).Append(':').Append(tab.Bounds).Append(':').Append(tab.SelectionKnown).Append(':').Append(tab.IsOffscreen).Append(':').Append(tab.IsHorizontalCandidate);
            }
            return signature.ToString();
        }

        private static string FormatRect(Rectangle r)
        {
            if (r == Rectangle.Empty) return "(không có)";
            return "L" + r.Left + " T" + r.Top + " R" + r.Right + " B" + r.Bottom;
        }
    }
}
