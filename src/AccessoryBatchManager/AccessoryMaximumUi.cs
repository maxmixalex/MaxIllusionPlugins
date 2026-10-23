using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Maker;
using MaximumUI;
using UnityEngine;

namespace AccessoryBatchManager {
    public sealed partial class AccessoryBatchManagerPlugin {
        private const string EditorMountId = GUID + ".editor";
        private static readonly string[] ClothingSlotNames = { "上衣", "下衣", "胸罩", "内裤", "手套", "连裤袜", "袜子", "鞋子(室内)", "鞋子(室外)" };
        private AccessoryEditorView editorView;
        private MountOptions editorOptions;
        private ChaControl editorCharacter;
        private int editorCoordinate = -1;
        private string editorCardName;
        private float nextEditorPoll;
        private readonly AccessoryEditorSession editorSession = new AccessoryEditorSession();
        private readonly ScrollPosition batchPosition = new ScrollPosition();
        private readonly ScrollPosition sourcePosition = new ScrollPosition();
        private readonly ScrollPosition destinationPosition = new ScrollPosition();
        private readonly SlotSnapshot batchSnapshot = new SlotSnapshot();
        private readonly SlotSnapshot sourceSnapshot = new SlotSnapshot();
        private readonly SlotSnapshot destinationSnapshot = new SlotSnapshot();

        private void UnmountMaximumUi() {
            editorSession.Clear();
            confirmedAction = null;
            confirmationText = string.Empty;
            if (editorView != null) editorView.Model = new AccessoryEditorModel();
            MaximumUIRuntime.Unmount(EditorMountId);
            editorView = null;
            editorOptions = null;
            editorCharacter = null;
            editorCoordinate = -1;
            editorCardName = null;
            nextEditorPoll = 0f;
            coordinateCardSource.Reset();
            batchSnapshot.Clear();
            sourceSnapshot.Clear();
            destinationSnapshot.Clear();
        }

        private void EnsureMaximumUi() {
            if (editorView != null) return;
            MaximumUIRuntime.EnsureHost();
            editorView = new AccessoryEditorView();
            editorOptions = new MountOptions {
                Visible = visible && MakerAPI.InsideMaker, BuildEveryFrame = false,
                SoftMouseCapture = true, SoftKeyboardCapture = true, SortOrder = 130
            };
            MaximumUIRuntime.Mount(EditorMountId, editorView, editorOptions);
            RefreshEditor();
            MaximumUIRuntime.Focus(EditorMountId);
        }

        private void UpdateMaximumUi() {
            if (editorOptions == null) return;
            bool wasVisible = editorOptions.Visible;
            MaximumUIRuntime.SetVisible(EditorMountId, visible && MakerAPI.InsideMaker);
            if (wasVisible && !editorOptions.Visible) {
                confirmedAction = null;
                editorSession.Clear();
            }
            if (!editorOptions.Visible) return;
            if (!wasVisible) {
                nextEditorPoll = 0f;
                MaximumUIRuntime.Focus(EditorMountId);
                RefreshEditor();
            }
            if (Time.unscaledTime < nextEditorPoll) return;
            nextEditorPoll = Time.unscaledTime + 0.25f;
            ChaControl character = AccessoryOperations.GetCharacter();
            if (character == null || character.nowCoordinate == null) {
                if (editorView.Model.Available) RefreshEditor();
                return;
            }
            bool changed = character != editorCharacter || character.fileStatus.coordinateType != editorCoordinate;
            changed |= batchSnapshot.Changed(AccessoryOperations.CurrentParts(character), null);
            if (tab == 1) {
                RefreshCoordinateSource(character);
                ChaFileCoordinate source = SourceCoordinate(character);
                ChaFileCoordinate destination = AccessoryOperations.GetCoordinate(character, destinationCoordinate);
                changed |= sourceSnapshot.Changed(source.accessory.parts, source.clothes.parts);
                changed |= destinationSnapshot.Changed(destination.accessory.parts, destination.clothes.parts);
                changed |= editorCardName != coordinateCardSource.DisplayName;
            }
            AccessoryEditorModel m = editorView.Model;
            changed |= m.Status != status || m.Confirming != (confirmedAction != null) ||
                m.FollowCoordinate != syncDestinationToCurrentCoordinate.Value || m.ImmediateSync != alwaysImmediateSync.Value ||
                m.SelectionHighlight != selectionHighlightEnabled.Value || m.ClearAfter != clearSelectionAfterOperation.Value;
            changed |= editorSession.Target != null && !editorSession.Target.Matches(CaptureEditorTarget());
            if (changed) RefreshEditor();
            else UpdateHighlights(character);
        }

        private void RefreshCoordinateSource(ChaControl character) {
            if (syncDestinationToCurrentCoordinate.Value)
                destinationCoordinate = Mathf.Clamp(character.fileStatus.coordinateType, 0, CoordinateNames.Length - 1);
            if (sourceIsCoordinateCard && !coordinateCardSource.TryRefresh()) {
                sourceIsCoordinateCard = false;
                withinSources.Clear();
                sourceSelectionAnchor = -1;
                ClearLastSelected();
                status = "服装卡来源已失效，已切回角色搭配。";
            } else if (!sourceIsCoordinateCard) coordinateCardSource.TryRefresh();
        }

        private ChaFileCoordinate SourceCoordinate(ChaControl character) {
            return sourceIsCoordinateCard ? coordinateCardSource.Coordinate : AccessoryOperations.GetCoordinate(character, sourceCoordinate);
        }

        private void RefreshEditor() {
            if (editorView == null) return;
            ChaControl character = AccessoryOperations.GetCharacter();
            bool available = character != null && character.nowCoordinate != null;
            if (editorCharacter != character) {
                ClearAllSelections();
                confirmedAction = null;
            }
            editorCharacter = character;
            editorCoordinate = available ? character.fileStatus.coordinateType : -1;
            AccessoryEditorModel m = new AccessoryEditorModel {
                Version = Version, Available = available, Page = tab, Status = status,
                FollowCoordinate = syncDestinationToCurrentCoordinate.Value, ImmediateSync = alwaysImmediateSync.Value,
                SelectionHighlight = selectionHighlightEnabled.Value, ClearAfter = clearSelectionAfterOperation.Value,
                ReverseParent = reverseParent, SyncScroll = syncScroll,
                Confirming = confirmedAction != null, Confirmation = confirmationText
            };
            if (available) {
                ChaFileAccessory.PartsInfo[] parts = AccessoryOperations.CurrentParts(character);
                m.Batch = BuildListModel(character, AccessoryListRole.Batch, parts, null, false, selected, batchPosition);
                m.Summary = "饰品 " + AccessoryOperations.FilledCount(parts) + " / 槽位 " + m.Batch.Count;
                batchSnapshot.Changed(parts, null);
                if (tab == 1) {
                    RefreshCoordinateSource(character);
                    ChaFileCoordinate source = SourceCoordinate(character);
                    ChaFileCoordinate destination = AccessoryOperations.GetCoordinate(character, destinationCoordinate);
                    m.Source = BuildListModel(character, AccessoryListRole.Source, source.accessory.parts, source.clothes.parts,
                        sourceListIsClothing, withinSources, sourcePosition);
                    m.Source.CanToggleKind = true;
                    m.Destination = BuildListModel(character, AccessoryListRole.Destination, destination.accessory.parts, destination.clothes.parts,
                        destinationListIsClothing, withinDestinations, syncScroll ? sourcePosition : destinationPosition);
                    m.Destination.CanToggleKind = sourceListIsClothing;
                    m.Sources = SourceNames(); m.Destinations = CoordinateNames;
                    m.SourceIndex = sourceIsCoordinateCard ? CoordinateNames.Length : sourceCoordinate;
                    m.DestinationIndex = destinationCoordinate;
                    m.CanCopy = HasValidMapping(false);
                    m.CanMove = !sourceIsCoordinateCard && HasValidMapping(true);
                    m.CanInsert = !destinationListIsClothing && withinSources.Count > 0;
                    m.CanAdjust = !sourceListIsClothing && !destinationListIsClothing && m.CanCopy;
                    m.Mapping = MappingHint(false);
                    editorCardName = coordinateCardSource.DisplayName;
                    sourceSnapshot.Changed(source.accessory.parts, source.clothes.parts);
                    destinationSnapshot.Changed(destination.accessory.parts, destination.clothes.parts);
                }
                // Source invalidation can produce a status during this refresh.
                m.Status = status;
            }
            if (editorSession.Refresh(CaptureEditorTarget()) && confirmedAction != null) {
                confirmedAction = null;
                m.Status = status = "角色、来源、目标或选区已变化，已取消待确认操作。";
            }
            AccessoryEditorTarget target = editorSession.Target;
            int revision = editorSession.ModelRevision;
            m.Execute = command => InvokeEditor(revision, target, delegate { ExecuteEditorCommand(command); });
            m.ChangeSetting = (setting, value) => InvokeEditor(revision, target, delegate { ChangeEditorSetting(setting, value); });
            m.SelectSlot = (role, index, request) => InvokeEditor(revision, target, delegate { SelectEditorSlot(role, index, request); });
            m.SelectionCommand = (role, command) => InvokeEditor(revision, target, delegate { SelectEditorCommand(role, command); });
            m.ChangePage = index => InvokeEditor(revision, target, delegate { tab = index; RefreshEditor(); });
            m.ChangeSource = index => InvokeEditor(revision, target, delegate { SetSourceIndex(index); RefreshEditor(); });
            m.ChangeDestination = index => InvokeEditor(revision, target, delegate {
                destinationCoordinate = Mathf.Clamp(index, 0, CoordinateNames.Length - 1);
                // Preserve the chosen slot numbers when choosing a destination.
                RefreshEditor();
            });
            m.Confirming = confirmedAction != null;
            editorView.Model = m;
            MaximumUIRuntime.MarkDirty(editorView);
            UpdateHighlights(character);
        }

        private AccessoryEditorTarget CaptureEditorTarget() {
            if (!enabled || !visible || !MakerAPI.InsideMaker) return null;
            ChaControl character = AccessoryOperations.GetCharacter();
            if (character == null || character.nowCoordinate == null || character.fileStatus == null) return null;
            bool copy = tab == 1;
            if (copy) RefreshCoordinateSource(character);
            return new AccessoryEditorTarget(character, tab, copy ? sourceCoordinate : -1, copy ? destinationCoordinate : -1,
                copy && sourceIsCoordinateCard, copy && sourceListIsClothing, copy && destinationListIsClothing, copy && reverseParent,
                copy ? SourceCoordinate(character) : null,
                copy ? AccessoryOperations.GetCoordinate(character, destinationCoordinate) : null,
                tab == 0 ? selected : null, copy ? withinSources : null, copy ? withinDestinations : null,
                (alwaysImmediateSync.Value ? 1 : 0) | (clearSelectionAfterOperation.Value ? 2 : 0) |
                (syncDestinationToCurrentCoordinate.Value ? 4 : 0));
        }

        private void InvokeEditor(int revision, AccessoryEditorTarget target, Action action) {
            if (editorView == null || revision != editorSession.ModelRevision) return;
            if (editorSession.TryInvoke(revision, target, CaptureEditorTarget(), action)) return;
            confirmedAction = null;
            status = "角色、来源或目标已变化，请重新选择操作。";
            RefreshEditor();
        }

        private AccessoryListModel BuildListModel(ChaControl character, AccessoryListRole role,
            ChaFileAccessory.PartsInfo[] parts, ChaFileClothes.PartsInfo[] clothes, bool clothing,
            HashSet<int> selection, ScrollPosition scroll) {
            int count = clothing ? Math.Min(AccessoryOperations.ClothingSlotCount, clothes == null ? 0 : clothes.Length)
                : AccessoryOperations.EffectiveSlotCount(parts);
            PruneSelection(selection, count);
            return new AccessoryListModel {
                Role = role, Count = count, Clothing = clothing, Selection = selection, Scroll = scroll,
                GetName = delegate(int i) {
                    if (i < 0 || i >= count || character == null) return string.Empty;
                    return clothing ? ClothingSlotNames[i] + " · " + AccessoryOperations.GetClothingName(character, clothes[i], i)
                        : (i + 1) + "  " + AccessoryOperations.GetName(character, parts[i]);
                }
            };
        }

        private bool EditorCharacterIsCurrent() {
            ChaControl current = AccessoryOperations.GetCharacter();
            if (current != null && current == editorCharacter && current.fileStatus.coordinateType == editorCoordinate) return true;
            status = "角色或当前搭配已经改变，请重新选择操作。";
            RefreshEditor();
            return false;
        }

        private AccessoryListModel ListFor(AccessoryListRole role) {
            return role == AccessoryListRole.Batch ? editorView.Model.Batch : role == AccessoryListRole.Source ? editorView.Model.Source : editorView.Model.Destination;
        }

        private void SelectEditorSlot(AccessoryListRole role, int index, SelectionRequest request) {
            if (!EditorCharacterIsCurrent() || request == null) return;
            AccessoryListModel list = ListFor(role);
            int anchor = role == AccessoryListRole.Batch ? batchSelectionAnchor : role == AccessoryListRole.Source ? sourceSelectionAnchor : destinationSelectionAnchor;
            anchor = AccessorySelectionEditing.Apply(list.Selection, list.Count, anchor, index, request.Selected, request.Range);
            SetEditorAnchor(role, anchor);
            int coordinate = list.Clothing ? -2 : role == AccessoryListRole.Batch ? editorCoordinate
                : role == AccessoryListRole.Source ? (sourceIsCoordinateCard ? -1 : sourceCoordinate) : destinationCoordinate;
            if (request.Selected) SetLastSelected(index, coordinate);
            else if (lastSelectedSlot == index && lastSelectedCoordinate == coordinate) ClearLastSelected();
            RefreshEditor();
        }

        private void SetEditorAnchor(AccessoryListRole role, int value) {
            if (role == AccessoryListRole.Batch) batchSelectionAnchor = value;
            else if (role == AccessoryListRole.Source) sourceSelectionAnchor = value;
            else destinationSelectionAnchor = value;
        }

        private void SelectEditorCommand(AccessoryListRole role, AccessorySelectionCommand command) {
            if (!EditorCharacterIsCurrent()) return;
            AccessoryListModel list = ListFor(role);
            int anchor = -1;
            if (command == AccessorySelectionCommand.All) {
                SelectAll(list.Selection, list.Count); anchor = list.Count - 1;
            } else if (command == AccessorySelectionCommand.None) list.Selection.Clear();
            else if (command == AccessorySelectionCommand.ToggleKind) {
                if (!list.CanToggleKind) return;
                if (role == AccessoryListRole.Source) {
                    sourceListIsClothing = !sourceListIsClothing;
                    if (!sourceListIsClothing && destinationListIsClothing) {
                        destinationListIsClothing = false;
                        withinDestinations.Clear(); destinationSelectionAnchor = -1;
                    }
                } else destinationListIsClothing = !destinationListIsClothing;
                list.Selection.Clear();
            } else {
                ChaFileCoordinate coordinate = role == AccessoryListRole.Batch ? editorCharacter.nowCoordinate
                    : role == AccessoryListRole.Source ? SourceCoordinate(editorCharacter) : AccessoryOperations.GetCoordinate(editorCharacter, destinationCoordinate);
                anchor = list.Clothing ? SelectNonEmptyClothing(list.Selection, coordinate.clothes.parts)
                    : SelectNonEmpty(list.Selection, coordinate.accessory.parts, list.Count);
            }
            SetEditorAnchor(role, anchor);
            if (anchor >= 0) {
                int coordinate = list.Clothing ? -2 : role == AccessoryListRole.Batch ? editorCoordinate
                    : role == AccessoryListRole.Source ? (sourceIsCoordinateCard ? -1 : sourceCoordinate) : destinationCoordinate;
                SetLastSelected(anchor, coordinate);
            } else ClearLastSelected();
            RefreshEditor();
        }

        private void ChangeEditorSetting(AccessoryEditorSetting setting, bool value) {
            switch (setting) {
                case AccessoryEditorSetting.ImmediateSync: alwaysImmediateSync.Value = value; break;
                case AccessoryEditorSetting.SelectionHighlight: selectionHighlightEnabled.Value = value; break;
                case AccessoryEditorSetting.ClearAfter: clearSelectionAfterOperation.Value = value; break;
                case AccessoryEditorSetting.FollowCoordinate: syncDestinationToCurrentCoordinate.Value = value; break;
                case AccessoryEditorSetting.ReverseParent: reverseParent = value; break;
                case AccessoryEditorSetting.SyncScroll:
                    if (!value) destinationPosition.Position = sourcePosition.Position;
                    syncScroll = value; break;
            }
            Config.Save();
            RefreshEditor();
        }

        private void ExecuteEditorCommand(AccessoryEditorCommand command) {
            if (command == AccessoryEditorCommand.Close) {
                visible = false; confirmedAction = null;
                editorSession.Clear();
                MaximumUIRuntime.SetVisible(EditorMountId, false);
                accessoryHighlighter.Clear();
                return;
            }
            if (command == AccessoryEditorCommand.Cancel) { confirmedAction = null; RefreshEditor(); return; }
            if (!EditorCharacterIsCurrent()) return;
            ChaControl character = editorCharacter;
            switch (command) {
                case AccessoryEditorCommand.Confirm:
                    Action action = confirmedAction; confirmedAction = null;
                    if (action != null) action(); break;
                case AccessoryEditorCommand.Remove:
                    int[] slots = selected.ToArray();
                    RequestConfirmation("确定移除选中的 " + slots.Length + " 个槽位内容吗？", delegate {
                        int count = AccessoryOperations.RemoveSelected(character, slots);
                        selected.Clear(); ClearLastSelected();
                        CompleteDataOperation(character, "已移除 " + count + " 个饰品。");
                    }); break;
                case AccessoryEditorCommand.Compact:
                    RequestConfirmation("确定整理当前搭配吗？所有非空饰品会依次前移。", delegate {
                        int count = AccessoryOperations.Compact(character);
                        selected.Clear(); ClearLastSelected();
                        CompleteDataOperation(character, count == 0 ? "没有需要整理的空隙。" : "已整理，前移 " + count + " 个饰品。");
                    }); break;
                case AccessoryEditorCommand.Copy: if (HasValidMapping(false)) ConfirmSlotOperation(character, false); break;
                case AccessoryEditorCommand.Move: if (!sourceIsCoordinateCard && HasValidMapping(true)) ConfirmSlotOperation(character, true); break;
                case AccessoryEditorCommand.InsertFirst: if (withinSources.Count > 0) ConfirmInsertOperation(character, true); break;
                case AccessoryEditorCommand.InsertLast: if (withinSources.Count > 0) ConfirmInsertOperation(character, false); break;
                case AccessoryEditorCommand.Adjust1: ConfirmAdjustment(character, 0, MirrorMode.None, "调整①复制"); break;
                case AccessoryEditorCommand.Adjust1LeftRight: ConfirmAdjustment(character, 0, MirrorMode.LeftRight, "调整①左右反转复制"); break;
                case AccessoryEditorCommand.Adjust1UpDown: ConfirmAdjustment(character, 0, MirrorMode.UpDown, "调整①上下反转复制"); break;
                case AccessoryEditorCommand.Adjust2: ConfirmAdjustment(character, 1, MirrorMode.None, "调整②复制"); break;
                case AccessoryEditorCommand.Adjust2LeftRight: ConfirmAdjustment(character, 1, MirrorMode.LeftRight, "调整②左右反转复制"); break;
                case AccessoryEditorCommand.Adjust2UpDown: ConfirmAdjustment(character, 1, MirrorMode.UpDown, "调整②上下反转复制"); break;
                case AccessoryEditorCommand.RefreshModel:
                    RequestConfirmation("确定立即同步饰品数据并重建当前角色模型吗？", delegate {
                        accessoryHighlighter.Clear(); AccessoryOperations.RefreshCharacter(character);
                        status = "数据同步完成，角色模型已更新。";
                    }); break;
            }
            RefreshEditor();
        }

        // Compare only cheap identity fields at 4 Hz while open. Localized labels
        // are resolved only for visible virtual rows, never for the entire list.
        private sealed class SlotSnapshot {
            private ChaFileAccessory.PartsInfo[] sourceParts;
            private ChaFileClothes.PartsInfo[] sourceClothes;
            private int[] types = new int[0];
            private int[] ids = new int[0];
            private int[] clothingIds = new int[0];
            internal bool Changed(ChaFileAccessory.PartsInfo[] parts, ChaFileClothes.PartsInfo[] clothes) {
                int count = parts == null ? 0 : parts.Length;
                int clothingCount = clothes == null ? 0 : clothes.Length;
                bool changed = !ReferenceEquals(sourceParts, parts) || !ReferenceEquals(sourceClothes, clothes) ||
                    ids.Length != count || clothingIds.Length != clothingCount;
                sourceParts = parts;
                sourceClothes = clothes;
                if (ids.Length != count) { ids = new int[count]; types = new int[count]; }
                if (clothingIds.Length != clothingCount) clothingIds = new int[clothingCount];
                for (int i = 0; i < count; i++) {
                    int type = parts[i] == null ? -1 : parts[i].type;
                    int id = parts[i] == null ? -1 : parts[i].id;
                    changed |= types[i] != type || ids[i] != id;
                    types[i] = type; ids[i] = id;
                }
                for (int i = 0; i < clothingCount; i++) {
                    int id = clothes[i] == null ? -1 : clothes[i].id;
                    changed |= clothingIds[i] != id; clothingIds[i] = id;
                }
                return changed;
            }

            internal void Clear() {
                sourceParts = null;
                sourceClothes = null;
                types = ids = clothingIds = new int[0];
            }
        }
    }
}
