using System;
using System.Collections.Generic;
using MaximumUI;

namespace ClothingControl {
    internal sealed class WardrobeRow {
        internal string Label;
        internal int Slot, Type, Item;
        internal bool Visible;
        internal Action<bool> Change;
    }

    internal sealed class WardrobeViewModel {
        internal string[] Targets = new string[0], Coordinates = new string[0], Presets = new string[0];
        internal int TargetIndex = -1, CoordinateIndex = -1, PresetIndex = -1;
        internal string Version = string.Empty, Name = string.Empty, Status = string.Empty, Error;
        internal int Revision;
        internal bool Busy, CanWrite, HasTarget;
        internal readonly List<WardrobeRow> Rows = new List<WardrobeRow>();
        internal Action<int> SelectTarget, SelectCoordinate, SelectPreset;
        internal Action<string> ChangeName;
        internal Action Apply, Create, Overwrite, Delete, Reload, Close;

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
                !Same(Targets, other.Targets) || !Same(Coordinates, other.Coordinates) || !Same(Presets, other.Presets) ||
                Rows.Count != other.Rows.Count) return false;
            for (int i = 0; i < Rows.Count; i++) {
                WardrobeRow a = Rows[i], b = other.Rows[i];
                if (a.Slot != b.Slot || a.Type != b.Type || a.Item != b.Item || a.Label != b.Label || a.Visible != b.Visible) return false;
            }
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
            Window window = new Window("ClothingControl " + m.Version, 410f, 640f, "clothing-window",
                minWidth: 360f, minHeight: 600f, closable: true,
                onVisibleChange: value => { if (!value && m.Close != null) m.Close(); });
            VStack body = new VStack(6f) {
                Selector("角色", "wardrobe-target", m.Targets, m.TargetIndex, m.SelectTarget, !m.Busy),
                Selector("配装", "wardrobe-coordinate", m.Coordinates, m.CoordinateIndex, m.SelectCoordinate, canEdit),
                new Separator(),
                new Label("饰品"),
                new VirtualList(m.Rows.Count, 32f, i => {
                    WardrobeRow row = m.Rows[i];
                    return new Checkbox(row.Label, row.Visible, value => {
                        if (canEdit && row.Change != null) row.Change(value);
                    }, "accessory-" + row.Slot).Enabled(canEdit).Height(32f);
                }, "wardrobe-accessories") { Height = 0f }.MinHeight(120f).ExpandHeight().ExpandWidth(),
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
    }
}
