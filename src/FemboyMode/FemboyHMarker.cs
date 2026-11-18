using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace FemboyMode
{
    /// <summary>
    /// An element of the game's existing H UI, NOT an IMGUI overlay.
    /// The native female gauge is the upper-left HUD reference point;
    /// the symbol inherits the font/color from existing Unity UI text.
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
        private GameObject mark;
        private Text label;

        public void Initialize(HSprite sceneSprite)
        {
            sprite = sceneSprite;
            EnsureNativeHudMark();
        }

        private void LateUpdate()
        {
            if (sprite == null)
            {
                if (mark != null) mark.SetActive(false);
                return;
            }

            if (mark == null)
                EnsureNativeHudMark();
            if (mark == null)
                return;

            bool visible = sprite.flags != null && sprite.flags.isFreeH &&
                FemboyModePlugin.ShowFreeHMarker != null &&
                FemboyModePlugin.ShowFreeHMarker.Value;

            if (visible)
            {
                var heroine = sprite.GetLeadingHeroine();
                visible = heroine != null &&
                    FemboyModePlugin.IsFemboy(heroine.chaCtrl);
            }

            if (mark.activeSelf != visible)
                mark.SetActive(visible);
        }

        private void EnsureNativeHudMark()
        {
            if (sprite == null || mark != null)
                return;

            // Attach directly to the existing female gauge's RectTransform.
            // It inherits the native Canvas, scaling, visibility and layout.
            RectTransform nativeParent = null;
            if (sprite.gauge != null && sprite.gauge.Length > 0 &&
                sprite.gauge[0] != null && sprite.gauge[0].imgBar != null)
                nativeParent = sprite.gauge[0].imgBar.transform.parent as RectTransform;

            if (nativeParent == null && sprite.rawFace != null)
                nativeParent = sprite.rawFace.transform as RectTransform;
            if (nativeParent == null)
                return;

            mark = new GameObject("FemboyMode_GaugeSymbol",
                typeof(RectTransform), typeof(Text));
            mark.transform.SetParent(nativeParent, false);
            mark.SetActive(false);
            RectTransform rect = mark.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(25f, 25f);
            rect.anchoredPosition = new Vector2(-10f, 4f);

            label = mark.GetComponent<Text>();
            Text nativeLabel = null;
            Text[] labels = sprite.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i] != label &&
                    labels[i].font != null)
                {
                    nativeLabel = labels[i];
                    break;
                }
            }

            label.font = nativeLabel != null ? nativeLabel.font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = label.font != null && label.font.HasCharacter('⚥')
                ? "⚥" : "♂";
            label.fontSize = nativeLabel != null
                ? Mathf.Max(15, nativeLabel.fontSize) : 19;
            label.fontStyle = nativeLabel != null
                ? nativeLabel.fontStyle : FontStyle.Bold;
            label.color = nativeLabel != null
                ? nativeLabel.color : Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Femboy marker attached to native female H gauge: " +
                    nativeParent.name);
        }

        private void OnDestroy()
        {
            if (mark != null) Destroy(mark);
        }
    }
}
