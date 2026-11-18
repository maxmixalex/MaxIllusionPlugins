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
        /// The base game's aibulayer_02 order is:
        /// 0 hand, 1 finger, 2 tongue, 3 denma vibrator (IDs 5/6),
        /// 4 p_item_vibe phallus-shaped toy (IDs 7/8).
        /// This removes the LAST item, not the massager before it.
        /// </summary>
        internal static bool IsPhallusToyLayer(int icon, int leftItemId, int rightItemId)
        {
            return icon == 25 && leftItemId == 7 && rightItemId == 8;
        }

        // Native MetaballShoot emits its first droplet immediately and
        // then uses one aInterval entry for each additional droplet.
        // A multiplier of 2 therefore doubles (intervals + 1), not just
        // the number of wait times. Clamped to avoid excessive physics load.
        internal static int FluidIntervalCount(int nativeIntervals, float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                amount = 1f;
            if (amount < 0.25f) amount = 0.25f;
            if (amount > 5f) amount = 5f;
            int baseShots = nativeIntervals < 1 ? 2 : nativeIntervals + 1;
            int shots = (int)Math.Ceiling(baseShots * amount);
            if (shots < 2) shots = 2;
            if (shots > 80) shots = 80;
            return shots - 1;
        }

        internal static float BallisticHorizontalFraction(float elevation)
        {
            return (float)Math.Cos(SafeFluidElevation(elevation) *
                Math.PI / 180.0);
        }

        internal static float BallisticVerticalFraction(float elevation)
        {
            return (float)Math.Sin(SafeFluidElevation(elevation) *
                Math.PI / 180.0);
        }

        private static float SafeFluidElevation(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 18f;
            if (value < -10f) return -10f;
            if (value > 60f) return 60f;
            return value;
        }

        internal static bool IsFemaleInsertionFinishStart(string state)
        {
            if (string.IsNullOrEmpty(state))
                return false;
            if (state.StartsWith("A_", StringComparison.Ordinal))
                state = state.Substring(2);
            return state == "WF_IN_Start" || state == "WS_IN_Start" ||
                   state == "SF_IN_Start" || state == "SS_IN_Start";
        }

        internal static float SafeShaftFraction(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.50f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        internal static bool CanSwapFullController(bool enabled, bool explicitOptIn)
        {
            return enabled && explicitOptIn;
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
            {
                // Female-initiative poses use a +38 offset in HSonyu.Proc
                // (Game.isAdd20 && nowAnimationInfo.isFemaleInitiative).
                // The previous filter silently missed those scenes.
                int code = voiceCode;
                if ((code >= 356 && code <= 357) ||
                    (code >= 363 && code <= 368))
                    code -= 38;
                return code == 318 || code == 319 ||
                    (code >= 325 && code <= 330);
            }
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
