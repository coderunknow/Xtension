using System;
using System.Threading;
using System.Windows.Forms;

namespace Xtension
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            using (SingleInstance instance = SingleInstance.Acquire())
            {
                if (instance.IsSecondary)
                {
                    if (!instance.SignalExisting())
                    {
                        MessageBox.Show(
                            "Xtension đã đang chạy nhưng không phản hồi. Hãy mở Trình quản lý tác vụ để đóng tiến trình Xtension cũ rồi thử lại.",
                            AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    AppLog.Error("Unhandled UI exception: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    AppLog.Error("Unhandled exception: " + (ex == null ? "unknown" : ex.GetType().Name + ": " + ex.Message));
                };

                try
                {
                    NativeMethods.EnablePerMonitorDpiAwareness();
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    AppPaths.EnsureDirectory();
                    AppLog.Info("Process starting. OS " + Environment.OSVersion.VersionString + ", " + (IntPtr.Size == 8 ? "x64" : "x86") + ".");

                    Application.Run(new TrayAppContext(SettingsStore.Default));
                    return 0;
                }
                catch (Exception ex)
                {
                    AppLog.Error("Fatal startup error: " + ex.GetType().Name + ": " + ex.Message);
                    MessageBox.Show(
                        "Xtension không khởi động được. Chi tiết đã được ghi vào nhật ký:\r\n" + AppPaths.LogFile,
                        AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
        }
    }
}
