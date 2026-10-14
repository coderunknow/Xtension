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
        // Minimum spacing between periodic UI Automation scans while Brave is in front.
        public const int ScanIntervalMs = 500;
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
        // Scan cadence for the dashboard while Brave is not in front (diagnostics only, never the overlay).
        public const int DashboardScanIntervalMs = 2000;
        public const int LogRotationBytes = 1024 * 1024;
        public const int LogThrottleMs = 10000;
        // Single-instance and cross-process names. Local\ keeps them per user session.
        public const string SingleInstanceMutexName = @"Local\Xtension.SingleInstance";
        public const string ShowDashboardEventName = @"Local\Xtension.ShowDashboard";
    }
}
