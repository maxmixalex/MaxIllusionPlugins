using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace JuiceControl {
    internal static class SkinEffectsBridge {
        private const string PluginGuid = "Marco.SkinEffects";
        private const string ControllerTypeName = "KK_SkinEffects.SkinEffectsController";
        private const string EffectKindTypeName = "KK_SkinEffects.SkinEffectKind";
        private const string TextureLoaderTypeName = "KK_SkinEffects.TextureLoader";
        private const string EffectIdPrefix = "skineffects:";

        private static Type controllerType;
        private static Type effectKindType;
        private static Type textureLoaderType;
        private static MethodInfo getEffectLevelMethod;
        private static MethodInfo setEffectLevelMethod;
        private static MethodInfo getTextureCountMethod;
        private static bool initialized;

        public static bool IsAvailable { get; private set; }
        public static string StatusLabel { get; private set; } = "未安装";
        public static string SourcePluginName { get { return "SkinEffects"; } }
        public static string DisplayPrefix { get { return "[Ex]"; } }
        public static readonly List<SkinEffectInfo> Effects = new List<SkinEffectInfo>();

        public static string GetEffectId(SkinEffectInfo effect) {
            return EffectIdPrefix + (effect == null ? string.Empty : effect.Name);
        }

        public static bool TryGetEffectName(string id, out string effectName) {
            effectName = null;
            if (string.IsNullOrEmpty(id) || !id.StartsWith(EffectIdPrefix, StringComparison.Ordinal)) return false;

            effectName = id.Substring(EffectIdPrefix.Length);
            return !string.IsNullOrEmpty(effectName);
        }

        public static void Refresh(bool force = false) {
            if (initialized && !force) return;

            ClearState();
            // Cache unsuccessful discovery as well. Without this, every OnGUI
            // event scans all loaded assemblies when the optional mod is absent.
            initialized = true;
            Assembly skinEffectsAssembly;
            if (!TryFindSkinEffectsAssembly(out skinEffectsAssembly)) return;

            try {
                if (TryInitializeEnumApi(skinEffectsAssembly)) {
                    IsAvailable = true;
                    StatusLabel = "已安装 (新API)";
                    return;
                }

                ClearState();
                initialized = true;
                if (TryInitializeLegacyApi(skinEffectsAssembly)) {
                    IsAvailable = true;
                    StatusLabel = "已安装 (旧API)";
                    return;
                }
            } catch (Exception ex) {
                JuiceControl.LogDebug("Unsupported SkinEffects API: " + ex);
            }

            ClearState();
            initialized = true;
            StatusLabel = "已安装但API不兼容";
        }

        public static SkinEffectInfo FindEffect(string name) {
            for (int i = 0; i < Effects.Count; i++) {
                if (string.Equals(Effects[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return Effects[i];
                }
            }

            return null;
        }

        public static int GetLevel(ChaControl cha, SkinEffectInfo effect) {
            object controller = GetController(cha);
            if (controller == null || effect == null) return 0;

            try {
                object value = effect.LevelProperty != null
                    ? effect.LevelProperty.GetValue(controller, null)
                    : getEffectLevelMethod.Invoke(controller, new[] { effect.EnumValue });
                return Mathf.Clamp(Convert.ToInt32(value), 0, effect.MaxLevel);
            } catch (Exception ex) {
                JuiceControl.LogDebug("Failed to read SkinEffects level: " + ex);
                return 0;
            }
        }

        public static void SetLevel(ChaControl cha, SkinEffectInfo effect, int level) {
            object controller = GetController(cha);
            if (controller == null || effect == null) return;

            try {
                level = Mathf.Clamp(level, 0, effect.MaxLevel);
                if (effect.LevelProperty != null) {
                    effect.LevelProperty.SetValue(controller, level, null);
                } else {
                    setEffectLevelMethod.Invoke(controller, new object[] { effect.EnumValue, level, true });
                }
            } catch (Exception ex) {
                JuiceControl.LogDebug("Failed to set SkinEffects level: " + ex);
            }
        }

        private static void ClearState() {
            initialized = false;
            IsAvailable = false;
            StatusLabel = "未安装";
            Effects.Clear();
            controllerType = null;
            effectKindType = null;
            textureLoaderType = null;
            getEffectLevelMethod = null;
            setEffectLevelMethod = null;
            getTextureCountMethod = null;
        }

        private static bool TryFindSkinEffectsAssembly(out Assembly skinEffectsAssembly) {
            skinEffectsAssembly = null;

            try {
                foreach (var pluginInfo in Chainloader.PluginInfos) {
                    bool candidate = string.Equals(pluginInfo.Key, PluginGuid, StringComparison.OrdinalIgnoreCase) ||
                        pluginInfo.Key.IndexOf("SkinEffects", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (pluginInfo.Value != null && pluginInfo.Value.Metadata != null) {
                        candidate = candidate ||
                            string.Equals(pluginInfo.Value.Metadata.GUID, PluginGuid, StringComparison.OrdinalIgnoreCase) ||
                            pluginInfo.Value.Metadata.GUID.IndexOf("SkinEffects", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pluginInfo.Value.Metadata.Name.IndexOf("Skin Effects", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pluginInfo.Value.Metadata.Name.IndexOf("SkinEffects", StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    if (!candidate) {
                        continue;
                    }

                    if (pluginInfo.Value == null || pluginInfo.Value.Instance == null) continue;

                    Assembly assembly = pluginInfo.Value.Instance.GetType().Assembly;
                    if (!IsSkinEffectsAssembly(assembly)) continue;

                    skinEffectsAssembly = assembly;
                    return true;
                }
            } catch (Exception ex) {
                JuiceControl.LogDebug("Failed to inspect BepInEx plugin list for SkinEffects: " + ex);
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++) {
                if (!IsSkinEffectsAssembly(assemblies[i])) continue;

                skinEffectsAssembly = assemblies[i];
                return true;
            }

            return false;
        }

        private static bool IsSkinEffectsAssembly(Assembly assembly) {
            if (assembly == null) return false;

            try {
                return assembly.GetType(ControllerTypeName, false) != null;
            } catch {
                return false;
            }
        }

        private static bool TryInitializeEnumApi(Assembly assembly) {
            Type controller = assembly.GetType(ControllerTypeName, false);
            Type effectKind = assembly.GetType(EffectKindTypeName, false);
            Type textureLoader = assembly.GetType(TextureLoaderTypeName, false);
            if (controller == null || effectKind == null || !effectKind.IsEnum || textureLoader == null) return false;

            MethodInfo getLevel = controller.GetMethod("GetEffectLevel", BindingFlags.Instance | BindingFlags.Public, null, new[] { effectKind }, null);
            MethodInfo setLevel = controller.GetMethod("SetEffectLevel", BindingFlags.Instance | BindingFlags.Public, null, new[] { effectKind, typeof(int), typeof(bool) }, null);
            MethodInfo getTextureCount = textureLoader.GetMethod("GetTextureCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { effectKind }, null);
            if (getLevel == null || setLevel == null || getTextureCount == null) return false;

            controllerType = controller;
            effectKindType = effectKind;
            textureLoaderType = textureLoader;
            getEffectLevelMethod = getLevel;
            setEffectLevelMethod = setLevel;
            getTextureCountMethod = getTextureCount;

            Array values = Enum.GetValues(effectKindType);
            for (int i = 0; i < values.Length; i++) {
                object enumValue = values.GetValue(i);
                int id = Convert.ToInt32(enumValue);
                if (id < 0) continue;

                string name = Enum.GetName(effectKindType, enumValue);
                int maxLevel = GetTextureCount(enumValue);
                if (maxLevel <= 0) maxLevel = GetFallbackMaxLevel(name);
                Effects.Add(new SkinEffectInfo(name, GetDisplayName(name), enumValue, null, maxLevel));
            }

            return Effects.Count > 0;
        }

        private static bool TryInitializeLegacyApi(Assembly assembly) {
            Type controller = assembly.GetType(ControllerTypeName, false);
            Type textureLoader = assembly.GetType(TextureLoaderTypeName, false);
            if (controller == null) return false;

            controllerType = controller;
            textureLoaderType = textureLoader;

            AddLegacyEffect("BloodLevel", "处女血", "BldTexturesCount", 3);
            AddLegacyEffect("PussyJuiceLevel", "爱液", "PussyJuiceTexturesCount", 3);
            AddLegacyEffect("BukkakeLevel", "身体精液", "CumTexturesCount", 3);
            AddLegacyEffect("AnalBukkakeLevel", "肛门精液", "AnalCumTexturesCount", 3);
            AddLegacyEffect("SweatLevel", "汗水/湿身", "WetTexturesFaceCount", 3);
            AddLegacyEffect("TearLevel", "扩展泪水", "TearTexturesCount", 3);
            AddLegacyEffect("DroolLevel", "口水", "DroolTexturesCount", 3);
            AddLegacyEffect("SalivaLevel", "唾液", "SalivaTexturesCount", 3);
            AddLegacyEffect("CumInNoseLevel", "鼻内精液", "CumInNoseTexturesCount", 3);
            AddLegacyEffect("ButtLevel", "臀部泛红", "ButtTexturesCount", 4);
            AddLegacyEffect("BlushLevel", "扩展脸红", "BlushTexturesFaceCount", 4);

            return Effects.Count > 0;
        }

        private static void AddLegacyEffect(string propertyName, string displayName, string textureCountPropertyName, int fallbackMaxLevel) {
            PropertyInfo property = controllerType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || !property.CanWrite) return;

            int maxLevel = GetTextureCount(textureCountPropertyName, fallbackMaxLevel);
            Effects.Add(new SkinEffectInfo(propertyName, displayName, null, property, maxLevel));
        }

        private static object GetController(ChaControl cha) {
            if (!IsAvailable || cha == null || controllerType == null) return null;
            return cha.GetComponent(controllerType);
        }

        private static int GetTextureCount(object enumValue) {
            try {
                object value = getTextureCountMethod.Invoke(null, new[] { enumValue });
                return Convert.ToInt32(value);
            } catch {
                return 0;
            }
        }

        private static int GetTextureCount(string propertyName, int fallbackMaxLevel) {
            if (textureLoaderType == null) return fallbackMaxLevel;

            try {
                PropertyInfo property = textureLoaderType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null) return fallbackMaxLevel;

                object value = property.GetValue(null, null);
                int maxLevel = Convert.ToInt32(value);
                return maxLevel > 0 ? maxLevel : fallbackMaxLevel;
            } catch {
                return fallbackMaxLevel;
            }
        }

        private static int GetFallbackMaxLevel(string effectName) {
            switch (effectName) {
                case "BlushBody":
                case "BlushFace":
                case "ButtBlushBody":
                    return 4;
                default:
                    return 3;
            }
        }

        private static string GetDisplayName(string effectName) {
            switch (effectName) {
                case "VirginBloodBody": return "处女血";
                case "PussyJuiceBody": return "爱液";
                case "PussyBukkakeBody": return "阴部内射";
                case "AnalBukkakeBody": return "肛门内射";
                case "WetBody": return "身体汗/湿";
                case "WetFace": return "脸部汗/湿";
                case "BlushBody": return "身体泛红";
                case "BlushFace": return "脸部泛红";
                case "ButtBlushBody": return "臀部泛红";
                case "TearFace": return "扩展泪水";
                case "DroolFace": return "口水";
                case "SalivaFace": return "唾液";
                case "CumInNoseFace": return "鼻内精液";
                default: return effectName;
            }
        }
    }

    internal class SkinEffectInfo {
        public readonly string Name;
        public readonly string DisplayName;
        public readonly object EnumValue;
        public readonly PropertyInfo LevelProperty;
        public readonly int MaxLevel;

        public SkinEffectInfo(string name, string displayName, object enumValue, PropertyInfo levelProperty, int maxLevel) {
            Name = name;
            DisplayName = displayName;
            EnumValue = enumValue;
            LevelProperty = levelProperty;
            MaxLevel = maxLevel;
        }
    }

}
