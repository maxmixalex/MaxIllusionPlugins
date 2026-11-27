using System;
using ClothingControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class WardrobeViewTests {
        internal const int TestCount = 10;

        internal static void RunAll() {
            MinimumWindowRetainsToolbar();
            SelectorsAndBusyState();
            DisplayEqualityIncludesBusinessIdentity();
            FiltersAndBatchOperateOnDisplayedRows();
            GarmentPageDispatchesNativeCommands();
            LinkedOnePieceBottomRemainsOperable();
            NineGarmentRowsFitWindowWithoutInternalScroll();
            ClothingStateOptionsInvalidateTheRetainedUi();
            FreeHDisablesFullCoordinateReloadWithoutDisablingClothing();
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
            view.Model.Page = 1; // Accessory list is still intentionally virtualized.
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
            view.Model.Page = 1; // Accessory page follows clothing.
            int selected = -1, writes = 0;
            view.Model.SelectTarget = i => selected = i;
            view.Model.Rows[0].Change = delegate { writes++; };
            Widget window = view.Build(null);
            ((ArrowButton)Find(window, "wardrobe-target-previous")).OnClick();
            Require(selected == 1, "Character selector wraps backward.");
            ((ArrowButton)Find(window, "wardrobe-target-next")).OnClick();
            Require(selected == 1, "Character selector advances independently.");
            ((Button)Find(window, "wardrobe-target-current")).OnClick();
            Require(selected == 1, "The selected value is a next-choice button.");
            view.Model.Busy = true;
            window = view.Build(null);
            Require(!Find(window, "wardrobe-target-current").Enabled && !Find(window, "wardrobe-apply").Enabled &&
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
            m.Page = 1;
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
            m.Page = 0; m.ShoeMode = 0;
            int writes = 0, restored = 0, shoe = -1;
            m.ChangeShoeMode = value => shoe = value;
            m.RestoreClothing = () => restored++;
            m.Garments.Add(new WardrobeRow { Slot = 0, Label = "Gloves", Item = 12,
                State = 1, Visible = true, AllowedStates = new[] { 0, 1, 3 },
                ChangeState = value => { Require(value == 3, "Explicit native garment state."); writes++; }
            });
            WardrobeView view = new WardrobeView { Model = m };
            Widget root = view.Build(null);
            ((SegmentedControl<int>)Find(root, "wardrobe-shoe-mode")).OnChange(1);
            ((Button)Find(root, "wardrobe-restore-clothing")).OnClick();
            ((ArrowButton)Find(root, "garment-state-0-next")).OnClick();
            Require(shoe == 1 && restored == 1 && writes == 1, "Garment page dispatches all three actions.");
            m.Busy = true; root = view.Build(null);
            Require(!Find(root, "wardrobe-restore-clothing").Enabled && !Find(root, "wardrobe-shoe-mode").Enabled,
                "Pending load blocks garment editing.");
            ((Button)Find(root, "wardrobe-restore-clothing")).OnClick();
            ((ArrowButton)Find(root, "garment-state-0-next")).OnClick();
            Require(restored == 1 && writes == 1, "Busy callbacks cannot write.");
        }

        private static void LinkedOnePieceBottomRemainsOperable() {
            // A genuine empty lower slot is distinct from a one-piece
            // top that supplies the game's native bottom-state channel.
            WardrobeViewModel model = Create();
            model.Page = 0;
            model.Garments.Add(new WardrobeRow {
                Slot = 0, Label = "上衣", State = 0, Item = 120,
                AllowedStates = new[] {0, 1, 3}, ChangeState = delegate {}
            });
            model.Garments.Add(new WardrobeRow {
                Slot = 1, Label = "下装", State = -1, Item = 0,
                AllowedStates = new int[0]
            });
            Widget widget = new WardrobeView { Model = model }.Build(null);
            Require(Find(widget, "garment-not-equipped-1") != null,
                "Truly absent bottom may show an unequipped placeholder");

            int requested = -1;
            model.Garments[1].Item = 120; // native linked top identity
            model.Garments[1].Label = "下装（连体）";
            model.Garments[1].State = 1;
            model.Garments[1].AllowedStates = new[] {0, 1, 3};
            model.Garments[1].ChangeState = value => requested = value;
            widget = new WardrobeView { Model = model }.Build(null);
            Require(Find(widget, "garment-not-equipped-1") == null &&
                Find(widget, "garment-not-changeable-1") == null &&
                Find(widget, "garment-state-1-current") is Button,
                "One-piece bottom must retain a real cycling control");
            ((ArrowButton)Find(widget, "garment-state-1-next")).OnClick();
            Require(requested == 3,
                "One-piece lower half dispatches native bottom-state change");
            Require(Find(widget, "wardrobe-restore-clothing") != null,
                "Cross-slot state does not displace regular wardrobe controls");
        }

        private static void NineGarmentRowsFitWindowWithoutInternalScroll() {
            WardrobeViewModel m = Create();
            m.Page = 0;
            m.ShoeMode = 0;
            m.ChangeShoeMode = delegate { };
            m.RestoreClothing = delegate { };
            for (int i = 0; i < 9; i++) {
                m.Garments.Add(new WardrobeRow {
                    Slot = i, Label = "garment-" + i, Item = i + 1, State = 0,
                    AllowedStates = new[] { 0, 1, 2, 3 }, ChangeState = delegate { }
                });
            }
            Window window = (Window)new WardrobeView { Model = m }.Build(null);
            Require(window.Height >= 900f && window.MinHeight >= 900f,
                "Default and minimum clothing window heights fit all nine rows.");
            Require(((SegmentedControl<int>)Find(window, "wardrobe-page")).GetText(0) == "服装",
                "The first tab is Clothing rather than Accessories.");
            Require(Find(window, "wardrobe-garment-rows") != null &&
                Find(window, "wardrobe-garments") == null,
                "All nine garment rows render normally, with no independent virtual scrollbar.");
            Require(Find(window, "wardrobe-target-current") is Button &&
                Find(window, "wardrobe-coordinate-current") is Button &&
                Find(window, "wardrobe-preset-current") is Button &&
                Find(window, "garment-state-0-current") is Button,
                "Every main selector cycles with buttons instead of dropdowns.");
            LayoutNode tree = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(ThemePalettes.MetroLight()))
                .Layout(window, new MuiRect(0, 0, window.Width, window.Height));
            LayoutNode first = FindNode(tree, "garment-0");
            LayoutNode last = FindNode(tree, "garment-8");
            LayoutNode shoe = FindNode(tree, "wardrobe-shoe-mode");
            LayoutNode restore = FindNode(tree, "wardrobe-restore-clothing");
            LayoutNode presets = FindNode(tree, "wardrobe-preset-row");
            Require(first != null && last != null && shoe != null && restore != null &&
                presets != null, "Nine garments, shoe mode, restore and presets all exist.");
            Require(last.Rect.Y > first.Rect.Y && shoe.Rect.Y >= last.Rect.Bottom &&
                restore.Rect.Y >= shoe.Rect.Bottom && presets.Rect.Y >= restore.Rect.Bottom,
                "Shoe mode and Restore full outfit follow all garment rows, ahead of presets.");
            Require(presets.Rect.Bottom <= window.Height,
                "The taller default window fits all garment rows and the lower toolbar.");
            LayoutNode minimum = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(ThemePalettes.MetroLight()))
                .Layout(window, new MuiRect(0, 0, window.MinWidth, window.MinHeight));
            LayoutNode minLast = FindNode(minimum, "garment-8");
            LayoutNode minShoe = FindNode(minimum, "wardrobe-shoe-mode");
            LayoutNode minRestore = FindNode(minimum, "wardrobe-restore-clothing");
            LayoutNode minPresets = FindNode(minimum, "wardrobe-preset-row");
            Require(minLast != null && minShoe != null && minRestore != null &&
                minPresets != null && minShoe.Rect.Y >= minLast.Rect.Bottom &&
                minRestore.Rect.Y >= minShoe.Rect.Bottom &&
                minPresets.Rect.Y >= minRestore.Rect.Bottom &&
                minPresets.Rect.Bottom <= window.MinHeight,
                "Minimum window size must not overlap the last garment, shoe, restore and preset controls.");
        }

        private static void ClothingStateOptionsInvalidateTheRetainedUi() {
            WardrobeViewModel a = Create(), b = Create();
            a.Garments.Add(new WardrobeRow {
                Slot = 0, Item = 42, Label = "top", State = 1,
                AllowedStates = new[] { 0, 1, 2, 3 }, ChangeState = delegate { }
            });
            b.Garments.Add(new WardrobeRow {
                Slot = 0, Item = 42, Label = "top", State = 1,
                AllowedStates = new[] { 0, 1, 2, 3 }, ChangeState = delegate { }
            });
            Require(a.SameDisplayAs(b), "Identical native garment values retain widgets.");
            b.Garments[0].State = 2;
            Require(!a.SameDisplayAs(b), "Changing a partial clothing state invalidates the view.");
            b.Garments[0].State = 1;
            b.Garments[0].AllowedStates = new[] { 0, 3 };
            Require(!a.SameDisplayAs(b), "Native capabilities changes invalidate options.");
        }

        private static void FreeHDisablesFullCoordinateReloadWithoutDisablingClothing() {
            WardrobeViewModel m = Create();
            m.CanChangeCoordinate = false;
            m.Page = 0;
            m.Garments.Add(new WardrobeRow {
                Slot = 0, State = 0, Item = 42, Label = "top",
                AllowedStates = new[] { 0, 1, 3 }, ChangeState = delegate { }
            });
            WardrobeView view = new WardrobeView { Model = m };
            Widget widget = view.Build(null);
            Require(!Find(widget, "wardrobe-coordinate-current").Enabled &&
                !Find(widget, "wardrobe-coordinate-previous").Enabled &&
                !Find(widget, "wardrobe-coordinate-next").Enabled,
                "Free H must not reload a whole coordinate during animation.");
            Require(Find(widget, "wardrobe-garments") == null &&
                Find(widget, "garment-state-0-current").Enabled &&
                Find(widget, "wardrobe-apply").Enabled,
                "Free H still allows native garment edits and same-coordinate presets.");
            WardrobeViewModel copy = Create();
            Require(!m.SameDisplayAs(copy), "H-scene coordinate restriction must invalidate retained UI.");
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
            ((Button)Find(root, "wardrobe-preset-scope")).OnClick();
            Require(scope == 0, "Scope selection has its own callback.");
            model.Busy = true;
            root = view.Build(null);
            Require(!Find(root, "wardrobe-preset-scope").Enabled && !Find(root, "wardrobe-apply").Enabled,
                "Batch blocks scope and preset changes.");
            ((Button)Find(root, "wardrobe-preset-scope")).OnClick();
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
