using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Xtension
{
    /// <summary>Per-user "start with Windows" entry in HKCU\...\Run. Disabled by default; never requires administrator rights.</summary>
    internal static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = AppInfo.ProductName;

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("startup-read", "Startup entry could not be read: " + ex.GetType().Name);
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null) throw new InvalidOperationException("The current-user Run registry key could not be opened.");
                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
