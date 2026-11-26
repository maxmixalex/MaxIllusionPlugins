using System;
using System.Reflection;

namespace JuiceControl {
    // Optional API adapter: never changes other plugins while building UI.
    internal struct LoveJuiceSettings {
        internal bool Available, Enabled, Face, Body, Intimate, Milk, Shower, Special;

        internal bool SameValues(LoveJuiceSettings other) {
            return Available == other.Available && (!Available ||
                Enabled == other.Enabled && Face == other.Face &&
                Body == other.Body && Intimate == other.Intimate && Milk == other.Milk &&
                Shower == other.Shower && Special == other.Special);
        }
    }

    internal sealed class LoveJuiceIntegration {
        private MethodInfo read, write, open;

        internal LoveJuiceIntegration() { }
        internal LoveJuiceIntegration(Type supplied) { Bind(supplied); }

        private static bool KnownKey(string key) {
            switch (key) {
                case "Enabled": case "Face": case "Body":
                case "Intimate": case "Milk": case "Shower": case "Special": return true;
                default: return false;
            }
        }

        private void Bind(Type type) {
            if (type == null) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            try {
                MethodInfo getter = type.GetMethod("TryGetSetting", flags, null,
                    new[] { typeof(string), typeof(bool).MakeByRefType() }, null);
                MethodInfo setter = type.GetMethod("TrySetSetting", flags, null,
                    new[] { typeof(string), typeof(bool) }, null);
                MethodInfo panel = type.GetMethod("ShowControlPanel", flags, null, Type.EmptyTypes, null);
                if (getter == null || setter == null || panel == null ||
                    getter.ReturnType != typeof(bool) || setter.ReturnType != typeof(bool) ||
                    panel.ReturnType != typeof(bool)) return;
                read = getter; write = setter; open = panel;
            } catch (AmbiguousMatchException) { }
        }

        private void Discover() {
            if (read != null) return;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    Type type = assembly.GetType("LoveJuice.LoveJuicePlugin", false);
                    if (type != null) { Bind(type); if (read != null) return; }
                } catch (Exception) { }
            }
            // Never cache misses: optional plugins may become available later.
        }

        internal bool TryGet(string key, out bool value) {
            value = false;
            if (!KnownKey(key)) return false;
            Discover();
            if (read == null) return false;
            try {
                object[] args = { key, false };
                if (!Convert.ToBoolean(read.Invoke(null, args)) || !(args[1] is bool)) return false;
                value = (bool)args[1];
                return true;
            } catch (Exception) { return false; }
        }

        internal LoveJuiceSettings Snapshot() {
            LoveJuiceSettings state = new LoveJuiceSettings();
            // Failed readback is not equivalent to a switched-off source.
            if (!TryGet("Enabled", out state.Enabled) ||
                !TryGet("Face", out state.Face) ||
                !TryGet("Body", out state.Body) ||
                !TryGet("Intimate", out state.Intimate) ||
                !TryGet("Milk", out state.Milk) ||
                !TryGet("Shower", out state.Shower) ||
                !TryGet("Special", out state.Special)) return new LoveJuiceSettings();
            state.Available = true;
            return state;
        }

        internal bool TrySet(string key, bool value) {
            bool current;
            if (!TryGet(key, out current) || write == null) return false;
            try {
                if (!Convert.ToBoolean(write.Invoke(null, new object[] { key, value }))) return false;
            } catch (Exception) { return false; }
            bool accepted;
            return TryGet(key, out accepted) && accepted == value;
        }

        internal bool OpenPanel() {
            if (!Snapshot().Available || open == null) return false;
            try { return Convert.ToBoolean(open.Invoke(null, null)); }
            catch (Exception) { return false; }
        }
    }
}
