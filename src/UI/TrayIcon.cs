using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Xtension
{
    /// <summary>Builds the notification-area icon at runtime, so no binary icon file is needed in the repository.</summary>
    internal static class TrayIcon
    {
        public static Icon Create()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (SolidBrush background = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    FillRoundedRectangle(graphics, background, new Rectangle(1, 1, 30, 30), 7);
                }
                using (Pen pen = new Pen(Color.White, 3.0f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, 10, 10, 22, 22);
                    graphics.DrawLine(pen, 22, 10, 10, 22);
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    // Clone so the returned Icon owns its handle; the native handle is destroyed in finally.
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
        }

        private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.FillPath(brush, path);
            }
        }
    }
}
