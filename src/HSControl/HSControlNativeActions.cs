using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HSControl
{
    // HCtrl.ForcePlay / ChangeSW mapping derived from the installed original
    // KK_HCtrl.dll rather than treating every button as a gauge assignment.
    internal static class HSControlNativeActions
    {
        private const BindingFlags Flags = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Procs =
            typeof(HSceneProc).GetField("lstProc", Flags);

        private static HActionBase CurrentAction(HSceneProc owner)
        {
            if (owner == null || owner.flags == null || Procs == null)
                return null;
            try {
                IList list = Procs.GetValue(owner) as IList;
                int mode = (int)owner.flags.mode;
                return list != null && mode >= 0 && mode < list.Count
                    ? list[mode] as HActionBase : null;
            } catch { return null; }
        }

        internal static bool HasAction(HSceneProc owner) {
            return CurrentAction(owner) != null;
        }

        private static bool SetPrivateMember(object owner, string name, object value)
        {
            if (owner == null) return false;
            try {
                FieldInfo f = owner.GetType().GetField(name, Flags);
                if (f == null) return false;
                f.SetValue(owner, value);
                return true;
            } catch { return false; }
        }

        private static bool SetNativeFinish(HFlag flag, int value)
        {
            if (flag == null) return false;
            try {
                FieldInfo field = typeof(HFlag).GetField("finish", Flags);
                if (field == null) return false;
                object converted = field.FieldType.IsEnum ?
                    Enum.ToObject(field.FieldType, value) :
                    Convert.ChangeType(value, field.FieldType);
                field.SetValue(flag, converted);
                return true;
            } catch { return false; }
        }

        private static bool RunNativeLoopProc(HActionBase action)
        {
            if (action == null) return false;
            try {
                MethodInfo method = action.GetType().GetMethod("LoopProc", Flags,
                    null, new[] { typeof(bool) }, null);
                if (method == null) return false;
                method.Invoke(action, new object[] { false });
                return true;
            } catch { return false; }
        }

        private static void Voice(HFlag flags, int id, int index = 0)
        {
            if (flags != null && flags.voice != null &&
                flags.voice.playVoices != null &&
                index >= 0 && index < flags.voice.playVoices.Length)
                flags.voice.playVoices[index] = id;
        }

        // In original HCtrl the Weak and Strong buttons do NOT call SetPlay.
        // They issue native motionchange (ClickKind 6) only when the strength
        // differs from the currently selected Houshi/Sonyu loop.
        internal static bool TryChangeStrength(HSceneProc owner, bool strong)
        {
            HActionBase action = CurrentAction(owner);
            if (action == null) return false;
            HFlag flags = owner.flags;
            int mode = (int)flags.mode;
            if (!HSControlActionRules.OriginalCanForce(mode, strong ? 1 : 0))
                return false;
            bool current = false;
            if (mode == 1 || mode == 6 || mode == 8) {
                current = flags.voice != null && flags.voice.loopMotionAorB;
            } else {
                FieldInfo strength = action.GetType().GetField("isSW", Flags);
                if (strength != null) {
                    try { current = Convert.ToBoolean(strength.GetValue(action)); }
                    catch { }
                }
            }
            if (strong != current)
                flags.click = (HFlag.ClickKind)6;
            return true;
        }

        internal static bool TryStartAibuFemaleFinish(HSceneProc owner)
        {
            if (owner == null || owner.flags == null ||
                (int)owner.flags.mode != 0 || owner.hand == null)
                return false;
            HActionBase action = CurrentAction(owner);
            if (action == null) return false;
            try {
                owner.hand.ForceFinish(true);
                for (int i = 0; i < 3; i++)
                    owner.hand.DetachItemByUseItem(i);
                if (!action.SetPlay("Orgasm_Start", true)) return false;
                owner.flags.AddAibuOrg();
                Voice(owner.flags, 142);
                return true;
            } catch { return false; }
        }

        // The original has a standalone "Loop" button, separate from weak/
        // strong. It supports native masturbation and lesbian states too.
        internal static bool TryStartLoop(HSceneProc owner, bool strong)
        {
            HActionBase action = CurrentAction(owner);
            if (action == null) return false;
            HFlag flags = owner.flags;
            int mode = (int)flags.mode;
            if (mode == 0 || mode == 4) return false;
            string animation;
            if (mode == 3 || mode == 5) {
                animation = flags.gaugeFemale >= 70f ? "SLoop" :
                    flags.gaugeFemale >= 40f ? "MLoop" : "WLoop";
            } else {
                animation = HSControlActionRules.LoopName(
                    mode, flags.isAnalPlay, strong);
            }
            if (string.IsNullOrEmpty(animation)) return false;
            try {
                if (!action.SetPlay(animation, true)) return false;
                flags.click = HFlag.ClickKind.none;
                SetNativeFinish(flags, 0);
                flags.voiceWait = false;
                if (mode == 1 || mode == 6 || mode == 8) {
                    flags.speedHoushi = 0;
                    flags.SetHoushiPlay();
                    if (flags.gaugeMale >= 70f) flags.gaugeMale = 35f;
                }
                if (mode == 3) Voice(flags,
                    animation == "SLoop" ? 403 :
                    animation == "MLoop" ? 402 : 401);
                flags.speedCalc = 0f;
                return true;
            } catch { return false; }
        }

        // 2=male pre, 3=female pre, 4=both pre,
        // 5=male finish, 6=female finish, 7=both finish.
        internal static bool TryForceAction(HSceneProc owner, int kind)
        {
            HActionBase action = CurrentAction(owner);
            if (action == null || owner == null || owner.flags == null)
                return false;
            HFlag flags = owner.flags;
            int mode = (int)flags.mode;
            if (!HSControlActionRules.OriginalCanForce(mode, kind))
                return false;

            if (mode == 0) return kind == 6 &&
                TryStartAibuFemaleFinish(owner);
            if (mode == 4) return false;

            bool pre = kind >= 2 && kind <= 4;
            bool male = kind == 2 || kind == 4 || kind == 5 || kind == 7;
            bool female = kind == 3 || kind == 4 || kind == 6 || kind == 7;

            try {
                if (mode == 1 || mode == 6 || mode == 8)
                {
                    if (pre) {
                        if (!male) return false;
                        flags.gaugeMale = Mathf.Max(flags.gaugeMale, 70f);
                        flags.click = (HFlag.ClickKind)42;
                        return RunNativeLoopProc(action);
                    }
                    if (kind != 5) return false;
                    bool autoFinish = mode == 1
                        ? owner.sprite != null && owner.sprite.IsHoushiAutoFinish()
                        : mode == 6
                        ? owner.sprite != null && owner.sprite.Is3PHoushiAutoFinish()
                        : owner.sprite != null && owner.sprite.IsHoushi3PDarkAutoFinish();
                    bool inside = flags.nowAnimationInfo != null &&
                        flags.nowAnimationInfo.sysTaii != 0 &&
                        flags.nowAnimationInfo.sysTaii != 1 &&
                        !autoFinish;
                    string anim = inside ? "IN_Start" : "M_OUT_Start";
                    if (!action.SetPlay(anim, true)) return false;
                    Voice(flags, inside ? 205 : 204);
                    flags.voiceWait = false;
                    flags.timeDecreaseGaugeCalc = 0f;
                    return true;
                }

                if (mode == 2 || mode == 7 || mode == 9)
                {
                    if (pre) {
                        int finish;
                        if (kind == 2) {
                            bool autoFinish = mode == 2
                                ? owner.sprite != null && owner.sprite.IsSonyuAutoFinish()
                                : mode == 7
                                ? owner.sprite != null && owner.sprite.IsSonyu3PAutoFinish()
                                : owner.sprite != null && owner.sprite.IsSonyu3PDarkAutoFinish();
                            finish = autoFinish ? 2 : 1;
                        }
                        else if (kind == 3) finish = 3;
                        else finish = 4;
                        if (!SetNativeFinish(flags, finish)) return false;
                        if (male) flags.gaugeMale =
                            Mathf.Max(flags.gaugeMale, 70f);
                        if (female) flags.gaugeFemale =
                            Mathf.Max(flags.gaugeFemale, 70f);
                        return RunNativeLoopProc(action);
                    }

                    string start;
                    int voice;
                    if (kind == 5) {
                        bool outFinish = mode == 2
                            ? owner.sprite != null && owner.sprite.IsSonyuAutoFinish()
                            : mode == 7
                            ? owner.sprite != null && owner.sprite.IsSonyu3PAutoFinish()
                            : owner.sprite != null && owner.sprite.IsSonyu3PDarkAutoFinish();
                        start = outFinish ? "M_OUT_Start" : "M_IN_Start";
                        voice = outFinish ? 334 : 324;
                        if (!outFinish) flags.isInsideFinish = true;
                    } else if (kind == 6) {
                        start = "WF_IN_Start";
                        voice = 325;
                    } else {
                        start = "WS_IN_Start";
                        voice = 325;
                        flags.isInsideFinish = true;
                    }
                    if (flags.isAnalPlay) start = "A_" + start;
                    if (!action.SetPlay(start, true)) return false;
                    if (mode == 7) voice += 500;
                    else if (mode == 9) voice += 700;
                    Voice(flags, voice);
                    return true;
                }

                if (mode == 3 || mode == 5)
                {
                    if (kind == 3) {
                        if (!action.SetPlay("OLoop", true)) return false;
                        Voice(flags, mode == 3 ? 404 : 603);
                    } else if (kind == 6) {
                        if (!action.SetPlay("Orgasm", true)) return false;
                        Voice(flags, mode == 3 ? 406 : 604);
                        flags.timeDecreaseGaugeCalc = 0f;
                    } else return false;
                    if (mode == 5) {
                        Voice(flags, kind == 3 ? 603 : 604, 1);
                    }
                    return true;
                }
                return false;
            } catch { return false; }
        }
    }
}
