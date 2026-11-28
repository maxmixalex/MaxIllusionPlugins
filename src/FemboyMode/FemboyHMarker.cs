using System;
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace FemboyMode
{
    internal static class FemboyHMarkerHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void Initialize(HSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            FemboyHMarker marker =
                __instance.gameObject.GetComponent<FemboyHMarker>();
            if (marker == null)
                marker = __instance.gameObject.AddComponent<FemboyHMarker>();
            marker.Bind(__instance);
        }
    }

    /// <summary>
    /// A 22px circular sex marker alongside the original portrait-status
    /// chips. No duplicate text banner, opaque 73px panel, or enormous icon.
    /// Placed at the portrait top-left + approximately 65px to sit directly
    /// after the native "淫乱" style chip in the RX18 H HUD.
    /// </summary>
    public sealed class FemboyHMarker : MonoBehaviour
    {
        private HSprite sprite;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform portrait;
        private RectTransform markerRect;
        private GameObject marker;
        private bool? lastVisible;
        private bool positionLogged;
        private float nextRetry;
        private static Sprite circleSprite;
        private static readonly Vector3[] Corners = new Vector3[4];

        internal void Bind(HSprite value)
        {
            sprite = value;
            Create();
        }

        private void LateUpdate()
        {
            if (sprite == null || sprite.flags == null)
            {
                if (marker != null) marker.SetActive(false);
                return;
            }

            if (marker == null && Time.unscaledTime >= nextRetry)
            {
                nextRetry = Time.unscaledTime + 1f;
                Create();
            }
            if (marker == null || portrait == null || canvasRect == null)
                return;

            bool visible = FemboyModePlugin.ShowFreeHMarker != null &&
                FemboyModePlugin.ShowFreeHMarker.Value &&
                sprite.flags.isFreeH && IsMarked();
            if (marker.activeSelf != visible) marker.SetActive(visible);
            if (lastVisible != visible)
            {
                lastVisible = visible;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Femboy HUD circle visible=" + visible +
                    " anchor=" + portrait.name);
            }
            if (!visible) return;

            // Read position from the actual native portrait; never from
            // a variable-width gauge fill or its clipped progress bars.
            portrait.GetWorldCorners(Corners);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                camera, Corners[1]);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, camera, out local))
                return;

            // The native status chip extends along the left 50-60px of the
            // portrait top edge. This little circle belongs immediately next
            // to it rather than floating on top of the portrait/gauge.
            float x = Mathf.Min(67f,
                Mathf.Max(36f, portrait.rect.width * 0.52f));
            Vector2 target = local + new Vector2(x, -10.5f);
            Rect rect = canvasRect.rect;
            const float half = 11f;
            markerRect.anchoredPosition = new Vector2(
                Mathf.Clamp(target.x, rect.xMin + half + 2f,
                    rect.xMax - half - 2f),
                Mathf.Clamp(target.y, rect.yMin + half + 2f,
                    rect.yMax - half - 2f));
            if (!positionLogged)
            {
                positionLogged = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Femboy HUD circle positioned near portrait status: " +
                    markerRect.anchoredPosition +
                    " size=22 icon=centred procedural male glyph");
            }
        }

        private bool IsMarked()
        {
            SaveData.Heroine heroine = sprite.GetLeadingHeroine();
            if (heroine != null && heroine.chaCtrl != null &&
                FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return true;
            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                HSceneProc current = scenes[i];
                if (current != null && current.sprite == sprite &&
                    current.lstFemale != null &&
                    current.lstFemale.Count == 1 &&
                    current.lstFemale[0] != null &&
                    FemboyModePlugin.IsFemboy(current.lstFemale[0]))
                    return true;
            }
            return false;
        }

        // Crisp circular medallion, same teal/cream/ink palette as the
        // game's original top-left status chips. Generated once in memory;
        // no png or external fonts need to ship in the plugin.
        private static Sprite Circle()
        {
            if (circleSprite != null) return circleSprite;
            const int side = 64;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32,
                false);
            texture.name = "FemboyMode_NativeStatusCircle";
            var pixels = new Color32[side * side];
            Color32 inner = new Color32(18, 119, 148, 255);
            Color32 rim = new Color32(237, 246, 239, 255);
            Color32 outer = new Color32(21, 43, 64, 255);
            Color32 maleGlyph = new Color32(246, 251, 251, 255);
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dx = x + 0.5f - side * 0.5f;
                    float dy = y + 0.5f - side * 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 color = radius <= 25.0f ? inner :
                        radius <= 28.0f ? rim :
                        radius <= 30.0f ? outer :
                        new Color32(0, 0, 0, 0);
                    // Draw the complete male symbol inside the *same*
                    // centered sprite. Fonts have unequal glyph bearings
                    // and baselines, so TextAnchor.MiddleCenter was never
                    // true optical centering on 22px HUD chips.
                    if (radius <= 25f &&
                        FemboyRules.MaleMarkerGlyphPixel(x, y))
                        color = maleGlyph;
                    pixels[y * side + x] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            circleSprite = Sprite.Create(texture,
                new Rect(0, 0, side, side), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }

        private void Create()
        {
            if (sprite == null || marker != null) return;
            portrait = sprite.rawFace == null ? null :
                sprite.rawFace.rectTransform;
            if (portrait == null) return;
            Canvas[] ancestors = portrait.GetComponentsInParent<Canvas>(true);
            if (ancestors == null || ancestors.Length == 0) return;
            canvas = ancestors[ancestors.Length - 1];
            canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;

            marker = new GameObject("FemboyMode_StatusSexIcon",
                typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(canvasRect, false);
            marker.transform.SetAsLastSibling();
            markerRect = marker.GetComponent<RectTransform>();
            markerRect.anchorMin = markerRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(22f, 22f);
            Image image = marker.GetComponent<Image>();
            image.sprite = Circle();
            image.color = Color.white;
            image.raycastTarget = false;
            marker.SetActive(false);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }
    }
}
