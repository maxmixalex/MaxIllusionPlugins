using System;
using HarmonyLib;

namespace MultiTeamH {
    [HarmonyPatch(typeof(HSprite), "InitHeroine")]
    internal static class NativeThreePPortraitHook {
        [HarmonyPrefix]
        private static void PreserveTeamPortrait(HSprite __instance) {
            TeamSession.DetachManagedPortraitBeforeNative3P(__instance);
        }

        [HarmonyFinalizer]
        private static Exception RecoverTeamPortrait(HSprite __instance, Exception __exception) {
            if (__exception == null) return null;
            try { TeamSession.RestoreManagedPortraitAfterNative3P(__instance); }
            catch (Exception cleanupError) { DiagnosticProbe.Warning("Could not restore a managed portrait: " + cleanupError); }
            return __exception;
        }

        [HarmonyPostfix]
        private static void RestoreTeamPortrait(HSprite __instance) {
            TeamSession.RestoreManagedPortraitAfterNative3P(__instance);
        }
    }

    [HarmonyPatch(typeof(HSprite), "CreateMotionList")]
    internal static class SpecialActionMenuBuildHooks {
        private static void Prefix(HSprite __instance, ref HFlag.EMode __state) {
            __state = (HFlag.EMode)(-1);
            if (TeamSession.IsManagedSprite(__instance)) DiagnosticProbe.Info("CreateMotionList-enter");
            if (!TeamSession.BeginSpecialMenuBuild(__instance, out __state)) __state = (HFlag.EMode)(-1);
        }

        private static void Postfix(HSprite __instance, ref HFlag.EMode __state) {
            if ((int)__state < 0) return;
            HFlag.EMode previous = __state;
            __state = (HFlag.EMode)(-1);
            TeamSession.EndSpecialMenuBuild(__instance, previous);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(HSprite __instance, ref HFlag.EMode __state, Exception __exception) {
            try { Postfix(__instance, ref __state); }
            catch (Exception cleanupError) {
                DiagnosticProbe.Warning("Could not restore the special action menu mode: " + cleanupError);
                if (__exception == null) return cleanupError;
            }
            return __exception;
        }
    }

}
