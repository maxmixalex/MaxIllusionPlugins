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
            int mode = (int)flags.mode;
            if (!HSControlActionRules.CanStartLoop(mode, flags.rely, flags.nowAnimationInfo != null)) return false;
            string animation = HSControlActionRules.LoopName(mode, flags.isAnalPlay, strong);
            IList list = Procs.GetValue(owner) as IList;
            if (list == null || mode >= list.Count) return false;
            HActionBase action = list[mode] as HActionBase;
            if (action == null || !action.SetPlay(animation, true)) return false;

            flags.click = HFlag.ClickKind.none;
            flags.finish = HFlag.FinishKind.none;
            flags.voiceWait = false;
            flags.isInsideFinish = false;
            if (mode == 1 || mode == 6) {
                flags.speedHoushi = 0;
                if (flags.gaugeMale >= 70f) flags.gaugeMale = 35f;
                flags.SetHoushiPlay();
            }
            return true;
        }
    }
}
