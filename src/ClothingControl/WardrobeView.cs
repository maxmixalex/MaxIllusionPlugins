using System;
using System.Collections.Generic;
using MaximumUI;

namespace ClothingControl {
    internal sealed class WardrobeRow {
        internal string Label;
        internal int Slot, Type, Item, Group, State = -1;
        internal int[] AllowedStates = new int[0];
        internal bool Visible;
        internal Action<bool> Change;
        internal Action<int> ChangeState;
    }

    internal sealed class WardrobeViewModel {
        internal string[] Targets = new string[0], Coordinates = new string[0], Presets = new string[0];
        internal int TargetIndex = -1, CoordinateIndex = -1, PresetIndex = -1;
        internal string Version = string.Empty, Name = string.Empty, Status = string.Empty, Error;
        internal int Revision, Page, AccessoryGroup, ShoeMode = -1;
        internal string AccessoryQuery = string.Empty;
        // The search input is immediate; the applied filter is debounced.
        internal string AccessoryFilter = string.Empty;
        internal bool HighlightOnHover;
        internal bool Busy, CanWrite, HasTarget, CanScenePresets, HasSceneTargets;
        internal bool CanChangeCoordinate = true;
        internal int Scope;
        internal readonly List<WardrobeRow> Rows = new List<WardrobeRow>();
        internal readonly List<WardrobeRow> Garments = new List<WardrobeRow>();
        internal Action<int> SelectTarget, SelectCoordinate, SelectPreset, ChangePage, ChangeAccessoryGroup, ChangeShoeMode;
        internal Action<string> ChangeName, ChangeAccessoryQuery;
        internal Action<bool> ChangeHighlightOnHover;
        internal Action<int, bool> ChangeAccessoryHover;
        internal Action<bool> SetFilteredAccessories;
        internal Action Apply, Create, Overwrite, Delete, Reload, Close, Cancel;
        internal Action<bool> SetAllGarments;
        internal Action<int> ChangeScope;

        internal List<WardrobeRow> FilteredAccessories() {
            List<WardrobeRow> result = new List<WardrobeRow>();
            string query = (AccessoryFilter ?? string.Empty).Trim();
            foreach (WardrobeRow row in Rows) {
                if (AccessoryGroup == 1 && row.Group != 0) continue;
                if (AccessoryGroup == 2 && row.Group != 1) continue;
                if (AccessoryGroup == 3 && (row.Group == 0 || row.Group == 1)) continue;
                if (query.Length > 0 && (row.Label ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (row.Slot + 1).ToString() != query) continue;
                result.Add(row);
            }
            return result;
        }

        private static bool SameRows(List<WardrobeRow> a, List<WardrobeRow> b) {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) {
                WardrobeRow x = a[i], y = b[i];
                if (x.Slot != y.Slot || x.Type != y.Type || x.Item != y.Item || x.Group != y.Group ||
                    x.State != y.State || x.Label != y.Label || x.Visible != y.Visible) return false;
                if (x.AllowedStates.Length != y.AllowedStates.Length) return false;
                for (int k = 0; k < x.AllowedStates.Length; k++)
                    if (x.AllowedStates[k] != y.AllowedStates[k]) return false;
            }
            return true;
        }

        private static bool Same(string[] a, string[] b) {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        internal bool SameDisplayAs(WardrobeViewModel other) {
            if (other == null || Revision != other.Revision || Version != other.Version ||
                Name != other.Name || Status != other.Status || Error != other.Error ||
                Busy != other.Busy || CanWrite != other.CanWrite || HasTarget != other.HasTarget ||
                CanChangeCoordinate != other.CanChangeCoordinate ||
                Scope != other.Scope || CanScenePresets != other.CanScenePresets || HasSceneTargets != other.HasSceneTargets ||
                TargetIndex != other.TargetIndex || CoordinateIndex != other.CoordinateIndex || PresetIndex != other.PresetIndex ||
                Page != other.Page || ShoeMode != other.ShoeMode || AccessoryGroup != other.AccessoryGroup ||
                AccessoryQuery != other.AccessoryQuery ||
                AccessoryFilter != other.AccessoryFilter ||
                HighlightOnHover != other.HighlightOnHover ||
                !Same(Targets, other.Targets) || !Same(Coordinates, other.Coordinates) || !Same(Presets, other.Presets) ||
                !SameRows(Rows, other.Rows) || !SameRows(Garments, other.Garments)) return false;
            return true;
        }
    }

    internal sealed class WardrobeView : View {
        internal WardrobeViewModel Model = new WardrobeViewModel();

        // A compact native MaximumUI cycle selector: left/right arrows plus
        // the currently selected value as a button (clicking it advances).
        // Unlike ComboBox, it never opens a dropdown or steals keyboard focus.
        private static int NextIndex(int selected, int count, int direction) {
            if (count <= 0) return -1;
            if (selected < 0 || selected >= count) return direction < 0 ? count - 1 : 0;
            return (selected + (direction < 0 ? count - 1 : 1)) % count;
        }

        private static HStack CycleButtons(string key, string[] values, int selected,
            Action<int> changed, bool enabled) {
            int count = values == null ? 0 : values.Length;
            bool active = enabled && count > 1 && changed != null;
            string current = selected >= 0 && selected < count ? values[selected] : "无";
            Action<int> choose = i => {
                if (active && i >= 0 && i < count && i != selected) changed(i);
            };
            return new HStack(4f, key + "-cycle") {
                new ArrowButton(ArrowDirection.Left,
                    () => choose(NextIndex(selected, count, -1)),
                    key + "-previous").Enabled(active).Width(28f),
                new Button(current, () => choose(NextIndex(selected, count, 1)),
                    key + "-current").Enabled(active).ExpandWidth(),
                new ArrowButton(ArrowDirection.Right,
                    () => choose(NextIndex(selected, count, 1)),
                    key + "-next").Enabled(active).Width(28f)
            }.Height(32f);
        }

        private static int[] OptionIndices(string[] options) {
            int count = options == null ? 0 : options.Length;
            int[] indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;
            return indices;
        }

        private static Widget DropdownSelector(string label, string key,
            string[] values, int selected, Action<int> changed, bool enabled) {
            string[] options = values ?? new string[0];
            return new HStack(4f, key + "-row") {
                new Label(label).Width(60f),
                new ComboBox<int>("", selected, OptionIndices(options),
                    i => i < 0 || i >= options.Length ? "无" : options[i],
                    changed, key) { MaxVisibleItems = 10 }
                    .Enabled(enabled && options.Length > 0).ExpandWidth()
            }.Height(32f);
        }

        private static Widget Selector(string label, string key, string[] values,
            int selected, Action<int> changed, bool enabled, Widget prefix = null) {
            return new HStack(4f, key + "-row") {
                prefix ?? new Label(label).Width(60f),
                CycleButtons(key, values, selected, changed, enabled).ExpandWidth()
            }.Height(32f);
        }

        public override Widget Build(BuildContext context) {
            WardrobeViewModel m = Model;
            bool canEdit = m.HasTarget && !m.Busy;
            bool hasPreset = m.PresetIndex >= 0 && m.PresetIndex < m.Presets.Length;
            bool canPreset = !m.Busy && (m.Scope == 1 ? m.CanScenePresets && m.HasSceneTargets : m.HasTarget);
            Widget scope = m.CanScenePresets ? new Button(
                m.Scope == 0 ? "角色预设" : "场景预设", () => {
                    if (!m.Busy && m.ChangeScope != null) m.ChangeScope(1 - m.Scope);
                }, "wardrobe-preset-scope").Enabled(!m.Busy).Width(96f) : null;
            Window window = new Window("ClothingControl " + m.Version, 430f, 900f, "clothing-window",
                minWidth: 370f, minHeight: 900f, closable: true,
                onVisibleChange: value => { if (!value && m.Close != null) m.Close(); });
            VStack body = new VStack(6f) {
                DropdownSelector("角色", "wardrobe-target", m.Targets, m.TargetIndex,
                    m.SelectTarget, !m.Busy),
                DropdownSelector("配装", "wardrobe-coordinate", m.Coordinates, m.CoordinateIndex,
                    m.SelectCoordinate, canEdit && m.CanChangeCoordinate),
                new Separator(),
                new SegmentedControl<int>(m.Page, new[] { 0, 1 }, i => i == 0 ? "服装" : "饰品",
                    value => { if (!m.Busy && m.ChangePage != null) m.ChangePage(value); }, "wardrobe-page").Enabled(!m.Busy).Height(32f),
                m.Page == 0 ? BuildGarments(m, canEdit) : BuildAccessories(m, canEdit),
                new Separator(),
                Selector("预设", "wardrobe-preset", m.Presets, m.PresetIndex, m.SelectPreset, !m.Busy, scope),
                new TextField(m.Name, m.ChangeName, "wardrobe-name") { Placeholder = "新预设名称", MaxLength = 80 }.Enabled(m.CanWrite && !m.Busy).Height(32f),
                new HStack(4f) {
                    new Button("应用选中", m.Apply, "wardrobe-apply").Enabled(canPreset && hasPreset).ExpandWidth(),
                    new Button("新建保存", m.Create, "wardrobe-create").Enabled(canPreset && m.CanWrite).ExpandWidth()
                }.Height(32f),
                new HStack(4f) {
                    new Button("覆盖保存", m.Overwrite, "wardrobe-overwrite").Enabled(canPreset && hasPreset && m.CanWrite).ExpandWidth(),
                    new Button("删除选中", m.Delete, "wardrobe-delete").Enabled(!m.Busy && hasPreset && m.CanWrite).ExpandWidth(),
                    new Button("重载 XML", m.Reload, "wardrobe-reload").Enabled(!m.Busy).ExpandWidth()
                }.Height(32f),
                BuildStatus(m)
            };
            if (!string.IsNullOrEmpty(m.Error)) body.Add(new ScrollView("wardrobe-error-scroll") {
                new HelpBox("XML: " + m.Error, HelpBoxType.Error, "wardrobe-error").ExpandWidth()
            }.Height(72f));
            window.Add(body.ExpandHeight());
            return window;
        }

        private static Widget BuildStatus(WardrobeViewModel m) {
            Widget label = new Label(m.Status ?? string.Empty, "wardrobe-status") { WordWrap = true }.MinHeight(20f);
            if (!m.Busy || m.Cancel == null) return label;
            return new HStack(4f) {
                label.ExpandWidth(),
                new Button("取消", () => { if (m.Busy) m.Cancel(); }, "wardrobe-cancel").Width(68f).Height(32f)
            };
        }

        private static Widget BuildAccessories(WardrobeViewModel m, bool canEdit) {
            List<WardrobeRow> rows = m.FilteredAccessories();
            return new VStack(4f, "wardrobe-accessory-panel") {
                new HStack(4f) {
                    new TextField(m.AccessoryQuery, m.ChangeAccessoryQuery, "wardrobe-accessory-search") {
                        Placeholder = "名称 / 槽位", MaxLength = 80
                    }.Enabled(canEdit).ExpandWidth(),
                    CycleButtons("wardrobe-accessory-group",
                        new[] { "全部", "主", "副", "其他" },
                        m.AccessoryGroup, m.ChangeAccessoryGroup, canEdit).Width(152f)
                }.Height(32f),
                new Checkbox("悬停高亮对应饰品", m.HighlightOnHover,
                    value => {
                        if (m.ChangeHighlightOnHover != null)
                            m.ChangeHighlightOnHover(value);
                    }, "wardrobe-highlight-hover")
                    .Enabled(canEdit).Height(28f),
                new HStack(4f) {
                    new Button("全部显示", () => {
                        if (canEdit && rows.Count > 0 && m.SetFilteredAccessories != null) m.SetFilteredAccessories(true);
                    }, "wardrobe-show-filtered").Enabled(canEdit && rows.Count > 0 && m.SetFilteredAccessories != null).ExpandWidth(),
                    new Button("全部隐藏", () => {
                        if (canEdit && rows.Count > 0 && m.SetFilteredAccessories != null) m.SetFilteredAccessories(false);
                    }, "wardrobe-hide-filtered").Enabled(canEdit && rows.Count > 0 && m.SetFilteredAccessories != null).ExpandWidth(),
                    new Label(rows.Count + "/" + m.Rows.Count, "wardrobe-accessory-count").Width(54f)
                }.Height(32f),
                new VirtualList(rows.Count, 32f, i => {
                    WardrobeRow row = rows[i];
                    string group = row.Group == 0 ? "M " : row.Group == 1 ? "S " : "";
                    return new Checkbox(group + row.Label, row.Visible, value => {
                        if (canEdit && row.Change != null) row.Change(value);
                    }, "accessory-" + row.Slot) {
                        OnHoverChanged = over => {
                            if (m.HighlightOnHover && m.ChangeAccessoryHover != null)
                                m.ChangeAccessoryHover(row.Slot, over);
                        }
                    }.Enabled(canEdit).Height(32f);
                }, "wardrobe-accessories") { Height = 0f }.MinHeight(120f).ExpandHeight().ExpandWidth()
            }.ExpandHeight().ExpandWidth();
        }

        private static Widget BuildGarments(WardrobeViewModel m, bool canEdit) {
            var body = new VStack(4f, "wardrobe-garment-panel");
            body.Add(new HStack(4f) {
                new Button("全部穿着", () => {
                    if (canEdit && m.SetAllGarments != null) m.SetAllGarments(true);
                }, "wardrobe-wear-all")
                    .Enabled(canEdit && m.SetAllGarments != null).ExpandWidth(),
                new Button("全部隐藏", () => {
                    if (canEdit && m.SetAllGarments != null) m.SetAllGarments(false);
                }, "wardrobe-hide-all")
                    .Enabled(canEdit && m.SetAllGarments != null).ExpandWidth()
            }.Height(32f));
            if (m.Garments.Count == 0) {
                body.Add(new Label("当前配装没有可调整的服装部位。", "wardrobe-no-garments") {
                    WordWrap = true
                }.ExpandWidth());
            } else {
                // There are at most nine native garment kinds. Rendering them
                // directly avoids VirtualList's independent scroll area and
                // keeps the shoe/restore actions below the last garment row.
                var garmentRows = new VStack(4f, "wardrobe-garment-rows");
                foreach (WardrobeRow row in m.Garments) {
                    bool editable = canEdit && row.ChangeState != null && row.AllowedStates.Length > 1;
                    int selected = Array.IndexOf(row.AllowedStates, row.State);
                    string[] labels = new string[row.AllowedStates.Length];
                    for (int i = 0; i < labels.Length; i++) {
                        int state = row.AllowedStates[i];
                        labels[i] = state == 0 ? "穿好" : state == 1 ? "半脱" :
                            state == 2 ? "挂下" : "脱掉";
                    }
                    Action<int> change = index => {
                        if (editable && index >= 0 && index < row.AllowedStates.Length)
                            row.ChangeState(row.AllowedStates[index]);
                    };
                    Widget control = row.Item <= 0
                        ? (Widget)new Label("未装备", "garment-not-equipped-" + row.Slot).ExpandWidth()
                        : row.AllowedStates.Length <= 1
                            ? (Widget)new Label(row.State == 0 ? "穿好 · 当前无法切换" :
                                row.State == 1 ? "半脱 · 当前无法切换" :
                                row.State == 2 ? "挂下 · 当前无法切换" :
                                row.State == 3 ? "脱掉 · 当前无法切换" : "状态不可用",
                                "garment-not-changeable-" + row.Slot).ExpandWidth()
                            : CycleButtons("garment-state-" + row.Slot, labels,
                                selected, change, editable).ExpandWidth();
                    garmentRows.Add(new HStack(4f, "garment-" + row.Slot) {
                        new Label(row.Label).Width(94f), control
                    }.Height(36f));
                }
                body.Add(garmentRows.ExpandWidth());
            }
            if (m.ShoeMode >= 0) body.Add(new HStack(4f) {
                new Label("鞋型").Width(60f),
                new SegmentedControl<int>(m.ShoeMode, new[] { 0, 1 }, i => i == 0 ? "室内" : "室外",
                    m.ChangeShoeMode, "wardrobe-shoe-mode").Enabled(canEdit).ExpandWidth()
            }.Height(32f));
            return body.ExpandWidth().ExpandHeight();
        }
    }
}
