using System;
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace FemboyMode
{
    /// <summary>
    /// Build a visible, native-looking heroine badge from the current H UI's
    /// own sprite/font, rather than draw an unrelated custom icon or rely on
    /// an unsupported Unicode gender glyph.
    /// </summary>
    internal static class FemboyHMarkerHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void OnSpriteInitialized(HSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            var marker = __instance.gameObject.GetComponent<FemboyHMarker>();
            if (marker == null)
                marker = __instance.gameObject.AddComponent<FemboyHMarker>();
            marker.Initialize(__instance);
        }
    }

    public sealed class FemboyHMarker : MonoBehaviour
    {
        private HSprite sprite;
        private GameObject badge;
        private Image background;
        private Text caption;
        private RectTransform badgeRect;
        private RectTransform canvasRect;
        private RectTransform portraitRect;
        private Canvas canvas;
        private float nextSearchTime;
        private bool? lastVisible;
        private bool reportedPosition;
        private static readonly Vector3[] Corners = new Vector3[4];

        internal void Initialize(HSprite sceneSprite)
        {
            sprite = sceneSprite;
            TryCreate();
        }

        private void LateUpdate()
        {
            if (sprite == null || sprite.flags == null)
            {
                if (badge != null) badge.SetActive(false);
                return;
            }
            if ((badge == null || portraitRect == null) &&
                Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;
                TryCreate();
            }

            if (badge == null || portraitRect == null || canvasRect == null)
                return;

            bool visible = IsMarkedScene();
            if (badge.activeSelf != visible) badge.SetActive(visible);
            if (lastVisible != visible)
            {
                lastVisible = visible;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Native heroine badge visible=" + visible +
                    " freeH=" + sprite.flags.isFreeH +
                    " source=" + portraitRect.name +
                    " canvas=" + canvas.name);
            }
            if (!visible) return;

            // The old glyph was anchored to the gauge's fill Bar. A fill
            // image can be masked, tiny or off-screen depending on HUD scale.
            // Anchor to the portrait, draw on the *outer* canvas, and clamp
            // the badge to the visible canvas rectangle.
            portraitRect.GetWorldCorners(Corners);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                camera, Corners[2]);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, camera, out local))
                return;

            float halfWidth = badgeRect.rect.width * 0.5f;
            float halfHeight = badgeRect.rect.height * 0.5f;
            Vector2 desired = local + new Vector2(halfWidth + 4f,
                -halfHeight - 3f);
            Rect c = canvasRect.rect;
            badgeRect.anchoredPosition = new Vector2(
                Mathf.Clamp(desired.x, c.xMin + halfWidth + 3f,
                    c.xMax - halfWidth - 3f),
                Mathf.Clamp(desired.y, c.yMin + halfHeight + 3f,
                    c.yMax - halfHeight - 3f));

            if (!reportedPosition)
            {
                reportedPosition = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Native heroine badge drawn: label='" + caption.text +
                    "' position=" + badgeRect.anchoredPosition +
                    " size=" + badgeRect.rect.size +
                    " canvasRect=" + c +
                    " portrait=" + portraitRect.name);
            }
        }

        private bool IsMarkedScene()
        {
            if (sprite.flags == null || !sprite.flags.isFreeH ||
                FemboyModePlugin.ShowFreeHMarker == null ||
                !FemboyModePlugin.ShowFreeHMarker.Value)
                return false;

            SaveData.Heroine heroine = sprite.GetLeadingHeroine();
            if (heroine != null && heroine.chaCtrl != null &&
                FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return true;

            // In some H modes the sprite's own leading-heroine pointer is
            // stale even though the live HSceneProc has a marked female.
            HSceneProc[] processes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < processes.Length; i++)
            {
                HSceneProc process = processes[i];
                if (process != null && process.sprite == sprite &&
                    process.lstFemale != null && process.lstFemale.Count == 1 &&
                    process.lstFemale[0] != null &&
                    FemboyModePlugin.IsFemboy(process.lstFemale[0]))
                    return true;
            }
            return false;
        }

        private static Font FindNativeFont(Canvas owner, bool chinese)
        {
            Text[] texts = owner.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Font candidate = texts[i] == null ? null : texts[i].font;
                if (candidate == null) continue;
                if (!chinese || candidate.HasCharacter('伪'))
                    return candidate;
            }
            try
            {
                Font fallback = Font.CreateDynamicFontFromOSFont(
                    chinese ? new[] { "Microsoft YaHei UI", "Microsoft YaHei",
                        "Noto Sans CJK SC", "Yu Gothic UI", "Arial" } :
                        new[] { "Arial", "Yu Gothic UI" }, 18);
                if (fallback != null) return fallback;
            }
            catch (Exception) { }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void TryCreate()
        {
            if (sprite == null || badge != null) return;
            // Prefer the actual heroine avatar in the top-left native H HUD.
            portraitRect = sprite.rawFace == null ? null :
                sprite.rawFace.rectTransform;
            if (portraitRect == null && sprite.gauge != null &&
                sprite.gauge.Length > 0 && sprite.gauge[0] != null &&
                sprite.gauge[0].imgBar != null)
                portraitRect = sprite.gauge[0].imgBar.rectTransform;
            if (portraitRect == null) return;

            Canvas[] ancestors = portraitRect.GetComponentsInParent<Canvas>(true);
            if (ancestors == null || ancestors.Length == 0) return;
            canvas = ancestors[ancestors.Length - 1];
            canvasRect = canvas == null ? null :
                canvas.transform as RectTransform;
            if (canvasRect == null) return;

            bool chinese = Type.GetType(
                "KK_Chinese.Core.Patch.Text_Display, KK_Chinese", false) != null;
            Font font = FindNativeFont(canvas, chinese);
            if (font == null) return;

            badge = new GameObject("FemboyMode_NativeHeroineBadge",
                typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            badge.transform.SetParent(canvasRect, false);
            badge.transform.SetAsLastSibling();
            badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(chinese ? 73f : 90f, 27f);

            CanvasGroup group = badge.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            background = badge.GetComponent<Image>();
            Image nativeFrame = portraitRect.GetComponentInParent<Image>();
            Image nativeGauge = sprite.gauge != null &&
                sprite.gauge.Length > 0 && sprite.gauge[0] != null
                ? sprite.gauge[0].imgBar : null;
            background.sprite = nativeFrame != null &&
                nativeFrame.sprite != null ? nativeFrame.sprite :
                nativeGauge == null ? null : nativeGauge.sprite;
            background.type = background.sprite != null &&
                background.sprite.border.sqrMagnitude > 0f ?
                Image.Type.Sliced : Image.Type.Simple;
            Color accent = nativeGauge == null ? new Color(0.27f, 0.64f, 0.89f) :
                nativeGauge.color;
            background.color = Color.Lerp(new Color(0.06f, 0.15f, 0.24f, 1f),
                new Color(accent.r, accent.g, accent.b, 1f), 0.38f);
            background.color = new Color(background.color.r,
                background.color.g, background.color.b, 0.98f);
            background.raycastTarget = false;
            Outline border = badge.AddComponent<Outline>();
            border.effectColor = new Color(0.04f, 0.06f, 0.09f, 0.92f);
            border.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject textObject = new GameObject("NativeBadgeLabel",
                typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(badgeRect, false);
            RectTransform labelRect = textObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(3f, 1f);
            labelRect.offsetMax = new Vector2(-3f, -1f);
            caption = textObject.GetComponent<Text>();
            caption.font = font;
            caption.text = (font.HasCharacter('♂') ? "♂ " : "") +
                (chinese && font.HasCharacter('伪') ? "伪娘" : "FEMBOY");
            caption.fontSize = chinese ? 16 : 14;
            caption.fontStyle = FontStyle.Bold;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = new Color(1f, 0.98f, 0.87f, 1f);
            caption.raycastTarget = false;
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            Outline textOutline = textObject.AddComponent<Outline>();
            textOutline.effectColor = new Color(0.03f, 0.03f, 0.06f, 0.95f);
            textOutline.effectDistance = new Vector2(1f, -1f);

            badge.SetActive(false);
            FemboyModePlugin.LogOptionalIntegrationProblem(
                "Native H heroine badge created with portrait anchor='" +
                portraitRect.name + "', font='" + font.name +
                "', text='" + caption.text + "'; visibility checked per frame.");
        }

        private void OnDestroy()
        {
            if (badge != null) Destroy(badge);
        }
    }
}
