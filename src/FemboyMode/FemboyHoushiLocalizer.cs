using System;
using System.Collections.Generic;
using System.Reflection;

namespace FemboyMode
{
    /// <summary>
    /// Match MultiTeamH's translation order: KK_Chinese first, then
    /// XUnity AutoTranslator. Keep the English internal action marker stable;
    /// only the native motion-row label is translated.
    /// </summary>
    internal static class FemboyHoushiLocalizer
    {
        private static readonly Dictionary<string, string> Cache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Type ChineseType = Type.GetType(
            "KK_Chinese.Core.Patch.Text_Display, KK_Chinese", false);
        private static readonly MethodInfo ChineseGetText = ChineseType == null
            ? null : ChineseType.GetMethod("GetText", BindingFlags.Public |
                BindingFlags.Static, null, new[] { typeof(string) }, null);
        private static readonly Type TranslatorType = Type.GetType(
            "XUnity.AutoTranslator.Plugin.Core.AutoTranslator, XUnity.AutoTranslator.Plugin.Core", false);
        private static readonly PropertyInfo TranslatorDefault = TranslatorType == null
            ? null : TranslatorType.GetProperty("Default",
                BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo TranslateMethod = FindTranslatorMethod();

        internal static string LocalizeReversed(string original)
        {
            if (!FemboyRules.IsReversedHoushiName(original)) return original;
            string actionName = original.StartsWith("[反转] ", StringComparison.Ordinal)
                ? original.Substring("[反转] ".Length)
                : original.Substring("[Reversed] ".Length);
            string localized = Localize(actionName);
            bool chinese = ChineseType != null || ContainsChinese(localized);
            return (chinese ? "[反转] " : "[Reversed] ") + localized;
        }

        private static bool ContainsChinese(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if (c >= '\u3400' && c <= '\u9fff') return true;
            return false;
        }

        private static string Localize(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            string cached;
            if (Cache.TryGetValue(source, out cached)) return cached;
            try
            {
                if (ChineseGetText != null)
                {
                    string translation = ChineseGetText.Invoke(null,
                        new object[] { source }) as string;
                    if (!string.IsNullOrEmpty(translation) && translation != source)
                        return Cache[source] = translation;
                }
            }
            catch (Exception) { }
            try
            {
                object translator = TranslatorDefault == null ? null :
                    TranslatorDefault.GetValue(null, null);
                if (translator != null && TranslateMethod != null)
                {
                    object[] args = { source, null };
                    if ((bool)TranslateMethod.Invoke(translator, args) &&
                        args[1] is string && !string.IsNullOrEmpty((string)args[1]))
                        return Cache[source] = (string)args[1];
                }
            }
            catch (Exception) { }
            return Cache[source] = source;
        }

        private static MethodInfo FindTranslatorMethod()
        {
            if (TranslatorDefault == null) return null;
            foreach (MethodInfo method in TranslatorDefault.PropertyType.GetMethods())
            {
                ParameterInfo[] args = method.GetParameters();
                if (method.Name == "TryTranslate" && args.Length == 2 &&
                    args[0].ParameterType == typeof(string) &&
                    args[1].ParameterType == typeof(string).MakeByRefType())
                    return method;
            }
            return null;
        }
    }
}