using System;
using System.Collections.Generic;
using MaximumUI;

namespace ClothingControl {
    internal sealed class WardrobeRow {
        internal string Label;
        internal int Slot, Type, Item, Group;
        internal bool Visible;
        internal Action<bool> Change;
    }

    internal sealed class WardrobeViewModel {
        internal string[] Targets = new string[0], Coordinates = new string[0], Presets = new string[0];
        internal int TargetIndex = -1, CoordinateIndex = -1, PresetIndex = -1;
        internal string Version = string.Empty, Name = string.Empty, Status = string.Empty, Error;
        internal int Revision, Page, AccessoryGroup, ShoeMode = -1;
        internal string AccessoryQuery = string.Empty;
        internal bool Busy, CanWrite, HasTarget;
        internal readonly List<WardrobeRow> Rows = new List<WardrobeRow>();
        internal readonly List<WardrobeRow> Garments = new List<WardrobeRow>();
        internal Action<int> SelectTarget, SelectCoordinate, SelectPreset, ChangePage, ChangeAccessoryGroup, ChangeShoeMode;
        internal Action<string> ChangeName, ChangeAccessoryQuery;
        internal Action<bool> SetFilteredAccessories;
        internal Action Apply, Create, Overwrite, Delete, Reload, Close, RestoreClothing;

        internal List<WardrobeRow> FilteredAccessories() {
            List<WardrobeRow> result = new List<WardrobeRow>();
            string query = (AccessoryQuery ?? string.Empty).Trim();
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
                    x.Label != y.Label || x.Visible != y.Visible) return false;
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
                TargetIndex != other.TargetIndex || CoordinateIndex != other.CoordinateIndex || PresetIndex != other.PresetIndex ||
                Page != other.Page || ShoeMode != other.ShoeMode || AccessoryGroup != other.AccessoryGroup ||
                AccessoryQuery != other.AccessoryQuery ||
                !Same(Targets, other.Targets) || !Same(Coordinates, other.Coordinates) || !Same(Presets, other.Presets) ||
                !SameRows(Rows, other.Rows) || !SameRows(Garments, other.Garments)) return false;
            return true;
        }
    }

    internal sealed class WardrobeView : View {
        internal WardrobeViewModel Model = new WardrobeViewModel();

        private static Widget Selector(string label, string key, string[] values, int selected, Action<int> changed, bool enabled) {
            int[] ids = new int[values.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = i;
            bool active = enabled && values.Length > 0 && changed != null;
            Action<int> choose = i => { if (active && i >= 0 && i < values.Length) changed(i); };
            return new HStack(4f, key + "-row") {
                new Label(label).Width(60f),
                new ArrowButton(ArrowDirection.Left, () => choose(selected <= 0 ? values.Length - 1 : selected - 1), key + "-previous").Enabled(active).Width(28f),
                new ComboBox<int>(string.Empty, selected, ids, i => i >= 0 && i < values.Length ? values[i] : "无",
                    choose, key).Enabled(active).ExpandWidth(),
                new ArrowButton(ArrowDirection.Right, () => choose(selected >= values.Length - 1 ? 0 : selected + 1), key + "-next").Enabled(active).Width(28f)
            }.Height(32f);
        }

        public override Widget Build(BuildContext context) {
            WardrobeViewModel m = Model;
            bool canEdit = m.HasTarget && !m.Busy;
            bool hasPreset = m.PresetIndex >= 0 && m.PresetIndex < m.Presets.Length;
            Window window = new Window("ClothingControl " + m.Version, 430f, 720f, "clothing-window",
                minWidth: 370f, minHeight: 700f, closable: true,
                onVisibleChange: value => { if (!value && m.Close != null) m.Close(); });
            VStack body = new VStack(6f) {
                Selector("角色", "wardrobe-target", m.Targets, m.TargetIndex, m.SelectTarget, !m.Busy),
                Selector("配装", "wardrobe-coordinate", m.Coordinates, m.CoordinateIndex, m.SelectCoordinate, canEdit),
                new Separator(),
                new SegmentedControl<int>(m.Page, new[] { 0, 1 }, i => i == 0 ? "饰品" : "服装",
                    value => { if (!m.Busy && m.ChangePage != null) m.ChangePage(value); }, "wardrobe-page").Enabled(!m.Busy).Height(32f),
                m.Page == 1 ? BuildGarments(m, canEdit) : BuildAccessories(m, canEdit),
                new Separator(),
                Selector("预设", "wardrobe-preset", m.Presets, m.PresetIndex, m.SelectPreset, !m.Busy),
                new TextField(m.Name, m.ChangeName, "wardrobe-name") { Placeholder = "新预设名称", MaxLength = 80 }.Enabled(m.CanWrite && !m.Busy).Height(32f),
                new HStack(4f) {
                    new Button("应用选中", m.Apply, "wardrobe-apply").Enabled(canEdit && hasPreset).ExpandWidth(),
                    new Button("新建保存", m.Create, "wardrobe-create").Enabled(canEdit && m.CanWrite).ExpandWidth()
                }.Height(32f),
                new HStack(4f) {
                    new Button("覆盖保存", m.Overwrite, "wardrobe-overwrite").Enabled(canEdit && hasPreset && m.CanWrite).ExpandWidth(),
                    new Button("删除选中", m.Delete, "wardrobe-delete").Enabled(!m.Busy && hasPreset && m.CanWrite).ExpandWidth(),
                    new Button("重载 XML", m.Reload, "wardrobe-reload").Enabled(!m.Busy).ExpandWidth()
                }.Height(32f),
                new Label(m.Status ?? string.Empty, "wardrobe-status") { WordWrap = true }.MinHeight(20f)
            };
            if (!string.IsNullOrEmpty(m.Error)) body.Add(new ScrollView("wardrobe-error-scroll") {
                new HelpBox("XML: " + m.Error, HelpBoxType.Error, "wardrobe-error").ExpandWidth()
            }.Height(72f));
            window.Add(body.ExpandHeight());
            return window;
        }

        private static Widget BuildAccessories(WardrobeViewModel m, bool canEdit) {
            List<WardrobeRow> rows = m.FilteredAccessories();
            return new VStack(4f, "wardrobe-accessory-panel") {
                new HStack(4f) {
                    new TextField(m.AccessoryQuery, m.ChangeAccessoryQuery, "wardrobe-accessory-search") {
                        Placeholder = "名称 / 槽位", MaxLength = 80
                    }.Enabled(canEdit).ExpandWidth(),
                    new ComboBox<int>("", m.AccessoryGroup, new[] { 0, 1, 2, 3 },
                        i => new[] { "全部", "主", "副", "其他" }[Math.Max(0, Math.Min(3, i))],
                        m.ChangeAccessoryGroup, "wardrobe-accessory-group").Enabled(canEdit).Width(100f)
                }.Height(32f),
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
                    }, "accessory-" + row.Slot).Enabled(canEdit).Height(32f);
                }, "wardrobe-accessories") { Height = 0f }.MinHeight(120f).ExpandHeight().ExpandWidth()
            }.ExpandHeight().ExpandWidth();
        }

        private static Widget BuildGarments(WardrobeViewModel m, bool canEdit) {
            var body = new VStack(4f, "wardrobe-garment-panel");
            if (m.ShoeMode >= 0) body.Add(new HStack(4f) {
                new Label("鞋型").Width(60f),
                new SegmentedControl<int>(m.ShoeMode, new[] { 0, 1 }, i => i == 0 ? "室内" : "室外",
                    m.ChangeShoeMode, "wardrobe-shoe-mode").Enabled(canEdit).ExpandWidth()
            }.Height(32f));
            body.Add(new Button("恢复完整穿着", () => {
                if (canEdit && m.RestoreClothing != null) m.RestoreClothing();
            }, "wardrobe-restore-clothing").Enabled(canEdit && m.RestoreClothing != null).ExpandWidth().Height(32f));
            if (m.Garments.Count == 0)
                body.Add(new Label("当前配装没有可调整的手套、袜子或鞋。", "wardrobe-no-garments") { WordWrap = true }.ExpandWidth());
            else body.Add(new VirtualList(m.Garments.Count, 32f, i => {
                WardrobeRow row = m.Garments[i];
                return new Checkbox(row.Label, row.Visible, value => {
                    if (canEdit && row.Change != null) row.Change(value);
                }, "garment-" + row.Slot).Enabled(canEdit).Height(32f);
            }, "wardrobe-garments") { Height = 0f }.MinHeight(120f).ExpandWidth().ExpandHeight());
            return body.ExpandWidth().ExpandHeight();
        }
    }
}
