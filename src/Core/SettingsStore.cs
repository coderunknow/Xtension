using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>
    /// Tiny key=value settings file. Values are parsed once and cached in memory, so a render, a hover tick or a
    /// scan never re-reads the file. Unknown keys read from the file are preserved when the file is rewritten,
    /// a rewrite is atomic, and a failed replacement cannot leave a stray temporary file behind.
    ///
    /// Failure policy: a read error falls back to defaults and is logged once per throttle window; a write error
    /// is reported to the caller so the UI can say that the value applies only until Xtension restarts.
    /// </summary>
    internal sealed class SettingsStore
    {
        /// <summary>Key that keeps the overlay enabled flag. Kept for existing callers and stored files.</summary>
        public const string OverlayEnabledKey = "overlayEnabled";

        private readonly object _gate = new object();
        private readonly string _path;
        private Dictionary<string, string> _values;
        private bool _loaded;

        public SettingsStore(string path)
        {
            _path = path;
        }

        public static SettingsStore Default
        {
            get { return new SettingsStore(AppPaths.SettingsFile); }
        }

        public string FilePath
        {
            get { return _path; }
        }

        /// <summary>Number of stored entries, including preserved unknown keys.</summary>
        public int Count
        {
            get
            {
                lock (_gate)
                {
                    EnsureLoadedLocked();
                    return _values.Count;
                }
            }
        }

        /// <summary>Snapshot of every key currently stored, including preserved unknown keys.</summary>
        public string[] Keys
        {
            get
            {
                lock (_gate)
                {
                    EnsureLoadedLocked();
                    List<string> keys = new List<string>(_values.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    return keys.ToArray();
                }
            }
        }

        /// <summary>
        /// Raw stored value for a key, or an empty string when it is absent. Used by the backup importer, which must
        /// reproduce a key it does not understand byte for byte.
        /// </summary>
        public string GetRaw(string key)
        {
            string value = GetString(key, null);
            return value == null ? string.Empty : value;
        }

        public string GetString(string key, string fallback)
        {
            try
            {
                lock (_gate)
                {
                    EnsureLoadedLocked();
                    string value;
                    if (_values.TryGetValue(key, out value)) return value;
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("settings-read", "Settings could not be read; defaults are used: " + ex.GetType().Name);
            }
            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            string text = GetString(key, null);
            if (text == null) return fallback;
            bool parsed;
            return bool.TryParse(text.Trim(), out parsed) ? parsed : fallback;
        }

        /// <summary>
        /// Integer value clamped into [min, max]. A missing, malformed or out-of-range stored value falls back to
        /// the supplied default, so a hand-edited file can never produce a nonsensical setting.
        /// </summary>
        public int GetInt(string key, int fallback, int min, int max)
        {
            if (min > max) throw new ArgumentException("The minimum must not exceed the maximum.", "min");
            string text = GetString(key, null);
            if (text == null) return fallback;
            int parsed;
            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return fallback;
            if (parsed < min) return min;
            if (parsed > max) return max;
            return parsed;
        }

        public void SetBool(string key, bool value)
        {
            SetString(key, value ? "true" : "false");
        }

        public void SetInt(string key, int value)
        {
            SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Stores a value and writes the file immediately. Throws when the file cannot be written.</summary>
        public void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A settings key is required.", "key");
            lock (_gate)
            {
                EnsureLoadedLocked();
                _values[key] = value == null ? string.Empty : value;
                WriteAllLocked(_values);
            }
        }

        /// <summary>Stores several values and writes the file once. Throws when the file cannot be written.</summary>
        public void SetMany(string[] keys, string[] values)
        {
            if (keys == null || values == null || keys.Length != values.Length)
                throw new ArgumentException("Keys and values must have the same length.", "keys");
            lock (_gate)
            {
                EnsureLoadedLocked();
                for (int index = 0; index < keys.Length; index++)
                {
                    if (string.IsNullOrEmpty(keys[index])) throw new ArgumentException("A settings key is required.", "keys");
                    _values[keys[index]] = values[index] == null ? string.Empty : values[index];
                }
                WriteAllLocked(_values);
            }
        }

        /// <summary>Writes the cached values, including any unknown keys read from the file, atomically.</summary>
        public void Save()
        {
            lock (_gate)
            {
                EnsureLoadedLocked();
                WriteAllLocked(_values);
            }
        }

        /// <summary>Drops the cache so the next read comes from disk again. Used by tests and by recovery paths.</summary>
        public void Reload()
        {
            lock (_gate)
            {
                _loaded = false;
                EnsureLoadedLocked();
            }
        }

        private void EnsureLoadedLocked()
        {
            if (_loaded) return;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(_path))
            {
                FileInfo info = new FileInfo(_path);
                if (info.Length > AppInfo.MaxSettingsFileBytes)
                {
                    // Never load an unbounded file into memory. The file is left untouched on disk.
                    AppLog.WarnThrottled("settings-size", "The settings file is larger than the supported " +
                        AppInfo.MaxSettingsFileBytes + " bytes and was ignored; defaults are used.");
                }
                else
                {
                    foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                    {
                        int equals = line.IndexOf('=');
                        if (equals <= 0) continue;
                        string key = line.Substring(0, equals).Trim();
                        string value = line.Substring(equals + 1).Trim();
                        if (key.Length == 0 || key.Length > AppInfo.MaxSettingsKeyLength) continue;
                        if (value.Length > AppInfo.MaxSettingsValueLength) continue;
                        values[key] = value;
                    }
                }
            }
            _values = values;
            _loaded = true;
        }

        private void WriteAllLocked(Dictionary<string, string> values)
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            List<string> keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            StringBuilder text = new StringBuilder();
            foreach (string key in keys) text.Append(key).Append('=').Append(values[key]).Append("\r\n");

            string temporary = _path + ".tmp";
            try
            {
                // Write-then-move so a crash never leaves a half-written settings file.
                File.WriteAllText(temporary, text.ToString(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
            }
            catch
            {
                // A replacement that failed must not leave settings.ini.tmp behind for the next start.
                TryDelete(temporary);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("settings-tmp", "A temporary settings file could not be removed: " + ex.GetType().Name);
            }
        }
    }
}
