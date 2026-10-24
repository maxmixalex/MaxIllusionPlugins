using System;
using System.Collections.Generic;
using MaximumUI;
using UnityEngine;

namespace JuiceControl {
    public partial class JuiceControl {
        private const string EditorMountId = GUID + ".editor";
        private JuiceEditorView editorView;
        private MountOptions editorOptions;
        private ChaControl editorCharacter;
        private float nextEditorPoll;
        private readonly JuiceEditorSession editorSession = new JuiceEditorSession();

        private void ReleaseMaximumUi() {
            showWindow = false;
            editorSession.Close(CloseNameDialog);
            MaximumUIRuntime.Unmount(EditorMountId);
            if (editorView != null) editorView.Model = new JuiceEditorModel();
            editorView = null;
            editorOptions = null;
            editorCharacter = null;
            selectedCharacter = null;
            cachedCharacters = null;
            characterScanFrame = -1;
            nextEditorPoll = 0f;
        }

        private void EnsureMaximumUi() {
            if (editorView != null) return;
            MaximumUIRuntime.EnsureHost();
            editorView = new JuiceEditorView();
            editorOptions = new MountOptions {
                Visible = showWindow && Enabled.Value, BuildEveryFrame = false,
                SoftMouseCapture = true, SoftKeyboardCapture = true, SortOrder = 120
            };
            MaximumUIRuntime.Mount(EditorMountId, editorView, editorOptions);
            RefreshEditor();
            MaximumUIRuntime.Focus(EditorMountId);
        }

        private void UpdateMaximumUi() {
            if (editorOptions == null) return;
            bool wasVisible = editorOptions.Visible;
            MaximumUIRuntime.SetVisible(EditorMountId, showWindow && Enabled.Value);
            if (wasVisible && !editorOptions.Visible) editorSession.Close(CloseNameDialog);
            if (!editorOptions.Visible) return;
            if (!wasVisible) {
                nextEditorPoll = 0f;
                MaximumUIRuntime.Focus(EditorMountId);
                RefreshEditor();
            }
            if (Time.unscaledTime < nextEditorPoll) return;
            nextEditorPoll = Time.unscaledTime + 0.2f;
            RefreshIntegrations(false);
            List<CharacterEntry> characters = GetCharacters();
            EnsureSelectedCharacter(characters);
            if (EditorStateChanged(characters)) RefreshEditor(characters);
        }

        private bool EditorStateChanged(List<CharacterEntry> characters) {
            JuiceEditorModel m = editorView.Model;
            if (editorCharacter != selectedCharacter || m.Characters.Length != characters.Count || editorSession.CharacterCount != characters.Count ||
                m.Status != statusText || m.Error != presetLoadError || m.CanSave != presetStore.CanSave ||
                m.CharacterPresetIndex != selectedCharacterPresetIndex || m.GroupPresetIndex != selectedGroupPresetIndex ||
                m.CharacterPresets.Length != presetXml.CharacterPresets.Count || m.GroupPresets.Length != presetXml.GroupPresets.Count ||
                m.Integration != SkinEffectsBridge.StatusLabel || m.NameDialogVisible != showNameDialog || m.NameError != nameDialogMessage)
                return true;
            for (int i = 0; i < characters.Count; i++)
                if (m.Characters[i] != characters[i].DisplayName || !editorSession.MatchesCharacterAt(i, characters[i].Character)) return true;
            if (selectedCharacter == null) return m.Effects.Count != 0;
            int expected = SiruParts.Length + 2 + (SkinEffectsBridge.IsAvailable ? SkinEffectsBridge.Effects.Count : 0);
            if (m.Effects.Count != expected) return true;
            for (int i = 0; i < m.Effects.Count; i++) {
                JuiceEffectRow row = m.Effects[i];
                if (row.Value != CurrentEffectValue(i) || row.Locked != IsEffectLocked(selectedCharacter, row.Id)) return true;
            }
            return false;
        }

        private int CurrentEffectValue(int i) {
            if (i < SiruParts.Length) return selectedCharacter.GetSiruFlags(SiruParts[i].Part);
            if (i == SiruParts.Length) return selectedCharacter.tearsLv;
            if (i == SiruParts.Length + 1) return GetBlushLevel(selectedCharacter);
            return SkinEffectsBridge.GetLevel(selectedCharacter, SkinEffectsBridge.Effects[i - SiruParts.Length - 2]);
        }

        private void RefreshEditor() { RefreshEditor(GetCharacters()); }

        private void RefreshEditor(List<CharacterEntry> characters) {
            if (editorView == null) return;
            EnsureSelectedCharacter(characters);
            editorCharacter = selectedCharacter;
            JuiceEditorModel m = new JuiceEditorModel {
                Version = Version, CharacterIndex = GetSelectedCharacterIndex(characters), HasCharacter = selectedCharacter != null,
                CharacterPresetIndex = selectedCharacterPresetIndex, GroupPresetIndex = selectedGroupPresetIndex,
                CanSave = presetStore.CanSave, Error = presetLoadError, Status = statusText, Integration = SkinEffectsBridge.StatusLabel,
                NameDialogVisible = showNameDialog, Name = nameDialogText, NamePrompt = GetNameDialogPrompt(), NameError = nameDialogMessage,
                Characters = new string[characters.Count], CharacterPresets = new string[presetXml.CharacterPresets.Count],
                GroupPresets = new string[presetXml.GroupPresets.Count],
                Execute = ExecuteEditorCommand,
                SelectCharacter = delegate(int i) { SelectCharacter(characters, i); RefreshEditor(); },
                SelectCharacterPreset = delegate(int i) { SelectCharacterPreset(i); RefreshEditor(); },
                SelectGroupPreset = delegate(int i) { SelectGroupPreset(i); RefreshEditor(); },
                ChooseCharacterPreset = delegate(int i) {
                    if (i < 0 || i >= presetXml.CharacterPresets.Count) return;
                    SelectCharacterPreset(i);
                    statusText = "已选择汁液预设: " + characterPresetName;
                    RefreshEditor();
                },
                ChooseGroupPreset = delegate(int i) {
                    if (i < 0 || i >= presetXml.GroupPresets.Count) return;
                    SelectGroupPreset(i);
                    statusText = "已选择场景预设: " + groupPresetName;
                    RefreshEditor();
                },
                ChangeName = delegate(string text) {
                    nameDialogText = text; editorView.Model.Name = text;
                    MaximumUIRuntime.MarkDirty(editorView);
                },
                LockAll = delegate(bool locked) {
                    if (selectedCharacter == null) return;
                    if (locked) CaptureAllEffectLocks(selectedCharacter); else ClearLockedEffects(selectedCharacter);
                    statusText = locked ? "已锁定全部汁液。" : "已解除全部汁液锁定。";
                    RefreshEditor();
                }
            };
            object[] identities = new object[characters.Count];
            for (int i = 0; i < characters.Count; i++) {
                m.Characters[i] = characters[i].DisplayName;
                identities[i] = characters[i].Character;
            }
            for (int i = 0; i < m.CharacterPresets.Length; i++) m.CharacterPresets[i] = presetXml.CharacterPresets[i].Name;
            for (int i = 0; i < m.GroupPresets.Length; i++) m.GroupPresets[i] = presetXml.GroupPresets[i].Name;
            ChaControl character = selectedCharacter;
            if (character != null) {
                for (int i = 0; i < SiruParts.Length; i++) {
                    SiruPartInfo part = SiruParts[i];
                    AddEditorEffect(m, character, "siru:" + part.Part, part.DisplayName,
                        character.GetSiruFlags(part.Part), SiruMaxLevel,
                        delegate(int level) { SetSiruPart(character, part.Part, (byte)level); });
                }
                AddEditorEffect(m, character, "tear", "泪水", character.tearsLv, TearMaxLevel,
                    delegate(int level) { SetTearLevel(character, (byte)level); });
                AddEditorEffect(m, character, "blush", "脸红", GetBlushLevel(character), BlushMaxLevel,
                    delegate(int level) { SetBlushLevel(character, level); });
                if (SkinEffectsBridge.IsAvailable) {
                    for (int i = 0; i < SkinEffectsBridge.Effects.Count; i++) {
                        SkinEffectInfo effect = SkinEffectsBridge.Effects[i];
                        AddEditorEffect(m, character, SkinEffectsBridge.GetEffectId(effect),
                            SkinEffectsBridge.DisplayPrefix + " " + effect.DisplayName,
                            SkinEffectsBridge.GetLevel(character, effect), effect.MaxLevel,
                            delegate(int level) { SkinEffectsBridge.SetLevel(character, effect, level); });
                    }
                }
                m.AllLocked = AreAllEffectsLocked(character);
            }
            PresetXml library = presetXml;
            string path = presetPath;
            object characterPreset = GetCurrentCharacterPreset();
            object groupPreset = GetCurrentGroupPreset();
            editorSession.Bind(m, identities, new object[] { library, character, characterPreset, groupPreset }, path,
                delegate { return isActiveAndEnabled && Enabled != null && Enabled.Value && showWindow && editorOptions != null && editorOptions.Visible; },
                delegate {
                    if (!ReferenceEquals(presetXml, library) || presetPath != path ||
                        !ReferenceEquals(selectedCharacter, character) ||
                        !ReferenceEquals(GetCurrentCharacterPreset(), characterPreset) ||
                        !ReferenceEquals(GetCurrentGroupPreset(), groupPreset)) return false;
                    List<CharacterEntry> current = GetCharacters();
                    if (editorSession.CharacterCount != current.Count) return false;
                    for (int i = 0; i < current.Count; i++)
                        if (!editorSession.MatchesCharacterAt(i, current[i].Character)) return false;
                    return true;
                });
            editorView.Model = m;
            MaximumUIRuntime.MarkDirty(editorView);
        }

        private void AddEditorEffect(JuiceEditorModel model, ChaControl character, string id, string name, int value, int maximum, Action<int> change) {
            model.Effects.Add(new JuiceEffectRow {
                Id = id, Name = name, Value = value, Maximum = maximum, Locked = IsEffectLocked(character, id),
                DisplayValue = id == "blush" ? FormatBlushLevel(value) : value.ToString(),
                Change = delegate(int next) {
                    if (character == null || character != selectedCharacter) return;
                    next = Mathf.Clamp(next, 0, maximum);
                    change(next);
                    UpdateLockedEffectValue(character, id, next);
                    RefreshEditor();
                },
                Lock = delegate(bool locked) {
                    if (character == null || character != selectedCharacter) return;
                    SetEffectLocked(character, id, locked, value);
                    RefreshEditor();
                }
            });
        }

        private void ExecuteEditorCommand(JuiceEditorCommand command) {
            switch (command) {
                case JuiceEditorCommand.Close:
                    showWindow = false;
                    editorSession.Close(CloseNameDialog);
                    MaximumUIRuntime.SetVisible(EditorMountId, false);
                    return;
                case JuiceEditorCommand.Refresh:
                    if (selectedCharacter != null) RefreshCurrentState(selectedCharacter);
                    statusText = "已获取当前汁液状态。"; break;
                case JuiceEditorCommand.Clear:
                    if (selectedCharacter != null) { ApplyState(selectedCharacter, new CharaSkinState()); ClearLockedEffects(selectedCharacter); }
                    statusText = "已清空汁液。"; break;
                case JuiceEditorCommand.Fill:
                    if (selectedCharacter != null) {
                        ApplyState(selectedCharacter, CharaSkinState.CreateMax(SiruMaxLevel, TearMaxLevel, BlushMaxLevel, SkinEffectsBridge.Effects));
                        RefreshCurrentState(selectedCharacter);
                    }
                    statusText = "已设置为全满汁液。"; break;
                case JuiceEditorCommand.ApplyCharacter: ApplyCurrentCharacterPresetToSelected(); break;
                case JuiceEditorCommand.ApplyGroup: ApplyCurrentGroupPreset(GetCharacters()); break;
                case JuiceEditorCommand.CreateCharacter: OpenNameDialog(NameDialogMode.CreateCharacter, string.Empty); break;
                case JuiceEditorCommand.CreateGroup: OpenNameDialog(NameDialogMode.CreateGroup, string.Empty); break;
                case JuiceEditorCommand.OverwriteCharacter: OpenNameDialog(NameDialogMode.OverwriteCharacter, GetCurrentCharacterPresetName()); break;
                case JuiceEditorCommand.OverwriteGroup: OpenNameDialog(NameDialogMode.OverwriteGroup, GetCurrentGroupPresetName()); break;
                case JuiceEditorCommand.DeleteCharacter: DeleteCurrentCharacterPreset(); break;
                case JuiceEditorCommand.DeleteGroup: DeleteCurrentGroupPreset(); break;
                case JuiceEditorCommand.OpenXml: OpenPresetXml(); break;
                case JuiceEditorCommand.ReloadXml:
                    if (LoadPresets()) statusText = "已重载预设 XML。";
                    RefreshIntegrations(true); break;
                case JuiceEditorCommand.ConfirmName: ConfirmNameDialog(); break;
                case JuiceEditorCommand.CancelName: CloseNameDialog(); break;
            }
            RefreshEditor();
        }
    }
}
