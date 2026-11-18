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
        /// Original RX18 aibulayer_02 entry 4 is the phallus-shaped
        /// p_item_vibe (AibuItemObject IDs 7/8), immediately after
        /// the electric-massager denma row 3. Disable only this entry.
        /// </summary>
        internal static bool IsPhallusToyLayer(int icon, int leftItemId, int rightItemId)
        {
            return icon == 25 && leftItemId == 7 && rightItemId == 8;
        }

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
        /// The original HVoiceCtrl loads the two heroine voice tables, not a
        /// separate male voice channel. These codes designate the female
        /// climax-specific line choices in the Aibu and Sonyu state machines.
        /// 142/143 = Aibu climax/start recovery; 318/319 and 325..330 =
        /// Sonyu female climax/combined-finish voice selections.
        /// Never suppress male-only inside/outside cue indices (320..324).
        /// </summary>
        internal static bool IsFemaleClimaxVoice(int mode, int voiceCode)
        {
            if (mode == 0)
                return voiceCode == 142 || voiceCode == 143;
            if (mode == 2)
                return voiceCode == 318 || voiceCode == 319 ||
                    (voiceCode >= 325 && voiceCode <= 330);
            return false;
        }

        /// <summary>
        /// A Sonyu climax affecting the heroine uses finish kinds orgW,
        /// sameW, orgS, sameS. Inside/outside-only belong to the actual
        /// male participant and must never retarget the heroine.
        /// </summary>
        internal static bool IsFemaleFinishAnimation(int mode, int finishKind, string state)
        {
            if (mode != 2 || finishKind < 3 || finishKind > 6 ||
                string.IsNullOrEmpty(state))
                return false;

            if (state.StartsWith("A_", StringComparison.Ordinal))
                state = state.Substring(2);

            return state == "OLoop" ||
                   state.StartsWith("WF_", StringComparison.Ordinal) ||
                   state.StartsWith("SF_", StringComparison.Ordinal) ||
                   state.StartsWith("WS_", StringComparison.Ordinal) ||
                   state.StartsWith("SS_", StringComparison.Ordinal);
        }

        /// <summary>
        /// Native male appendage rig group. Keep the pose follower scoped to
        /// these bones: the partner's whole-body motion is not copied.
        /// </summary>
        internal static string MaleSegmentBoneName(int index)
        {
            return index < 100 || index > 109
                ? null : "cm_J_dan" + index.ToString() + "_00";
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
