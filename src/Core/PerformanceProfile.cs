using System;

namespace Xtension
{
    /// <summary>
    /// How much background work Xtension does. <see cref="Custom"/> is not a preset: it means the stored individual
    /// values no longer match any preset, and it is what an existing installation keeps (the dev3 values).
    /// A profile never selects a safety boundary: it may only choose cadences inside the documented settings ranges.
    /// </summary>
    internal enum PerformanceProfile
    {
        /// <summary>The user edited a profile-controlled value, so the values are authoritative and no preset applies.</summary>
        Custom = 0,
        /// <summary>The dev3 defaults: 500 ms scans, 250 ms foreground poll, 100 ms hover poll.</summary>
        Balanced = 1,
        /// <summary>The documented minimum scan interval and the fastest poll cadence.</summary>
        Fast = 2,
        /// <summary>Fewer periodic scans and slower polls while the user is not interacting with a browser.</summary>
        LowResource = 3
    }

    /// <summary>
    /// The cadence parameters one profile selects. Values are all inside the documented bounds; the scan budgets,
    /// staleness window, close-validation deadline, cooldown, stall threshold and confirmation rules are constants in
    /// <see cref="AppInfo"/> and are deliberately not part of a profile.
    /// </summary>
    internal sealed class ProfileCadence
    {
        public PerformanceProfile Profile;
        public int ScanIntervalMs;
        public int ForegroundPollMs;
        public int HoverPollMs;
        public int DashboardScanIntervalMs;
        /// <summary>True when the profile uses event notifications as the primary trigger (always on in this build).</summary>
        public bool EventDriven;
    }

    /// <summary>
    /// The profile table and the rule that decides which profile a stored configuration represents. The persisted
    /// marker records what the user last selected; the individual settings stay authoritative, so a value the user
    /// edited later survives a restart and the configuration is reported as <see cref="PerformanceProfile.Custom"/>
    /// instead of being silently reset to the old preset.
    /// </summary>
    internal static class Profiles
    {
        public const string BalancedToken = "balanced";
        public const string FastToken = "fast";
        public const string LowResourceToken = "low-resource";
        public const string CustomToken = "custom";

        private static readonly ProfileCadence[] Table = new ProfileCadence[]
        {
            new ProfileCadence { Profile = PerformanceProfile.Custom, ScanIntervalMs = AppInfo.ScanIntervalMs,
                ForegroundPollMs = AppInfo.ForegroundPollMs, HoverPollMs = AppInfo.HoverPollMs,
                DashboardScanIntervalMs = AppInfo.DashboardScanIntervalMs, EventDriven = true },
            new ProfileCadence { Profile = PerformanceProfile.Balanced, ScanIntervalMs = AppInfo.ScanIntervalMs,
                ForegroundPollMs = AppInfo.ForegroundPollMs, HoverPollMs = AppInfo.HoverPollMs,
                DashboardScanIntervalMs = AppInfo.DashboardScanIntervalMs, EventDriven = true },
            new ProfileCadence { Profile = PerformanceProfile.Fast, ScanIntervalMs = AppInfo.MinScanIntervalMs,
                ForegroundPollMs = AppInfo.MinForegroundPollMs, HoverPollMs = AppInfo.MinHoverPollMs,
                DashboardScanIntervalMs = AppInfo.FastDashboardScanIntervalMs, EventDriven = true },
            new ProfileCadence { Profile = PerformanceProfile.LowResource, ScanIntervalMs = AppInfo.LowResourceScanIntervalMs,
                ForegroundPollMs = AppInfo.LowResourceForegroundPollMs, HoverPollMs = AppInfo.LowResourceHoverPollMs,
                DashboardScanIntervalMs = AppInfo.LowResourceDashboardScanIntervalMs, EventDriven = true }
        };

        public static bool IsPreset(PerformanceProfile profile)
        {
            return profile == PerformanceProfile.Balanced
                || profile == PerformanceProfile.Fast
                || profile == PerformanceProfile.LowResource;
        }

        /// <summary>Cadence for a profile. Custom keeps the balanced cadence, because the values themselves decide.</summary>
        public static ProfileCadence CadenceFor(PerformanceProfile profile)
        {
            foreach (ProfileCadence row in Table)
            {
                if (row.Profile == profile) return Clone(row);
            }
            foreach (ProfileCadence row in Table)
            {
                if (row.Profile == PerformanceProfile.Balanced) return Clone(row);
            }
            return Clone(Table[0]);
        }

        /// <summary>
        /// A copy of a table row. Callers get their own instance, so a caller cannot mutate the table and a later
        /// request cannot see a value somebody else changed.
        /// </summary>
        private static ProfileCadence Clone(ProfileCadence source)
        {
            ProfileCadence copy = new ProfileCadence();
            copy.Profile = source.Profile;
            copy.ScanIntervalMs = source.ScanIntervalMs;
            copy.ForegroundPollMs = source.ForegroundPollMs;
            copy.HoverPollMs = source.HoverPollMs;
            copy.DashboardScanIntervalMs = source.DashboardScanIntervalMs;
            copy.EventDriven = source.EventDriven;
            return copy;
        }

        /// <summary>Stable token for the persisted marker.</summary>
        public static string Token(PerformanceProfile profile)
        {
            if (profile == PerformanceProfile.Fast) return FastToken;
            if (profile == PerformanceProfile.LowResource) return LowResourceToken;
            if (profile == PerformanceProfile.Custom) return CustomToken;
            return BalancedToken;
        }

        public static PerformanceProfile ParseToken(string token, PerformanceProfile fallback)
        {
            if (string.Equals(token, FastToken, StringComparison.OrdinalIgnoreCase)) return PerformanceProfile.Fast;
            if (string.Equals(token, LowResourceToken, StringComparison.OrdinalIgnoreCase)) return PerformanceProfile.LowResource;
            if (string.Equals(token, CustomToken, StringComparison.OrdinalIgnoreCase)) return PerformanceProfile.Custom;
            if (string.Equals(token, BalancedToken, StringComparison.OrdinalIgnoreCase)) return PerformanceProfile.Balanced;
            return fallback;
        }

        /// <summary>
        /// Which profile a stored marker plus stored values represent. A preset only wins while the stored
        /// profile-controlled values still equal that preset's mapping; otherwise the configuration is custom, which is
        /// what stops a later restart from overwriting a value the user chose after picking a preset.
        /// </summary>
        public static PerformanceProfile Resolve(PerformanceProfile marker, int scanIntervalMs)
        {
            if (!IsPreset(marker)) return PerformanceProfile.Custom;
            return CadenceFor(marker).ScanIntervalMs == scanIntervalMs ? marker : PerformanceProfile.Custom;
        }

        /// <summary>
        /// Applies a preset to a settings instance. Only profile-controlled values are written; every other setting,
        /// including the language, the visibility mode, the geometry, the appearance and the browser list, is left
        /// exactly as it was.
        /// </summary>
        public static void ApplyTo(PerformanceProfile profile, AppSettings settings)
        {
            if (settings == null) return;
            if (!IsPreset(profile))
            {
                settings.Profile = PerformanceProfile.Custom;
                return;
            }
            ProfileCadence cadence = CadenceFor(profile);
            settings.Profile = profile;
            settings.ScanIntervalMs = cadence.ScanIntervalMs;
            settings.Normalize();
        }
    }
}
