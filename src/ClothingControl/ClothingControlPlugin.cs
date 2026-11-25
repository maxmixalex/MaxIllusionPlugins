using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using MaximumUI;
using Studio;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(ClothingControl.ClothingControlPlugin.Version)]

namespace ClothingControl {
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    [BepInProcess(KoikatuAPI.StudioProcessName)]
    [BepInPlugin(GUID, "ClothingControl", Version)]
    public sealed class ClothingControlPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.ClothingControl";
        public const string Version = "0.1.1";
        private const string MountId = GUID + ".window";
        private static readonly string[] CoordinateNames = {
            "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣"
        };
        private ConfigEntry<KeyboardShortcut> shortcut;
        private WardrobePresetStore store;
        private WardrobeView view;
        private ChaControl target;
        private bool visible;
        private int generation;
        private float nextPoll;
        private string selectedPreset, draft = string.Empty, status = "就绪。";
        private PendingApply pending;
        private List<ChaControl> previousCharacters = new List<ChaControl>();

        private sealed class PendingApply {
            internal ChaControl Target;
            internal WardrobePreset Preset;
            internal int Coordinate;
            internal WardrobeLoadGate Gate;
        }

        private void Awake() {
            shortcut = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.F7), "Open the wardrobe editor in Maker or Studio.");
            store = new WardrobePresetStore(Path.Combine(Paths.ConfigPath, GUID + ".presets.xml"));
            store.Reload();
        }

        private void OnEnable() {
            CharacterApi.CharacterReloaded += OnCharacterReloaded;
            CharacterApi.CoordinateLoaded += OnCoordinateLoaded;
        }

        private void OnCharacterReloaded(object sender, CharaReloadEventArgs e) {
            ObserveReload(e.ReloadedCharacter);
        }

        private void OnCoordinateLoaded(object sender, CoordinateEventArgs e) {
            ObserveReload(e.Character);
        }

        private void ObserveReload(ChaControl character) {
            if (character == null || !ReferenceEquals(character, target)) return;
            if (pending != null && ReferenceEquals(pending.Target, character))
                pending.Gate.NotifyReload(Time.frameCount, Coordinate(character));
            else generation++;
            nextPoll = 0f;
        }

        private void OnDisable() {
            CharacterApi.CharacterReloaded -= OnCharacterReloaded;
            CharacterApi.CoordinateLoaded -= OnCoordinateLoaded;
            Close();
            if (view != null) MaximumUIRuntime.Unmount(MountId);
            view = null;
            target = null;
            previousCharacters.Clear();
        }

        private void OnDestroy() { OnDisable(); }

        private static bool IsStudio() { return UnityEngine.Object.FindObjectOfType<StudioScene>() != null; }
        private static bool SupportedScene() { return MakerAPI.InsideAndLoaded || IsStudio(); }

        private static List<ChaControl> Characters() {
            List<ChaControl> result = new List<ChaControl>();
            if (MakerAPI.InsideAndLoaded) {
                ChaControl character = MakerAPI.GetCharacterControl();
                if (character != null) result.Add(character);
                return result;
            }
            if (!IsStudio()) return result;
            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            if (studio == null || studio.dicObjectCtrl == null) return result;
            List<int> keys = new List<int>(studio.dicObjectCtrl.Keys);
            keys.Sort();
            foreach (int key in keys) {
                ObjectCtrlInfo info = studio.dicObjectCtrl[key];
                OCICharFemale female = info as OCICharFemale;
                OCICharMale male = info as OCICharMale;
                ChaControl character = female != null ? (ChaControl)female.female : male != null ? male.male : null;
                if (character != null && !result.Contains(character)) result.Add(character);
            }
            return result;
        }

        private static bool HasCharacter(List<ChaControl> characters, ChaControl character) {
            if (character == null) return false;
            foreach (ChaControl candidate in characters) if (ReferenceEquals(candidate, character)) return true;
            return false;
        }

        private static int CoordinateCount(ChaControl character) {
            return character == null || character.chaFile == null || character.chaFile.coordinate == null
                ? 0 : character.chaFile.coordinate.Length;
        }

        private static int Coordinate(ChaControl character) {
            return character == null || character.fileStatus == null ? -1 : character.fileStatus.coordinateType;
        }

        private static bool ReadAccessories(ChaControl character, out ChaFileAccessory.PartsInfo[] parts, out bool[] shown) {
            parts = null;
            shown = null;
            if (character == null || character.fileStatus == null || character.nowCoordinate == null ||
                character.nowCoordinate.accessory == null) return false;
            parts = character.nowCoordinate.accessory.parts;
            shown = character.fileStatus.showAccessory;
            return parts != null && shown != null;
        }

        private static string CharacterName(ChaControl character, int index) {
            string name = character.chaFile != null && character.chaFile.parameter != null
                ? character.chaFile.parameter.fullname : null;
            return (index + 1) + ". " + (string.IsNullOrEmpty(name) ? character.name : name);
        }

        private static string AccessoryName(ChaControl character, ChaFileAccessory.PartsInfo part, int slot) {
            string name = null;
            try {
                ListInfoBase info = character.lstCtrl.GetListInfo((ChaListDefine.CategoryNo)part.type, part.id);
                if (info != null) name = info.Name;
            } catch { }
            return (slot + 1) + ". " + (string.IsNullOrEmpty(name) ? part.type + ":" + part.id : name);
        }

        private void Update() {
            if (shortcut.Value.IsDown() && !MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUIRuntime.IsModalOpen) {
                if (visible) Close();
                else if (SupportedScene()) Open();
            }
            if (!visible) return;
            if (pending != null) AdvancePending();
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .25f;
            if (!SupportedScene()) { Close(); return; }
            Refresh();
        }

        private void Open() {
            if (view == null) {
                MaximumUIRuntime.EnsureHost();
                view = new WardrobeView();
                MaximumUIRuntime.Mount(MountId, view, new MountOptions {
                    Visible = false, BuildEveryFrame = false, SoftMouseCapture = true, SoftKeyboardCapture = true, SortOrder = 125
                });
            }
            visible = true;
            generation++;
            nextPoll = 0f;
            Refresh();
            MaximumUIRuntime.SetVisible(MountId, true);
            MaximumUIRuntime.Focus(MountId);
        }

        private void Close() {
            visible = false;
            generation++;
            pending = null;
            if (view != null) MaximumUIRuntime.SetVisible(MountId, false);
        }

        private bool SessionCurrent(int token, ChaControl character) {
            return visible && token == generation && ReferenceEquals(target, character) && SupportedScene();
        }

        private bool TargetCurrent(int token, ChaControl character) {
            return SessionCurrent(token, character) && pending == null && HasCharacter(Characters(), character);
        }

        private void Refresh() {
            if (!visible || view == null) return;
            List<ChaControl> characters = Characters();
            bool rosterChanged = previousCharacters.Count != characters.Count;
            if (!rosterChanged)
                for (int i = 0; i < characters.Count; i++)
                    if (!ReferenceEquals(previousCharacters[i], characters[i])) { rosterChanged = true; break; }
            if (rosterChanged) { previousCharacters = characters; generation++; }
            if (!HasCharacter(characters, target)) {
                ChaControl next = characters.Count > 0 ? characters[0] : null;
                if (!ReferenceEquals(next, target)) {
                    target = next;
                    generation++;
                    pending = null;
                }
            }
            ChaControl character = target;
            if (view.Model.CoordinateIndex != Coordinate(character)) generation++;
            int token = generation;
            string[] names = new string[characters.Count];
            for (int i = 0; i < names.Length; i++) names[i] = CharacterName(characters[i], i);
            int coordinateCount = CoordinateCount(character);
            string[] coordinates = new string[coordinateCount];
            for (int i = 0; i < coordinates.Length; i++)
                coordinates[i] = i < CoordinateNames.Length ? CoordinateNames[i] : "配装 " + (i + 1);
            string[] presets = store.Names();
            int presetIndex = Array.IndexOf(presets, selectedPreset);
            if (presetIndex < 0 && presets.Length > 0) {
                presetIndex = 0;
                selectedPreset = presets[0];
            } else if (presets.Length == 0) selectedPreset = null;

            WardrobeViewModel model = new WardrobeViewModel {
                Version = Version, Revision = token,
                Targets = names, TargetIndex = characters.IndexOf(character), Coordinates = coordinates,
                CoordinateIndex = Coordinate(character), Presets = presets, PresetIndex = presetIndex,
                Name = draft, Status = status, Error = store.Error, CanWrite = store.CanWrite,
                Busy = pending != null, HasTarget = character != null && coordinateCount > 0,
                Close = delegate { if (visible && token == generation) Close(); }
            };
            model.SelectTarget = index => {
                if (!SessionCurrent(token, character) || pending != null || index < 0 || index >= characters.Count ||
                    !HasCharacter(Characters(), characters[index])) return;
                if (!ReferenceEquals(target, characters[index])) { target = characters[index]; generation++; }
                Refresh();
            };
            model.SelectCoordinate = index => {
                if (!TargetCurrent(token, character)) return;
                if (Coordinate(character) != index) BeginCoordinateChange(character, index, null);
            };
            model.SelectPreset = index => {
                if (!SessionCurrent(token, character) || pending != null || index < 0 || index >= presets.Length || store.Find(presets[index]) == null) return;
                if (selectedPreset != presets[index]) generation++;
                selectedPreset = presets[index]; draft = selectedPreset; Refresh();
            };
            model.ChangeName = value => {
                if (!SessionCurrent(token, character) || pending != null) return;
                draft = value ?? string.Empty;
                view.Model.Name = draft;
                MaximumUIRuntime.MarkDirty(view);
            };
            model.Create = () => { if (TargetCurrent(token, character)) Save(character, draft, false); };
            string capturedPreset = selectedPreset;
            model.Overwrite = () => { if (TargetCurrent(token, character) && capturedPreset == selectedPreset) Save(character, capturedPreset, true); };
            model.Apply = () => { if (TargetCurrent(token, character) && capturedPreset == selectedPreset) Apply(character, store.Find(capturedPreset)); };
            model.Delete = () => {
                if (!SessionCurrent(token, character) || pending != null || capturedPreset != selectedPreset) return;
                string message;
                status = store.Delete(capturedPreset, out message) ? "已删除预设。" : message;
                generation++; Refresh();
            };
            model.Reload = () => {
                if (!SessionCurrent(token, character) || pending != null) return;
                status = store.Reload() ? "已重载 XML。" : "XML 读取失败。";
                generation++; Refresh();
            };

            ChaFileAccessory.PartsInfo[] parts;
            bool[] shown;
            if (ReadAccessories(character, out parts, out shown)) {
                for (int i = 0; i < Math.Min(parts.Length, shown.Length); i++) {
                    ChaFileAccessory.PartsInfo part = parts[i];
                    if (part == null || part.type == 120) continue;
                    int slot = i, type = part.type, item = part.id, coordinate = Coordinate(character);
                    model.Rows.Add(new WardrobeRow {
                        Slot = slot, Type = type, Item = item, Label = AccessoryName(character, part, slot), Visible = shown[slot],
                        Change = value => {
                            if (!TargetCurrent(token, character) || Coordinate(character) != coordinate) return;
                            SetMatchingAccessory(character, new AccessoryVisibility {
                                Slot = slot, Type = type, Item = item, Visible = value
                            });
                            Refresh();
                        }
                    });
                }
            }
            if (!view.Model.SameDisplayAs(model)) {
                view.Model = model;
                MaximumUIRuntime.MarkDirty(view);
            }
        }

        private static bool ChangeCoordinate(ChaControl character, int index) {
            if (index < 0 || index >= CoordinateCount(character)) return false;
            if (Coordinate(character) == index) return true;
            if (MakerAPI.InsideAndLoaded) {
                if (!ReferenceEquals(MakerAPI.GetCharacterControl(), character)) return false;
                var maker = MakerAPI.GetMakerBase();
                if (maker == null || maker.customCtrl == null || maker.customCtrl.ddCoordinate == null ||
                    index >= maker.customCtrl.ddCoordinate.options.Count) return false;
                maker.customCtrl.ddCoordinate.value = index;
                return true;
            }
            if (!IsStudio()) return false;
            character.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)index);
            return true;
        }

        private static bool Capture(ChaControl character, out WardrobePreset snapshot) {
            snapshot = null;
            int coordinate = Coordinate(character);
            if (coordinate < 0 || coordinate >= CoordinateCount(character)) return false;
            ChaFileAccessory.PartsInfo[] parts;
            bool[] shown;
            if (!ReadAccessories(character, out parts, out shown)) return false;
            WardrobePreset candidate = new WardrobePreset { Coordinate = coordinate };
            for (int i = 0; i < parts.Length; i++) {
                ChaFileAccessory.PartsInfo part = parts[i];
                if (part == null || part.type == 120) continue;
                if (i >= shown.Length) return false; // Never manufacture missing visibility data.
                candidate.Accessories.Add(new AccessoryVisibility { Slot = i, Type = part.type, Item = part.id, Visible = shown[i] });
            }
            snapshot = candidate;
            return true;
        }

        private void Save(ChaControl character, string name, bool overwrite) {
            WardrobePreset snapshot;
            string message;
            if (!Capture(character, out snapshot)) status = "当前配装状态尚未准备好，未保存。";
            else if (store.Save(snapshot, name, overwrite, out message)) {
                selectedPreset = name.Trim(); draft = selectedPreset;
                status = "已保存预设：" + selectedPreset;
                generation++;
            } else status = message;
            Refresh();
        }

        private void Apply(ChaControl character, WardrobePreset preset) {
            if (preset == null || preset.Coordinate < 0 || preset.Coordinate >= CoordinateCount(character)) {
                status = "预设与当前角色的配装不兼容。"; Refresh(); return;
            }
            if (Coordinate(character) == preset.Coordinate) {
                ApplyAccessories(character, preset);
                return;
            }
            BeginCoordinateChange(character, preset.Coordinate, preset);
        }

        private void BeginCoordinateChange(ChaControl character, int destination, WardrobePreset preset) {
            int source = Coordinate(character);
            if (destination < 0 || destination >= CoordinateCount(character) || source < 0 || pending != null) return;
            // Register the operation BEFORE invoking the native API: a
            // synchronous reload callback must not be lost.
            pending = new PendingApply {
                Target = character, Preset = preset, Coordinate = destination,
                Gate = new WardrobeLoadGate(source, destination, Time.frameCount, Time.unscaledTime)
            };
            generation++;
            status = "正在载入配装…";
            try {
                if (!ChangeCoordinate(character, destination)) { pending = null; status = "配装切换失败。"; }
            } catch (Exception ex) {
                pending = null; status = ex.GetBaseException().Message; Logger.LogWarning(ex);
            }
            Refresh();
        }

        private static bool SameAccessory(ChaFileAccessory.PartsInfo left, ChaFileAccessory.PartsInfo right) {
            bool emptyLeft = left == null || left.type == 120;
            bool emptyRight = right == null || right.type == 120;
            if (emptyLeft || emptyRight) return emptyLeft && emptyRight;
            return left.type == right.type && left.id == right.id;
        }

        // Coordinate index alone can change before the resource reload.
        // Compare the actual working outfit with the requested stored outfit.
        // Ignore empty accessory padding so MoreAccessories can extend slots.
        private static bool OutfitReady(ChaControl character, int index) {
            if (character == null || index < 0 || index >= CoordinateCount(character) ||
                Coordinate(character) != index || character.nowCoordinate == null) return false;
            ChaFileCoordinate desired = character.chaFile.coordinate[index];
            if (desired == null || desired.accessory == null || desired.clothes == null ||
                character.nowCoordinate.clothes == null) return false;
            ChaFileAccessory.PartsInfo[] parts;
            bool[] shown;
            if (!ReadAccessories(character, out parts, out shown)) return false;
            var expected = desired.accessory.parts;
            var actualClothes = character.nowCoordinate.clothes.parts;
            var expectedClothes = desired.clothes.parts;
            if (expected == null || actualClothes == null || expectedClothes == null ||
                actualClothes.Length != expectedClothes.Length) return false;
            for (int i = 0; i < actualClothes.Length; i++) {
                var a = actualClothes[i]; var b = expectedClothes[i];
                if (a == null || b == null) { if (a != b) return false; }
                else if (a.id != b.id) return false;
            }
            for (int i = 0; i < Math.Max(parts.Length, expected.Length); i++) {
                var a = i < parts.Length ? parts[i] : null;
                var b = i < expected.Length ? expected[i] : null;
                if (!SameAccessory(a, b)) return false;
                if (a != null && a.type != 120 && i >= shown.Length) return false;
            }
            return true;
        }

        private void AdvancePending() {
            PendingApply operation = pending;
            bool current = SupportedScene() && ReferenceEquals(target, operation.Target) &&
                HasCharacter(Characters(), operation.Target);
            int coordinate = current ? Coordinate(operation.Target) : -1;
            bool ready = current && OutfitReady(operation.Target, operation.Coordinate);
            WardrobeLoadResult result = operation.Gate.Poll(Time.frameCount, Time.unscaledTime, current, coordinate, ready);
            if (result == WardrobeLoadResult.Waiting) return;
            pending = null;
            generation++;
            if (result == WardrobeLoadResult.Cancelled) status = "目标或配装已变更，取消操作。";
            else if (result == WardrobeLoadResult.TimedOut) status = "配装载入超时，未修改饰品。";
            else if (operation.Preset != null) { ApplyAccessories(operation.Target, operation.Preset); return; }
            else status = "已切换配装。";
            Refresh();
        }

        private static bool SetMatchingAccessory(ChaControl character, AccessoryVisibility entry) {
            ChaFileAccessory.PartsInfo[] parts;
            bool[] shown;
            int slot = entry.Slot;
            if (!ReadAccessories(character, out parts, out shown) || slot < 0 || slot >= parts.Length ||
                slot >= shown.Length || parts[slot] == null || parts[slot].type == 120 ||
                !entry.Matches(slot, parts[slot].type, parts[slot].id)) return false;
            if (shown[slot] != entry.Visible) character.SetAccessoryState(slot, entry.Visible);
            return true;
        }

        private void ApplyAccessories(ChaControl character, WardrobePreset preset) {
            int applied = 0;
            try {
                if (Coordinate(character) != preset.Coordinate) { status = "配装已经变更，未应用。"; Refresh(); return; }
                foreach (AccessoryVisibility entry in preset.Accessories)
                    if (SetMatchingAccessory(character, entry)) applied++;
                status = "已应用预设：" + applied + " 项匹配，" + (preset.Accessories.Count - applied) + " 项跳过。";
            } catch (Exception ex) { status = "应用中断：" + ex.GetBaseException().Message; Logger.LogWarning(ex); }
            Refresh();
        }
    }
}
