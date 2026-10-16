using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

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
        /// <summary>
        /// A new icon the caller owns. <see cref="AppIcon.Small"/> decodes its own instance on every call, so this
        /// icon shares nothing with the tray, with a window or with any other caller.
        /// </summary>
        public static Icon Create()
        {
            try
            {
                Icon embedded = AppIcon.Small();
                if (embedded != null) return embedded;
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
            // Availability is a property of the resource, so this must not build an icon handle just to answer it.
            return AppIcon.IsAvailable();
        }

        /// <summary>
        /// Fallback drawing, used only when no embedded icon is available. The bitmap is encoded as a small 32-bit
        /// ICO in memory and decoded into an icon the caller owns: a handle taken from a bitmap with
        /// <c>Bitmap.GetHicon</c> belongs to nobody, so an icon built from one could neither outlive its creator nor
        /// be disposed safely by the code that receives it.
        /// </summary>
        public static Icon CreatePainted(int size)
        {
            if (size < 16) size = 16;
            using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
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

                return IconFromBitmap(bitmap);
            }
        }

        /// <summary>Decodes a bitmap into a caller-owned icon through an in-memory single-frame ICO.</summary>
        internal static Icon IconFromBitmap(Bitmap bitmap)
        {
            byte[] bytes = EncodeIcon(bitmap);
            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                return new Icon(stream);
            }
        }

        /// <summary>
        /// A 32-bit single-frame ICO: the format Windows and System.Drawing.Icon both read, and the same layout the
        /// shipped asset uses (BITMAPINFOHEADER with the height doubled, bottom-up BGRA rows, zeroed AND mask, which
        /// is what makes the alpha channel the only transparency source).
        /// </summary>
        internal static byte[] EncodeIcon(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int maskStride = ((width + 31) / 32) * 4;
            int imageBytes = 40 + width * height * 4 + maskStride * height;
            byte[] bytes = new byte[22 + imageBytes];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)1);
                writer.Write((byte)(width >= 256 ? 0 : width));
                writer.Write((byte)(height >= 256 ? 0 : height));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)imageBytes);
                writer.Write((uint)22);

                writer.Write((uint)40);
                writer.Write(width);
                writer.Write(height * 2);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)0);
                writer.Write((uint)0);
                writer.Write(0);
                writer.Write(0);
                writer.Write((uint)0);
                writer.Write((uint)0);

                // Bottom-up rows of straight (non-premultiplied) BGRA, which is the order the format stores them in.
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        writer.Write(pixel.B);
                        writer.Write(pixel.G);
                        writer.Write(pixel.R);
                        writer.Write(pixel.A);
                    }
                }
                // The AND mask stays zero: with a 32-bit frame the alpha channel decides transparency.
                for (int index = 0; index < maskStride * height; index++) writer.Write((byte)0);
            }
            return bytes;
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
