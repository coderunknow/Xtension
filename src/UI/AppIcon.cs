using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace Xtension
{
    /// <summary>
    /// The application and notification-area icon, decoded from the multi-size resource that is compiled into the
    /// executable. Loading is dependency-free (System.Drawing already declares the icon type) and Windows picks the
    /// frame that fits the requested size.
    ///
    /// The cache holds the resource <em>bytes</em>, never a live icon handle, and every call decodes a new icon the
    /// caller owns. That is deliberate: an earlier version returned one cached icon to every caller, so a single
    /// owner that disposed what it was given destroyed the handle every window and the tray were still using. There
    /// are only a handful of callers, so decoding per call costs nothing measurable and removes the shared-ownership
    /// failure mode entirely.
    ///
    /// The resource is produced by <c>tools/generate-icon.py</c>; when it is missing, the tray falls back to the
    /// painted icon, so a build without assets still shows something.
    /// </summary>
    internal static class AppIcon
    {
        public const string ResourceName = "Xtension.AppIcon.ico";

        private static readonly object Gate = new object();
        private static byte[] _bytes;
        private static bool _missing;

        /// <summary>Icon for a notification-area size, or null when no resource is embedded.</summary>
        public static Icon Small()
        {
            return Decode(System.Windows.Forms.SystemInformation.SmallIconSize);
        }

        /// <summary>Icon for a window or dialog caption, or null when no resource is embedded.</summary>
        public static Icon Large()
        {
            return Decode(new Size(32, 32));
        }

        /// <summary>True when the embedded resource is available. Does not create an icon handle.</summary>
        public static bool IsAvailable()
        {
            byte[] bytes;
            return TryRead(out bytes) && bytes.Length > 0;
        }

        /// <summary>
        /// Gives a window its own copy of the icon. Never throws: a window without an icon is still usable.
        /// </summary>
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

        /// <summary>
        /// The embedded resource bytes, read once and cached. Returns false when the resource is absent or
        /// unreadable, which is what makes the tray fall back to the painted icon.
        /// </summary>
        internal static bool TryRead(out byte[] bytes)
        {
            lock (Gate)
            {
                if (_bytes != null)
                {
                    bytes = _bytes;
                    return true;
                }
                bytes = null;
                if (_missing) return false;
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                    {
                        if (stream == null)
                        {
                            _missing = true;
                            AppLog.WarnThrottled("app-icon", "No embedded icon resource; the painted tray icon is used.");
                            return false;
                        }
                        MemoryStream buffer = new MemoryStream();
                        byte[] chunk = new byte[8192];
                        int read;
                        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0) buffer.Write(chunk, 0, read);
                        _bytes = buffer.ToArray();
                        bytes = _bytes;
                        return bytes.Length > 0;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.WarnThrottled("app-icon", "The embedded icon could not be read: " + ex.GetType().Name);
                    _missing = true;
                    return false;
                }
            }
        }

        /// <summary>
        /// One freshly decoded icon for the requested size, or null when there is nothing to decode. The caller owns
        /// what it receives and may dispose it without affecting any other window, because nothing is shared here
        /// except the immutable resource bytes.
        /// </summary>
        private static Icon Decode(Size size)
        {
            byte[] bytes;
            if (!TryRead(out bytes)) return null;
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes, false))
                {
                    return new Icon(stream, size);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("app-icon", "The embedded icon could not be decoded: " + ex.GetType().Name);
                return null;
            }
        }
    }
}
