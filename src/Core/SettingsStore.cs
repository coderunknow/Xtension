using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xtension
{
    /// <summary>Tiny key=value settings file. Unknown lines are dropped on rewrite; failures fall back to defaults.</summary>
    internal sealed class SettingsStore
    {
        public const string OverlayEnabledKey = "overlayEnabled";

        private readonly string _path;

        public SettingsStore(string path)
        {
            _path = path;
        }

        public static SettingsStore Default
        {
            get { return new SettingsStore(AppPaths.SettingsFile); }
        }

        public bool GetBool(string key, bool fallback)
        {
            try
            {
                Dictionary<string, string> values = ReadAll();
                string text;
                bool parsed;
                if (values.TryGetValue(key, out text) && bool.TryParse(text, out parsed)) return parsed;
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("settings-read", "Settings could not be read; defaults are used: " + ex.Message);
            }
            return fallback;
        }

        public void SetBool(string key, bool value)
        {
            Dictionary<string, string> values = ReadAll();
            values[key] = value ? "true" : "false";
            WriteAll(values);
        }

        private Dictionary<string, string> ReadAll()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(_path)) return values;
            foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
            {
                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }
            return values;
        }

        private void WriteAll(Dictionary<string, string> values)
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> pair in values) lines.Add(pair.Key + "=" + pair.Value);
            // Write-then-move so a crash never leaves a half-written settings file.
            string temporary = _path + ".tmp";
            File.WriteAllLines(temporary, lines.ToArray(), Encoding.UTF8);
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
        }
    }
}
