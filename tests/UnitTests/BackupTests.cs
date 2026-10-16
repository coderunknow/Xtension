using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xtension.Tests
{
    /// <summary>
    /// Settings backup and restore. The file is a settings file, so it gets exactly the validation the settings file
    /// gets: bounded size, bounded values, unknown keys preserved, and nothing applied unless the whole document was
    /// understood. These tests also pin the privacy rule — a backup contains settings and nothing else.
    /// </summary>
    internal static class BackupTests
    {
        [Test]
        public static void ExportAndImportRoundTripEverySetting()
        {
            AppSettings source = AppSettings.Defaults();
            source.OverlayEnabled = false;
            source.VisibilityMode = OverlayVisibilityMode.HoverOnSelectedTab;
            source.ButtonSizeLogical = 34;
            source.ButtonInsetLogical = 7;
            source.HoverGraceMs = 500;
            source.ScanIntervalMs = 900;
            source.Language = AppLanguage.English;
            source.Profile = PerformanceProfile.Custom;
            source.ButtonColor = OverlayColor.Blue;
            source.ButtonOpacityPercent = 70;
            source.ButtonBorder = false;
            source.Browsers = BrowserCatalog.Mask(BrowserKind.Brave);

            string document = SettingsBackup.Export(source);
            BackupImportResult imported = SettingsBackup.Parse(document);
            Assert.IsTrue(imported.Accepted, "an exported document must import: " + imported.Failure);
            Assert.AreEqual(0, imported.RejectedKeys.Count, "a round trip rejects nothing");
            Assert.AreEqual(0, imported.ClampedKeys.Count, "a round trip clamps nothing");
            Assert.IsTrue(imported.Settings.Matches(source), "every value survives the round trip");

            string directory = TempDirectory();
            try
            {
                SettingsStore store = new SettingsStore(Path.Combine(directory, "settings.ini"));
                string failure;
                Assert.IsTrue(SettingsBackup.ApplyToStore(store, imported, out failure), "apply writes the settings file: " + failure);
                AppSettings reloaded = AppSettings.Load(store);
                Assert.IsTrue(reloaded.Matches(source), "the applied file reloads to the same configuration");
            }
            finally
            {
                Cleanup(directory);
            }
        }

        [Test]
        public static void PrivateDataNeverEntersABackup()
        {
            AppSettings settings = AppSettings.Defaults();
            string document = SettingsBackup.Export(settings);
            foreach (string line in document.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                int separator = trimmed.IndexOf('=');
                Assert.IsTrue(separator > 0, "every line is a key=value pair: " + trimmed);
                string key = trimmed.Substring(0, separator);
                Assert.IsTrue(Array.IndexOf(AppSettings.KnownKeys, key) >= 0 || key == SettingsBackup.SchemaKey
                    || key == SettingsBackup.ProductKey || key == SettingsBackup.VersionKey,
                    "a backup carries settings keys only, not '" + key + "'");
            }
            // A log line, a tab title or a path must never be part of the document.
            Assert.IsFalse(document.Contains("app.log"), "a backup must not name the log file");
            Assert.IsFalse(document.Contains("\\"), "a backup must not contain a file path");
        }

        [Test]
        public static void UnknownKeysArePreservedAndReported()
        {
            string directory = TempDirectory();
            try
            {
                SettingsStore store = new SettingsStore(Path.Combine(directory, "settings.ini"));
                store.SetString("somethingFromANewerBuild", "the-older-install-value");
                store.SetBool(AppSettings.OverlayEnabledKey, true);
                store.Save();

                // A backup from another build carries keys this version does not know. Its own value must survive the
                // import, and the key must be reported, because silently dropping it would lose another build's
                // configuration.
                string document = SettingsBackup.Export(AppSettings.Defaults())
                    + "somethingFromANewerBuild=value-from-the-backup\r\n"
                    + "anotherUnknownKey=second\r\n";
                BackupImportResult imported = SettingsBackup.Parse(document);
                Assert.IsTrue(imported.Accepted, "the fixture document imports");
                Assert.AreEqual(2, imported.PreservedKeys.Count, "every unknown key is reported: " + imported.PreservedKeys.Count);
                string failure;
                Assert.IsTrue(SettingsBackup.ApplyToStore(store, imported, out failure), "apply writes the settings file: " + failure);
                Assert.AreEqual("value-from-the-backup", store.GetString("somethingFromANewerBuild", string.Empty),
                    "an unknown key the backup carries takes the backup's value");
                Assert.AreEqual("second", store.GetString("anotherUnknownKey", string.Empty),
                    "an unknown key that only the backup knows is written, not dropped");

                // A store key that the backup does not mention keeps its current value.
                store.SetString("thirdUnknownKey", "keep-me");
                store.Save();
                BackupImportResult again = SettingsBackup.Parse(SettingsBackup.Export(AppSettings.Defaults()));
                Assert.IsTrue(SettingsBackup.ApplyToStore(store, again, out failure), "a second import writes the settings file");
                Assert.AreEqual("keep-me", store.GetString("thirdUnknownKey", string.Empty),
                    "a key the backup does not mention keeps its value");
            }
            finally
            {
                Cleanup(directory);
            }
        }

        [Test]
        public static void OutOfRangeValuesAreClampedAndReported()
        {
            string document = "schema=1\r\nproduct=Xtension\r\nappVersion=0.0.0\r\n"
                + AppSettings.ButtonSizeKey + "=999\r\n"
                + AppSettings.ButtonInsetKey + "=-4\r\n"
                + AppSettings.HoverGraceKey + "=999999\r\n"
                + AppSettings.ScanIntervalKey + "=1\r\n"
                + AppSettings.ButtonOpacityKey + "=1\r\n";
            BackupImportResult imported = SettingsBackup.Parse(document);
            Assert.IsTrue(imported.Accepted, "a document with hostile values is still a valid document: " + imported.Failure);
            AppSettings settings = imported.Settings;
            Assert.IsTrue(settings.ButtonSizeLogical >= AppInfo.MinButtonSizeLogical && settings.ButtonSizeLogical <= AppInfo.MaxButtonSizeLogical,
                "size clamped into range: " + settings.ButtonSizeLogical);
            Assert.IsTrue(settings.ButtonInsetLogical >= AppInfo.MinButtonInsetLogical && settings.ButtonInsetLogical <= AppInfo.MaxButtonInsetLogical,
                "inset clamped into range: " + settings.ButtonInsetLogical);
            Assert.IsTrue(settings.HoverGraceMs >= AppInfo.MinHoverGraceMs && settings.HoverGraceMs <= AppInfo.MaxHoverGraceMs,
                "grace clamped into range: " + settings.HoverGraceMs);
            Assert.IsTrue(settings.ScanIntervalMs >= AppInfo.MinScanIntervalMs && settings.ScanIntervalMs <= AppInfo.MaxScanIntervalMs,
                "interval clamped into range: " + settings.ScanIntervalMs);
            Assert.IsTrue(settings.ButtonOpacityPercent >= AppInfo.MinButtonOpacityPercent
                && settings.ButtonOpacityPercent <= AppInfo.MaxButtonOpacityPercent,
                "opacity clamped into range: " + settings.ButtonOpacityPercent);
            Assert.IsTrue(imported.ClampedKeys.Count >= 4, "every clamped key is reported: " + imported.ClampedKeys.Count);
            Assert.AreEqual(0, imported.RejectedKeys.Count, "clamping is not rejection");
        }

        [Test]
        public static void UnreadableValuesAreRejectedAndKeepTheCurrentSetting()
        {
            string document = "schema=1\r\nproduct=Xtension\r\nappVersion=0.0.0\r\n"
                + AppSettings.ButtonSizeKey + "=not-a-number\r\n"
                + AppSettings.BrowsersKey + "=chrome\r\n";
            BackupImportResult imported = SettingsBackup.Parse(document);
            Assert.IsTrue(imported.Accepted, "one bad value does not discard the whole document: " + imported.Failure);
            Assert.AreEqual(2, imported.RejectedKeys.Count, "both unusable keys are reported: " + imported.RejectedKeys.Count);
            Assert.IsTrue(imported.RejectedKeys.Contains(AppSettings.ButtonSizeKey), "the unusable size is reported by name");
            Assert.IsTrue(imported.RejectedKeys.Contains(AppSettings.BrowsersKey), "the unusable browser list is reported by name");
            // The report is ordered, so the same document always produces the same list for the user to read.
            for (int index = 1; index < imported.RejectedKeys.Count; index++)
            {
                Assert.IsTrue(string.CompareOrdinal(imported.RejectedKeys[index - 1], imported.RejectedKeys[index]) < 0,
                    "the rejected names are reported in a stable order: " + imported.RejectedKeys[index - 1]
                    + " then " + imported.RejectedKeys[index]);
            }
            // A rejected value falls back to the documented default, and the resulting browser list may only contain
            // browsers this build can confirm.
            Assert.AreEqual("brave", BrowserCatalog.MaskTokenList(imported.Settings.Browsers),
                "a rejected browser list falls back to documented defaults, never to an unverified browser");
            Assert.IsTrue(imported.Settings.ButtonSizeLogical >= AppInfo.MinButtonSizeLogical
                && imported.Settings.ButtonSizeLogical <= AppInfo.MaxButtonSizeLogical,
                "a rejected size is replaced by a safe value: " + imported.Settings.ButtonSizeLogical);

            // A key the document omits keeps its documented default rather than the current file value.
            BackupImportResult partial = SettingsBackup.Parse("schema=1\r\nproduct=Xtension\r\n" + AppSettings.OverlayEnabledKey + "=false\r\n");
            Assert.IsTrue(partial.Accepted, "a partial document is still a valid document");
            Assert.IsFalse(partial.Settings.OverlayEnabled, "a present key is applied");
            Assert.AreEqual(AppInfo.DefaultButtonSizeLogical, partial.Settings.ButtonSizeLogical,
                "a key the document omits keeps its documented default");
        }

        [Test]
        public static void InvalidDocumentsAreRefusedWithAReason()
        {
            AssertRefused(string.Empty, StringId.BackupImportEmpty, "an empty document");
            AssertRefused("   \r\n\r\n", StringId.BackupImportEmpty, "a whitespace document");
            AssertRefused(new string('x', AppInfo.MaxSettingsBackupBytes + 1), StringId.BackupImportTooLarge, "an oversized document");
            AssertRefused("overlayEnabled=true\r\n", StringId.BackupImportNotABackup, "a document without a schema");
            AssertRefused("schema=not-a-number\r\nproduct=Xtension\r\n", StringId.BackupImportNotABackup, "an unreadable schema");
            AssertRefused("schema=1\r\nproduct=SomeOtherApp\r\n", StringId.BackupImportNotABackup, "another product's file");
            AssertRefused("schema=" + (AppInfo.SettingsBackupSchema + 1) + "\r\nproduct=Xtension\r\n",
                StringId.BackupImportUnsupportedSchema, "a newer schema");
            AssertRefused(null, StringId.BackupImportEmpty, "a missing document");

            // Refusing must never hand back settings that could be applied by a careless caller.
            BackupImportResult refused = SettingsBackup.Parse("overlayEnabled=true\r\n");
            Assert.IsFalse(refused.Accepted, "the refused document is not accepted");
            Assert.IsTrue(refused.Settings == null, "a refused import carries no settings to apply");
            Assert.IsTrue(!string.IsNullOrEmpty(refused.Failure), "a refused import explains itself in the log");

            string failure;
            Assert.IsFalse(SettingsBackup.ApplyToStore(null, refused, out failure), "a refused import is never written");
            Assert.IsTrue(!string.IsNullOrEmpty(failure), "the write refusal explains itself");
        }

        private static void AssertRefused(string document, StringId expected, string because)
        {
            BackupImportResult imported = SettingsBackup.Parse(document);
            Assert.IsFalse(imported.Accepted, because + " must be refused");
            Assert.AreEqual(expected, imported.Message, because + " must be refused with its own sentence");
        }

        private static string TempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "xtension-dev4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void Cleanup(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
