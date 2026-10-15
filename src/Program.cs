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
                if (instance.Unavailable)
                {
                    MessageBox.Show(Strings.Get(StringId.SingleInstanceUnavailable),
                        AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                if (instance.IsSecondary)
                {
                    if (!instance.SignalExisting())
                    {
                        MessageBox.Show(Strings.Get(StringId.SingleInstanceNotResponding),
                            AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    AppLog.Error("Unhandled UI exception: " + ScanSafety.DescribeError("UI", e.Exception));
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    AppLog.Error("Unhandled exception: " + (ex == null ? "unknown" : ScanSafety.DescribeError("Program", ex)));
                };

                try
                {
                    NativeMethods.EnablePerMonitorDpiAwareness();
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    AppPaths.EnsureDirectory();
                    AppLog.Info("Process starting. OS " + Environment.OSVersion.VersionString + ", " + (IntPtr.Size == 8 ? "x64" : "x86") + ".");

                    // The language is applied before any window or menu is created, so the first paint is correct.
                    SettingsStore store = SettingsStore.Default;
                    AppSettings settings = AppSettings.Load(store);
                    Strings.Language = settings.Language;

                    Application.Run(new TrayAppContext(store, settings));
                    return 0;
                }
                catch (Exception ex)
                {
                    AppLog.Error("Fatal startup error: " + ScanSafety.DescribeError("Program", ex));
                    MessageBox.Show(Strings.Get(StringId.FatalStartupError, AppPaths.LogFile),
                        AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
        }
    }
}
