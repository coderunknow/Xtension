using System;

namespace Xtension
{
    /// <summary>How the custom × decides to become visible. AlwaysWhenSafe is the established default.</summary>
    internal enum OverlayVisibilityMode
    {
        /// <summary>Show the × whenever the safety and eligibility rules are satisfied (the previous behaviour).</summary>
        AlwaysWhenSafe = 0,
        /// <summary>Show the × only while the pointer is over the confirmed selected tab or the × itself.</summary>
        HoverOnSelectedTab = 1
    }

    /// <summary>
    /// The typed Xtension settings. Values are validated on load and on assignment, so the rest of the app can
    /// read them without repeating bounds checks. Defaults preserve the dev2 behaviour for existing users and for
    /// a missing or unreadable file.
    /// </summary>
    internal sealed class AppSettings
    {
        public const string OverlayEnabledKey = "overlayEnabled";
        public const string VisibilityModeKey = "visibilityMode";
        public const string ButtonSizeKey = "buttonSize";
        public const string ButtonInsetKey = "buttonInset";
        public const string HoverGraceKey = "hoverGraceMs";
        public const string ScanIntervalKey = "scanIntervalMs";
        public const string LanguageKey = "language";

        public const string VisibilityAlwaysToken = "always";
        public const string VisibilityHoverToken = "hover";

        public bool OverlayEnabled;
        public OverlayVisibilityMode VisibilityMode;
        public int ButtonSizeLogical;
        public int ButtonInsetLogical;
        public int HoverGraceMs;
        public int ScanIntervalMs;
        public AppLanguage Language;

        public static AppSettings Defaults()
        {
            AppSettings settings = new AppSettings();
            settings.OverlayEnabled = true;
            settings.VisibilityMode = OverlayVisibilityMode.AlwaysWhenSafe;
            settings.ButtonSizeLogical = AppInfo.DefaultButtonSizeLogical;
            settings.ButtonInsetLogical = AppInfo.DefaultButtonInsetLogical;
            settings.HoverGraceMs = AppInfo.DefaultHoverGraceMs;
            settings.ScanIntervalMs = AppInfo.ScanIntervalMs;
            settings.Language = AppLanguage.Vietnamese;
            return settings;
        }

        /// <summary>Reads every value with an explicit default and a bounded range. Invalid data falls back safely.</summary>
        public static AppSettings Load(SettingsStore store)
        {
            if (store == null) return Defaults();
            AppSettings settings = new AppSettings();
            settings.OverlayEnabled = store.GetBool(OverlayEnabledKey, true);
            settings.VisibilityMode = ParseVisibility(store.GetString(VisibilityModeKey, VisibilityAlwaysToken));
            settings.ButtonSizeLogical = store.GetInt(ButtonSizeKey, AppInfo.DefaultButtonSizeLogical,
                AppInfo.MinButtonSizeLogical, AppInfo.MaxButtonSizeLogical);
            settings.ButtonInsetLogical = store.GetInt(ButtonInsetKey, AppInfo.DefaultButtonInsetLogical,
                AppInfo.MinButtonInsetLogical, AppInfo.MaxButtonInsetLogical);
            settings.HoverGraceMs = store.GetInt(HoverGraceKey, AppInfo.DefaultHoverGraceMs,
                AppInfo.MinHoverGraceMs, AppInfo.MaxHoverGraceMs);
            settings.ScanIntervalMs = store.GetInt(ScanIntervalKey, AppInfo.ScanIntervalMs,
                AppInfo.MinScanIntervalMs, AppInfo.MaxScanIntervalMs);
            settings.Language = Strings.ParseLanguage(store.GetString(LanguageKey, Strings.LanguageToken(AppLanguage.Vietnamese)),
                AppLanguage.Vietnamese);
            settings.Normalize();
            return settings;
        }

        /// <summary>Writes all values with a single file replacement.</summary>
        public void Save(SettingsStore store)
        {
            if (store == null) return;
            Normalize();
            string[] keys = new string[]
            {
                OverlayEnabledKey, VisibilityModeKey, ButtonSizeKey, ButtonInsetKey,
                HoverGraceKey, ScanIntervalKey, LanguageKey
            };
            string[] values = new string[]
            {
                OverlayEnabled ? "true" : "false",
                VisibilityToken(VisibilityMode),
                ButtonSizeLogical.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ButtonInsetLogical.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HoverGraceMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ScanIntervalMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Strings.LanguageToken(Language)
            };
            store.SetMany(keys, values);
        }

        /// <summary>Forces every field into its supported range. Safe to call after any assignment.</summary>
        public void Normalize()
        {
            ButtonSizeLogical = TabGeometry.ClampButtonSize(ButtonSizeLogical);
            ButtonInsetLogical = TabGeometry.ClampButtonInset(ButtonInsetLogical);
            if (HoverGraceMs < AppInfo.MinHoverGraceMs) HoverGraceMs = AppInfo.MinHoverGraceMs;
            if (HoverGraceMs > AppInfo.MaxHoverGraceMs) HoverGraceMs = AppInfo.MaxHoverGraceMs;
            if (ScanIntervalMs < AppInfo.MinScanIntervalMs) ScanIntervalMs = AppInfo.MinScanIntervalMs;
            if (ScanIntervalMs > AppInfo.MaxScanIntervalMs) ScanIntervalMs = AppInfo.MaxScanIntervalMs;
            if (VisibilityMode != OverlayVisibilityMode.HoverOnSelectedTab) VisibilityMode = OverlayVisibilityMode.AlwaysWhenSafe;
            if (Language != AppLanguage.English) Language = AppLanguage.Vietnamese;
        }

        public AppSettings Copy()
        {
            AppSettings copy = new AppSettings();
            copy.OverlayEnabled = OverlayEnabled;
            copy.VisibilityMode = VisibilityMode;
            copy.ButtonSizeLogical = ButtonSizeLogical;
            copy.ButtonInsetLogical = ButtonInsetLogical;
            copy.HoverGraceMs = HoverGraceMs;
            copy.ScanIntervalMs = ScanIntervalMs;
            copy.Language = Language;
            return copy;
        }

        /// <summary>True when both instances hold the same values. Used to skip redundant writes and re-renders.</summary>
        public bool Matches(AppSettings other)
        {
            if (other == null) return false;
            return OverlayEnabled == other.OverlayEnabled
                && VisibilityMode == other.VisibilityMode
                && ButtonSizeLogical == other.ButtonSizeLogical
                && ButtonInsetLogical == other.ButtonInsetLogical
                && HoverGraceMs == other.HoverGraceMs
                && ScanIntervalMs == other.ScanIntervalMs
                && Language == other.Language;
        }

        public static OverlayVisibilityMode ParseVisibility(string token)
        {
            if (string.Equals(token, VisibilityHoverToken, StringComparison.OrdinalIgnoreCase)) return OverlayVisibilityMode.HoverOnSelectedTab;
            return OverlayVisibilityMode.AlwaysWhenSafe;
        }

        public static string VisibilityToken(OverlayVisibilityMode mode)
        {
            return mode == OverlayVisibilityMode.HoverOnSelectedTab ? VisibilityHoverToken : VisibilityAlwaysToken;
        }
    }
}
