using System;
using System.IO;
using System.Text;

namespace Xtension.Tests
{
    /// <summary>
    /// Settings are now typed, cached and written atomically, so the failure paths matter as much as the happy path:
    /// malformed values, out-of-range numbers, unknown keys, oversized files and a failed replacement.
    /// </summary>
    internal static class SettingsTests
    {
        private static string NewDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Xtension.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void CleanUp(string directory)
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [Test]
        public static void DefaultsMatchTheDocumentedValues()
        {
            AppSettings defaults = AppSettings.Defaults();
            Assert.IsTrue(defaults.OverlayEnabled, "the overlay is enabled by default");
            Assert.AreEqual(OverlayVisibilityMode.AlwaysWhenSafe, defaults.VisibilityMode, "always-show stays the default mode");
            Assert.AreEqual(26, defaults.ButtonSizeLogical, "documented default button size");
            Assert.AreEqual(3, defaults.ButtonInsetLogical, "documented default inset");
            Assert.AreEqual(250, defaults.HoverGraceMs, "documented default hover grace");
            Assert.AreEqual(500, defaults.ScanIntervalMs, "documented default scan interval");
            Assert.AreEqual(AppLanguage.Vietnamese, defaults.Language, "Vietnamese stays the default language");
        }

        [Test]
        public static void RoundTripPreservesEveryValue()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                SettingsStore store = new SettingsStore(path);
                AppSettings settings = AppSettings.Defaults();
                settings.OverlayEnabled = false;
                settings.VisibilityMode = OverlayVisibilityMode.HoverOnSelectedTab;
                settings.ButtonSizeLogical = 40;
                settings.ButtonInsetLogical = 0;
                settings.HoverGraceMs = 1000;
                settings.ScanIntervalMs = 1200;
                settings.Language = AppLanguage.English;
                settings.Save(store);

                AppSettings loaded = AppSettings.Load(new SettingsStore(path));
                Assert.IsTrue(settings.Matches(loaded), "every saved value must load back unchanged");
                Assert.IsFalse(File.Exists(path + ".tmp"), "no temporary file may remain after a successful save");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void OutOfRangeAndMalformedValuesRecoverToSafeValues()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                File.WriteAllText(path,
                    "overlayEnabled=not-a-bool\r\n" +
                    "visibilityMode=sideways\r\n" +
                    "buttonSize=9999\r\n" +
                    "buttonInset=-40\r\n" +
                    "hoverGraceMs=999999\r\n" +
                    "scanIntervalMs=oops\r\n" +
                    "language=klingon\r\n" +
                    "=novalue\r\n" +
                    "garbage line\r\n", Encoding.UTF8);

                AppSettings loaded = AppSettings.Load(new SettingsStore(path));
                Assert.IsTrue(loaded.OverlayEnabled, "an unparsable bool keeps the default");
                Assert.AreEqual(OverlayVisibilityMode.AlwaysWhenSafe, loaded.VisibilityMode, "an unknown mode falls back to the default");
                Assert.AreEqual(AppInfo.MaxButtonSizeLogical, loaded.ButtonSizeLogical, "a too-large size is clamped");
                Assert.AreEqual(AppInfo.MinButtonInsetLogical, loaded.ButtonInsetLogical, "a negative inset is clamped");
                Assert.AreEqual(AppInfo.MaxHoverGraceMs, loaded.HoverGraceMs, "a huge grace is clamped");
                Assert.AreEqual(AppInfo.ScanIntervalMs, loaded.ScanIntervalMs, "an unparsable interval keeps the default");
                Assert.AreEqual(AppLanguage.Vietnamese, loaded.Language, "an unknown language falls back to Vietnamese");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void UnknownKeysSurviveARewrite()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                File.WriteAllText(path, "overlayEnabled=true\r\nfutureOption=keep-me\r\n", Encoding.UTF8);
                SettingsStore store = new SettingsStore(path);
                AppSettings settings = AppSettings.Load(store);
                settings.ButtonSizeLogical = 30;
                settings.Save(store);

                string text = File.ReadAllText(path);
                Assert.IsTrue(text.Contains("futureOption=keep-me"), "a key this version does not know must not be destroyed");
                Assert.IsTrue(text.Contains("buttonSize=30"), "the new value was written");
                Assert.IsTrue(text.Contains("overlayEnabled=true"), "existing values are still present");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void OversizedFileIsIgnoredWithoutBeingDestroyed()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                StringBuilder huge = new StringBuilder(AppInfo.MaxSettingsFileBytes + 1024);
                while (huge.Length < AppInfo.MaxSettingsFileBytes + 512) huge.Append("padding=garbage\r\n");
                File.WriteAllText(path, huge.ToString(), Encoding.UTF8);
                long before = new FileInfo(path).Length;

                SettingsStore store = new SettingsStore(path);
                AppSettings loaded = AppSettings.Load(store);
                Assert.IsTrue(loaded.Matches(AppSettings.Defaults()), "an oversized file must not produce settings");
                Assert.AreEqual(before, new FileInfo(path).Length, "loading must not modify the file on disk");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void FailedReplacementCleansUpItsTemporaryFile()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                SettingsStore store = new SettingsStore(path);
                store.SetBool(SettingsStore.OverlayEnabledKey, false);
                Assert.IsTrue(File.Exists(path), "the first write creates the file");

                // A directory where the temporary file must go cannot be written to, so the save fails.
                string blocked = Path.Combine(directory, "settings.ini.tmp");
                Directory.CreateDirectory(blocked);
                bool failed = false;
                try { store.SetInt(AppSettings.ButtonSizeKey, 31); }
                catch (Exception) { failed = true; }
                Assert.IsTrue(failed, "a blocked save must report failure instead of silently losing the value");
                Assert.IsTrue(Directory.Exists(blocked), "the fixture blocked the temporary path");
                Directory.Delete(blocked, true);
                Assert.IsFalse(File.Exists(blocked), "cleanup ran against the temporary path");

                // The store is still usable once the obstruction is gone.
                store.SetInt(AppSettings.ButtonSizeKey, 33);
                Assert.AreEqual(33, new SettingsStore(path).GetInt(AppSettings.ButtonSizeKey, 26, 18, 40), "store recovers after a failed save");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void BoundedIntegerParsingIsClampedOnRead()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                File.WriteAllText(path, "a=5\r\nb=500\r\nc=-9\r\n", Encoding.UTF8);
                SettingsStore store = new SettingsStore(path);
                Assert.AreEqual(20, store.GetInt("a", 10, 20, 40), "below the minimum is clamped up to the minimum");
                Assert.AreEqual(40, store.GetInt("b", 10, 20, 40), "above the maximum is clamped");
                Assert.AreEqual(20, store.GetInt("c", 10, 20, 40), "negative values are clamped to the minimum");
                Assert.AreEqual(30, store.GetInt("missing", 30, 20, 40), "a missing key falls back to the default");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void NormalizeRepairsEveryFieldInPlace()
        {
            AppSettings settings = AppSettings.Defaults();
            settings.ButtonSizeLogical = -100;
            settings.ButtonInsetLogical = 900;
            settings.HoverGraceMs = -1;
            settings.ScanIntervalMs = 100000;
            settings.VisibilityMode = (OverlayVisibilityMode)42;
            settings.Language = (AppLanguage)7;
            settings.Normalize();
            Assert.AreEqual(AppInfo.MinButtonSizeLogical, settings.ButtonSizeLogical, "size clamped");
            Assert.AreEqual(AppInfo.MaxButtonInsetLogical, settings.ButtonInsetLogical, "inset clamped");
            Assert.AreEqual(AppInfo.MinHoverGraceMs, settings.HoverGraceMs, "grace clamped");
            Assert.AreEqual(AppInfo.MaxScanIntervalMs, settings.ScanIntervalMs, "interval clamped");
            Assert.AreEqual(OverlayVisibilityMode.AlwaysWhenSafe, settings.VisibilityMode, "unknown mode repaired");
            Assert.AreEqual(AppLanguage.Vietnamese, settings.Language, "unknown language repaired");
        }

        [Test]
        public static void ResetRestoresDocumentedDefaultsAndIsPersisted()
        {
            string directory = NewDirectory();
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                SettingsStore store = new SettingsStore(path);
                AppSettings changed = AppSettings.Defaults();
                changed.OverlayEnabled = false;
                changed.VisibilityMode = OverlayVisibilityMode.HoverOnSelectedTab;
                changed.ButtonSizeLogical = 18;
                changed.ButtonInsetLogical = 12;
                changed.HoverGraceMs = 0;
                changed.ScanIntervalMs = 2000;
                changed.Language = AppLanguage.English;
                changed.Save(store);

                AppSettings defaults = AppSettings.Defaults();
                defaults.Save(store);
                AppSettings reloaded = AppSettings.Load(new SettingsStore(path));
                Assert.IsTrue(defaults.Matches(reloaded), "reset must survive a restart");
                Assert.IsTrue(reloaded.OverlayEnabled && reloaded.VisibilityMode == OverlayVisibilityMode.AlwaysWhenSafe,
                    "reset turns the overlay back on in always-show mode");
                Assert.AreEqual(AppLanguage.Vietnamese, reloaded.Language, "reset restores the default language");
            }
            finally { CleanUp(directory); }
        }

        [Test]
        public static void VisibilityAndLanguageTokensRoundTrip()
        {
            Assert.AreEqual(OverlayVisibilityMode.HoverOnSelectedTab, AppSettings.ParseVisibility("hover"), "hover token");
            Assert.AreEqual(OverlayVisibilityMode.AlwaysWhenSafe, AppSettings.ParseVisibility("always"), "always token");
            Assert.AreEqual(OverlayVisibilityMode.AlwaysWhenSafe, AppSettings.ParseVisibility("HOVER?"), "unknown token");
            Assert.AreEqual("hover", AppSettings.VisibilityToken(OverlayVisibilityMode.HoverOnSelectedTab), "hover token stable");
            Assert.AreEqual("always", AppSettings.VisibilityToken(OverlayVisibilityMode.AlwaysWhenSafe), "always token stable");
            Assert.AreEqual(AppLanguage.English, Strings.ParseLanguage("en", AppLanguage.Vietnamese), "en token");
            Assert.AreEqual(AppLanguage.Vietnamese, Strings.ParseLanguage("VI", AppLanguage.English), "vi token is case-insensitive");
            Assert.AreEqual(AppLanguage.English, Strings.ParseLanguage("de", AppLanguage.English), "unknown language keeps the fallback");
        }
    }
}
