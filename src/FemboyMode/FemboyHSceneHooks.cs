using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Restrict only the current marked heroine's vanilla Free H actions.
    /// The game's own SetSonyuStart initializes the buttons; later state changes can also reactivate action buttons.
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
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.SetSonyuActionButtonActive))]
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
            if (!FemboyRules.HasBothFrontButtons(buttons.Count, baseIndex))
                return;

            // Capture availability BEFORE hiding the original insertion
            // buttons. Some imported positions expose ONLY the normal pair;
            // if we hide that pair without offering anal the user cannot
            // start the selected action at all.
            bool normalOffered = IsOffered(buttons[baseIndex]) ||
                IsOffered(buttons[baseIndex + 1]);
            int analIndex = FemboyRules.AnalInsertStartIndex(femaleInitiative);
            bool hasAnalPair = FemboyRules.HasBothAnalButtons(
                buttons.Count, analIndex);
            bool analOffered = hasAnalPair &&
                (IsOffered(buttons[analIndex]) ||
                 IsOffered(buttons[analIndex + 1]));

            // Reuse the two game-owned anal bubbles and their native onClick
            // listeners; never generate a detached button or call SetPlay.
            // Vanilla may hide them for positions that only list the front
            // choice. Only restore those two bubbles in that case. Native
            // OnInsertAnalClick/OnInsertAnalNoVoiceClick retain ownership of
            // insertion, voice conditions and the subsequent scene state.
            if (FemboyRules.ShouldOfferAnalFallback(normalOffered,
                    analOffered, hasAnalPair)) {
                for (int i = analIndex; i < analIndex + 2; i++) {
                    if (buttons[i] == null) continue;
                    buttons[i].gameObject.SetActive(true);
                    buttons[i].interactable = true;
                }
            }

            // Vanilla can reactivate front on a subsequent position change.
            // Keep the restriction confined to the current marked heroine.
            if (buttons[baseIndex] != null)
                buttons[baseIndex].gameObject.SetActive(false);
            if (buttons[baseIndex + 1] != null)
                buttons[baseIndex + 1].gameObject.SetActive(false);
        }

        private static bool IsOffered(UnityEngine.UI.Button button) {
            return button != null && button.gameObject.activeSelf &&
                button.interactable;
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
