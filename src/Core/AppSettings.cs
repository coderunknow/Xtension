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
    /// The × fill colour. Chosen from a fixed set so the appearance never becomes unreadable: every entry has been
    /// checked against the white cross and against a light and a dark tab strip.
    /// </summary>
    internal enum OverlayColor
    {
        /// <summary>Dark slate (the established dev2/dev3 colour).</summary>
        Dark = 0,
        Red = 1,
        Blue = 2,
        Green = 3
    }

    /// <summary>
    /// The typed Xtension settings. Values are validated on load and on assignment, so the rest of the app can
    /// read them without repeating bounds checks. Defaults preserve the established behaviour for existing users and
    /// for a missing or unreadable file.
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
        public const string ProfileKey = "performanceProfile";
        public const string BrowsersKey = "browsers";
        public const string ButtonColorKey = "buttonColor";
        public const string ButtonOpacityKey = "buttonOpacity";
        public const string ButtonBorderKey = "buttonBorder";

        public const string VisibilityAlwaysToken = "always";
        public const string VisibilityHoverToken = "hover";
        public const string ColorDarkToken = "dark";
        public const string ColorRedToken = "red";
        public const string ColorBlueToken = "blue";
        public const string ColorGreenToken = "green";

        /// <summary>Every key this version writes, in a fixed order, so a backup file is stable and diffable.</summary>
        public static readonly string[] KnownKeys = new string[]
        {
            OverlayEnabledKey, VisibilityModeKey, ButtonSizeKey, ButtonInsetKey, HoverGraceKey, ScanIntervalKey,
            LanguageKey, ProfileKey, BrowsersKey, ButtonColorKey, ButtonOpacityKey, ButtonBorderKey
        };

        public bool OverlayEnabled;
        public OverlayVisibilityMode VisibilityMode;
        public int ButtonSizeLogical;
        public int ButtonInsetLogical;
        public int HoverGraceMs;
        public int ScanIntervalMs;
        public AppLanguage Language;
        /// <summary>Which preset the user last selected, or Custom when the stored values do not match it.</summary>
        public PerformanceProfile Profile;
        /// <summary>Bitmask of the browsers the user enabled. Brave only by default, which is the established behaviour.</summary>
        public int Browsers;
        public OverlayColor ButtonColor;
        public int ButtonOpacityPercent;
        public bool ButtonBorder;

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
            settings.Profile = PerformanceProfile.Balanced;
            settings.Browsers = BrowserCatalog.Mask(BrowserKind.Brave);
            settings.ButtonColor = OverlayColor.Dark;
            settings.ButtonOpacityPercent = AppInfo.DefaultButtonOpacityPercent;
            settings.ButtonBorder = true;
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
            settings.ButtonOpacityPercent = store.GetInt(ButtonOpacityKey, AppInfo.DefaultButtonOpacityPercent,
                AppInfo.MinButtonOpacityPercent, AppInfo.MaxButtonOpacityPercent);
            settings.ButtonBorder = store.GetBool(ButtonBorderKey, true);
            settings.ButtonColor = ParseColor(store.GetString(ButtonColorKey, ColorDarkToken));
            // A browser that this build cannot confirm is dropped at load, whatever the file says.
            settings.Browsers = BrowserCatalog.ParseMask(store.GetString(BrowsersKey, BrowserCatalog.Token(BrowserKind.Brave)));
            if (settings.Browsers == BrowserCatalog.NoBrowsers)
            {
                settings.Browsers = BrowserCatalog.ParseMask(BrowserCatalog.Token(BrowserKind.Brave));
            }
            // The marker records what the user last selected; the values stay authoritative, so a value edited after
            // the preset was chosen is never silently replaced by the preset on the next start.
            PerformanceProfile marker = Profiles.ParseToken(store.GetString(ProfileKey, Profiles.Token(PerformanceProfile.Balanced)),
                PerformanceProfile.Balanced);
            settings.Profile = Profiles.Resolve(marker, settings.ScanIntervalMs);
            settings.Normalize();
            return settings;
        }

        /// <summary>Writes all values with a single file replacement.</summary>
        public void Save(SettingsStore store)
        {
            if (store == null) return;
            Normalize();
            string[] keys = KnownKeys;
            string[] values = Values();
            store.SetMany(keys, values);
        }

        /// <summary>The persisted value for every known key, in <see cref="KnownKeys"/> order.</summary>
        public string[] Values()
        {
            Normalize();
            return new string[]
            {
                OverlayEnabled ? "true" : "false",
                VisibilityToken(VisibilityMode),
                ButtonSizeLogical.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ButtonInsetLogical.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HoverGraceMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ScanIntervalMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Strings.LanguageToken(Language),
                Profiles.Token(Profile),
                BrowserCatalog.MaskTokenList(Browsers),
                ColorToken(ButtonColor),
                ButtonOpacityPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ButtonBorder ? "true" : "false"
            };
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
            if (ButtonOpacityPercent < AppInfo.MinButtonOpacityPercent) ButtonOpacityPercent = AppInfo.MinButtonOpacityPercent;
            if (ButtonOpacityPercent > AppInfo.MaxButtonOpacityPercent) ButtonOpacityPercent = AppInfo.MaxButtonOpacityPercent;
            if (ButtonColor != OverlayColor.Red && ButtonColor != OverlayColor.Blue && ButtonColor != OverlayColor.Green)
                ButtonColor = OverlayColor.Dark;
            // A browser the user cannot have enabled (unknown token, or a browser below its support level) is not kept.
            Browsers = Browsers & BrowserCatalog.SupportedMask();
            if (Browsers == BrowserCatalog.NoBrowsers) Browsers = BrowserCatalog.Mask(BrowserKind.Brave);
            // A preset marker only survives while its value is still the stored one; otherwise the configuration is
            // custom and no later start may overwrite the value the user chose.
            Profile = Profiles.Resolve(Profile, ScanIntervalMs);
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
            copy.Profile = Profile;
            copy.Browsers = Browsers;
            copy.ButtonColor = ButtonColor;
            copy.ButtonOpacityPercent = ButtonOpacityPercent;
            copy.ButtonBorder = ButtonBorder;
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
                && Language == other.Language
                && Profile == other.Profile
                && Browsers == other.Browsers
                && ButtonColor == other.ButtonColor
                && ButtonOpacityPercent == other.ButtonOpacityPercent
                && ButtonBorder == other.ButtonBorder;
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

        public static OverlayColor ParseColor(string token)
        {
            if (string.Equals(token, ColorRedToken, StringComparison.OrdinalIgnoreCase)) return OverlayColor.Red;
            if (string.Equals(token, ColorBlueToken, StringComparison.OrdinalIgnoreCase)) return OverlayColor.Blue;
            if (string.Equals(token, ColorGreenToken, StringComparison.OrdinalIgnoreCase)) return OverlayColor.Green;
            return OverlayColor.Dark;
        }

        public static string ColorToken(OverlayColor color)
        {
            if (color == OverlayColor.Red) return ColorRedToken;
            if (color == OverlayColor.Blue) return ColorBlueToken;
            if (color == OverlayColor.Green) return ColorGreenToken;
            return ColorDarkToken;
        }
    }
}
