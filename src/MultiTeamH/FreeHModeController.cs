using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ActionGame.H;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiTeamH {
    [DefaultExecutionOrder(20000)]
    internal sealed class FreeHModeController : MonoBehaviour {
        private FreeHScene scene;
        private FreeHScene.Member member;
        private ManualLogSource log;
        private Toggle multiToggle;
        private Toggle normalToggle;
        private Toggle nativeMultiToggle;
        private Toggle darkToggle;
        private Toggle femaleToggleTemplate;
        private Toggle[] slotToggles;
        private Toggle[] stageToggles;
        private Toggle[] statusToggles;
        private GameObject normalPanel;
        private Button enterButton;
        private Button femaleButton;
        private Button maleButton;
        private CharaHInfoComponent femaleCard;
        private CharaHInfoComponent maleCard;
        private SaveData.Heroine primaryFemaleBeforeSelection;
        private SaveData.Heroine secondaryFemale;
        private SaveData.Player primaryMaleBeforeSelection;
        private SaveData.Player secondaryMale;
        private bool awaitingSecondaryFemale;
        private bool awaitingSecondaryMale;
        private int selectedSlot;
        private readonly int[] teamStage = new int[2];
        private readonly int[] teamStatus = new int[2];
        private TMP_Text normalHeader;
        private Text multiModeLabel;
        private Sprite multiModeNormalSprite;
        private Sprite multiModeSelectedSprite;
        private readonly List<UnityEngine.Object> generatedModeAssets = new List<UnityEngine.Object>();
        private TMP_Text[] clonedModeTmpLabels;
        private Text[] clonedModeTextLabels;
        private string normalHeaderText;

        internal bool IsSelected { get; private set; }

        internal bool Initialize(FreeHScene owner, ManualLogSource logger) {
            scene = owner;
            log = logger;
            Traverse tr = Traverse.Create(scene);
            member = tr.Field("member").GetValue<FreeHScene.Member>();
            normalToggle = tr.Field("tglNormal3P").GetValue<Toggle>();
            nativeMultiToggle = tr.Field("tgl3P").GetValue<Toggle>();
            darkToggle = tr.Field("tglDark3P").GetValue<Toggle>();
            femaleToggleTemplate = tr.Field("tglFemal3P1").GetValue<Toggle>();
            normalPanel = tr.Field("objNormal").GetValue<GameObject>();
            enterButton = tr.Field("enterButton").GetValue<Button>();
            femaleButton = tr.Field("femaleSelectButtonNormal").GetValue<Button>();
            maleButton = tr.Field("maleSelectButtonNormal").GetValue<Button>();
            femaleCard = tr.Field("cardFemaleNormal").GetValue<CharaHInfoComponent>();
            maleCard = tr.Field("cardMaleNormal").GetValue<CharaHInfoComponent>();
            stageToggles = new[] {
                tr.Field("tglFirstNormal").GetValue<Toggle>(),
                tr.Field("tglInexperienceNormal").GetValue<Toggle>(),
                tr.Field("tglExprienceNormal").GetValue<Toggle>(),
                tr.Field("tglLewdnessNormal").GetValue<Toggle>()
            };
            statusToggles = new[] {
                tr.Field("tglSafeNormal").GetValue<Toggle>(),
                tr.Field("tglDangerNormal").GetValue<Toggle>()
            };
            if (member == null || normalToggle == null || nativeMultiToggle == null || darkToggle == null || femaleToggleTemplate == null ||
                normalPanel == null || enterButton == null || femaleButton == null || maleButton == null || femaleCard == null || maleCard == null) {
                log.LogError("MultiTeamH could not find the RX18 normal Free H controls.");
                enabled = false;
                return false;
            }

            FindNormalHeader();
            CreateModeToggle();
            CreateSlotToggles();
            teamStage[0] = teamStage[1] = member.resultStage1.Value;
            teamStatus[0] = teamStatus[1] = member.resultStatus.Value;
            MultiTeamSelection.ResolveRemembered();
            secondaryFemale = MultiTeamSelection.RememberedFemale;
            secondaryMale = MultiTeamSelection.RememberedMale;
            if (MultiTeamSelection.HasRememberedSettings) {
                teamStage[1] = MultiTeamSelection.RememberedStage;
                teamStatus[1] = MultiTeamSelection.RememberedStatus;
            }
            femaleButton.onClick.AddListener(OnFemaleSelectClicked);
            maleButton.onClick.AddListener(OnMaleSelectClicked);
            enterButton.onClick.AddListener(OnEnterClicked);
            Canvas.willRenderCanvases += RefreshNativeText;
            log.LogInfo("MultiTeamH added the native 多P Free H option based on the normal panel.");
            StartCoroutine(ProbeModeToggle());
            return true;
        }

        private IEnumerator ProbeModeToggle() {
            yield return null;
            DumpModeToggle("next-frame");
            yield return new WaitForSeconds(2f);
            DumpModeToggle("after-2s");
        }

        private void DumpModeToggle(string reason) {
            if (multiToggle == null) return;
            RectTransform rect = multiToggle.transform as RectTransform;
            DiagnosticProbe.Info("mode-toggle " + reason + " active=" + multiToggle.gameObject.activeInHierarchy + " on=" + multiToggle.isOn +
                " sibling=" + multiToggle.transform.GetSiblingIndex() + " rect=" + (rect == null ? "null" : rect.rect.ToString()) +
                " pos=" + (rect == null ? "null" : rect.anchoredPosition.ToString()));
            Graphic[] graphics = multiToggle.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++) {
                Graphic graphic = graphics[i];
                TMP_Text tmp = graphic as TMP_Text;
                Text text = graphic as Text;
                Image image = graphic as Image;
                RawImage raw = graphic as RawImage;
                string detail = tmp != null ? " text='" + tmp.text + "' font=" + (tmp.font == null ? "null" : tmp.font.name) :
                    text != null ? " text='" + text.text + "' font=" + (text.font == null ? "null" : text.font.name) :
                    image != null ? " sprite=" + (image.overrideSprite == null ? "null" : image.overrideSprite.name) :
                    raw != null ? " texture=" + (raw.texture == null ? "null" : raw.texture.name) : string.Empty;
                DiagnosticProbe.Info("mode-graphic path=" + RelativePath(graphic.transform, multiToggle.transform) + " type=" + graphic.GetType().Name +
                    " enabled=" + graphic.enabled + " active=" + graphic.gameObject.activeInHierarchy + " raycast=" + graphic.raycastTarget + detail);
            }
        }

        private static string RelativePath(Transform value, Transform root) {
            string result = value.name;
            while (value.parent != null && value != root) { value = value.parent; if (value != root) result = value.name + "/" + result; }
            return result;
        }

        private void FindNormalHeader() {
            TMP_Text[] labels = normalPanel.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++) {
                string value = labels[i].text ?? string.Empty;
                if (value.Contains("选择女生") || value.Contains("女の子選択") || value.Contains("女子選択")) {
                    normalHeader = labels[i];
                    normalHeaderText = value;
                    break;
                }
            }
        }

        private void CreateModeToggle() {
            // Clone the native Normal button, then remove only the baked label
            // from the clone. This preserves the exact native border/gradient.
            GameObject clone = Instantiate(normalToggle.gameObject, darkToggle.transform.parent, false);
            clone.name = "MultiTeamH_ModeToggle";
            clone.transform.SetSiblingIndex(darkToggle.transform.GetSiblingIndex());
            multiToggle = clone.GetComponent<Toggle>();
            multiToggle.onValueChanged = new Toggle.ToggleEvent();
            multiToggle.isOn = false;
            multiToggle.interactable = true;
            CreateStableModeLabel(clone);
            multiToggle.onValueChanged.AddListener(OnModeChanged);
            ReflowModeToggles();
            clone.transform.localScale = Vector3.one;
            CanvasGroup[] groups = clone.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++) {
                groups[i].alpha = 1f;
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }
            clone.SetActive(true);
        }

        private void ReflowModeToggles() {
            Transform parent = nativeMultiToggle.transform.parent;
            if (parent.GetComponent<HorizontalLayoutGroup>() != null || parent.GetComponent<GridLayoutGroup>() != null) return;
            Traverse tr = Traverse.Create(scene);
            Toggle[] toggles = {
                tr.Field("tglNormal3P").GetValue<Toggle>(),
                tr.Field("tglMasturbation3P").GetValue<Toggle>(),
                tr.Field("tglLesbian3P").GetValue<Toggle>(),
                nativeMultiToggle,
                multiToggle,
                darkToggle
            };
            for (int i = 0; i < toggles.Length; i++) if (toggles[i] == null) return;
            RectTransform first = toggles[0].transform as RectTransform;
            RectTransform last = darkToggle.transform as RectTransform;
            if (first == null || last == null) return;
            float left = first.anchoredPosition.x - first.rect.width * first.pivot.x;
            float right = last.anchoredPosition.x + last.rect.width * (1f - last.pivot.x);
            float gap = Mathf.Max(2f, (last.anchoredPosition.x - first.anchoredPosition.x - first.rect.width * 4f) / 4f);
            float width = Mathf.Max(72f, (right - left - gap * 5f) / 6f);
            for (int i = 0; i < toggles.Length; i++) {
                RectTransform rect = toggles[i].transform as RectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                Vector2 position = rect.anchoredPosition;
                position.x = left + width * rect.pivot.x + i * (width + gap);
                rect.anchoredPosition = position;
            }
        }

        private void CreateSlotToggles() {
            slotToggles = new Toggle[2];
            string[] labels = { "队1", "队2" };
            RectTransform panelRect = normalPanel.transform as RectTransform;
            float width = 118f;
            float height = 38f;
            for (int i = 0; i < slotToggles.Length; i++) {
                GameObject clone = Instantiate(femaleToggleTemplate.gameObject, normalPanel.transform, false);
                clone.name = "MultiTeamH_Slot" + i;
                Toggle toggle = clone.GetComponent<Toggle>();
                toggle.group = null;
                toggle.onValueChanged = new Toggle.ToggleEvent();
                SetToggleWithoutNotify(toggle, false);
                int slot = i;
                toggle.onValueChanged.AddListener(delegate(bool on) { if (on) SelectSlot(slot); });
                RectTransform rect = clone.transform as RectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2((i - 0.5f) * (width + 10f), -18f);
                rect.localScale = Vector3.one;
                SetToggleLabel(clone, labels[i]);
                clone.SetActive(false);
                slotToggles[i] = toggle;
            }
            selectedSlot = 0;
            SetToggleWithoutNotify(slotToggles[0], true);
            SetToggleWithoutNotify(slotToggles[1], false);
        }

        private static void SetToggleWithoutNotify(Toggle toggle, bool value) {
            if (toggle == null) return;
            FieldInfo field = AccessTools.Field(typeof(Toggle), "m_IsOn");
            if (field != null) field.SetValue(toggle, value);
            else toggle.isOn = value;
            MethodInfo effect = AccessTools.Method(typeof(Toggle), "PlayEffect");
            if (effect != null) effect.Invoke(toggle, new object[] { true });
        }

        private void SelectSlot(int slot) {
            SaveCurrentSlotSettings();
            selectedSlot = Mathf.Clamp(slot, 0, 1);
            for (int i = 0; i < slotToggles.Length; i++) {
                if (i != selectedSlot && slotToggles[i].isOn) slotToggles[i].isOn = false;
            }
            member.resultStage1.Value = teamStage[selectedSlot];
            member.resultStatus.Value = teamStatus[selectedSlot];
            for (int i = 0; i < stageToggles.Length; i++) if (stageToggles[i] != null) stageToggles[i].isOn = i == teamStage[selectedSlot];
            for (int i = 0; i < statusToggles.Length; i++) if (statusToggles[i] != null) statusToggles[i].isOn = i == teamStatus[selectedSlot];
            RefreshCards();
        }

        private void RefreshCards() {
            bool secondTeam = selectedSlot == 1;
            SaveData.Heroine female = secondTeam ? secondaryFemale : member.resultHeroine.Value;
            SaveData.Player male = secondTeam ? secondaryMale : member.resultPlayer.Value;
            if (female == null) femaleCard.Clear(); else femaleCard.SetCharaInfo(female.charFile);
            if (male == null) maleCard.Clear(); else maleCard.SetCharaInfo(male.charFile);
        }

        private void OnModeChanged(bool selected) {
            if (!selected) SaveCurrentSlotSettings();
            IsSelected = selected;
            for (int i = 0; i < slotToggles.Length; i++) slotToggles[i].gameObject.SetActive(selected);
            if (normalHeader != null) normalHeader.text = selected ? "选择角色" : normalHeaderText;
            if (!selected) return;
            Traverse.Create(scene).Field("mode").SetValue(0);
            Traverse.Create(scene).Method("SetMainCanvasObject", new object[] { 0 }).GetValue();
            normalPanel.SetActive(true);
            SelectSlot(0);
        }

        private void OnFemaleSelectClicked() {
            if (!IsSelected || selectedSlot != 1) return;
            primaryFemaleBeforeSelection = member.resultHeroine.Value;
            awaitingSecondaryFemale = true;
        }

        private void OnMaleSelectClicked() {
            if (!IsSelected || selectedSlot != 1) return;
            primaryMaleBeforeSelection = member.resultPlayer.Value;
            awaitingSecondaryMale = true;
        }

        private void LateUpdate() {
            // Re-add the render callback so it runs after localization callbacks
            // that restore the cloned toggle's original “一般” label.
            Canvas.willRenderCanvases -= RefreshNativeText;
            Canvas.willRenderCanvases += RefreshNativeText;
            RefreshNativeText();
            if (!IsSelected || member == null) return;
            if (awaitingSecondaryFemale && member.resultHeroine.Value != null && !ReferenceEquals(member.resultHeroine.Value, primaryFemaleBeforeSelection)) {
                secondaryFemale = member.resultHeroine.Value;
                member.resultHeroine.Value = primaryFemaleBeforeSelection;
                awaitingSecondaryFemale = false;
                MultiTeamSelection.Remember(secondaryFemale, secondaryMale, teamStage[1], teamStatus[1]);
                RefreshCards();
            }
            if (awaitingSecondaryMale && member.resultPlayer.Value != null && !ReferenceEquals(member.resultPlayer.Value, primaryMaleBeforeSelection)) {
                secondaryMale = member.resultPlayer.Value;
                member.resultPlayer.Value = primaryMaleBeforeSelection;
                awaitingSecondaryMale = false;
                MultiTeamSelection.Remember(secondaryFemale, secondaryMale, teamStage[1], teamStatus[1]);
                RefreshCards();
            }
            bool ready = member.resultHeroine.Value != null && secondaryFemale != null && member.resultPlayer.Value != null && secondaryMale != null && member.resultMapInfo.Value != null;
            if (!ready) enterButton.interactable = false;
        }

        private void RefreshNativeText() {
            if (clonedModeTmpLabels != null) {
                for (int i = 0; i < clonedModeTmpLabels.Length; i++) {
                    TMP_Text label = clonedModeTmpLabels[i];
                    if (label != null && label != multiModeLabel) label.enabled = false;
                }
            }
            if (clonedModeTextLabels != null) {
                for (int i = 0; i < clonedModeTextLabels.Length; i++) if (clonedModeTextLabels[i] != null) clonedModeTextLabels[i].enabled = false;
            }
            RefreshModeCover();
            if (multiModeLabel != null) { multiModeLabel.enabled = true; multiModeLabel.text = "多P"; }
            if (slotToggles != null) {
                string[] labels = { "队1", "队2" };
                for (int i = 0; i < slotToggles.Length; i++) if (slotToggles[i] != null) SetToggleLabel(slotToggles[i].gameObject, labels[i]);
            }
            if (IsSelected && normalHeader != null) normalHeader.text = "选择角色";
        }

        private void CreateStableModeLabel(GameObject button) {
            clonedModeTmpLabels = button.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text template = clonedModeTmpLabels.Length == 0 ? null : clonedModeTmpLabels[0];
            for (int i = 0; i < clonedModeTmpLabels.Length; i++) clonedModeTmpLabels[i].enabled = false;
            clonedModeTextLabels = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < clonedModeTextLabels.Length; i++) clonedModeTextLabels[i].enabled = false;

            Image[] images = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++) {
                if (images[i].transform.name == "Background") {
                    multiModeNormalSprite = CreateTextlessButtonSprite(images[i].overrideSprite, "MultiTeamH_Normal");
                    images[i].sprite = multiModeNormalSprite;
                    images[i].overrideSprite = multiModeNormalSprite;
                } else if (images[i].transform.name == "Checkmark") {
                    multiModeSelectedSprite = CreateTextlessButtonSprite(images[i].overrideSprite, "MultiTeamH_Selected");
                    images[i].sprite = multiModeSelectedSprite;
                    images[i].overrideSprite = multiModeSelectedSprite;
                }
            }

            GameObject labelObject = new GameObject("MultiTeamH_NativeLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            labelObject.layer = button.layer;
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(button.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(0f, 3.5f);
            rect.SetAsLastSibling();
            multiModeLabel = labelObject.GetComponent<Text>();
            multiModeLabel.raycastTarget = false;
            multiModeLabel.alignment = TextAnchor.MiddleCenter;
            multiModeLabel.text = "多P";
            multiModeLabel.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 28);
            multiModeLabel.fontSize = 24;
            multiModeLabel.fontStyle = FontStyle.Bold;
            multiModeLabel.color = Color.white;
            Outline outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.22f, 0.55f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            multiModeLabel.enabled = true;
        }

        private void RefreshModeCover() {
            // The cloned images already use private, textless sprites.  Keeping
            // this method makes the render callback harmless on older saves.
        }

        private Sprite CreateTextlessButtonSprite(Sprite source, string assetName) {
            // Native button sprites can be tightly packed. Accessing
            // Sprite.textureRect on those throws and used to abort creation of
            // the entire mode entry. A sliced UI sprite has no need to inherit
            // the atlas rectangle, so build it at a stable private size.
            const int width = 256;
            const int height = 64;
            Texture2D texture = null;
            try {
                texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                texture.name = assetName + "_Texture";
                Color32[] pixels = new Color32[width * height];
                bool selected = assetName.IndexOf("Selected", StringComparison.OrdinalIgnoreCase) >= 0;
                Color top = selected ? new Color32(238, 126, 182, 255) : new Color32(73, 116, 226, 255);
                Color bottom = selected ? new Color32(207, 73, 139, 255) : new Color32(28, 52, 151, 255);
                const int radius = 7;
                for (int y = 0; y < height; y++) {
                    Color fill = Color.Lerp(bottom, top, height <= 1 ? 0f : y / (height - 1f));
                    for (int x = 0; x < width; x++) {
                        int dx = x < radius ? radius - x : x >= width - radius ? x - (width - radius - 1) : 0;
                        int dy = y < radius ? radius - y : y >= height - radius ? y - (height - radius - 1) : 0;
                        bool outside = dx > 0 && dy > 0 && dx * dx + dy * dy > radius * radius;
                        if (outside) { pixels[y * width + x] = new Color32(0, 0, 0, 0); continue; }
                        bool border = x <= 1 || y <= 1 || x >= width - 2 || y >= height - 2 ||
                            (dx > 0 && dy > 0 && dx * dx + dy * dy >= (radius - 2) * (radius - 2));
                        pixels[y * width + x] = border ? new Color32(245, 245, 255, 255) : (Color32)fill;
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                float pixelsPerUnit = source == null ? 100f : source.pixelsPerUnit;
                Sprite result = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                result.name = assetName;
                generatedModeAssets.Add(texture);
                generatedModeAssets.Add(result);
                return result;
            } catch (Exception ex) {
                if (texture != null) Destroy(texture);
                log.LogWarning("Could not create the MultiP button texture: " + ex.Message);
                return source;
            }
        }

        private static void SetToggleLabel(GameObject root, string text) {
            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++) tmps[i].text = text;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].text = text;
        }

        private void OnEnterClicked() {
            if (!IsSelected || member.resultHeroine.Value == null || secondaryFemale == null || member.resultPlayer.Value == null || secondaryMale == null) return;
            teamStage[selectedSlot] = member.resultStage1.Value;
            teamStatus[selectedSlot] = member.resultStatus.Value;
            MultiTeamSelection.Remember(secondaryFemale, secondaryMale, teamStage[1], teamStatus[1]);
            MultiTeamSelection.Set(secondaryFemale, secondaryMale, teamStage[1], teamStatus[1]);
            Traverse.Create(scene).Field("mode").SetValue(0);
            OpenHData open = Singleton<Manager.Scene>.Instance.commonSpace.GetComponent<OpenHData>();
            OpenHData.Data data = open == null ? null : open.data as OpenHData.Data;
            if (data == null) {
                log.LogError("MultiTeamH could not find the Free H launch data.");
                MultiTeamSelection.Clear();
                return;
            }
            data.lstFemale = new List<SaveData.Heroine> { member.resultHeroine.Value };
            data.player = member.resultPlayer.Value;
            data.peepCategory = new List<int> { 0 };
            data.isDarkness = false;
            data.stageFreeH1 = teamStage[0];
            data.statusFreeH = teamStatus[0];
            MultiTeamSelection.MarkLaunchDataReady();
        }

        private void OnDestroy() {
            SaveCurrentSlotSettings();
            Canvas.willRenderCanvases -= RefreshNativeText;
            if (normalHeader != null && normalHeaderText != null) normalHeader.text = normalHeaderText;
            if (multiToggle != null) Destroy(multiToggle.gameObject);
            if (slotToggles != null) {
                for (int i = 0; i < slotToggles.Length; i++) if (slotToggles[i] != null) Destroy(slotToggles[i].gameObject);
            }
            for (int i = 0; i < generatedModeAssets.Count; i++) if (generatedModeAssets[i] != null) Destroy(generatedModeAssets[i]);
            if (MultiTeamHPlugin.Instance != null) MultiTeamHPlugin.Instance.FreeHModeEnded(this);
        }

        private void SaveCurrentSlotSettings() {
            if (!IsSelected || member == null || selectedSlot < 0 || selectedSlot >= 2) return;
            teamStage[selectedSlot] = member.resultStage1.Value;
            teamStatus[selectedSlot] = member.resultStatus.Value;
            MultiTeamSelection.Remember(secondaryFemale, secondaryMale, teamStage[1], teamStatus[1]);
        }

    }

    [HarmonyPatch(typeof(ChaFileControl), "LoadCharaFile", new Type[] { typeof(string), typeof(byte), typeof(bool), typeof(bool) })]
    internal static class CardPathHooks {
        [HarmonyPostfix]
        private static void Loaded(ChaFileControl __instance, string filename, bool __result) {
            if (__result) MultiTeamSelection.RecordLoadedCardPath(__instance, filename);
        }
    }

    internal static class MultiTeamSelection {
        private sealed class RecentCardLoad {
            internal WeakReference Card;
            internal string Path;
        }

        private static readonly List<RecentCardLoad> recentCardLoads = new List<RecentCardLoad>();
        private static ConfigEntry<string> femaleCard;
        private static ConfigEntry<string> maleCard;
        private static ConfigEntry<int> stageEntry;
        private static ConfigEntry<int> statusEntry;
        private static ConfigFile config;
        private static SaveData.Heroine secondaryFemale;
        private static SaveData.Player secondaryMale;
        private static int secondaryStage;
        private static int secondaryStatus;
        private static SaveData.Heroine rememberedFemale;
        private static SaveData.Player rememberedMale;
        private static int rememberedStage;
        private static int rememberedStatus;
        private static bool hasRememberedSettings;
        internal static bool HasPending { get; private set; }
        internal static bool LaunchDataReady { get; private set; }
        internal static SaveData.Heroine SecondaryFemale { get { return secondaryFemale; } }
        internal static SaveData.Player SecondaryMale { get { return secondaryMale; } }
        internal static int SecondaryStage { get { return secondaryStage; } }
        internal static int SecondaryStatus { get { return secondaryStatus; } }
        internal static SaveData.Heroine RememberedFemale { get { return rememberedFemale; } }
        internal static SaveData.Player RememberedMale { get { return rememberedMale; } }
        internal static int RememberedStage { get { return rememberedStage; } }
        internal static int RememberedStatus { get { return rememberedStatus; } }
        internal static bool HasRememberedSettings { get { return hasRememberedSettings; } }

        internal static void RecordLoadedCardPath(ChaFileControl card, string path) {
            if (card == null || string.IsNullOrEmpty(path)) return;
            try {
                if (!File.Exists(path)) return;
                for (int i = recentCardLoads.Count - 1; i >= 0; i--) {
                    object target = recentCardLoads[i].Card.Target;
                    if (target == null || ReferenceEquals(target, card)) recentCardLoads.RemoveAt(i);
                }
                recentCardLoads.Add(new RecentCardLoad { Card = new WeakReference(card), Path = Path.GetFullPath(path) });
                if (recentCardLoads.Count > 128) recentCardLoads.RemoveAt(0);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("card path capture failed: " + ex.Message);
            }
        }

        private static string LoadedPath(ChaFileControl card) {
            if (card == null) return null;
            for (int i = recentCardLoads.Count - 1; i >= 0; i--)
                if (ReferenceEquals(recentCardLoads[i].Card.Target, card) && File.Exists(recentCardLoads[i].Path))
                    return recentCardLoads[i].Path;
            return null;
        }

        private static string ResolveCardPath(string saved, bool female) {
            if (string.IsNullOrEmpty(saved)) return null;
            string root = Path.Combine(Path.Combine(Path.Combine(Paths.GameRootPath, "UserData"), "chara"), female ? "female" : "male");
            if (Path.IsPathRooted(saved) && File.Exists(saved)) return Path.GetFullPath(saved);
            if (!Path.IsPathRooted(saved)) {
                string relative = Path.Combine(root, saved);
                if (File.Exists(relative)) return Path.GetFullPath(relative);
            }
            string name = Path.GetFileName(saved);
            if (string.IsNullOrEmpty(name) || !Directory.Exists(root)) return null;
            string direct = Path.Combine(root, name);
            if (File.Exists(direct)) return Path.GetFullPath(direct);
            string[] matches = Directory.GetFiles(root, name, SearchOption.AllDirectories);
            if (matches.Length == 1) return Path.GetFullPath(matches[0]);
            if (matches.Length > 1) MultiTeamHPlugin.LogWarning("Multiple " + (female ? "female" : "male") +
                " cards have the saved filename " + name + "; reselect team 2 to record its exact path.");
            return null;
        }

        internal static void Configure(ConfigEntry<string> female, ConfigEntry<string> male, ConfigEntry<int> stage, ConfigEntry<int> status, ConfigFile owner) {
            femaleCard = female;
            maleCard = male;
            stageEntry = stage;
            statusEntry = status;
            config = owner;
            rememberedStage = stageEntry == null ? 0 : stageEntry.Value;
            rememberedStatus = statusEntry == null ? 0 : statusEntry.Value;
            hasRememberedSettings = (femaleCard != null && !string.IsNullOrEmpty(femaleCard.Value)) ||
                (maleCard != null && !string.IsNullOrEmpty(maleCard.Value));
        }

        internal static void ResolveRemembered() {
            if (rememberedFemale == null && femaleCard != null && !string.IsNullOrEmpty(femaleCard.Value) && Singleton<Manager.Game>.IsInstance()) {
                List<SaveData.Heroine> heroines = Singleton<Manager.Game>.Instance.HeroineList;
                if (heroines != null) for (int i = 0; i < heroines.Count; i++) {
                    SaveData.Heroine value = heroines[i];
                    if (value != null && value.charFile != null && string.Equals(value.charFile.charaFileName, femaleCard.Value, StringComparison.OrdinalIgnoreCase)) {
                        rememberedFemale = value;
                        break;
                    }
                }
            }
            if (rememberedMale == null && maleCard != null && !string.IsNullOrEmpty(maleCard.Value) && Singleton<Manager.Game>.IsInstance()) {
                SaveData.Player value = Singleton<Manager.Game>.Instance.Player;
                if (value != null && value.charFile != null && string.Equals(value.charFile.charaFileName, maleCard.Value, StringComparison.OrdinalIgnoreCase)) rememberedMale = value;
            }
            if (rememberedFemale == null && femaleCard != null && !string.IsNullOrEmpty(femaleCard.Value)) {
                try {
                    string path = ResolveCardPath(femaleCard.Value, true);
                    ChaFileControl file = new ChaFileControl();
                    if (path != null && file.LoadCharaFile(path, 0xff, false, true)) {
                        rememberedFemale = new SaveData.Heroine(file, false);
                        if (femaleCard.Value != path) femaleCard.Value = path;
                        MultiTeamHPlugin.LogInfo("Restored team 2 female card from " + path);
                    } else MultiTeamHPlugin.LogWarning("Could not restore team 2 female card: " + femaleCard.Value);
                } catch (Exception ex) { MultiTeamHPlugin.LogWarning("Could not load remembered team 2 female: " + ex.Message); }
            }
            if (rememberedMale == null && maleCard != null && !string.IsNullOrEmpty(maleCard.Value)) {
                try {
                    string path = ResolveCardPath(maleCard.Value, false);
                    ChaFileControl file = new ChaFileControl();
                    if (path != null && file.LoadCharaFile(path, 0xff, false, true)) {
                        rememberedMale = new SaveData.Player(file, false);
                        if (maleCard.Value != path) maleCard.Value = path;
                        MultiTeamHPlugin.LogInfo("Restored team 2 male card from " + path);
                    } else MultiTeamHPlugin.LogWarning("Could not restore team 2 male card: " + maleCard.Value);
                } catch (Exception ex) { MultiTeamHPlugin.LogWarning("Could not load remembered team 2 male: " + ex.Message); }
            }
            hasRememberedSettings = rememberedFemale != null || rememberedMale != null || hasRememberedSettings;
            if (config != null) config.Save();
        }

        internal static void Remember(SaveData.Heroine female, SaveData.Player male, int stage, int status) {
            if (female != null) rememberedFemale = female;
            if (male != null) rememberedMale = male;
            rememberedStage = stage;
            rememberedStatus = status;
            hasRememberedSettings = rememberedFemale != null || rememberedMale != null;
            if (femaleCard != null && female != null && female.charFile != null) {
                string name = female.charFile.charaFileName ?? string.Empty;
                femaleCard.Value = LoadedPath(female.charFile) ?? ResolveCardPath(name, true) ?? name;
            }
            if (maleCard != null && male != null && male.charFile != null) {
                string name = male.charFile.charaFileName ?? string.Empty;
                maleCard.Value = LoadedPath(male.charFile) ?? ResolveCardPath(name, false) ?? name;
            }
            if (stageEntry != null) stageEntry.Value = stage;
            if (statusEntry != null) statusEntry.Value = status;
            if (config != null) config.Save();
        }

        internal static void Set(SaveData.Heroine female, SaveData.Player male, int stage, int status) {
            secondaryFemale = female;
            secondaryMale = male;
            secondaryStage = stage;
            secondaryStatus = status;
            HasPending = female != null && male != null;
            LaunchDataReady = false;
        }

        internal static void MarkLaunchDataReady() { LaunchDataReady = HasPending; }

        internal static void Complete() {
            secondaryFemale = null;
            secondaryMale = null;
            secondaryStage = 0;
            secondaryStatus = 0;
            HasPending = false;
            LaunchDataReady = false;
        }

        internal static void Clear() { Complete(); }
    }
}
