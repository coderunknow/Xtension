using System;
using System.IO;

namespace Xtension
{
    /// <summary>Per-user storage locations. Nothing is written outside %LOCALAPPDATA%\Xtension.</summary>
    internal static class AppPaths
    {
        public static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.ProductName);
        public static readonly string LogFile = Path.Combine(DirectoryPath, "app.log");
        public static readonly string SettingsFile = Path.Combine(DirectoryPath, "settings.ini");

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(DirectoryPath);
        }
    }
}
