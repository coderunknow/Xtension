using System;
using System.Globalization;
using System.Text;

namespace Xtension
{
    /// <summary>State of one scan lane, for diagnostics. A lane holds one running scan and at most one pending request.</summary>
    internal sealed class WorkerSnapshot
    {
        public bool Busy;
        public long RunningForMs;

        public string Describe()
        {
            if (!Busy) return "idle";
            return "running " + RunningForMs + " ms";
        }
    }

    /// <summary>State of the best-effort event sources. Polling is the fallback whenever a source is not working.</summary>
    internal sealed class EventWatchSnapshot
    {
        public bool Registered;
        public long Registrations;
        public long Failures;
        /// <summary>Sanitised description of the last failure (operation and exception type only).</summary>
        public string LastFailure = string.Empty;

        public string Describe()
        {
            if (Registered) return "registered";
            if (Failures > 0) return "unavailable (" + Failures + " failures)";
            return "not registered";
        }
    }

    /// <summary>
    /// Read-only view of the controller for the dashboard, the diagnostics window and the diagnostic report.
    /// Snapshots are cheap value copies; they never contain a tab title other than the selected tab's accessible
    /// name, which the diagnostics window displays on screen and the copied report never includes.
    /// </summary>
    internal sealed class ActiveTabSnapshot
    {
        public bool Enabled;
        public bool OverlayVisible;
        /// <summary>True when the foreground window is a browser this build can confirm tabs for and the user enabled.</summary>
        public bool SupportedBrowserInForeground;
        /// <summary>Which browser is in the foreground, or None.</summary>
        public BrowserKind Browser;
        /// <summary>True when the browser in the foreground is known but the user turned it off in settings.</summary>
        public bool ForegroundBrowserDisabled;
        /// <summary>True when the foreground window belongs to a browser this build knows but does not support.</summary>
        public bool BrowserUnsupported;
        /// <summary>True when the foreground window belongs to Xtension itself (a window or a notification).</summary>
        public bool OwnWindowInForeground;
        public bool Stalled;
        public OverlayVisibilityMode VisibilityMode;
        public PerformanceProfile Profile;
        public string IdleReason = string.Empty;
        public string Notice = string.Empty;
        public string HiddenReason = string.Empty;
        /// <summary>Latest accepted scan. Null before the first scan or while the overlay is disabled.</summary>
        public ScanResult Scan;
        /// <summary>Age of the confirmation that authorises the current eligibility, or -1 when nothing is confirmed.</summary>
        public long ConfirmationAgeMs = -1;
        /// <summary>Age of the newest scan result, or -1 when no scan has completed.</summary>
        public long LastScanAgeMs = -1;
        /// <summary>True when the newest scan is older than the staleness window, so it cannot authorise anything.</summary>
        public bool ScanStale;
        public ScanMetricsSnapshot Metrics;
        public WorkerSnapshot OverlayWorker;
        public WorkerSnapshot CloseWorker;
        public WorkerSnapshot DashboardWorker;
        public EventWatchSnapshot Watcher;
        /// <summary>Sanitised, English, log-style summary of the effective configuration.</summary>
        public string EffectiveSettings = string.Empty;
        /// <summary>Copy of the settings the controller is using, so a report can be built from the snapshot alone.</summary>
        public AppSettings Settings;
    }

    /// <summary>
    /// Builds the plain-text diagnostic report the user can copy. The report is written from bounded, non-private
    /// fields only: no tab titles, no window titles, no URLs, no provider messages and no browsing history.
    /// </summary>
    internal static class DiagnosticsReport
    {
        public static string Build(ActiveTabSnapshot snapshot, AppSettings settings)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Xtension " + AppInfo.DisplayVersion + " diagnostic report");
            text.AppendLine("This report contains no tab titles, window titles, URLs or page content.");
            text.AppendLine("os=" + Environment.OSVersion.VersionString + " arch=" + (IntPtr.Size == 8 ? "x64" : "x86")
                + " cores=" + Environment.ProcessorCount);
            text.AppendLine();

            if (snapshot == null)
            {
                text.AppendLine("state=unavailable");
                return text.ToString();
            }

            text.AppendLine("overlayEnabled=" + snapshot.Enabled);
            text.AppendLine("overlayVisible=" + snapshot.OverlayVisible);
            text.AppendLine("browserInForeground=" + BrowserName(snapshot.Browser)
                + (snapshot.SupportedBrowserInForeground ? " (confirmed identity)"
                    : snapshot.ForegroundBrowserDisabled ? " (turned off in settings)"
                    : snapshot.BrowserUnsupported ? " (accessibility contract not implemented in this build)" : "")
                + " level=" + SupportToken(snapshot.Browser) + " contract=" + ContractToken(snapshot.Browser)
                + " scannerImplemented=" + BrowserCatalog.IsScannerImplemented(snapshot.Browser));
            text.AppendLine("ownWindowInForeground=" + snapshot.OwnWindowInForeground);
            text.AppendLine("stalled=" + snapshot.Stalled);
            text.AppendLine("hiddenReason=" + Single(snapshot.HiddenReason));
            if (!string.IsNullOrEmpty(snapshot.IdleReason)) text.AppendLine("idleReason=" + Single(snapshot.IdleReason));
            if (!string.IsNullOrEmpty(snapshot.Notice)) text.AppendLine("notice=" + Single(snapshot.Notice));

            ScanResult scan = snapshot.Scan;
            if (scan == null)
            {
                text.AppendLine("lastScan=none");
            }
            else
            {
                text.AppendLine("lastScanOutcome=" + scan.Outcome + " key=" + scan.Key + " tabs=" + scan.Tabs.Count
                    + " nodes=" + scan.NodesVisited + " elapsedMs=" + scan.ElapsedMs + " dpi=" + scan.Dpi
                    + " ageMs=" + snapshot.LastScanAgeMs + " stale=" + snapshot.ScanStale);
                if (scan.SelectedHorizontalTab != null)
                {
                    // The rectangle is diagnostic geometry, not browsing content; the accessible name is not copied.
                    text.AppendLine("confirmedTabRect=" + scan.SelectedHorizontalTab.Bounds.Left + ","
                        + scan.SelectedHorizontalTab.Bounds.Top + "," + scan.SelectedHorizontalTab.Bounds.Width + "x"
                        + scan.SelectedHorizontalTab.Bounds.Height);
                }
                if (!string.IsNullOrEmpty(scan.Error)) text.AppendLine("lastScanError=" + Single(scan.Error));
            }
            text.AppendLine("confirmationAgeMs=" + snapshot.ConfirmationAgeMs);

            text.AppendLine("overlayWorker=" + Describe(snapshot.OverlayWorker));
            text.AppendLine("closeWorker=" + Describe(snapshot.CloseWorker));
            text.AppendLine("dashboardWorker=" + Describe(snapshot.DashboardWorker));
            text.AppendLine("eventWatch=" + (snapshot.Watcher == null ? "n/a" : snapshot.Watcher.Describe())
                + (snapshot.Watcher == null || string.IsNullOrEmpty(snapshot.Watcher.LastFailure)
                    ? string.Empty : " lastFailure=" + snapshot.Watcher.LastFailure));

            text.AppendLine("metrics=" + (snapshot.Metrics == null ? "n/a" : snapshot.Metrics.Describe()));
            text.AppendLine();
            text.AppendLine("profile=" + Profiles.Token(settings == null ? PerformanceProfile.Balanced : snapshot.Profile));
            text.AppendLine("effectiveSettings=" + Single(SettingsSummary.Build(settings)));
            return text.ToString();
        }

        private static string SupportToken(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            if (!BrowserCatalog.TryGet(kind, out descriptor)) return "none";
            if (descriptor.Support == BrowserSupport.Verified) return "verified";
            if (descriptor.Support == BrowserSupport.Experimental) return "experimental";
            return "unsupported";
        }

        private static string ContractToken(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            if (!BrowserCatalog.TryGet(kind, out descriptor)) return "none";
            return BrowserCatalog.ContractToken(descriptor.Contract);
        }

        private static string BrowserName(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            return BrowserCatalog.TryGet(kind, out descriptor) ? descriptor.DisplayName : "none";
        }

        private static string Describe(WorkerSnapshot worker)
        {
            return worker == null ? "n/a" : worker.Describe();
        }

        /// <summary>Collapses a localized sentence into one line so the report stays parseable.</summary>
        private static string Single(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }

    /// <summary>One-line, culture-independent summary of the effective configuration. Contains no private data.</summary>
    internal static class SettingsSummary
    {
        public static string Build(AppSettings settings)
        {
            if (settings == null) return "n/a";
            AppSettings copy = settings.Copy();
            copy.Normalize();
            ProfileCadence cadence = Profiles.CadenceFor(copy.Profile);
            StringBuilder text = new StringBuilder();
            text.Append("mode=").Append(AppSettings.VisibilityToken(copy.VisibilityMode))
                .Append(" size=").Append(copy.ButtonSizeLogical.ToString(CultureInfo.InvariantCulture))
                .Append(" inset=").Append(copy.ButtonInsetLogical.ToString(CultureInfo.InvariantCulture))
                .Append(" grace=").Append(copy.HoverGraceMs.ToString(CultureInfo.InvariantCulture)).Append("ms")
                .Append(" scanInterval=").Append(copy.ScanIntervalMs.ToString(CultureInfo.InvariantCulture)).Append("ms")
                .Append(" profile=").Append(Profiles.Token(copy.Profile))
                .Append(" poll=").Append(cadence.ForegroundPollMs.ToString(CultureInfo.InvariantCulture)).Append("ms")
                .Append(" hoverPoll=").Append(cadence.HoverPollMs.ToString(CultureInfo.InvariantCulture)).Append("ms")
                .Append(" dashboardScan=").Append(cadence.DashboardScanIntervalMs.ToString(CultureInfo.InvariantCulture)).Append("ms")
                .Append(" color=").Append(AppSettings.ColorToken(copy.ButtonColor))
                .Append(" opacity=").Append(copy.ButtonOpacityPercent.ToString(CultureInfo.InvariantCulture)).Append("%")
                .Append(" border=").Append(copy.ButtonBorder ? "on" : "off")
                .Append(" language=").Append(Strings.LanguageToken(copy.Language))
                .Append(" browsers=").Append(BrowserCatalog.MaskTokenList(copy.Browsers));
            return text.ToString();
        }
    }
}
