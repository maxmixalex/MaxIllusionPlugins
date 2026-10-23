using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace MultiTeamH {
    /// <summary>
    /// Runtime compatibility for the unmodified ExpressionControl plugin.
    /// All integration lives in MultiTeamH so ExpressionControl can be updated
    /// or restored independently.
    /// </summary>
    internal static class ExpressionControlCompat {
        private static readonly Dictionary<ChaControl, ExpressionState> States = new Dictionary<ChaControl, ExpressionState>();
        private static FieldInfo femaleListField;
        private static FieldInfo orgasmField;
        private static FieldInfo motionField;
        private static FieldInfo actionField;
        private static FieldInfo feraField;
        private static FieldInfo updateStateField;
        private static FieldInfo hFlagField;
        private static ChaControl lastActiveFemale;
        private static bool patched;
        private static readonly float[] lastListProbe = new float[2];
        private static int forcedTeam = -1;

        internal static void TryPatch(Harmony harmony, ManualLogSource log) {
            if (patched || harmony == null) return;
            Type type = Type.GetType("ExpressionControl.ExpressionControl, KK_ExpressionControl", false);
            if (type == null) return;
            MethodInfo proc = AccessTools.Method(type, "Proc");
            MethodInfo updateFemale = AccessTools.Method(type, "UpdateFemale");
            if (proc == null || updateFemale == null) {
                log.LogWarning("ExpressionControl compatibility was not applied because its expected methods were not found.");
                return;
            }

            femaleListField = AccessTools.Field(type, "femaleList");
            orgasmField = AccessTools.Field(type, "orgazumCount");
            motionField = AccessTools.Field(type, "nowMotion");
            actionField = AccessTools.Field(type, "nowHAction");
            feraField = AccessTools.Field(type, "isFeraAction");
            updateStateField = AccessTools.Field(type, "updateState");
            hFlagField = AccessTools.Field(type, "hFrag");
            harmony.Patch(proc,
                prefix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(BeforeProc)),
                postfix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(AfterProc)));
            harmony.Patch(updateFemale,
                postfix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(AfterUpdateFemale)));
            patched = true;
            log.LogInfo("MultiTeamH patched ExpressionControl at runtime; its DLL remains unmodified.");
        }

        private static void BeforeProc(object __instance, ref ProcScope __state) {
            TeamSession session = FindActiveSession();
            if (session == null || __instance == null) return;
            int target = forcedTeam >= 0 ? forcedTeam : session.ActiveTeam;
            ChaControl active = session.GetFemaleObject(target);
            if (active == null) return;
            HFlag targetFlags = session.GetExpressionFlagsObject(target);
            DiagnosticProbe.OnceInfo("expression-target-" + active.GetInstanceID(), "expression-target female=" + active.gameObject.name +
                " flags=" + (targetFlags == null ? "null" : targetFlags.GetInstanceID().ToString()));

            SwitchExpressionState(__instance, active);
            object originalFlag = hFlagField == null ? null : hFlagField.GetValue(null);
            if (hFlagField != null) hFlagField.SetValue(null, targetFlags);
            if (femaleListField == null) return;
            IList original = femaleListField.GetValue(__instance) as IList;
            if (original == null) return;
            IList filtered = Activator.CreateInstance(original.GetType()) as IList;
            if (filtered == null) return;
            bool activeAdded = false;
            for (int i = 0; i < original.Count; i++) {
                object data = original[i];
                ChaControl female = GetFemale(data);
                if (female == null || !session.ContainsFemale(female)) filtered.Add(data);
                else if (female == active && !activeAdded) { filtered.Add(data); activeAdded = true; }
            }
            __state = new ProcScope { OriginalFemaleList = original, OriginalFlag = originalFlag, Session = session,
                Target = target, IsTopLevel = forcedTeam < 0 };
            femaleListField.SetValue(__instance, filtered);
            if (target >= 0 && target < lastListProbe.Length && UnityEngine.Time.realtimeSinceStartup - lastListProbe[target] >= 2f) {
                lastListProbe[target] = UnityEngine.Time.realtimeSinceStartup;
                DiagnosticProbe.Info("expression-list original=" + original.Count + " filtered=" + filtered.Count + " target=" + active.gameObject.name +
                    " forced=" + forcedTeam +
                    " motion=" + Convert.ToString(motionField == null ? null : motionField.GetValue(__instance)) +
                    " action=" + Convert.ToString(actionField == null ? null : actionField.GetValue(__instance)) +
                    " orgasm=" + Convert.ToString(orgasmField == null ? null : orgasmField.GetValue(__instance)));
            }
        }

        private static void AfterProc(object __instance, MethodBase __originalMethod, ProcScope __state) {
            if (__instance != null && __state != null && __state.OriginalFemaleList != null && femaleListField != null) {
                femaleListField.SetValue(__instance, __state.OriginalFemaleList);
            }
            if (__state == null) return;
            if (hFlagField != null) hFlagField.SetValue(null, __state.OriginalFlag);
            if (!__state.IsTopLevel || __originalMethod == null) return;
            int other = 1 - __state.Target;
            if (__state.Session.GetFemaleObject(other) == null) return;
            try {
                forcedTeam = other;
                __originalMethod.Invoke(__instance, null);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("ExpressionControl parallel Proc failed: " + ex);
            } finally {
                forcedTeam = -1;
                if (hFlagField != null) hFlagField.SetValue(null, __state.OriginalFlag);
            }
        }

        private static void AfterUpdateFemale(object __instance) {
            if (__instance == null || femaleListField == null) return;
            IList list = femaleListField.GetValue(__instance) as IList;
            if (list == null) return;
            // UpdateFemale appends to its list. MultiTeamH must call it again
            // after the second character and lesbian slot bindings exist, so
            // remove the stale managed entries it otherwise accumulates.
            HashSet<ChaControl> seenManaged = new HashSet<ChaControl>();
            for (int i = list.Count - 1; i >= 0; i--) {
                ChaControl female = GetFemale(list[i]);
                TeamSession session = FindSession(female);
                if (session == null) continue;
                if (!seenManaged.Add(female)) list.RemoveAt(i);
            }
            for (int i = 0; i < list.Count; i++) {
                object data = list[i];
                ChaControl female = GetFemale(data);
                if (female == null) continue;
                TeamSession session = FindSession(female);
                if (session == null) continue;
                FaceListCtrl face;
                HandCtrl hand;
                HVoiceCtrl.Voice voice;
                int voiceIndex;
                if (!session.TryGetExpressionBindings(female, out face, out hand, out voice, out voiceIndex)) continue;
                SetFieldOrBackingField(data, "faceListCtrl", face);
                SetFieldOrBackingField(data, "nowVoice", voice);
                SetFieldOrBackingField(data, "handCtrl", hand);
                SetFieldOrBackingField(data, "voiceIndex", voiceIndex);
            }
        }

        private static void SwitchExpressionState(object instance, ChaControl active) {
            if (lastActiveFemale == active) return;
            if (lastActiveFemale != null) States[lastActiveFemale] = ExpressionState.Capture(instance);
            lastActiveFemale = active;
            ExpressionState state;
            if (!States.TryGetValue(active, out state)) state = new ExpressionState();
            state.Restore(instance);
        }

        private static TeamSession FindActiveSession() {
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) if (sessions[i] != null && sessions[i].ActiveFemaleObject != null) return sessions[i];
            return null;
        }

        private static TeamSession FindSession(ChaControl female) {
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) if (sessions[i] != null && sessions[i].ContainsFemale(female)) return sessions[i];
            return null;
        }

        private static ChaControl GetFemale(object femaleData) {
            if (femaleData == null) return null;
            FieldInfo field = AccessTools.Field(femaleData.GetType(), "female");
            return field == null ? null : field.GetValue(femaleData) as ChaControl;
        }

        private static void SetFieldOrBackingField(object target, string name, object value) {
            if (target == null) return;
            FieldInfo field = AccessTools.Field(target.GetType(), name) ?? AccessTools.Field(target.GetType(), "<" + name + ">k__BackingField");
            if (field != null) field.SetValue(target, value);
        }

        private sealed class ProcScope {
            internal IList OriginalFemaleList;
            internal object OriginalFlag;
            internal TeamSession Session;
            internal int Target;
            internal bool IsTopLevel;
        }

        private sealed class ExpressionState {
            private int orgasmCount;
            private string motion = string.Empty;
            private string action = string.Empty;
            private bool fera;
            private bool updateState;

            internal static ExpressionState Capture(object instance) {
                return new ExpressionState {
                    orgasmCount = Get<int>(orgasmField, instance),
                    motion = Get<string>(motionField, instance) ?? string.Empty,
                    action = Get<string>(actionField, instance) ?? string.Empty,
                    fera = Get<bool>(feraField, instance),
                    updateState = Get<bool>(updateStateField, instance)
                };
            }

            internal void Restore(object instance) {
                Set(orgasmField, instance, orgasmCount);
                Set(motionField, instance, motion);
                Set(actionField, instance, action);
                Set(feraField, instance, fera);
                Set(updateStateField, instance, updateState);
            }

            private static T Get<T>(FieldInfo field, object instance) {
                if (field == null) return default(T);
                object value = field.GetValue(instance);
                return value is T ? (T)value : default(T);
            }

            private static void Set(FieldInfo field, object instance, object value) {
                if (field != null) field.SetValue(instance, value);
            }
        }
    }
}
