using System;
using System.Collections;
using System.Reflection;

namespace HSControl
{
    internal static class HSControlNativeActions
    {
        private static readonly FieldInfo Procs =
            typeof(HSceneProc).GetField("lstProc", BindingFlags.Instance | BindingFlags.NonPublic);

        private static HActionBase CurrentAction(HSceneProc scene) {
            if (scene == null || scene.flags == null || Procs == null) return null;
            IList actions;
            try { actions = Procs.GetValue(scene) as IList; }
            catch { return null; }
            int mode = (int)scene.flags.mode;
            return actions != null && mode >= 0 && mode < actions.Count
                ? actions[mode] as HActionBase : null;
        }

        // Reproduces the crucial Aibu female-finish transition in the
        // installed HCtrl.ForcePlayAibu(PlayState.o_female). Simply setting
        // gaugeFemale=100 never changes the game's animation state.
        internal static bool TryStartAibuFemaleFinish(HSceneProc scene) {
            if (scene == null || scene.flags == null ||
                (int)scene.flags.mode != 0 || scene.hand == null ||
                scene.flags.click != HFlag.ClickKind.none ||
                !HSControlActionRules.IsAutoCaressState(0,
                    scene.flags.nowAnimStateName, scene.flags.rely))
                return false;
            HActionBase action = CurrentAction(scene);
            if (action == null) return false;
            try {
                // Release actual native Aibu props before starting the
                // game's registered climax animation.
                scene.hand.ForceFinish(true);
                for (int i = 0; i < 3; i++)
                    scene.hand.DetachItemByUseItem(i);
                if (!action.SetPlay("Orgasm_Start", true))
                    return false;
                scene.flags.AddAibuOrg();
                if (scene.flags.voice != null &&
                    scene.flags.voice.playVoices != null &&
                    scene.flags.voice.playVoices.Length > 0)
                    scene.flags.voice.playVoices[0] = 142;
                return true;
            } catch { return false; }
        }

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
