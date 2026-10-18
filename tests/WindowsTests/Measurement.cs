using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Xtension.Tests
{
    /// <summary>
    /// Measures a shipped Xtension EXE. This is not a functional test: it launches the given binary, optionally in
    /// front of an isolated Brave window, and reports startup time, idle CPU, memory, handles and the scan latency
    /// the binary itself logs. The performance CI job runs the published previous release and the candidate on the
    /// same runner so the numbers are comparable; nothing here is ever used as an acceptance criterion on its own.
    ///
    /// Usage: --measure <appExe> <seconds> <label> [braveExe] [profile]
    /// Output: one machine-readable "MEASURE ..." line, plus human-readable context lines.
    /// </summary>
    internal static class Measurement
    {
        private delegate bool WindowCallback(IntPtr hwnd, IntPtr state);

        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);

        private static readonly List<IntPtr> Collected = new List<IntPtr>();
        private const string ReadyMarker = "started. Overlay";

        public static int Run(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("FAIL --measure needs: <appExe> <seconds> <label> [braveExe] [profile]");
                return 2;
            }

            string appPath = args[1];
            int seconds;
            if (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) || seconds < 5)
            {
                Console.WriteLine("FAIL --measure needs a sample length of at least 5 seconds.");
                return 2;
            }
            string label = args[3];
            string bravePath = args.Length > 4 ? args[4] : null;
            string profile = args.Length > 5 ? args[5] : null;
            if (!File.Exists(appPath))
            {
                Console.WriteLine("FAIL measured binary not found: " + appPath);
                return 2;
            }

            Process browser = null;
            Process app = null;
            IntPtr braveWindow = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrEmpty(bravePath))
                {
                    if (!File.Exists(bravePath))
                    {
                        Console.WriteLine("FAIL Brave fixture not found: " + bravePath);
                        return 2;
                    }
                    browser = Process.Start(new ProcessStartInfo(bravePath,
                        "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-background-networking " +
                        "--disable-component-update --disable-sync --new-window about:blank about:blank about:blank") { UseShellExecute = false });
                    braveWindow = WaitForWindow(browser.Id, 30000);
                    IntPtr browserWindow = braveWindow;
                    if (browserWindow == IntPtr.Zero)
                    {
                        Console.WriteLine("FAIL the Brave fixture window never appeared.");
                        return 1;
                    }
                    ShowWindow(browserWindow, 3);
                    SetForegroundWindow(browserWindow);
                    Thread.Sleep(1500);
                }

                string logPath = AppPaths.LogFile;
                long offset = 0;
                if (File.Exists(logPath)) offset = new FileInfo(logPath).Length;
                Console.WriteLine("MEASURE-INFO label=" + label + " os=" + Environment.OSVersion.Version + " cores=" + Environment.ProcessorCount
                    + " app=" + Path.GetFileName(appPath) + " brave=" + (browser == null ? "none" : "isolated") + " seconds=" + seconds);

                Stopwatch startup = Stopwatch.StartNew();
                app = Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = false });
                long readyMs = WaitForReadyMarker(app, logPath, offset, 30000);
                startup.Stop();
                if (readyMs < 0)
                {
                    Console.WriteLine("FAIL the measured binary never reported itself ready.");
                    return 1;
                }

                // Warm up: the first scans, the tray icon and the JIT-ed paths should not be counted as idle cost.
                Thread.Sleep(3000);
                app.Refresh();
                TimeSpan cpuBefore = app.TotalProcessorTime;
                Stopwatch window = Stopwatch.StartNew();
                long stateLines = 0;
                List<double> latencies = new List<double>();

                while (window.ElapsedMilliseconds < seconds * 1000)
                {
                    Thread.Sleep(1000);
                    app.Refresh();
                    // State lines are the application's own evidence that it kept reacting during the sample.
                    stateLines += CountStateLines(logPath, offset);
                    // The scan cost itself is measured with the same scanner the application uses, against the same
                    // browser window, so both binaries are measured under identical UI Automation conditions.
                    if (braveWindow != IntPtr.Zero) SampleEnvironmentScan(braveWindow, latencies);
                }
                window.Stop();
                app.Refresh();
                TimeSpan cpuAfter = app.TotalProcessorTime;
                double cpuSeconds = (cpuAfter - cpuBefore).TotalSeconds;
                double cpuPercent = cpuSeconds / window.Elapsed.TotalSeconds * 100.0;

                latencies.Sort();
                double p50 = Percentile(latencies, 0.50);
                double p90 = Percentile(latencies, 0.90);
                Console.WriteLine("MEASURE label=" + label
                    + " startupToReadyMs=" + readyMs
                    + " idleCpuPercent=" + cpuPercent.ToString("F2", CultureInfo.InvariantCulture)
                    + " workingSetMb=" + (app.WorkingSet64 / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)
                    + " privateMb=" + (app.PrivateMemorySize64 / 1048576.0).ToString("F1", CultureInfo.InvariantCulture)
                    + " handles=" + app.HandleCount
                    + " sampleSeconds=" + window.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)
                    + " stateLines=" + stateLines
                    + " uiaScanMsP50=" + p50.ToString("F1", CultureInfo.InvariantCulture)
                    + " uiaScanMsP90=" + p90.ToString("F1", CultureInfo.InvariantCulture));
                // The product's own counters and timings, taken from the lines this run appended to its log. Every
                // field is a counter or a millisecond value; nothing private is involved. Missing values are reported
                // as "n/a" rather than as zero, so an absent measurement can never look like a fast one.
                Console.WriteLine(BuildLatencyLine(label, logPath, offset));
                if (braveWindow != IntPtr.Zero) SampleReaction(app.Id, braveWindow, label);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL measurement error: " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
            finally
            {
                // Both processes are fixtures of this job; leave nothing running for the next measurement.
                Kill(app);
                Kill(browser);
                Thread.Sleep(1000);
            }
        }

        private static void SampleReaction(int processId, IntPtr browserWindow, string label)
        {
            List<double> timings = new List<double>();
            const int requested = 5;
            for (int sample = 0; sample < requested; sample++)
            {
                SetForegroundWindow(browserWindow);
                ScanResult before = UiaTabScanner.Scan(browserWindow, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                if (before.Outcome != ScanOutcome.Ready || NativeMethods.GetForegroundRootWindow() != browserWindow) continue;
                System.Drawing.Rectangle prior = TabGeometry.GetOverlayBounds(before.SelectedHorizontalTab.Bounds, before.Dpi,
                    AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                IntPtr overlay = FindVisibleWindow(processId, true);
                if (overlay == IntPtr.Zero || NativeMethods.GetWindowBounds(overlay) != prior) continue;
                Stopwatch reaction = Stopwatch.StartNew();
                // Read-only fixture navigation, never a close command. Always release modifiers.
                keybd_event(0x11, 0, 0, UIntPtr.Zero);
                try { keybd_event(0x09, 0, 0, UIntPtr.Zero); }
                finally
                {
                    keybd_event(0x09, 0, 2, UIntPtr.Zero);
                    keybd_event(0x11, 0, 2, UIntPtr.Zero);
                }
                System.Drawing.Rectangle observed = System.Drawing.Rectangle.Empty;
                while (reaction.ElapsedMilliseconds < 5000)
                {
                    overlay = FindVisibleWindow(processId, true);
                    if (overlay != IntPtr.Zero)
                    {
                        observed = NativeMethods.GetWindowBounds(overlay);
                        if (observed != prior) break;
                    }
                    Thread.Sleep(10);
                }
                reaction.Stop();
                ScanResult after = UiaTabScanner.Scan(browserWindow, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                if (observed.IsEmpty || observed == prior || after.Outcome != ScanOutcome.Ready
                    || TabInfo.SameIdentity(before.SelectedHorizontalTab.RuntimeId, after.SelectedHorizontalTab.RuntimeId)
                    || NativeMethods.GetForegroundRootWindow() != browserWindow) continue;
                System.Drawing.Rectangle expected = TabGeometry.GetOverlayBounds(after.SelectedHorizontalTab.Bounds, after.Dpi,
                    AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                if (observed == expected) timings.Add(reaction.ElapsedMilliseconds);
                Thread.Sleep(150);
            }
            timings.Sort();
            Console.WriteLine("MEASURE-REACTION label=" + label + " requested=" + requested + " samples=" + timings.Count
                + " failed=" + (requested - timings.Count)
                + " tabSwitchToVisibleMsP50=" + (timings.Count == 0 ? "n/a" : Percentile(timings, 0.50).ToString("F1", CultureInfo.InvariantCulture))
                + " tabSwitchToVisibleMsP95=" + (timings.Count == 0 ? "n/a" : Percentile(timings, 0.95).ToString("F1", CultureInfo.InvariantCulture))
                + " tabSwitchToVisibleMsMax=" + (timings.Count == 0 ? "n/a" : timings[timings.Count - 1].ToString("F1", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// One machine-readable line with the application's own statistics, parsed from the last "Scan metrics" line
        /// it wrote during this run. The format is the product's, so this reader fails to "n/a" instead of guessing.
        /// </summary>
        private static string BuildLatencyLine(string label, string logPath, long offset)
        {
            string text = ReadLog(logPath, offset, 262144);
            string line = null;
            if (text != null)
            {
                int index = 0;
                while (true)
                {
                    int marker = text.IndexOf("Scan metrics:", index, StringComparison.Ordinal);
                    if (marker < 0) break;
                    int end = text.IndexOf('\n', marker);
                    line = end < 0 ? text.Substring(marker) : text.Substring(marker, end - marker);
                    index = marker + 1;
                }
            }
            return "MEASURE-LATENCY label=" + label
                + " requests=" + Statistic(line, @"requests=(\d+)")
                + " refused=" + Statistic(line, @"refused=(\d+)")
                + " completed=" + Statistic(line, @"completed=(\d+)")
                + " stale=" + Statistic(line, @"stale=(\d+)")
                + " discarded=" + Statistic(line, @"discarded=(\d+)")
                + " errors=" + Statistic(line, @"errors=(\d+)")
                + " stalls=" + Statistic(line, @"stalls=(\d+)")
                + " scanMsP50=" + Statistic(line, @"scanMs p50=([0-9.]+)")
                + " scanMsP95=" + Statistic(line, @"p95=([0-9.]+)")
                + " scanMsMax=" + Statistic(line, @"max=(\d+)")
                + " queueMsP95=" + Statistic(line, @"queueMs p95=([0-9.]+)")
                + " events=" + Statistic(line, @"events=(\d+)")
                + " samples=" + Statistic(line, @"samples=(\d+)")
                + " eventToVisibleMsP95=" + Statistic(line, @"eventToVisibleMs p95=(\d+)")
                + " lastEventToVisibleMs=" + Statistic(line, @"lastEventToVisibleMs=(\d+)");
        }

        private static string Statistic(string line, string pattern)
        {
            if (string.IsNullOrEmpty(line)) return "n/a";
            Match match = Regex.Match(line, pattern);
            return match.Success ? match.Groups[1].Value : "n/a";
        }

        /// <summary>Waits for the readiness line the application logs at the end of its own startup.</summary>
        private static long WaitForReadyMarker(Process app, string logPath, long offset, int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                if (app.HasExited) return -1;
                string text = ReadLog(logPath, offset, 262144);
                if (text != null && text.IndexOf(ReadyMarker, StringComparison.Ordinal) >= 0) return watch.ElapsedMilliseconds;
                Thread.Sleep(100);
            }
            return -1;
        }

        /// <summary>Counts the application's own state lines, which is how often it re-evaluated the overlay.</summary>
        private static long CountStateLines(string logPath, long offset)
        {
            string text = ReadLog(logPath, offset, 1048576);
            if (text == null) return 0;
            long count = 0;
            int index = 0;
            while (true)
            {
                int marker = text.IndexOf("Overlay state:", index, StringComparison.Ordinal);
                if (marker < 0) break;
                count++;
                index = marker + 1;
            }
            return count;
        }

        /// <summary>
        /// One scan of the fixture browser window with the shipped scanner, to time UI Automation itself. The scan
        /// runs on a dedicated MTA thread, exactly like the application's scan lanes, so the number is comparable.
        /// </summary>
        private static void SampleEnvironmentScan(IntPtr window, List<double> latencies)
        {
            double elapsed = -1;
            Thread worker = new Thread(delegate()
            {
                try
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    ScanResult result = UiaTabScanner.Scan(window, AppInfo.DefaultButtonSizeLogical, AppInfo.DefaultButtonInsetLogical);
                    watch.Stop();
                    if (result != null && result.Outcome != ScanOutcome.Error) elapsed = watch.Elapsed.TotalMilliseconds;
                }
                catch (Exception)
                {
                    // A measurement must never fail because one sample could not be taken.
                }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
            if (worker.Join(5000) && elapsed >= 0) latencies.Add(elapsed);
        }

        private static string ReadLog(string logPath, long offset, int maximumBytes)
        {
            try
            {
                if (!File.Exists(logPath)) return null;
                using (FileStream stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= offset) return string.Empty;
                    long start = offset;
                    if (stream.Length - start > maximumBytes) start = stream.Length - maximumBytes;
                    stream.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[(int)Math.Min(maximumBytes, stream.Length - start)];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    return Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static double Percentile(List<double> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0;
            int index = (int)Math.Ceiling(sorted.Count * fraction) - 1;
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return sorted[index];
        }

        private static IntPtr WaitForWindow(int processId, int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                IntPtr found = FindVisibleWindow(processId, false);
                if (found != IntPtr.Zero) return found;
                Thread.Sleep(250);
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindVisibleWindow(int processId, bool allowSmall)
        {
            Collected.Clear();
            EnumWindows(delegate(IntPtr hwnd, IntPtr state)
            {
                uint owner;
                NativeMethods.GetWindowThreadProcessId(hwnd, out owner);
                if (owner != (uint)processId) return true;
                if (!NativeMethods.IsWindowVisible(hwnd)) return true;
                System.Drawing.Rectangle bounds = NativeMethods.GetWindowBounds(hwnd);
                if (allowSmall)
                {
                    if (bounds.Width < 18 || bounds.Width > 80 || bounds.Height < 18 || bounds.Height > 80) return true;
                }
                else if (bounds.Width < 200 || bounds.Height < 100) return true;
                Collected.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return Collected.Count > 0 ? Collected[0] : IntPtr.Zero;
        }

        private static void Kill(Process process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited) process.Kill();
                process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
