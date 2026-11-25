using UnityEngine;

namespace MultiTeamH {
    internal enum BackgroundClimaxFinish {
        Outside,
        Inside
    }

    /// <summary>
    /// Requests the same finish action as the native UI. Animation, voices,
    /// counters and recovery continue to be owned by the game's action graph.
    /// </summary>
    internal static class BackgroundClimax {
        internal static bool TryRequest(HFlag flags, AnimatorStateInfo animation, bool isTransitioning, bool enabled, bool isBackground,
            BackgroundClimaxFinish finish, bool houshiOutsideOnly) {
            if (!enabled || !isBackground || isTransitioning || flags == null || flags.isHSceneEnd ||
                flags.lockGugeMale || flags.voiceWait || flags.gaugeMale < 100f ||
                float.IsNaN(flags.gaugeMale) || float.IsInfinity(flags.gaugeMale) ||
                flags.click != HFlag.ClickKind.none || flags.finish != HFlag.FinishKind.none ||
                flags.selectAnimationListInfo != null || flags.nowAnimationInfo == null) return false;

            bool outside = finish != BackgroundClimaxFinish.Inside;
            if (flags.mode == HFlag.EMode.houshi) {
                if (flags.nowAnimStateName != "OLoop" || !animation.IsName("OLoop")) return false;
                // Native SetHoushiAutoFinish exposes an inside action only for
                // numCtrl == 1 while the team's outside toggle is off.
                outside |= flags.nowAnimationInfo.numCtrl != 1 || houshiOutsideOnly;
            } else if (flags.mode == HFlag.EMode.sonyu) {
                string state = flags.nowAnimStateName;
                if (state != "WLoop" && state != "SLoop" &&
                    state != "A_WLoop" && state != "A_SLoop") return false;
                if (!animation.IsName(state)) return false;
            } else {
                // The parallel update excludes shared 3P/lesbian graphs. Never
                // extend this request to an action whose finish path is unknown.
                return false;
            }

            flags.click = outside ? HFlag.ClickKind.outside : HFlag.ClickKind.inside;
            flags.voiceWait = true;
            return true;
        }
    }
}
