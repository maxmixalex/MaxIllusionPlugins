using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using KKAPI.Maker;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[assembly: System.Reflection.AssemblyFileVersion(AccessoryBatchManager.AccessoryBatchManagerPlugin.Version)]

namespace AccessoryBatchManager {
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    [BepInDependency("com.joan6694.illusionplugins.moreaccessories")]
    [BepInDependency("ClothesToAccessories", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SliderHighlight", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
    [BepInPlugin(GUID, "Accessory Batch Manager", Version)]
    public sealed partial class AccessoryBatchManagerPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.AccessoryBatchManager";
        public const string Version = "0.7.0";

        private static readonly string[] CoordinateNames = { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣" };

        private ConfigEntry<KeyboardShortcut> toggleShortcut;
        private ConfigEntry<bool> alwaysImmediateSync;
        private ConfigEntry<bool> syncDestinationToCurrentCoordinate;
        private ConfigEntry<bool> selectionHighlightEnabled;
        private ConfigEntry<bool> clearSelectionAfterOperation;
        private bool visible;
        private int tab;
        private int sourceCoordinate;
        private int destinationCoordinate = 1;
        private int sourceSelectionAnchor = -1;
        private int destinationSelectionAnchor = -1;
        private int batchSelectionAnchor = -1;
        private bool reverseParent;
        private bool syncScroll = true;
        private bool sourceIsCoordinateCard;
        private bool sourceListIsClothing;
        private bool destinationListIsClothing;
        private int lastSelectedSlot = -1;
        private int lastSelectedCoordinate = -1;
        private string status = string.Empty;
        private string confirmationText = string.Empty;
        private Action confirmedAction;
        private readonly HashSet<int> selected = new HashSet<int>();
        private readonly HashSet<int> withinSources = new HashSet<int>();
        private readonly HashSet<int> withinDestinations = new HashSet<int>();
        private GameObject nativeFrontButtons;
        private Transform nativeAddButtons;
        private bool installingNativeButtons;
        private Coroutine nativeButtonInstall;
        private readonly CoordinateCardSource coordinateCardSource = new CoordinateCardSource();
        private readonly AccessoryHighlighter accessoryHighlighter = new AccessoryHighlighter();

        private void Awake() {
            toggleShortcut = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.Equals, KeyCode.LeftShift), "角色编辑器中打开窗口的快捷键，默认为 +（Shift+=），小键盘 + 也可使用。");
            alwaysImmediateSync = Config.Bind("General", "Always immediately sync", false, "每次修改饰品数据后立即重建角色模型。关闭可保留批量操作的高性能。");
            syncDestinationToCurrentCoordinate = Config.Bind("General", "Keep copy destination on current coordinate", true,
                "复制/移动页面的目标服装搭配始终跟随角色当前正在编辑的服装搭配。");
            selectionHighlightEnabled = Config.Bind("Highlight", "Selection highlight", true, "多选槽位时高亮对应饰品。");
            clearSelectionAfterOperation = Config.Bind("General", "Clear selection after operation", false, "每次操作完成后清除批量管理及复制/移动页面的选区。");
            if (toggleShortcut.Value.MainKey == KeyCode.Backslash)
                toggleShortcut.Value = new KeyboardShortcut(KeyCode.Equals, KeyCode.LeftShift);
            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MakerAPI.MakerExiting += OnMakerExiting;
        }

        private void OnDestroy() {
            UnmountMaximumUi();
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.MakerExiting -= OnMakerExiting;
            if (nativeButtonInstall != null) StopCoroutine(nativeButtonInstall);
            accessoryHighlighter.Dispose();
            if (nativeFrontButtons != null) Object.Destroy(nativeFrontButtons);
        }

        private void OnDisable() {
            visible = false;
            UnmountMaximumUi();
            accessoryHighlighter.Clear();
        }

        private void OnMakerFinishedLoading(object sender, EventArgs eventArgs) {
            if (installingNativeButtons) return;
            installingNativeButtons = true;
            nativeButtonInstall = StartCoroutine(InstallNativeButtons());
        }

        private void OnMakerExiting(object sender, EventArgs eventArgs) {
            if (nativeButtonInstall != null) StopCoroutine(nativeButtonInstall);
            nativeButtonInstall = null;
            visible = false;
            UnmountMaximumUi();
            ClearAllSelections();
            sourceIsCoordinateCard = false;
            nativeFrontButtons = null;
            nativeAddButtons = null;
            installingNativeButtons = false;
            accessoryHighlighter.Clear();
        }

        private void Update() {
            if (!MakerAPI.InsideMaker) {
                visible = false;
                accessoryHighlighter.Clear();
                if (editorView != null) UnmountMaximumUi();
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool plusPressed = (Input.GetKeyDown(KeyCode.Equals) && shift) || Input.GetKeyDown(KeyCode.KeypadPlus);
            if (!MaximumUI.MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUI.MaximumUIRuntime.IsModalOpen &&
                (toggleShortcut.Value.IsDown() || plusPressed)) {
                visible = !visible;
                if (!visible) accessoryHighlighter.Clear();
            }
            UpdateMaximumUi();
        }

        private void LateUpdate() {
            if (!MakerAPI.InsideMaker || nativeFrontButtons == null || nativeAddButtons == null) return;
            Transform parent = nativeAddButtons.parent;
            if (parent == null || nativeFrontButtons.transform.parent != parent) return;
            int addButtonsIndex = nativeAddButtons.GetSiblingIndex();
            if (nativeFrontButtons.transform.GetSiblingIndex() != addButtonsIndex + 1)
                nativeFrontButtons.transform.SetSiblingIndex(Math.Min(parent.childCount - 1, addButtonsIndex + 1));
        }

        private void OnGUI() {
            if (!visible || !MakerAPI.InsideMaker) return;
            EnsureMaximumUi();
        }

        private string[] SourceNames() {
            if (string.IsNullOrEmpty(coordinateCardSource.DisplayName)) return CoordinateNames;
            return CoordinateNames.Concat(new[] { coordinateCardSource.DisplayName }).ToArray();
        }

        private void SetSourceIndex(int index) {
            bool card = index >= CoordinateNames.Length && !string.IsNullOrEmpty(coordinateCardSource.DisplayName);
            if (sourceIsCoordinateCard != card) {
                withinSources.Clear();
                sourceSelectionAnchor = -1;
                ClearLastSelected();
            }
            sourceIsCoordinateCard = card;
            if (!card) sourceCoordinate = Mathf.Clamp(index, 0, CoordinateNames.Length - 1);
        }

        private static int CycleIndex(int value, int delta, int count) {
            int next = (value + delta) % count;
            return next < 0 ? next + count : next;
        }

        private static int CycleCoordinate(int value, int delta) {
            int next = (value + delta) % CoordinateNames.Length;
            return next < 0 ? next + CoordinateNames.Length : next;
        }

        private void ConfirmSlotOperation(ChaControl character, bool move) {
            int pairCount = PairCount();
            string action = move ? "移动" : "复制";
            string sourceName = sourceIsCoordinateCard ? coordinateCardSource.DisplayName : CoordinateNames[sourceCoordinate];
            ChaFileAccessory.PartsInfo[] cardParts = sourceIsCoordinateCard ? coordinateCardSource.Parts : null;
            ChaFileCoordinate cardCoordinate = sourceIsCoordinateCard ? coordinateCardSource.Coordinate : null;
            List<int> sources = Sorted(withinSources);
            List<int> destinations = Sorted(withinDestinations);
            int capturedSourceCoordinate = sourceCoordinate;
            int capturedDestinationCoordinate = destinationCoordinate;
            bool capturedReverseParent = reverseParent;
            bool capturedSourceClothing = sourceListIsClothing;
            bool capturedDestinationClothing = destinationListIsClothing;
            RequestConfirmation("确定从“" + sourceName + "”向“" + CoordinateNames[destinationCoordinate] + "”" + action + " " + pairCount + " 组槽位吗？", delegate {
                object materialEditorState = MaterialEditorBridge.CaptureState(character);
                int copied;
                if (capturedSourceClothing) {
                    ChaFileClothes.PartsInfo[] clothing = cardCoordinate != null
                        ? cardCoordinate.clothes.parts
                        : AccessoryOperations.GetCoordinate(character, capturedSourceCoordinate).clothes.parts;
                    copied = capturedDestinationClothing
                        ? AccessoryOperations.CopyOrMoveClothing(character, clothing, capturedSourceCoordinate, capturedDestinationCoordinate, sources, destinations, move)
                        : AccessoryOperations.CopyOrMoveClothingToAccessories(character, clothing, capturedSourceCoordinate, capturedDestinationCoordinate, sources, destinations, capturedReverseParent, move);
                    if (copied > 0) {
                        MaterialEditorBridge.RestoreState(materialEditorState);
                        if (cardCoordinate != null)
                            MaterialEditorBridge.CopyClothingFromCoordinateCard(character, cardCoordinate, capturedDestinationCoordinate, sources, destinations, !capturedDestinationClothing);
                        else
                            MaterialEditorBridge.CopyOrMoveClothingProperties(character, capturedSourceCoordinate, capturedDestinationCoordinate,
                                sources, destinations, !capturedDestinationClothing, move);
                    }
                }
                else {
                    copied = cardParts != null
                        ? AccessoryOperations.CopyFromParts(character, cardParts, capturedDestinationCoordinate, sources, destinations, capturedReverseParent)
                        : AccessoryOperations.CopyOrMoveBetweenCoordinates(character, capturedSourceCoordinate, capturedDestinationCoordinate, sources, destinations, capturedReverseParent, move);
                    if (copied > 0) {
                        MaterialEditorBridge.RestoreState(materialEditorState);
                        if (cardParts != null)
                            MaterialEditorBridge.CopyFromCoordinateCard(character, cardCoordinate, capturedDestinationCoordinate, sources, destinations);
                        else
                            MaterialEditorBridge.CopyOrMoveAccessoryProperties(character, capturedSourceCoordinate,
                                capturedDestinationCoordinate, sources, destinations, move);
                    }
                }
                CompleteDataOperation(character, "已" + action + " " + copied + " 组槽位。");
            });
        }

        private void ConfirmInsertOperation(ChaControl character, bool atFront) {
            if (destinationListIsClothing) {
                status = "服装槽位类别固定，不支持最前或最后插入。";
                return;
            }
            string position = atFront ? "最前" : "最后";
            string sourceName = sourceIsCoordinateCard ? coordinateCardSource.DisplayName : CoordinateNames[sourceCoordinate];
            ChaFileCoordinate sourceCoordinateData = sourceIsCoordinateCard ? coordinateCardSource.Coordinate : AccessoryOperations.GetCoordinate(character, sourceCoordinate);
            ChaFileAccessory.PartsInfo[] sourceParts = sourceListIsClothing
                ? AccessoryOperations.ConvertClothingToAccessories(character, sourceCoordinateData.clothes.parts)
                : sourceCoordinateData.accessory.parts;
            ChaFileCoordinate cardCoordinate = sourceIsCoordinateCard ? coordinateCardSource.Coordinate : null;
            List<int> sources = Sorted(withinSources);
            int capturedSourceCoordinate = sourceCoordinate;
            int capturedDestinationCoordinate = destinationCoordinate;
            bool capturedReverseParent = reverseParent;
            bool capturedSourceClothing = sourceListIsClothing;
            RequestConfirmation("确定将“" + sourceName + "”中选中的 " + sources.Count + " 个槽位插入到“" +
                CoordinateNames[destinationCoordinate] + "”的" + position + "吗？", delegate {
                object materialEditorState = MaterialEditorBridge.CaptureState(character);
                int insertIndex;
                List<int> destinations;
                int copied = AccessoryOperations.InsertSlots(character, sourceParts, capturedDestinationCoordinate,
                    sources, atFront, capturedReverseParent, out insertIndex, out destinations);
                if (copied > 0) {
                    MaterialEditorBridge.RestoreState(materialEditorState);
                    if (capturedSourceClothing) {
                        MaterialEditorBridge.ShiftAccessoriesForInsert(character, capturedDestinationCoordinate, insertIndex, copied);
                        if (cardCoordinate != null)
                            MaterialEditorBridge.CopyClothingFromCoordinateCard(character, cardCoordinate, capturedDestinationCoordinate, sources, destinations, true);
                        else
                            MaterialEditorBridge.CopyOrMoveClothingProperties(character, capturedSourceCoordinate, capturedDestinationCoordinate,
                                sources, destinations, true, false);
                    }
                    else if (cardCoordinate != null) {
                        MaterialEditorBridge.ShiftAccessoriesForInsert(character, capturedDestinationCoordinate, insertIndex, copied);
                        MaterialEditorBridge.CopyFromCoordinateCard(character, cardCoordinate, capturedDestinationCoordinate, sources, destinations);
                    }
                    else {
                        MaterialEditorBridge.InsertAccessories(character, capturedSourceCoordinate, capturedDestinationCoordinate,
                            sources, destinations, insertIndex);
                    }
                }
                CompleteDataOperation(character, "已向" + position + "插入 " + copied + " 个槽位。");
            });
        }

        private void ConfirmAdjustment(ChaControl character, int adjustment, MirrorMode mirror, string label) {
            int pairCount = PairCount();
            ChaFileAccessory.PartsInfo[] cardParts = sourceIsCoordinateCard ? coordinateCardSource.Parts : null;
            List<int> sources = Sorted(withinSources);
            List<int> destinations = Sorted(withinDestinations);
            int capturedSourceCoordinate = sourceCoordinate;
            int capturedDestinationCoordinate = destinationCoordinate;
            RequestConfirmation("确定执行“" + label + "”到 " + pairCount + " 组槽位吗？", delegate {
                int copied = cardParts != null
                    ? AccessoryOperations.CopyAdjustmentsFromParts(character, cardParts, capturedDestinationCoordinate, sources, destinations, adjustment, mirror)
                    : AccessoryOperations.CopyAdjustmentsBetweenCoordinates(character, capturedSourceCoordinate, capturedDestinationCoordinate, sources, destinations, adjustment, mirror);
                CompleteDataOperation(character, label + "完成，共 " + copied + " 组。");
            });
        }

        private void CompleteDataOperation(ChaControl character, string message) {
            if (clearSelectionAfterOperation.Value) ClearAllSelections();
            if (alwaysImmediateSync.Value) {
                accessoryHighlighter.Clear();
                AccessoryOperations.RefreshCharacter(character);
                status = message + " 已同步角色模型。";
            } else {
                status = message + " 尚未同步角色模型。";
            }
        }

        private void ClearAllSelections() {
            selected.Clear();
            withinSources.Clear();
            withinDestinations.Clear();
            batchSelectionAnchor = -1;
            sourceSelectionAnchor = -1;
            destinationSelectionAnchor = -1;
            ClearLastSelected();
        }

        private void AddFront(ChaControl character, int count) {
            int currentCoordinate = character.fileStatus.coordinateType;
            AccessoryOperations.AddAtFront(character, count);
            selected.Clear();
            ClearLastSelected();
            if (!sourceIsCoordinateCard && sourceCoordinate == currentCoordinate) ShiftSelection(withinSources, count);
            if (destinationCoordinate == currentCoordinate) ShiftSelection(withinDestinations, count);
            CompleteDataOperation(character, "已在最前添加 " + count + " 个空槽，原饰品整体后移。");
        }

        private void RequestConfirmation(string text, Action action) {
            confirmationText = text;
            AccessoryEditorTarget target = CaptureEditorTarget();
            int lifetime = editorSession.LifetimeRevision;
            confirmedAction = delegate {
                if (!editorSession.TryConfirm(lifetime, target, CaptureEditorTarget(), action))
                    status = "角色、来源、目标或选区已变化，请重新选择并确认操作。";
            };
            visible = true;
        }

        private IEnumerator InstallNativeButtons() {
            nativeFrontButtons = null;
            RemoveDuplicateNativeButtonGroups(null);
            GameObject sourceGroup = null;
            for (int frame = 0; frame < 600 && sourceGroup == null; frame++) {
                sourceGroup = GameObject.Find("Add Buttons Group");
                if (sourceGroup == null || sourceGroup.transform.childCount < 2) {
                    sourceGroup = null;
                    yield return null;
                }
            }
            if (sourceGroup == null || !MakerAPI.InsideMaker) {
                installingNativeButtons = false;
                yield break;
            }

            // MoreAccessories treats content child 0 as the accessory-slot template.  Putting our
            // group there makes its UpdateUI clone buttons as slots and abort at 20. Keep both
            // groups in the native content, with the front-insert group after the original one.
            nativeAddButtons = sourceGroup.transform;
            Transform content = nativeAddButtons.parent;
            nativeFrontButtons = Object.Instantiate(sourceGroup, content);
            nativeFrontButtons.name = "Add Front Buttons Group";
            nativeFrontButtons.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
            Button[] buttons = nativeFrontButtons.GetComponentsInChildren<Button>(true);
            if (buttons.Length < 2) {
                Object.Destroy(nativeFrontButtons);
                nativeFrontButtons = null;
                installingNativeButtons = false;
                yield break;
            }
            ConfigureNativeButton(buttons[0], "+1 到最前", 1);
            ConfigureNativeButton(buttons[1], "+10 到最前", 10);
            installingNativeButtons = false;
        }

        private static void RemoveDuplicateNativeButtonGroups(GameObject keep) {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject candidate in objects) {
                if (candidate == null || candidate == keep || candidate.name != "Add Front Buttons Group") continue;
                if (candidate.scene.IsValid()) Object.Destroy(candidate);
            }
        }

        private void ConfigureNativeButton(Button button, string label, int count) {
            button.gameObject.name = "Add Front " + count + " Button";
            SetNativeText(button.gameObject, label);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(delegate {
                ChaControl character = AccessoryOperations.GetCharacter();
                if (character != null) AddFront(character, count);
            });
        }

        private static void SetNativeText(GameObject root, string text) {
            foreach (Component component in root.GetComponentsInChildren<Component>(true)) {
                if (component == null) continue;
                PropertyInfo property = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.PropertyType == typeof(string) && property.CanWrite) {
                    property.SetValue(component, text, null);
                }
            }
        }

        private void UpdateHighlights(ChaControl character) {
            if (!visible || character == null || tab == 2) {
                accessoryHighlighter.Clear();
                return;
            }
            int currentCoordinate = character.fileStatus.coordinateType;
            var highlighted = new HashSet<int>();
            if (selectionHighlightEnabled.Value) {
                if (tab == 0) highlighted.UnionWith(selected);
                else {
                    if (!sourceListIsClothing && !sourceIsCoordinateCard && sourceCoordinate == currentCoordinate) highlighted.UnionWith(withinSources);
                    if (!destinationListIsClothing && destinationCoordinate == currentCoordinate) highlighted.UnionWith(withinDestinations);
                }
            }
            int accent = lastSelectedCoordinate == currentCoordinate && highlighted.Contains(lastSelectedSlot) ? lastSelectedSlot : -1;
            accessoryHighlighter.Update(character, accent, highlighted);
        }

        private void SetLastSelected(int slot, int coordinate) {
            lastSelectedSlot = slot;
            lastSelectedCoordinate = coordinate;
        }

        private void ClearLastSelected() {
            lastSelectedSlot = -1;
            lastSelectedCoordinate = -1;
        }

        private string MappingHint(bool move) {
            string counts = "（来源 " + withinSources.Count + "，目标 " + withinDestinations.Count + "）";
            if (withinSources.Count == 0 || withinDestinations.Count == 0) return "请选择来源和目标" + counts + "。";
            if (sourceListIsClothing && destinationListIsClothing) {
                if (!AccessoryOperations.IsValidClothingMapping(Sorted(withinSources), Sorted(withinDestinations), move,
                    sourceIsCoordinateCard ? -1 : sourceCoordinate, destinationCoordinate))
                    return "服装只能对应同类槽位；室内鞋和室外鞋例外，可交叉或一对多" + counts + "。";
                return "同类服装槽位将一一对应；鞋子允许室内/室外交叉或一对多，共 " + PairCount() + " 组。";
            }
            if (withinSources.Count == withinDestinations.Count) return "按槽位编号从小到大一一对应，共 " + withinSources.Count + " 组。";
            if (!move && withinSources.Count == 1) return "一个来源将复制到 " + withinDestinations.Count + " 个目标。";
            return (move ? "移动时来源与目标数量必须相同" : "多来源复制时，来源与目标数量必须相同") + counts + "。";
        }

        private bool HasValidMapping(bool move) {
            if (withinSources.Count == 0 || withinDestinations.Count == 0) return false;
            if (sourceListIsClothing) {
                if (destinationListIsClothing)
                    return AccessoryOperations.IsValidClothingMapping(Sorted(withinSources), Sorted(withinDestinations), move,
                        sourceIsCoordinateCard ? -1 : sourceCoordinate, destinationCoordinate);
                return withinSources.Count == withinDestinations.Count || !move && withinSources.Count == 1;
            }
            if (withinSources.Count == withinDestinations.Count)
                return sourceIsCoordinateCard || sourceCoordinate != destinationCoordinate || !Sorted(withinSources).SequenceEqual(Sorted(withinDestinations));
            return !move && withinSources.Count == 1;
        }

        private int PairCount() { return withinSources.Count == 1 ? withinDestinations.Count : Math.Min(withinSources.Count, withinDestinations.Count); }
        private static List<int> Sorted(HashSet<int> values) { return values.OrderBy(delegate(int value) { return value; }).ToList(); }

        private static void SelectAll(HashSet<int> selection, int count) {
            selection.Clear();
            for (int i = 0; i < count; i++) selection.Add(i);
        }

        private static int SelectNonEmpty(HashSet<int> selection, ChaFileAccessory.PartsInfo[] parts, int count) {
            selection.Clear();
            int last = -1;
            for (int i = 0; i < count && i < parts.Length; i++) {
                if (AccessoryOperations.IsEmpty(parts[i])) continue;
                selection.Add(i);
                last = i;
            }
            return last;
        }

        private static int SelectNonEmptyClothing(HashSet<int> selection, ChaFileClothes.PartsInfo[] parts) {
            selection.Clear();
            int last = -1;
            for (int i = 0; i < AccessoryOperations.ClothingSlotCount && i < parts.Length; i++) {
                if (parts[i] == null || parts[i].id == 0) continue;
                selection.Add(i);
                last = i;
            }
            return last;
        }

        private static void SetSelected(HashSet<int> selection, int index, bool value) {
            if (value) selection.Add(index);
            else selection.Remove(index);
        }

        private static void PruneSelection(HashSet<int> selection, int count) {
            selection.RemoveWhere(delegate(int index) { return index < 0 || index >= count; });
        }

        private static void ShiftSelection(HashSet<int> selection, int count) {
            int[] values = selection.ToArray();
            selection.Clear();
            foreach (int value in values) selection.Add(value + count);
        }

        private static string NameAt(ChaControl character, ChaFileAccessory.PartsInfo[] parts, int index) {
            return index >= 0 && index < parts.Length ? AccessoryOperations.GetName(character, parts[index]) : "空";
        }

    }
}
