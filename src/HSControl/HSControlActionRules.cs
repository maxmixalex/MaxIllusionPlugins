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
            // action names for the 3P Houshi/Sonyu states (modes 6/7).
            // Rely-controlled scenes retain ownership of their own loops.
            return !rely && hasAnimation &&
                (mode == 1 || mode == 2 || mode == 6 || mode == 7);
        }

        internal static bool IsLoop(string state) {
            return !string.IsNullOrEmpty(state) &&
                (state.IndexOf("WLoop", StringComparison.Ordinal) >= 0 ||
                 state.IndexOf("SLoop", StringComparison.Ordinal) >= 0);
        }

        internal static string LoopName(int mode, bool anal, bool strong) {
            if (mode == 1 || mode == 6) return strong ? "SLoop" : "WLoop";
            if (mode == 2 || mode == 7) return (anal ? "A_" : "") + (strong ? "SLoop" : "WLoop");
            return null;
        }
    }


}
