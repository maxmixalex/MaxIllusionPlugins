using System;
using System.Collections.Generic;
using MaximumUI;

namespace ClothingControl {
    internal sealed class WardrobeRow {
        internal string Label;
        internal int Slot;
        internal bool Visible;
        internal Action<bool> Change;
    }

    internal sealed class WardrobeViewModel {
        internal string[] Targets = new string[0], Coordinates = new string[0], Presets = new string[0];
        internal int TargetIndex = -1, CoordinateIndex = -1, PresetIndex = -1;
        internal string Name = string.Empty, Status = string.Empty, Error;
        internal bool Busy, CanWrite, HasTarget;
        internal readonly List<WardrobeRow> Rows = new List<WardrobeRow>();
        internal Action<int> SelectTarget, SelectCoordinate, SelectPreset;
        internal Action<string> ChangeName;
        internal Action Apply, Create, Overwrite, Delete, Reload, Close;
    }

    internal sealed class WardrobeView : View {
        internal WardrobeViewModel Model = new WardrobeViewModel();

        private static Widget Selector(string label, string key, string[] values, int selected, Action<int> changed, bool enabled) {
            int[] ids = new int[values.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = i;
            return new HStack(6f, key + "-row") {
                new Label(label).Width(76f),
                new ComboBox<int>(string.Empty, selected, ids, i => i >= 0 && i < values.Length ? values[i] : "无",
                    changed, key).Enabled(enabled && values.Length > 0).ExpandWidth()
            }.Height(32f);
        }

        public override Widget Build(BuildContext context) {
            WardrobeViewModel m = Model;
            bool canEdit = m.HasTarget && !m.Busy;
            bool hasPreset = m.PresetIndex >= 0 && m.PresetIndex < m.Presets.Length;
            Window window = new Window("ClothingControl " + ClothingControlPlugin.Version, 410f, 560f, "clothing-window",
                minWidth: 350f, minHeight: 420f, closable: true,
                onVisibleChange: value => { if (!value && m.Close != null) m.Close(); });
            VStack body = new VStack(6f) {
                Selector("角色", "wardrobe-target", m.Targets, m.TargetIndex, m.SelectTarget, !m.Busy),
                Selector("配装", "wardrobe-coordinate", m.Coordinates, m.CoordinateIndex, m.SelectCoordinate, canEdit),
                new Separator(),
                new Label("饰品"),
                new VirtualList(m.Rows.Count, 32f, i => {
                    WardrobeRow row = m.Rows[i];
                    return new Checkbox(row.Label, row.Visible, row.Change, "accessory-" + row.Slot).Enabled(canEdit).Height(32f);
                }, "wardrobe-accessories").MinHeight(100f).ExpandHeight().ExpandWidth(),
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
            if (!string.IsNullOrEmpty(m.Error)) body.Add(new Label(m.Error, "wardrobe-error") { WordWrap = true }.MinHeight(28f));
            window.Add(body.ExpandHeight());
            return window;
        }
    }
}
