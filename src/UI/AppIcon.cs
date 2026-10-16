using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace Xtension
{
    /// <summary>
    /// The application and notification-area icon, loaded from the multi-size resource that is compiled into the
    /// executable. Loading is dependency-free (System.Drawing already declares the icon type), the frames are picked
    /// by Windows for the requested size, and the loaded instance is cached because every window shares it.
    ///
    /// The resource is produced from <c>assets/xtension-icon.svg</c> and <c>tools/generate-icon.py</c>; when the
    /// resource is missing the tray falls back to the previously painted icon, so a build without assets still runs.
    /// </summary>
    internal static class AppIcon
    {
        public const string ResourceName = "Xtension.AppIcon.ico";

        private static readonly object Gate = new object();
        private static Icon _small;
        private static Icon _large;
        private static bool _missing;

        /// <summary>Icon for a notification-area size, or null when no resource is embedded.</summary>
        public static Icon Small()
        {
            lock (Gate)
            {
                if (_small == null && !_missing) _small = Load(System.Windows.Forms.SystemInformation.SmallIconSize);
                return _small;
            }
        }

        /// <summary>Icon for a window or dialog caption, or null when no resource is embedded.</summary>
        public static Icon Large()
        {
            lock (Gate)
            {
                if (_large == null && !_missing) _large = Load(new Size(32, 32));
                return _large;
            }
        }

        /// <summary>Applies the shared icon to a window. Never throws: a window without an icon is still usable.</summary>
        public static void Attach(System.Windows.Forms.Form form)
        {
            if (form == null) return;
            try
            {
                Icon icon = Large();
                if (icon != null)
                {
                    form.Icon = icon;
                    form.ShowIcon = true;
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("app-icon", "The window icon could not be applied: " + ex.GetType().Name);
            }
        }

        /// <summary>Number of frames in the embedded icon, for diagnostics and for the packaging check.</summary>
        public static int FrameCount()
        {
            byte[] bytes;
            if (!TryRead(out bytes)) return 0;
            return ReadFrameCount(bytes);
        }

        internal static int ReadFrameCount(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 22) return 0;
            if (BitConverter.ToUInt16(bytes, 0) != 0 || BitConverter.ToUInt16(bytes, 2) != 1) return 0;
            return BitConverter.ToUInt16(bytes, 4);
        }

        internal static bool TryRead(out byte[] bytes)
        {
            bytes = null;
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null) return false;
                    MemoryStream buffer = new MemoryStream();
                    byte[] chunk = new byte[8192];
                    int read;
                    while ((read = stream.Read(chunk, 0, chunk.Length)) > 0) buffer.Write(chunk, 0, read);
                    bytes = buffer.ToArray();
                    return bytes.Length > 0;
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("app-icon", "The embedded icon could not be read: " + ex.GetType().Name);
                return false;
            }
        }

        private static Icon Load(Size size)
        {
            byte[] bytes;
            if (!TryRead(out bytes))
            {
                _missing = true;
                AppLog.WarnThrottled("app-icon", "No embedded icon resource; the painted tray icon is used.");
                return null;
            }
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    using (Icon decoded = new Icon(stream, size))
                    {
                        return (Icon)decoded.Clone();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("app-icon", "The embedded icon could not be decoded: " + ex.GetType().Name);
                _missing = true;
                return null;
            }
        }
    }
}
