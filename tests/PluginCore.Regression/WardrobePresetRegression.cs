using System;
using System.IO;
using ClothingControl;

internal static class WardrobePresetRegression {
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CoordinateLoadBarrier() {
        WardrobeLoadGate gate = new WardrobeLoadGate(0, 2, 10, 0f);
        Require(gate.Poll(11, .1f, true, 0, false) == WardrobeLoadResult.Waiting, "Old coordinate is still loading.");
        gate.NotifyReload(12, 2);
        Require(gate.Poll(12, .2f, true, 2, false) == WardrobeLoadResult.Waiting, "Reload event alone is not enough.");
        Require(gate.Poll(13, .3f, true, 2, true) == WardrobeLoadResult.Waiting, "First matching frame is not enough.");
        Require(gate.Poll(13, .3f, true, 2, true) == WardrobeLoadResult.Waiting, "Repeated read in one frame cannot satisfy the barrier.");
        Require(gate.Poll(14, .4f, true, 2, true) == WardrobeLoadResult.Ready, "Verified outfit and reload signal release the barrier.");
        gate = new WardrobeLoadGate(0, 2, 10, 0f);
        Require(gate.Poll(11, .1f, true, 2, true) == WardrobeLoadResult.Waiting, "A missing event requires sustained matching.");
        Require(gate.Poll(12, .3f, true, 2, false) == WardrobeLoadResult.Waiting, "Transient match is discarded.");
        Require(gate.Poll(13, .4f, true, 2, true) == WardrobeLoadResult.Waiting, "Restart stability after mismatch.");
        Require(gate.Poll(14, .9f, true, 2, true) == WardrobeLoadResult.Waiting, "Do not write too early.");
        Require(gate.Poll(15, 1.2f, true, 2, true) == WardrobeLoadResult.Ready, "Stable fallback supports versions without reload events.");
        gate = new WardrobeLoadGate(0, 2, 10, 0f);
        gate.Poll(11, .1f, true, 2, false);
        Require(gate.Poll(12, .2f, true, 0, false) == WardrobeLoadResult.Cancelled, "A manual coordinate change cancels a pending operation.");
        gate = new WardrobeLoadGate(0, 2, 10, 0f);
        Require(gate.Poll(11, .2f, false, 2, true) == WardrobeLoadResult.Cancelled, "A replaced character is never written.");
        Require(gate.Poll(12, 8f, true, 0, false) == WardrobeLoadResult.TimedOut, "Timeout leaves the state untouched.");
    }

    internal static void RunAll() {
        CoordinateLoadBarrier();
        string directory = Path.Combine(Path.GetTempPath(), "wardrobe-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string path = Path.Combine(directory, "presets.xml");
            WardrobePresetStore store = new WardrobePresetStore(path);
            Require(store.Reload(), "Missing preset file loads empty.");
            WardrobePreset state = new WardrobePreset { Coordinate = 2 };
            state.Accessories.Add(new AccessoryVisibility { Slot = 21, Type = 121, Item = 42, Visible = false });
            string error;
            Require(store.Save(state, "Travel", false, out error), "Save new: " + error);
            state.Accessories[0].Visible = true;
            Require(!store.Find("Travel").Accessories[0].Visible, "Saved snapshot is independent of the editor.");
            Require(!store.Save(state, "travel", false, out error), "Duplicate case-insensitive names rejected.");
            WardrobePreset loaded = store.Find("Travel");
            Require(loaded.Coordinate == 2 && loaded.Accessories[0].Matches(21, 121, 42), "Snapshot retains coordinate and accessory identity.");
            Require(!loaded.Accessories[0].Matches(21, 121, 43), "Replaced accessory cannot receive a stale slot state.");
            Require(!loaded.Accessories[0].Matches(22, 121, 42), "Different slot cannot receive a stale state.");
            Require(store.Save(state, "Travel", true, out error), "Explicit overwrite: " + error);
            WardrobePresetStore reloaded = new WardrobePresetStore(path);
            Require(reloaded.Reload() && reloaded.Find("Travel").Accessories[0].Visible, "XML round trip and overwrite.");
            Require(File.Exists(path + ".bak"), "Existing preset is backed up.");
            File.WriteAllText(path, "<invalid");
            Require(!reloaded.Reload() && !reloaded.CanWrite && reloaded.Find("Travel") != null,
                "Bad XML preserves the previous in-memory preset.");
            Require(!reloaded.Delete("Travel", out error) && File.ReadAllText(path) == "<invalid", "Corrupt source cannot be overwritten.");
            File.Copy(path + ".bak", path, true);
            Require(reloaded.Reload() && reloaded.Delete("Travel", out error), "After recovery, delete works.");
            Require(reloaded.Names().Length == 0, "Delete removed exactly the selected preset.");
        } finally { Directory.Delete(directory, true); }
    }
}
