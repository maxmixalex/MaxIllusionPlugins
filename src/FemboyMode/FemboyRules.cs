using System;

namespace FemboyMode
{
    /// <summary>
    /// Small, dependency-free rules shared by the plugin and the headless
    /// tests. Keep game-specific actions in the Harmony integration classes.
    /// </summary>
    internal static class FemboyRules
    {
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
        /// Deduplicate particle FX for the currently active heroine, not
        /// for the shared HFlag instance alone.
        /// </summary>
        internal static long EffectKey(int flagInstanceId, int heroineIndex)
        {
            return ((long)flagInstanceId << 2) | (uint)(heroineIndex & 3);
        }
    }
}
