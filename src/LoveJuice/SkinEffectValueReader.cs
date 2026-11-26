using System;
using System.Globalization;
using System.Reflection;

namespace LoveJuice {
    // The installed 2023 KK_SkinEffects.dll exposes public *Level properties,
    // not SkinEffectKind/GetEffectLevel. Map the newer logical names used by
    // JuiceControl to the actual legacy controller properties without writes.
    internal static class SkinEffectValueReader {
        internal static string LegacyProperty(string name) {
            if (string.IsNullOrEmpty(name)) return null;
            switch (name.ToLowerInvariant()) {
                case "droolface": case "droollevel": return "DroolLevel";
                case "salivaface": case "salivalevel": return "SalivaLevel";
                case "wetface": case "wetbody": case "sweatlevel": return "SweatLevel";
                case "pussyjuicebody": case "pussyjuicelevel": return "PussyJuiceLevel";
                case "tearface": case "tearlevel": return "TearLevel";
                case "blushface": case "blushbody": case "blushlevel": return "BlushLevel";
                case "pussybukkakebody": case "bukkakelevel": return "BukkakeLevel";
                case "analbukkakebody": case "analbukkakelevel": return "AnalBukkakeLevel";
                case "virginbloodbody": case "bloodlevel": return "BloodLevel";
                case "cuminnoseface": case "cuminnoselevel": return "CumInNoseLevel";
                case "buttblushbody": case "buttlevel": return "ButtLevel";
                default: return null;
            }
        }

        internal static bool TryReadLegacy(object controller, string name, out int level) {
            level = -1;
            if (controller == null) return false;
            string propertyName = LegacyProperty(name);
            if (propertyName == null) return false;
            try {
                PropertyInfo property = controller.GetType().GetProperty(
                    propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead) return false;
                object raw = property.GetValue(controller, null);
                if (raw == null || raw is bool) return false;
                int observed = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                if (observed < 0 || observed > 255) return false;
                level = observed;
                return true;
            } catch { return false; }
        }
    }
}
