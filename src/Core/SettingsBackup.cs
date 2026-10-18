using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>What an import did, in terms the window can show the user and a test can assert on.</summary>
    internal sealed class BackupImportResult
    {
        public bool Accepted;
        /// <summary>Schema version read from the file, or 0 when the file was rejected before one was read.</summary>
        public int Schema;
        /// <summary>Known keys the file supplied with an accepted value.</summary>
        public int AppliedCount;
        /// <summary>Keys whose value was outside its documented range and was clamped to the bound.</summary>
        public List<string> ClampedKeys = new List<string>();
        /// <summary>Keys whose value could not be parsed. The current value was kept; the file's value was not used.</summary>
        public List<string> RejectedKeys = new List<string>();
        /// <summary>Keys this version does not know. They are preserved exactly as the normal settings path preserves them.</summary>
        public List<string> PreservedKeys = new List<string>();
        /// <summary>
        /// Raw value for every unknown key the document carried, so an import can write back exactly what the file
        /// said instead of dropping it after reporting it as preserved.
        /// </summary>
        public Dictionary<string, string> PreservedValues = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Validated settings the file describes. Never applied by this class itself.</summary>
        public AppSettings Settings;
        /// <summary>Localized sentence explaining the outcome, or None.</summary>
        public StringId Message = StringId.None;
        /// <summary>English, bounded technical reason for the log. Never contains file content.</summary>
        public string Failure = string.Empty;
    }

    /// <summary>
    /// Export and import of the Xtension configuration. The format is the settings file itself plus a schema line, so
    /// restoring a backup needs no parser other than the one the application already uses, no dependency, and no
    /// serialization of anything except settings: no log, no tab title, no URL and no machine information.
    ///
    /// Import is validate-then-write: nothing is written until the whole document has been parsed and bounded, every
    /// value goes through the same ranges as a normal load, and a failed write restores the previous file content.
    /// </summary>
    internal static class SettingsBackup
    {
        public const string HeaderLine = "# Xtension settings backup";
        public const string SchemaKey = "schema";
        public const string ProductKey = "product";
        public const string VersionKey = "appVersion";
        public const string ProductValue = "Xtension";

        /// <summary>Serializes a settings instance as a backup document.</summary>
        public static string Export(AppSettings settings)
        {
            AppSettings copy = settings == null ? AppSettings.Defaults() : settings.Copy();
            copy.Normalize();
            StringBuilder text = new StringBuilder();
            text.Append(HeaderLine).Append("\r\n");
            text.Append(SchemaKey).Append('=').Append(AppInfo.SettingsBackupSchema).Append("\r\n");
            text.Append(ProductKey).Append('=').Append(ProductValue).Append("\r\n");
            text.Append(VersionKey).Append('=').Append(AppInfo.SemanticVersion).Append("\r\n");
            string[] values = copy.Values();
            for (int index = 0; index < AppSettings.KnownKeys.Length; index++)
            {
                text.Append(AppSettings.KnownKeys[index]).Append('=').Append(values[index]).Append("\r\n");
            }
            return text.ToString();
        }

        /// <summary>
        /// Parses and validates a backup document. Nothing is written here: the caller applies the outcome through the
        /// normal settings path, and can abandon it without touching the current configuration.
        /// </summary>
        public static BackupImportResult Parse(string text)
        {
            BackupImportResult outcome = new BackupImportResult();
            if (text == null || text.Trim().Length == 0)
            {
                outcome.Message = StringId.BackupImportEmpty;
                outcome.Failure = "empty backup document";
                return outcome;
            }
            if (text.Length > AppInfo.MaxSettingsBackupBytes)
            {
                outcome.Message = StringId.BackupImportTooLarge;
                outcome.Failure = "backup larger than " + AppInfo.MaxSettingsBackupBytes + " characters";
                return outcome;
            }

            Dictionary<string, string> values = ReadLines(text);
            string schemaText;
            if (!values.TryGetValue(SchemaKey, out schemaText))
            {
                outcome.Message = StringId.BackupImportNotABackup;
                outcome.Failure = "no schema key";
                return outcome;
            }
            int schema;
            if (!int.TryParse(schemaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out schema) || schema < 1)
            {
                outcome.Message = StringId.BackupImportNotABackup;
                outcome.Failure = "unreadable schema";
                return outcome;
            }
            if (schema > AppInfo.SettingsBackupSchema)
            {
                outcome.Schema = schema;
                outcome.Message = StringId.BackupImportUnsupportedSchema;
                outcome.Failure = "schema " + schema + " is newer than " + AppInfo.SettingsBackupSchema;
                return outcome;
            }
            string product;
            if (values.TryGetValue(ProductKey, out product)
                && !string.Equals(product, ProductValue, StringComparison.OrdinalIgnoreCase))
            {
                outcome.Message = StringId.BackupImportNotABackup;
                outcome.Failure = "product mismatch";
                return outcome;
            }

            // Start from the documented defaults and take only what the file really contains: a key the file omits
            // keeps its documented default, which is what "restore this backup" means.
            AppSettings settings = AppSettings.Defaults();
            int present = 0;
            foreach (string key in AppSettings.KnownKeys)
            {
                string raw;
                if (!values.TryGetValue(key, out raw)) continue;
                present++;
                ApplyKey(settings, key, raw, outcome);
            }
            settings.Normalize();
            // The marker never overrides the values: a preset name whose value is not the preset's value is custom.
            settings.Profile = Profiles.Resolve(settings.Profile, settings.ScanIntervalMs);
            outcome.AppliedCount = present - outcome.RejectedKeys.Count;

            foreach (KeyValuePair<string, string> entry in values)
            {
                if (Array.IndexOf(AppSettings.KnownKeys, entry.Key) >= 0) continue;
                if (IsMetadataKey(entry.Key)) continue;
                outcome.PreservedKeys.Add(entry.Key);
                outcome.PreservedValues[entry.Key] = entry.Value;
            }
            outcome.PreservedKeys.Sort(StringComparer.Ordinal);
            Deduplicate(outcome.ClampedKeys);
            outcome.ClampedKeys.Sort(StringComparer.Ordinal);
            outcome.RejectedKeys.Sort(StringComparer.Ordinal);

            outcome.Schema = schema;
            outcome.Settings = settings;
            outcome.Accepted = true;
            outcome.Message = StringId.BackupImportDone;
            return outcome;
        }

        private static Dictionary<string, string> ReadLines(string text)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                if (key.Length == 0 || key.Length > AppInfo.MaxSettingsKeyLength) continue;
                if (value.Length > AppInfo.MaxSettingsValueLength) continue;
                values[key] = value;
            }
            return values;
        }

        private static void ApplyKey(AppSettings settings, string key, string raw, BackupImportResult outcome)
        {
            if (key == AppSettings.OverlayEnabledKey)
            {
                bool value;
                if (!bool.TryParse(raw, out value)) { outcome.RejectedKeys.Add(key); return; }
                settings.OverlayEnabled = value;
                return;
            }
            if (key == AppSettings.ButtonBorderKey)
            {
                bool value;
                if (!bool.TryParse(raw, out value)) { outcome.RejectedKeys.Add(key); return; }
                settings.ButtonBorder = value;
                return;
            }
            if (key == AppSettings.VisibilityModeKey)
            {
                if (!IsToken(raw, AppSettings.VisibilityAlwaysToken, AppSettings.VisibilityHoverToken))
                {
                    outcome.RejectedKeys.Add(key);
                    return;
                }
                settings.VisibilityMode = AppSettings.ParseVisibility(raw);
                return;
            }
            if (key == AppSettings.LanguageKey)
            {
                if (!IsToken(raw, "vi", "en")) { outcome.RejectedKeys.Add(key); return; }
                settings.Language = Strings.ParseLanguage(raw, AppLanguage.Vietnamese);
                return;
            }
            if (key == AppSettings.ButtonColorKey)
            {
                if (!IsToken(raw, AppSettings.ColorDarkToken, AppSettings.ColorRedToken, AppSettings.ColorBlueToken,
                    AppSettings.ColorGreenToken))
                {
                    outcome.RejectedKeys.Add(key);
                    return;
                }
                settings.ButtonColor = AppSettings.ParseColor(raw);
                return;
            }
            if (key == AppSettings.ProfileKey)
            {
                if (!IsToken(raw, Profiles.BalancedToken, Profiles.FastToken, Profiles.LowResourceToken, Profiles.CustomToken))
                {
                    outcome.RejectedKeys.Add(key);
                    return;
                }
                settings.Profile = Profiles.ParseToken(raw, PerformanceProfile.Custom);
                return;
            }
            if (key == AppSettings.BrowsersKey)
            {
                int mask = BrowserCatalog.ParseMask(raw);
                if (mask == BrowserCatalog.NoBrowsers) { outcome.RejectedKeys.Add(key); return; }
                settings.Browsers = mask;
                return;
            }
            if (key == AppSettings.ButtonSizeKey)
            {
                settings.ButtonSizeLogical = Bounded(raw, key, AppInfo.DefaultButtonSizeLogical,
                    AppInfo.MinButtonSizeLogical, AppInfo.MaxButtonSizeLogical, outcome);
                return;
            }
            if (key == AppSettings.ButtonInsetKey)
            {
                settings.ButtonInsetLogical = Bounded(raw, key, AppInfo.DefaultButtonInsetLogical,
                    AppInfo.MinButtonInsetLogical, AppInfo.MaxButtonInsetLogical, outcome);
                return;
            }
            if (key == AppSettings.HoverGraceKey)
            {
                settings.HoverGraceMs = Bounded(raw, key, AppInfo.DefaultHoverGraceMs,
                    AppInfo.MinHoverGraceMs, AppInfo.MaxHoverGraceMs, outcome);
                return;
            }
            if (key == AppSettings.ScanIntervalKey)
            {
                settings.ScanIntervalMs = Bounded(raw, key, AppInfo.ScanIntervalMs,
                    AppInfo.MinScanIntervalMs, AppInfo.MaxScanIntervalMs, outcome);
                return;
            }
            if (key == AppSettings.ButtonOpacityKey)
            {
                settings.ButtonOpacityPercent = Bounded(raw, key, AppInfo.DefaultButtonOpacityPercent,
                    AppInfo.MinButtonOpacityPercent, AppInfo.MaxButtonOpacityPercent, outcome);
                return;
            }
        }

        /// <summary>A number inside its documented range. Out of range is clamped and reported; unparsable is rejected.</summary>
        private static int Bounded(string raw, string key, int fallback, int minimum, int maximum, BackupImportResult outcome)
        {
            int value;
            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                outcome.RejectedKeys.Add(key);
                return fallback;
            }
            if (value < minimum)
            {
                outcome.ClampedKeys.Add(key);
                return minimum;
            }
            if (value > maximum)
            {
                outcome.ClampedKeys.Add(key);
                return maximum;
            }
            return value;
        }

        private static bool IsToken(string raw, params string[] tokens)
        {
            string trimmed = raw == null ? string.Empty : raw.Trim();
            foreach (string token in tokens)
            {
                if (string.Equals(trimmed, token, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void Deduplicate(List<string> keys)
        {
            for (int index = keys.Count - 1; index > 0; index--)
            {
                if (keys.IndexOf(keys[index]) < index) keys.RemoveAt(index);
            }
        }

        private static bool IsMetadataKey(string key)
        {
            return string.Equals(key, SchemaKey, StringComparison.Ordinal)
                || string.Equals(key, ProductKey, StringComparison.Ordinal)
                || string.Equals(key, VersionKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes a validated import into the store, preserving the values of keys this version does not know.
        /// The store commits cache and file atomically; a failed write leaves both unchanged.
        /// </summary>
        public static bool ApplyToStore(SettingsStore store, BackupImportResult outcome, out string failure)
        {
            failure = string.Empty;
            if (store == null || outcome == null || !outcome.Accepted || outcome.Settings == null)
            {
                failure = "nothing to apply";
                return false;
            }

            try
            {
                List<string> keys = new List<string>();
                List<string> values = new List<string>();
                // Keys this version does not know keep their current value, unless the backup brings its own value,
                // in which case the backup's value wins (that is what "import this file" means).
                foreach (string key in store.Keys)
                {
                    if (Array.IndexOf(AppSettings.KnownKeys, key) >= 0) continue;
                    if (IsMetadataKey(key)) continue;
                    if (outcome.PreservedValues.ContainsKey(key)) continue;
                    keys.Add(key);
                    values.Add(store.GetRaw(key));
                }
                foreach (KeyValuePair<string, string> entry in outcome.PreservedValues)
                {
                    keys.Add(entry.Key);
                    values.Add(entry.Value);
                }
                string[] document = outcome.Settings.Values();
                for (int index = 0; index < AppSettings.KnownKeys.Length; index++)
                {
                    keys.Add(AppSettings.KnownKeys[index]);
                    values.Add(document[index]);
                }
                store.SetMany(keys.ToArray(), values.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                failure = "the backup could not be written: " + ex.GetType().Name;
                return false;
            }
        }

    }
}
