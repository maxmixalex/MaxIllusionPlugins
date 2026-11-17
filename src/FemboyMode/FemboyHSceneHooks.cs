using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
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
        private struct HiddenButtonState
        {
            public GameObject First, Second;
            public bool FirstWasActive, SecondWasActive;
        }

        private static readonly Dictionary<int, HiddenButtonState> HiddenButtons =
            new Dictionary<int, HiddenButtonState>();

        // Restore only controls we hid before the native menu rebuild. That
        // prevents one marked heroine from hiding buttons for the next one.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.SetSonyuStart))]
        private static void RestoreBeforeMenuRebuild(HSprite __instance)
        {
            if (__instance == null) return;

            int id = __instance.GetInstanceID();
            HiddenButtonState prior;
            if (!HiddenButtons.TryGetValue(id, out prior))
                return;
            HiddenButtons.Remove(id);
            if (prior.First != null) prior.First.SetActive(prior.FirstWasActive);
            if (prior.Second != null) prior.Second.SetActive(prior.SecondWasActive);
        }

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

            // Preserve each button's native visibility for a future switch
            // to an unmarked heroine or another animation category.
            GameObject first = buttons[0].gameObject;
            GameObject second = buttons[1].gameObject;
            HiddenButtons[__instance.GetInstanceID()] = new HiddenButtonState
            {
                First = first,
                Second = second,
                FirstWasActive = first.activeSelf,
                SecondWasActive = second.activeSelf
            };
            if (HiddenButtons.Count > 16) HiddenButtons.Clear();

            // Vanilla positions 0 and 1 are the two non-anal insert actions.
            first.SetActive(false);
            second.SetActive(false);

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
