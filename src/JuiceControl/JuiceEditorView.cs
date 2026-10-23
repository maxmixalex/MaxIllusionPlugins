using System;
using System.Collections.Generic;
using MaximumUI;

namespace JuiceControl {
    internal enum JuiceEditorCommand {
        Close, Refresh, Clear, Fill, ApplyCharacter, CreateCharacter, OverwriteCharacter,
        DeleteCharacter, ApplyGroup, CreateGroup, OverwriteGroup, DeleteGroup,
        OpenXml, ReloadXml, ConfirmName, CancelName
    }

    internal sealed class JuiceEffectRow {
        public string Id;
        public string Name;
        public int Value;
        public int Maximum;
        public bool Locked;
        public string DisplayValue;
        public Action<int> Change;
        public Action<bool> Lock;
    }

    internal sealed class JuiceEditorModel {
        public string Version;
        public string[] Characters = new string[0];
        public int CharacterIndex;
        public bool HasCharacter;
        public List<JuiceEffectRow> Effects = new List<JuiceEffectRow>();
        public bool AllLocked;
        public string[] CharacterPresets = new string[0];
        public string[] GroupPresets = new string[0];
        public int CharacterPresetIndex;
        public int GroupPresetIndex;
        public bool CanSave;
        public string Error;
        public string Status;
        public string Integration;
        public bool NameDialogVisible;
        public string NamePrompt;
        public string Name;
        public string NameError;
        public Action<int> SelectCharacter;
        public Action<int> SelectCharacterPreset;
        public Action<int> SelectGroupPreset;
        public Action<bool> LockAll;
        public Action<string> ChangeName;
        public Action<JuiceEditorCommand> Execute;
    }

    // Pure composition: the game adapter supplies state and existing business actions.
    // Keeping this file free of game calls also lets regression tests exercise the
    // exact shipped control tree, write guards and callbacks.
    internal sealed class JuiceEditorView : View {
        internal JuiceEditorModel Model = new JuiceEditorModel();
        private int page;

        public override Widget Build(BuildContext context) {
            JuiceEditorModel m = Model;
            Window window = new Window("JuiceControl  " + m.Version, 620f, 760f,
                key: "juice-window", minWidth: 480f, minHeight: 560f,
                closable: true, onVisibleChange: delegate(bool value) {
                    if (!value) Execute(m, JuiceEditorCommand.Close);
                });
            window.Add(new VStack(10f) {
                new HStack(8f) {
                    new Label("角色效果") { Role = LabelRole.Heading }.ExpandWidth(),
                    new StatusBadge("JUICE", StatusBadgeKind.Info).Width(76f)
                }.Height(34f),
                Choice("角色", m.Characters, m.CharacterIndex, m.SelectCharacter, "juice-character")
                    .Enabled(m.Characters.Length > 0).Height(32f),
                new HStack(6f) {
                    PageButton("效果与锁定", 0), PageButton("角色 / 场景预设", 1)
                }.Height(32f),
                new Separator(),
                MUI.If(!m.CanSave, new HelpBox("XML 读取失败，保存和删除已暂停。详情见“角色 / 场景预设”。", HelpBoxType.Warning)
                    .MinHeight(44f)),
                (page == 0 ? BuildEffects(m) : BuildPresets(m)).ExpandWidth().ExpandHeight(),
                new Separator(),
                new Label(string.IsNullOrEmpty(m.Status) ? "就绪。" : m.Status) {
                    Role = LabelRole.Muted, WordWrap = true
                }.MinHeight(26f),
                new Label(m.Integration ?? string.Empty) { Role = LabelRole.Muted }.Height(22f)
            }.ExpandHeight());
            if (m.NameDialogVisible) window.Add(BuildNameDialog(m));
            return window;
        }

        private Widget PageButton(string text, int index) {
            return new Selectable(text, page == index, delegate(bool value) {
                page = index;
                MaximumUIRuntime.MarkDirty(this);
            }, "juice-page-" + index).ExpandWidth();
        }

        private Widget BuildEffects(JuiceEditorModel m) {
            if (!m.HasCharacter)
                return new HelpBox("当前场景中没有找到角色。进入角色编辑器或场景后会自动刷新。", HelpBoxType.Info);
            VirtualList rows = new VirtualList(m.Effects.Count, 40f, delegate(int i) {
                JuiceEffectRow effect = m.Effects[i];
                return new HStack(8f, "effect-" + effect.Id) {
                    new Label(effect.Name) { WordWrap = true }.Width(154f),
                    new SliderInt(string.Empty, effect.Value, 0, effect.Maximum, effect.Change, "value-" + effect.Id)
                        .ExpandWidth().Tooltip(effect.DisplayValue),
                    new Checkbox("锁定", effect.Locked, effect.Lock, "lock-" + effect.Id).Width(70f)
                }.Height(36f);
            }, "juice-effects") { Height = 0f };
            return new VStack(10f) {
                new HStack(10f) {
                    new Label("效果等级") { Role = LabelRole.Muted }.ExpandWidth(),
                    new Label("拖动或使用方向键调整") { Role = LabelRole.Muted }.Width(176f)
                }.Height(24f),
                rows.ExpandHeight().ExpandWidth(),
                new Checkbox("固定全部效果", m.AllLocked, m.LockAll, "juice-lock-all").Height(30f),
                new HStack(8f) {
                    Command(m, "获取当前状态", JuiceEditorCommand.Refresh),
                    Command(m, "清空效果", JuiceEditorCommand.Clear),
                    Command(m, "全部设满", JuiceEditorCommand.Fill)
                }.Height(32f)
            }.ExpandHeight();
        }

        private Widget BuildPresets(JuiceEditorModel m) {
            bool characterPreset = m.CharacterPresets.Length > 0;
            bool groupPreset = m.GroupPresets.Length > 0;
            VStack content = new VStack(12f) {
                new Label("角色预设") { Role = LabelRole.Heading }.Height(30f),
                Choice(string.Empty, m.CharacterPresets, m.CharacterPresetIndex,
                    m.SelectCharacterPreset, "juice-character-preset").Enabled(characterPreset).Height(32f),
                new Grid(2, 8f, 8f) {
                    Command(m, "应用到当前角色", JuiceEditorCommand.ApplyCharacter, m.HasCharacter && characterPreset, true),
                    Command(m, "新建保存", JuiceEditorCommand.CreateCharacter, m.CanSave && m.HasCharacter),
                    Command(m, "覆盖 / 重命名", JuiceEditorCommand.OverwriteCharacter, m.CanSave && m.HasCharacter && characterPreset),
                    Command(m, "删除选中", JuiceEditorCommand.DeleteCharacter, m.CanSave && characterPreset)
                },
                new Separator(),
                new Label("场景预设") { Role = LabelRole.Heading }.Height(30f),
                new Label("按场景角色位置保存与应用整组效果。") { Role = LabelRole.Muted, WordWrap = true }.MinHeight(24f),
                Choice(string.Empty, m.GroupPresets, m.GroupPresetIndex,
                    m.SelectGroupPreset, "juice-group-preset").Enabled(groupPreset).Height(32f),
                new Grid(2, 8f, 8f) {
                    Command(m, "应用到场景", JuiceEditorCommand.ApplyGroup, m.Characters.Length > 0 && groupPreset, true),
                    Command(m, "新建保存", JuiceEditorCommand.CreateGroup, m.CanSave && m.Characters.Length > 0),
                    Command(m, "覆盖 / 重命名", JuiceEditorCommand.OverwriteGroup, m.CanSave && m.Characters.Length > 0 && groupPreset),
                    Command(m, "删除选中", JuiceEditorCommand.DeleteGroup, m.CanSave && groupPreset)
                },
                new Separator(),
                new HStack(8f) {
                    Command(m, "打开 XML", JuiceEditorCommand.OpenXml),
                    Command(m, "重载 XML", JuiceEditorCommand.ReloadXml)
                }.Height(32f)
            };
            if (!m.CanSave) content.Add(new HelpBox(
                "预设读取失败，保存和删除已暂停。仍可应用上次载入的预设。\n" + (m.Error ?? string.Empty), HelpBoxType.Error,
                "juice-preset-error").MinHeight(100f));
            return new ScrollView("juice-presets-scroll") { content.ExpandWidth() }.ExpandHeight();
        }

        private Widget BuildNameDialog(JuiceEditorModel m) {
            return new Dialog(m.NamePrompt, "juice-name-dialog") {
                Width = 420f, Height = string.IsNullOrEmpty(m.NameError) ? 180f : 230f,
                OnClose = delegate { Execute(m, JuiceEditorCommand.CancelName); },
            }.Content(new VStack(12f) {
                new TextField(m.Name, m.ChangeName, "juice-preset-name") {
                    OnSubmit = delegate(string text) {
                        if (m.ChangeName != null) m.ChangeName(text);
                        Execute(m, JuiceEditorCommand.ConfirmName);
                    }
                }
                    .DefaultFocus().Height(32f),
                MUI.If(!string.IsNullOrEmpty(m.NameError), new HelpBox(m.NameError ?? string.Empty, HelpBoxType.Error)),
                new HStack(8f) {
                    Command(m, "保存", JuiceEditorCommand.ConfirmName, m.CanSave, true),
                    Command(m, "取消", JuiceEditorCommand.CancelName)
                }.Height(32f)
            });
        }

        private static Widget Choice(string label, string[] names, int selected, Action<int> changed, string key) {
            int[] ids = new int[names.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = i;
            return new SearchableComboBox<int>(label, selected, ids,
                delegate(int i) { return i >= 0 && i < names.Length ? names[i] : "无"; }, changed, key) {
                    LabelWidth = string.IsNullOrEmpty(label) ? 0f : 50f, FilterPlaceholder = "搜索…", MaxVisibleItems = 10
                }.ExpandWidth();
        }

        private static Widget Command(JuiceEditorModel m, string text, JuiceEditorCommand command,
            bool enabled = true, bool primary = false) {
            return new Button(text, delegate { Execute(m, command); }, "juice-command-" + command) {
                Variant = primary ? ButtonVariant.Primary : ButtonVariant.Default
            }.Enabled(enabled).ExpandWidth().Height(32f);
        }

        private static void Execute(JuiceEditorModel m, JuiceEditorCommand command) {
            if (m.Execute != null) m.Execute(command);
        }
    }
}
