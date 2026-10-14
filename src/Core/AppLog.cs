using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>
    /// Append-only diagnostic log with size-based rotation.
    /// Privacy rule: never write tab or window titles here. Only outcomes, counts and timings.
    /// Logging must never throw into the caller.
    /// </summary>
    internal static class AppLog
    {
        private static readonly object WriteGate = new object();
        private static readonly object ThrottleGate = new object();
        private static readonly Dictionary<string, long> LastThrottled = new Dictionary<string, long>(StringComparer.Ordinal);

        public static void Info(string message)
        {
            Write(AppPaths.LogFile, "INFO", message, AppInfo.LogRotationBytes);
        }

        public static void Warn(string message)
        {
            Write(AppPaths.LogFile, "WARN", message, AppInfo.LogRotationBytes);
        }

        public static void Error(string message)
        {
            Write(AppPaths.LogFile, "ERROR", message, AppInfo.LogRotationBytes);
        }

        /// <summary>Writes a warning at most once per LogThrottleMs for the same key.</summary>
        public static void WarnThrottled(string key, string message)
        {
            lock (ThrottleGate)
            {
                long now = Clock.NowMs;
                long last;
                if (LastThrottled.TryGetValue(key, out last) && now - last < AppInfo.LogThrottleMs) return;
                LastThrottled[key] = now;
            }
            Warn(message);
        }

        internal static void Write(string path, string level, string message, long rotateBytes)
        {
            try
            {
                lock (WriteGate)
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    FileInfo info = new FileInfo(path);
                    if (info.Exists && info.Length > rotateBytes) RotateFile(path);
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + "  " + message + Environment.NewLine;
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Xtension log write failed: " + ex.Message);
            }
        }

        internal static void RotateFile(string path)
        {
            string previous = path + ".old";
            if (File.Exists(previous)) File.Delete(previous);
            File.Move(path, previous);
        }
    }
}
