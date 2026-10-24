using System;
using System.Collections.Generic;
using MaximumUI;

namespace AccessoryBatchManager {
    internal enum AccessoryListRole { Batch, Source, Destination }
    internal enum AccessoryEditorCommand {
        Close, Remove, Compact, Copy, Move, InsertFirst, InsertLast,
        Adjust1, Adjust1LeftRight, Adjust1UpDown, Adjust2, Adjust2LeftRight, Adjust2UpDown,
        RefreshModel, Confirm, Cancel
    }
    internal enum AccessoryEditorSetting { ImmediateSync, SelectionHighlight, ClearAfter, FollowCoordinate, ReverseParent, SyncScroll }
    internal enum AccessorySelectionCommand { All, NonEmpty, None, ToggleKind }

    internal sealed class AccessoryListModel {
        public AccessoryListRole Role;
        public int Count;
        public bool Clothing;
        public bool CanToggleKind;
        public HashSet<int> Selection = new HashSet<int>();
        public Func<int, string> GetName;
        public ScrollPosition Scroll = new ScrollPosition();
    }

    internal sealed class AccessoryEditorModel {
        public string Version;
        public bool Available;
        public int Page;
        public string Status;
        public string Summary;
        public string Mapping;
        public string[] Sources = new string[0];
        public string[] Destinations = new string[0];
        public int SourceIndex;
        public int DestinationIndex;
        public bool FollowCoordinate;
        public bool ImmediateSync;
        public bool SelectionHighlight;
        public bool ClearAfter;
        public bool ReverseParent;
        public bool SyncScroll;
        public bool CanCopy;
        public bool CanMove;
        public bool CanInsert;
        public bool CanAdjust;
        public bool Confirming;
        public string Confirmation;
        public AccessoryListModel Batch = new AccessoryListModel { Role = AccessoryListRole.Batch };
        public AccessoryListModel Source = new AccessoryListModel { Role = AccessoryListRole.Source };
        public AccessoryListModel Destination = new AccessoryListModel { Role = AccessoryListRole.Destination };
        public Action<int> ChangePage;
        public Action<int> ChangeSource;
        public Action<int> ChangeDestination;
        public Action<AccessoryListRole, int, SelectionRequest> SelectSlot;
        public Action<AccessoryListRole, AccessorySelectionCommand> SelectionCommand;
        public Action<AccessoryEditorSetting, bool> ChangeSetting;
        public Action<AccessoryEditorCommand> Execute;
    }

    internal sealed class AccessoryEditorView : View {
        internal AccessoryEditorModel Model = new AccessoryEditorModel();

        public override Widget Build(BuildContext context) {
            AccessoryEditorModel m = Model;
            Window window = new Window("饰品批量管理 " + m.Version, 900f, 760f,
                key: "accessory-window", minWidth: 740f, minHeight: 600f,
                closable: true, onVisibleChange: delegate(bool value) {
                    if (!value) Execute(m, AccessoryEditorCommand.Close);
                });
            VStack body = new VStack(10f) {
                new HStack(6f) {
                    Page(m, "批量管理", 0), Page(m, "复制/移动", 1), Page(m, "设置", 2)
                }.Height(32f),
                new Separator()
            };
            if (!m.Available) body.Add(new HelpBox("正在等待角色编辑器加载……", HelpBoxType.Info).ExpandHeight());
            else body.Add((m.Page == 0 ? BuildBatch(m) : m.Page == 1 ? BuildCopy(m) : BuildSettings(m)).ExpandHeight().ExpandWidth());
            body.Add(new Separator());
            body.Add(new Label(m.Status ?? string.Empty) {
                Role = LabelRole.Muted, WordWrap = true
            }.MinHeight(26f));
            window.Add(body.ExpandHeight());
            if (m.Confirming) window.Add(new Dialog("确认操作", "accessory-confirm") {
                Width = 460f, Height = 220f,
                OnClose = delegate { Execute(m, AccessoryEditorCommand.Cancel); }
            }.Content(new VStack(14f) {
                new Label(m.Confirmation) { WordWrap = true }.MinHeight(90f),
                new HStack(8f) {
                    Command(m, "确认", AccessoryEditorCommand.Confirm, true, true),
                    Command(m, "取消", AccessoryEditorCommand.Cancel)
                }.Height(34f)
            }));
            return window;
        }

        private static Widget Page(AccessoryEditorModel m, string title, int index) {
            return new Selectable(title, m.Page == index, delegate(bool value) {
                if (m.ChangePage != null) m.ChangePage(index);
            }, "accessory-page-" + index).ExpandWidth();
        }

        private static Widget BuildBatch(AccessoryEditorModel m) {
            // Keep the original toolbar above the list, including its order.
            return new VStack(8f) {
                new HStack(8f) {
                    SelectionButton(m, m.Batch, "全选", AccessorySelectionCommand.All),
                    SelectionButton(m, m.Batch, "全不选", AccessorySelectionCommand.None),
                    Command(m, "移除选中饰品", AccessoryEditorCommand.Remove, m.Batch.Selection.Count > 0, width: 126f),
                    Command(m, "一键整理（保持顺序）", AccessoryEditorCommand.Compact, width: 176f),
                    new Spacer(),
                    new Label(m.Summary, "accessory-summary") { Role = LabelRole.Muted }.Width(138f)
                }.Height(32f),
                BuildList(m, m.Batch).ExpandHeight()
            }.ExpandHeight();
        }

        private static Widget BuildCopy(AccessoryEditorModel m) {
            return new VStack(8f) {
                new HStack(12f) {
                    CycleChoice("来源:", m.Sources, m.SourceIndex, m.ChangeSource, "accessory-source").ExpandWidth(),
                    CycleChoice("目标:", m.Destinations, m.DestinationIndex, m.ChangeDestination, "accessory-destination", !m.FollowCoordinate)
                        .Enabled(!m.FollowCoordinate).ExpandWidth()
                }.Height(32f),
                new HStack(18f) {
                    BuildColumn(m, m.Source).ExpandWidth().ExpandHeight(),
                    BuildColumn(m, m.Destination).ExpandWidth().ExpandHeight()
                }.ExpandHeight(),
                new Label(m.Mapping) { Role = LabelRole.Muted, WordWrap = true }.MinHeight(24f),
                new HStack(8f) {
                    Setting(m, "位置左右交换", AccessoryEditorSetting.ReverseParent, m.ReverseParent).Width(132f),
                    Setting(m, "同步滚动", AccessoryEditorSetting.SyncScroll, m.SyncScroll).Width(100f),
                    Command(m, "复制", AccessoryEditorCommand.Copy, m.CanCopy),
                    Command(m, "移动", AccessoryEditorCommand.Move, m.CanMove),
                    Command(m, "最前插入", AccessoryEditorCommand.InsertFirst, m.CanInsert),
                    Command(m, "最后插入", AccessoryEditorCommand.InsertLast, m.CanInsert)
                }.Height(32f),
                Adjustment(m, 0), Adjustment(m, 1)
            }.ExpandHeight();
        }

        private static Widget BuildColumn(AccessoryEditorModel m, AccessoryListModel list) {
            return new VStack(6f) {
                new HStack(4f) {
                    new Label(list.Role == AccessoryListRole.Source ? "来源" : "目标").Width(36f),
                    SelectionButton(m, list, "全选", AccessorySelectionCommand.All),
                    SelectionButton(m, list, "全选非空", AccessorySelectionCommand.NonEmpty),
                    SelectionButton(m, list, "全不选", AccessorySelectionCommand.None),
                    SelectionButton(m, list, list.Clothing ? "服装" : "饰品", AccessorySelectionCommand.ToggleKind)
                        .Enabled(list.CanToggleKind)
                }.Height(30f),
                BuildList(m, list).ExpandHeight()
            };
        }

        private static Widget BuildList(AccessoryEditorModel m, AccessoryListModel list) {
            return new VirtualList(list.Count, 30f, delegate(int index) {
                string label = list.GetName == null ? (index + 1).ToString() : list.GetName(index);
                return new Selectable(label, list.Selection.Contains(index), key: "slot-" + list.Role + "-" + index) {
                    Value = index, ShowCheckbox = true,
                    OnSelect = delegate(SelectionRequest request) {
                        if (m.SelectSlot != null) m.SelectSlot(list.Role, index, request);
                    }
                }.Height(28f).ExpandWidth().Tooltip(label);
            }, "accessory-list-" + list.Role) { Height = 0f, ScrollPosition = list.Scroll }.ExpandWidth();
        }

        private static Widget Adjustment(AccessoryEditorModel m, int index) {
            return new HStack(8f) {
                new Label(index == 0 ? "调整①" : "调整②") { Role = LabelRole.Muted }.Width(76f),
                Command(m, "复制", index == 0 ? AccessoryEditorCommand.Adjust1 : AccessoryEditorCommand.Adjust2, m.CanAdjust),
                Command(m, "左右反转", index == 0 ? AccessoryEditorCommand.Adjust1LeftRight : AccessoryEditorCommand.Adjust2LeftRight, m.CanAdjust),
                Command(m, "上下反转", index == 0 ? AccessoryEditorCommand.Adjust1UpDown : AccessoryEditorCommand.Adjust2UpDown, m.CanAdjust)
            }.Height(30f);
        }

        private static Widget BuildSettings(AccessoryEditorModel m) {
            return new ScrollView("accessory-settings") {
                new VStack(12f) {
                    new Label("数据同步") { Role = LabelRole.Subheading }.Height(28f),
                    new Label("默认只修改角色数据；需要查看最终结果时可立即重建模型。") {
                        Role = LabelRole.Muted, WordWrap = true
                    }.MinHeight(32f),
                    new HStack(12f) {
                        Setting(m, "每次操作后总是立即同步", AccessoryEditorSetting.ImmediateSync, m.ImmediateSync).Width(285f),
                        Command(m, "立即刷新数据并更新角色模型", AccessoryEditorCommand.RefreshModel)
                    }.Height(34f),
                    new Space(12f),
                    new Label("饰品高亮") { Role = LabelRole.Subheading }.Height(28f),
                    Setting(m, "启用选择高亮（最近选择为橙色，其余为蓝色）", AccessoryEditorSetting.SelectionHighlight, m.SelectionHighlight).Height(30f),
                    Setting(m, "操作完成后清除选区", AccessoryEditorSetting.ClearAfter, m.ClearAfter).Height(30f),
                    Setting(m, "复制/移动目标始终跟随当前服装搭配", AccessoryEditorSetting.FollowCoordinate, m.FollowCoordinate).Height(30f)
                }.ExpandWidth()
            }.ExpandHeight();
        }

        private static Widget CycleChoice(string label, string[] names, int selected, Action<int> changed, string key, bool enabled = true) {
            return new HStack(4f) {
                new Label(label).Width(42f),
                new ArrowButton(ArrowDirection.Left, delegate { Cycle(names, selected, -1, changed); }, key + "-previous")
                    .Enabled(enabled && names.Length > 0).Width(28f),
                Choice(string.Empty, names, selected, changed, key).Enabled(enabled && names.Length > 0).ExpandWidth(),
                new ArrowButton(ArrowDirection.Right, delegate { Cycle(names, selected, 1, changed); }, key + "-next")
                    .Enabled(enabled && names.Length > 0).Width(28f)
            };
        }

        private static void Cycle(string[] names, int selected, int direction, Action<int> changed) {
            if (changed != null && names.Length > 0) changed((selected + direction + names.Length) % names.Length);
        }

        private static Widget Choice(string label, string[] names, int selected, Action<int> changed, string key) {
            int[] ids = new int[names.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = i;
            return new ComboBox<int>(label, selected, ids,
                delegate(int i) { return i >= 0 && i < names.Length ? names[i] : "无"; }, changed, key) { LabelWidth = string.IsNullOrEmpty(label) ? 0f : 42f };
        }

        private static Widget SelectionButton(AccessoryEditorModel m, AccessoryListModel list, string text, AccessorySelectionCommand command) {
            return new Button(text, delegate {
                if (m.SelectionCommand != null) m.SelectionCommand(list.Role, command);
            }, "selection-" + list.Role + "-" + command).Width(command == AccessorySelectionCommand.NonEmpty ? 80f : command == AccessorySelectionCommand.None ? 64f : 52f);
        }

        private static Widget Setting(AccessoryEditorModel m, string text, AccessoryEditorSetting setting, bool value) {
            return new Checkbox(text, value, delegate(bool next) {
                if (m.ChangeSetting != null) m.ChangeSetting(setting, next);
            }, "accessory-setting-" + setting);
        }

        private static Widget Command(AccessoryEditorModel m, string text, AccessoryEditorCommand command,
            bool enabled = true, bool primary = false, float width = 0f) {
            Button button = new Button(text, delegate { Execute(m, command); }, "accessory-command-" + command) {
                Variant = primary ? ButtonVariant.Primary : ButtonVariant.Default
            }.Enabled(enabled).Height(32f);
            return width > 0f ? button.Width(width) : button.ExpandWidth();
        }

        private static void Execute(AccessoryEditorModel m, AccessoryEditorCommand command) {
            if (m.Execute != null) m.Execute(command);
        }
    }

    internal static class AccessorySelectionEditing {
        // Original toggle/range behavior, shared by accessory and clothing rows.
        internal static int Apply(HashSet<int> selection, int count, int anchor, int index, bool value, bool range) {
            if (selection == null || index < 0 || index >= count) return anchor;
            int first = range && anchor >= 0 && anchor < count ? Math.Min(anchor, index) : index;
            int last = range && anchor >= 0 && anchor < count ? Math.Max(anchor, index) : index;
            for (int i = first; i <= last; i++) {
                if (value) selection.Add(i); else selection.Remove(i);
            }
            return index;
        }
    }
}
