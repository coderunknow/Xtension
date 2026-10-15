using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// The custom × button. A non-activating, tool-window form: it never takes focus from Brave, so the
    /// foreground window stays the one that was validated. Geometry is set in physical pixels with SetWindowPos.
    ///
    /// The window is the hit-test rectangle, so the configurable size is applied by resizing this window, not by
    /// only changing what is painted. <see cref="ShowAt"/> is idempotent: repeating the same verified placement
    /// costs one cheap visibility query and no move, no geometry read and no repaint.
    /// </summary>
    internal sealed class CloseButtonOverlay : Form
    {
        // Logical coordinate space for painting. The real size is scaled per monitor by GetOverlayBounds.
        private const int DesignSize = 26;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int WmMouseActivate = 0x0021;
        private const int MaNoActivate = 3;

        private readonly ToolTip _toolTip;
        private bool _hover;
        private bool _pressed;
        private bool _armed;
        private Rectangle _placedBounds = Rectangle.Empty;
        private int _paintedSize = -1;

        /// <summary>Raised on a completed left click inside the button. The argument is the click position in screen coordinates.</summary>
        public event Action<Point> Clicked;

        public CloseButtonOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Size = new Size(DesignSize, DesignSize);

            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 450;
            _toolTip.ReshowDelay = 150;
            _toolTip.AutoPopDelay = 5000;
            ApplyLanguage();
        }

        /// <summary>Refreshes the localized tooltip and accessible text. Safe to call on every language change.</summary>
        public void ApplyLanguage()
        {
            _toolTip.SetToolTip(this, Strings.Get(StringId.OverlayTooltip));
            AccessibleName = Strings.Get(StringId.OverlayAccessibleName);
            AccessibleDescription = Strings.Get(StringId.OverlayAccessibleDescription);
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
            base.WndProc(ref m);
        }

        /// <summary>
        /// Shows the button at a screen rectangle without activating it. Repeating a placement that was already
        /// applied and verified is almost free, which keeps the periodic scan path from moving or repainting a
        /// window that has not changed. Any failure hides the button and reports the reason.
        /// </summary>
        public bool ShowAt(Rectangle bounds, out string failure)
        {
            failure = null;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Hide();
                _placedBounds = Rectangle.Empty;
                failure = "Overlay bounds are empty.";
                return false;
            }

            if (Visible && bounds == _placedBounds && _paintedSize == bounds.Width && NativeMethods.IsWindowVisible(Handle))
            {
                // Already placed, already verified, already painted. Nothing to do and nothing to redraw.
                return true;
            }

            // Position before first Show: avoid flashing a clickable button at an old/default location.
            if (!Visible) { Bounds = bounds; Show(); }
            bool moved = NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost,
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (!moved || !NativeMethods.IsWindowVisible(Handle) || NativeMethods.GetWindowBounds(Handle) != bounds)
            {
                Hide();
                _placedBounds = Rectangle.Empty;
                _paintedSize = -1;
                failure = moved ? "Overlay visibility/bounds verification failed." : "SetWindowPos failed; Win32 error " + error + ".";
                return false;
            }
            _placedBounds = bounds;
            if (_paintedSize != bounds.Width)
            {
                // The painter scales from the window size, so a size change must repaint; a pure move must not.
                _paintedSize = bounds.Width;
                Invalidate();
            }
            return true;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                Capture = false;
                _armed = false;
                _pressed = false;
                _hover = false;
                _placedBounds = Rectangle.Empty;
                _paintedSize = -1;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            _armed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _armed = e.Button == MouseButtons.Left;
            _pressed = _armed;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool completed = _armed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
            _armed = false;
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);

            if (completed)
            {
                Action<Point> handler = Clicked;
                if (handler != null) handler(PointToScreen(e.Location));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Width / (float)DesignSize;
            graphics.ScaleTransform(scale, scale);

            Color fill = _pressed
                ? Color.FromArgb(153, 27, 27)
                : (_hover ? Color.FromArgb(220, 70, 75) : Color.FromArgb(38, 45, 57));
            using (SolidBrush brush = new SolidBrush(fill))
            {
                graphics.FillEllipse(brush, 1f, 1f, DesignSize - 3f, DesignSize - 3f);
            }
            using (Pen border = new Pen(_hover ? Color.FromArgb(255, 170, 170) : Color.FromArgb(210, 216, 225), 1f))
            {
                graphics.DrawEllipse(border, 1f, 1f, DesignSize - 3f, DesignSize - 3f);
            }
            using (Pen cross = new Pen(Color.White, _hover ? 2.25f : 2f))
            {
                cross.StartCap = LineCap.Round;
                cross.EndCap = LineCap.Round;
                int inset = DesignSize / 3;
                graphics.DrawLine(cross, inset, inset, DesignSize - inset, DesignSize - inset);
                graphics.DrawLine(cross, DesignSize - inset, inset, inset, DesignSize - inset);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}
