using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// The custom × button. A non-activating, tool-window form: it never takes focus from Brave, so the
    /// foreground window stays the one that was validated. Geometry is set in physical pixels with SetWindowPos.
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
            _toolTip.SetToolTip(this, "Đóng tab Brave đang được chọn (nhấp một lần)");
            AccessibleName = "Đóng tab Brave đang được chọn";
            AccessibleDescription = "Chỉ đóng tab đã được xác nhận là tab đang hoạt động.";
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

        /// <summary>Shows the button at a screen rectangle without activating it. Idempotent for an unchanged rectangle.</summary>
        public void ShowAt(Rectangle bounds)
        {
            if (!Visible) Show();
            if (Bounds == bounds) return;
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HwndTopmost,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                _armed = false;
                _pressed = false;
                _hover = false;
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
