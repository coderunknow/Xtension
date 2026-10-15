using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// A control's localized text plus its arguments. Stored in <see cref="Control.Tag"/> so any control can be
    /// re-localized in place when the user switches language, without rebuilding the window.
    /// </summary>
    internal sealed class LocalizedText
    {
        private readonly StringId _id;
        private readonly object[] _args;

        public LocalizedText(StringId id, object[] args)
        {
            _id = id;
            _args = args;
        }

        /// <summary>The string this control shows. Exposed so a test can find a control by meaning, not by order.</summary>
        public StringId Id
        {
            get { return _id; }
        }

        public string Format()
        {
            if (_args == null || _args.Length == 0) return Strings.Get(_id);
            if (_args.Length == 1) return Strings.Get(_id, _args[0]);
            if (_args.Length == 2) return Strings.Get(_id, _args[0], _args[1]);
            return Strings.Get(_id, _args[0], _args[1], _args[2]);
        }
    }

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

        /// <summary>Applies the text for an id and remembers it, so a later language change can refresh it.</summary>
        public static T Localize<T>(T control, StringId id, params object[] args) where T : Control
        {
            control.Tag = new LocalizedText(id, args);
            ApplyLocalizedText(control);
            return control;
        }

        /// <summary>Re-applies the localized text of one control, if it was created with <see cref="Localize"/>.</summary>
        public static void ApplyLocalizedText(Control control)
        {
            if (control == null) return;
            LocalizedText text = control.Tag as LocalizedText;
            if (text == null) return;
            control.Text = text.Format();
        }

        /// <summary>Re-applies the localized text of the control and of every child. Used after a language change.</summary>
        public static void ApplyLanguage(Control root)
        {
            if (root == null) return;
            ApplyLocalizedText(root);
            foreach (Control child in root.Controls) ApplyLanguage(child);
        }

        // Keep a usable viewport even on a laptop at high scaling. Content scrolls rather than escaping the desktop.
        public static void FitToWorkingArea(Form form)
        {
            Rectangle area = Screen.FromControl(form).WorkingArea;
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
            form.Size = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
        }

        /// <summary>Height a single-line control needs for the current font, so numeric rows stay readable.</summary>
        public static int TextRowHeight(Control control)
        {
            return Math.Max(24, control.Font.Height + 12);
        }
    }

    internal sealed class WrappingCheckBox : CheckBox
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            Size natural = base.GetPreferredSize(proposedSize);
            if (Parent == null) return natural;
            int width = Math.Max(1, Parent.ClientSize.Width - Parent.Padding.Horizontal - Margin.Horizontal);
            if (natural.Width <= width) return natural;
            int glyph = Math.Max(24, Font.Height + 8);
            Size text = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, width - glyph), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            return new Size(width, Math.Max(natural.Height, text.Height + 8));
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
