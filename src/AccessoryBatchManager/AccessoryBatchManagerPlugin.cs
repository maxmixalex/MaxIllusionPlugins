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
    public sealed class AccessoryBatchManagerPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.AccessoryBatchManager";
        public const string Version = "0.6.3";

        private const int WindowId = 68410931;
        private const int ConfirmWindowId = WindowId + 1;
        private const int ComboWindowId = WindowId + 2;
        private const float WindowWidth = 700f;
        private const float WindowHeight = 720f;
        private const float FoldedWindowHeight = 20f;
        private static readonly string[] TabNames = { "批量管理", "复制/移动", "设置" };
        private static readonly string[] CoordinateNames = { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣" };

        private ConfigEntry<KeyboardShortcut> toggleShortcut;
        private ConfigEntry<bool> alwaysImmediateSync;
        private ConfigEntry<bool> syncDestinationToCurrentCoordinate;
        private ConfigEntry<bool> selectionHighlightEnabled;
        private ConfigEntry<bool> clearSelectionAfterOperation;
        private bool visible;
        private bool windowFolded;
        private Rect windowRect = new Rect(60f, 40f, WindowWidth, WindowHeight);
        private Rect confirmRect = new Rect(0f, 0f, 360f, 145f);
        private Vector2 batchScroll;
        private Vector2 sourceScroll;
        private Vector2 destinationScroll;
        private int tab;
        private int sourceCoordinate;
        private int destinationCoordinate = 1;
        private int sourceSelectionAnchor = -1;
        private int destinationSelectionAnchor = -1;
        private int batchSelectionAnchor = -1;
        private Rect sourceComboAnchor;
        private Rect destinationComboAnchor;
        private int openComboOwner;
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
        private GUISkin whiteSkin;
        private GUIStyle windowStyle;
        private GUIStyle foldedWindowStyle;
        private GUIStyle rowStyle;
        private GUIStyle statusStyle;
        private GUIStyle tabStyle;
        private GameObject nativeFrontButtons;
        private Transform nativeAddButtons;
        private bool installingNativeButtons;
        private ClassicComboBox combo;
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
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.MakerExiting -= OnMakerExiting;
            accessoryHighlighter.Dispose();
            if (nativeFrontButtons != null) Object.Destroy(nativeFrontButtons);
        }

        private void OnMakerFinishedLoading(object sender, EventArgs eventArgs) {
            if (installingNativeButtons) return;
            installingNativeButtons = true;
            StartCoroutine(InstallNativeButtons());
        }

        private void OnMakerExiting(object sender, EventArgs eventArgs) {
            visible = false;
            confirmedAction = null;
            selected.Clear();
            batchSelectionAnchor = -1;
            ClearLastSelected();
            withinSources.Clear();
            withinDestinations.Clear();
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
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool plusPressed = (Input.GetKeyDown(KeyCode.Equals) && shift) || Input.GetKeyDown(KeyCode.KeypadPlus);
            if (toggleShortcut.Value.IsDown() || plusPressed) {
                visible = !visible;
                if (!visible) accessoryHighlighter.Clear();
            }
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
            EnsureStyles();

            GUISkin oldSkin = GUI.skin;
            Color oldBackground = GUI.backgroundColor;
            Color oldContent = GUI.contentColor;
            GUI.skin = whiteSkin;
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.8f);
            GUI.contentColor = Color.black;
            windowRect.height = windowFolded ? FoldedWindowHeight : Mathf.Max(windowRect.height, WindowHeight);
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "饰品批量管理 " + Version, windowFolded ? foldedWindowStyle : windowStyle);
            windowRect.x = Mathf.Clamp(windowRect.x, -WindowWidth + 40f, Screen.width - 40f);
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Screen.height - 30f);

            if (confirmedAction != null) {
                if (confirmRect.x == 0f && confirmRect.y == 0f)
                    confirmRect = new Rect((Screen.width - confirmRect.width) * 0.5f, (Screen.height - confirmRect.height) * 0.5f, confirmRect.width, confirmRect.height);
                confirmRect = GUI.Window(ConfirmWindowId, confirmRect, DrawConfirmation, "确认操作", windowStyle);
            }

            if (combo.Show) {
                combo.Follow(windowRect);
                combo.Rect = GUI.Window(ComboWindowId, combo.Rect, combo.DrawWindow, string.Empty, whiteSkin.box);
                GUI.BringWindowToFront(ComboWindowId);
            }

            if (Event.current != null && Event.current.type == EventType.Repaint) {
                UpdateHighlights(AccessoryOperations.GetCharacter());
            }

            GUI.skin = oldSkin;
            GUI.backgroundColor = oldBackground;
            GUI.contentColor = oldContent;
            BlockMakerInput();
        }

        private void DrawWindow(int id) {
            float buttonSize = windowStyle.fontSize * 1.4f;
            float closeX = windowRect.width - buttonSize;
            float foldX = closeX - buttonSize;
            if (GUI.Button(new Rect(foldX, 0f, buttonSize, buttonSize), windowFolded ? "+" : "-")) {
                windowFolded = !windowFolded;
                combo.Hide();
                if (!windowFolded) windowRect.height = WindowHeight;
            }
            if (GUI.Button(new Rect(closeX, 0f, buttonSize, buttonSize), "×")) {
                combo.Hide();
                visible = false;
                accessoryHighlighter.Clear();
            }
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, foldX), FoldedWindowHeight));
            if (windowFolded) return;
            GUI.enabled = confirmedAction == null;

            GUILayout.Space(18f);
            tab = GUILayout.Toolbar(tab, TabNames, tabStyle, GUILayout.Height(28f));
            ChaControl character = AccessoryOperations.GetCharacter();
            if (character == null) {
                GUILayout.Label("正在等待角色编辑器加载……", statusStyle);
                GUI.enabled = true;
                return;
            }

            switch (tab) {
                case 0: DrawBatch(character); break;
                case 1: DrawWithinCoordinate(character); break;
                default: DrawSettings(character); break;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(status, statusStyle, GUILayout.Height(22f));
            GUI.enabled = true;
        }

        private void DrawBatch(ChaControl character) {
            ChaFileAccessory.PartsInfo[] parts = AccessoryOperations.CurrentParts(character);
            int shown = AccessoryOperations.EffectiveSlotCount(parts);
            PruneSelection(selected, shown);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(80f))) {
                SelectAll(selected, shown);
                SetLastSelected(shown - 1, character.fileStatus.coordinateType);
            }
            if (GUILayout.Button("全不选", GUILayout.Width(80f))) {
                selected.Clear();
                ClearLastSelected();
            }
            GUILayout.Space(8f);
            GUI.enabled = selected.Count > 0 && confirmedAction == null;
            if (GUILayout.Button("移除选中饰品", GUILayout.Width(140f))) {
                int requested = selected.Count;
                RequestConfirmation("确定移除选中的 " + requested + " 个槽位内容吗？", delegate {
                    int count = AccessoryOperations.RemoveSelected(character, selected.ToArray());
                    selected.Clear();
                    ClearLastSelected();
                    CompleteDataOperation(character, "已移除 " + count + " 个饰品。");
                });
            }
            GUI.enabled = confirmedAction == null;
            if (GUILayout.Button("一键整理（保持顺序）", GUILayout.Width(170f))) {
                RequestConfirmation("确定整理当前搭配吗？所有非空饰品会依次前移。", delegate {
                    int count = AccessoryOperations.Compact(character);
                    selected.Clear();
                    ClearLastSelected();
                    CompleteDataOperation(character, count == 0 ? "没有需要整理的空隙。" : "已整理，前移 " + count + " 个饰品。");
                });
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("饰品 " + AccessoryOperations.FilledCount(parts) + " / 槽位 " + shown, GUILayout.Width(125f));
            GUILayout.EndHorizontal();

            batchScroll = GUILayout.BeginScrollView(batchScroll, GUI.skin.box, GUILayout.Height(605f));
            DrawShiftSelectableList(character, parts, shown, selected, ref batchSelectionAnchor, character.fileStatus.coordinateType);
            GUILayout.EndScrollView();
        }

        private void DrawWithinCoordinate(ChaControl character) {
            if (syncDestinationToCurrentCoordinate.Value)
                destinationCoordinate = Mathf.Clamp(character.fileStatus.coordinateType, 0, CoordinateNames.Length - 1);
            bool cardAvailable = coordinateCardSource.TryRefresh();
            if (sourceIsCoordinateCard && !cardAvailable) {
                sourceIsCoordinateCard = false;
                withinSources.Clear();
                sourceSelectionAnchor = -1;
                ClearLastSelected();
                status = "服装卡来源已失效，已切回角色搭配。";
            }
            DrawCoordinateControllers();
            ChaFileCoordinate sourceCoordinateData = sourceIsCoordinateCard ? coordinateCardSource.Coordinate : character.chaFile.coordinate[sourceCoordinate];
            ChaFileCoordinate destinationCoordinateData = character.chaFile.coordinate[destinationCoordinate];
            ChaFileAccessory.PartsInfo[] sourceParts = sourceCoordinateData.accessory.parts;
            ChaFileAccessory.PartsInfo[] destinationParts = destinationCoordinateData.accessory.parts;
            ChaFileClothes.PartsInfo[] sourceClothes = sourceCoordinateData.clothes.parts;
            ChaFileClothes.PartsInfo[] destinationClothes = destinationCoordinateData.clothes.parts;
            int sourceShown = sourceListIsClothing ? AccessoryOperations.ClothingSlotCount : AccessoryOperations.EffectiveSlotCount(sourceParts);
            int destinationShown = destinationListIsClothing ? AccessoryOperations.ClothingSlotCount : AccessoryOperations.EffectiveSlotCount(destinationParts);
            PruneSelection(withinSources, sourceShown);
            PruneSelection(withinDestinations, destinationShown);
            if (sourceSelectionAnchor >= sourceShown) sourceSelectionAnchor = -1;
            if (destinationSelectionAnchor >= destinationShown) destinationSelectionAnchor = -1;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(325f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("来源");
            if (GUILayout.Button("全选", GUILayout.Width(48f))) {
                SelectAll(withinSources, sourceShown);
                sourceSelectionAnchor = sourceShown - 1;
                SetLastSelected(sourceSelectionAnchor, sourceIsCoordinateCard ? -1 : sourceCoordinate);
            }
            if (GUILayout.Button("全选非空", GUILayout.Width(70f))) {
                sourceSelectionAnchor = sourceListIsClothing
                    ? SelectNonEmptyClothing(withinSources, sourceClothes)
                    : SelectNonEmpty(withinSources, sourceParts, sourceShown);
                SetLastSelected(sourceSelectionAnchor, sourceIsCoordinateCard ? -1 : sourceCoordinate);
            }
            if (GUILayout.Button("全不选", GUILayout.Width(58f))) {
                withinSources.Clear();
                sourceSelectionAnchor = -1;
                ClearLastSelected();
            }
            if (GUILayout.Button(sourceListIsClothing ? "服装" : "饰品", GUILayout.Width(48f))) {
                sourceListIsClothing = !sourceListIsClothing;
                if (!sourceListIsClothing) destinationListIsClothing = false;
                withinSources.Clear();
                sourceSelectionAnchor = -1;
                ClearLastSelected();
            }
            GUILayout.EndHorizontal();
            Vector2 oldSourceScroll = sourceScroll;
            sourceScroll = GUILayout.BeginScrollView(sourceScroll, GUI.skin.box, GUILayout.Height(390f));
            if (sourceListIsClothing)
                DrawShiftSelectableClothingList(character, sourceClothes, withinSources, ref sourceSelectionAnchor, sourceIsCoordinateCard ? -1 : sourceCoordinate);
            else
                DrawShiftSelectableList(character, sourceParts, sourceShown, withinSources, ref sourceSelectionAnchor, sourceIsCoordinateCard ? -1 : sourceCoordinate);
            GUILayout.EndScrollView();
            if (syncScroll && sourceScroll != oldSourceScroll) destinationScroll = sourceScroll;
            GUILayout.EndVertical();
            GUILayout.Space(18f);
            GUILayout.BeginVertical(GUILayout.Width(325f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("目标");
            if (GUILayout.Button("全选", GUILayout.Width(48f))) {
                SelectAll(withinDestinations, destinationShown);
                destinationSelectionAnchor = destinationShown - 1;
                SetLastSelected(destinationSelectionAnchor, destinationCoordinate);
            }
            if (GUILayout.Button("全选非空", GUILayout.Width(70f))) {
                destinationSelectionAnchor = destinationListIsClothing
                    ? SelectNonEmptyClothing(withinDestinations, destinationClothes)
                    : SelectNonEmpty(withinDestinations, destinationParts, destinationShown);
                SetLastSelected(destinationSelectionAnchor, destinationCoordinate);
            }
            if (GUILayout.Button("全不选", GUILayout.Width(58f))) {
                withinDestinations.Clear();
                destinationSelectionAnchor = -1;
                ClearLastSelected();
            }
            GUI.enabled = sourceListIsClothing && confirmedAction == null;
            if (GUILayout.Button(destinationListIsClothing ? "服装" : "饰品", GUILayout.Width(48f))) {
                destinationListIsClothing = !destinationListIsClothing;
                withinDestinations.Clear();
                destinationSelectionAnchor = -1;
                ClearLastSelected();
            }
            GUI.enabled = confirmedAction == null;
            GUILayout.EndHorizontal();
            Vector2 oldDestinationScroll = destinationScroll;
            destinationScroll = GUILayout.BeginScrollView(destinationScroll, GUI.skin.box, GUILayout.Height(390f));
            if (destinationListIsClothing)
                DrawShiftSelectableClothingList(character, destinationClothes, withinDestinations, ref destinationSelectionAnchor, destinationCoordinate);
            else
                DrawShiftSelectableList(character, destinationParts, destinationShown, withinDestinations, ref destinationSelectionAnchor, destinationCoordinate);
            GUILayout.EndScrollView();
            if (syncScroll && destinationScroll != oldDestinationScroll) sourceScroll = destinationScroll;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Label(MappingHint(false), statusStyle, GUILayout.Height(20f));
            GUILayout.BeginHorizontal();
            reverseParent = GUILayout.Toggle(reverseParent, "位置左右交换", GUILayout.Width(125f));
            syncScroll = GUILayout.Toggle(syncScroll, "同步滚动", GUILayout.Width(100f));
            GUI.enabled = HasValidMapping(false) && confirmedAction == null;
            if (GUILayout.Button("复制", GUILayout.Width(70f))) ConfirmSlotOperation(character, false);
            GUI.enabled = !sourceIsCoordinateCard && HasValidMapping(true) && confirmedAction == null;
            if (GUILayout.Button("移动", GUILayout.Width(70f))) ConfirmSlotOperation(character, true);
            GUI.enabled = !destinationListIsClothing && withinSources.Count > 0 && confirmedAction == null;
            if (GUILayout.Button("最前插入", GUILayout.Width(90f))) ConfirmInsertOperation(character, true);
            if (GUILayout.Button("最后插入", GUILayout.Width(90f))) ConfirmInsertOperation(character, false);
            GUI.enabled = confirmedAction == null;
            GUILayout.EndHorizontal();

            GUI.enabled = !sourceListIsClothing && !destinationListIsClothing && confirmedAction == null;
            DrawAdjustmentButtons(character, 0, "调整①");
            DrawAdjustmentButtons(character, 1, "调整②");
            GUI.enabled = confirmedAction == null;
        }

        private void DrawAdjustmentButtons(ChaControl character, int adjustment, string label) {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            GUI.enabled = HasValidMapping(false) && confirmedAction == null;
            if (GUILayout.Button("复制", GUILayout.Width(120f))) ConfirmAdjustment(character, adjustment, MirrorMode.None, label + "复制");
            if (GUILayout.Button("左右反转", GUILayout.Width(120f))) ConfirmAdjustment(character, adjustment, MirrorMode.LeftRight, label + "左右反转复制");
            if (GUILayout.Button("上下反转", GUILayout.Width(120f))) ConfirmAdjustment(character, adjustment, MirrorMode.UpDown, label + "上下反转复制");
            GUI.enabled = confirmedAction == null;
            GUILayout.EndHorizontal();
        }

        private void DrawCoordinateControllers() {
            string[] sourceNames = SourceNames();
            int sourceIndex = sourceIsCoordinateCard ? CoordinateNames.Length : sourceCoordinate;
            GUILayout.BeginHorizontal();
            GUILayout.Label("来源:", GUILayout.Width(42f));
            if (GUILayout.Button("<", GUILayout.Width(28f))) SetSourceIndex(CycleIndex(sourceIndex, -1, sourceNames.Length));
            bool sourceClicked = GUILayout.Button(sourceNames[sourceIndex], GUILayout.Width(175f));
            Rect sourceRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint && sourceRect.width > 1f) sourceComboAnchor = sourceRect;
            if (sourceClicked) OpenCoordinateDropdown(sourceComboAnchor, true);
            if (GUILayout.Button(">", GUILayout.Width(28f))) SetSourceIndex(CycleIndex(sourceIndex, 1, sourceNames.Length));
            GUILayout.Label("→", new GUIStyle(statusStyle) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(24f));
            GUILayout.Label("目标:", GUILayout.Width(42f));
            bool controlsEnabled = GUI.enabled;
            GUI.enabled = controlsEnabled && !syncDestinationToCurrentCoordinate.Value;
            if (GUILayout.Button("<", GUILayout.Width(28f))) destinationCoordinate = CycleCoordinate(destinationCoordinate, -1);
            bool destinationClicked = GUILayout.Button(CoordinateNames[destinationCoordinate], GUILayout.Width(175f));
            Rect destinationRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint && destinationRect.width > 1f) destinationComboAnchor = destinationRect;
            if (destinationClicked) OpenCoordinateDropdown(destinationComboAnchor, false);
            if (GUILayout.Button(">", GUILayout.Width(28f))) destinationCoordinate = CycleCoordinate(destinationCoordinate, 1);
            GUI.enabled = controlsEnabled;
            GUILayout.EndHorizontal();
        }

        private void OpenCoordinateDropdown(Rect buttonRect, bool source) {
            int owner = source ? 1 : 2;
            if (combo.Show && openComboOwner == owner) {
                combo.Hide();
                openComboOwner = 0;
                return;
            }
            string[] names = source ? SourceNames() : CoordinateNames;
            combo.Set(windowRect, buttonRect, names, delegate(int index) {
                if (source) SetSourceIndex(index);
                else destinationCoordinate = index;
                openComboOwner = 0;
            });
            openComboOwner = owner;
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
                        : character.chaFile.coordinate[capturedSourceCoordinate].clothes.parts;
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
            ChaFileCoordinate sourceCoordinateData = sourceIsCoordinateCard ? coordinateCardSource.Coordinate : character.chaFile.coordinate[sourceCoordinate];
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

        private void DrawSettings(ChaControl character) {
            GUILayout.Space(18f);
            GUILayout.Label("数据同步", new GUIStyle(statusStyle) { fontStyle = FontStyle.Bold }, GUILayout.Height(28f));
            GUILayout.Label("默认只修改角色数据；需要查看最终结果时可立即重建模型。", statusStyle, GUILayout.Height(32f));
            GUILayout.BeginHorizontal();
            bool immediate = GUILayout.Toggle(alwaysImmediateSync.Value, "每次操作后总是立即同步", GUILayout.Width(285f), GUILayout.Height(34f));
            if (immediate != alwaysImmediateSync.Value) {
                alwaysImmediateSync.Value = immediate;
                Config.Save();
            }
            if (GUILayout.Button("立即刷新数据并更新角色模型", GUILayout.Height(34f))) {
                RequestConfirmation("确定立即同步饰品数据并重建当前角色模型吗？", delegate {
                    accessoryHighlighter.Clear();
                    AccessoryOperations.RefreshCharacter(character);
                    status = "数据同步完成，角色模型已更新。";
                });
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(24f);
            GUILayout.Label("饰品高亮", new GUIStyle(statusStyle) { fontStyle = FontStyle.Bold }, GUILayout.Height(28f));
            bool selection = GUILayout.Toggle(selectionHighlightEnabled.Value, "启用选择高亮（最近选择为橙色，其余为蓝色）", GUILayout.Height(30f));
            if (selection != selectionHighlightEnabled.Value) {
                selectionHighlightEnabled.Value = selection;
                Config.Save();
            }
            bool clearAfter = GUILayout.Toggle(clearSelectionAfterOperation.Value, "操作完成后清除选区", GUILayout.Height(30f));
            if (clearAfter != clearSelectionAfterOperation.Value) {
                clearSelectionAfterOperation.Value = clearAfter;
                Config.Save();
            }
            bool followCurrent = GUILayout.Toggle(syncDestinationToCurrentCoordinate.Value,
                "复制/移动目标始终跟随当前服装搭配", GUILayout.Height(30f));
            if (followCurrent != syncDestinationToCurrentCoordinate.Value) {
                syncDestinationToCurrentCoordinate.Value = followCurrent;
                if (followCurrent) destinationCoordinate = Mathf.Clamp(character.fileStatus.coordinateType, 0, CoordinateNames.Length - 1);
                Config.Save();
            }
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
            combo.Hide();
            confirmationText = text;
            confirmedAction = action;
            visible = true;
            confirmRect.x = (Screen.width - confirmRect.width) * 0.5f;
            confirmRect.y = (Screen.height - confirmRect.height) * 0.5f;
        }

        private void DrawConfirmation(int id) {
            GUI.DragWindow(new Rect(0f, 0f, confirmRect.width, 23f));
            GUILayout.Space(8f);
            GUILayout.Label(confirmationText, statusStyle, GUILayout.Height(55f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确认", GUILayout.Height(30f))) {
                Action action = confirmedAction;
                confirmedAction = null;
                if (action != null) action();
            }
            if (GUILayout.Button("取消", GUILayout.Height(30f))) confirmedAction = null;
            GUILayout.EndHorizontal();
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

        private void DrawShiftSelectableList(ChaControl character, ChaFileAccessory.PartsInfo[] parts, int count, HashSet<int> selection, ref int anchor, int coordinate) {
            for (int i = 0; i < count; i++) {
                bool value = selection.Contains(i);
                GUILayout.BeginHorizontal(rowStyle, GUILayout.Height(25f));
                bool changed = GUILayout.Toggle(value, (i + 1) + "  " + AccessoryOperations.GetName(character, parts[i]));
                if (changed != value) {
                    bool shift = Event.current != null && Event.current.shift;
                    if (shift && anchor >= 0) {
                        int first = Math.Min(anchor, i);
                        int last = Math.Max(anchor, i);
                        for (int slot = first; slot <= last; slot++) SetSelected(selection, slot, changed);
                    } else {
                        SetSelected(selection, i, changed);
                    }
                    if (changed) SetLastSelected(i, coordinate);
                    else if (lastSelectedSlot == i && lastSelectedCoordinate == coordinate) ClearLastSelected();
                    anchor = i;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawShiftSelectableClothingList(ChaControl character, ChaFileClothes.PartsInfo[] parts,
            HashSet<int> selection, ref int anchor, int coordinate) {
            string[] slotNames = { "上衣", "下衣", "胸罩", "内裤", "手套", "连裤袜", "袜子", "鞋子(室内)", "鞋子(室外)" };
            for (int i = 0; i < AccessoryOperations.ClothingSlotCount; i++) {
                bool value = selection.Contains(i);
                GUILayout.BeginHorizontal(rowStyle, GUILayout.Height(25f));
                string itemName = AccessoryOperations.GetClothingName(character, parts[i], i);
                bool changed = GUILayout.Toggle(value, slotNames[i] + " · " + itemName);
                if (changed != value) {
                    bool shift = Event.current != null && Event.current.shift;
                    if (shift && anchor >= 0) {
                        int first = Math.Min(anchor, i);
                        int last = Math.Max(anchor, i);
                        for (int slot = first; slot <= last; slot++) SetSelected(selection, slot, changed);
                    }
                    else SetSelected(selection, i, changed);
                    if (changed) SetLastSelected(i, -2);
                    else if (lastSelectedSlot == i && lastSelectedCoordinate == -2) ClearLastSelected();
                    anchor = i;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void UpdateHighlights(ChaControl character) {
            if (!visible || character == null || windowFolded || tab == 2) {
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

        private void BlockMakerInput() {
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool overWindow = windowRect.Contains(mouse) || (confirmedAction != null && confirmRect.Contains(mouse)) || (combo != null && combo.Show && combo.Rect.Contains(mouse));
            if (overWindow && (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))) Input.ResetInputAxes();
        }

        private void EnsureStyles() {
            if (whiteSkin != null) return;
            whiteSkin = ClassicGuiSkin.Create(GUI.skin);
            windowStyle = new GUIStyle(whiteSkin.window);
            foldedWindowStyle = ClassicGuiSkin.CreateFoldedWindowStyle(windowStyle);
            rowStyle = ClassicGuiSkin.CreatePanelStyle();
            statusStyle = new GUIStyle(whiteSkin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
            tabStyle = new GUIStyle(whiteSkin.button) { alignment = TextAnchor.MiddleCenter };
            combo = new ClassicComboBox(ComboWindowId, whiteSkin.label.fontSize);
        }
    }
}
