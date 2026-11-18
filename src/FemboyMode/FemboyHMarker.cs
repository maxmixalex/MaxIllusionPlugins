using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Upper-left marker visible exclusively for the current marked heroine
    /// in Free H. Uses IMGUI rather than altering the game's sprite hierarchy
    /// or requiring a bundled image/font asset.
    /// </summary>
    internal static class FemboyHMarkerHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void OnSpriteInitialized(HSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null)
                return;

            FemboyHMarker marker = __instance.gameObject.GetComponent<FemboyHMarker>();
            if (marker == null)
                marker = __instance.gameObject.AddComponent<FemboyHMarker>();
            marker.Initialize(__instance);
        }
    }

    public sealed class FemboyHMarker : MonoBehaviour
    {
        private HSprite sprite;
        private GUIStyle labelStyle;

        public void Initialize(HSprite sceneSprite)
        {
            sprite = sceneSprite;
        }

        private void OnGUI()
        {
            if (sprite == null || sprite.flags == null || !sprite.flags.isFreeH ||
                FemboyModePlugin.ShowFreeHMarker == null ||
                !FemboyModePlugin.ShowFreeHMarker.Value)
                return;

            var heroine = sprite.GetLeadingHeroine();
            if (heroine == null ||
                !FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 14;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.normal.textColor = new Color(0.85f, 1f, 0.98f, 1f);
            }

            float scale = Mathf.Clamp(Screen.height / 900f, 0.85f, 1.5f);
            float x = 15f * scale;
            float y = 15f * scale;
            float w = 118f * scale;
            float h = 28f * scale;
            Color old = GUI.color;

            // Dark base and teal edge: visible independently of scene lighting.
            GUI.color = new Color(0.075f, 0.12f, 0.16f, 0.88f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.30f, 0.90f, 0.83f, 1f);
            GUI.DrawTexture(new Rect(x, y, 4f * scale, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            labelStyle.fontSize = Mathf.RoundToInt(14f * scale);
            GUI.Label(new Rect(x + 4f * scale, y, w - 4f * scale, h),
                "⚥  FEMBOY", labelStyle);
            GUI.color = old;
        }
    }
}
