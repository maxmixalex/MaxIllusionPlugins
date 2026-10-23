using System;
using System.Collections.Generic;
using MaximumUI;

namespace ClothingCompression {
    /// <summary>Real MaximumUI widgets; game selection and native drawing live outside this view.</summary>
    internal sealed class CompressionEditorView : View {
        internal CompressionController Controller;
        internal List<ItemDescriptor> BindingItems = new List<ItemDescriptor>();
        internal List<DeformationEntry> ImportEntries = new List<DeformationEntry>();
        internal Action OnInvalidated;
        internal Action<bool> OnVisibleChange;
        internal Func<CompressionController, bool> IsCurrentController;
        internal int ImportCoordinate = -1;
        private readonly string title;
        private DeformationKind newKind;
        private DeformationEntry importEntry;
        private DeformationEntry previousSelection;
        private CompressionController previousController;
        private int previousCoordinate = -1;
        private int editorGeneration;
        private float splitRatio = 0.29f;

        internal CompressionEditorView(string title) { this.title = title; }

        public override Widget Build(BuildContext context) {
            Window window = new Window(title, 900f, 720f, key: "compression-window", minWidth: 680f, minHeight: 460f,
                closable: true, onVisibleChange: value => { if (OnVisibleChange != null) OnVisibleChange(value); });
            CompressionController controller = Controller;
            if (controller == null || controller.ChaControl == null) {
                window.Add(new HelpBox("请在 Maker 中编辑角色，或在 Studio 中选中角色。"));
                return window;
            }
            ValidateImportCoordinate();
            if (!ReferenceEquals(previousController, controller) || !ReferenceEquals(previousSelection, controller.Selected) || previousCoordinate != controller.CoordinateIndex) {
                previousController = controller;
                previousSelection = controller.Selected;
                previousCoordinate = controller.CoordinateIndex;
                editorGeneration++;
            }
            VStack content = new VStack(8f).ExpandWidth().ExpandHeight();
            content.Add(new HStack(8f) {
                new Label(controller.ChaFileControl.parameter.fullname) { Role = LabelRole.Heading }.ExpandWidth(),
                new StatusBadge(controller.Dirty ? "未保存" : "已保存", controller.Dirty ? StatusBadgeKind.Warning : StatusBadgeKind.Success).Width(76f),
                new Label(controller.Entries.Count + " 个形变") { Role = LabelRole.Muted }.Width(86f)
            }.Height(30f).ExpandWidth());
            TwoPaneLayout panes = new TwoPaneLayout(splitRatio, value => { splitRatio = value; Invalidate(); }, "compression-panes") {
                MinFirst = 200f, MinSecond = 380f
            }.ExpandWidth().ExpandHeight();
            panes.Add(BuildList(controller));
            panes.Add(BuildEditor(controller));
            content.Add(panes);
            content.Add(new HStack(8f) {
                new Label("修改即时预览；保存仅写入已绑定的形变。") { Role = LabelRole.Muted, WordWrap = true }.ExpandWidth(),
                new Button("从 XML 重载", ActionFor(controller, delegate {
                    if (controller.Dirty && context != null && context.Dialogs != null)
                        context.Dialogs.Confirm("重新载入形变", "当前未保存的编辑将被已保存配置替换。", ActionFor(controller, controller.ReloadFromDisk));
                    else controller.ReloadFromDisk();
                }), "reload").Width(112f),
                new Button("保存形变设置", ActionFor(controller, delegate { controller.Save(); }), "save") { Variant = ButtonVariant.Primary }.Width(134f)
            }.Height(36f).ExpandWidth());
            if (!string.IsNullOrEmpty(controller.Status))
                content.Add(new Label(controller.Status, "status") { Role = LabelRole.Muted, WordWrap = true }.ExpandWidth());
            window.Add(content);
            return window;
        }

        private Widget BuildList(CompressionController controller) {
            VStack content = new VStack(8f).ExpandWidth().ExpandHeight();
            content.Add(new Label("形变列表") { Role = LabelRole.Heading });
            content.Add(new ComboBox<DeformationKind>("新增种类", newKind, CompressionEditorSupport.Kinds,
                CompressionEditorSupport.KindName, value => { newKind = value; Invalidate(); }, "new-kind") { LabelWidth = 72f }.Height(30f).ExpandWidth());
            content.Add(new Button("新增形变", ActionFor(controller, delegate { controller.Add(newKind); }), "add") { Variant = ButtonVariant.Primary }.Height(32f).ExpandWidth());
            DeformationEntry[] entries = new DeformationEntry[controller.Entries.Count];
            controller.Entries.CopyTo(entries, 0);
            int coordinate = controller.CoordinateIndex;
            content.Add(new VirtualList(entries.Length, 32f, index => {
                DeformationEntry entry = entries[index];
                return new Selectable((entry.Enabled ? "● " : "○ ") + CompressionEditorSupport.FormatEntryLabel(index, entry),
                    ReferenceEquals(entry, controller.Selected), value => {
                        if (!value || !CanEdit(controller, coordinate, null)) return;
                        ActionFor(controller, delegate {
                            int selected = controller.Entries.IndexOf(entry);
                            if (selected >= 0) {
                                controller.SelectedIndex = selected;
                                ClothingCompressionPlugin.Instance.RefreshMakerControls(controller);
                            }
                        })();
                    }, "entry:" + entry.Id).Height(30f).ExpandWidth();
            }, "entries") { Height = 0f, AlwaysShowVertical = true }.ExpandWidth().ExpandHeight());
            content.Add(new HStack(6f) {
                new Button("复制", ActionFor(controller, controller.DuplicateSelected, controller.Selected), "duplicate").ExpandWidth(),
                new Button("删除", ActionFor(controller, controller.DeleteSelected, controller.Selected), "delete") { Variant = ButtonVariant.Danger }.ExpandWidth()
            }.Enabled(controller.Selected != null).Height(30f));
            content.Add(new HStack(6f) {
                new Button("上移", ActionFor(controller, delegate { controller.MoveSelected(-1); ClothingCompressionPlugin.Instance.RefreshMakerControls(controller); }, controller.Selected), "move-up").Enabled(controller.SelectedIndex > 0).ExpandWidth(),
                new Button("下移", ActionFor(controller, delegate { controller.MoveSelected(1); ClothingCompressionPlugin.Instance.RefreshMakerControls(controller); }, controller.Selected), "move-down").Enabled(controller.SelectedIndex + 1 < controller.Entries.Count).ExpandWidth()
            }.Enabled(controller.Selected != null).Height(30f));
            return new Panel("list-panel").Content(content).ExpandWidth().ExpandHeight();
        }

        private Widget BuildEditor(CompressionController controller) {
            VStack fields = new VStack(9f).ExpandWidth();
            DeformationEntry entry = controller.Selected;
            if (entry == null) {
                fields.Add(new HelpBox("当前搭配没有形变。可以新增形变，或从其他服装搭配复制。"));
                fields.Add(BuildImport(controller));
            } else {
                int coordinate = controller.CoordinateIndex;
                string scope = "edit:" + editorGeneration + ":";
                fields.Add(new Label("形变设置") { Role = LabelRole.Heading });
                fields.Add(new Toggle("启用这个形变", entry.Enabled,
                    value => Modify(controller, entry, coordinate, delegate { entry.Enabled = value; }, false), scope + "enabled").Height(28f));
                fields.Add(new ComboBox<DeformationKind>("种类", entry.Kind, CompressionEditorSupport.Kinds, CompressionEditorSupport.KindName,
                    value => Modify(controller, entry, coordinate, delegate { CompressionEditorSupport.SetKind(entry, value); }, true), scope + "kind") { LabelWidth = 86f }.Height(30f).ExpandWidth());
                fields.Add(new HStack(12f) {
                    new Label("应用侧").Width(86f),
                    new Checkbox(CompressionEditorSupport.IsArm(entry.Kind) ? "左臂" : "左腿", entry.Left,
                        value => Modify(controller, entry, coordinate, delegate { entry.Left = value; }, true), scope + "left").Width(92f),
                    new Checkbox(CompressionEditorSupport.IsArm(entry.Kind) ? "右臂" : "右腿", entry.Right,
                        value => Modify(controller, entry, coordinate, delegate { entry.Right = value; }, true), scope + "right").Width(92f)
                }.Height(30f));
                List<ItemDescriptor> options = new List<ItemDescriptor>(BindingItems.Count + 1) { null };
                options.AddRange(BindingItems);
                ItemDescriptor bound = ItemCatalog.Resolve(BindingItems, entry);
                fields.Add(new SearchableComboBox<ItemDescriptor>("绑定对象", bound, options,
                    item => item == null ? "未绑定（仅身体预览）" : item.Display,
                    item => Modify(controller, entry, coordinate, delegate { ItemCatalog.Bind(entry, item); }, true), scope + "binding") {
                    LabelWidth = 86f, MaxVisibleItems = 10, FilterPlaceholder = "搜索服装或饰品"
                }.Height(32f).ExpandWidth());
                if (entry.BindingKind == BoundItemKind.None)
                    fields.Add(new HelpBox("未绑定：当前只形变身体，不处理服装/饰品网格，也不会保存到配置库。", HelpBoxType.Warning));
                else if (bound == null)
                    fields.Add(new HelpBox("当前搭配中找不到已绑定物品：" + (entry.BindingName ?? entry.ItemKey) + "。可重新选择绑定对象。", HelpBoxType.Warning));
                fields.Add(new ComboBox<SaveScope>("保存级别", entry.SaveScope, new[] { SaveScope.CharacterAndItem, SaveScope.ItemOnly },
                    value => value == SaveScope.CharacterAndItem ? "角色 + 服装/饰品" : "仅服装/饰品",
                    value => Modify(controller, entry, coordinate, delegate { entry.SaveScope = value; }, false), scope + "scope") { LabelWidth = 86f }.Height(30f).ExpandWidth());
                fields.Add(new SeparatorText(CompressionEditorSupport.KindName(entry.Kind) + "参数"));
                fields.Add(Parameter(controller, entry, "束口位置", "position", entry.Position, -0.5f, 1.5f, 1f, value => entry.Position = value));
                fields.Add(Parameter(controller, entry, "影响宽度", "width", entry.Width, 0.005f, 0.5f, 1f, value => entry.Width = value));
                bool arm = CompressionEditorSupport.IsArm(entry.Kind);
                fields.Add(Parameter(controller, entry, "内收深度 (cm)", "depth", entry.Depth, 0f, arm ? 0.06f : 0.10f, 100f, value => entry.Depth = value));
                fields.Add(Parameter(controller, entry, arm ? "手臂侧隆起 (cm)" : "上侧隆起 (cm)", "proximal", entry.ProximalBulge, 0f, arm ? 0.05f : 0.08f, 100f, value => entry.ProximalBulge = value));
                if (!arm) fields.Add(Parameter(controller, entry, "下侧隆起 (cm)", "distal", entry.DistalBulge, 0f, 0.08f, 100f, value => entry.DistalBulge = value));
                fields.Add(new SeparatorText("服装外扩"));
                fields.Add(Parameter(controller, entry, "X (cm)", "clearance-x", entry.GarmentClearanceX, 0f, 0.10f, 100f, value => entry.GarmentClearanceX = value));
                fields.Add(Parameter(controller, entry, "Y (cm)", "clearance-y", entry.GarmentClearanceY, 0f, 0.10f, 100f, value => entry.GarmentClearanceY = value));
                fields.Add(Parameter(controller, entry, "Z (cm)", "clearance-z", entry.GarmentClearanceZ, 0f, 0.10f, 100f, value => entry.GarmentClearanceZ = value));
                fields.Add(BuildImport(controller));
                fields.Add(new HelpBox("绑定物品关闭时形变停用；启用时同步含对应骨骼的已穿着蒙皮服饰。普通 MeshRenderer 暂不支持。"));
            }
            VStack editor = new VStack(8f).ExpandWidth().ExpandHeight();
            editor.Add(new ScrollView("editor-scroll") { Height = 0f }.Content(fields).ExpandWidth().ExpandHeight());
            editor.Add(new HStack(8f) {
                new Button("立即重算", ActionFor(controller, controller.ApplyNow), "rebuild").ExpandWidth(),
                new Button("取消绑定", ActionFor(controller, delegate { ItemCatalog.Bind(entry, null); controller.Changed(true); }, entry), "unbind").Enabled(entry != null && entry.BindingKind != BoundItemKind.None).ExpandWidth()
            }.Height(32f));
            return new Panel("editor-panel").Content(editor).ExpandWidth().ExpandHeight();
        }

        private Widget Parameter(CompressionController controller, DeformationEntry entry, string label, string key,
            float value, float min, float max, float scale, Action<float> setter) {
            int coordinate = controller.CoordinateIndex;
            Action<float> update = displayed => Modify(controller, entry, coordinate, delegate { setter(displayed / scale); }, true);
            string scope = "edit:" + editorGeneration + ":";
            return new HStack(8f) {
                new Label(label).Width(132f),
                new Slider(value * scale, min * scale, max * scale, update, scope + key + ":slider").ExpandWidth(),
                new InputFloat(string.Empty, value * scale, update, scope + key) {
                    HasMin = true, Min = min * scale, HasMax = true, Max = max * scale,
                    Step = scale == 1f ? 0.001f : 0.01f, StepFast = scale == 1f ? 0.01f : 0.1f, Format = "0.###"
                }.Width(118f)
            }.Height(30f).ExpandWidth();
        }

        private Widget BuildImport(CompressionController controller) {
            List<int> coordinates = new List<int>();
            for (int i = 0; i < CompressionEditorSupport.CoordinateNames.Length; i++) if (i != controller.CoordinateIndex) coordinates.Add(i);
            if (!ImportEntries.Contains(importEntry)) {
                string previousId = importEntry == null ? null : importEntry.Id;
                importEntry = previousId == null ? null : ImportEntries.Find(value => value.Id == previousId);
                if (importEntry == null && ImportEntries.Count > 0) importEntry = ImportEntries[0];
            }
            DeformationEntry source = importEntry;
            int sourceCoordinate = ImportCoordinate;
            return new VStack(8f) {
                new SeparatorText("从其他服装搭配复制"),
                new ComboBox<int>("来源搭配", ImportCoordinate, coordinates, index => CompressionEditorSupport.CoordinateNames[index],
                    index => { ImportCoordinate = index; importEntry = null; Invalidate(); }, "import-coordinate") { LabelWidth = 86f }.Height(30f).ExpandWidth(),
                new SearchableComboBox<DeformationEntry>("来源形变", source, ImportEntries,
                    value => value == null ? "该搭配没有已保存形变" : (value.BindingName ?? "未命名") + "（" + CompressionEditorSupport.KindName(value.Kind) + "）",
                    value => { importEntry = value; MaximumUIRuntime.MarkDirty(this); }, "import-entry") { LabelWidth = 86f, FilterPlaceholder = "搜索已保存形变" }.Enabled(ImportEntries.Count > 0).Height(30f).ExpandWidth(),
                new HStack(8f) {
                    new Button("复制所选形变", ActionFor(controller, delegate { controller.ImportEntry(source); }), "import-selected").Enabled(source != null).ExpandWidth(),
                    new Button("复制全部形变", ActionFor(controller, delegate { controller.ImportFromCoordinate(sourceCoordinate); }), "import-all").Enabled(ImportEntries.Count > 0).ExpandWidth()
                }.Height(30f)
            }.ExpandWidth();
        }

        internal void ValidateImportCoordinate() {
            int current = Controller == null ? -1 : Controller.CoordinateIndex;
            if (ImportCoordinate < 0 || ImportCoordinate >= CompressionEditorSupport.CoordinateNames.Length || ImportCoordinate == current)
                ImportCoordinate = current >= 0 ? (current + 1) % CompressionEditorSupport.CoordinateNames.Length : 0;
        }

        private Action ActionFor(CompressionController controller, Action action, DeformationEntry selected = null) {
            int coordinate = controller.CoordinateIndex;
            return delegate {
                if (!CanEdit(controller, coordinate, selected)) { Invalidate(); return; }
                action();
                Invalidate();
            };
        }

        private void Modify(CompressionController controller, DeformationEntry selected, int coordinate, Action mutation, bool rebuild) {
            if (!CanEdit(controller, coordinate, selected)) { Invalidate(); return; }
            mutation();
            controller.Changed(rebuild);
            Invalidate();
        }

        private bool CanEdit(CompressionController controller, int coordinate, DeformationEntry selected) {
            return ReferenceEquals(Controller, controller) && controller != null && controller.ChaControl != null &&
                controller.CoordinateIndex == coordinate && (selected == null || ReferenceEquals(selected, controller.Selected)) &&
                (IsCurrentController == null || IsCurrentController(controller));
        }

        private void Invalidate() {
            if (OnInvalidated != null) OnInvalidated();
            MaximumUIRuntime.MarkDirty(this);
        }
    }
}
