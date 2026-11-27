using System;
using System.IO;
using System.Collections.Generic;
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

    private sealed class Actor {
        internal string Key;
        internal Actor(string key) { Key = key; }
    }

    private static void SceneRoundTrip(string path, WardrobePresetStore store) {
        string alice = WardrobeSceneIdentity.Key("Alice", "C:/cards/ALICE.png");
        string bob = WardrobeSceneIdentity.Key("Bob", "D:/cards/bob.png");
        Require(alice == WardrobeSceneIdentity.Key("Alice", @"D:\other\alice.png"), "Card basenames survive relocation.");
        Require(alice != WardrobeSceneIdentity.Key("Other", "alice.png"), "Same card basename with another name does not match.");
        Require(WardrobeSceneIdentity.Key("", "alice.png") == null, "Unnamed characters have no guessed identity.");
        WardrobeScenePreset scene = new WardrobeScenePreset();
        scene.Members.Add(new WardrobeSceneMember { Key = alice, DisplayName = "Alice", State = new WardrobePreset { Coordinate = 1 } });
        scene.Members.Add(new WardrobeSceneMember { Key = bob, DisplayName = "Bob", State = new WardrobePreset { Coordinate = 2 } });
        string error;
        Require(store.SaveScene(scene, "Legacy", false, out error), "Scene namespace permits same name as role preset.");
        scene.Members[0].State.Coordinate = 4;
        Require(store.FindScene("Legacy").Members[0].State.Coordinate == 1, "Saved scene is isolated from the caller.");
        Require(!store.SaveScene(scene, "legacy", false, out error), "Scene names are unique case-insensitively.");
        var a = new Actor(alice); var b = new Actor(bob); var unknown = new Actor("unknown");
        WardrobeScenePreset loaded = store.FindScene("Legacy");
        List<WardrobeSceneMatch<Actor>> matches = WardrobeSceneIdentity.Match(loaded, new[] { b, unknown, a }, x => x.Key);
        Require(matches.Count == 2 && ReferenceEquals(matches[0].Target, a) && ReferenceEquals(matches[1].Target, b),
            "Reordering or adding a character does not alter identity matches.");
        matches = WardrobeSceneIdentity.Match(loaded, new[] { a, new Actor(alice), b }, x => x.Key);
        Require(matches.Count == 1 && ReferenceEquals(matches[0].Target, b), "Ambiguous duplicate character is skipped.");
        matches = WardrobeSceneIdentity.Match(loaded, new[] { unknown }, x => x.Key);
        Require(matches.Count == 0, "Missing characters are not assigned by index.");
        WardrobeScenePreset invalid = loaded.Copy(); invalid.Members.Add(invalid.Members[0].Copy());
        Require(!store.SaveScene(invalid, "Duplicate", false, out error), "Duplicate saved identity rejected before writing.");
        Require(store.SaveScene(scene, "Legacy", true, out error), "Scene overwrite works.");
        WardrobePresetStore reopened = new WardrobePresetStore(path);
        Require(reopened.Reload() && reopened.Find("Legacy") != null &&
            reopened.FindScene("Legacy").Members[0].State.Coordinate == 4, "Both preset namespaces survive XML reload.");
        Require(reopened.DeleteScene("Legacy", out error) && reopened.Find("Legacy") != null && reopened.SceneNames().Length == 0,
            "Deleting a scene never deletes the character preset with the same name.");
    }

    internal static void RunAll() {
        CoordinateLoadBarrier();

        // ChaControl.notBot means the TOP garment owns the lower
        // clothes state dictionary. A one-piece bathing suit does NOT
        // need a second standalone bottom item ID.
        Require(WardrobeGarmentIdentity.IsLinkedBottom(
            "bot", true, 120, true), "One-piece top provides bot state");
        Require(WardrobeGarmentIdentity.EffectiveItem(
            "bot", 0, 120, true) == 120,
            "Native linked bottom is identified by its owning top item");
        Require(WardrobeGarmentIdentity.EffectiveItem(
            "bot", 211, 120, true) == 120,
            "Linked top identity takes priority over stale standalone bot ID");
        Require(!WardrobeGarmentIdentity.IsLinkedBottom(
            "bot", false, 120, true) &&
            WardrobeGarmentIdentity.EffectiveItem(
                "bot", 211, 120, false) == 211,
            "Ordinary separately equipped bottom stays independent");
        Require(!WardrobeGarmentIdentity.IsLinkedBottom(
            "bot", true, 0, true) &&
            !WardrobeGarmentIdentity.IsLinkedBottom(
                "bot", true, 120, false),
            "Neither a bare top nor a missing native bot-state channel invents clothing");
        Require(WardrobeGarmentIdentity.EffectiveItem(
            "shorts", 0, 120, true) == 0,
            "Linked one-piece lower half must not mutate underwear identity");
        string directory = Path.Combine(Path.GetTempPath(), "wardrobe-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            string path = Path.Combine(directory, "presets.xml");
            WardrobePresetStore store = new WardrobePresetStore(path);
            Require(store.Reload(), "Missing preset file loads empty.");
            WardrobePreset onePiece = new WardrobePreset { Coordinate = 3 };
            onePiece.Garments.Add(new GarmentVisibility {
                Kind = "bot", Item = 120, Visible = false, State = 1
            });
            string linkedError;
            Require(store.Save(onePiece, "OnePieceSwimsuit", false,
                out linkedError), "Linked one-piece lower garment saves: " + linkedError);
            WardrobePresetStore linkedReload = new WardrobePresetStore(path);
            Require(linkedReload.Reload() &&
                linkedReload.Find("OnePieceSwimsuit").Garments[0]
                    .Matches("bot", 120) &&
                linkedReload.Find("OnePieceSwimsuit").Garments[0].State == 1,
                "Linked lower-half state survives XML save/reload and identity checks");
            Require(store.Delete("OnePieceSwimsuit", out linkedError),
                "Remove test linked outfit without touching other presets");
            WardrobePreset state = new WardrobePreset { Coordinate = 2, ShoeMode = 1 };
            state.Garments.Add(new GarmentVisibility { Kind = "gloves", Item = 42, Visible = false, State = 3 });
            state.Accessories.Add(new AccessoryVisibility { Slot = 21, Type = 121, Item = 42, Visible = false });
            string error;
            Require(store.Save(state, "Travel", false, out error), "Save new: " + error);
            state.Accessories[0].Visible = true;
            state.Garments[0].Visible = true;
            state.Garments[0].State = 1;
            Require(!store.Find("Travel").Accessories[0].Visible &&
                !store.Find("Travel").Garments[0].Visible && store.Find("Travel").Garments[0].State == 3,
                "Saved snapshot is independent of the editor.");
            Require(!store.Save(state, "travel", false, out error), "Duplicate case-insensitive names rejected.");
            WardrobePreset loaded = store.Find("Travel");
            Require(loaded.Coordinate == 2 && loaded.Accessories[0].Matches(21, 121, 42) &&
                loaded.ShoeMode == 1 && loaded.Garments[0].Matches("gloves", 42) &&
                loaded.Garments[0].State == 3, "Snapshot retains coordinate, garment and accessory identity.");
            Require(!loaded.Garments[0].Matches("socks", 42) && !loaded.Garments[0].Matches("gloves", 43),
                "Garment presets match kind and item identity.");
            WardrobePreset invalid = loaded.Copy();
            invalid.Garments.Add(new GarmentVisibility { Kind = "unknown", Item = 1, Visible = true });
            Require(!store.Save(invalid, "Invalid", false, out error), "Unknown garment kinds cannot be written.");
            invalid = loaded.Copy();
            invalid.Garments[0].State = 4;
            Require(!store.Save(invalid, "BadState", false, out error), "Invalid clothes state must be rejected.");
            Require(GarmentVisibility.IsSupported("top") && GarmentVisibility.IsSupported("panst") &&
                GarmentVisibility.IsSupported("shorts"), "All native clothing kinds supported.");
            Require(!loaded.Accessories[0].Matches(21, 121, 43), "Replaced accessory cannot receive a stale slot state.");
            Require(!loaded.Accessories[0].Matches(22, 121, 42), "Different slot cannot receive a stale state.");
            Require(store.Save(state, "Travel", true, out error), "Explicit overwrite: " + error);
            WardrobePresetStore reloaded = new WardrobePresetStore(path);
            Require(reloaded.Reload() && reloaded.Find("Travel").Accessories[0].Visible &&
                reloaded.Find("Travel").Garments[0].Visible &&
                reloaded.Find("Travel").Garments[0].State == 1 && reloaded.Find("Travel").ShoeMode == 1, "XML round trip and overwrite.");
            Require(File.Exists(path + ".bak"), "Existing preset is backed up.");
            File.WriteAllText(path, "<invalid");
            Require(!reloaded.Reload() && !reloaded.CanWrite && reloaded.Find("Travel") != null,
                "Bad XML preserves the previous in-memory preset.");
            Require(!reloaded.Delete("Travel", out error) && File.ReadAllText(path) == "<invalid", "Corrupt source cannot be overwritten.");
            File.Copy(path + ".bak", path, true);
            Require(reloaded.Reload() && reloaded.Delete("Travel", out error), "After recovery, delete works.");
            Require(reloaded.Names().Length == 0, "Delete removed exactly the selected preset.");
            File.WriteAllText(path, "<ClothingPresets><Preset Name=\"Legacy\" Coordinate=\"0\"><Accessory Slot=\"0\" Type=\"121\" Item=\"1\" Visible=\"true\" /></Preset></ClothingPresets>");
            Require(reloaded.Reload(), "Legacy XML without garment or shoe fields is valid.");
            WardrobePreset legacy = reloaded.Find("Legacy");
            Require(legacy.ShoeMode == -1 && legacy.Garments.Count == 0, "Legacy preset does not manufacture garment settings.");
            Require(reloaded.SceneNames().Length == 0, "Old XML loads without scene presets.");
            SceneRoundTrip(path, reloaded);
            File.WriteAllText(path,
                "<ClothingPresets><Preset Name=\"OldVisible\" Coordinate=\"0\"><Garment Kind=\"gloves\" Item=\"42\" Visible=\"false\" /></Preset></ClothingPresets>");
            Require(reloaded.Reload() && reloaded.Find("OldVisible").Garments[0].State == -1 &&
                !reloaded.Find("OldVisible").Garments[0].Visible,
                "Legacy boolean-only garment XML does not invent an explicit state.");
        } finally { Directory.Delete(directory, true); }
    }
}
