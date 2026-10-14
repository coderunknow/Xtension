using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Xtension
{
    /// <summary>Window-query and window-move P/Invoke. Every declaration matches the Win32 signature in the SDK.</summary>
    internal static class NativeMethods
    {
        internal const uint GA_ROOT = 2;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr DpiAwarePerMonitorV2 = new IntPtr(-4);

        #pragma warning disable 0649 // RECT is filled by GetWindowRect.
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        #pragma warning restore 0649

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        // Windows 10 1607+. Missing on older systems, so calls are guarded with EntryPointNotFoundException.
        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref uint size);

        /// <summary>
        /// Opts the process into per-monitor DPI awareness so UIA and window coordinates are physical pixels.
        /// The application manifest already requests this; this call is the fallback for older Windows.
        /// </summary>
        public static void EnablePerMonitorDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(DpiAwarePerMonitorV2);
            }
            catch (EntryPointNotFoundException)
            {
                try { SetProcessDPIAware(); }
                catch (EntryPointNotFoundException) { }
            }
        }

        public static IntPtr GetForegroundRootWindow()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return IntPtr.Zero;
            IntPtr root = GetAncestor(foreground, GA_ROOT);
            return root == IntPtr.Zero ? foreground : root;
        }

        public static Rectangle GetWindowBounds(IntPtr hwnd)
        {
            RECT rect;
            if (!GetWindowRect(hwnd, out rect)) return Rectangle.Empty;
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static string GetWindowTitle(IntPtr hwnd)
        {
            StringBuilder text = new StringBuilder(512);
            try
            {
                GetWindowText(hwnd, text, text.Capacity);
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("window-title", "Window title could not be read: " + ex.GetType().Name);
            }
            return text.ToString();
        }

        /// <summary>DPI of the window's monitor, or 96 when the window or API is unavailable.</summary>
        public static int GetWindowDpi(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi >= 48 && dpi <= 960) return (int)dpi;
            }
            catch (EntryPointNotFoundException) { }
            return TabGeometry.BaseDpi;
        }
    }
}
