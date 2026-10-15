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
        // Scan cadence for the dashboard while Brave is not in front (diagnostics only, never the overlay).
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

        // Single-instance and cross-process names. Local\ keeps them per user session.
        public const string SingleInstanceMutexName = @"Local\Xtension.SingleInstance";
        public const string ShowDashboardEventName = @"Local\Xtension.ShowDashboard";
    }
}
