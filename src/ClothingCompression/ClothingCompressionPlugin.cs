using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using TMPro;
using UniRx;
using UnityEngine;

[assembly: System.Reflection.AssemblyFileVersion(ClothingCompression.ClothingCompressionPlugin.Version)]

namespace ClothingCompression {
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInDependency(Sideloader.Sideloader.GUID)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    internal sealed class ClothingCompressionPlugin : BaseUnityPlugin {
        internal const string Guid = "MaxMixAlex.ClothingCompression";
        internal const string Name = "Clothing Compression";
        internal const string Version = "0.3.19";
        internal static readonly string[] CoordinateNames = { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣" };
        private static readonly FieldInfo ControlObjectsField = typeof(BaseGuiEntry).GetField("_controlObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DropdownOptionsField = typeof(MakerDropdown).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(MakerDropdown).GetField("<Options>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static ManualLogSource Log;
        internal static MeshAccessDetour MeshAccess;
        internal static ClothingCompressionPlugin Instance;
        internal static CompressionLibraryStore Library;
        internal static ConfigEntry<KeyboardShortcut> ToggleWindow;
        internal static ConfigEntry<bool> VerboseDiagnostics;

        private CompressionGui gui;
        private MakerDropdown deformationControl, kindControl, bindingControl, importCoordinateControl, importDeformationControl;
        private MakerRadioButtons saveScopeControl;
        private MakerText bindingWarningControl;
        private MakerToggle enabledControl;
        private MakerSideToggleRow sideControl;
        private MakerSlider positionControl, widthControl, depthControl, proximalControl, distalControl, clearanceXControl, clearanceYControl, clearanceZControl;
        private readonly List<BaseGuiEntry> detailControls = new List<BaseGuiEntry>();
        private List<DeformationEntry> makerImportEntries = new List<DeformationEntry>();
        private int makerImportCoordinate = -1, makerImportEntry;
        private bool refreshingMaker;

        private void Awake() {
            Instance = this; Log = Logger; Library = new CompressionLibraryStore(); Library.Reload();
            ToggleWindow = Config.Bind("General", "Show advanced menu", new KeyboardShortcut(KeyCode.F8), "Open the advanced deformation editor.");
            VerboseDiagnostics = Config.Bind("Debug", "Verbose mesh diagnostics", false,
                "Write detailed mesh replacement and fingerprint diagnostics to the BepInEx log. Enable only while troubleshooting.");
            CharacterApi.RegisterExtraBehaviour<CompressionController>(Guid);
            MakerAPI.RegisterCustomSubCategories += RegisterMakerControls;
            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MeshAccess = new MeshAccessDetour(); gui = new CompressionGui();
        }

        private void Update() { if (ToggleWindow.Value.IsDown()) ToggleAdvanced(); }
        private void OnGUI() { gui.Draw(); }
        private void OnDestroy() {
            MakerAPI.RegisterCustomSubCategories -= RegisterMakerControls;
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            if (MeshAccess != null) MeshAccess.Dispose(); MeshAccess = null;
        }
        internal void ToggleAdvanced() { gui.Visible = !gui.Visible; }
        private void OnMakerFinishedLoading(object sender, EventArgs e) { RefreshMakerControls(CurrentMakerController()); }

        private void RegisterMakerControls(object sender, RegisterSubCategoriesEvent e) {
            // Maker can be entered repeatedly in the same game process. Controls from
            // its previous scene must not be retained or revisited by refresh calls.
            detailControls.Clear();
            makerImportEntries.Clear();
            makerImportCoordinate = -1;
            makerImportEntry = 0;
            MakerCategory baseCategory = MakerConstants.Clothes.Copy;
            MakerCategory cat = new MakerCategory(baseCategory.CategoryName, "clothingCompression", baseCategory.Position + 1, "勒肉形变");
            e.AddSubCategory(cat);

            // First row: dynamically refreshed deformation selector.
            deformationControl = e.AddControl(new MakerDropdown("选择形变", new[] { "无形变" }, cat, 0, this));
            MakerSeparator detailSeparator = e.AddControl(new MakerSeparator(cat, this));
            detailControls.Add(detailSeparator);

            enabledControl = e.AddControl(new MakerToggle(cat, "启用", true, this));
            kindControl = e.AddControl(new MakerDropdown("形变类别", new[] { "大腿", "整腿定位", "膝部", "大臂", "前臂" }, cat, 0, this));
            sideControl = e.AddControl(new MakerSideToggleRow(cat, this));
            bindingControl = e.AddControl(new MakerDropdown("绑定服装/饰品", new[] { "未绑定（始终应用）" }, cat, 0, this));
            bindingWarningControl = e.AddControl(new MakerText("未绑定：当前只形变身体，不会处理服装/饰品网格。", cat, this));
            positionControl = e.AddControl(new MakerSlider(cat, "束口位置", -0.5f, 1.5f, 0.32f, this));
            widthControl = e.AddControl(new MakerSlider(cat, "影响宽度", 0.005f, 0.5f, 0.09f, this));
            depthControl = e.AddControl(CreateScaledMakerSlider(cat, "内收深度", 0f, 0.10f, 0.006f));
            proximalControl = e.AddControl(CreateScaledMakerSlider(cat, "身体侧隆起", 0f, 0.08f, 0.003f));
            distalControl = e.AddControl(CreateScaledMakerSlider(cat, "末端侧隆起", 0f, 0.08f, 0.003f));
            clearanceXControl = e.AddControl(CreateScaledMakerSlider(cat, "服装外扩 X", 0f, 0.10f, 0.0015f));
            clearanceYControl = e.AddControl(CreateScaledMakerSlider(cat, "服装外扩 Y", 0f, 0.10f, 0.0015f));
            clearanceZControl = e.AddControl(CreateScaledMakerSlider(cat, "服装外扩 Z", 0f, 0.10f, 0.0015f));
            saveScopeControl = e.AddControl(new MakerRadioButtons(cat, this, "保存级别", 0,
                new[] { "角色 + 服装/饰品", "仅服装/饰品" }));
            detailControls.Add(enabledControl); detailControls.Add(kindControl); detailControls.Add(sideControl);
            detailControls.Add(bindingControl); detailControls.Add(bindingWarningControl); detailControls.Add(positionControl);
            detailControls.Add(widthControl); detailControls.Add(depthControl); detailControls.Add(proximalControl);
            detailControls.Add(distalControl); detailControls.Add(clearanceXControl); detailControls.Add(clearanceYControl); detailControls.Add(clearanceZControl);
            detailControls.Add(saveScopeControl);

            e.AddControl(new MakerSeparator(cat, this));
            MakerButton add = e.AddControl(new MakerButton("新增形变", cat, this));
            MakerButton duplicate = e.AddControl(new MakerButton("复制形变", cat, this));
            MakerButton delete = e.AddControl(new MakerButton("删除形变", cat, this));
            e.AddControl(new MakerSeparator(cat, this));
            importCoordinateControl = e.AddControl(new MakerDropdown("来源服装搭配", CoordinateNames, cat, 0, this));
            importDeformationControl = e.AddControl(new MakerDropdown("来源形变", new[] { "该搭配没有已保存形变" }, cat, 0, this));
            MakerButton import = e.AddControl(new MakerButton("追加形变副本", cat, this));
            e.AddControl(new MakerSeparator(cat, this));
            MakerButton save = e.AddControl(new MakerButton("保存形变设置", cat, this));
            MakerButton advanced = e.AddControl(new MakerButton("高级面板 (" + ToggleWindow.Value.Serialize() + ")", cat, this));

            deformationControl.ValueChanged.Subscribe(v => { if (refreshingMaker) return; CompressionController c = CurrentMakerController(); if (c != null && c.Entries.Count > 0) { c.SelectedIndex = Mathf.Clamp(v, 0, c.Entries.Count - 1); RefreshMakerControls(c); } });
            enabledControl.ValueChanged.Subscribe(v => SetSelected(x => x.Enabled = v, false));
            kindControl.ValueChanged.Subscribe(v => SetSelected(x => SetKind(x, v == 0 ? DeformationKind.Thigh : v == 1 ? DeformationKind.LowerThigh : v == 2 ? DeformationKind.Knee : v == 3 ? DeformationKind.UpperArm : DeformationKind.Arm), true));
            sideControl.ValueChanged.Subscribe(v => SetSelected(x => { x.Left = (v & 1) != 0; x.Right = (v & 2) != 0; }, true));
            bindingControl.ValueChanged.Subscribe(v => SetBinding(v));
            positionControl.ValueChanged.Subscribe(v => SetSelected(x => x.Position = v, true));
            widthControl.ValueChanged.Subscribe(v => SetSelected(x => x.Width = v, true));
            depthControl.ValueChanged.Subscribe(v => SetSelected(x => x.Depth = v, true));
            proximalControl.ValueChanged.Subscribe(v => SetSelected(x => x.ProximalBulge = v, true));
            distalControl.ValueChanged.Subscribe(v => SetSelected(x => x.DistalBulge = v, true));
            clearanceXControl.ValueChanged.Subscribe(v => SetSelected(x => x.GarmentClearanceX = v, true));
            clearanceYControl.ValueChanged.Subscribe(v => SetSelected(x => x.GarmentClearanceY = v, true));
            clearanceZControl.ValueChanged.Subscribe(v => SetSelected(x => x.GarmentClearanceZ = v, true));
            saveScopeControl.ValueChanged.Subscribe(v => SetSelected(x => x.SaveScope = v == 0 ? SaveScope.CharacterAndItem : SaveScope.ItemOnly, false));
            add.OnClick.AddListener(delegate { CompressionController c = CurrentMakerController(); if (c != null) { c.Add(DeformationKind.Thigh); RefreshMakerControls(c); } });
            duplicate.OnClick.AddListener(delegate { CompressionController c = CurrentMakerController(); if (c != null) { c.DuplicateSelected(); RefreshMakerControls(c); } });
            delete.OnClick.AddListener(delegate { CompressionController c = CurrentMakerController(); if (c != null) { c.DeleteSelected(); RefreshMakerControls(c); } });
            importCoordinateControl.ValueChanged.Subscribe(v => {
                if (refreshingMaker) return;
                makerImportCoordinate = Mathf.Clamp(v, 0, CoordinateNames.Length - 1); makerImportEntry = 0;
                RefreshMakerImportControls(CurrentMakerController());
            });
            importDeformationControl.ValueChanged.Subscribe(v => { if (!refreshingMaker) makerImportEntry = Mathf.Clamp(v, 0, Math.Max(0, makerImportEntries.Count - 1)); });
            import.OnClick.AddListener(delegate {
                CompressionController c = CurrentMakerController();
                if (c == null) return;
                if (makerImportCoordinate == c.CoordinateIndex) {
                    Log.LogMessage("请选择另一个服装搭配作为来源。"); return;
                }
                DeformationEntry source = makerImportEntry >= 0 && makerImportEntry < makerImportEntries.Count ? makerImportEntries[makerImportEntry] : null;
                if (c.ImportEntry(source)) RefreshMakerControls(c);
            });
            save.OnClick.AddListener(delegate { CompressionController c = CurrentMakerController(); if (c != null) c.Save(); });
            advanced.OnClick.AddListener(ToggleAdvanced);
        }

        private void SetSelected(Action<DeformationEntry> setter, bool rebuild) {
            if (refreshingMaker) return;
            CompressionController controller = CurrentMakerController(); DeformationEntry entry = controller == null ? null : controller.Selected;
            if (entry == null) return; setter(entry); controller.Changed(rebuild);
        }

        private void SetBinding(int index) {
            if (refreshingMaker) return;
            CompressionController controller = CurrentMakerController(); DeformationEntry entry = controller == null ? null : controller.Selected;
            if (entry == null) return;
            List<ItemDescriptor> items = ItemCatalog.Get(controller.ChaControl);
            ItemCatalog.Bind(entry, index <= 0 || index > items.Count ? null : items[index - 1]); controller.Changed(true);
        }

        internal void RefreshMakerControls(CompressionController controller) {
            if (!MakerAPI.InsideMaker || deformationControl == null) return;
            refreshingMaker = true;
            try {
                DeformationEntry selected = controller == null ? null : controller.Selected;
                SetDetailControlsVisible(selected != null);
                string[] deformationLabels = controller == null || controller.Entries.Count == 0
                    ? new[] { "无形变" }
                    : controller.Entries.Select((x, i) => ShortenOption(FormatEntryLabel(i, x), 28)).ToArray();
                // KK's bundled TMP dropdown renders its only popup row blank. A second,
                // informational option forces the normal item population path.
                if (controller != null && controller.Entries.Count == 1)
                    deformationLabels = deformationLabels.Concat(new[] { "（当前仅有一个形变）" }).ToArray();
                UpdateDropdown(deformationControl, deformationLabels, controller == null ? 0 : controller.SelectedIndex);

                List<ItemDescriptor> items = controller == null ? new List<ItemDescriptor>() : ItemCatalog.Get(controller.ChaControl);
                string[] bindingLabels = new[] { "<color=#D52B1E>未绑定（始终应用）</color>" }.Concat(items.Select(x => ShortenOption(x.Display, 28))).ToArray();
                int bindingIndex = 0;
                if (selected != null && selected.BindingKind != BoundItemKind.None) {
                    int found = items.FindIndex(x => ItemCatalog.Matches(selected, x) && x.Slot == selected.PreferredSlot);
                    if (found < 0) found = items.FindIndex(x => ItemCatalog.Matches(selected, x));
                    if (found < 0) found = items.FindIndex(x => x.Kind == selected.BindingKind && x.Slot == selected.PreferredSlot &&
                        string.Equals(x.Name ?? string.Empty, selected.BindingName ?? string.Empty, StringComparison.Ordinal));
                    bindingIndex = found < 0 ? 0 : found + 1;
                }
                UpdateDropdown(bindingControl, bindingLabels, bindingIndex);
                UpdateBindingWarning(selected != null && selected.BindingKind == BoundItemKind.None);
                RefreshMakerImportControls(controller);
                if (selected == null) return;
                enabledControl.Value = selected.Enabled; kindControl.Value = selected.Kind == DeformationKind.Thigh ? 0 : selected.Kind == DeformationKind.LowerThigh ? 1 : selected.Kind == DeformationKind.Knee ? 2 : selected.Kind == DeformationKind.UpperArm ? 3 : 4;
                sideControl.Value = (selected.Left ? 1 : 0) | (selected.Right ? 2 : 0); positionControl.Value = selected.Position;
                widthControl.Value = selected.Width; depthControl.Value = selected.Depth; proximalControl.Value = selected.ProximalBulge;
                distalControl.Value = selected.DistalBulge; clearanceXControl.Value = selected.GarmentClearanceX;
                clearanceYControl.Value = selected.GarmentClearanceY; clearanceZControl.Value = selected.GarmentClearanceZ;
                saveScopeControl.Value = selected.SaveScope == SaveScope.CharacterAndItem ? 0 : 1;
                ConfigureNumericInput(positionControl); ConfigureNumericInput(widthControl); ConfigureNumericInput(depthControl);
                ConfigureNumericInput(proximalControl); ConfigureNumericInput(distalControl); ConfigureNumericInput(clearanceXControl);
                ConfigureNumericInput(clearanceYControl); ConfigureNumericInput(clearanceZControl);
            } finally { refreshingMaker = false; }
        }

        private void RefreshMakerImportControls(CompressionController controller) {
            if (importCoordinateControl == null || importDeformationControl == null) return;
            bool wasRefreshing = refreshingMaker; refreshingMaker = true;
            try {
                int current = controller == null ? -1 : controller.CoordinateIndex;
                if (makerImportCoordinate < 0 || makerImportCoordinate >= CoordinateNames.Length)
                    makerImportCoordinate = current >= 0 && current < CoordinateNames.Length ? (current + 1) % CoordinateNames.Length : 0;
                UpdateDropdown(importCoordinateControl, CoordinateNames, makerImportCoordinate);
                makerImportEntries = controller == null || makerImportCoordinate == current
                    ? new List<DeformationEntry>()
                    : Library.LoadCoordinateEntries(controller.MakeCoordinateKey(makerImportCoordinate));
                makerImportEntry = Mathf.Clamp(makerImportEntry, 0, Math.Max(0, makerImportEntries.Count - 1));
                string[] labels = makerImportEntries.Count == 0
                    ? new[] { makerImportCoordinate == current ? "请选择其他服装搭配" : "该搭配没有已保存形变" }
                    : makerImportEntries.Select((x, i) => ShortenOption(FormatEntryLabel(i, x), 28)).ToArray();
                // Work around KK's blank single-item popup template, as with the main selector.
                if (makerImportEntries.Count == 1) labels = labels.Concat(new[] { "（该搭配仅有一个形变）" }).ToArray();
                UpdateDropdown(importDeformationControl, labels, makerImportEntry);
            } finally { refreshingMaker = wasRefreshing; }
        }

        internal static string FormatEntryLabel(int index, DeformationEntry entry) {
            string item = entry == null || entry.BindingKind == BoundItemKind.None || string.IsNullOrEmpty(entry.BindingName) ? "未绑定" : entry.BindingName;
            string kind = entry != null && entry.Kind == DeformationKind.UpperArm ? "大臂" : entry != null && entry.Kind == DeformationKind.Arm ? "前臂" : entry != null && entry.Kind == DeformationKind.Knee ? "膝部" : entry != null && entry.Kind == DeformationKind.LowerThigh ? "整腿" : "大腿";
            return (index + 1) + " " + item + "（" + kind + "）";
        }

        private static void UpdateDropdown(MakerDropdown control, string[] options, int value) {
            if (control == null) return;
            if (DropdownOptionsField != null) DropdownOptionsField.SetValue(control, options);
            IList objects = ControlObjectsField == null ? null : ControlObjectsField.GetValue(control) as IList;
            if (objects != null) {
                foreach (object item in objects) {
                    GameObject go = item as GameObject; if (go == null) continue;
                    TMP_Dropdown dropdown = go.GetComponentInChildren<TMP_Dropdown>(true); if (dropdown == null) continue;
                    dropdown.ClearOptions(); dropdown.AddOptions(options.ToList()); dropdown.value = Mathf.Clamp(value, 0, Math.Max(0, options.Length - 1)); dropdown.RefreshShownValue();
                    ConfigureDropdownText(dropdown.captionText);
                    ConfigureDropdownText(dropdown.itemText);
                    if (dropdown.itemText != null && options.Length > 0)
                        dropdown.itemText.text = options[Mathf.Clamp(value, 0, options.Length - 1)];
                }
            }
            control.Value = Mathf.Clamp(value, 0, Math.Max(0, options.Length - 1));
        }

        private static void ConfigureDropdownText(TMP_Text text) {
            if (text == null) return;
            text.richText = true;
            text.enableWordWrapping = false;
            // Ellipsis is broken on KK's old TMP dropdown item template and renders a blank row.
            // Options are shortened beforehand, so Truncate is both safe and visually equivalent.
            text.overflowMode = TextOverflowModes.Truncate;
        }

        private static string ShortenOption(string value, int maxCharacters) {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters) return value;
            return value.Substring(0, Math.Max(1, maxCharacters - 1)) + "…";
        }

        internal static void SetKind(DeformationEntry entry, DeformationKind kind) {
            if (entry.Kind == kind) return;
            entry.Kind = kind;
            entry.Position = kind == DeformationKind.Thigh ? 0.32f : kind == DeformationKind.LowerThigh ? 0.58f : kind == DeformationKind.Knee ? 0f : kind == DeformationKind.UpperArm ? 0.55f : 0.68f;
            entry.Width = kind == DeformationKind.Knee ? 0.14f : kind == DeformationKind.LowerThigh ? 0.12f : kind == DeformationKind.Thigh ? 0.09f : 0.10f;
        }

        private void SetDetailControlsVisible(bool visible) {
            foreach (BaseGuiEntry control in detailControls) {
                IList objects = control == null || ControlObjectsField == null ? null : ControlObjectsField.GetValue(control) as IList;
                if (objects == null) continue;
                foreach (object item in objects) {
                    GameObject go = item as GameObject;
                    if (go != null) go.SetActive(visible);
                }
            }
        }

        private static void ConfigureNumericInput(BaseGuiEntry control) {
            IList objects = control == null || ControlObjectsField == null ? null : ControlObjectsField.GetValue(control) as IList;
            if (objects == null) return;
            foreach (object item in objects) {
                GameObject go = item as GameObject; if (go == null) continue;
                foreach (TMP_InputField input in go.GetComponentsInChildren<TMP_InputField>(true)) {
                    input.contentType = TMP_InputField.ContentType.DecimalNumber;
                    input.characterValidation = TMP_InputField.CharacterValidation.Decimal;
                }
                foreach (UnityEngine.UI.InputField input in go.GetComponentsInChildren<UnityEngine.UI.InputField>(true)) {
                    input.contentType = UnityEngine.UI.InputField.ContentType.DecimalNumber;
                    input.characterValidation = UnityEngine.UI.InputField.CharacterValidation.Decimal;
                }
            }
        }

        private void UpdateBindingWarning(bool show) {
            if (bindingWarningControl == null) return;
            bindingWarningControl.Text = "未绑定：当前只形变身体，不会处理服装/饰品网格。";
            IList objects = ControlObjectsField == null ? null : ControlObjectsField.GetValue(bindingWarningControl) as IList;
            if (objects == null) return;
            foreach (object item in objects) {
                GameObject go = item as GameObject; if (go == null) continue;
                go.SetActive(show);
                foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true)) text.color = new Color(0.95f, 0.32f, 0.05f, 1f);
            }
        }

        private MakerSlider CreateScaledMakerSlider(MakerCategory category, string label, float min, float max, float initial) {
            return new MakerSlider(category, label, min, max, initial, this) {
                ValueToString = value => (value * 100f).ToString("0.###"),
                StringToValue = text => {
                    float value;
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                        !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return 0f;
                    return value / 100f;
                }
            };
        }

        private static CompressionController CurrentMakerController() { ChaControl cha = MakerAPI.GetCharacterControl(); return cha == null ? null : cha.GetComponent<CompressionController>(); }
    }
}
