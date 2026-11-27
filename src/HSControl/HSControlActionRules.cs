using System;
using System.Collections;
using System.Reflection;

namespace HSControl
{
    // Small, game-independent action-routing rules used both by the native
    // HScene adapter and by headless tests.
    internal static class HSControlActionRules
    {
        internal static bool CanStartLoop(int mode, bool rely, bool hasAnimation) {
            // The installed KK_HCtrl.dll uses the same native WLoop/SLoop
            // action names for 3P and MMF Houshi/Sonyu states (modes 6/7/8/9).
            // Rely-controlled scenes retain ownership of their own loops.
            return !rely && hasAnimation &&
                (mode == 1 || mode == 2 || mode == 6 || mode == 7 ||
                 mode == 8 || mode == 9);
        }

        // Gauge staging is a manual action: it must not depend on the automatic
        // climax policy or on whether the current animation is a loop.
        internal static bool IsAutoCaressState(int nativeMode, string state, bool rely)
        {
            // HCtrl has M/A/S_Idle, while some installed animation packs
            // expose plain Idle. Actual caress still requires a live native
            // HandCtrl.useAreaItems entry (validated by HSControlAibuDriver).
            return nativeMode == 0 &&
                   (state == "M_Idle" || state == "A_Idle" ||
                    state == "S_Idle" || state == "Idle");
        }

        internal static bool CanStageGauge(int mode, bool hasAnimation, bool nativeClickPending) {
            // Gauge staging only adjusts the actual native gauge. It doesn't
            // call SetPlay, alter the current click or need a loop's metadata.
            // Thus it must remain available during Aibu (mode 0), while an
            // animation is loading, and when a hand already owns an input.
            return mode == 0 || mode == 1 || mode == 2 ||
                   mode == 6 || mode == 7 || mode == 8 || mode == 9;
        }

        // Original HCtrl.CanProc(PlayState): 0=loop,
        // 2/3/4=male/female/simultaneous pre-climax,
        // 5/6/7=male/female/simultaneous climax.
        internal static bool OriginalCanForce(int mode, int kind) {
            if (kind == 8 || kind == 0 || kind == 1)
                return mode == 1 || mode == 2 || mode == 6 ||
                       mode == 7 || mode == 8 || mode == 9;
            if (kind == 2 || kind == 5)
                return mode == 1 || mode == 2 || mode == 6 ||
                       mode == 7 || mode == 8 || mode == 9;
            if (kind == 3)
                return mode == 2 || mode == 3 || mode == 5 ||
                       mode == 7 || mode == 9;
            if (kind == 6)
                return mode == 0 || mode == 2 || mode == 3 ||
                       mode == 5 || mode == 7 || mode == 9;
            if (kind == 4 || kind == 7)
                return mode == 2 || mode == 7 || mode == 9;
            return false;
        }

        internal static bool IsLoop(string state) {
            return !string.IsNullOrEmpty(state) &&
                (state.IndexOf("WLoop", StringComparison.Ordinal) >= 0 ||
                 state.IndexOf("SLoop", StringComparison.Ordinal) >= 0);
        }

        internal static string LoopName(int mode, bool anal, bool strong) {
            if (mode == 1 || mode == 6 || mode == 8) return strong ? "SLoop" : "WLoop";
            if (mode == 2 || mode == 7 || mode == 9)
                return (anal ? "A_" : "") + (strong ? "SLoop" : "WLoop");
            return null;
        }
    }


}
