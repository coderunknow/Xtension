using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// The custom × button. A non-activating, tool-window form: it never takes focus from the browser, so the
    /// foreground window stays the one that was validated. Geometry is set in physical pixels with SetWindowPos.
    ///
    /// The window is the hit-test rectangle, so the configurable size is applied by resizing this window, not by
    /// only changing what is painted. <see cref="ShowAt"/> is idempotent: repeating the same verified placement
    /// costs one cheap visibility query and no move, no geometry read and no repaint.
    ///
    /// Appearance (colour, opacity, border) changes only what is painted and the window's uniform alpha. It never
    /// moves the window, never changes the hit rectangle and never makes the button visible when authorisation is
    /// missing, because visibility is still decided by the controller from a fresh scan.
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
        private OverlayColor _color = OverlayColor.Dark;
        private int _opacityPercent = AppInfo.DefaultButtonOpacityPercent;
        private bool _border = true;

        /// <summary>Raised on a completed left click inside the button. The argument is the click position in screen coordinates.</summary>
        public event Action<Point> Clicked;

        public CloseButtonOverlay()
        {
            // Set before the handle exists: uniform alpha and the transparency key both live on the layered window.
            AllowTransparency = true;
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

        /// <summary>
        /// Applies the configured appearance. Returns true when something changed, so the caller can decide whether a
        /// repaint notification is worth sending. The window is deliberately not moved or resized here: geometry stays
        /// the business of <see cref="ShowAt"/> and of the shared TabGeometry rules.
        /// </summary>
        public bool ApplyAppearance(AppSettings settings)
        {
            if (settings == null) return false;
            AppSettings copy = settings.Copy();
            copy.Normalize();
            bool changed = copy.ButtonColor != _color
                || copy.ButtonOpacityPercent != _opacityPercent
                || copy.ButtonBorder != _border;
            if (!changed) return false;
            _color = copy.ButtonColor;
            _opacityPercent = copy.ButtonOpacityPercent;
            _border = copy.ButtonBorder;
            ApplyOpacity();
            if (IsHandleCreated) Invalidate();
            return true;
        }

        private void ApplyOpacity()
        {
            double value = _opacityPercent >= AppInfo.MaxButtonOpacityPercent
                ? 1.0
                : _opacityPercent / (double)AppInfo.MaxButtonOpacityPercent;
            if (Math.Abs(Opacity - value) > 0.001) Opacity = value;
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

            CancelPendingClick();
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

        /// <summary>A press must never survive a change of displayed identity or placement.</summary>
        public void CancelPendingClick()
        {
            _armed = false;
            _pressed = false;
            Capture = false;
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

            Color fill = _pressed ? PressedFill(_color) : (_hover ? HoverFill(_color) : Fill(_color));
            Color edge = _hover ? HoverBorder(_color) : Border(_color);
            using (SolidBrush brush = new SolidBrush(fill))
            {
                graphics.FillEllipse(brush, 1f, 1f, DesignSize - 3f, DesignSize - 3f);
            }
            if (_border)
            {
                using (Pen border = new Pen(edge, 1f))
                {
                    graphics.DrawEllipse(border, 1f, 1f, DesignSize - 3f, DesignSize - 3f);
                }
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

        private static Color Fill(OverlayColor color)
        {
            if (color == OverlayColor.Red) return Color.FromArgb(185, 28, 28);
            if (color == OverlayColor.Blue) return Color.FromArgb(37, 99, 235);
            if (color == OverlayColor.Green) return Color.FromArgb(5, 150, 105);
            return Color.FromArgb(38, 45, 57);
        }

        private static Color HoverFill(OverlayColor color)
        {
            if (color == OverlayColor.Red) return Color.FromArgb(220, 70, 75);
            if (color == OverlayColor.Blue) return Color.FromArgb(59, 130, 246);
            if (color == OverlayColor.Green) return Color.FromArgb(16, 185, 129);
            return Color.FromArgb(58, 68, 84);
        }

        private static Color PressedFill(OverlayColor color)
        {
            if (color == OverlayColor.Red) return Color.FromArgb(153, 27, 27);
            if (color == OverlayColor.Blue) return Color.FromArgb(29, 78, 216);
            if (color == OverlayColor.Green) return Color.FromArgb(4, 120, 87);
            return Color.FromArgb(24, 29, 38);
        }

        private static Color Border(OverlayColor color)
        {
            if (color == OverlayColor.Dark) return Color.FromArgb(210, 216, 225);
            return Color.FromArgb(232, 240, 255);
        }

        private static Color HoverBorder(OverlayColor color)
        {
            if (color == OverlayColor.Dark) return Color.FromArgb(255, 170, 170);
            return Color.White;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}
