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
        // Free H has one active Sonyu UI. Cache only the *current* pose and
        // supported button variants, never replace game-owned UnityEvents.
        // A pose/heroine change invalidates this fallback automatically.
        private static HSprite fallbackOwner;
        private static HSceneProc.AnimationListInfo fallbackPose;
        private static bool fallbackFemaleInitiative;
        private static bool fallbackWithVoice;
        private static bool fallbackWithoutVoice;
        private static bool routingFallback;

        private static HSceneProc.AnimationListInfo CurrentPose(HSprite sprite)
        {
            HFlag flags = sprite == null ? null : sprite.flags;
            if (flags == null) return null;
            return flags.selectAnimationListInfo ?? flags.nowAnimationInfo;
        }

        private static void ClearFallback()
        {
            fallbackOwner = null;
            fallbackPose = null;
            fallbackWithVoice = false;
            fallbackWithoutVoice = false;
        }

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
            // Native rebuilds can also occur when Free H ends or the active
            // heroine changes. Avoid leaving a stale redirect armed.
            ClearFallback();
            if (__instance == null || __instance.flags == null ||
                __instance.sonyu == null || __instance.sonyu.categoryActionButton == null ||
                !IsMarkedLeadingCharacter(__instance))
                return;

            var buttons = __instance.sonyu.categoryActionButton.lstButton;
            if (buttons == null)
                return;

            // Match vanilla SetSonyuStart exactly: two seven-button pages,
            // selected by the pose's isFemaleInitiative property.
            HSceneProc.AnimationListInfo selectedPose = CurrentPose(__instance);
            bool femaleInitiative = selectedPose != null && selectedPose.isFemaleInitiative;
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

            // An imported front-only pose has no real anal animation. Show
            // the game's native anal-shaped bubbles as *aliases*, retaining
            // their original onClick events. Harmony intercepts the two anal
            // callbacks ONLY while this exact pose is the active fallback;
            // it then runs the corresponding regular-insertion method.
            // Never claim the front-only animation has become anal.
            if (selectedPose != null &&
                FemboyRules.ShouldOfferAnalFallback(normalOffered,
                    analOffered, hasAnalPair)) {
                bool withVoice = IsOffered(buttons[baseIndex]);
                bool withoutVoice = IsOffered(buttons[baseIndex + 1]);
                for (int variant = 0; variant < 2; variant++) {
                    var bubble = buttons[analIndex + variant];
                    if (bubble == null) continue;
                    bool offered = variant == 0 ? withVoice : withoutVoice;
                    bubble.gameObject.SetActive(offered);
                    bubble.interactable = offered;
                }
                fallbackOwner = __instance;
                fallbackPose = selectedPose;
                fallbackFemaleInitiative = femaleInitiative;
                fallbackWithVoice = withVoice;
                fallbackWithoutVoice = withoutVoice;
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

        // The native anal bubble listeners are left untouched. Redirect only
        // the synthetic bubble of an animation that has no anal entry.
        // On a genuine anal action the game-owned handler runs unmodified.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertAnalClick))]
        private static bool RedirectAnalWithVoice(HSprite __instance)
        {
            return RedirectFallback(__instance, true);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertAnalNoVoiceClick))]
        private static bool RedirectAnalWithoutVoice(HSprite __instance)
        {
            return RedirectFallback(__instance, false);
        }

        private static bool RedirectFallback(HSprite sprite, bool withVoice)
        {
            HSceneProc.AnimationListInfo pose = CurrentPose(sprite);
            bool matches = ReferenceEquals(fallbackOwner, sprite) &&
                ReferenceEquals(fallbackPose, pose) &&
                pose != null && pose.isFemaleInitiative == fallbackFemaleInitiative &&
                IsMarkedLeadingCharacter(sprite);
            if (!FemboyRules.UseFrontAnimationForAnalFallback(
                    matches, withVoice ? fallbackWithVoice : fallbackWithoutVoice))
                return true;

            // The ordinary handler is normally blocked for marked heroines.
            // Grant a narrowly scoped exception for this alias only, and
            // ALWAYS restore the guard, including on Unity-side exceptions.
            bool prior = routingFallback;
            routingFallback = true;
            try {
                if (withVoice) sprite.OnInsertClick();
                else sprite.OnInsertNoVoiceClick();
            } finally {
                routingFallback = prior;
                // Native Sonyu may immediately change flags.nowAnimationInfo.
                // The next native button rebuild will recalculate eligibility.
                ClearFallback();
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertClick))]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnInsertNoVoiceClick))]
        private static bool PreventNonAnalAction(HSprite __instance)
        {
            // Block ordinary insertion except the actual native callback
            // invoked by this exact, temporary anal-labelled alias.
            return routingFallback || !IsMarkedLeadingCharacter(__instance);
        }
    }
}
