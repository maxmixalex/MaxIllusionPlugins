using System;
using HarmonyLib;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// The native HHoushi climax still targets the original heroine's Siru
    /// material, even when the rendered service roles have been reversed.
    /// Forward new stain writes to the receiver actor instead, preserving
    /// outside-mode and clearing behavior and respecting MaleJuice.
    /// </summary>
    internal static class FemboyReversedSiruHooks
    {
        private static int loggedTransfers;
        private static int missingReceiverWarned;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetSiruFlags))]
        private static bool BeforeSetSiruFlags(
            ChaControl __instance, ChaFileDefine.SiruParts _parts, byte _state)
        {
            if (__instance == null || _state == 0 ||
                !FemboyModePlugin.IsFemboy(__instance))
                return true;

            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                HSceneProc proc = scenes[i];
                if (proc == null || proc.flags == null ||
                    !proc.flags.isFreeH || proc.flags.mode != HFlag.EMode.houshi ||
                    proc.flags.nowAnimationInfo == null ||
                    !FemboyRules.IsReversedHoushiName(
                        proc.flags.nowAnimationInfo.nameAnimation) ||
                    proc.lstFemale == null || proc.lstFemale.Count != 1 ||
                    !ReferenceEquals(proc.lstFemale[0], __instance) ||
                    proc.male == null)
                    continue;

                // In reversed Houshi the original heroine is the receiver,
                // not the body that native SiruPaste should stain.
                ChaControl receiver = proc.male;
                try
                {
                    receiver.SetSiruFlags(_parts, _state);
                    if (loggedTransfers++ < 5)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Reversed Houshi Siru material transferred to male " +
                            "receiver: part=" + _parts + " intensity=" + _state);
                }
                catch (Exception ex)
                {
                    if (missingReceiverWarned++ < 2)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Reversed Houshi male Siru transfer unavailable: " +
                            ex.GetType().Name + ": " + ex.Message +
                            "; prevented heroine staining.");
                }
                // Never write a new stain to the visually reversed performer.
                // Native Siru clearing (state 0) is deliberately not blocked.
                return false;
            }
            return true;
        }
    }
}
