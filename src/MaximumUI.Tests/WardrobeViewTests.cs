using System;
using ClothingControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class WardrobeViewTests {
        internal const int TestCount = 6;

        internal static void RunAll() {
            MinimumWindowRetainsToolbar();
            SelectorsAndBusyState();
            DisplayEqualityIncludesBusinessIdentity();
            FiltersAndBatchOperateOnDisplayedRows();
            GarmentPageDispatchesNativeCommands();
            SceneScopeAndCancelAreIndependent();
        }

        private static WardrobeViewModel Create() {
            var model = new WardrobeViewModel {
                Version = "test", Targets = new[] { "A", "B" }, Coordinates = new[] { "Casual", "Travel" },
                Presets = new[] { "A", "B" }, TargetIndex = 0, CoordinateIndex = 0, PresetIndex = 0,
                HasTarget = true, CanWrite = true, Status = "Ready"
            };
            for (int i = 0; i < 48; i++) model.Rows.Add(new WardrobeRow {
                Slot = i, Type = 121, Item = i + 1, Label = "Accessory " + i
            });
            return model;
        }

        private static void MinimumWindowRetainsToolbar() {
            WardrobeView view = new WardrobeView { Model = Create() };
            foreach (bool error in new[] { false, true }) {
                view.Model.CanWrite = !error;
                view.Model.Error = error ? new string('x', 180) : null;
                Window window = (Window)view.Build(null);
                LayoutNode tree = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(ThemePalettes.MetroLight()))
                    .Layout(window, new MuiRect(0, 0, window.MinWidth, window.MinHeight));
                LayoutNode rows = FindNode(tree, "wardrobe-accessories");
                LayoutNode presets = FindNode(tree, "wardrobe-preset-row");
                LayoutNode status = FindNode(tree, "wardrobe-status");
                Require(rows != null && rows.Rect.Height >= 120f && rows.Children.Count == 0, "Virtual list has a useful bounded viewport.");
                Require(presets.Rect.Y > rows.Rect.Bottom && status.Rect.Bottom <= window.MinHeight, "Preset toolbar stays below the list.");
                if (error) {
                    LayoutNode recovery = FindNode(tree, "wardrobe-error-scroll");
                    Require(recovery != null && recovery.Rect.Y >= status.Rect.Bottom && recovery.Rect.Bottom <= window.MinHeight,
                        "Error details must not push the toolbar offscreen.");
                }
            }
        }

        private static void SelectorsAndBusyState() {
            WardrobeView view = new WardrobeView { Model = Create() };
            int selected = -1, writes = 0;
            view.Model.SelectTarget = i => selected = i;
            view.Model.Rows[0].Change = delegate { writes++; };
            Widget window = view.Build(null);
            ((ArrowButton)Find(window, "wardrobe-target-previous")).OnClick();
            Require(selected == 1, "Character selector wraps backward.");
            ((ArrowButton)Find(window, "wardrobe-target-next")).OnClick();
            Require(selected == 1, "Character selector advances independently.");
            view.Model.Busy = true;
            window = view.Build(null);
            Require(!Find(window, "wardrobe-target").Enabled && !Find(window, "wardrobe-apply").Enabled &&
                !Find(window, "wardrobe-create").Enabled && !Find(window, "wardrobe-reload").Enabled, "Pending load disables commands.");
            Widget row = ((VirtualList)Find(window, "wardrobe-accessories")).BuildItem(0);
            ((Checkbox)row).OnChange(true);
            Require(!row.Enabled && writes == 0, "A stale row callback cannot edit a busy view.");
            view.Model.Busy = false; view.Model.CanWrite = false;
            window = view.Build(null);
            Require(Find(window, "wardrobe-apply").Enabled && Find(window, "wardrobe-reload").Enabled &&
                !Find(window, "wardrobe-create").Enabled && !Find(window, "wardrobe-delete").Enabled, "XML recovery preserves non-writing operations.");
        }

        private static void DisplayEqualityIncludesBusinessIdentity() {
            WardrobeViewModel a = Create(), b = Create();
            Require(a.SameDisplayAs(b), "Identical display can keep its retained controls.");
            b.Rows[0].Item = 200;
            Require(!a.SameDisplayAs(b), "A replacement item invalidates the row even if the label matches.");
            b = Create(); b.Revision++;
            Require(!a.SameDisplayAs(b), "A changed character generation invalidates old callbacks.");
        }

        private static void FiltersAndBatchOperateOnDisplayedRows() {
            WardrobeViewModel m = Create();
            m.Rows[0].Group = 0; m.Rows[1].Group = 1; m.Rows[2].Group = 2;
            m.AccessoryGroup = 2;
            Require(m.FilteredAccessories().Count == 1 && m.FilteredAccessories()[0].Slot == 1, "Secondary group filters exact rows.");
            m.AccessoryQuery = "2";
            Require(m.FilteredAccessories().Count == 1 && m.FilteredAccessories()[0].Slot == 1, "Search accepts displayed slot number.");
            int calls = 0;
            m.SetFilteredAccessories = value => { Require(!value, "Batch hide uses false."); calls++; };
            WardrobeView view = new WardrobeView { Model = m };
            Widget root = view.Build(null);
            Require(((VirtualList)Find(root, "wardrobe-accessories")).ItemCount == 1, "Only filtered rows are rendered.");
            ((Button)Find(root, "wardrobe-hide-filtered")).OnClick();
            Require(calls == 1, "Batch action is explicit.");
            m.AccessoryQuery = "not found";
            root = view.Build(null);
            Require(!Find(root, "wardrobe-hide-filtered").Enabled, "Empty filter disables batch writes.");
        }

        private static void GarmentPageDispatchesNativeCommands() {
            WardrobeViewModel m = Create();
            m.Page = 1; m.ShoeMode = 0;
            int writes = 0, restored = 0, shoe = -1;
            m.ChangeShoeMode = value => shoe = value;
            m.RestoreClothing = () => restored++;
            m.Garments.Add(new WardrobeRow { Slot = 0, Label = "Gloves", Item = 12, Visible = true, Change = value => {
                Require(!value, "Explicit garment change."); writes++;
            }});
            WardrobeView view = new WardrobeView { Model = m };
            Widget root = view.Build(null);
            ((SegmentedControl<int>)Find(root, "wardrobe-shoe-mode")).OnChange(1);
            ((Button)Find(root, "wardrobe-restore-clothing")).OnClick();
            ((Checkbox)((VirtualList)Find(root, "wardrobe-garments")).BuildItem(0)).OnChange(false);
            Require(shoe == 1 && restored == 1 && writes == 1, "Garment page dispatches all three actions.");
            m.Busy = true; root = view.Build(null);
            Require(!Find(root, "wardrobe-restore-clothing").Enabled && !Find(root, "wardrobe-shoe-mode").Enabled,
                "Pending load blocks garment editing.");
            ((Button)Find(root, "wardrobe-restore-clothing")).OnClick();
            ((Checkbox)((VirtualList)Find(root, "wardrobe-garments")).BuildItem(0)).OnChange(false);
            Require(restored == 1 && writes == 1, "Busy callbacks cannot write.");
        }

        private static void SceneScopeAndCancelAreIndependent() {
            WardrobeViewModel model = Create();
            model.CanScenePresets = true; model.HasSceneTargets = true; model.HasTarget = false;
            model.Scope = 1;
            int scope = -1, cancelled = 0;
            model.ChangeScope = value => scope = value;
            model.Cancel = () => cancelled++;
            WardrobeView view = new WardrobeView { Model = model };
            Widget root = view.Build(null);
            Require(Find(root, "wardrobe-apply").Enabled && Find(root, "wardrobe-create").Enabled,
                "Scene presets do not depend on a selected character.");
            ((ComboBox<int>)Find(root, "wardrobe-preset-scope")).OnChange(0);
            Require(scope == 0, "Scope selection has its own callback.");
            model.Busy = true;
            root = view.Build(null);
            Require(!Find(root, "wardrobe-preset-scope").Enabled && !Find(root, "wardrobe-apply").Enabled,
                "Batch blocks scope and preset changes.");
            ((ComboBox<int>)Find(root, "wardrobe-preset-scope")).OnChange(1);
            ((Button)Find(root, "wardrobe-cancel")).OnClick();
            Require(scope == 0 && cancelled == 1, "Cancellation remains available while other commands are blocked.");
            model.Busy = false; model.HasSceneTargets = false;
            root = view.Build(null);
            Require(!Find(root, "wardrobe-create").Enabled && Find(root, "wardrobe-delete").Enabled,
                "Empty scenes allow preset maintenance but not application.");
        }

        private static Widget Find(Widget root, string key) {
            if (Equals(root.Key, key)) return root;
            foreach (Widget child in root.Children) {
                Widget match = Find(child, key);
                if (match != null) return match;
            }
            return null;
        }

        private static LayoutNode FindNode(LayoutNode root, string key) {
            if (Equals(root.Widget.Key, key)) return root;
            foreach (LayoutNode child in root.Children) {
                LayoutNode match = FindNode(child, key);
                if (match != null) return match;
            }
            return null;
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException("Wardrobe view: " + message);
        }
    }
}
