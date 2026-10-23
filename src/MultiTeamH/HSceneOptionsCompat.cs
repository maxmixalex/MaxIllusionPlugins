using System;
using System.Reflection;
using HarmonyLib;

namespace MultiTeamH {
    // HSceneOptions caches HFlag, lstProc and AnimationToggle.proc when the
    // scene is created. The native H scene can be rebound to either team, but
    // those cached references do not follow it unless we update them as well.
    internal static class HSceneOptionsCompat {
        private static readonly Type OptionsType = Type.GetType("KK_HSceneOptions.HSceneOptions, KK_HSceneOptions", false);
        private static readonly FieldInfo FlagsField = OptionsType == null ? null : AccessTools.Field(OptionsType, "flags");
        private static readonly FieldInfo ActionsField = OptionsType == null ? null : AccessTools.Field(OptionsType, "lstProc");
        private static readonly FieldInfo FemalesField = OptionsType == null ? null : AccessTools.Field(OptionsType, "lstFemale");
        private static readonly FieldInfo MalesField = OptionsType == null ? null : AccessTools.Field(OptionsType, "lstmMale");
        private static readonly FieldInfo VoiceField = OptionsType == null ? null : AccessTools.Field(OptionsType, "voice");
        private static readonly FieldInfo HandsField = OptionsType == null ? null : AccessTools.Field(OptionsType, "hands");
        private static readonly FieldInfo ToggleField = OptionsType == null ? null : AccessTools.Field(OptionsType, "animationToggle");
        private static readonly Type ToggleType = Type.GetType("KK_HSceneOptions.AnimationToggle, KK_HSceneOptions", false);
        private static readonly FieldInfo ProcField = ToggleType == null ? null : AccessTools.Field(ToggleType, "proc");
        private static readonly FieldInfo ForceOLoopField = ToggleType == null ? null : AccessTools.Field(ToggleType, "forceOLoop");
        private static readonly FieldInfo ForceStopVoiceField = ToggleType == null ? null : AccessTools.Field(ToggleType, "forceStopVoice");
        private static readonly FieldInfo OrgasmTimerField = ToggleType == null ? null : AccessTools.Field(ToggleType, "orgasmTimer");
        private static readonly FieldInfo AnimationNameField = ToggleType == null ? null : AccessTools.Field(ToggleType, "animationName");
        private static readonly MethodInfo UpdateProcMethod = ToggleType == null ? null : AccessTools.Method(ToggleType, "UpdateProc");
        private static readonly ToggleState[] States = { new ToggleState(), new ToggleState() };
        private static TeamState boundTeam;
        private static object boundToggle;
        private static bool warned;

        internal static void Bind(HSceneProc scene, TeamState team, int index) {
            if (OptionsType == null || scene == null || team == null || team.Flags == null || index < 0 || index >= States.Length) return;
            try {
                object toggle = ToggleField == null ? null : ToggleField.GetValue(null);
                if (boundToggle != toggle) {
                    boundTeam = null;
                    boundToggle = toggle;
                    States[0].Initialized = false;
                    States[1].Initialized = false;
                }
                if (toggle != null && boundTeam != null && boundTeam != team) {
                    int oldIndex = boundTeam.IsClone ? 1 : 0;
                    States[oldIndex].Capture(toggle);
                }
                bool changed = boundTeam != team;
                FlagsField?.SetValue(null, team.Flags);
                ActionsField?.SetValue(null, team.Actions);
                FemalesField?.SetValue(null, scene.lstFemale);
                MalesField?.SetValue(null, team.ContextMales);
                VoiceField?.SetValue(null, scene.voice);
                object[] hands = HandsField == null ? null : HandsField.GetValue(null) as object[];
                if (hands != null && hands.Length >= 2) {
                    hands[0] = team.Controllers.Hand;
                    hands[1] = scene.hand1;
                }
                boundTeam = team;
                if (toggle == null || team.Flags.nowAnimationInfo == null) return;
                int mode = (int)team.Flags.mode;
                HActionBase action = team.Actions != null && mode >= 0 && mode < team.Actions.Count ? team.Actions[mode] : null;
                bool wrongAction = ProcField != null && action != null && !ReferenceEquals(ProcField.GetValue(toggle), action);
                bool wrongAnimation = AnimationNameField != null &&
                    !Equals(AnimationNameField.GetValue(toggle), team.Flags.nowAnimationInfo.nameAnimation);
                if ((changed || wrongAction || wrongAnimation) && UpdateProcMethod != null) UpdateProcMethod.Invoke(toggle, null);
                if (changed) States[index].Restore(toggle);
            } catch (Exception ex) {
                if (!warned) {
                    warned = true;
                    DiagnosticProbe.Warning("hscene-options bind failed: " + ex);
                }
            }
        }

        internal static void Reset() {
            boundTeam = null;
            boundToggle = null;
            States[0].Initialized = false;
            States[1].Initialized = false;
            warned = false;
        }

        internal static string Describe(TeamState team) {
            if (OptionsType == null) return "absent";
            object toggle = ToggleField == null ? null : ToggleField.GetValue(null);
            if (toggle == null) return "toggle-null";
            int mode = team == null || team.Flags == null ? -1 : (int)team.Flags.mode;
            HActionBase expected = team == null || team.Actions == null || mode < 0 || mode >= team.Actions.Count
                ? null : team.Actions[mode];
            return "flagsMatch=" + ReferenceEquals(FlagsField?.GetValue(null), team?.Flags) +
                " actionMatch=" + ReferenceEquals(ProcField?.GetValue(toggle), expected) +
                " forceOLoop=" + (ForceOLoopField == null ? "unknown" : ForceOLoopField.GetValue(toggle).ToString());
        }

        private sealed class ToggleState {
            internal bool Initialized;
            private bool forceOLoop;
            private bool forceStopVoice;
            private float orgasmTimer;

            internal void Capture(object toggle) {
                if (ForceOLoopField != null) forceOLoop = (bool)ForceOLoopField.GetValue(toggle);
                if (ForceStopVoiceField != null) forceStopVoice = (bool)ForceStopVoiceField.GetValue(toggle);
                if (OrgasmTimerField != null) orgasmTimer = (float)OrgasmTimerField.GetValue(toggle);
                Initialized = true;
            }

            internal void Restore(object toggle) {
                ForceOLoopField?.SetValue(toggle, forceOLoop);
                ForceStopVoiceField?.SetValue(toggle, forceStopVoice);
                OrgasmTimerField?.SetValue(toggle, orgasmTimer);
            }
        }
    }
}
