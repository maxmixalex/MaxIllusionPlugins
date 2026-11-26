using System;
using System.Reflection;
using UnityEngine;

namespace LoveJuice {
    // Runtime-only, read-only adapters. The installed mod order does not
    // matter, and neither plugin is a mandatory reference for LoveJuice.
    internal sealed class OptionalEffectBridge {
        private Type controllerType, kindType;
        private MethodInfo getLevel, isFemboy;
        private bool searched;

        internal void Reset() {
            controllerType = null;
            kindType = null;
            getLevel = null;
            isFemboy = null;
            searched = false;
        }

        private void Discover() {
            if (searched) return;
            searched = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    Type skin = assembly.GetType("KK_SkinEffects.SkinEffectsController", false);
                    Type kind = assembly.GetType("KK_SkinEffects.SkinEffectKind", false);
                    if (skin != null && kind != null && kind.IsEnum) {
                        MethodInfo method = skin.GetMethod("GetEffectLevel", BindingFlags.Instance | BindingFlags.Public,
                            null, new[] { kind }, null);
                        if (method != null) {
                            controllerType = skin;
                            kindType = kind;
                            getLevel = method;
                        }
                    }
                    Type femboy = assembly.GetType("FemboyMode.FemboyModePlugin", false);
                    if (femboy != null)
                        isFemboy = femboy.GetMethod("IsFemboy", BindingFlags.Static | BindingFlags.Public,
                            null, new[] { typeof(ChaControl) }, null);
                } catch (ReflectionTypeLoadException) { } catch (Exception) { }
            }
        }

        internal int Level(ChaControl character, string kind) {
            if (character == null) return -1;
            Discover();
            if (controllerType == null || kindType == null || getLevel == null) return -1;
            try {
                object controller = character.GetComponent(controllerType);
                if (controller == null) return -1;
                object option = Enum.Parse(kindType, kind, false);
                object raw = getLevel.Invoke(controller, new[] { option });
                int level = Convert.ToInt32(raw);
                return level >= 0 ? level : -1;
            } catch { return -1; }
        }

        internal bool IsMarked(ChaControl character) {
            if (character == null) return false;
            Discover();
            if (isFemboy == null) return false;
            try {
                return (bool)isFemboy.Invoke(null, new object[] { character });
            } catch { return false; }
        }
    }
}
