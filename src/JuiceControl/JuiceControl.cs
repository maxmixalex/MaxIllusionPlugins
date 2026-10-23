using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using KKAPI.Utilities;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(JuiceControl.JuiceControl.Version)]

namespace JuiceControl {
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    [BepInDependency("Marco.SkinEffects", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInPlugin(GUID, "JuiceControl", Version)]
    public class JuiceControl : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.JuiceControl";
        public const string Version = "0.1.3";

        private const int WindowId = 68150210;
        private const int ComboWindowId = WindowId + 1;
        private const int NameDialogWindowId = WindowId + 2;
        private const string DefaultPresetFileName = GUID + ".presets.xml";
        private const float DefaultWindowWidth = 390f;
        private const float DefaultWindowHeight = 700f;
        private const float MinWindowWidth = 360f;
        private const float MinWindowHeight = 560f;
        private const float FoldedWindowHeight = 20f;
        private const float NameDialogWidth = 260f;
        private const float NameDialogHeight = 126f;
        private const float EffectRowHeight = 22f;
        private const int SiruMaxLevel = 3;
        private const int TearMaxLevel = 3;
        private const int BlushMaxLevel = 4;

        private static readonly FieldInfo Male1Field = typeof(HSceneProc).GetField("male1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly SiruPartInfo[] SiruParts = {
            new SiruPartInfo(ChaFileDefine.SiruParts.SiruKao, "脸部精液"),
            new SiruPartInfo(ChaFileDefine.SiruParts.SiruFrontUp, "前上精液"),
            new SiruPartInfo(ChaFileDefine.SiruParts.SiruFrontDown, "前下精液"),
            new SiruPartInfo(ChaFileDefine.SiruParts.SiruBackUp, "后上精液"),
            new SiruPartInfo(ChaFileDefine.SiruParts.SiruBackDown, "后下精液")
        };

        internal static JuiceControl Instance { get; private set; }
        internal static ConfigEntry<bool> Enabled { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> ToggleMenu { get; private set; }
        internal static ConfigEntry<string> PresetFileName { get; private set; }
        internal static ConfigEntry<string> SkinEffectsIntegrationStatus { get; private set; }
        internal static ConfigEntry<bool> DebugLogging { get; private set; }

        private bool showWindow;
        private bool windowFolded;
        private Rect windowRect = new Rect(80f, 60f, DefaultWindowWidth, DefaultWindowHeight);
        private Vector2 effectScrollPosition;
        private bool mouseDownOnWindow;
        private Rect nameDialogRect = new Rect(0f, 0f, NameDialogWidth, NameDialogHeight);
        private bool showNameDialog;
        private bool focusNameDialogInput;
        private NameDialogMode nameDialogMode;
        private string nameDialogText = string.Empty;
        private string nameDialogMessage = string.Empty;
        private object nameDialogPreset;
        private ChaControl nameDialogCharacter;
        private readonly PresetStore presetStore = new PresetStore();
        private PresetXml presetXml { get { return presetStore.Data; } }
        private string presetPath { get { return presetStore.Path; } }
        private int selectedCharacterPresetIndex;
        private int selectedGroupPresetIndex;
        private string characterPresetName = string.Empty;
        private string groupPresetName = string.Empty;
        private ChaControl selectedCharacter;
        private string statusText = string.Empty;
        private Rect characterDropdownAnchor;
        private Rect characterPresetDropdownAnchor;
        private Rect groupPresetDropdownAnchor;
        private int characterScanFrame = -1;
        private List<CharacterEntry> cachedCharacters;

        private JuiceGui ui;
        private readonly Dictionary<ChaControl, Dictionary<string, int>> lockedEffectLevels = new Dictionary<ChaControl, Dictionary<string, int>>();

        private void Awake() {
            Instance = this;

            Enabled = Config.Bind("General", "Enabled", true, new ConfigDescription("Enable the manual skin effect editor.", null, new ConfigurationManagerAttributes { Order = 10 }));
            ToggleMenu = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.Alpha0), new ConfigDescription("Hotkey used to open the editor.", null, new ConfigurationManagerAttributes { Order = 9 }));
            PresetFileName = Config.Bind("Presets", "Preset XML file", DefaultPresetFileName, new ConfigDescription("Preset XML path. Relative paths are resolved under BepInEx/config.", null, new ConfigurationManagerAttributes { Order = 1 }));
            SkinEffectsIntegrationStatus = Config.Bind("Integration", "SkinEffects", "未检测", new ConfigDescription("Current SkinEffects integration status.", null, new ConfigurationManagerAttributes { Order = 0, ReadOnly = true }));
            DebugLogging = Config.Bind("Debug", "Logging", false, new ConfigDescription("Enable verbose logging.", null, new ConfigurationManagerAttributes { IsAdvanced = true }));

            PresetFileName.SettingChanged += OnPresetFileChanged;
            LoadPresets();
            RefreshIntegrations(true);
        }

        private void OnDestroy() {
            if (PresetFileName != null) PresetFileName.SettingChanged -= OnPresetFileChanged;
            lockedEffectLevels.Clear();
            cachedCharacters = null;
            selectedCharacter = null;
            if (Instance == this) Instance = null;
        }

        private void Start() {
            // Awake can precede optional plugins' initialization. Retry once when
            // all plugins have reached Start, then cache both hits and misses.
            RefreshIntegrations(true);
        }

        private void OnPresetFileChanged(object sender, EventArgs args) {
            LoadPresets();
        }

        private void Update() {
            if (!Enabled.Value) return;

            if (ToggleMenu.Value.IsDown()) {
                showWindow = !showWindow;
            }
        }

        private void LateUpdate() {
            if (!Enabled.Value) return;

            if (lockedEffectLevels.Count > 0) {
                ApplyLockedEffects();
            }
        }

        private void OnGUI() {
            if (!Enabled.Value || !showWindow) return;

            InitStyles();
            RefreshIntegrations(false);

            Color oldColor = GUI.color;
            Color oldContentColor = GUI.contentColor;
            Color oldBackgroundColor = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try {
                GUI.color = Color.white;
                GUI.contentColor = Color.black;
                GUI.backgroundColor = new Color(1f, 1f, 1f, 0.8f);

                windowRect.width = Mathf.Max(windowRect.width, DefaultWindowWidth);
                windowRect.height = windowFolded ? FoldedWindowHeight : Mathf.Max(windowRect.height, DefaultWindowHeight);
                windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "JuiceControl " + Version, windowFolded ? ui.FoldedWindowStyle : ui.WindowStyle);
                windowRect = ClipWindowPosition(windowRect, !windowFolded);

                if (showNameDialog) {
                    nameDialogRect = GUI.ModalWindow(NameDialogWindowId, nameDialogRect, DrawNameDialog, "JuiceControl " + Version, ui.WindowStyle);
                    nameDialogRect = ClipPopupWindowPosition(nameDialogRect);
                }

                ui.DrawComboBox();
            } finally {
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
                GUI.enabled = oldEnabled;
            }

            ui.HandleMouseInput(windowRect, showNameDialog, nameDialogRect, ref mouseDownOnWindow);
        }

        private void InitStyles() {
            if (ui != null) return;

            ui = new JuiceGui(ComboWindowId);
        }

        private void DrawWindow(int windowId) {
            float buttonSize = ui.WindowStyle.fontSize * 1.4f;
            float buttonY = 0f;
            float closeX = windowRect.width - buttonSize;
            float foldX = closeX - buttonSize;
            if (GUI.Button(new Rect(foldX, buttonY, buttonSize, buttonSize), windowFolded ? "+" : "-", ui.ButtonStyle)) {
                windowFolded = !windowFolded;
                ui.Combo.Hide();
                if (!windowFolded) {
                    windowRect.height = DefaultWindowHeight;
                }
            }
            if (GUI.Button(new Rect(closeX, buttonY, buttonSize, buttonSize), "×", ui.ButtonStyle)) {
                ui.Combo.Hide();
                showWindow = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, foldX), FoldedWindowHeight));

            if (windowFolded) {
                return;
            }

            GUILayout.Space(18f);
            List<CharacterEntry> characters = GetCharacters();
            EnsureSelectedCharacter(characters);

            DrawCharacterSelector(characters);
            DrawSeparator();
            DrawEffectList();
            DrawJuiceControlBar();
            DrawSeparator();
            DrawPresetPanel(characters);
            DrawSeparator();
            DrawStatusBar();

            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, foldX), FoldedWindowHeight));
        }

        private void DrawCharacterSelector(List<CharacterEntry> characters) {
            GUILayout.BeginHorizontal();
            GUILayout.Label("角色:", ui.LabelStyle, GUILayout.Width(42f));
            GUI.enabled = characters.Count > 0;
            if (GUILayout.Button("<", ui.ButtonStyle, GUILayout.Width(28f))) SelectCharacter(characters, GetSelectedCharacterIndex(characters) - 1);

            bool clicked = GUILayout.Button(GetSelectedCharacterDisplayName(characters), ui.ButtonStyle, GUILayout.MinWidth(230f));
            Rect buttonRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint && buttonRect.width > 1f) characterDropdownAnchor = buttonRect;
            if (clicked) OpenCharacterDropdown(characterDropdownAnchor, characters);

            if (GUILayout.Button(">", ui.ButtonStyle, GUILayout.Width(28f))) SelectCharacter(characters, GetSelectedCharacterIndex(characters) + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void OpenCharacterDropdown(Rect buttonRect, List<CharacterEntry> characters) {
            if (ui.Combo == null || characters.Count == 0) return;

            string[] items = new string[characters.Count];
            for (int i = 0; i < characters.Count; i++) {
                items[i] = characters[i].DisplayName;
            }

            ui.Combo.Set(windowRect, buttonRect, items, delegate(int index) {
                if (index < 0 || index >= characters.Count) return;
                selectedCharacter = characters[index].Character;
                statusText = "已选择角色: " + characters[index].DisplayName;
            });
        }

        private void DrawEffectList() {
            GUILayout.BeginVertical();
            if (selectedCharacter == null) {
                GUILayout.Label("当前场景中没有找到角色。", ui.LabelStyle);
                GUILayout.EndVertical();
                return;
            }

            effectScrollPosition = GUILayout.BeginScrollView(effectScrollPosition, GUILayout.Height(Mathf.Max(160f, windowRect.height - 430f)));

            for (int i = 0; i < SiruParts.Length; i++) {
                SiruPartInfo partInfo = SiruParts[i];
                string id = "siru:" + partInfo.Part;
                DrawEffectItem(partInfo.DisplayName, id, selectedCharacter.GetSiruFlags(partInfo.Part), SiruMaxLevel, delegate(int level) {
                    SetSiruPart(selectedCharacter, partInfo.Part, (byte)level);
                }, null);
            }

            DrawEffectItem("泪水", "tear", selectedCharacter.tearsLv, TearMaxLevel, delegate(int level) {
                SetTearLevel(selectedCharacter, (byte)level);
            }, null);

            DrawEffectItem("脸红", "blush", GetBlushLevel(selectedCharacter), BlushMaxLevel, delegate(int level) {
                SetBlushLevel(selectedCharacter, level);
            }, FormatBlushLevel);

            if (SkinEffectsBridge.IsAvailable) {
                for (int i = 0; i < SkinEffectsBridge.Effects.Count; i++) {
                    SkinEffectInfo effect = SkinEffectsBridge.Effects[i];
                    SkinEffectInfo capturedEffect = effect;
                    DrawEffectItem(SkinEffectsBridge.DisplayPrefix + " " + effect.DisplayName, SkinEffectsBridge.GetEffectId(effect), SkinEffectsBridge.GetLevel(selectedCharacter, effect), effect.MaxLevel, delegate(int level) {
                        SkinEffectsBridge.SetLevel(selectedCharacter, capturedEffect, level);
                    }, null);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawEffectItem(string displayName, string id, int level, int maxLevel, Action<int> setLevel, Func<int, string> formatter) {
            bool locked = IsEffectLocked(selectedCharacter, id);
            int clampedMax = Mathf.Max(0, maxLevel);
            int currentLevel = Mathf.Clamp(level, 0, clampedMax);

            GUILayout.BeginHorizontal(GUILayout.Height(EffectRowHeight));
            GUILayout.Label(displayName, ui.LabelStyle, GUILayout.Width(120f), GUILayout.Height(EffectRowHeight));

            int nextLevel = currentLevel;
            if (GUILayout.Button("−", ui.ButtonStyle, GUILayout.Width(32f), GUILayout.Height(EffectRowHeight)))
                nextLevel = Mathf.Max(0, currentLevel - 1);
            string text = formatter == null ? currentLevel.ToString() : formatter(currentLevel);
            if (GUILayout.Button(text, ui.ButtonStyle, GUILayout.MinWidth(78f), GUILayout.ExpandWidth(true), GUILayout.Height(EffectRowHeight)))
                nextLevel = clampedMax == 0 ? 0 : (currentLevel + 1) % (clampedMax + 1);
            if (GUILayout.Button("+", ui.ButtonStyle, GUILayout.Width(32f), GUILayout.Height(EffectRowHeight)))
                nextLevel = Mathf.Min(clampedMax, currentLevel + 1);

            bool newLocked = GUILayout.Toggle(locked, GUIContent.none, GUILayout.Width(24f), GUILayout.Height(EffectRowHeight));
            if (nextLevel != currentLevel) {
                setLevel(nextLevel);
                UpdateLockedEffectValue(selectedCharacter, id, nextLevel);
            }
            if (newLocked != locked) {
                SetEffectLocked(selectedCharacter, id, newLocked, nextLevel);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawJuiceControlBar() {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUI.enabled = selectedCharacter != null;
            if (GUILayout.Button("获取当前状态", ui.ButtonStyle, GUILayout.Height(EffectRowHeight), GUILayout.ExpandWidth(true))) {
                RefreshCurrentState(selectedCharacter);
                statusText = "已获取当前汁液状态。";
            }

            bool allLocked = selectedCharacter != null && AreAllEffectsLocked(selectedCharacter);
            bool newAllLocked = GUILayout.Toggle(allLocked, "固定全部汁液", ui.ToggleStyle, GUILayout.Height(EffectRowHeight), GUILayout.ExpandWidth(true));
            if (newAllLocked != allLocked) {
                if (newAllLocked) {
                    CaptureAllEffectLocks(selectedCharacter);
                    statusText = "已锁定全部汁液。";
                } else {
                    ClearLockedEffects(selectedCharacter);
                    statusText = "已解除全部汁液锁定。";
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = selectedCharacter != null;
            if (GUILayout.Button("清空汁液", ui.ButtonStyle, GUILayout.Height(EffectRowHeight))) {
                ApplyState(selectedCharacter, new CharaSkinState());
                ClearLockedEffects(selectedCharacter);
                statusText = "已清空汁液。";
            }

            if (GUILayout.Button("全满汁液", ui.ButtonStyle, GUILayout.Height(EffectRowHeight))) {
                ApplyState(selectedCharacter, CharaSkinState.CreateMax(SiruMaxLevel, TearMaxLevel, BlushMaxLevel, SkinEffectsBridge.Effects));
                RefreshCurrentState(selectedCharacter);
                statusText = "已设置为全满汁液。";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawPresetPanel(List<CharacterEntry> characters) {
            if (!presetStore.CanSave) {
                GUILayout.Label("当前 XML 载入失败：仍可应用已载入预设；修复并重载后才能保存。", ui.LabelStyle);
            }
            DrawCharacterPresetSelector();
            DrawCharacterPresetToolbar();
            DrawGroupPresetSelector();
            DrawGroupPresetToolbar(characters);
            DrawXmlToolbar();
        }

        private void DrawCharacterPresetSelector() {
            GUILayout.BeginHorizontal();
            GUILayout.Label("汁液预设:", ui.LabelStyle, GUILayout.Width(70f));
            GUI.enabled = HasCharacterPreset();
            if (GUILayout.Button("<", ui.ButtonStyle, GUILayout.Width(28f))) SelectCharacterPreset(selectedCharacterPresetIndex - 1);
            bool clicked = GUILayout.Button(GetCurrentCharacterPresetName(), ui.ButtonStyle);
            Rect buttonRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint && buttonRect.width > 1f) characterPresetDropdownAnchor = buttonRect;
            if (clicked) OpenCharacterPresetDropdown(characterPresetDropdownAnchor);
            if (GUILayout.Button(">", ui.ButtonStyle, GUILayout.Width(28f))) SelectCharacterPreset(selectedCharacterPresetIndex + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void OpenCharacterPresetDropdown(Rect buttonRect) {
            if (ui.Combo == null || presetXml == null || presetXml.CharacterPresets.Count == 0) return;

            string[] items = new string[presetXml.CharacterPresets.Count];
            for (int i = 0; i < presetXml.CharacterPresets.Count; i++) {
                items[i] = presetXml.CharacterPresets[i].Name;
            }

            ui.Combo.Set(windowRect, buttonRect, items, delegate(int index) {
                if (index < 0 || index >= presetXml.CharacterPresets.Count) return;
                selectedCharacterPresetIndex = index;
                characterPresetName = presetXml.CharacterPresets[index].Name;
                statusText = "已选择汁液预设: " + characterPresetName;
            });
        }

        private void DrawCharacterPresetToolbar() {
            GUILayout.BeginHorizontal();
            GUI.enabled = selectedCharacter != null && HasCharacterPreset();
            if (GUILayout.Button("应用选中", ui.ButtonStyle, GUILayout.Height(22f))) ApplyCurrentCharacterPresetToSelected();
            GUI.enabled = presetStore.CanSave && selectedCharacter != null;
            if (GUILayout.Button("新建保存", ui.ButtonStyle, GUILayout.Height(22f))) OpenNameDialog(NameDialogMode.CreateCharacter, string.Empty);
            GUI.enabled = presetStore.CanSave && selectedCharacter != null && HasCharacterPreset();
            if (GUILayout.Button("覆盖保存", ui.ButtonStyle, GUILayout.Height(22f))) OpenNameDialog(NameDialogMode.OverwriteCharacter, GetCurrentCharacterPresetName());
            GUI.enabled = presetStore.CanSave && HasCharacterPreset();
            if (GUILayout.Button("删除选中", ui.ButtonStyle, GUILayout.Height(22f))) DeleteCurrentCharacterPreset();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawGroupPresetSelector() {
            GUILayout.BeginHorizontal();
            GUILayout.Label("场景预设:", ui.LabelStyle, GUILayout.Width(70f));
            GUI.enabled = HasGroupPreset();
            if (GUILayout.Button("<", ui.ButtonStyle, GUILayout.Width(28f))) SelectGroupPreset(selectedGroupPresetIndex - 1);
            bool clicked = GUILayout.Button(GetCurrentGroupPresetName(), ui.ButtonStyle);
            Rect buttonRect = GUILayoutUtility.GetLastRect();
            if (Event.current != null && Event.current.type == EventType.Repaint && buttonRect.width > 1f) groupPresetDropdownAnchor = buttonRect;
            if (clicked) OpenGroupPresetDropdown(groupPresetDropdownAnchor);
            if (GUILayout.Button(">", ui.ButtonStyle, GUILayout.Width(28f))) SelectGroupPreset(selectedGroupPresetIndex + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void OpenGroupPresetDropdown(Rect buttonRect) {
            if (ui.Combo == null || presetXml == null || presetXml.GroupPresets.Count == 0) return;

            string[] items = new string[presetXml.GroupPresets.Count];
            for (int i = 0; i < presetXml.GroupPresets.Count; i++) {
                items[i] = presetXml.GroupPresets[i].Name;
            }

            ui.Combo.Set(windowRect, buttonRect, items, delegate(int index) {
                if (index < 0 || index >= presetXml.GroupPresets.Count) return;
                selectedGroupPresetIndex = index;
                groupPresetName = presetXml.GroupPresets[index].Name;
                statusText = "已选择场景预设: " + groupPresetName;
            });
        }

        private void DrawGroupPresetToolbar(List<CharacterEntry> characters) {
            GUILayout.BeginHorizontal();
            GUI.enabled = characters.Count > 0 && HasGroupPreset();
            if (GUILayout.Button("应用选中", ui.ButtonStyle, GUILayout.Height(22f))) ApplyCurrentGroupPreset(characters);
            GUI.enabled = presetStore.CanSave && characters.Count > 0;
            if (GUILayout.Button("新建保存", ui.ButtonStyle, GUILayout.Height(22f))) OpenNameDialog(NameDialogMode.CreateGroup, string.Empty);
            GUI.enabled = presetStore.CanSave && characters.Count > 0 && HasGroupPreset();
            if (GUILayout.Button("覆盖保存", ui.ButtonStyle, GUILayout.Height(22f))) OpenNameDialog(NameDialogMode.OverwriteGroup, GetCurrentGroupPresetName());
            GUI.enabled = presetStore.CanSave && HasGroupPreset();
            if (GUILayout.Button("删除选中", ui.ButtonStyle, GUILayout.Height(22f))) DeleteCurrentGroupPreset();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawXmlToolbar() {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("打开XML", ui.ButtonStyle, GUILayout.Height(22f))) OpenPresetXml();
            if (GUILayout.Button("重载XML", ui.ButtonStyle, GUILayout.Height(22f))) {
                if (LoadPresets()) statusText = "已重载预设 XML。";
                RefreshIntegrations(true);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSeparator() {
            GUILayout.Space(4f);
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, ui.LineStyle);
            GUILayout.Space(4f);
        }

        private void DrawStatusBar() {
            GUILayout.Label(string.IsNullOrEmpty(statusText) ? "就绪。" : statusText, ui.LabelStyle, GUILayout.Height(20f));
        }

        private void DrawNameDialog(int windowId) {
            GUI.contentColor = Color.black;

            Rect rect = new Rect(8f, 28f, nameDialogRect.width - 16f, 22f);
            GUI.Label(rect, GetNameDialogPrompt(), ui.LabelStyle);

            rect.y += 24f;
            GUI.SetNextControlName("JuiceControlNameInput");
            nameDialogText = GUI.TextField(rect, nameDialogText, ui.TextFieldStyle);
            if (focusNameDialogInput) {
                GUI.FocusControl("JuiceControlNameInput");
                focusNameDialogInput = false;
            }

            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Return &&
                GUI.GetNameOfFocusedControl() == "JuiceControlNameInput") {
                ConfirmNameDialog();
                Event.current.Use();
            }

            rect.y += 24f;
            GUI.Label(rect, nameDialogMessage, ui.LabelStyle);

            rect.y += 24f;
            rect.width = (nameDialogRect.width - 20f) / 2f;
            if (GUI.Button(rect, "确定", ui.ButtonStyle)) {
                ConfirmNameDialog();
            }

            rect.x += rect.width + 4f;
            if (GUI.Button(rect, "取消", ui.ButtonStyle)) {
                CloseNameDialog();
            }

            ui.DragNamedDialog(nameDialogRect);
        }

        private void OpenNameDialog(NameDialogMode mode, string defaultName) {
            if (!CanEditPresets()) return;
            nameDialogMode = mode;
            nameDialogPreset = mode == NameDialogMode.OverwriteCharacter ? (object)GetCurrentCharacterPreset()
                : mode == NameDialogMode.OverwriteGroup ? GetCurrentGroupPreset() : null;
            nameDialogCharacter = selectedCharacter;
            nameDialogText = defaultName ?? string.Empty;
            nameDialogMessage = string.Empty;
            showNameDialog = true;
            focusNameDialogInput = true;
            if (ui.Combo != null) ui.Combo.Hide();

            nameDialogRect.width = NameDialogWidth;
            nameDialogRect.height = NameDialogHeight;
            nameDialogRect = MoveScreenCenter(nameDialogRect);
        }

        private void CloseNameDialog() {
            showNameDialog = false;
            nameDialogMode = NameDialogMode.None;
            nameDialogPreset = null;
            nameDialogCharacter = null;
            nameDialogMessage = string.Empty;
            GUI.FocusControl(null);
        }

        private void ConfirmNameDialog() {
            if (!CanEditPresets()) return;
            // The modal normally prevents selection changes, but scene changes or
            // external callbacks can still replace the character/preset underneath it.
            bool characterMode = nameDialogMode == NameDialogMode.CreateCharacter || nameDialogMode == NameDialogMode.OverwriteCharacter;
            bool changed = characterMode && (nameDialogCharacter == null || nameDialogCharacter != selectedCharacter);
            changed |= nameDialogMode == NameDialogMode.OverwriteCharacter && !ReferenceEquals(nameDialogPreset, GetCurrentCharacterPreset());
            changed |= nameDialogMode == NameDialogMode.OverwriteGroup && !ReferenceEquals(nameDialogPreset, GetCurrentGroupPreset());
            if (changed) {
                nameDialogMessage = statusText = "角色或预设选择已变化，请取消后重新打开保存窗口。";
                return;
            }
            bool saved = false;
            switch (nameDialogMode) {
                case NameDialogMode.CreateCharacter:
                    saved = CreateCharacterPresetFromSelected(nameDialogText);
                    break;
                case NameDialogMode.OverwriteCharacter:
                    saved = OverwriteCurrentCharacterPreset(nameDialogText);
                    break;
                case NameDialogMode.CreateGroup:
                    saved = CreateGroupPresetFromScene(GetCharacters(), nameDialogText);
                    break;
                case NameDialogMode.OverwriteGroup:
                    saved = OverwriteCurrentGroupPreset(GetCharacters(), nameDialogText);
                    break;
            }

            if (saved) CloseNameDialog();
        }

        private string GetNameDialogPrompt() {
            switch (nameDialogMode) {
                case NameDialogMode.CreateCharacter: return "请输入汁液预设名";
                case NameDialogMode.OverwriteCharacter: return "要覆盖（并重命名）吗？";
                case NameDialogMode.CreateGroup: return "请输入场景预设名";
                case NameDialogMode.OverwriteGroup: return "要覆盖（并重命名）吗？";
                default: return string.Empty;
            }
        }

        private int GetSelectedCharacterIndex(List<CharacterEntry> characters) {
            for (int i = 0; i < characters.Count; i++) {
                if (characters[i].Character == selectedCharacter) return i;
            }

            return characters.Count > 0 ? 0 : -1;
        }

        private string GetSelectedCharacterDisplayName(List<CharacterEntry> characters) {
            int index = GetSelectedCharacterIndex(characters);
            return index >= 0 && index < characters.Count ? characters[index].DisplayName : "无";
        }

        private void SelectCharacter(List<CharacterEntry> characters, int index) {
            if (characters.Count == 0) return;

            if (index < 0) index = characters.Count - 1;
            if (index >= characters.Count) index = 0;
            selectedCharacter = characters[index].Character;
            statusText = "已选择角色: " + characters[index].DisplayName;
        }

        private bool CreateCharacterPresetFromSelected(string requestedName) {
            if (!CanEditPresets()) return false;
            if (selectedCharacter == null) return false;

            string name = NormalizePresetName(requestedName, BuildUniqueCharacterPresetName());
            if (presetXml.FindCharacterPreset(name) != null) {
                nameDialogMessage = "名称已存在。";
                statusText = "汁液预设名称已存在: " + name;
                return false;
            }

            CharacterPreset preset = new CharacterPreset {
                Name = name,
                State = CaptureState(selectedCharacter)
            };
            presetXml.CharacterPresets.Add(preset);
            selectedCharacterPresetIndex = presetXml.CharacterPresets.Count - 1;
            characterPresetName = name;
            presetStore.Save();
            statusText = "已新建汁液预设: " + name;
            return true;
        }

        private bool OverwriteCurrentCharacterPreset(string requestedName) {
            if (!CanEditPresets()) return false;
            CharacterPreset preset = GetCurrentCharacterPreset();
            if (selectedCharacter == null || preset == null) return false;

            string name = NormalizePresetName(requestedName, preset.Name);
            CharacterPreset existing = presetXml.FindCharacterPreset(name);
            if (existing != null && !ReferenceEquals(existing, preset)) {
                nameDialogMessage = "名称已存在。";
                statusText = "汁液预设名称已存在: " + name;
                return false;
            }

            preset.Name = name;
            preset.State = CaptureState(selectedCharacter);
            characterPresetName = name;
            presetStore.Save();
            statusText = "已覆盖汁液预设: " + preset.Name;
            return true;
        }

        private bool CreateGroupPresetFromScene(List<CharacterEntry> characters, string requestedName) {
            if (!CanEditPresets()) return false;
            if (characters.Count == 0) return false;

            string name = NormalizePresetName(requestedName, BuildUniqueGroupPresetName());
            if (presetXml.FindGroupPreset(name) != null) {
                nameDialogMessage = "名称已存在。";
                statusText = "场景预设名称已存在: " + name;
                return false;
            }

            GroupPreset preset = new GroupPreset { Name = name };
            presetXml.GroupPresets.Add(preset);
            selectedGroupPresetIndex = presetXml.GroupPresets.Count - 1;
            groupPresetName = name;
            SaveGroupPresetData(preset, characters);
            statusText = "已新建场景预设: " + name;
            return true;
        }

        private bool OverwriteCurrentGroupPreset(List<CharacterEntry> characters, string requestedName) {
            if (!CanEditPresets()) return false;
            GroupPreset preset = GetCurrentGroupPreset();
            if (characters.Count == 0 || preset == null) return false;

            string name = NormalizePresetName(requestedName, preset.Name);
            GroupPreset existing = presetXml.FindGroupPreset(name);
            if (existing != null && !ReferenceEquals(existing, preset)) {
                nameDialogMessage = "名称已存在。";
                statusText = "场景预设名称已存在: " + name;
                return false;
            }

            preset.Name = name;
            groupPresetName = name;
            SaveGroupPresetData(preset, characters);
            statusText = "已覆盖场景预设: " + preset.Name;
            return true;
        }

        private void SaveGroupPresetData(GroupPreset preset, List<CharacterEntry> characters) {
            preset.Characters.Clear();
            for (int i = 0; i < characters.Count; i++) {
                CharacterEntry entry = characters[i];
                preset.Characters.Add(new RolePreset {
                    Role = entry.Role,
                    DisplayName = entry.Name,
                    State = CaptureState(entry.Character)
                });
            }

            presetStore.Save();
        }

        private string BuildUniqueCharacterPresetName() {
            int index = presetXml.CharacterPresets.Count + 1;
            string name;
            do {
                name = "汁液预设 " + index++;
            } while (presetXml.FindCharacterPreset(name) != null);

            return name;
        }

        private string BuildUniqueGroupPresetName() {
            int index = presetXml.GroupPresets.Count + 1;
            string name;
            do {
                name = "场景预设 " + index++;
            } while (presetXml.FindGroupPreset(name) != null);

            return name;
        }

        private void OpenPresetXml() {
            try {
                // Opening a file must not overwrite external edits or a malformed
                // file with an empty in-memory fallback.
                string path = ResolvePresetPath(PresetFileName == null ? DefaultPresetFileName : PresetFileName.Value);
                Process.Start(path);
                statusText = "已打开 XML: " + path;
            } catch (Exception ex) {
                statusText = "打开 XML 失败: " + ex.Message;
                LogDebug(ex);
            }
        }

        private void RefreshIntegrations(bool force) {
            SkinEffectsBridge.Refresh(force);
            UpdateSkinEffectsStatusConfig();
        }

        private void UpdateSkinEffectsStatusConfig() {
            if (SkinEffectsIntegrationStatus == null) return;

            string status = SkinEffectsBridge.StatusLabel;
            if (SkinEffectsIntegrationStatus.Value != status) {
                SkinEffectsIntegrationStatus.Value = status;
            }
        }

        private bool IsEffectLocked(ChaControl cha, string id) {
            Dictionary<string, int> levels;
            return cha != null && lockedEffectLevels.TryGetValue(cha, out levels) && levels.ContainsKey(id);
        }

        private void SetEffectLocked(ChaControl cha, string id, bool locked, int level) {
            if (cha == null) return;

            Dictionary<string, int> levels = GetLockedLevelMap(cha);
            if (locked) {
                levels[id] = level;
                statusText = "已锁定当前值。";
            } else {
                levels.Remove(id);
                statusText = "已解除锁定。";
            }
        }

        private void UpdateLockedEffectValue(ChaControl cha, string id, int level) {
            Dictionary<string, int> levels;
            if (cha != null && lockedEffectLevels.TryGetValue(cha, out levels) && levels.ContainsKey(id)) {
                levels[id] = level;
            }
        }

        private void ClearLockedEffects(ChaControl cha) {
            if (cha != null) {
                lockedEffectLevels.Remove(cha);
            }
        }

        private Dictionary<string, int> GetLockedLevelMap(ChaControl cha) {
            Dictionary<string, int> levels;
            if (!lockedEffectLevels.TryGetValue(cha, out levels)) {
                levels = new Dictionary<string, int>();
                lockedEffectLevels.Add(cha, levels);
            }

            return levels;
        }

        private void RefreshCurrentState(ChaControl cha) {
            if (cha == null) return;

            Dictionary<string, int> levels;
            if (!lockedEffectLevels.TryGetValue(cha, out levels)) return;

            List<string> ids = new List<string>(levels.Keys);
            for (int i = 0; i < ids.Count; i++) {
                levels[ids[i]] = GetEffectValue(cha, ids[i]);
            }
        }

        private bool AreAllEffectsLocked(ChaControl cha) {
            if (cha == null) return false;

            List<string> ids = GetAllEffectIds();
            if (ids.Count == 0) return false;

            for (int i = 0; i < ids.Count; i++) {
                if (!IsEffectLocked(cha, ids[i])) return false;
            }

            return true;
        }

        private void CaptureAllEffectLocks(ChaControl cha) {
            if (cha == null) return;

            Dictionary<string, int> levels = GetLockedLevelMap(cha);
            levels.Clear();
            List<string> ids = GetAllEffectIds();
            for (int i = 0; i < ids.Count; i++) {
                string id = ids[i];
                levels[id] = GetEffectValue(cha, id);
            }
        }

        private List<string> GetAllEffectIds() {
            List<string> ids = new List<string>();
            for (int i = 0; i < SiruParts.Length; i++) {
                ids.Add("siru:" + SiruParts[i].Part);
            }

            ids.Add("tear");
            ids.Add("blush");
            if (SkinEffectsBridge.IsAvailable) {
                for (int i = 0; i < SkinEffectsBridge.Effects.Count; i++) {
                    ids.Add(SkinEffectsBridge.GetEffectId(SkinEffectsBridge.Effects[i]));
                }
            }

            return ids;
        }

        private void ApplyLockedEffects() {
            List<ChaControl> staleCharacters = null;
            foreach (KeyValuePair<ChaControl, Dictionary<string, int>> pair in lockedEffectLevels) {
                ChaControl cha = pair.Key;
                if (cha == null) {
                    if (staleCharacters == null) staleCharacters = new List<ChaControl>();
                    staleCharacters.Add(cha);
                    continue;
                }

                foreach (KeyValuePair<string, int> level in pair.Value) {
                    SetEffectValue(cha, level.Key, level.Value);
                }
            }

            if (staleCharacters != null) {
                for (int i = 0; i < staleCharacters.Count; i++) {
                    lockedEffectLevels.Remove(staleCharacters[i]);
                }
            }
        }

        private int GetEffectValue(ChaControl cha, string id) {
            if (cha == null) return 0;

            if (id == "tear") return cha.tearsLv;
            if (id == "blush") return GetBlushLevel(cha);
            if (id.StartsWith("siru:", StringComparison.Ordinal)) {
                ChaFileDefine.SiruParts part = (ChaFileDefine.SiruParts)Enum.Parse(typeof(ChaFileDefine.SiruParts), id.Substring(5));
                return cha.GetSiruFlags(part);
            }
            string skinEffectName;
            if (SkinEffectsBridge.TryGetEffectName(id, out skinEffectName)) {
                SkinEffectInfo effect = SkinEffectsBridge.FindEffect(skinEffectName);
                return effect == null ? 0 : SkinEffectsBridge.GetLevel(cha, effect);
            }

            return 0;
        }

        private void SetEffectValue(ChaControl cha, string id, int level) {
            if (cha == null) return;

            if (id == "tear") {
                SetTearLevel(cha, (byte)level);
                return;
            }
            if (id == "blush") {
                SetBlushLevel(cha, level);
                return;
            }
            if (id.StartsWith("siru:", StringComparison.Ordinal)) {
                ChaFileDefine.SiruParts part = (ChaFileDefine.SiruParts)Enum.Parse(typeof(ChaFileDefine.SiruParts), id.Substring(5));
                SetSiruPart(cha, part, (byte)level);
                return;
            }
            string skinEffectName;
            if (SkinEffectsBridge.TryGetEffectName(id, out skinEffectName)) {
                SkinEffectInfo effect = SkinEffectsBridge.FindEffect(skinEffectName);
                if (effect != null) SkinEffectsBridge.SetLevel(cha, effect, level);
            }
        }

        private List<CharacterEntry> GetCharacters() {
            if (cachedCharacters != null && characterScanFrame == Time.frameCount) return cachedCharacters;
            List<CharacterEntry> entries = new List<CharacterEntry>();
            HSceneProc hSceneProc = UnityEngine.Object.FindObjectOfType<HSceneProc>();

            if (hSceneProc != null) {
                AddCharacter(entries, hSceneProc.male, "男1");
                AddCharacter(entries, GetMale1(hSceneProc), "男2");

                if (hSceneProc.lstFemale != null) {
                    for (int i = 0; i < hSceneProc.lstFemale.Count; i++) {
                        AddCharacter(entries, hSceneProc.lstFemale[i], "女" + (i + 1));
                    }
                }
            }

            ChaControl[] allCharacters = UnityEngine.Object.FindObjectsOfType<ChaControl>();
            for (int i = 0; i < allCharacters.Length; i++) {
                AddCharacter(entries, allCharacters[i], "角色" + (entries.Count + 1));
            }

            cachedCharacters = entries;
            characterScanFrame = Time.frameCount;
            return entries;
        }

        private static ChaControl GetMale1(HSceneProc hSceneProc) {
            return Male1Field == null ? null : Male1Field.GetValue(hSceneProc) as ChaControl;
        }

        private static void AddCharacter(List<CharacterEntry> entries, ChaControl cha, string role) {
            if (cha == null || ContainsCharacter(entries, cha)) return;
            entries.Add(new CharacterEntry(cha, role, GetCharacterName(cha)));
        }

        private static bool ContainsCharacter(List<CharacterEntry> entries, ChaControl cha) {
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i].Character == cha) return true;
            }

            return false;
        }

        private static string GetCharacterName(ChaControl cha) {
            try {
                if (cha.fileParam != null && !string.IsNullOrEmpty(cha.fileParam.fullname)) {
                    return cha.fileParam.fullname;
                }
            } catch {
                // Some transient ChaControl instances can be half-initialized while scenes load.
            }

            return cha.name;
        }

        private void EnsureSelectedCharacter(List<CharacterEntry> characters) {
            if (selectedCharacter != null && ContainsCharacter(characters, selectedCharacter)) return;

            selectedCharacter = characters.Count > 0 ? characters[0].Character : null;
            if (selectedCharacter != null && string.IsNullOrEmpty(characterPresetName)) {
                characterPresetName = BuildDefaultCharacterPresetName(characters[0]);
            }
        }

        private string GetSelectedCharacterLabel() {
            if (selectedCharacter == null) return "无";

            try {
                return GetCharacterName(selectedCharacter);
            } catch {
                return selectedCharacter.name;
            }
        }

        private static string BuildDefaultCharacterPresetName(CharacterEntry entry) {
            return entry == null ? "Preset" : entry.Name + " Skin";
        }

        private static Rect ClipWindowPosition(Rect rect, bool keepFullyVisible) {
            float maxWidth = Mathf.Max(320f, Screen.width - 20f);
            float minWidth = Mathf.Min(MinWindowWidth, maxWidth);
            float maxHeight = Mathf.Max(FoldedWindowHeight, Screen.height - 20f);
            float minHeight = keepFullyVisible ? Mathf.Min(MinWindowHeight, maxHeight) : FoldedWindowHeight;
            rect.width = Mathf.Clamp(rect.width, minWidth, maxWidth);
            rect.height = Mathf.Clamp(rect.height, minHeight, maxHeight);
            if (keepFullyVisible) {
                rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
                rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            } else {
                rect.x = Mathf.Clamp(rect.x, -rect.width * 0.9f, Screen.width - rect.width * 0.1f);
                rect.y = Mathf.Clamp(rect.y, -rect.height * 0.9f, Screen.height - rect.height * 0.1f);
            }

            return rect;
        }

        private static Rect MoveScreenCenter(Rect rect) {
            rect.x = Mathf.Max(0f, (Screen.width - rect.width) / 2f);
            rect.y = Mathf.Max(0f, (Screen.height - rect.height) / 2f);
            return rect;
        }

        private static Rect ClipPopupWindowPosition(Rect rect) {
            rect.width = Mathf.Min(rect.width, Mathf.Max(120f, Screen.width - 20f));
            rect.height = Mathf.Min(rect.height, Mathf.Max(FoldedWindowHeight, Screen.height - 20f));
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        private void SetSiruPart(ChaControl cha, ChaFileDefine.SiruParts part, byte level) {
            if (cha == null) return;

            level = (byte)Mathf.Clamp(level, 0, SiruMaxLevel);
            if (cha.GetSiruFlags(part) == level) return;

            cha.SetSiruFlags(part, level);
            cha.UpdateSiru(true);
        }

        private static void SetTearLevel(ChaControl cha, byte level) {
            if (cha == null) return;
            cha.tearsLv = (byte)Mathf.Clamp(level, 0, TearMaxLevel);
        }

        private static int GetBlushLevel(ChaControl cha) {
            if (cha == null || cha.fileStatus == null) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(cha.fileStatus.hohoAkaRate * BlushMaxLevel), 0, BlushMaxLevel);
        }

        private static void SetBlushLevel(ChaControl cha, int level) {
            if (cha == null) return;
            cha.ChangeHohoAkaRate(Mathf.Clamp01(level / (float)BlushMaxLevel));
        }

        private static string FormatBlushLevel(int level) {
            return Mathf.RoundToInt(level * 100f / BlushMaxLevel) + "%";
        }

        private void ApplyState(ChaControl cha, CharaSkinState state) {
            if (cha == null || state == null) return;

            bool siruChanged = false;
            for (int i = 0; i < SiruParts.Length; i++) {
                byte level = (byte)Mathf.Clamp(state.GetSiruLevel(SiruParts[i].Part), 0, SiruMaxLevel);
                if (cha.GetSiruFlags(SiruParts[i].Part) == level) continue;

                cha.SetSiruFlags(SiruParts[i].Part, level);
                siruChanged = true;
            }

            if (siruChanged) cha.UpdateSiru(true);
            SetTearLevel(cha, state.TearLevel);
            SetBlushLevel(cha, state.BlushLevel);

            if (SkinEffectsBridge.IsAvailable) {
                for (int i = 0; i < SkinEffectsBridge.Effects.Count; i++) {
                    SkinEffectInfo effect = SkinEffectsBridge.Effects[i];
                    SkinEffectsBridge.SetLevel(cha, effect, state.GetSkinEffectLevel(effect.Name));
                }
            }
            // A manual preset change should also become the value kept by active
            // locks, rather than being undone by the next LateUpdate.
            RefreshCurrentState(cha);
        }

        private CharaSkinState CaptureState(ChaControl cha) {
            CharaSkinState state = new CharaSkinState {
                TearLevel = cha == null ? (byte)0 : cha.tearsLv,
                BlushLevel = GetBlushLevel(cha)
            };

            if (cha != null) {
                for (int i = 0; i < SiruParts.Length; i++) {
                    state.SetSiruLevel(SiruParts[i].Part, cha.GetSiruFlags(SiruParts[i].Part));
                }

                if (SkinEffectsBridge.IsAvailable) {
                    for (int i = 0; i < SkinEffectsBridge.Effects.Count; i++) {
                        SkinEffectInfo effect = SkinEffectsBridge.Effects[i];
                        state.SetSkinEffectLevel(effect.Name, SkinEffectsBridge.GetLevel(cha, effect));
                    }
                }
            }

            return state;
        }

        private void ApplyCurrentCharacterPresetToSelected() {
            CharacterPreset preset = GetCurrentCharacterPreset();
            if (selectedCharacter == null || preset == null) return;

            ApplyState(selectedCharacter, preset.State);
            statusText = "已应用角色预设到选中角色: " + preset.Name;
        }

        private void ApplyCurrentGroupPreset(List<CharacterEntry> characters) {
            GroupPreset preset = GetCurrentGroupPreset();
            if (preset == null) return;

            int applied = 0;
            for (int i = 0; i < characters.Count; i++) {
                CharacterEntry entry = characters[i];
                RolePreset rolePreset = preset.FindRole(entry.Role);
                if (rolePreset == null) continue;

                ApplyState(entry.Character, rolePreset.State);
                applied++;
            }

            statusText = "已一键替换全部角色: " + preset.Name + " (" + applied + " 个槽位)";
        }

        private void DeleteCurrentCharacterPreset() {
            if (!CanEditPresets()) return;
            if (!HasCharacterPreset()) return;

            string name = presetXml.CharacterPresets[selectedCharacterPresetIndex].Name;
            presetXml.CharacterPresets.RemoveAt(selectedCharacterPresetIndex);
            selectedCharacterPresetIndex = ClampIndex(selectedCharacterPresetIndex, presetXml.CharacterPresets.Count);
            presetStore.Save();
            characterPresetName = GetCurrentCharacterPresetName();
            if (characterPresetName == "(无角色预设)") characterPresetName = string.Empty;
            statusText = "已删除角色预设: " + name;
        }

        private void DeleteCurrentGroupPreset() {
            if (!CanEditPresets()) return;
            if (!HasGroupPreset()) return;

            string name = presetXml.GroupPresets[selectedGroupPresetIndex].Name;
            presetXml.GroupPresets.RemoveAt(selectedGroupPresetIndex);
            selectedGroupPresetIndex = ClampIndex(selectedGroupPresetIndex, presetXml.GroupPresets.Count);
            presetStore.Save();
            groupPresetName = GetCurrentGroupPresetName();
            if (groupPresetName == "(无群体预设)") groupPresetName = string.Empty;
            statusText = "已删除群体预设: " + name;
        }

        private void SelectCharacterPreset(int index) {
            if (presetXml == null || presetXml.CharacterPresets.Count == 0) return;

            if (index < 0) index = presetXml.CharacterPresets.Count - 1;
            if (index >= presetXml.CharacterPresets.Count) index = 0;
            selectedCharacterPresetIndex = index;
            characterPresetName = presetXml.CharacterPresets[selectedCharacterPresetIndex].Name;
        }

        private void SelectGroupPreset(int index) {
            if (presetXml == null || presetXml.GroupPresets.Count == 0) return;

            if (index < 0) index = presetXml.GroupPresets.Count - 1;
            if (index >= presetXml.GroupPresets.Count) index = 0;
            selectedGroupPresetIndex = index;
            groupPresetName = presetXml.GroupPresets[selectedGroupPresetIndex].Name;
        }

        private bool HasCharacterPreset() {
            return presetXml != null && presetXml.CharacterPresets.Count > 0 && selectedCharacterPresetIndex >= 0 && selectedCharacterPresetIndex < presetXml.CharacterPresets.Count;
        }

        private bool HasGroupPreset() {
            return presetXml != null && presetXml.GroupPresets.Count > 0 && selectedGroupPresetIndex >= 0 && selectedGroupPresetIndex < presetXml.GroupPresets.Count;
        }

        private CharacterPreset GetCurrentCharacterPreset() {
            return HasCharacterPreset() ? presetXml.CharacterPresets[selectedCharacterPresetIndex] : null;
        }

        private GroupPreset GetCurrentGroupPreset() {
            return HasGroupPreset() ? presetXml.GroupPresets[selectedGroupPresetIndex] : null;
        }

        private string GetCurrentCharacterPresetName() {
            CharacterPreset preset = GetCurrentCharacterPreset();
            return preset == null ? "(无角色预设)" : preset.Name;
        }

        private string GetCurrentGroupPresetName() {
            GroupPreset preset = GetCurrentGroupPreset();
            return preset == null ? "(无群体预设)" : preset.Name;
        }

        private bool CanEditPresets() {
            if (presetStore.CanSave) return true;
            nameDialogMessage = statusText = "当前配置的预设 XML 未成功载入，请修复并重载后再保存。";
            return false;
        }

        private bool LoadPresets() {
            presetStore.Invalidate();
            // A dialog opened for the previous file must not save after a path switch.
            showNameDialog = false;
            nameDialogMode = NameDialogMode.None;
            nameDialogPreset = null;
            nameDialogCharacter = null;
            try {
                string path = ResolvePresetPath(PresetFileName == null ? DefaultPresetFileName : PresetFileName.Value);
                presetStore.Load(path);
            } catch (Exception ex) {
                statusText = "载入预设 XML 失败，保留当前预设: " + ex.Message;
                Logger.LogWarning(statusText);
                return false;
            }
            selectedCharacterPresetIndex = ClampIndex(selectedCharacterPresetIndex, presetXml.CharacterPresets.Count);
            selectedGroupPresetIndex = ClampIndex(selectedGroupPresetIndex, presetXml.GroupPresets.Count);
            characterPresetName = HasCharacterPreset() ? GetCurrentCharacterPresetName() : string.Empty;
            groupPresetName = HasGroupPreset() ? GetCurrentGroupPresetName() : string.Empty;
            LogDebug("Loaded presets from " + presetPath);
            return true;
        }

        private static int ClampIndex(int index, int count) {
            if (count <= 0) return 0;
            return Mathf.Clamp(index, 0, count - 1);
        }

        private static string NormalizePresetName(string value, string fallback) {
            string name = string.IsNullOrEmpty(value) ? fallback : value.Trim();
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        private static string ResolvePresetPath(string configValue) {
            string fileName = configValue == null ? string.Empty : configValue.Trim();
            if (fileName.Length == 0) fileName = DefaultPresetFileName;
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(Paths.ConfigPath, fileName);
        }

        internal static void LogDebug(object data) {
            if (Instance != null && DebugLogging != null && DebugLogging.Value) {
                Instance.Logger.LogInfo(data);
            }
        }

        private class SiruPartInfo {
            public readonly ChaFileDefine.SiruParts Part;
            public readonly string DisplayName;

            public SiruPartInfo(ChaFileDefine.SiruParts part, string displayName) {
                Part = part;
                DisplayName = displayName;
            }
        }

        private class CharacterEntry {
            public readonly ChaControl Character;
            public readonly string Role;
            public readonly string Name;
            public readonly string DisplayName;

            public CharacterEntry(ChaControl character, string role, string name) {
                Character = character;
                Role = role;
                Name = string.IsNullOrEmpty(name) ? character.name : name;
                DisplayName = Role + " - " + Name;
            }
        }

        private enum NameDialogMode {
            None,
            CreateCharacter,
            OverwriteCharacter,
            CreateGroup,
            OverwriteGroup
        }
    }

    internal static class JuiceControlGuiUtility {
        static JuiceControlGuiUtility() {
            TexGuiWindowOn = LoadTexture(Base64TexWindowOn);
            TexGuiWindowOff = LoadTexture(Base64TexWindowOff);
            TexGuiWindowFoldOn = LoadTexture(Base64TexWindowFoldOn);
            TexGuiWindowFoldOff = LoadTexture(Base64TexWindowFoldOff);
            TexGuiBackgroundOn = LoadTexture(Base64TexBackgroundOn);
            TexGuiButtonNormal = LoadTexture(Base64TexButtonNormal);
            TexGuiButtonHover = LoadTexture(Base64TexButtonHover);
            TexGuiButtonActive = LoadTexture(Base64TexButtonActive);
            TexGuiInputNormal = LoadTexture(Base64TexInputNormal);
            TexGuiInputActive = LoadTexture(Base64TexInputActive);
            TexBlack = CreateSolidTexture(Color.black);
            TexGray = CreateSolidTexture(new Color(0.8f, 0.8f, 0.8f, 1f));
        }

        public static int GetPix(int value) {
            return (int)((1f + (Screen.width / 1280f - 1f) * 0.6f) * value);
        }

        public static bool GetAnyMouseButtonDown() {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }

        public static bool GetAnyMouseButton() {
            return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
        }

        private static Texture2D LoadTexture(string base64) {
            Texture2D texture = new Texture2D(32, 32);
            texture.LoadImage(Convert.FromBase64String(base64));
            return texture;
        }

        private static Texture2D CreateSolidTexture(Color color) {
            Texture2D texture = new Texture2D(4, 4);
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        internal static readonly Texture2D TexGuiWindowOn;
        internal static readonly Texture2D TexGuiWindowOff;
        internal static readonly Texture2D TexGuiWindowFoldOn;
        internal static readonly Texture2D TexGuiWindowFoldOff;
        internal static readonly Texture2D TexGuiBackgroundOn;
        internal static readonly Texture2D TexGuiButtonNormal;
        internal static readonly Texture2D TexGuiButtonHover;
        internal static readonly Texture2D TexGuiButtonActive;
        internal static readonly Texture2D TexGuiInputNormal;
        internal static readonly Texture2D TexGuiInputActive;
        internal static readonly Texture2D TexBlack;
        internal static readonly Texture2D TexGray;

        private const string Base64TexWindowOn = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAtElEQVR42uyX0QqAIAxFnfjsL6e/7A+YlsayMsvZQ+yCIFT3bBPTgfdelAKAKU2NaFN+zx6eRAAeQdHcvxxT6a9OIt+ids41ha+13mUSArXBa/XMJcLmrcY3oKVcksq8+NbkckGadJtfZSLFYEFafbLoyyyGZ8AABjAA7eRRUvjgIY08nQe8yAxgwF8ABm9tyt/EJ/eirO1G3avaTbsbUjM/QJ6A7nqEKqS3AYEKhKSFmgUYAGTwBp/Tq57RAAAAAElFTkSuQmCC";
        private const string Base64TexWindowOff = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAxklEQVR42uyX0Q3DIAxETcQsLMCAhf1gAZahoYIILqWirdOPynxFinPPJ5STrXLOhCfGeKuPjtbOo85a6/GFQkAVXxU+gRAyAFDcGLOkmlKaQg5AL74q/AJ0QDYucfjWtXtUIQQW8ZmTjS4+xUHm7B5dXO5AAAIQAJGeRC4/YI9XVuE9TeWSBSCAvwK4/tfmjImfzEUF4Dld9N2X+bQ5YIGg+BDXFdIG17ciHJoaxncNtb7fWD5wc1pA9JMij6vRNyvUXYABAFQxZQThBU+WAAAAAElFTkSuQmCC";
        private const string Base64TexWindowFoldOn = "iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAYAAABB7B6eAAAAfUlEQVR42uyT0QqAIAxFnfTsL6u/vB9YWlvYKpJyb14YCOq5dxOBiJwWAEReJtcnOZcvO9WgraIKp48VNX+5SX6kRsSu+CGEUyclaC6snSkjauG94BejbVx+FFzdTTIu4MVv+FMn3hkL+PWHpdddmHcwDabBNGh+spVWAQYAeaxBMZcK1SAAAAAASUVORK5CYII=";
        private const string Base64TexWindowFoldOff = "iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAYAAABB7B6eAAAAeklEQVR42uyUwQnAIAxFo3QWF3DA2v3MAi6TUrHS/mLJId78J0F9Lx9BJyKEYea9LRPpUs/FGA/ccChocC34I0LJS4DwEIKKWkoZSrrgCdeCf0Rd4q3gcDfd7+hyzibwURNPk3M1EMvpscX0BkuwBEtAtA2+XLOcAgwAe189mW2MybwAAAAASUVORK5CYII=";
        private const string Base64TexBackgroundOn = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAjElEQVR42uyXUQrAIAxDm13c7eTdHArDdd2PrT8JFAuCeRSEBqoqowCU1u4yR/2d43VTAZ51qZprUBUXYDSfJRfCMo+SCZFl7kGkmVsQmywWGolY3zHUGLjP5RMgAAEIQAACEIAABNh6aOgbSuY2VL2XTyB9M/ZS0ppg8gWRFs3+IKLDKRyIlHh+CjAAfuTkaxIqAb4AAAAASUVORK5CYII=";
        private const string Base64TexButtonNormal = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAoklEQVR42uyXMQ7DIAxFbZSFlROFk6cnYmV0cIQrRJsRs3xLjoAM72GEkElEyLLF2VIWpzK+TH4+zLp46Y9aK62MGKMNc8uPCchq8IsIh777XXGyHof37scqBNocEIAABCAAgWNecHyOfyvg8SjNjOAJ/8cK3vBZArcAAhCAgArklJI7uDMz97mUUnYIsB3BUwWPSgycTNYmj72aV3tucQswAKPmcSH9pvfQAAAAAElFTkSuQmCC";
        private const string Base64TexButtonHover = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmElEQVR42uyXMQrAIAxFE+mip9L730fHVCVSETsal/8htLXDe/zSISQiNKYm1pHD0xgfcwWfzirC7YyZ9Z1dKrNfXL2JdC+RtfYr9NaCo8uBAAQgAAEIPOtBKeUo0Hv/38Bp+I7hLOE7lrOGrxL4CyAAAQg0gRRCMAcrM7E+S875hgCPT9BbsGhi4iQaa/K8q1mt5yOvAAMAfwPPqY0XyRgAAAAASUVORK5CYII=";
        private const string Base64TexButtonActive = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmUlEQVR42uyXwQqAIAyGl3RoVx/Ioz5z1x7Ia8da4UKM6OQWtMEPMoT/24YMAQCmSpG0dVasPYdyCKSZBDln6Bneez4m0sIAa2/jBxB0pXqtCEcHNunq6y44UA4DMAAD+AYAIoobs6drE5LmtxFIQLQe49sFewUGYAAGYAC/AEiK6zhdXzMKDQDkEZxdkFrHXD3nVL/nuwADAAyFRZEqr0SGAAAAAElFTkSuQmCC";
        private const string Base64TexInputNormal = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkUlEQVR42uyXQQrAIAwEE+nD6svUl9mfpaYYkUKPmkN3QSTksJO9LYsImUopZ/sqrVVs70opPQMrwMuYbblCzUtmkKMPas60R+ajIMw5Z7t+6eUfScTgYa5qfppEDeSs0Gl8AdwTAAAAAAAAAAAAAAAAwO8B4lSXXIrJ5RjA6IYjhd5YdpXT0Y5tv62e23ALMABi1TXAc3c4+wAAAABJRU5ErkJggg==";
        private const string Base64TexInputActive = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkElEQVR42uyXSw7AIAhEofFgev9DUWjxE+OyYhczCQuJybzMiiERoTqqrCObxzy658KYRqivZwZhWzKzLZhc/nGL1KvZ2DPpIkcYzx4OkrmSRJgv0pCLDovfVISPmP8hAQAAAAAAAAAAAAAAAADAAIo3o/CL+PHuhSX2NK91MPm7pbAbZEi7rPpgWD2vugUYAKxOtLWwqwjPAAAAAElFTkSuQmCC";
    }


}
