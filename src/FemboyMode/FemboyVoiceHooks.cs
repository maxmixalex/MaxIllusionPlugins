using HarmonyLib;

namespace FemboyMode
{
    /// <summary>
    /// Per-actor HVoiceCtrl integration. Original Koikatsu HVoiceCtrl has
    /// heroine voice tables only; the male player does not have a matching
    /// numbered voice channel. Keep other heroines and all other dialogue.
    /// </summary>
    internal static class FemboyVoiceHooks
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
        private static void BeforeFemaleVoice(
            HVoiceCtrl __instance, ChaControl _female, int _main)
        {
            if (__instance == null || _female == null ||
                FemboyModePlugin.SuppressFemaleClimaxVoice == null ||
                !FemboyModePlugin.SuppressFemaleClimaxVoice.Value)
                return;

            HFlag flags = __instance.flags;
            if (flags == null || !flags.isFreeH ||
                (_main != 0 && _main != 1) ||
                flags.voice == null || flags.voice.playVoices == null ||
                flags.voice.playVoices.Length <= _main ||
                !FemboyModePlugin.IsFemboy(_female))
                return;

            int code = flags.voice.playVoices[_main];
            if (!FemboyRules.IsFemaleClimaxVoice((int)flags.mode, code))
                return;

            // Silence only the marked actor's female climax dialogue.
            // Do not write HFlag.finish, alter the male participant's state,
            // or change another heroine's pending voice events.
            flags.voice.playVoices[_main] = -1;
            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Suppressed female climax voice code " + code +
                    " for marked actor at channel " + _main + ".");
        }
    }
}
