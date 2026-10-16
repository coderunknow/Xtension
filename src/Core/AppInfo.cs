namespace Xtension
{
    /// <summary>
    /// Product identity and every bounded runtime constant.
    /// The version comes from the VERSION file through the generated ProductVersion class.
    /// </summary>
    internal static class AppInfo
    {
        public const string ProductName = "Xtension";
        public const string SemanticVersion = ProductVersion.Semantic;
        public const string DisplayVersion = ProductVersion.Display;

        // Foreground detection is cheap (no UI Automation), so it runs more often than scans.
        public const int ForegroundPollMs = 250;
        // Hover mode needs a shorter tick so the button appears promptly under the pointer. Each tick is a few
        // cheap window queries and no UI Automation call.
        public const int HoverPollMs = 100;
        // Default and bounds for the configurable periodic UI Automation scan interval.
        public const int ScanIntervalMs = 500;
        public const int MinScanIntervalMs = 300;
        public const int MaxScanIntervalMs = 2000;
        // Scan budgets. A scan that exceeds one of them is incomplete and the overlay stays hidden.
        public const int ScanTimeBudgetMs = 1000;
        public const int ScanMaxNodes = 1500;
        public const int ScanMaxDepth = 12;
        public const int ScanMaxSiblings = 400;
        // The overlay is hidden when no successful scan has completed within this window.
        public const int ScanStaleMs = 1500;
        // A scan still running after this long is reported as stalled in diagnostics.
        public const int ScanStallMs = 5000;
        // A close validation that does not return within this window is abandoned.
        public const int CloseValidationTimeoutMs = 3000;
        // After a close keystroke, clicks are ignored for this long so a double-click cannot close the next tab.
        public const int CloseCooldownMs = 800;
        // Scan cadence for the dashboard while no supported browser is in front (diagnostics only, never the overlay).
        public const int DashboardScanIntervalMs = 2000;
        public const int LogRotationBytes = 1024 * 1024;
        public const int LogThrottleMs = 10000;

        // Custom × geometry, in logical pixels at 96 DPI. The same values drive painting, hit testing,
        // placement and the close click validation, so they can never disagree.
        public const int DefaultButtonSizeLogical = 26;
        public const int MinButtonSizeLogical = 18;
        public const int MaxButtonSizeLogical = 40;
        public const int DefaultButtonInsetLogical = 3;
        public const int MinButtonInsetLogical = 0;
        public const int MaxButtonInsetLogical = 12;

        // Hover mode: how long the × survives after the pointer leaves the tab, so moving onto the button
        // itself cannot make it flicker away. Applies only to hover mode.
        public const int DefaultHoverGraceMs = 250;
        public const int MinHoverGraceMs = 0;
        public const int MaxHoverGraceMs = 1000;

        // Local settings file limits. A malformed or hostile file must not be able to exhaust memory.
        public const int MaxSettingsFileBytes = 64 * 1024;
        public const int MaxSettingsKeyLength = 64;
        public const int MaxSettingsValueLength = 256;

        // Performance profiles (see Profiles). Every value is inside the documented setting range; a profile never
        // selects a scan budget, a staleness window, a deadline, a cooldown or a confirmation rule.
        public const int MinForegroundPollMs = 100;
        public const int MinHoverPollMs = 50;
        public const int FastDashboardScanIntervalMs = 1500;
        public const int LowResourceScanIntervalMs = 1200;
        public const int LowResourceForegroundPollMs = 500;
        public const int LowResourceHoverPollMs = 250;
        public const int LowResourceDashboardScanIntervalMs = 5000;

        // Best-effort UI Automation event notifications. Polling remains the fallback, so a registration that hangs or
        // fails only delays the fast path; it never removes a scan.
        public const int EventWatchRegisterTimeoutMs = 1500;
        // Floor between two event-triggered scan requests. The worker already allows one scan at a time, and the
        // controller keeps at most one event-driven request pending, so an event storm cannot become a scan loop.
        public const int EventWatchMinScanGapMs = 80;

        // Diagnostics are bounded: a fixed number of timings and at most one summary line per interval.
        public const int MetricSampleCapacity = 64;
        // One statistics line at most every 15 s, or every 50 completed scans, whichever comes first. Both bounds
        // keep the volume of the log linear, and the scan-count bound is what makes a short measurement window
        // (the CI resource job samples 20 s) contain real numbers instead of an empty line.
        public const int MetricsLogIntervalMs = 15000;
        public const int MetricsLogScanCount = 50;

        // Settings backup: the file is read into memory only up to this size, and only this schema is written.
        public const int MaxSettingsBackupBytes = 256 * 1024;
        public const int SettingsBackupSchema = 1;

        // Appearance bounds. Opacity is applied as uniform window alpha, so it cannot change the hit area.
        public const int DefaultButtonOpacityPercent = 100;
        public const int MinButtonOpacityPercent = 30;
        public const int MaxButtonOpacityPercent = 100;

        // In-app notification surface: one non-activating, sound-free window instead of a Shell balloon or toast.
        public const int NoticeWidthLogical = 360;
        public const int NoticeMinHeightLogical = 76;
        public const int NoticeAutoCloseMs = 7000;
        public const int NoticeMarginLogical = 14;

        // Single-instance and cross-process names. Local\ keeps them per user session.
        public const string SingleInstanceMutexName = @"Local\Xtension.SingleInstance";
        public const string ShowDashboardEventName = @"Local\Xtension.ShowDashboard";
    }
}
