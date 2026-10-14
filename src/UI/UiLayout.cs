using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    internal static class UiLayout
    {
        public static TableLayoutPanel Stack()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.ColumnCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.AutoSize = true;
            panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panel.Dock = DockStyle.Top;
            panel.Margin = Padding.Empty;
            return panel;
        }

        public static void Add(TableLayoutPanel panel, Control control)
        {
            int row = panel.RowCount++;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(control, 0, row);
        }

        // Keep a usable viewport even on a laptop at high scaling. Content scrolls rather than escaping the desktop.
        public static void FitToWorkingArea(Form form)
        {
            Rectangle area = Screen.FromControl(form).WorkingArea;
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
            form.Size = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
        }
    }

    /// <summary>Wrap and measure text at the available width, including Vietnamese accents and padding.</summary>
    internal sealed class WrappingLabel : Label
    {
        public WrappingLabel()
        {
            AutoSize = true;
            Dock = DockStyle.Fill;
            UseMnemonic = false;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width > 1 && proposedSize.Width < int.MaxValue
                ? proposedSize.Width : Math.Max(1, Width);
            int textWidth = Math.Max(1, width - Padding.Horizontal);
            Size measured = TextRenderer.MeasureText(Text, Font, new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
            return new Size(width, measured.Height + Padding.Vertical);
        }
    }
}
