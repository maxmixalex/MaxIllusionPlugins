using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClothingCompression;
using MaximumUI;

internal static class CompressionEditorRegression {
    internal static void Run(Action<string, Action> run) {
        run("MaximumUI clothing fields update real controller values with unit conversion", Parameters);
        run("MaximumUI clothing kind, sides and enabled state preserve behavior", KindAndSides);
        run("MaximumUI clothing queued edits cannot cross selection or coordinate changes", StaleEdits);
        run("MaximumUI clothing add, copy, reorder, select and delete use real operations", ListActions);
        run("MaximumUI clothing binding and scope save all fields to the prefixed path", BindingAndSave);
        run("MaximumUI clothing imports clone identities and keep the source unchanged", Import);
        run("MaximumUI clothing importer preserves selection across refreshed snapshots", ImportSelection);
        run("MaximumUI clothing layout keeps save actions and scroll view inside small windows", Layout);
        run("MaximumUI clothing empty editor still exposes creation and import", EmptyEditor);
        run("MaximumUI clothing continuous edits reuse catalog and import snapshots", CachedEdits);
        run("MaximumUI clothing catalog and import caches follow their actual sources", CacheInvalidation);
        run("MaximumUI clothing failed reload retains cached valid import data", CacheAfterFailedReload);
    }

    private sealed class Fixture : IDisposable {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "clothing-ui-" + Guid.NewGuid().ToString("N"));
        private readonly string previousPath = BepInEx.Paths.ConfigPath;
        private readonly CompressionLibraryStore previousStore = ClothingCompressionPlugin.Library;
        internal readonly CompressionController Controller;
        internal readonly CompressionEditorView View;
        internal int Invalidations;
        internal bool Visible = true;
        internal Fixture(bool add = true) {
            Directory.CreateDirectory(directory);
            BepInEx.Paths.ConfigPath = directory;
            ClothingCompressionPlugin.Library = new CompressionLibraryStore();
            ChaControl character = new ChaControl();
            Controller = new CompressionController { ChaControl = character, ChaFileControl = character.chaFile };
            View = new CompressionEditorView("Clothing editor") {
                Controller = Controller,
                OnInvalidated = () => Invalidations++,
                OnVisibleChange = value => Visible = value
            };
            if (add) Controller.Add(DeformationKind.Thigh);
        }
        public void Dispose() {
            ClothingCompressionPlugin.Library = previousStore;
            BepInEx.Paths.ConfigPath = previousPath;
            Directory.Delete(directory, true);
        }
    }

    private static void Parameters() {
        using (Fixture f = new Fixture()) {
            Widget root = f.View.Build(null);
            Check(All<InputFloat>(root).Count() == 8, "all eight original leg parameters need editable numeric fields");
            InputFloat depth = Find<InputFloat>(root, ":depth");
            Near(0.6f, depth.Value, "centimetre display");
            depth.OnChange(1.25f);
            Near(0.0125f, f.Controller.Selected.Depth, "input converts centimetres back to stored units");
            Slider clearance = Find<Slider>(root, ":clearance-x:slider");
            clearance.OnChange(0.25f);
            Near(0.0025f, f.Controller.Selected.GarmentClearanceX, "slider uses the same conversion");
            Find<InputFloat>(root, ":width").OnChange(0.12f);
            Near(0.12f, f.Controller.Selected.Width, "normalized fields must not be scaled");
            Check(f.Controller.Dirty && f.Controller.StartedCount >= 4, "edits schedule the real deformation rebuild");
            Check(f.Invalidations == 3, "the retained view invalidates once for each input edit");
        }
    }

    private static void KindAndSides() {
        using (Fixture f = new Fixture()) {
            Widget root = f.View.Build(null);
            Find<ComboBox<DeformationKind>>(root, ":kind").OnChange(DeformationKind.UpperArm);
            Near(0.55f, f.Controller.Selected.Position, "changing kind resets its location as before");
            Near(0.10f, f.Controller.Selected.Width, "changing kind resets its width as before");
            Near(0f, f.Controller.Selected.DistalBulge, "arm kinds do not retain a hidden distal bulge");
            root = f.View.Build(null);
            Check(All<InputFloat>(root).Count() == 7, "arm editor only exposes applicable parameters");
            Find<Checkbox>(root, ":left").OnChange(false);
            Find<Checkbox>(root, ":right").OnChange(true);
            Find<Toggle>(root, ":enabled").OnChange(false);
            Check(!f.Controller.Selected.Left && f.Controller.Selected.Right && !f.Controller.Selected.Enabled, "side and enable callbacks reach the selected entry");
        }
    }

    private static void StaleEdits() {
        using (Fixture f = new Fixture()) {
            DeformationEntry first = f.Controller.Selected;
            InputFloat old = Find<InputFloat>(f.View.Build(null), ":depth");
            f.Controller.Add(DeformationKind.Thigh);
            old.OnChange(8f);
            Near(0.006f, first.Depth, "an edit from the former selection must not mutate it after switching");
            InputFloat current = Find<InputFloat>(f.View.Build(null), ":depth");
            f.Controller.ChaControl.fileStatus.coordinateType = 1;
            current.OnChange(8f);
            Near(0.006f, f.Controller.Selected.Depth, "an edit cannot cross a coordinate switch");
            InputFloat next = Find<InputFloat>(f.View.Build(null), ":depth");
            Check(!Equals(current.Key, next.Key), "numeric input state must be distinct after a coordinate switch");
            f.View.IsCurrentController = candidate => false;
            next.OnChange(8f);
            Near(0.006f, f.Controller.Selected.Depth, "a stale Studio/Maker selection must not receive queued edits");
        }
    }

    private static void ListActions() {
        using (Fixture f = new Fixture()) {
            Widget root = f.View.Build(null);
            Find<ComboBox<DeformationKind>>(root, "new-kind").OnChange(DeformationKind.Knee);
            Find<Button>(root, "add").OnClick();
            Check(f.Controller.Entries.Count == 2 && f.Controller.Selected.Kind == DeformationKind.Knee, "new kind and add action");
            string originalId = f.Controller.Selected.Id;
            Find<Button>(f.View.Build(null), "duplicate").OnClick();
            Check(f.Controller.Entries.Count == 3 && f.Controller.Selected.Id != originalId, "copy must create a distinct shape identity");
            DeformationEntry copied = f.Controller.Selected;
            Find<Button>(f.View.Build(null), "move-up").OnClick();
            Check(f.Controller.SelectedIndex == 1 && ReferenceEquals(f.Controller.Entries[1], copied), "reorder keeps the selected entry");
            VirtualList list = Find<VirtualList>(f.View.Build(null), "entries");
            ((Selectable)list.BuildItem(0)).OnChange(true);
            Check(f.Controller.SelectedIndex == 0, "virtual row selection");
            Find<Button>(f.View.Build(null), "delete").OnClick();
            Check(f.Controller.Entries.Count == 2 && ReferenceEquals(f.Controller.Entries[0], copied), "deleting updates the actual controller list");
        }
    }

    private static void BindingAndSave() {
        using (Fixture f = new Fixture()) {
            ItemDescriptor item = new ItemDescriptor { Kind = BoundItemKind.Accessory, Category = 121, Id = 40, Slot = 2,
                Name = "Fixture binding", ModGuid = "example.fixture", OriginalId = 7, Property = "Accessory" };
            f.View.BindingItems.Add(item);
            Widget root = f.View.Build(null);
            Find<SearchableComboBox<ItemDescriptor>>(root, ":binding").OnChange(item);
            Find<ComboBox<SaveScope>>(f.View.Build(null), ":scope").OnChange(SaveScope.ItemOnly);
            Find<Button>(f.View.Build(null), "save").OnClick();
            Check(!f.Controller.Dirty && ClothingCompressionPlugin.Library.Reload(), "save goes through the production store");
            Check(Path.GetFileName(ClothingCompressionPlugin.Library.Path) == "MaxMixAlex.ClothingCompression.xml", "prefixed production path");
            SavedProfile saved = ClothingCompressionPlugin.Library.Data.Profiles.Single();
            Check(saved.Scope == SaveScope.ItemOnly && saved.Entries.Single().BindingModGuid == "example.fixture" && saved.Entries.Single().PreferredSlot == 2,
                "binding identifiers and save scope survive actual XML persistence");
            Find<Button>(f.View.Build(null), "unbind").OnClick();
            Find<Button>(f.View.Build(null), "save").OnClick();
            Check(ClothingCompressionPlugin.Library.Data.Profiles.Count == 0, "unbinding then saving removes the previously persisted shape");
        }
    }

    private static void Import() {
        using (Fixture f = new Fixture(false)) {
            DeformationEntry source = DeformationEntry.Create(DeformationKind.UpperArm);
            source.BindingName = "Import fixture";
            f.View.ImportEntries.Add(source);
            Widget root = f.View.Build(null);
            Check(!Find<ComboBox<int>>(root, "import-coordinate").Options.Contains(f.Controller.CoordinateIndex), "current coordinate must not be offered as an import source");
            Find<Button>(root, "import-selected").OnClick();
            Check(f.Controller.Entries.Count == 1 && f.Controller.Selected.Kind == DeformationKind.UpperArm && f.Controller.Selected.Id != source.Id,
                "import calls the real clone operation");
            f.Controller.Selected.Depth = 0.04f;
            Near(0.003f, source.Depth, "the source preset is independent of imported edits");
        }
    }

    private static void ImportSelection() {
        using (Fixture f = new Fixture()) {
            DeformationEntry first = DeformationEntry.Create(DeformationKind.Thigh);
            DeformationEntry second = DeformationEntry.Create(DeformationKind.Knee);
            f.View.ImportEntries.Add(first); f.View.ImportEntries.Add(second);
            Find<SearchableComboBox<DeformationEntry>>(f.View.Build(null), "import-entry").OnChange(second);
            f.View.ImportEntries = new List<DeformationEntry> { first.Clone(), second.Clone() };
            SearchableComboBox<DeformationEntry> selector = Find<SearchableComboBox<DeformationEntry>>(f.View.Build(null), "import-entry");
            Check(selector.Value.Id == second.Id, "reloading the source snapshot must not reset selection to its first row");
        }
    }

    private static void Layout() {
        using (Fixture f = new Fixture()) {
            foreach (MuiVec2 size in new[] { new MuiVec2(680f, 460f), new MuiVec2(900f, 720f) }) {
                Widget root = f.View.Build(null);
                LayoutNode layout = new LayoutEngine().Layout(root, new MuiRect(0f, 0f, size.X, size.Y));
                LayoutNode save = Nodes(layout).Single(node => Equals(node.Widget.Key, "save"));
                LayoutNode scroll = Nodes(layout).Single(node => Equals(node.Widget.Key, "editor-scroll"));
                Check(save.Rect.Right <= size.X && save.Rect.Bottom <= size.Y && save.Rect.Height >= 22f,
                    "save must remain visible at " + size.X + "x" + size.Y);
                Check(scroll.Rect.Height > 100f && scroll.Rect.Bottom < save.Rect.Bottom, "the editor must receive a usable scrolling viewport");
                Check(scroll.Children[0].Rect.Height > scroll.Rect.Height, "all parameter content remains reachable through scrolling");
            }
        }
    }

    private static void EmptyEditor() {
        using (Fixture f = new Fixture(false)) {
            Widget root = f.View.Build(null);
            Check(Find<Button>(root, "add").Enabled && All<ComboBox<int>>(root).Any(), "empty editor preserves add/import paths");
            Check(!Find<Button>(root, "unbind").Enabled, "unbound empty editor cannot run an unbind action");
            f.View.Controller = null;
            root = f.View.Build(null);
            Check(All<HelpBox>(root).Any() && !All<InputFloat>(root).Any(), "missing character has a readable empty state");
            ((Window)root).OnVisibleChange(false);
            Check(!f.Visible, "the close button must update the owning mount's visibility");
        }
    }

    private sealed class CachedFixture : IDisposable {
        private readonly Fixture fixture = new Fixture();
        private readonly Action<CompressionController> previousRefresh = ClothingCompressionPlugin.Instance.OnRefreshMakerControls;
        private readonly float previousTime = UnityEngine.Time.realtimeSinceStartup;
        private readonly int previousFrame = UnityEngine.Time.frameCount;
        internal CompressionController Selected;
        internal readonly CompressionGui Gui;
        internal readonly CompressionEditorData MakerData = new CompressionEditorData();
        internal CompressionController Controller { get { return fixture.Controller; } }

        internal CachedFixture() {
            Selected = Controller;
            for (int i = 0; i < 20; i++) {
                Controller.ChaControl.nowCoordinate.accessory.parts[i].type = 121;
                Controller.ChaControl.nowCoordinate.accessory.parts[i].id = 100 + i;
                Controller.ChaControl.objAccessory[i] = new UnityEngine.GameObject();
            }
            DeformationEntry source = DeformationEntry.Create(DeformationKind.Knee);
            ItemCatalog.Bind(source, new ItemDescriptor { Kind = BoundItemKind.Accessory, Category = 121, Id = 100, Slot = 0, Name = "Anonymous source" });
            ClothingCompressionPlugin.Library.SaveEntries(Controller.MakeCoordinateKey(1), new[] { source });
            Gui = new CompressionGui(() => Selected) { Visible = true };
            ClothingCompressionPlugin.Instance.OnRefreshMakerControls = current => {
                Gui.Invalidate();
                // The actual native Maker adapter uses this same production cache;
                // the host only replaces the external KKAPI controls around it.
                MakerData.Refresh(current, 1);
            };
            MakerData.Refresh(Controller, 1);
            Gui.Update();
        }

        public void Dispose() {
            ClothingCompressionPlugin.Instance.OnRefreshMakerControls = previousRefresh;
            Gui.Dispose();
            UnityEngine.Time.realtimeSinceStartup = previousTime;
            UnityEngine.Time.frameCount = previousFrame;
            fixture.Dispose();
        }
    }

    private static void CachedEdits() {
        using (CachedFixture f = new CachedFixture()) {
            List<ItemDescriptor> items = f.Gui.View.BindingItems;
            List<DeformationEntry> imports = f.Gui.View.ImportEntries;
            int lookups = f.Controller.ChaControl.lstCtrl.Lookups;
            int rendererScans = UnityEngine.GameObject.ComponentsInChildrenCalls;
            int rebuilds = f.Controller.StartedCount;
            for (int i = 0; i < 200; i++) {
                UnityEngine.Time.frameCount++;
                UnityEngine.Time.realtimeSinceStartup += 1f / 60f;
                Find<Slider>(f.Gui.View.Build(null), ":depth:slider").OnChange(0.1f + i * 0.01f);
                f.Gui.Update();
            }
            Check(f.Controller.StartedCount == rebuilds + 200, "each slider edit still schedules the actual deformation update");
            Check(f.Controller.ChaControl.lstCtrl.Lookups == lookups && UnityEngine.GameObject.ComponentsInChildrenCalls == rendererScans,
                "200 edits must not repeat name/AutoResolver or renderer catalog scans");
            Check(ReferenceEquals(items, f.Gui.View.BindingItems) && ReferenceEquals(imports, f.Gui.View.ImportEntries),
                "numeric rebuilds must reuse the catalog and cloned import list");
            Check(f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 1 &&
                f.MakerData.CatalogRefreshCount == 1 && f.MakerData.ImportRefreshCount == 1,
                "both advanced and native Maker data caches remain at their initial refresh");
            Console.WriteLine("EDITOR CACHE WORK: 200 edits × 20 equipped items: 0 additional UI catalog lookups or import copies; 200 deformation updates scheduled.");
            f.Controller.Add(DeformationKind.Knee);
            f.Gui.Update();
            Find<ComboBox<DeformationKind>>(f.Gui.View.Build(null), ":kind").OnChange(DeformationKind.UpperArm);
            Find<SearchableComboBox<ItemDescriptor>>(f.Gui.View.Build(null), ":binding").OnChange(items[0]);
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 1 &&
                f.MakerData.CatalogRefreshCount == 1 && f.MakerData.ImportRefreshCount == 1,
                "entry selection/kind/binding edits change the editor without replacing its data sources");
        }
    }

    private static void CacheInvalidation() {
        using (CachedFixture f = new CachedFixture()) {
            Check(f.Gui.View.ImportEntries.Count == 1, "initial source comes from the real saved library");
            Find<ComboBox<int>>(f.Gui.View.Build(null), "import-coordinate").OnChange(2);
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 2 && f.Gui.View.ImportEntries.Count == 0,
                "changing import coordinate only refreshes that source");
            DeformationEntry source = DeformationEntry.Create(DeformationKind.Knee);
            ItemCatalog.Bind(source, f.Gui.View.BindingItems[0]);
            ClothingCompressionPlugin.Library.SaveEntries(f.Controller.MakeCoordinateKey(2), new[] { source });
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 3 && f.Gui.View.ImportEntries.Count == 1,
                "a successful save invalidates imports without re-enumerating equipment");
            Check(ClothingCompressionPlugin.Library.Reload(), "saved fixture reload");
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 4,
                "successful reload replaces the import snapshot once");

            int previousFrame = UnityEngine.Time.frameCount;
            try {
                UnityEngine.Time.frameCount = ((previousFrame / 15) + 1) * 15;
                f.Controller.ChaControl.nowCoordinate.accessory.parts[0].id = 999;
                typeof(CompressionController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Controller, null);
                f.Gui.Update();
            } finally { UnityEngine.Time.frameCount = previousFrame; }
            Check(f.Gui.Data.CatalogRefreshCount == 2 && f.Gui.Data.ImportRefreshCount == 4 && f.Gui.View.BindingItems[0].Id == 999,
                "controller equipment detection refreshes catalog even with unsaved entry edits");
            f.Controller.ChaControl.fileStatus.coordinateType = 2;
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 3 && f.Gui.Data.ImportRefreshCount == 5 && f.Gui.View.ImportCoordinate == 3,
                "a coordinate switch refreshes both data sources and excludes itself from importing");
            ChaControl another = new ChaControl();
            f.Selected = new CompressionController { ChaControl = another, ChaFileControl = another.chaFile };
            UnityEngine.Time.realtimeSinceStartup += 1f;
            f.Gui.Update();
            Check(f.Gui.Data.CatalogRefreshCount == 4 && f.Gui.Data.ImportRefreshCount == 6 && ReferenceEquals(f.Gui.View.Controller, f.Selected),
                "a character switch cannot reuse the previous character's snapshots");
            f.Selected = null;
            UnityEngine.Time.realtimeSinceStartup += 1f;
            f.Gui.Update();
            Check(f.Gui.View.Controller == null && f.Gui.View.BindingItems.Count == 0 && f.Gui.View.ImportEntries.Count == 0,
                "deselecting clears the previous character and data");
        }
    }

    private static void CacheAfterFailedReload() {
        using (CachedFixture f = new CachedFixture()) {
            List<DeformationEntry> imports = f.Gui.View.ImportEntries;
            File.WriteAllText(ClothingCompressionPlugin.Library.Path, "<broken>");
            f.Controller.ReloadFromDisk();
            f.Gui.Update();
            Check(ReferenceEquals(imports, f.Gui.View.ImportEntries) && imports.Count == 1 &&
                f.Gui.Data.CatalogRefreshCount == 1 && f.Gui.Data.ImportRefreshCount == 1,
                "a rejected XML document cannot clear or replace the last valid cached source");
            Check(!string.IsNullOrEmpty(f.Controller.Status), "the editor still exposes the reload diagnostic");
        }
    }

    private static IEnumerable<T> All<T>(Widget widget) where T : Widget {
        if (widget is T) yield return (T)widget;
        foreach (Widget child in widget.Children) foreach (T match in All<T>(child)) yield return match;
    }
    private static T Find<T>(Widget root, string suffix) where T : Widget {
        return All<T>(root).Single(widget => widget.Key != null && widget.Key.ToString().EndsWith(suffix, StringComparison.Ordinal));
    }
    private static IEnumerable<LayoutNode> Nodes(LayoutNode node) {
        yield return node;
        foreach (LayoutNode child in node.Children) foreach (LayoutNode item in Nodes(child)) yield return item;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(float expected, float actual, string message) {
        if (Math.Abs(expected - actual) > 0.000001f) throw new Exception(message + ": " + actual + " != " + expected);
    }
}
