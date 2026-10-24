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
        private DeformationEntry previousSelection;
        private CompressionController previousController;
        private int previousCoordinate = -1;
        private int editorGeneration;

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
            content.Add(new Label("角色：" + controller.ChaFileControl.parameter.fullname + "　形变：" + controller.Entries.Count + "　" +
                (controller.Dirty ? "（未保存）" : string.Empty)).Height(26f).ExpandWidth());
            content.Add(new HStack(8f) {
                BuildList(controller).Width(218f).ExpandHeight(),
                BuildEditor(controller).ExpandWidth().ExpandHeight()
            }.ExpandWidth().ExpandHeight());
            content.Add(new HStack(8f) {
                new Spacer(),
                new Button("保存形变设置", ActionFor(controller, delegate { controller.Save(); }), "save").Width(134f),
                new Button("从 XML 重载", ActionFor(controller, controller.ReloadFromDisk), "reload").Width(112f)
            }.Height(32f).ExpandWidth());
            if (!string.IsNullOrEmpty(controller.Status))
                content.Add(new Label(controller.Status, "status") { Role = LabelRole.Muted, WordWrap = true }.ExpandWidth());
            window.Add(content);
            return window;
        }

        private Widget BuildList(CompressionController controller) {
            VStack content = new VStack(8f).ExpandWidth().ExpandHeight();
            content.Add(new Button("+ 新增形变", ActionFor(controller, delegate { controller.Add(DeformationKind.Thigh); }), "add").Height(30f).ExpandWidth());
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
            }, "entries") { Height = 0f }.ExpandWidth().ExpandHeight());
            content.Add(new HStack(6f) {
                new Button("复制", ActionFor(controller, controller.DuplicateSelected, controller.Selected), "duplicate").ExpandWidth(),
                new Button("删除", ActionFor(controller, controller.DeleteSelected, controller.Selected), "delete") { Variant = ButtonVariant.Danger }.ExpandWidth()
            }.Enabled(controller.Selected != null).Height(30f));
            content.Add(new HStack(6f) {
                new ArrowButton(ArrowDirection.Up, ActionFor(controller, delegate { controller.MoveSelected(-1); ClothingCompressionPlugin.Instance.RefreshMakerControls(controller); }, controller.Selected), "move-up").Enabled(controller.SelectedIndex > 0).ExpandWidth(),
                new ArrowButton(ArrowDirection.Down, ActionFor(controller, delegate { controller.MoveSelected(1); ClothingCompressionPlugin.Instance.RefreshMakerControls(controller); }, controller.Selected), "move-down").Enabled(controller.SelectedIndex + 1 < controller.Entries.Count).ExpandWidth()
            }.Enabled(controller.Selected != null).Height(30f));
            return new Panel("list-panel").Content(content).ExpandHeight();
        }

        private Widget BuildEditor(CompressionController controller) {
            VStack fields = new VStack(4f).ExpandWidth();
            DeformationEntry entry = controller.Selected;
            if (entry == null) {
                fields.Add(new HelpBox("当前搭配没有形变。可以新增形变，或从其他服装搭配复制。"));
                fields.Add(BuildImport(controller));
            } else {
                int coordinate = controller.CoordinateIndex;
                string scope = "edit:" + editorGeneration + ":";
                fields.Add(new Toggle("启用这个形变", entry.Enabled,
                    value => Modify(controller, entry, coordinate, delegate { entry.Enabled = value; }, false), scope + "enabled").Height(28f));
                HStack kinds = new HStack(6f) { new Label("种类").Width(72f) };
                foreach (DeformationKind kind in CompressionEditorSupport.Kinds) {
                    DeformationKind captured = kind;
                    kinds.Add(new RadioButton(kind == DeformationKind.LowerThigh ? "整腿" : CompressionEditorSupport.KindName(kind), entry.Kind == kind,
                        delegate { Modify(controller, entry, coordinate, delegate { CompressionEditorSupport.SetKind(entry, captured); }, true); },
                        scope + "kind-" + kind).ExpandWidth());
                }
                fields.Add(kinds.Height(30f).ExpandWidth());
                fields.Add(new HStack(12f) {
                    new Label("应用侧").Width(72f),
                    new Checkbox(CompressionEditorSupport.IsArm(entry.Kind) ? "左臂" : "左腿", entry.Left,
                        value => Modify(controller, entry, coordinate, delegate { entry.Left = value; }, true), scope + "left").Width(92f),
                    new Checkbox(CompressionEditorSupport.IsArm(entry.Kind) ? "右臂" : "右腿", entry.Right,
                        value => Modify(controller, entry, coordinate, delegate { entry.Right = value; }, true), scope + "right").Width(92f)
                }.Height(30f));
                List<ItemDescriptor> options = new List<ItemDescriptor>(BindingItems.Count + 1) { null };
                options.AddRange(BindingItems);
                ItemDescriptor bound = ItemCatalog.Resolve(BindingItems, entry);
                fields.Add(new ComboBox<ItemDescriptor>("绑定对象", bound, options,
                    item => item == null ? "未绑定（始终应用）" : item.Display,
                    item => Modify(controller, entry, coordinate, delegate { ItemCatalog.Bind(entry, item); }, true), scope + "binding") {
                    LabelWidth = 72f, MaxVisibleItems = 10
                }.Height(32f).ExpandWidth());
                if (entry.BindingKind == BoundItemKind.None)
                    fields.Add(new HelpBox("未绑定：当前只形变身体，不会处理服装/饰品网格。", HelpBoxType.Warning));
                else if (bound == null)
                    fields.Add(new HelpBox("当前搭配中找不到已绑定物品：" + (entry.BindingName ?? entry.ItemKey) + "。可重新选择绑定对象。", HelpBoxType.Warning));
                fields.Add(new HStack(8f) {
                    new Label("保存级别").Width(72f),
                    new RadioButton("角色 + 服装/饰品", entry.SaveScope == SaveScope.CharacterAndItem,
                        delegate { Modify(controller, entry, coordinate, delegate { entry.SaveScope = SaveScope.CharacterAndItem; }, false); }, scope + "scope-character").Width(170f),
                    new RadioButton("仅服装/饰品", entry.SaveScope == SaveScope.ItemOnly,
                        delegate { Modify(controller, entry, coordinate, delegate { entry.SaveScope = SaveScope.ItemOnly; }, false); }, scope + "scope-item").ExpandWidth()
                }.Height(30f));
                fields.Add(new SeparatorText(entry.Kind == DeformationKind.LowerThigh ? "整腿定位参数" : CompressionEditorSupport.KindName(entry.Kind) + "参数"));
                fields.Add(Parameter(controller, entry, "束口位置", "position", entry.Position, -0.5f, 1.5f, 1f, value => entry.Position = value));
                fields.Add(Parameter(controller, entry, "影响宽度", "width", entry.Width, 0.005f, 0.5f, 1f, value => entry.Width = value));
                bool arm = CompressionEditorSupport.IsArm(entry.Kind);
                fields.Add(Parameter(controller, entry, "内收深度", "depth", entry.Depth, 0f, arm ? 0.06f : 0.10f, 100f, value => entry.Depth = value));
                fields.Add(Parameter(controller, entry, arm ? "手臂侧隆起" : "上侧隆起", "proximal", entry.ProximalBulge, 0f, arm ? 0.05f : 0.08f, 100f, value => entry.ProximalBulge = value));
                if (!arm) fields.Add(Parameter(controller, entry, "下侧隆起", "distal", entry.DistalBulge, 0f, 0.08f, 100f, value => entry.DistalBulge = value));
                fields.Add(Parameter(controller, entry, "服装外扩 X", "clearance-x", entry.GarmentClearanceX, 0f, 0.10f, 100f, value => entry.GarmentClearanceX = value));
                fields.Add(Parameter(controller, entry, "服装外扩 Y", "clearance-y", entry.GarmentClearanceY, 0f, 0.10f, 100f, value => entry.GarmentClearanceY = value));
                fields.Add(Parameter(controller, entry, "服装外扩 Z", "clearance-z", entry.GarmentClearanceZ, 0f, 0.10f, 100f, value => entry.GarmentClearanceZ = value));
                fields.Add(BuildImport(controller));
                fields.Add(new HelpBox("主绑定对象关闭时形变停用；启用时会自动同步所有含对应骨骼的已穿着蒙皮服饰，以避免多层服装穿模。普通 MeshRenderer 暂时不能形变。"));
            }
            VStack editor = new VStack(8f).ExpandWidth().ExpandHeight();
            editor.Add(new ScrollView("editor-scroll") { Height = 0f }.Content(fields).ExpandWidth().ExpandHeight());
            if (entry != null) editor.Add(new HStack(8f) {
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
                new Label((value * scale).ToString(scale == 1f ? "F3" : "0.###"), scope + key).Width(56f)
            }.Height(26f).ExpandWidth();
        }

        private Widget BuildImport(CompressionController controller) {
            List<int> coordinates = new List<int>();
            for (int i = 0; i < CompressionEditorSupport.CoordinateNames.Length; i++) coordinates.Add(i);
            int sourceCoordinate = ImportCoordinate;
            int coordinate = controller.CoordinateIndex;
            return new VStack(8f) {
                new SeparatorText("从其他服装搭配复制"),
                new HStack(8f) {
                    new ComboBox<int>("来源搭配", ImportCoordinate, coordinates, index => CompressionEditorSupport.CoordinateNames[index],
                        index => {
                            if (CanEdit(controller, coordinate, null) && index >= 0 && index < CompressionEditorSupport.CoordinateNames.Length && index != coordinate) {
                                ImportCoordinate = index; Invalidate();
                            }
                        }, "import-coordinate") {
                        LabelWidth = 72f
                    }.ExpandWidth(),
                    new Button("复制形变", ActionFor(controller, delegate { controller.ImportFromCoordinate(sourceCoordinate); }), "import-all").Width(90f)
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
