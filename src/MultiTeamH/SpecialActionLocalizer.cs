using System;
using System.Collections.Generic;
using System.Reflection;

namespace MultiTeamH {
    internal static class SpecialActionLocalizer {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Type ChineseTextType = Type.GetType("KK_Chinese.Core.Patch.Text_Display, KK_Chinese", false);
        private static readonly MethodInfo ChineseGetTextMethod = ChineseTextType == null ? null :
            ChineseTextType.GetMethod("GetText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        private static readonly Type AutoTranslatorType = Type.GetType(
            "XUnity.AutoTranslator.Plugin.Core.AutoTranslator, XUnity.AutoTranslator.Plugin.Core", false);
        private static readonly PropertyInfo DefaultTranslatorProperty = AutoTranslatorType == null ? null :
            AutoTranslatorType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo TryTranslateMethod = FindTryTranslateMethod();

        internal static string Localize(string value) {
            if (string.IsNullOrEmpty(value)) return value;
            string cached;
            if (Cache.TryGetValue(value, out cached)) return cached;
            try {
                if (ChineseGetTextMethod != null) {
                    string translated = ChineseGetTextMethod.Invoke(null, new object[] { value }) as string;
                    if (!string.IsNullOrEmpty(translated) && translated != value) return Cache[value] = translated;
                }
            } catch { }
            try {
                object translator = DefaultTranslatorProperty == null ? null : DefaultTranslatorProperty.GetValue(null, null);
                if (translator != null && TryTranslateMethod != null) {
                    object[] args = { value, null };
                    if ((bool)TryTranslateMethod.Invoke(translator, args) && args[1] is string && !string.IsNullOrEmpty((string)args[1]))
                        return Cache[value] = (string)args[1];
                }
            } catch { }
            return Cache[value] = value;
        }

        private static MethodInfo FindTryTranslateMethod() {
            if (DefaultTranslatorProperty == null) return null;
            MethodInfo[] methods = DefaultTranslatorProperty.PropertyType.GetMethods();
            for (int i = 0; i < methods.Length; i++) {
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (methods[i].Name == "TryTranslate" && parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(string).MakeByRefType()) return methods[i];
            }
            return null;
        }
    }
}
