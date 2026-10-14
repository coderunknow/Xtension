using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>Shows the latest scan in detail. Tab titles appear here only; they are never written to the log.</summary>
    internal sealed class DiagnosticsForm : Form
    {
        private readonly Label _status;
        private readonly Label _summary;
        private readonly ListView _list;
        private string _renderSignature = string.Empty;

        public DiagnosticsForm()
        {
            Text = AppInfo.ProductName + " — Chẩn đoán " + AppInfo.DisplayVersion;
            MinimumSize = new Size(720, 420);
            Size = new Size(980, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);

            _status = new Label();
            _status.Dock = DockStyle.Top;
            _status.Height = 58;
            _status.Padding = new Padding(12, 8, 8, 8);
            _status.BackColor = Color.White;
            _status.ForeColor = Color.FromArgb(31, 41, 55);
            _status.Text = "Đang chờ kết quả quét…";

            _summary = new Label();
            _summary.Dock = DockStyle.Bottom;
            _summary.Height = 64;
            _summary.Padding = new Padding(12, 8, 8, 8);
            _summary.BackColor = Color.FromArgb(235, 239, 245);
            _summary.ForeColor = Color.FromArgb(71, 85, 105);

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = true;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = new Font("Segoe UI", 9F);
            _list.Columns.Add("Tab / tên truy cập", 300);
            _list.Columns.Add("Đang chọn", 90);
            _list.Columns.Add("Trong thanh ngang", 130);
            _list.Columns.Add("Vị trí màn hình (px)", 200);
            _list.Columns.Add("Ngoài màn hình", 120);

            Controls.Add(_list);
            Controls.Add(_summary);
            Controls.Add(_status);
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
                _status.Text = snapshot.IdleReason;
                _list.Items.Clear();
                _summary.Text = "Chưa có kết quả quét. Nút × được ẩn cho đến khi Xtension xác nhận được tab đang chọn.";
                return;
            }

            _status.Text = result.Message + "   [" + result.Outcome + "]";
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (TabInfo tab in result.Tabs)
            {
                ListViewItem item = new ListViewItem(string.IsNullOrEmpty(tab.Name) ? "(TabItem không có tên)" : tab.Name);
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
            if (result.Outcome == ScanOutcome.Ready && result.SelectedHorizontalTab != null)
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
            signature.Append(snapshot.OverlayVisible).Append('|').Append(snapshot.IdleReason).Append('|');
            if (result == null) return signature.ToString();
            signature.Append(result.Outcome).Append('|').Append(result.Message).Append('|').Append(result.WindowTitle);
            foreach (TabInfo tab in result.Tabs)
            {
                signature.Append('|').Append(tab.Name).Append(':').Append(tab.Selected).Append(':').Append(tab.Bounds);
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
