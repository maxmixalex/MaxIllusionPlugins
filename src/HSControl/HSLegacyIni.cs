using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HSControl
{
    // Imports the original KK_HCtrl.ini as defaults only. Existing BepInEx
    // settings always take precedence; the legacy file is never modified.
    internal static class HSLegacyIni
    {
        internal static HSControlSettings Read(string path) {
            HSControlSettings settings = new HSControlSettings();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return settings;
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inConfig = false;
            foreach (string raw in File.ReadAllLines(path)) {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[') {
                    inConfig = string.Equals(line, "[Config]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inConfig) continue;
                int equals = line.IndexOf('=');
                if (equals < 1) continue;
                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                int comment = value.IndexOf(';');
                if (comment >= 0) value = value.Substring(0, comment).Trim();
                values[key] = value;
            }
            settings.AutoStrength = Boolean(values, "autoSW", settings.AutoStrength);
            settings.AutoMaleFinish = Boolean(values, "autoMaleOrga", settings.AutoMaleFinish);
            settings.AutoFemaleFinish = Boolean(values, "autoFemaleOrga", settings.AutoFemaleFinish);
            settings.AutoReplay = Boolean(values, "autoReplay", settings.AutoReplay);
            settings.Mode = Integer(values, "autoSpeedMode", settings.Mode);
            settings.Band = Integer(values, "autoSpeedMode_Half", settings.Band);
            settings.Linkage = Integer(values, "autoSpeedMode_LinkageType", settings.Linkage);
            settings.RangeMin = Number(values, "autoSpeedMode_RangeMin", settings.RangeMin);
            settings.RangeMax = Number(values, "autoSpeedMode_RangeMax", settings.RangeMax);
            settings.StrengthMinSeconds = Number(values, "autoSWTimerMin", settings.StrengthMinSeconds);
            settings.StrengthMaxSeconds = Number(values, "autoSWTimerMax", settings.StrengthMaxSeconds);
            settings.Sanitize();
            return settings;
        }

        private static bool Boolean(Dictionary<string, string> values, string key, bool fallback) {
            string v;
            bool result;
            return values.TryGetValue(key, out v) && bool.TryParse(v, out result) ? result : fallback;
        }

        private static int Integer(Dictionary<string, string> values, string key, int fallback) {
            string v;
            int result;
            return values.TryGetValue(key, out v) &&
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }

        private static float Number(Dictionary<string, string> values, string key, float fallback) {
            string v;
            float result;
            return values.TryGetValue(key, out v) &&
                float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
        }
    }
}
