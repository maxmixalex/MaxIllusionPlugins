using System;
using System.Collections;
using System.Reflection;

namespace HSControl
{
    internal static class HSControlNativeActions
    {
        private static readonly FieldInfo Procs =
            typeof(HSceneProc).GetField("lstProc", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryStartLoop(HSceneProc owner, bool strong) {
            if (owner == null || owner.flags == null || Procs == null) return false;
            HFlag flags = owner.flags;
            if (flags.click != HFlag.ClickKind.none) return false;
            int mode = (int)flags.mode;
            if (!HSControlActionRules.CanStartLoop(mode, flags.rely, flags.nowAnimationInfo != null)) return false;
            string animation = HSControlActionRules.LoopName(mode, flags.isAnalPlay, strong);
            IList list = Procs.GetValue(owner) as IList;
            if (list == null || mode >= list.Count) return false;
            HActionBase action = list[mode] as HActionBase;
            if (action == null || !action.SetPlay(animation, true)) return false;

            // SetPlay already enters the requested native loop. Do not
            // rewrite native click/finish/voice/gauge or force Houshi state:
            // those fields belong to the game's own animation transition.
            return true;
        }
    }
}
