using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Xtension
{
    /// <summary>
    /// Builds the notification-area icon. The shipped icon is the multi-size resource compiled into the executable
    /// (<c>assets/xtension.ico</c>, produced by <c>tools/generate-icon.py</c>), requested at the size Windows uses for
    /// the notification area so the small frames stay crisp. The painted fallback below exists only so a build whose
    /// resources are missing still shows an icon instead of a blank square.
    /// </summary>
    internal static class TrayIcon
    {
        /// <summary>A caller-owned icon. The shared cached instance is cloned, so disposing this one is harmless.</summary>
        public static Icon Create()
        {
            try
            {
                Icon embedded = AppIcon.Small();
                if (embedded != null) return (Icon)embedded.Clone();
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("tray-icon", "The embedded icon could not be used: " + ex.GetType().Name);
            }
            return CreatePainted(32);
        }

        /// <summary>
        /// True when the notification-area icon comes from the embedded multi-size resource. Logged at startup so a
        /// build that lost its resource (or a fallback in use) is visible in the log and in the acceptance run.
        /// </summary>
        public static bool UsesEmbedded()
        {
            return AppIcon.Small() != null;
        }

        /// <summary>Fallback drawing, used only when no embedded icon is available.</summary>
        public static Icon CreatePainted(int size)
        {
            if (size < 16) size = 16;
            using (Bitmap bitmap = new Bitmap(size, size))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                float scale = size / 32f;
                using (SolidBrush background = new SolidBrush(Color.FromArgb(37, 99, 235)))
                {
                    FillRoundedRectangle(graphics, background,
                        new Rectangle((int)Math.Round(1 * scale), (int)Math.Round(1 * scale),
                            (int)Math.Round(30 * scale), (int)Math.Round(30 * scale)),
                        Math.Max(1, (int)Math.Round(7 * scale)));
                }
                using (Pen pen = new Pen(Color.White, Math.Max(1.4f, 3.0f * scale)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, 10 * scale, 10 * scale, 22 * scale, 22 * scale);
                    graphics.DrawLine(pen, 22 * scale, 10 * scale, 10 * scale, 22 * scale);
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
