using HarmonyLib;
using KKAPI.MainGame;
using KKAPI.Utilities;

namespace FemboyMode
{
    /// <summary>
    /// Applies per-character Free H UI restrictions without modifying the
    /// game's built-in sex field or affecting unmarked characters.
    /// </summary>
    internal static class FemboyHSceneHooks
    {
        private static bool IsMarkedLeadingCharacter(HSprite sprite)
        {
            if (sprite == null || sprite.flags == null || !sprite.flags.isFreeH ||
                FemboyModePlugin.EnableFreeHRestrictions == null ||
                !FemboyModePlugin.EnableFreeHRestrictions.Value)
                return false;

            var heroine = sprite.GetLeadingHeroine();
            return heroine != null &&
                (FemboyModePlugin.IsFemboy(heroine.chaCtrl) ||
                 FemboyModePlugin.IsFemboy(heroine.charFile));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.SetSonyuStart))]
        private static void RestrictFrontOptions(HSprite __instance)
        {
            if (__instance == null || __instance.sonyu == null ||
                __instance.sonyu.categoryActionButton == null)
                return;

            var buttons = __instance.sonyu.categoryActionButton.lstButton;
            if (buttons == null || buttons.Count < 2)
                return;

            if (!IsMarkedLeadingCharacter(__instance))
                return;

            // Vanilla positions 0 and 1 are the two non-anal insert actions.
            // Do not unhide controls for other characters: another plugin may
            // have legitimately hidden those buttons.
            buttons[0].gameObject.SetActive(false);
            buttons[1].gameObject.SetActive(false);

            // Do not artificially enable an unavailable anal animation:
            // some positions have no corresponding action.
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertClick))]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertNoVoiceClick))]
        private static bool PreventNonAnalAction(HSprite __instance)
        {
            // Protect the action entry points as well as the visible buttons.
            return !IsMarkedLeadingCharacter(__instance);
        }
    }
}
