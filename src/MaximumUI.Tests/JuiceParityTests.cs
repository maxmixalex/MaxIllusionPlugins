using System;
using System.Collections.Generic;
using JuiceControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class JuiceParityTests {
        internal const int TestCount = 9;

        internal static void RunAll() {
            MainWindowPreservesLegacyOrderAndWords();
            ArrowSelectorsWrapWithoutApplyingPresets();
            NumericButtonsClampCycleAndPreserveFormatting();
            NameDialogKeepsPromptConfirmAndEnter();
            EmptySceneDisablesOnlyUnavailableActions();
            PresetToolbarsRemainOutsideTheEffectScroll();
            ReadbackDoesNotOverwriteKnownValuesOnFailure();
            UnavailableRowsAreReadOnlyDuringBuildAndInteraction();
            OptionalLoveJuiceControlsArePassiveAndReachable();
        }

        private static JuiceEditorView Create() {
            return new JuiceEditorView { Model = new JuiceEditorModel {
                Version = "test", Characters = new[] { "角色 A", "角色 B", "角色 C" }, HasCharacter = true,
                CharacterPresets = new[] { "预设 A", "预设 B" }, GroupPresets = new[] { "场景 A", "场景 B" },
                CanSave = true, Status = "就绪。"
            } };
        }

        private static void MainWindowPreservesLegacyOrderAndWords() {
            JuiceEditorView view = Create();
            Window root = (Window)view.Build(null);
            Require(root.Title == "JuiceControl test" && root.Width == 390f && root.Height == 700f,
                "preserve the original compact window and title");
            string[] commands = { "Refresh", "Clear", "Fill", "ApplyCharacter", "CreateCharacter", "OverwriteCharacter",
                "DeleteCharacter", "ApplyGroup", "CreateGroup", "OverwriteGroup", "DeleteGroup", "OpenXml", "ReloadXml" };
            string[] texts = { "获取当前状态", "清空汁液", "全满汁液", "应用选中", "新建保存", "覆盖保存", "删除选中",
                "应用选中", "新建保存", "覆盖保存", "删除选中", "打开XML", "重载XML" };
            for (int i = 0; i < commands.Length; i++)
                Require(Find<Button>(root, "juice-command-" + commands[i]).Text == texts[i], "legacy wording: " + commands[i]);
            Require(Find<Checkbox>(root, "juice-lock-all").Text == "固定全部汁液", "legacy all-effects lock wording");
            List<string> keys = new List<string>(); CollectKeys(root, keys);
            string[] order = { "juice-character", "juice-effects", "juice-command-Refresh", "juice-command-Clear",
                "juice-character-preset", "juice-command-ApplyCharacter", "juice-group-preset", "juice-command-ApplyGroup", "juice-command-OpenXml", "juice-status" };
            int previous = -1;
            foreach (string key in order) { int position = keys.IndexOf(key); Require(position > previous, "legacy section order: " + key); previous = position; }
            Require(!keys.Contains("juice-page-0") && !keys.Contains("juice-page-1"), "no added business tabs");
        }

        private static void OptionalLoveJuiceControlsArePassiveAndReachable() {
            JuiceEditorView view = Create();
            Widget unavailable = view.Build(null);
            Require(FindOrNull<Checkbox>(unavailable, "juice-love-enabled") == null,
                "missing LoveJuice does not expose a writable control");
            Require(FindOrNull<Label>(unavailable, "juice-love-unavailable") != null,
                "optional plugin status is visible");

            int edits = 0, opened = 0;
            string lastSource = string.Empty;
            view.Model.LoveJuice = new LoveJuiceSettings {
                Available = true, Enabled = false, Face = true, Body = true,
                Intimate = true, MalePre = false, Milk = false, Shower = false, Special = false
            };
            view.Model.ChangeLoveJuiceEnabled = delegate(bool value) {
                edits++; lastSource = "Enabled";
            };
            view.Model.ChangeLoveJuiceIntimate = delegate(bool value) {
                edits++; lastSource = "Intimate";
            };
            view.Model.ChangeLoveJuiceMalePre = delegate(bool value) {
                edits++; lastSource = "MalePre";
            };
            view.Model.OpenLoveJuice = delegate { opened++; };
            Widget ready = view.Build(null);
            Require(edits == 0 && opened == 0,
                "building an editor never writes to an optional effect plugin");
            Require(Find<Checkbox>(ready, "juice-love-intimate").Enabled &&
                Find<Checkbox>(ready, "juice-love-enabled").Enabled &&
                Find<Button>(ready, "juice-love-open").Enabled,
                "all optional controls are reachable inside the real JuiceControl view");
            Find<Checkbox>(ready, "juice-love-intimate").OnChange(false);
            Require(edits == 1 && lastSource == "Intimate",
                "source switch forwards only a deliberate user change");
            Find<Checkbox>(ready, "juice-love-enabled").OnChange(true);
            Find<Button>(ready, "juice-love-open").OnClick();
            Require(edits == 2 && lastSource == "Enabled" && opened == 1,
                "master switch and detail panel have independent actions");
            Find<Checkbox>(ready, "juice-love-male-pre").OnChange(true);
            Require(edits == 3 && lastSource == "MalePre",
                "optional male pre-fluid switch dispatches independently");
        }

        private static void ArrowSelectorsWrapWithoutApplyingPresets() {
            JuiceEditorView view = Create();
            int character = -1, preset = -1, group = -1, commands = 0;
            view.Model.SelectCharacter = delegate(int i) { character = i; };
            view.Model.SelectCharacterPreset = delegate(int i) { preset = i; };
            view.Model.SelectGroupPreset = delegate(int i) { group = i; };
            view.Model.Execute = delegate { commands++; };
            Widget root = view.Build(null);
            Find<ArrowButton>(root, "juice-character-previous").OnClick();
            Find<ArrowButton>(root, "juice-character-preset-previous").OnClick();
            Find<ArrowButton>(root, "juice-group-preset-previous").OnClick();
            Require(character == 2 && preset == 1 && group == 1 && commands == 0, "previous wraps each selection independently without applying it");
            view.Model.CharacterIndex = 2; view.Model.CharacterPresetIndex = 1; view.Model.GroupPresetIndex = 1;
            root = view.Build(null);
            Find<ArrowButton>(root, "juice-character-next").OnClick();
            Find<ArrowButton>(root, "juice-character-preset-next").OnClick();
            Find<ArrowButton>(root, "juice-group-preset-next").OnClick();
            Require(character == 0 && preset == 0 && group == 0 && commands == 0, "next wraps all three original selectors");
            int chosenPreset = -1, chosenGroup = -1;
            view.Model.ChooseCharacterPreset = delegate(int i) { chosenPreset = i; };
            view.Model.ChooseGroupPreset = delegate(int i) { chosenGroup = i; };
            root = view.Build(null);
            Find<ComboBox<int>>(root, "juice-character-preset").OnChange(1);
            Find<ComboBox<int>>(root, "juice-group-preset").OnChange(1);
            Require(chosenPreset == 1 && chosenGroup == 1 && preset == 0 && group == 0 && commands == 0,
                "dropdown selection retains its separate status callback without applying or changing arrow behavior");
        }

        private static void NumericButtonsClampCycleAndPreserveFormatting() {
            JuiceEditorView view = Create();
            int calls = 0, result = -1; bool locked = false;
            JuiceEffectRow effect = new JuiceEffectRow { Id = "blush", Name = "脸红", Maximum = 3, Value = 0, DisplayValue = "0%",
                Change = delegate(int value) { calls++; result = value; }, Lock = delegate(bool value) { locked = value; } };
            view.Model.Effects.Add(effect);
            Widget row = Find<VirtualList>(view.Build(null), "juice-effects").BuildItem(0);
            Require(Find<Button>(row, "value-blush").Text == "0%", "original formatted value is itself the cycle button");
            Find<Button>(row, "decrease-blush").OnClick(); Require(calls == 0, "decrement at zero is a no-op");
            Find<Button>(row, "increase-blush").OnClick(); Require(calls == 1 && result == 1 && !locked, "plus increments without changing the lock");
            effect.Value = 3; effect.DisplayValue = "100%";
            row = Find<VirtualList>(view.Build(null), "juice-effects").BuildItem(0);
            Find<Button>(row, "increase-blush").OnClick(); Require(calls == 1, "increment at the maximum is a no-op");
            Find<Button>(row, "value-blush").OnClick(); Require(result == 0 && calls == 2, "clicking the maximum value wraps to zero");
            Find<Checkbox>(row, "lock-blush").OnChange(true); Require(locked && calls == 2, "checkbox has its own lock action");
            effect.Maximum = 0; effect.Value = 8;
            row = Find<VirtualList>(view.Build(null), "juice-effects").BuildItem(0);
            Find<Button>(row, "value-blush").OnClick(); Require(calls == 2, "a zero-range effect cannot divide by zero or emit an invalid edit");
        }

        private static void ReadbackDoesNotOverwriteKnownValuesOnFailure() {
            int value;
            Require(!ReadbackState.TryConvert(null, 4, out value), "null is not zero");
            Require(!ReadbackState.TryConvert("invalid", 4, out value), "invalid reflection result is not zero");
            Require(!ReadbackState.TryConvert(7, 4, out value), "out-of-range read must not be truncated");
            Require(ReadbackState.TryConvert(0, 4, out value) && value == 0, "actual zero remains valid");

            Dictionary<string, int> levels = new Dictionary<string, int> {
                { "known", 3 }, { "zero", 2 }, { "removed", 1 }
            };
            ReadLevel read = delegate(string id, out int level) {
                level = 0;
                return id == "zero";
            };
            ReadbackState.RefreshKnown(levels, read);
            Require(levels["known"] == 3 && levels["zero"] == 0, "refresh preserves unreadable state and accepts real zero");
            ReadbackState.CaptureKnown(levels, new[] { "known", "zero", "unavailable" }, read);
            Require(levels.Count == 2 && levels["known"] == 3 && levels["zero"] == 0 &&
                !levels.ContainsKey("unavailable"), "capture cannot manufacture a zero lock for an unreadable row");
        }

        private static void UnavailableRowsAreReadOnlyDuringBuildAndInteraction() {
            JuiceEditorView view = Create();
            int writes = 0;
            JuiceEffectRow rowModel = new JuiceEffectRow {
                Id = "external", Name = "External", Value = 0, Maximum = 4, Available = false,
                Change = delegate { writes++; }, Lock = delegate { writes++; }
            };
            view.Model.Effects.Add(rowModel);
            for (int i = 0; i < 2; i++) {
                Widget row = Find<VirtualList>(view.Build(null), "juice-effects").BuildItem(0);
                Button current = Find<Button>(row, "value-external");
                Button increase = Find<Button>(row, "increase-external");
                Checkbox locked = Find<Checkbox>(row, "lock-external");
                Require(!current.Enabled && !increase.Enabled && !locked.Enabled && current.Text == "—", "unknown state is displayed without a fabricated numeric value");
                current.OnClick(); increase.OnClick(); locked.OnChange(true);
            }
            Require(writes == 0, "mount/rebuild and stale callbacks cannot write an unavailable value");
            rowModel.Locked = true;
            Widget lockedRow = Find<VirtualList>(view.Build(null), "juice-effects").BuildItem(0);
            Checkbox unlock = Find<Checkbox>(lockedRow, "lock-external");
            Require(unlock.Enabled, "an existing lock can still be released while its data source is unavailable");
            unlock.OnChange(false);
            Require(writes == 1, "only explicit unlock is allowed");
        }

        private static void NameDialogKeepsPromptConfirmAndEnter() {
            JuiceEditorView view = Create();
            view.Model.NameDialogVisible = true; view.Model.NamePrompt = "请输入汁液预设名"; view.Model.Name = "原名";
            string edited = null; JuiceEditorCommand command = JuiceEditorCommand.Close;
            view.Model.ChangeName = delegate(string value) { edited = value; };
            view.Model.Execute = delegate(JuiceEditorCommand value) { command = value; };
            Dialog dialog = Find<Dialog>(view.Build(null), "juice-name-dialog");
            Require(dialog.Title == "JuiceControl test", "the original modal title stays separate from its prompt");
            Require(Find<Button>(dialog, "juice-command-ConfirmName").Text == "确定" && Find<Button>(dialog, "juice-command-CancelName").Text == "取消",
                "name confirmation uses its original button text");
            Find<TextField>(dialog, "juice-preset-name").OnSubmit("新名");
            Require(edited == "新名" && command == JuiceEditorCommand.ConfirmName, "Enter submits the current input");
            dialog.OnClose(); Require(command == JuiceEditorCommand.CancelName, "closing remains cancellation");
        }

        private static void EmptySceneDisablesOnlyUnavailableActions() {
            JuiceEditorView view = Create(); view.Model.HasCharacter = false; view.Model.Characters = new string[0];
            Widget root = view.Build(null);
            foreach (string command in new[] { "Refresh", "Clear", "Fill", "ApplyCharacter", "CreateCharacter", "OverwriteCharacter", "ApplyGroup", "CreateGroup", "OverwriteGroup" })
                Require(!Find<Button>(root, "juice-command-" + command).Enabled, "no stale target for " + command);
            Require(!Find<ArrowButton>(root, "juice-character-previous").Enabled && !Find<ArrowButton>(root, "juice-character-next").Enabled,
                "empty character selector disables both arrows");
            Require(Find<Button>(root, "juice-command-DeleteCharacter").Enabled && Find<Button>(root, "juice-command-DeleteGroup").Enabled,
                "stored preset management remains usable without a scene character");
            Require(Find<Button>(root, "juice-command-OpenXml").Enabled && Find<Button>(root, "juice-command-ReloadXml").Enabled,
                "file recovery actions stay available");
        }

        private static void PresetToolbarsRemainOutsideTheEffectScroll() {
            JuiceEditorView view = Create();
            for (int i = 0; i < 30; i++) view.Model.Effects.Add(new JuiceEffectRow { Id = "row" + i, Name = "效果", Maximum = 3 });
            foreach (bool failure in new[] { false, true }) {
                view.Model.CanSave = !failure; view.Model.Error = failure ? "sample.xml: invalid data" : string.Empty;
                Window window = (Window)view.Build(null);
                LayoutNode root = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(ThemePalettes.MetroLight()))
                    .Layout(window, new MuiRect(0f, 0f, window.MinWidth, window.MinHeight));
                LayoutNode effects = FindLayout(root, "juice-effects"), presets = FindLayout(root, "juice-presets"), status = FindLayout(root, "juice-status");
                Require(effects.Rect.Height >= 120f, "minimum window preserves useful effect scrolling even during XML recovery");
                Require(presets.Rect.Y > effects.Rect.Bottom && status.Rect.Y >= presets.Rect.Bottom && status.Rect.Bottom <= window.Height,
                    "both preset toolbars and status remain beneath the independent effect viewport");
                Require(effects.Children.Count == 0, "effect rows remain virtual instead of building the whole list");
            }
        }

        private static void CollectKeys(Widget root, List<string> keys) {
            if (root.Key is string) keys.Add((string)root.Key);
            foreach (Widget child in root.Children) CollectKeys(child, keys);
        }

        private static T Find<T>(Widget root, string key) where T : Widget {
            T found = FindOrNull<T>(root, key);
            if (found == null) throw new InvalidOperationException("Missing " + key);
            return found;
        }

        private static T FindOrNull<T>(Widget root, string key) where T : Widget {
            if (root is T && Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) { T found = FindOrNull<T>(child, key); if (found != null) return found; }
            return null;
        }

        private static LayoutNode FindLayout(LayoutNode root, string key) {
            if (Equals(root.Widget.Key, key)) return root;
            foreach (LayoutNode child in root.Children) { LayoutNode found = FindLayout(child, key); if (found != null) return found; }
            return null;
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
