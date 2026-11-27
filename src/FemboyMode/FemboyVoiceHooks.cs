using System;
using HarmonyLib;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Keep the real heroine voice clip alive for its original duration.
    /// HSonyu waits on Manager.Voice.IsVoiceCheck; setting playVoices=-1
    /// (as older releases did) makes that wait end immediately and
    /// truncates WS/SF/SS climax loops. Mute only that particular LoadVoice.
    /// </summary>
    internal static class FemboyVoiceHooks
    {
        private struct VoiceContext
        {
            internal bool PreviousActive;
            internal Transform PreviousMouth;
            internal int PreviousCode;
        }

        // HVoiceCtrl.VoiceProc synchronously invokes Voice.OnecePlayChara,
        // which calls Manager.Voice.Play on Unity's main thread. Restore the
        // previous context even when the original method throws.
        private static bool pendingMute;
        private static Transform pendingMouth;
        private static int pendingCode;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
        private static void BeforeFemaleVoice(
            HVoiceCtrl __instance, ChaControl _female, int _main,
            out VoiceContext __state)
        {
            __state = new VoiceContext
            {
                PreviousActive = pendingMute,
                PreviousMouth = pendingMouth,
                PreviousCode = pendingCode
            };
            pendingMute = false;
            pendingMouth = null;
            pendingCode = -1;

            if (__instance == null || _female == null ||
                FemboyModePlugin.SuppressFemaleClimaxVoice == null ||
                !FemboyModePlugin.SuppressFemaleClimaxVoice.Value)
                return;

            HFlag flags = __instance.flags;
            if (flags == null || !flags.isFreeH ||
                (_main != 0 && _main != 1) ||
                flags.voice == null || flags.voice.playVoices == null ||
                flags.voice.playVoices.Length <= _main ||
                flags.transVoiceMouth == null ||
                flags.transVoiceMouth.Length <= _main ||
                !FemboyModePlugin.IsFemboy(_female))
                return;

            int code = flags.voice.playVoices[_main];
            if (!FemboyRules.IsFemaleClimaxVoice((int)flags.mode, code))
                return;

            pendingMute = true;
            pendingMouth = flags.transVoiceMouth[_main];
            pendingCode = code;
            // IMPORTANT: leave flags.voice.playVoices untouched. It is
            // consumed by native VoiceProc, and HSonyu checks clip lifetime.
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
        private static Exception EndFemaleVoice(
            Exception __exception, VoiceContext __state)
        {
            pendingMute = __state.PreviousActive;
            pendingMouth = __state.PreviousMouth;
            pendingCode = __state.PreviousCode;
            return __exception;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Manager.Voice), nameof(Manager.Voice.Play))]
        private static void AfterNativeVoiceCreated(
            Transform voiceTrans, Transform __result)
        {
            if (!pendingMute || voiceTrans == null ||
                !ReferenceEquals(voiceTrans, pendingMouth) ||
                __result == null)
                return;

            LoadVoice load = __result.GetComponentInChildren<LoadVoice>(true);
            if (load == null)
                return;

            FemboyVoiceMute mute =
                load.gameObject.GetComponent<FemboyVoiceMute>();
            if (mute == null)
                mute = load.gameObject.AddComponent<FemboyVoiceMute>();
            mute.Initialize(load);
            pendingMute = false;

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Muted native female climax clip " + pendingCode +
                    " without altering the voice event or its duration.");
        }
    }

    /// <summary>
    /// AudioSource.mute does not affect Manager.Voice.IsVoiceCheck, while
    /// LoadVoice's regular Update writes volume every frame. The mute
    /// component belongs to this one playback instance, never a shared bus.
    /// </summary>
    public sealed class FemboyVoiceMute : MonoBehaviour
    {
        private LoadVoice voice;

        internal void Initialize(LoadVoice instance)
        {
            voice = instance;
            ApplyMute();
        }

        private void LateUpdate()
        {
            ApplyMute();
        }

        private void ApplyMute()
        {
            if (voice != null && voice.audioSource != null)
                voice.audioSource.mute = true;
        }
    }
}
