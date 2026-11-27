using System;
using System.Reflection;
using UnityEngine;

namespace LoveJuice {
    // Only reads optional plugin state. Both the enum-based modern API and
    // the property-based legacy API are supported; no external state is set.
    internal sealed class OptionalEffectBridge {
        private Type controllerType, kindType;
        private MethodInfo getLevel, isFemboy;
        private bool searched;
        internal bool HasSkinEffects { get { Discover(); return controllerType != null; } }

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
                    if (skin != null) {
                        controllerType = skin;
                        Type kind = assembly.GetType("KK_SkinEffects.SkinEffectKind", false);
                        if (kind != null && kind.IsEnum) {
                            MethodInfo getter = skin.GetMethod("GetEffectLevel",
                                BindingFlags.Instance | BindingFlags.Public, null, new[] { kind }, null);
                            if (getter != null) {
                                kindType = kind;
                                getLevel = getter;
                            }
                        }
                    }
                    Type femboy = assembly.GetType("FemboyMode.FemboyModePlugin", false);
                    if (femboy != null)
                        isFemboy = femboy.GetMethod("IsFemboy", BindingFlags.Static | BindingFlags.Public,
                            null, new[] { typeof(ChaControl) }, null);
                } catch (ReflectionTypeLoadException) { } catch (Exception) { }
            }
        }

        internal int Level(ChaControl character, string name) {
            if (character == null || string.IsNullOrEmpty(name)) return -1;
            Discover();
            if (controllerType == null) return -1;
            try {
                object controller = character.GetComponent(controllerType);
                if (controller == null) return -1;
                if (kindType != null && getLevel != null) {
                    try {
                        object option = Enum.Parse(kindType, name, false);
                        object raw = getLevel.Invoke(controller, new[] { option });
                        int observed = Convert.ToInt32(raw);
                        if (observed >= 0) return observed;
                    } catch (ArgumentException) { }
                    catch (TargetInvocationException) { }
                }
                int legacy;
                return SkinEffectValueReader.TryReadLegacy(controller, name, out legacy) ? legacy : -1;
            } catch { return -1; }
        }

        internal bool IsMarked(ChaControl character) {
            if (character == null) return false;
            Discover();
            if (isFemboy == null) return false;
            try { return (bool)isFemboy.Invoke(null, new object[] { character }); }
            catch { return false; }
        }
    }
}
