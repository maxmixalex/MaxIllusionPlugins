using System;
using System.Collections.Generic;
using JuiceControl;

internal static class LoveJuiceBridgeRegression {
    private sealed class SimulatedPlugin {
        internal static readonly Dictionary<string, bool> Values = new Dictionary<string, bool>();
        internal static bool FailRead, FailWrite, Opened;
        public static bool TryGetSetting(string key, out bool value) {
            value = false;
            if (FailRead) return false;
            return Values.TryGetValue(key, out value);
        }
        public static bool TrySetSetting(string key, bool value) {
            if (FailWrite || !Values.ContainsKey(key)) return false;
            Values[key] = value;
            return true;
        }
        public static bool ShowControlPanel() { Opened = true; return true; }
    }

    private sealed class OldPlugin {
        public static bool TryGetSetting(string key, out bool value) { value = false; return true; }
        // No supported setter or panel contract.
    }

    internal static void RunAll() {
        SimulatedPlugin.Values.Clear();
        SimulatedPlugin.Values.Add("Enabled", false);
        SimulatedPlugin.Values.Add("Face", true);
        SimulatedPlugin.Values.Add("Body", true);
        SimulatedPlugin.Values.Add("Milk", false);
        SimulatedPlugin.Values.Add("Shower", false);
        SimulatedPlugin.Values.Add("Special", false);
        SimulatedPlugin.FailRead = SimulatedPlugin.FailWrite = SimulatedPlugin.Opened = false;
        var bridge = new LoveJuiceIntegration(typeof(SimulatedPlugin));
        LoveJuiceSettings state = bridge.Snapshot();
        Check(state.Available && !state.Enabled && state.Face && state.Body && !state.Milk,
            "Complete snapshot is available when the effect is switched off.");
        Check(bridge.TrySet("Enabled", true) && bridge.TrySet("Milk", true),
            "Master and individual source switches must be writable.");
        Check(bridge.Snapshot().Enabled && bridge.Snapshot().Milk,
            "Switches must be confirmed with a fresh readback.");
        Check(!bridge.TrySet("InjectedConfig", true),
            "Unknown keys cannot reach an optional plugin.");
        SimulatedPlugin.FailWrite = true;
        Check(!bridge.TrySet("Body", false) && SimulatedPlugin.Values["Body"],
            "Failed writes must not be reported as accepted.");
        SimulatedPlugin.FailWrite = false;
        SimulatedPlugin.FailRead = true;
        Check(!bridge.Snapshot().Available && !bridge.TrySet("Body", false),
            "Unavailable readback must disable editing, not look like false values.");
        SimulatedPlugin.FailRead = false;
        Check(bridge.OpenPanel() && SimulatedPlugin.Opened,
            "The original panel is opened through the optional interface.");
        Check(!new LoveJuiceIntegration(typeof(OldPlugin)).Snapshot().Available,
            "Unsupported versions must fail closed.");
        LoveJuiceSettings unavailable = new LoveJuiceSettings();
        Check(!unavailable.SameValues(bridge.Snapshot()) &&
            unavailable.SameValues(new LoveJuiceSettings()),
            "Editor refresh recognizes plugin discovery and disconnect.");
    }

    private static void Check(bool ok, string message) {
        if (!ok) throw new InvalidOperationException(message);
    }
}
