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
using MaxIllusionPlugins.Common;
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
    public partial class JuiceControl : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.JuiceControl";
        public const string Version = "0.2.1";

        private const string DefaultPresetFileName = GUID + ".presets.xml";
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
        private bool showNameDialog;
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
        private string presetLoadError = string.Empty;
        private int characterScanFrame = -1;
        private List<CharacterEntry> cachedCharacters;

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
            ReleaseMaximumUi();
            if (PresetFileName != null) PresetFileName.SettingChanged -= OnPresetFileChanged;
            lockedEffectLevels.Clear();
            cachedCharacters = null;
            selectedCharacter = null;
            if (Instance == this) Instance = null;
        }

        private void OnDisable() {
            ReleaseMaximumUi();
        }

        private void Start() {
            // Awake can precede optional plugins' initialization. Retry once when
            // all plugins have reached Start, then cache both hits and misses.
            RefreshIntegrations(true);
        }

        private void OnPresetFileChanged(object sender, EventArgs args) {
            LoadPresets();
            // Equal-sized files can have different preset names. Invalidate the
            // retained picker even when the status/count polling is unchanged.
            if (editorView != null) RefreshEditor();
        }

        private void Update() {
            if (Enabled.Value && !MaximumUI.MaximumUIRuntime.IsAnyTextFieldFocused &&
                !MaximumUI.MaximumUIRuntime.IsModalOpen && ToggleMenu.Value.IsDown()) {
                showWindow = !showWindow;
            }
            UpdateMaximumUi();
        }

        private void LateUpdate() {
            if (!Enabled.Value) return;

            if (lockedEffectLevels.Count > 0) {
                ApplyLockedEffects();
            }
        }

        private void OnGUI() {
            if (Enabled.Value && showWindow) EnsureMaximumUi();
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

        }

        private void CloseNameDialog() {
            showNameDialog = false;
            nameDialogMode = NameDialogMode.None;
            nameDialogPreset = null;
            nameDialogCharacter = null;
            nameDialogMessage = string.Empty;
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
            editorSession.Invalidate();
            presetStore.Invalidate();
            // A dialog opened for the previous file must not save after a path switch.
            CloseNameDialog();
            string path = null;
            try {
                path = ResolvePresetPath(PresetFileName == null ? DefaultPresetFileName : PresetFileName.Value);
                presetStore.Load(path);
            } catch (Exception ex) {
                statusText = "载入预设 XML 失败；详情见预设面板。";
                presetLoadError = XmlFileDiagnostics.Describe(ex) + "\n文件: " + (path ?? "(路径配置无效)") +
                    "\n请修复文件后点击「重载XML」。";
                // Keep the complete exception (including serializer inner errors
                // and the source path) available without enabling debug logging.
                Logger.LogWarning("Could not load preset XML: " + (path ?? "(invalid configured path)") + "\n" + ex);
                return false;
            }
            presetLoadError = string.Empty;
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

}
