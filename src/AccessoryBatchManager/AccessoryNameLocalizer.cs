using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AccessoryBatchManager {
    internal static class AccessoryNameLocalizer {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Type AutoTranslatorType = Type.GetType("XUnity.AutoTranslator.Plugin.Core.AutoTranslator, XUnity.AutoTranslator.Plugin.Core", false);
        private static readonly PropertyInfo DefaultTranslatorProperty = AutoTranslatorType == null ? null : AutoTranslatorType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo TryTranslateMethod = FindTryTranslateMethod();
        private static readonly Type ChineseTextType = Type.GetType("KK_Chinese.Core.Patch.Text_Display, KK_Chinese", false);
        private static readonly MethodInfo ChineseGetTextMethod = ChineseTextType == null ? null : ChineseTextType.GetMethod("GetText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);

        internal static string Localize(string value) {
            if (string.IsNullOrEmpty(value)) return value;
            string cached;
            if (Cache.TryGetValue(value, out cached)) return cached;
            string result = value;
            try {
                if (ChineseGetTextMethod != null) {
                    string chinese = ChineseGetTextMethod.Invoke(null, new object[] { value }) as string;
                    if (!string.IsNullOrEmpty(chinese) && !string.Equals(chinese, value, StringComparison.Ordinal)) {
                        Cache[value] = chinese;
                        return chinese;
                    }
                }
            }
            catch { }
            try {
                object translator = DefaultTranslatorProperty == null ? null : DefaultTranslatorProperty.GetValue(null, null);
                if (translator != null && TryTranslateMethod != null) {
                    object[] arguments = { value, null };
                    if ((bool)TryTranslateMethod.Invoke(translator, arguments) && arguments[1] is string && !string.IsNullOrEmpty((string)arguments[1]))
                        result = (string)arguments[1];
                }
            }
            catch { }
            Cache[value] = result;
            return result;
        }

        private static MethodInfo FindTryTranslateMethod() {
            if (DefaultTranslatorProperty == null) return null;
            return DefaultTranslatorProperty.PropertyType.GetMethods()
                .FirstOrDefault(delegate(MethodInfo method) {
                    ParameterInfo[] parameters = method.GetParameters();
                    return method.Name == "TryTranslate" && parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string).MakeByRefType();
                });
        }
    }
}
