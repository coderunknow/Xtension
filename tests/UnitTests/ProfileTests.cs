using System;

namespace Xtension.Tests
{
    /// <summary>
    /// Performance profiles. The contract these tests protect is narrow on purpose: a profile selects scan and poll
    /// cadences only. It must never move a safety value, and picking a preset must never overwrite a value the user
    /// edited afterwards — that is what the persisted marker plus the authoritative settings file have to guarantee.
    /// </summary>
    internal static class ProfileTests
    {
        [Test]
        public static void BalancedKeepsExactlyThePreviousReleaseDefaults()
        {
            ProfileCadence cadence = Profiles.CadenceFor(PerformanceProfile.Balanced);
            Assert.AreEqual(AppInfo.ScanIntervalMs, cadence.ScanIntervalMs, "balanced keeps the documented scan interval");
            Assert.AreEqual(AppInfo.ForegroundPollMs, cadence.ForegroundPollMs, "balanced keeps the documented poll");
            Assert.AreEqual(AppInfo.HoverPollMs, cadence.HoverPollMs, "balanced keeps the documented hover poll");
            Assert.AreEqual(AppInfo.DashboardScanIntervalMs, cadence.DashboardScanIntervalMs,
                "balanced keeps the documented dashboard cadence");
            Assert.AreEqual(Profiles.BalancedToken, Profiles.Token(PerformanceProfile.Balanced), "balanced token");

            AppSettings defaults = AppSettings.Defaults();
            Assert.AreEqual(PerformanceProfile.Balanced, defaults.Profile, "the default profile is balanced");
            Assert.AreEqual(AppInfo.ScanIntervalMs, defaults.ScanIntervalMs, "the default interval is unchanged");
            Assert.IsTrue(Profiles.IsPreset(defaults.Profile), "the default profile is a preset, not custom");
        }

        [Test]
        public static void EveryPresetStaysInsideTheDocumentedRanges()
        {
            foreach (PerformanceProfile profile in new PerformanceProfile[]
            {
                PerformanceProfile.Balanced, PerformanceProfile.Fast, PerformanceProfile.LowResource, PerformanceProfile.Custom
            })
            {
                ProfileCadence cadence = Profiles.CadenceFor(profile);
                Assert.IsTrue(cadence.ScanIntervalMs >= AppInfo.MinScanIntervalMs && cadence.ScanIntervalMs <= AppInfo.MaxScanIntervalMs,
                    profile + " scan interval inside the documented range: " + cadence.ScanIntervalMs);
                Assert.IsTrue(cadence.ForegroundPollMs >= AppInfo.MinForegroundPollMs,
                    profile + " foreground poll inside the documented range: " + cadence.ForegroundPollMs);
                Assert.IsTrue(cadence.HoverPollMs >= AppInfo.MinHoverPollMs,
                    profile + " hover poll inside the documented range: " + cadence.HoverPollMs);
                Assert.IsTrue(cadence.DashboardScanIntervalMs > 0, profile + " dashboard cadence is positive");
                Assert.IsTrue(cadence.EventDriven, profile + " keeps the event-driven early scan enabled");
            }
            // Fast is faster than balanced and low-resource is slower: the presets must actually differ.
            Assert.IsTrue(Profiles.CadenceFor(PerformanceProfile.Fast).ScanIntervalMs < Profiles.CadenceFor(PerformanceProfile.Balanced).ScanIntervalMs,
                "fast scans sooner than balanced");
            Assert.IsTrue(Profiles.CadenceFor(PerformanceProfile.LowResource).ScanIntervalMs > Profiles.CadenceFor(PerformanceProfile.Balanced).ScanIntervalMs,
                "low-resource scans less often than balanced");
        }

        [Test]
        public static void ApplyingAPresetChangesOnlyProfileControlledValues()
        {
            foreach (PerformanceProfile profile in new PerformanceProfile[]
            {
                PerformanceProfile.Balanced, PerformanceProfile.Fast, PerformanceProfile.LowResource
            })
            {
                AppSettings settings = AppSettings.Defaults();
                settings.OverlayEnabled = false;
                settings.VisibilityMode = OverlayVisibilityMode.HoverOnSelectedTab;
                settings.ButtonSizeLogical = AppInfo.MaxButtonSizeLogical;
                settings.ButtonInsetLogical = AppInfo.MaxButtonInsetLogical;
                settings.HoverGraceMs = 700;
                settings.Language = AppLanguage.English;
                settings.ButtonColor = OverlayColor.Green;
                settings.ButtonOpacityPercent = 60;
                settings.ButtonBorder = false;
                settings.Browsers = BrowserCatalog.Mask(BrowserKind.Brave);

                Profiles.ApplyTo(profile, settings);

                Assert.AreEqual(presetInterval(profile), settings.ScanIntervalMs, profile + " writes its interval");
                Assert.AreEqual(profile, settings.Profile, profile + " writes its marker");
                Assert.IsFalse(settings.OverlayEnabled, profile + " must not change whether the × is enabled");
                Assert.AreEqual(OverlayVisibilityMode.HoverOnSelectedTab, settings.VisibilityMode, profile + " must not change the mode");
                Assert.AreEqual(AppInfo.MaxButtonSizeLogical, settings.ButtonSizeLogical, profile + " must not change the geometry");
                Assert.AreEqual(AppInfo.MaxButtonInsetLogical, settings.ButtonInsetLogical, profile + " must not change the inset");
                Assert.AreEqual(700, settings.HoverGraceMs, profile + " must not change the hover grace");
                Assert.AreEqual(AppLanguage.English, settings.Language, profile + " must not change the language");
                Assert.AreEqual(OverlayColor.Green, settings.ButtonColor, profile + " must not change the appearance");
                Assert.AreEqual(60, settings.ButtonOpacityPercent, profile + " must not change the opacity");
                Assert.IsFalse(settings.ButtonBorder, profile + " must not change the border setting");
                Assert.AreEqual(BrowserCatalog.Mask(BrowserKind.Brave), settings.Browsers, profile + " must not change the browser list");
            }
            // The safety values are constants, not settings: no profile path may reach them.
            Assert.AreEqual(1000, AppInfo.ScanTimeBudgetMs, "the scan time budget is unchanged");
            Assert.AreEqual(1500, AppInfo.ScanStaleMs, "the staleness window is unchanged");
            Assert.AreEqual(3000, AppInfo.CloseValidationTimeoutMs, "the close validation deadline is unchanged");
            Assert.AreEqual(800, AppInfo.CloseCooldownMs, "the close cooldown is unchanged");
        }

        private static int presetInterval(PerformanceProfile profile)
        {
            return Profiles.CadenceFor(profile).ScanIntervalMs;
        }

        [Test]
        public static void AnEditedPresetValueBecomesCustomInsteadOfBeingReset()
        {
            int fastInterval = Profiles.CadenceFor(PerformanceProfile.Fast).ScanIntervalMs;
            Assert.AreEqual(PerformanceProfile.Fast, Profiles.Resolve(PerformanceProfile.Fast, fastInterval),
                "a preset whose value is unchanged stays that preset");
            Assert.AreEqual(PerformanceProfile.Custom, Profiles.Resolve(PerformanceProfile.Fast, fastInterval + 100),
                "a value the user edited after choosing a preset must not be reported (or later reset) as that preset");
            Assert.AreEqual(PerformanceProfile.Custom, Profiles.Resolve(PerformanceProfile.Custom, AppInfo.ScanIntervalMs),
                "the custom marker stays custom whatever the value is");
            Assert.AreEqual(PerformanceProfile.Custom, Profiles.Resolve(PerformanceProfile.Balanced, 1),
                "an out-of-preset value is custom even for the balanced marker");

            AppSettings settings = AppSettings.Defaults();
            settings.Profile = PerformanceProfile.Custom;
            settings.ScanIntervalMs = 1234;
            AppSettings copy = settings.Copy();
            copy.Normalize();
            Assert.AreEqual(1234, copy.ScanIntervalMs, "a custom interval survives normalization");
            Assert.AreEqual(PerformanceProfile.Custom, copy.Profile, "a custom profile marker survives normalization");
            Assert.AreEqual(PerformanceProfile.Custom, Profiles.Resolve(copy.Profile, copy.ScanIntervalMs),
                "the reported profile stays custom across a restart");
        }

        [Test]
        public static void ProfileTokensRoundTripAndUnknownTokensFallBack()
        {
            foreach (PerformanceProfile profile in new PerformanceProfile[]
            {
                PerformanceProfile.Balanced, PerformanceProfile.Fast, PerformanceProfile.LowResource, PerformanceProfile.Custom
            })
            {
                Assert.AreEqual(profile, Profiles.ParseToken(Profiles.Token(profile), PerformanceProfile.Balanced),
                    profile + " token round trip");
            }
            Assert.AreEqual(PerformanceProfile.Balanced, Profiles.ParseToken("turbo", PerformanceProfile.Balanced),
                "an unknown token falls back instead of throwing");
            Assert.AreEqual(PerformanceProfile.Balanced, Profiles.ParseToken(null, PerformanceProfile.Balanced),
                "a missing token falls back");
            Assert.AreEqual(PerformanceProfile.Fast, Profiles.ParseToken("FAST", PerformanceProfile.Balanced),
                "tokens are case-insensitive");
            Assert.IsFalse(Profiles.IsPreset(PerformanceProfile.Custom), "custom is not a preset");
            Assert.IsTrue(Profiles.IsPreset(PerformanceProfile.Fast), "fast is a preset");

            // An unknown marker must never be treated as a preset that could overwrite the stored interval.
            AppSettings edited = AppSettings.Defaults();
            edited.ScanIntervalMs = 1777;
            Profiles.ApplyTo(PerformanceProfile.Custom, edited);
            Assert.AreEqual(1777, edited.ScanIntervalMs, "applying custom leaves the interval alone");
            Assert.AreEqual(PerformanceProfile.Custom, edited.Profile, "applying custom keeps the custom marker");
        }
    }
}
