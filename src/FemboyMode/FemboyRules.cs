using System;

namespace FemboyMode
{
    /// <summary>
    /// Small, dependency-free rules shared by the plugin and the headless
    /// tests. Keep game-specific actions in the Harmony integration classes.
    /// </summary>
    internal static class FemboyRules
    {
        // Original Koikatsu h_particle TextAsset: LiquidSiru (particle ID 0)
        // is attached to this transform, not to the Kokan base bone.
        internal const string DefaultMaleFinishBone = "cm_J_dan109_00";

        /// <summary>
        /// Sonyu Start has two pages of seven actions, selected by
        /// HSceneProc.AnimationListInfo.isFemaleInitiative.
        /// </summary>
        internal static int FrontInsertStartIndex(bool femaleInitiative)
        {
            return femaleInitiative ? 7 : 0;
        }

        internal static bool HasBothFrontButtons(int buttonCount, int startIndex)
        {
            return startIndex >= 0 && buttonCount >= startIndex + 2;
        }

        internal static float SafeBoneScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 1f;
            if (value < 0.5f) return 0.5f;
            if (value > 2f) return 2f;
            return value;
        }

        /// <summary>
        /// Resolve a requested bone against actual installed rig names.
        /// Example: cm_j_dan100 -> cm_J_dan100_00 (observed in RX18).
        /// Rank: 0 exact, 1 case-insensitive, 2 exact base + _00,
        /// 3 exact base + other numeric suffix, -1 incompatible.
        /// Avoid loose prefix matching (100 must not match 101).
        /// </summary>
        internal static int BoneMatchRank(string requested, string actual)
        {
            if (string.IsNullOrEmpty(requested) || string.IsNullOrEmpty(actual))
                return -1;

            if (string.Equals(requested, actual, StringComparison.Ordinal))
                return 0;
            if (string.Equals(requested, actual, StringComparison.OrdinalIgnoreCase))
                return 1;

            if (actual.Length <= requested.Length + 1 ||
                !actual.StartsWith(requested, StringComparison.OrdinalIgnoreCase) ||
                actual[requested.Length] != '_')
                return -1;

            string suffix = actual.Substring(requested.Length + 1);
            for (int i = 0; i < suffix.Length; i++)
                if (suffix[i] < '0' || suffix[i] > '9')
                    return -1;

            return suffix == "00" ? 2 : 3;
        }

        /// <summary>
        /// Deduplicate particle FX for the currently active heroine, not
        /// for the shared HFlag instance alone.
        /// </summary>
        internal static long EffectKey(int flagInstanceId, int heroineIndex)
        {
            return ((long)flagInstanceId << 2) | (uint)(heroineIndex & 3);
        }
    }
}
