using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace FemboyMode
{
    /// <summary>
    /// Attach a native Unity UI marker to the same HScene Canvas that
    /// contains the heroine gauge. Earlier versions parented the marker
    /// under gauge.imgBar.parent, which can be clipped by a fill Mask.
    /// </summary>
    internal static class FemboyHMarkerHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void OnSpriteInitialized(HSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null)
                return;

            FemboyHMarker marker =
                __instance.gameObject.GetComponent<FemboyHMarker>();
            if (marker == null)
                marker = __instance.gameObject.AddComponent<FemboyHMarker>();
            marker.Initialize(__instance);
        }
    }

    public sealed class FemboyHMarker : MonoBehaviour
    {
        private HSprite sprite;
        private GameObject marker;
        private Text label;
        private Canvas canvas;
        private RectTransform source;
        private RectTransform canvasRect;
        private RectTransform markerRect;
        private float nextSearchTime;
        private bool reported;

        public void Initialize(HSprite sceneSprite)
        {
            sprite = sceneSprite;
            TryCreate();
        }

        private void LateUpdate()
        {
            if (sprite == null || !sprite.flags)
            {
                if (marker != null) marker.SetActive(false);
                return;
            }

            if (marker == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;
                TryCreate();
            }

            if (marker == null || canvasRect == null || source == null)
                return;

            bool visible = sprite.flags.isFreeH &&
                FemboyModePlugin.ShowFreeHMarker != null &&
                FemboyModePlugin.ShowFreeHMarker.Value;
            if (visible)
            {
                var heroine = sprite.GetLeadingHeroine();
                visible = heroine != null &&
                    FemboyModePlugin.IsFemboy(heroine.chaCtrl);
            }

            if (marker.activeSelf != visible)
                marker.SetActive(visible);
            if (!visible) return;

            // Position relative to the ORIGINAL female H gauge, but draw
            // at the top native Canvas level so Mask / RectMask2D used to
            // clip the gauge fill cannot hide our marker.
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                camera, corners[1]);
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, camera, out local))
            {
                markerRect.anchoredPosition = local +
                    new Vector2(26f, -16f);
            }
        }

        private void TryCreate()
        {
            if (sprite == null || marker != null)
                return;

            if (sprite.gauge != null && sprite.gauge.Length > 0 &&
                sprite.gauge[0] != null && sprite.gauge[0].imgBar != null)
                source = sprite.gauge[0].imgBar.rectTransform;

            if (source == null && sprite.rawFace != null)
                source = sprite.rawFace.rectTransform;
            if (source == null)
                return;

            Canvas[] ancestors = source.GetComponentsInParent<Canvas>(true);
            // Use the outermost H Canvas. A nested Canvas or mask may clip
            // the gauge bar and our old badge was invisible to the user.
            canvas = ancestors == null || ancestors.Length == 0
                ? null : ancestors[ancestors.Length - 1];
            if (canvas == null || canvas.transform == null)
                return;

            canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
                return;

            marker = new GameObject("FemboyMode_HeroineGaugeMark",
                typeof(RectTransform), typeof(Text));
            marker.transform.SetParent(canvasRect, false);
            marker.transform.SetAsLastSibling();
            marker.SetActive(false);

            markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(54f, 28f);

            label = marker.GetComponent<Text>();
            Text nativeText = source.GetComponentInParent<Text>();
            if (nativeText == null)
            {
                Text[] texts = canvas.GetComponentsInChildren<Text>(true);
                foreach (Text candidate in texts)
                {
                    if (candidate != null && candidate.font != null)
                    {
                        nativeText = candidate;
                        break;
                    }
                }
            }

            Font font = nativeText != null ? nativeText.font :
                Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                Object.Destroy(marker);
                marker = null;
                return;
            }

            label.font = font;
            // Never render an unsupported Unicode symbol as a blank box.
            label.text = font.HasCharacter('⚥') ? "⚥" :
                font.HasCharacter('♂') ? "♂" : "F";
            label.fontSize = 23;
            label.fontStyle = FontStyle.Bold;
            Color nativeColor = sprite.gauge != null &&
                sprite.gauge.Length > 0 && sprite.gauge[0] != null &&
                sprite.gauge[0].imgBar != null
                ? sprite.gauge[0].imgBar.color
                : (nativeText != null ? nativeText.color : Color.white);
            // Some native text templates have alpha=0 while inactive.
            // Use the visible heroine gauge color but force an opaque
            // glyph, with the black outline for contrast.
            nativeColor.a = 1f;
            label.color = nativeColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            Outline outline = marker.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            if (!reported)
            {
                reported = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Native H gauge marker attached to Canvas '" + canvas.name +
                    "' beside '" + source.name + "', glyph=" + label.text);
            }
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }
    }
}
