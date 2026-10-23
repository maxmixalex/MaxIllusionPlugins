using System;
using System.Collections.Generic;
using AccessoryBatchManager;
using JuiceControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class PluginEditorViewTests {
        internal const int TestCount = 19;

        internal static void RunAll() {
            JuiceEmptySceneAndXmlErrorStayVisible();
            JuiceEffectRowsRouteValueAndLockSeparately();
            JuicePresetFailureBlocksEveryWriteButKeepsReadActions();
            JuicePresetActionsAndSelectionsHaveDistinctTargets();
            JuiceNameDialogPreservesEditingAndCancel();
            JuiceBuildsOnlyTheActivePage();
            JuiceReloadInvalidatesOldPresetButtons();
            JuiceSameNameReplacementTracksCharacterIdentity();
            JuiceDisplayRefreshKeepsPendingValueEdit();
            JuiceShutdownReleasesNamingTargetsAndOldCallbacks();
            JuiceSessionGuardsAllControlsAndAllowsDismissal();
            AccessoryBatchActionsKeepTheirTargets();
            AccessoryVirtualRowsBoundFormattingWork();
            AccessoryCopyModeKeepsOperationGates();
            AccessorySelectionCarriesRoleAndModifiers();
            AccessoryShiftRangesToggleAndRespectBounds();
            AccessorySettingsDispatchPersistedChoices();
            AccessoryConfirmationDoesNotCommitOnClose();
            PluginWindowCompositionUsesNativeWidgetsAndResponsiveLists();
            Console.WriteLine("Plugin MaximumUI editor regressions: " + TestCount + " groups passed.");
        }

        private static void JuiceEmptySceneAndXmlErrorStayVisible() {
            JuiceEditorView view = Juice();
            view.Model.HasCharacter = false;
            view.Model.Characters = new string[0];
            view.Model.CanSave = false;
            Widget root = view.Build(null);
            Require(!Find<SearchableComboBox<int>>(root, "juice-character").Enabled, "empty scene cannot select a missing character");
            Require(FindAny<VirtualList>(root) == null, "empty scene does not expose stale effects");
            Require(Count<HelpBox>(root) == 2, "scene absence and XML failure are independently visible on effects page");
        }

        private static void JuiceEffectRowsRouteValueAndLockSeparately() {
            JuiceEditorView view = Juice();
            int changed = -1; bool locked = false; bool allLocked = false;
            view.Model.LockAll = delegate(bool value) { allLocked = value; };
            view.Model.Effects.Add(new JuiceEffectRow {
                Id = "external", Name = "Optional effect", Value = 2, Maximum = 7, Locked = false, DisplayValue = "2 / 7",
                Change = delegate(int value) { changed = value; }, Lock = delegate(bool value) { locked = value; }
            });
            Widget root = view.Build(null);
            VirtualList list = Find<VirtualList>(root, "juice-effects");
            Widget row = list.BuildItem(0);
            SliderInt valueControl = Find<SliderInt>(row, "value-external");
            Require(valueControl.Value == 2 && valueControl.Max == 7, "optional effects retain their own maximum");
            valueControl.OnChange(6);
            Require(changed == 6 && !locked, "changing a value does not accidentally toggle lock");
            Find<Checkbox>(row, "lock-external").OnChange(true);
            Find<Checkbox>(root, "juice-lock-all").OnChange(true);
            Require(locked && allLocked, "per-effect and all-effect lock callbacks remain available");
        }

        private static void JuicePresetFailureBlocksEveryWriteButKeepsReadActions() {
            JuiceEditorView view = Juice();
            view.Model.CharacterPresets = new[] { "one" };
            view.Model.GroupPresets = new[] { "scene" };
            view.Model.CanSave = false;
            view.Model.Error = "sample.xml: malformed document";
            SelectJuicePage(view, 1);
            Widget root = view.Build(null);
            JuiceEditorCommand[] writes = {
                JuiceEditorCommand.CreateCharacter, JuiceEditorCommand.OverwriteCharacter, JuiceEditorCommand.DeleteCharacter,
                JuiceEditorCommand.CreateGroup, JuiceEditorCommand.OverwriteGroup, JuiceEditorCommand.DeleteGroup
            };
            foreach (JuiceEditorCommand command in writes)
                Require(!Find<Button>(root, "juice-command-" + command).Enabled, "failed XML must disable " + command);
            Require(Find<Button>(root, "juice-command-ApplyCharacter").Enabled &&
                Find<Button>(root, "juice-command-ApplyGroup").Enabled && Find<Button>(root, "juice-command-ReloadXml").Enabled,
                "last valid data and recovery remain usable");
            Require(Find<HelpBox>(root, "juice-preset-error").Message.IndexOf("sample.xml", StringComparison.Ordinal) >= 0,
                "actual parser path and detail are retained in the scrollable page");
        }

        private static void JuicePresetActionsAndSelectionsHaveDistinctTargets() {
            JuiceEditorView view = Juice();
            view.Model.CharacterPresets = new[] { "one", "two" };
            view.Model.GroupPresets = new[] { "first", "second" };
            JuiceEditorCommand seen = JuiceEditorCommand.Close;
            int character = -1; int group = -1;
            view.Model.Execute = delegate(JuiceEditorCommand command) { seen = command; };
            view.Model.SelectCharacterPreset = delegate(int value) { character = value; };
            view.Model.SelectGroupPreset = delegate(int value) { group = value; };
            SelectJuicePage(view, 1);
            Widget root = view.Build(null);
            JuiceEditorCommand[] commands = {
                JuiceEditorCommand.ApplyCharacter, JuiceEditorCommand.CreateCharacter, JuiceEditorCommand.OverwriteCharacter,
                JuiceEditorCommand.DeleteCharacter, JuiceEditorCommand.ApplyGroup, JuiceEditorCommand.CreateGroup,
                JuiceEditorCommand.OverwriteGroup, JuiceEditorCommand.DeleteGroup, JuiceEditorCommand.OpenXml, JuiceEditorCommand.ReloadXml
            };
            foreach (JuiceEditorCommand command in commands) {
                Button button = Find<Button>(root, "juice-command-" + command);
                Require(button.Enabled, "valid presets enable " + command);
                button.OnClick(); Require(seen == command, "button targets correct preset operation: " + command);
            }
            Find<SearchableComboBox<int>>(root, "juice-character-preset").OnChange(1);
            Require(character == 1 && group == -1, "character and group selections are independent");
            Find<SearchableComboBox<int>>(root, "juice-group-preset").OnChange(0);
            Require(group == 0, "group picker keeps its own callback");
        }

        private static void JuiceNameDialogPreservesEditingAndCancel() {
            JuiceEditorView view = Juice();
            view.Model.NameDialogVisible = true;
            view.Model.Name = "Example";
            view.Model.NamePrompt = "Save preset";
            string typed = null; JuiceEditorCommand seen = JuiceEditorCommand.Close;
            view.Model.ChangeName = delegate(string value) { typed = value; };
            view.Model.Execute = delegate(JuiceEditorCommand command) { seen = command; };
            Widget root = view.Build(null);
            Dialog dialog = Find<Dialog>(root, "juice-name-dialog");
            TextField input = Find<TextField>(dialog, "juice-preset-name");
            Require(input.Value == "Example" && input.HasDefaultFocus, "name editor has actual text and modal default focus");
            input.OnChange("Edited"); Require(typed == "Edited", "typing reaches the naming model");
            dialog.OnClose(); Require(seen == JuiceEditorCommand.CancelName, "Escape/cancel never save");
            Find<Button>(dialog, "juice-command-ConfirmName").OnClick();
            Require(seen == JuiceEditorCommand.ConfirmName, "explicit Save dispatches naming confirmation");
        }

        private static void JuiceBuildsOnlyTheActivePage() {
            JuiceEditorView view = Juice();
            Widget root = view.Build(null);
            Require(FindAny<VirtualList>(root) != null && FindAny<ScrollView>(root) == null, "effects page does not build preset tree");
            SelectJuicePage(view, 1); root = view.Build(null);
            Require(FindAny<VirtualList>(root) == null && FindAny<ScrollView>(root) != null, "presets page does not build effect rows");
        }

        private static void JuiceReloadInvalidatesOldPresetButtons() {
            JuiceEditorSession session = new JuiceEditorSession();
            object character = new object(); object library = new object();
            object characterPreset = new object(); object groupPreset = new object();
            int deletedCharacters = 0, deletedGroups = 0, selections = 0;
            Action<JuiceEditorCommand> execute = delegate(JuiceEditorCommand command) {
                if (command == JuiceEditorCommand.DeleteCharacter) deletedCharacters++;
                if (command == JuiceEditorCommand.DeleteGroup) deletedGroups++;
            };
            JuiceEditorView first = Juice();
            first.Model.CharacterPresets = new[] { "same name" }; first.Model.GroupPresets = new[] { "same name" };
            first.Model.Execute = execute;
            first.Model.SelectCharacterPreset = delegate { selections++; };
            session.Bind(first.Model, new[] { character }, new[] { library, character, characterPreset, groupPreset }, "first.xml", delegate { return true; }, delegate { return true; });
            SelectJuicePage(first, 1);
            Widget oldTree = first.Build(null);
            Button oldCharacterDelete = Find<Button>(oldTree, "juice-command-DeleteCharacter");
            Button oldGroupDelete = Find<Button>(oldTree, "juice-command-DeleteGroup");
            SearchableComboBox<int> oldPicker = Find<SearchableComboBox<int>>(oldTree, "juice-character-preset");

            // The real reload path invalidates before reading, even if loading
            // fails and PresetStore retains its previous Data instance.
            session.Invalidate();
            JuiceEditorView second = Juice();
            second.Model.CharacterPresets = new[] { "same name" }; second.Model.GroupPresets = new[] { "same name" };
            second.Model.Execute = execute;
            session.Bind(second.Model, new[] { character }, new[] { library, character, characterPreset, groupPreset }, "second.xml", delegate { return true; }, delegate { return true; });
            oldCharacterDelete.OnClick(); oldGroupDelete.OnClick(); oldPicker.OnChange(0);
            Require(deletedCharacters == 0 && deletedGroups == 0 && selections == 0,
                "queued buttons/pickers from the previous file cannot mutate the replacement library at the same indexes");
            SelectJuicePage(second, 1);
            Widget currentTree = second.Build(null);
            Find<Button>(currentTree, "juice-command-DeleteCharacter").OnClick();
            Find<Button>(currentTree, "juice-command-DeleteGroup").OnClick();
            Require(deletedCharacters == 1 && deletedGroups == 1, "freshly bound buttons still dispatch both preset delete operations");
            Button currentDelete = Find<Button>(currentTree, "juice-command-DeleteCharacter");
            JuiceEditorModel third = Juice().Model; third.Execute = execute;
            session.Bind(third, new[] { character }, new[] { new object(), character, characterPreset, groupPreset }, "second.xml", delegate { return true; }, delegate { return true; });
            currentDelete.OnClick();
            Require(deletedCharacters == 1, "a replacement library invalidates callbacks even when its path and selected names are unchanged");
        }

        private static void JuiceSameNameReplacementTracksCharacterIdentity() {
            JuiceEditorSession session = new JuiceEditorSession();
            object first = new object(), oldSecond = new object(), replacement = new object(), selected = null;
            object[] live = { first, oldSecond };
            object[] targets = { first };
            Func<bool> current = delegate { return session.CharacterCount == live.Length && session.MatchesCharacterAt(0, live[0]) && session.MatchesCharacterAt(1, live[1]); };
            JuiceEditorView oldView = Juice(); oldView.Model.Characters = new[] { "A", "Same display name" };
            oldView.Model.SelectCharacter = delegate(int index) { selected = index == 0 ? first : oldSecond; };
            session.Bind(oldView.Model, live, targets, "presets.xml", delegate { return true; }, current);
            SearchableComboBox<int> oldPicker = Find<SearchableComboBox<int>>(oldView.Build(null), "juice-character");
            live[1] = replacement;
            Require(!session.MatchesCharacterAt(1, replacement), "snapshot retains character object identity despite an equal name/count");
            oldPicker.OnChange(1);
            Require(selected == null, "a stale picker cannot select the removed character before the next view rebuild");
            JuiceEditorView next = Juice(); next.Model.Characters = new[] { "A", "Same display name" };
            next.Model.SelectCharacter = delegate(int index) { selected = live[index]; };
            session.Bind(next.Model, live, targets, "presets.xml", delegate { return true; }, current);
            oldPicker.OnChange(1);
            Require(selected == null, "rebinding the replacement does not reactivate the previous picker");
            Find<SearchableComboBox<int>>(next.Build(null), "juice-character").OnChange(1);
            Require(ReferenceEquals(selected, replacement), "fresh identical-looking option selects the replacement character");
        }

        private static void JuiceDisplayRefreshKeepsPendingValueEdit() {
            JuiceEditorSession session = new JuiceEditorSession();
            object character = new object(), library = new object();
            object[] identities = { character }; object[] targets = { library, character };
            int applied = -1;
            JuiceEditorView oldView = Juice();
            oldView.Model.Effects.Add(new JuiceEffectRow {
                Id = "effect", Name = "Effect", Maximum = 3, Value = 1,
                Change = delegate(int value) { applied = value; }
            });
            session.Bind(oldView.Model, identities, targets, "same.xml", delegate { return true; }, delegate { return true; });
            VirtualList oldRows = Find<VirtualList>(oldView.Build(null), "juice-effects");
            SliderInt pending = Find<SliderInt>(oldRows.BuildItem(0), "value-effect");
            JuiceEditorModel refreshed = Juice().Model;
            refreshed.Status = "Updated displayed level";
            refreshed.Effects.Add(new JuiceEffectRow { Id = "effect", Name = "Effect", Maximum = 3, Value = 2 });
            session.Bind(refreshed, new[] { character }, new[] { library, character }, new string("same.xml".ToCharArray()), delegate { return true; }, delegate { return true; });
            pending.OnChange(3);
            Require(applied == 3, "polling a different displayed value before callback flush preserves the last pending slider edit for the same targets");
        }

        private static void JuiceShutdownReleasesNamingTargetsAndOldCallbacks() {
            JuiceEditorSession session = new JuiceEditorSession();
            object character = new object(), library = new object();
            object namingCharacter = character, namingPreset = new object();
            bool active = true; int saves = 0, clears = 0;
            JuiceEditorView view = Juice();
            view.Model.NameDialogVisible = true; view.Model.NamePrompt = "Save"; view.Model.Name = "Name";
            view.Model.Execute = delegate(JuiceEditorCommand command) { if (command == JuiceEditorCommand.ConfirmName) saves++; if (command == JuiceEditorCommand.Clear) clears++; };
            session.Bind(view.Model, new[] { character }, new[] { library, character }, "same.xml", delegate { return active; }, delegate { return true; });
            Widget tree = view.Build(null);
            Button oldSave = Find<Button>(tree, "juice-command-ConfirmName");
            Button oldClear = Find<Button>(tree, "juice-command-Clear");
            active = false;
            session.Close(delegate { namingCharacter = null; namingPreset = null; });
            oldSave.OnClick(); oldClear.OnClick();
            Require(namingCharacter == null && namingPreset == null && session.CharacterCount == 0 && saves == 0 && clears == 0,
                "shutdown dismisses naming targets, releases identity snapshots and suppresses queued edits");
            active = true;
            JuiceEditorView reopened = Juice();
            // Fresh business actions are rebound by the adapter when it remounts.
            reopened.Model.Execute = delegate(JuiceEditorCommand command) { if (command == JuiceEditorCommand.Clear) clears++; };
            session.Bind(reopened.Model, new[] { character }, new[] { library, character }, "same.xml", delegate { return active; }, delegate { return true; });
            oldSave.OnClick(); oldClear.OnClick();
            Require(saves == 0 && clears == 0, "reopening the same targets cannot replay callbacks from a disabled component");
            Find<Button>(reopened.Build(null), "juice-command-Clear").OnClick();
            Require(clears == 1, "fresh controls work after remounting");
        }

        private static void JuiceSessionGuardsAllControlsAndAllowsDismissal() {
            JuiceEditorSession session = new JuiceEditorSession();
            bool active = false, targetCurrent = true; int writes = 0, dismissals = 0;
            JuiceEditorModel model = Juice().Model;
            model.SelectCharacter = delegate { writes++; }; model.SelectCharacterPreset = delegate { writes++; };
            model.SelectGroupPreset = delegate { writes++; }; model.ChangeName = delegate { writes++; }; model.LockAll = delegate { writes++; };
            model.Execute = delegate(JuiceEditorCommand command) { if (command == JuiceEditorCommand.Close || command == JuiceEditorCommand.CancelName) dismissals++; else writes++; };
            model.Effects.Add(new JuiceEffectRow { Change = delegate { writes++; }, Lock = delegate { writes++; } });
            session.Bind(model, new object[0], new object[0], "same.xml", delegate { return active; }, delegate { return targetCurrent; });
            Action exercise = delegate {
                model.SelectCharacter(0); model.SelectCharacterPreset(0); model.SelectGroupPreset(0); model.ChangeName("value");
                model.LockAll(true); model.Effects[0].Change(1); model.Effects[0].Lock(true); model.Execute(JuiceEditorCommand.Fill);
            };
            exercise(); Require(writes == 0, "every retained callback observes component/window activity");
            active = true; targetCurrent = false;
            exercise(); Require(writes == 0, "every retained business callback checks current target identity");
            model.Execute(JuiceEditorCommand.Close); model.Execute(JuiceEditorCommand.CancelName);
            Require(dismissals == 2, "a current UI can still close/cancel after its business target disappears");
            targetCurrent = true; exercise(); Require(writes == 8, "each fresh callback still reaches its own business action");
        }

        private static void AccessoryBatchActionsKeepTheirTargets() {
            AccessoryEditorView view = Accessory();
            AccessoryEditorCommand seen = AccessoryEditorCommand.Close;
            AccessoryListRole selectedRole = AccessoryListRole.Destination;
            AccessorySelectionCommand selectedCommand = AccessorySelectionCommand.ToggleKind;
            view.Model.Execute = delegate(AccessoryEditorCommand command) { seen = command; };
            view.Model.SelectionCommand = delegate(AccessoryListRole role, AccessorySelectionCommand command) { selectedRole = role; selectedCommand = command; };
            Widget root = view.Build(null);
            Require(!Find<Button>(root, "accessory-command-Remove").Enabled, "empty selection disables removing slots");
            view.Model.Batch.Selection.Add(2); root = view.Build(null);
            Find<Button>(root, "accessory-command-Remove").OnClick(); Require(seen == AccessoryEditorCommand.Remove, "remove dispatches original remove flow");
            Find<Button>(root, "accessory-command-Compact").OnClick(); Require(seen == AccessoryEditorCommand.Compact, "compact remains separate from removal");
            Find<Button>(root, "selection-Batch-NonEmpty").OnClick();
            Require(selectedRole == AccessoryListRole.Batch && selectedCommand == AccessorySelectionCommand.NonEmpty, "non-empty batch selection routes role and operation");
        }

        private static void AccessoryVirtualRowsBoundFormattingWork() {
            AccessoryEditorView view = Accessory();
            int names = 0;
            view.Model.Batch.Count = 100000;
            view.Model.Batch.GetName = delegate(int i) { names++; return "Item " + i; };
            Widget root = view.Build(null);
            Require(names == 0, "building the real editor never formats all 100,000 item names");
            VirtualList list = Find<VirtualList>(root, "accessory-list-Batch");
            VirtualListCache cache = new VirtualListCache();
            LayoutEngine engine = new LayoutEngine();
            for (int frame = 0; frame < 300; frame++) {
                cache.BeginRange(list, 0, 13, 0, engine);
                for (int i = 0; i < 13; i++) cache.GetRow(i, new MuiRect(0, i * 30f, 500f, 30f), "slot" + i);
            }
            Require(names == 13 && cache.BuildCount == 13, "actual accessory row factory resolves only 13 visible names across 300 events");
            cache.BeginRange(list, 1, 14, 0, engine);
            for (int i = 1; i < 14; i++) cache.GetRow(i, new MuiRect(0, i * 30f, 500f, 30f), "slot" + i);
            Require(names == 14 && cache.Count == 13, "one-row scroll resolves only one new accessory name");
        }

        private static void AccessoryCopyModeKeepsOperationGates() {
            AccessoryEditorView view = Accessory(); view.Model.Page = 1;
            view.Model.CanCopy = true; view.Model.CanMove = false; view.Model.CanInsert = true; view.Model.CanAdjust = false;
            view.Model.FollowCoordinate = true;
            Widget root = view.Build(null);
            Require(Find<Button>(root, "accessory-command-Copy").Enabled && !Find<Button>(root, "accessory-command-Move").Enabled,
                "card-source/copy-only mapping cannot enable moving");
            Require(Find<Button>(root, "accessory-command-InsertFirst").Enabled && Find<Button>(root, "accessory-command-InsertLast").Enabled,
                "both insert positions remain available");
            Require(!Find<Button>(root, "accessory-command-Adjust1").Enabled && !Find<Button>(root, "accessory-command-Adjust2UpDown").Enabled,
                "clothing mappings do not enable accessory transform copies");
            Require(!Find<ComboBox<int>>(root, "accessory-destination").Enabled, "follow-current setting locks the destination picker");
            Find<ComboBox<int>>(root, "accessory-source").OnChange(2);
            Require(view.Model.SourceIndex == 2 && view.Model.DestinationIndex == 0, "source selection targets its own coordinate");
            view.Model.FollowCoordinate = false;
            Find<ComboBox<int>>(view.Build(null), "accessory-destination").OnChange(1);
            Require(view.Model.DestinationIndex == 1 && view.Model.SourceIndex == 2, "unlocked target selection stays independent");
        }

        private static void AccessorySelectionCarriesRoleAndModifiers() {
            AccessoryEditorView view = Accessory(); view.Model.Page = 1;
            AccessoryListRole seenRole = AccessoryListRole.Batch; int seenIndex = -1; SelectionRequest seenRequest = null;
            view.Model.SelectSlot = delegate(AccessoryListRole role, int index, SelectionRequest request) {
                seenRole = role; seenIndex = index; seenRequest = request;
            };
            Widget root = view.Build(null);
            VirtualList source = Find<VirtualList>(root, "accessory-list-Source");
            VirtualList destination = Find<VirtualList>(root, "accessory-list-Destination");
            SelectionRequest selection = new SelectionRequest { Range = true, Additive = true, Selected = true };
            ((Selectable)source.BuildItem(3)).OnSelect(selection);
            Require(seenRole == AccessoryListRole.Source && seenIndex == 3 && ReferenceEquals(seenRequest, selection), "source row preserves captured Shift/modifier request");
            ((Selectable)destination.BuildItem(7)).OnSelect(selection);
            Require(seenRole == AccessoryListRole.Destination && seenIndex == 7, "destination rows cannot modify source selection");
            Require(ReferenceEquals(source.ScrollPosition, destination.ScrollPosition), "sync mode shares retained scroll state");
        }

        private static void AccessoryShiftRangesToggleAndRespectBounds() {
            HashSet<int> selected = new HashSet<int>();
            int anchor = AccessorySelectionEditing.Apply(selected, 12, -1, 2, true, false);
            anchor = AccessorySelectionEditing.Apply(selected, 12, anchor, 6, true, true);
            Require(selected.SetEquals(new[] { 2, 3, 4, 5, 6 }) && anchor == 6, "Shift selects inclusive anchor range");
            anchor = AccessorySelectionEditing.Apply(selected, 12, anchor, 4, false, true);
            Require(selected.SetEquals(new[] { 2, 3 }) && anchor == 4, "Shift on deselection removes an inclusive range");
            anchor = AccessorySelectionEditing.Apply(selected, 12, 99, 11, true, true);
            Require(selected.Count == 3 && selected.Contains(11), "stale anchor cannot select outside available slots");
            Require(AccessorySelectionEditing.Apply(selected, 12, anchor, 12, true, true) == anchor && selected.Count == 3,
                "out-of-range stale row request leaves selection unchanged");
        }

        private static void AccessorySettingsDispatchPersistedChoices() {
            AccessoryEditorView view = Accessory(); view.Model.Page = 2;
            AccessoryEditorSetting seen = AccessoryEditorSetting.ReverseParent; bool value = false;
            view.Model.ChangeSetting = delegate(AccessoryEditorSetting setting, bool next) { seen = setting; value = next; };
            Widget root = view.Build(null);
            AccessoryEditorSetting[] settings = {
                AccessoryEditorSetting.ImmediateSync, AccessoryEditorSetting.SelectionHighlight,
                AccessoryEditorSetting.ClearAfter, AccessoryEditorSetting.FollowCoordinate
            };
            foreach (AccessoryEditorSetting setting in settings) {
                Find<Checkbox>(root, "accessory-setting-" + setting).OnChange(true);
                Require(seen == setting && value, "setting uses original persistent option: " + setting);
            }
        }

        private static void AccessoryConfirmationDoesNotCommitOnClose() {
            AccessoryEditorView view = Accessory(); view.Model.Confirming = true; view.Model.Confirmation = "Remove selected slots?";
            AccessoryEditorCommand seen = AccessoryEditorCommand.Close;
            view.Model.Execute = delegate(AccessoryEditorCommand command) { seen = command; };
            Widget root = view.Build(null); Dialog dialog = Find<Dialog>(root, "accessory-confirm");
            Require(seen == AccessoryEditorCommand.Close, "building a confirmation performs no mutation");
            dialog.OnClose(); Require(seen == AccessoryEditorCommand.Cancel, "Escape/close cancels pending operation");
            Find<Button>(dialog, "accessory-command-Confirm").OnClick();
            Require(seen == AccessoryEditorCommand.Confirm, "only explicit confirmation commits");
        }

        private static void PluginWindowCompositionUsesNativeWidgetsAndResponsiveLists() {
            AccessoryEditorView accessory = Accessory(); accessory.Model.Page = 1;
            Widget root = accessory.Build(null);
            Require(((Window)root).Closable && ((Window)root).Resizable, "embedded editors use native MaximumUI window lifecycle");
            Require(Find<VirtualList>(root, "accessory-list-Source").Layout.ExpandHeight &&
                Find<VirtualList>(root, "accessory-list-Destination").Layout.ExpandHeight,
                "resizing the editor changes both list viewports instead of retaining old fixed heights");
            LayoutNode layout = new LayoutEngine().Layout(root, new MuiRect(0f, 0f, 900f, 760f), "accessory");
            Require(layout != null && Count<VirtualList>(root) == 2, "the full copy page measures as two virtual lists");
            RequireMinimumListViewport((Window)root, "accessory-list-Source", 60f);
            RequireMinimumListViewport((Window)root, "accessory-list-Destination", 60f);
            JuiceEditorView juice = Juice();
            juice.Model.CanSave = false;
            root = juice.Build(null);
            Require(Find<VirtualList>(root, "juice-effects").Layout.ExpandHeight, "Juice effects fill remaining height");
            RequireMinimumListViewport((Window)root, "juice-effects", 80f);
            Require(Count<DrawCanvas>(root) == 0, "the migrated editor does not wrap old GUILayout in custom drawing");
        }

        private static void RequireMinimumListViewport(Window window, string key, float minimum) {
            LayoutMetrics metro = new LayoutMetrics {
                RowHeight = 26f, SmallRowHeight = 17f, WindowPaddingLeft = 16f, WindowPaddingTop = 44f,
                WindowPaddingRight = 16f, WindowPaddingBottom = 14f, PanelPadding = 12f, TextWidth = 7.8f
            };
            LayoutNode root = new LayoutEngine(metro).Layout(window, new MuiRect(0f, 0f, window.MinWidth, window.MinHeight));
            LayoutNode list = FindLayout(root, key);
            Require(list != null && list.Rect.Height >= minimum,
                "minimum window size must keep at least two list rows visible: " + key + " height=" + (list == null ? -1f : list.Rect.Height));
        }

        private static LayoutNode FindLayout(LayoutNode node, string key) {
            if (object.Equals(node.Widget.Key, key)) return node;
            foreach (LayoutNode child in node.Children) { LayoutNode found = FindLayout(child, key); if (found != null) return found; }
            return null;
        }

        private static JuiceEditorView Juice() {
            JuiceEditorView view = new JuiceEditorView();
            view.Model = new JuiceEditorModel {
                Version = "test", HasCharacter = true, Characters = new[] { "Character" }, CharacterIndex = 0,
                CanSave = true, Status = "Ready", Integration = "Optional effects available", NameError = string.Empty,
                AllLocked = false, CharacterPresetIndex = 0, GroupPresetIndex = 0,
                SelectCharacter = delegate(int index) { view.Model.CharacterIndex = index; }
            };
            return view;
        }

        private static AccessoryEditorView Accessory() {
            ScrollPosition sync = new ScrollPosition();
            AccessoryEditorView view = new AccessoryEditorView();
            view.Model = new AccessoryEditorModel {
                Version = "test", Available = true, Status = "Ready", Summary = "20 slots", Mapping = "Select source and target",
                ImmediateSync = false, SelectionHighlight = true, ClearAfter = false, ReverseParent = false, SyncScroll = true,
                Sources = new[] { "Coordinate A", "Coordinate B", "Card" }, Destinations = new[] { "Coordinate A", "Coordinate B" },
                SourceIndex = 0, DestinationIndex = 0,
                ChangeSource = delegate(int index) { view.Model.SourceIndex = index; },
                ChangeDestination = delegate(int index) { view.Model.DestinationIndex = index; },
                ChangePage = delegate(int page) { view.Model.Page = page; },
                Batch = new AccessoryListModel { Role = AccessoryListRole.Batch, Count = 20, Clothing = false, CanToggleKind = false },
                Source = new AccessoryListModel { Role = AccessoryListRole.Source, Count = 20, Scroll = sync, Clothing = false, CanToggleKind = true },
                Destination = new AccessoryListModel { Role = AccessoryListRole.Destination, Count = 20, Scroll = sync, Clothing = false, CanToggleKind = false }
            };
            return view;
        }

        private static void SelectJuicePage(JuiceEditorView view, int page) {
            Find<Selectable>(view.Build(null), "juice-page-" + page).OnChange(true);
        }

        private static T Find<T>(Widget root, string key) where T : Widget {
            if (root is T && object.Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) {
                T match = FindOrNull<T>(child, key);
                if (match != null) return match;
            }
            throw new InvalidOperationException("Missing " + typeof(T).Name + " with key " + key);
        }

        private static T FindOrNull<T>(Widget root, string key) where T : Widget {
            if (root is T && object.Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) { T match = FindOrNull<T>(child, key); if (match != null) return match; }
            return null;
        }

        private static T FindAny<T>(Widget root) where T : Widget {
            if (root is T) return (T)root;
            foreach (Widget child in root.Children) { T match = FindAny<T>(child); if (match != null) return match; }
            return null;
        }

        private static int Count<T>(Widget root) where T : Widget {
            int count = root is T ? 1 : 0;
            foreach (Widget child in root.Children) count += Count<T>(child);
            return count;
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
