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
        public Action<int> ChooseCharacterPreset;
        public Action<int> ChooseGroupPreset;
        public Action<bool> LockAll;
        public Action<string> ChangeName;
        public Action<JuiceEditorCommand> Execute;
    }

    // Keep the pre-migration single-window order and actions. Only the effect
    // rows scroll; the two preset toolbars remain directly available below them.
    internal sealed class JuiceEditorView : View {
        internal JuiceEditorModel Model = new JuiceEditorModel();

        public override Widget Build(BuildContext context) {
            JuiceEditorModel m = Model;
            Window window = new Window("JuiceControl " + m.Version, 390f, 700f,
                key: "juice-window", minWidth: 390f, minHeight: 700f,
                closable: true, onVisibleChange: delegate(bool value) {
                    if (!value) Execute(m, JuiceEditorCommand.Close);
                });
            window.Add(new VStack(4f) {
                Selector("角色:", 42f, m.Characters, m.CharacterIndex, m.SelectCharacter, "juice-character", "无"),
                new Separator(),
                BuildEffects(m).ExpandWidth().ExpandHeight(),
                new HStack(4f) {
                    Command(m, "获取当前状态", JuiceEditorCommand.Refresh, m.HasCharacter),
                    new Checkbox("固定全部汁液", m.AllLocked, m.LockAll, "juice-lock-all")
                        .Enabled(m.HasCharacter).ExpandWidth()
                }.Height(32f),
                new HStack(4f) {
                    Command(m, "清空汁液", JuiceEditorCommand.Clear, m.HasCharacter),
                    Command(m, "全满汁液", JuiceEditorCommand.Fill, m.HasCharacter)
                }.Height(32f),
                new Separator(),
                BuildPresets(m),
                new Separator(),
                new Label(string.IsNullOrEmpty(m.Status) ? "就绪。" : m.Status, "juice-status") {
                    WordWrap = true
                }.MinHeight(20f)
            }.ExpandHeight());
            if (m.NameDialogVisible) window.Add(BuildNameDialog(m));
            return window;
        }

        private static Widget BuildEffects(JuiceEditorModel m) {
            if (!m.HasCharacter) return new Label("当前场景中没有找到角色。", "juice-no-character");
            return new VirtualList(m.Effects.Count, 32f, delegate(int i) {
                JuiceEffectRow effect = m.Effects[i];
                int maximum = Math.Max(0, effect.Maximum);
                int value = Math.Max(0, Math.Min(effect.Value, maximum));
                return new HStack(4f, "effect-" + effect.Id) {
                    new Label(effect.Name).Width(120f),
                    new Button("−", delegate { Change(effect, value, Math.Max(0, value - 1)); }, "decrease-" + effect.Id).Width(32f),
                    new Button(effect.DisplayValue ?? value.ToString(), delegate {
                        Change(effect, value, value >= maximum ? 0 : value + 1);
                    }, "value-" + effect.Id).ExpandWidth(),
                    new Button("+", delegate { Change(effect, value, Math.Min(maximum, value + (value < maximum ? 1 : 0))); }, "increase-" + effect.Id).Width(32f),
                    new Checkbox(string.Empty, effect.Locked, effect.Lock, "lock-" + effect.Id).Width(24f)
                }.Height(32f);
            }, "juice-effects") { Height = 0f }.MinHeight(120f);
        }

        private static void Change(JuiceEffectRow effect, int previous, int next) {
            if (next != previous && effect.Change != null) effect.Change(next);
        }

        private static Widget BuildPresets(JuiceEditorModel m) {
            bool characterPreset = m.CharacterPresets.Length > 0;
            bool groupPreset = m.GroupPresets.Length > 0;
            VStack content = new VStack(4f, "juice-presets");
            if (!m.CanSave) content.Add(new ScrollView("juice-preset-error-scroll") {
                new HelpBox("当前 XML 载入失败：仍可应用已载入预设；修复并重载后才能保存。\n" + (m.Error ?? string.Empty),
                    HelpBoxType.Error, "juice-preset-error").ExpandWidth()
            }.Height(100f));
            content.Add(Selector("汁液预设:", 70f, m.CharacterPresets, m.CharacterPresetIndex,
                m.SelectCharacterPreset, "juice-character-preset", "(无角色预设)", m.ChooseCharacterPreset));
            content.Add(new HStack(4f) {
                Command(m, "应用选中", JuiceEditorCommand.ApplyCharacter, m.HasCharacter && characterPreset),
                Command(m, "新建保存", JuiceEditorCommand.CreateCharacter, m.CanSave && m.HasCharacter),
                Command(m, "覆盖保存", JuiceEditorCommand.OverwriteCharacter, m.CanSave && m.HasCharacter && characterPreset),
                Command(m, "删除选中", JuiceEditorCommand.DeleteCharacter, m.CanSave && characterPreset)
            }.Height(32f));
            content.Add(Selector("场景预设:", 70f, m.GroupPresets, m.GroupPresetIndex,
                m.SelectGroupPreset, "juice-group-preset", "(无群体预设)", m.ChooseGroupPreset));
            content.Add(new HStack(4f) {
                Command(m, "应用选中", JuiceEditorCommand.ApplyGroup, m.Characters.Length > 0 && groupPreset),
                Command(m, "新建保存", JuiceEditorCommand.CreateGroup, m.CanSave && m.Characters.Length > 0),
                Command(m, "覆盖保存", JuiceEditorCommand.OverwriteGroup, m.CanSave && m.Characters.Length > 0 && groupPreset),
                Command(m, "删除选中", JuiceEditorCommand.DeleteGroup, m.CanSave && groupPreset)
            }.Height(32f));
            content.Add(new HStack(4f) {
                Command(m, "打开XML", JuiceEditorCommand.OpenXml),
                Command(m, "重载XML", JuiceEditorCommand.ReloadXml)
            }.Height(32f));
            return content;
        }

        private static Widget BuildNameDialog(JuiceEditorModel m) {
            return new Dialog("JuiceControl " + m.Version, "juice-name-dialog") {
                Width = 390f, Height = string.IsNullOrEmpty(m.NameError) ? 220f : 260f,
                OnClose = delegate { Execute(m, JuiceEditorCommand.CancelName); }
            }.Content(new VStack(8f) {
                new Label(m.NamePrompt ?? string.Empty) { WordWrap = true }.MinHeight(20f),
                new TextField(m.Name, m.ChangeName, "juice-preset-name") {
                    OnSubmit = delegate(string text) {
                        if (m.ChangeName != null) m.ChangeName(text);
                        Execute(m, JuiceEditorCommand.ConfirmName);
                    }
                }.DefaultFocus().Height(32f),
                MUI.If(!string.IsNullOrEmpty(m.NameError), new Label(m.NameError ?? string.Empty) { WordWrap = true }.MinHeight(20f)),
                new HStack(4f) {
                    Command(m, "确定", JuiceEditorCommand.ConfirmName, m.CanSave),
                    Command(m, "取消", JuiceEditorCommand.CancelName)
                }.Height(32f)
            });
        }

        private static Widget Selector(string label, float labelWidth, string[] names, int selected,
            Action<int> changed, string key, string emptyText, Action<int> chosen = null) {
            int[] ids = new int[names.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = i;
            bool enabled = names.Length > 0;
            return new HStack(4f, key + "-row") {
                new Label(label).Width(labelWidth),
                new ArrowButton(ArrowDirection.Left, delegate { SelectRelative(names.Length, selected, -1, changed); }, key + "-previous").Enabled(enabled).Width(28f),
                new ComboBox<int>(string.Empty, selected, ids,
                    delegate(int i) { return i >= 0 && i < names.Length ? names[i] : emptyText; }, chosen ?? changed, key) {
                    MaxVisibleItems = 10
                }.Enabled(enabled).ExpandWidth(),
                new ArrowButton(ArrowDirection.Right, delegate { SelectRelative(names.Length, selected, 1, changed); }, key + "-next").Enabled(enabled).Width(28f)
            }.Height(32f);
        }

        private static void SelectRelative(int count, int selected, int delta, Action<int> changed) {
            if (count == 0 || changed == null) return;
            int next = selected + delta;
            if (next < 0) next = count - 1;
            if (next >= count) next = 0;
            changed(next);
        }

        private static Widget Command(JuiceEditorModel m, string text, JuiceEditorCommand command, bool enabled = true) {
            return new Button(text, delegate { Execute(m, command); }, "juice-command-" + command)
                .Enabled(enabled).ExpandWidth().Height(32f);
        }

        private static void Execute(JuiceEditorModel m, JuiceEditorCommand command) {
            if (m.Execute != null) m.Execute(command);
        }
    }
}
