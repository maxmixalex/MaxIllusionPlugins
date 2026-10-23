using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AccessoryBatchManager;
using ClothingCompression;
using MaxIllusionPlugins.Common;
using UnityEngine;

internal static class Program {
    private static int passed;
    private static int failed;
    private static readonly string TemporaryRoot = Path.Combine(Path.GetTempPath(), "accessory-clothing-tests-" + Guid.NewGuid().ToString("N"));

    private static int Main(string[] args) {
        if (args.Length != 0) return PresetFileValidation.Run(args);
        Directory.CreateDirectory(TemporaryRoot);
        try {
            Run("same-number accessory copy across coordinates", CrossCoordinateSameSlot);
            Run("same-number adjustment copy across coordinates", CrossCoordinateSameAdjustment);
            Run("copy reads current working coordinate", CurrentWorkingCopy);
            Run("same inactive coordinate never edits active coordinate", InactiveCoordinateCopy);
            Run("overlapping move preserves both original sources", OverlappingMove);
            Run("inactive coordinate adjustments use original sources", InactiveCoordinateAdjustments);
            Run("clothing to accessory same-number mapping", ClothingToAccessory);
            Run("invalid slot mapping has no partial side effects", InvalidSlotMapping);
            Run("card copy into inactive coordinate does not clear active metadata", InactiveCardEvents);
            Run("card adjustments copy normal empty slots independently", EmptyCardAdjustments);
            Run("card adjustments reject null source without partial writes", NullCardAdjustmentSource);
            Run("card adjustments initialize null destination slots", NullCardAdjustmentDestination);
            Run("card adjustments reject malformed vectors before writing", MalformedCardAdjustments);
            Run("cancelled character reload restores renderer state", CancelledReload);
            Run("failed character reload restores renderer state", FailedReload);
            Run("highlight owns a private material", PrivateHighlightMaterial);
            Run("clearing a highlight preserves external material edits", HighlightPreservesExternalChanges);
            Run("replaced accessory root receives highlight", HighlightSurvivesReplacement);
            Run("MaterialEditor snapshot survives in-place property edits", MaterialSnapshot);
            Run("deleted final deformation stays deleted after reload", DeleteFinalProfileEntry);
            Run("unbound deformation removes its former profile", UnbindProfileEntry);
            Run("save isolates other characters and unequipped items", SaveIsolation);
            Run("repeated equipped item does not duplicate shape identity", RepeatedEquipment);
            Run("character profile overrides global across repeated equipment", CharacterOverride);
            Run("legacy character keys and identity-less entries remain editable", LegacyProfile);
            Run("empty XML collections remain valid", EmptyXmlCollections);
            Run("profile storage preserves previous file as backup", BackupPreserved);
            Run("failed XML reload preserves memory and blocks replacement", FailedXmlReload);
            Run("initial corrupt XML cannot be overwritten", InitialCorruptXml);
            Run("successful XML reload permits saving after repair", RepairedXmlReload);
            Run("controller retains unsaved edits after XML read and save failures", ControllerCorruptXml);
            Run("enabled toggle schedules baked garment rebuild", ToggleBakedVisibility);
            Run("visibility poll schedules rebuild before weight-only refresh", PollBakedVisibility);
            Run("controller deletion supplies previously persisted identities", ControllerDeletion);
            Run("equipment catalog resolves once per frame", CatalogOncePerFrame);
            Run("empty controller does not rebuild forever", EmptyController);
            Run("unbound body preview leaves garments unchanged", UnboundPreview);
            Run("unrelated renderer skips native mesh cloning", UnrelatedMesh);
            CompressionEditorRegression.Run(Run);
            AccessoryEditorSessionRegression.Run(Run);
        }
        finally { Directory.Delete(TemporaryRoot, true); }
        Console.WriteLine("Accessory/Clothing regression: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test) {
        try { MakerEventBridge.Events.Clear(); test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Equal<T>(T expected, T actual, string message = "") {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(message + " expected=" + expected + ", actual=" + actual);
    }
    private static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws<T>(Action action) where T : Exception {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static ChaFileAccessory.PartsInfo Part(int id) { return new ChaFileAccessory.PartsInfo { type = 121, id = id }; }
    private static ChaControl Character() {
        var cha = new ChaControl();
        cha.nowCoordinate.accessory.parts[0] = Part(900);
        cha.chaFile.coordinate[0].accessory.parts[0] = Part(100);
        cha.chaFile.coordinate[1].accessory.parts[0] = Part(200);
        cha.chaFile.coordinate[1].accessory.parts[1] = Part(201);
        cha.chaFile.coordinate[1].accessory.parts[2] = Part(202);
        return cha;
    }

    private static void CrossCoordinateSameSlot() {
        var cha = Character();
        Equal(1, AccessoryOperations.CopyOrMoveBetweenCoordinates(cha, 1, 2, new[] { 0 }, new[] { 0 }, false, false));
        Equal(200, cha.chaFile.coordinate[2].accessory.parts[0].id);
        Equal(900, cha.nowCoordinate.accessory.parts[0].id, "unrelated current edits must survive");
        True(MakerEventBridge.Events.Contains("copy:1:2:0"), "same-index cross-coordinate event missing");
    }
    private static void CrossCoordinateSameAdjustment() {
        var cha = Character();
        cha.chaFile.coordinate[1].accessory.parts[0].addMove[1, 0] = new Vector3(7, 8, 9);
        Equal(1, AccessoryOperations.CopyAdjustmentsBetweenCoordinates(cha, 1, 2, new[] { 0 }, new[] { 0 }, 1, MirrorMode.None));
        Equal(7f, cha.chaFile.coordinate[2].accessory.parts[0].addMove[1, 0].x);
        Equal(900, cha.nowCoordinate.accessory.parts[0].id);
    }
    private static void CurrentWorkingCopy() {
        var cha = Character();
        Equal(1, AccessoryOperations.CopyOrMoveBetweenCoordinates(cha, 0, 2, new[] { 0 }, new[] { 1 }, false, false));
        Equal(900, cha.chaFile.coordinate[2].accessory.parts[1].id);
        True(ReferenceEquals(cha.nowCoordinate, AccessoryOperations.GetCoordinate(cha, 0)), "Maker should display the working copy");
    }
    private static void InactiveCoordinateCopy() {
        var cha = Character();
        Equal(1, AccessoryOperations.CopyOrMoveBetweenCoordinates(cha, 1, 1, new[] { 0 }, new[] { 2 }, false, false));
        Equal(200, cha.chaFile.coordinate[1].accessory.parts[2].id);
        Equal(900, cha.nowCoordinate.accessory.parts[0].id);
        Equal(120, cha.nowCoordinate.accessory.parts[2].type);
    }
    private static void OverlappingMove() {
        var cha = Character();
        Equal(2, AccessoryOperations.CopyOrMoveBetweenCoordinates(cha, 1, 1, new[] { 0, 1 }, new[] { 1, 2 }, false, true));
        var parts = cha.chaFile.coordinate[1].accessory.parts;
        Equal(120, parts[0].type); Equal(200, parts[1].id); Equal(201, parts[2].id);
    }
    private static void InactiveCoordinateAdjustments() {
        var cha = Character();
        var parts = cha.chaFile.coordinate[1].accessory.parts;
        parts[0].addMove[0, 0] = new Vector3(5, 0, 0);
        parts[1].addMove[0, 0] = new Vector3(6, 0, 0);
        Equal(2, AccessoryOperations.CopyAdjustmentsBetweenCoordinates(cha, 1, 1, new[] { 0, 1 }, new[] { 1, 2 }, 0, MirrorMode.None));
        Equal(5f, parts[1].addMove[0, 0].x); Equal(6f, parts[2].addMove[0, 0].x);
        Equal(0f, cha.nowCoordinate.accessory.parts[1].addMove[0, 0].x);
    }
    private static void ClothingToAccessory() {
        var cha = Character();
        cha.nowCoordinate.clothes.parts[0].id = 301;
        Equal(1, AccessoryOperations.CopyOrMoveClothingToAccessories(cha, cha.nowCoordinate.clothes.parts, 0, 2, new[] { 0 }, new[] { 0 }, false, true));
        Equal(301, cha.chaFile.coordinate[2].accessory.parts[0].id);
        Equal(0, cha.nowCoordinate.clothes.parts[0].id);
    }
    private static void InvalidSlotMapping() {
        var cha = Character();
        Equal(0, AccessoryOperations.CopyOrMoveBetweenCoordinates(cha, 1, 2, new[] { 0, 99 }, new[] { 1, 2 }, false, true));
        Equal(200, cha.chaFile.coordinate[1].accessory.parts[0].id);
        Equal(120, cha.chaFile.coordinate[2].accessory.parts[1].type);
        Equal(0, AccessoryOperations.CopyAdjustmentsBetweenCoordinates(cha, 1, 2, new[] { 0 }, new[] { 1 }, 2, MirrorMode.None));
    }
    private static void InactiveCardEvents() {
        var cha = Character();
        Equal(1, AccessoryOperations.CopyFromParts(cha, new[] { Part(707) }, 2, new[] { 0 }, new[] { 1 }, false));
        Equal(0, MakerEventBridge.Events.Count, "coordinate-less events must not target the current coordinate");
        Equal(1, AccessoryOperations.CopyFromParts(cha, new[] { Part(708) }, 0, new[] { 0 }, new[] { 1 }, false));
        True(MakerEventBridge.Events.Contains("changed:1"), "active coordinate still needs metadata invalidation");
    }
    private static void EmptyCardAdjustments() {
        var cha = Character();
        var source = new[] { new ChaFileAccessory.PartsInfo(), Part(708) };
        source[0].addMove[0, 0] = new Vector3(4, 5, 6);
        source[1].addMove[0, 0] = new Vector3(7, 8, 9);
        // External cards own separate PartsInfo instances; overlapping slot numbers
        // do not alias the destination coordinate or alter later source values.
        Equal(2, AccessoryOperations.CopyAdjustmentsFromParts(cha, source, 2, new[] { 0, 1 }, new[] { 1, 2 }, 0, MirrorMode.None));
        Equal(4f, cha.chaFile.coordinate[2].accessory.parts[1].addMove[0, 0].x);
        Equal(7f, cha.chaFile.coordinate[2].accessory.parts[2].addMove[0, 0].x);
        Equal(7f, source[1].addMove[0, 0].x);
        Equal(120, cha.chaFile.coordinate[2].accessory.parts[1].type);
    }
    private static void NullCardAdjustmentSource() {
        var cha = Character(); var first = Part(707);
        first.addMove[0, 0] = new Vector3(7, 0, 0);
        Equal(0, AccessoryOperations.CopyAdjustmentsFromParts(cha, new[] { first, null }, 2, new[] { 0, 1 }, new[] { 1, 2 }, 0, MirrorMode.None));
        Equal(0f, cha.chaFile.coordinate[2].accessory.parts[1].addMove[0, 0].x);
    }
    private static void NullCardAdjustmentDestination() {
        var cha = Character(); var source = Part(707);
        source.addMove[0, 0] = new Vector3(7, 0, 0);
        cha.chaFile.coordinate[2].accessory.parts[1] = null;
        Equal(1, AccessoryOperations.CopyAdjustmentsFromParts(cha, new[] { source }, 2, new[] { 0 }, new[] { 1 }, 0, MirrorMode.None));
        var destination = cha.chaFile.coordinate[2].accessory.parts[1];
        Equal(120, destination.type); Equal(7f, destination.addMove[0, 0].x);
        Equal(1f, destination.addMove[1, 2].x, "unmodified adjustment retains the game's default scale");
    }
    private static void MalformedCardAdjustments() {
        foreach (Vector3[,] malformed in new[] { null, new Vector3[1, 3], new Vector3[2, 2] }) {
            var cha = Character(); var valid = Part(707); var invalid = Part(708);
            valid.addMove[1, 0] = new Vector3(7, 0, 0); invalid.addMove = malformed;
            Equal(0, AccessoryOperations.CopyAdjustmentsFromParts(cha, new[] { valid, invalid }, 2, new[] { 0, 1 }, new[] { 1, 2 }, 1, MirrorMode.None));
            Equal(0f, cha.chaFile.coordinate[2].accessory.parts[1].addMove[1, 0].x);
            cha.chaFile.coordinate[2].accessory.parts[2].addMove = malformed;
            Equal(0, AccessoryOperations.CopyAdjustmentsFromParts(cha, new[] { valid }, 2, new[] { 0 }, new[] { 1, 2 }, 1, MirrorMode.None));
            Equal(0f, cha.chaFile.coordinate[2].accessory.parts[1].addMove[1, 0].x);
        }
    }
    private static void CancelledReload() {
        var cha = Character();
        var visible = cha.gameObject.Add(new MeshRenderer());
        var hidden = cha.gameObject.Add(new MeshRenderer { enabled = false });
        var nested = new FailingRoutine(false);
        cha.ReloadFactory = () => nested;
        AccessoryOperations.RefreshCharacter(cha);
        True(cha.StartedRoutine.MoveNext(), "reload should suspend");
        True(!visible.enabled, "reload hides character while running");
        ((IDisposable)cha.StartedRoutine).Dispose();
        True(visible.enabled, "cancelled reload failed to restore visibility");
        True(!hidden.enabled, "originally hidden renderer must stay hidden");
        True(nested.Disposed, "nested coroutine must be disposed");
        Equal(900, cha.chaFile.coordinate[0].accessory.parts[0].id, "normalization must retain working copy");
    }
    private static void FailedReload() {
        var cha = Character(); var renderer = cha.gameObject.Add(new MeshRenderer());
        var failing = new FailingRoutine(true); cha.ReloadFactory = () => failing;
        AccessoryOperations.RefreshCharacter(cha);
        True(cha.StartedRoutine.MoveNext(), "reload should suspend once");
        bool threw = false;
        try { cha.StartedRoutine.MoveNext(); } catch (InvalidOperationException) { threw = true; }
        True(threw, "test must inject an exception into ReloadAsync");
        True(renderer.enabled, "failed reload left renderer hidden");
        True(failing.Disposed, "failed coroutine not disposed");
    }
    private sealed class FailingRoutine : IEnumerator, IDisposable {
        private readonly bool fail;
        private int calls;
        internal bool Disposed;
        internal FailingRoutine(bool fail) { this.fail = fail; }
        public object Current { get { return null; } }
        public bool MoveNext() { if (++calls > 1 && fail) throw new InvalidOperationException("Injected reload failure"); return true; }
        public void Reset() { throw new NotSupportedException(); }
        public void Dispose() { Disposed = true; }
    }

    private static ChaControl HighlightCharacter(out MeshRenderer renderer, out Material material) {
        var cha = Character(); var root = new GameObject();
        renderer = root.Add(new MeshRenderer()); material = new Material();
        material.SetTexture("_MainTex", new Texture()); renderer.sharedMaterials = new[] { material };
        cha.objAccessory[0] = root; return cha;
    }
    private static void PrivateHighlightMaterial() {
        MeshRenderer renderer; Material original;
        var cha = HighlightCharacter(out renderer, out original); var shared = new Material();
        var color = new Color(.2f, .3f, .4f, 1); shared.SetColor("_Color", color);
        using (var highlight = new AccessoryHighlighter(() => shared)) {
            highlight.Update(cha, 0, new[] { 0 });
            Equal(2, renderer.sharedMaterials.Length);
            True(!ReferenceEquals(shared, renderer.sharedMaterials[1]), "accent must not borrow SliderHighlight's mutable material");
            Equal(color, shared.GetColor("_Color")); Equal(0, shared.GetInt("_UseMaterialColor"));
            True(shared.GetTexture("_MainTex") == null, "source textures leaked into another plugin's material");
        }
        Equal(1, renderer.sharedMaterials.Length);
    }
    private static void HighlightPreservesExternalChanges() {
        MeshRenderer renderer; Material original;
        var cha = HighlightCharacter(out renderer, out original); var shared = new Material(); var replacement = new Material();
        using (var highlight = new AccessoryHighlighter(() => shared)) {
            highlight.Update(cha, 0, new[] { 0 });
            var overlay = renderer.sharedMaterials[1]; renderer.sharedMaterials = new[] { replacement, overlay };
            highlight.Clear();
            Equal(1, renderer.sharedMaterials.Length); True(ReferenceEquals(replacement, renderer.sharedMaterials[0]), "clear restored stale base material");
            True(overlay.Destroyed, "private overlay must be released");
        }
    }
    private static void HighlightSurvivesReplacement() {
        MeshRenderer oldRenderer; Material original;
        var cha = HighlightCharacter(out oldRenderer, out original); var shared = new Material();
        using (var highlight = new AccessoryHighlighter(() => shared)) {
            highlight.Update(cha, 0, new[] { 0 });
            var root = new GameObject(); var next = root.Add(new MeshRenderer { sharedMaterials = new[] { original } });
            cha.objAccessory[0] = root;
            highlight.Update(cha, 0, new[] { 0 });
            Equal(1, oldRenderer.sharedMaterials.Length); Equal(2, next.sharedMaterials.Length);
        }
    }
    private static void MaterialSnapshot() {
        var controller = new FakeMaterialController(); var property = new FakeProperty { Slot = 2 };
        controller.Values.Add(property);
        object snapshot = MaterialEditorBridge.CaptureControllerState(controller);
        property.Slot = 42; controller.Values.Clear();
        MaterialEditorBridge.RestoreState(snapshot);
        Equal(2, controller.Values[0].Slot, "snapshot retained a live property object");
    }
    private sealed class FakeProperty { public int Slot; }
    private sealed class FakeMaterialController {
        private readonly List<FakeProperty> RendererPropertyList = new List<FakeProperty>();
        internal List<FakeProperty> Values { get { return RendererPropertyList; } }
    }

    private static CompressionLibraryStore Store() { return new CompressionLibraryStore(Path.Combine(TemporaryRoot, Guid.NewGuid().ToString("N") + ".xml")); }
    private static DeformationEntry Entry(string id, int item = 42, SaveScope scope = SaveScope.CharacterAndItem) {
        var entry = DeformationEntry.Create(DeformationKind.Thigh);
        entry.Id = id; entry.BindingKind = BoundItemKind.Accessory; entry.BindingCategory = 121;
        entry.BindingId = item; entry.BindingName = "Item " + item; entry.PreferredSlot = 0; entry.SaveScope = scope;
        return entry;
    }
    private static ItemDescriptor Item(int id = 42, int slot = 0) { return new ItemDescriptor { Kind = BoundItemKind.Accessory, Category = 121, Id = id, Slot = slot, Name = "Item " + id }; }
    private static void DeleteFinalProfileEntry() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("deleted") });
        var loaded = store.LoadEntries("1:Alice:coord:0", new[] { Item() });
        Equal(1, loaded.Count);
        store.SaveEntries("1:Alice:coord:0", new DeformationEntry[0], loaded.Select(x => x.Id));
        store.Reload(); Equal(0, store.LoadEntries("1:Alice:coord:0", new[] { Item() }).Count);
    }
    private static void UnbindProfileEntry() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("unbound") });
        var loaded = store.LoadEntries("1:Alice:coord:0", new[] { Item() });
        ItemCatalog.Bind(loaded[0], null);
        store.SaveEntries("1:Alice:coord:0", loaded, new[] { "unbound" });
        store.Reload(); Equal(0, store.Data.Profiles.Count);
    }
    private static void SaveIsolation() {
        var store = Store();
        store.SaveEntries("1:Alice:coord:0", new[] { Entry("alice"), Entry("not-equipped", 43) });
        store.SaveEntries("1:Bob:coord:0", new[] { Entry("bob") });
        store.SaveEntries("1:Alice:coord:1", new[] { Entry("other-coordinate") });
        store.SaveEntries("1:Alice:coord:0", new DeformationEntry[0], new[] { "alice" });
        store.Reload();
        Equal("bob", store.LoadEntries("1:Bob:coord:0", new[] { Item() }).Single().Id);
        Equal("other-coordinate", store.LoadEntries("1:Alice:coord:1", new[] { Item() }).Single().Id);
        Equal("not-equipped", store.LoadEntries("1:Alice:coord:0", new[] { Item(43) }).Single().Id);
    }
    private static void RepeatedEquipment() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("global", 42, SaveScope.ItemOnly) });
        var loaded = store.LoadEntries("1:Bob:coord:0", new[] { Item(42, 0), Item(42, 3) });
        Equal(1, loaded.Count); Equal("global", loaded[0].Id);
    }
    private static void CharacterOverride() {
        var store = Store(); var global = Entry("same", 42, SaveScope.ItemOnly); global.Depth = .01f;
        var personal = Entry("same"); personal.Depth = .02f;
        store.Data.Profiles.Add(new SavedProfile { Scope = SaveScope.ItemOnly, ItemKey = global.ItemKey, Entries = new List<DeformationEntry> { global } });
        store.Data.Profiles.Add(new SavedProfile { Scope = SaveScope.CharacterAndItem, CharacterKey = "1:Alice:coord:0", ItemKey = personal.ItemKey, Entries = new List<DeformationEntry> { personal } });
        var loaded = store.LoadEntries("1:Alice:coord:0", new[] { Item(), Item(42, 2) });
        Equal(1, loaded.Count); Equal(.02f, loaded[0].Depth);
    }
    private static void LegacyProfile() {
        var store = Store(); var legacy = Entry(null);
        store.Data.Profiles.Add(new SavedProfile { Scope = SaveScope.CharacterAndItem, CharacterKey = "1:old-card.png:Alice", ItemKey = legacy.ItemKey, Entries = new List<DeformationEntry> { legacy } });
        var loaded = store.LoadEntries("1:Alice:coord:0", new[] { Item() });
        Equal(1, loaded.Count); True(!string.IsNullOrEmpty(loaded[0].Id), "legacy entry needs a stable runtime shape key");
        store.SaveEntries("1:Alice:coord:0", new DeformationEntry[0], loaded.Select(x => x.Id));
        Equal(0, store.Data.Profiles.Count);
    }
    private static void EmptyXmlCollections() {
        var store = Store();
        AtomicXmlFile.Write(store.Path, new CompressionLibrary { Profiles = new List<SavedProfile> { new SavedProfile { Entries = null } } });
        store.Reload(); store.SaveEntries("1:Alice:coord:0", new DeformationEntry[0]);
        Equal(0, store.Data.Profiles.Count);
    }
    private static void BackupPreserved() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("old") });
        string old = File.ReadAllText(store.Path);
        store.SaveEntries("1:Alice:coord:0", new[] { Entry("new") }, new[] { "old" });
        True(File.Exists(store.Path + ".bak"), "existing .bak contract must be retained");
        Equal(old, File.ReadAllText(store.Path + ".bak"));
    }

    private static void FailedXmlReload() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("backup") });
        store.SaveEntries("1:Alice:coord:0", new[] { Entry("current") }, new[] { "backup" });
        CompressionLibrary current = store.Data;
        string backup = File.ReadAllText(store.Path + ".bak");
        const string corrupt = "<ClothingCompression><Profiles><Profile";
        File.WriteAllText(store.Path, corrupt); store.Reload();
        True(ReferenceEquals(current, store.Data), "a read failure must retain the last valid in-memory library");
        Equal("current", store.LoadEntries("1:Alice:coord:0", new[] { Item() }).Single().Id);
        Throws<InvalidOperationException>(() => store.SaveEntries("1:Alice:coord:0", new DeformationEntry[0], new[] { "current" }));
        Equal("current", store.LoadEntries("1:Alice:coord:0", new[] { Item() }).Single().Id, "blocked saves must not mutate memory");
        Equal(corrupt, File.ReadAllText(store.Path)); Equal(backup, File.ReadAllText(store.Path + ".bak"));
    }
    private static void InitialCorruptXml() {
        var store = Store(); const string corrupt = "not XML";
        File.WriteAllText(store.Path, corrupt);
        Equal(0, store.Data.Profiles.Count, "initial failed load provides a safe empty read model");
        Throws<InvalidOperationException>(() => store.SaveEntries("1:Alice:coord:0", new[] { Entry("new") }));
        Equal(corrupt, File.ReadAllText(store.Path));
        True(!File.Exists(store.Path + ".bak"), "a failed load must not generate a corrupt backup");
    }
    private static void RepairedXmlReload() {
        var store = Store(); store.SaveEntries("1:Alice:coord:0", new[] { Entry("original") });
        string valid = File.ReadAllText(store.Path);
        File.WriteAllText(store.Path, "broken"); store.Reload();
        Throws<InvalidOperationException>(() => store.SaveEntries("1:Alice:coord:0", new[] { Entry("blocked") }));
        File.WriteAllText(store.Path, valid); store.Reload();
        Equal(1, store.SaveEntries("1:Alice:coord:0", new[] { Entry("repaired") }, new[] { "original" }));
        store.Reload(); Equal("repaired", store.LoadEntries("1:Alice:coord:0", new[] { Item() }).Single().Id);
        Equal(valid, File.ReadAllText(store.Path + ".bak"));
    }
    private static void ControllerCorruptXml() {
        Time.frameCount = 30;
        var cha = new ChaControl(); cha.nowCoordinate.accessory.parts[0] = Part(42);
        var controller = Controller(cha); var store = Store(); ClothingCompressionPlugin.Library = store;
        store.SaveEntries("1:Alice:coord:0", new[] { Entry("saved") });
        Invoke(controller, "LoadLibraryEntries");
        controller.Entries[0].Depth = .045f; controller.Changed(false);
        int started = controller.StartedCount;
        const string corrupt = "broken XML";
        File.WriteAllText(store.Path, corrupt); controller.ReloadFromDisk();
        Equal(.045f, controller.Entries.Single().Depth, "failed reload must not discard unsaved edits");
        True(controller.Dirty, "failed reload must not mark unsaved edits as saved");
        Equal(started, controller.StartedCount, "failed reload must not rebuild shapes");
        Equal(0, controller.Save()); True(controller.Dirty, "failed save must retain the dirty marker");
        Equal(.045f, controller.Entries.Single().Depth); Equal(corrupt, File.ReadAllText(store.Path));
        True(controller.Status.Contains("失败"), "failed save needs visible feedback");
    }

    private static CompressionController Controller(ChaControl cha = null) {
        if (cha == null) cha = new ChaControl();
        return new CompressionController { ChaControl = cha, ChaFileControl = cha.chaFile };
    }
    private static object Invoke(object target, string method, params object[] arguments) {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
    private static void Set(object target, string field, object value) {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
    private static T Get<T>(object target, string field) {
        return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
    private static void ToggleBakedVisibility() {
        Time.frameCount = 1;
        var controller = Controller();
        var entry = DeformationEntry.Create(DeformationKind.Thigh); entry.Id = "toggle"; entry.Enabled = false;
        controller.Entries.Add(entry); Set(controller, "visibilitySignature", "toggle:1");
        controller.Changed(false);
        Equal(1, controller.StartedCount, "weight-only updates must rebuild baked garment visibility");
        Equal("toggle:1", Get<string>(controller, "visibilitySignature"), "visibility must only commit after a rebuild");
    }
    private static void PollBakedVisibility() {
        Time.frameCount = 15;
        var controller = Controller();
        var entry = DeformationEntry.Create(DeformationKind.Thigh); entry.Id = "toggle"; entry.Enabled = false;
        controller.Entries.Add(entry); Set(controller, "visibilitySignature", "toggle:1");
        var renderer = controller.ChaControl.gameObject.Add(new SkinnedMeshRenderer { name = "o_body_a", sharedMesh = new Mesh() });
        renderer.sharedMesh.AddBlendShapeFrame("MaxMixAlex.ClothingCompression.test.toggle", 100, new Vector3[0], new Vector3[0], new Vector3[0]);
        renderer.SetBlendShapeWeight(0, 100);
        Type stateType = typeof(CompressionController).GetNestedType("MeshState", BindingFlags.NonPublic);
        object state = Activator.CreateInstance(stateType, true);
        stateType.GetField("Renderer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, renderer);
        stateType.GetField("Generated", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, renderer.sharedMesh);
        var shapes = (Dictionary<string, int>)stateType.GetField("ShapeIndices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        shapes.Add("toggle", 0);
        ((IList)typeof(CompressionController).GetField("meshes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller)).Add(state);
        Set(controller, "rendererSignature", Invoke(controller, "GetRendererSignature"));
        Invoke(controller, "Update");
        Equal(1, controller.StartedCount, "poll must not swallow visibility change during ApplyWeights");
        Equal("toggle:1", Get<string>(controller, "visibilitySignature"));
    }
    private static void ControllerDeletion() {
        Time.frameCount = 31;
        var cha = new ChaControl(); cha.nowCoordinate.accessory.parts[0] = Part(42);
        var controller = Controller(cha); var store = Store(); ClothingCompressionPlugin.Library = store;
        store.SaveEntries("1:Alice:coord:0", new[] { Entry("delete-through-controller") });
        Invoke(controller, "LoadLibraryEntries");
        Equal(1, controller.Entries.Count);
        controller.DeleteSelected(); controller.Save(); store.Reload();
        Equal(0, store.LoadEntries("1:Alice:coord:0", new[] { Item() }).Count);
    }
    private static void CatalogOncePerFrame() {
        Time.frameCount = 40;
        var cha = new ChaControl();
        for (int i = 0; i < 20; i++) cha.nowCoordinate.accessory.parts[i] = Part(42 + i);
        var controller = Controller(cha); var entry = Entry("catalog");
        for (int i = 0; i < 100; i++) Invoke(controller, "IsBindingVisible", entry);
        Equal(20, cha.lstCtrl.Lookups, "100 visibility resolutions should scan 20 equipped items only once");
        Time.frameCount++;
        Invoke(controller, "IsBindingVisible", entry);
        Equal(40, cha.lstCtrl.Lookups, "next frame must observe equipment changes");
        Console.WriteLine("CATALOG WORK: 100 resolutions × 20 equipped items: 20 catalog lookups (previous path: 2000).");
    }
    private static void EmptyController() {
        Time.frameCount = 45;
        var controller = Controller(); Set(controller, "visibilitySignature", "deleted:1");
        controller.ApplyNow(); Invoke(controller, "Update");
        Equal(0, controller.StartedCount, "deleting all entries must settle without recurring rebuilds");
    }
    private static object BuildMesh(CompressionController controller, SkinnedMeshRenderer renderer, DeformationEntry entry, bool garment) {
        MethodInfo build = typeof(CompressionController).GetMethod("BuildMesh", BindingFlags.Instance | BindingFlags.NonPublic);
        object frames = Activator.CreateInstance(build.GetParameters()[3].ParameterType);
        return build.Invoke(controller, new object[] { renderer, new[] { entry }, garment, frames });
    }
    private static void UnboundPreview() {
        var controller = Controller(); var entry = DeformationEntry.Create(DeformationKind.Thigh);
        var mesh = new Mesh(); var renderer = new SkinnedMeshRenderer { sharedMesh = mesh, bones = new[] { new Transform { name = "cf_j_thigh00_L" } } };
        int before = UnityEngine.Object.InstantiateCalls;
        True(BuildMesh(controller, renderer, entry, true) == null, "unbound preview should affect the body only");
        True(ReferenceEquals(mesh, renderer.sharedMesh), "unbound preview replaced a garment mesh");
        Equal(before, UnityEngine.Object.InstantiateCalls, "unbound garment must be rejected before allocating a clone");
    }
    private static void UnrelatedMesh() {
        var controller = Controller(); var entry = DeformationEntry.Create(DeformationKind.Thigh);
        var renderer = new SkinnedMeshRenderer { sharedMesh = new Mesh(), bones = new[] { new Transform { name = "cf_j_head" } } };
        int before = UnityEngine.Object.InstantiateCalls;
        True(BuildMesh(controller, renderer, entry, false) == null, "unrelated bones should skip the mesh");
        Equal(before, UnityEngine.Object.InstantiateCalls, "unrelated renderer unnecessarily cloned its mesh");
    }
}
