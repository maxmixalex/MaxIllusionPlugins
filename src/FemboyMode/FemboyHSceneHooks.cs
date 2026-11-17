using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Restrict only the current marked heroine's vanilla Free H actions.
    /// The game's own SetSonyuStart always rebuilds the complete button list.
    /// </summary>
    internal static class FemboyHSceneHooks
    {
        internal static bool IsMarkedLeadingCharacter(HSprite sprite)
        {
            if (sprite == null || sprite.flags == null || !sprite.flags.isFreeH ||
                FemboyModePlugin.EnableFreeHRestrictions == null ||
                !FemboyModePlugin.EnableFreeHRestrictions.Value)
                return false;

            var heroine = sprite.GetLeadingHeroine();
            return heroine != null &&
                (FemboyModePlugin.IsFemboy(heroine.chaCtrl));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.SetSonyuStart))]
        private static void RestrictFrontOptions(HSprite __instance)
        {
            if (__instance == null || __instance.flags == null ||
                __instance.sonyu == null || __instance.sonyu.categoryActionButton == null ||
                !IsMarkedLeadingCharacter(__instance))
                return;

            var buttons = __instance.sonyu.categoryActionButton.lstButton;
            if (buttons == null)
                return;

            // Match vanilla SetSonyuStart exactly: two seven-button pages,
            // selected by the pose's isFemaleInitiative property.
            HFlag flags = __instance.flags;
            bool femaleInitiative = flags.selectAnimationListInfo != null
                ? flags.selectAnimationListInfo.isFemaleInitiative
                : flags.nowAnimationInfo != null && flags.nowAnimationInfo.isFemaleInitiative;
            int baseIndex = FemboyRules.FrontInsertStartIndex(femaleInitiative);
            if (!FemboyRules.HasBothFrontButtons(buttons.Count, baseIndex) )
                return;

            // SetSonyuStart calls SetActive(false) for every button before
            // rebuilding its current page. Therefore no retained UI state or
            // visibility-restoration cache is needed when switching actors.
            if (buttons[baseIndex] != null)
                buttons[baseIndex].gameObject.SetActive(false);
            if (buttons[baseIndex + 1] != null)
                buttons[baseIndex + 1].gameObject.SetActive(false);

            // Never force-enable the other actions; a pose may not support them.
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertClick))]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertNoVoiceClick))]
        private static bool PreventNonAnalAction(HSprite __instance)
        {
            // UI visibility is insufficient: other plugins can call the native
            // handler directly. Guard it for the marked leading heroine only.
            return !IsMarkedLeadingCharacter(__instance);
        }
    }
}
