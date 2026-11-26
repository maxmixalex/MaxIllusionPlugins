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
            return !rely && hasAnimation && (mode == 1 || mode == 2);
        }

        internal static bool IsLoop(string state) {
            return !string.IsNullOrEmpty(state) &&
                (state.IndexOf("WLoop", StringComparison.Ordinal) >= 0 ||
                 state.IndexOf("SLoop", StringComparison.Ordinal) >= 0);
        }

        internal static string LoopName(int mode, bool anal, bool strong) {
            if (mode == 1) return strong ? "SLoop" : "WLoop";
            if (mode == 2) return (anal ? "A_" : "") + (strong ? "SLoop" : "WLoop");
            return null;
        }
    }


}
