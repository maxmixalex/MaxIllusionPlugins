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
        private static FieldInfo hSceneProcField;
        private static Type femaleDataType;
        private static FieldInfo femaleDataActorField;
        private static FieldInfo femaleDataHostField;
        private static ChaControl lastActiveFemale;
        private static bool patched;
        private static readonly float[] lastListProbe = new float[2];
        private static int forcedTeam = -1;
        private static ProcScope activeProcScope;
        private static CharacterScope activeCharacterScope;

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
            hSceneProcField = AccessTools.Field(type, "hsceneProc");
            if (femaleListField == null || hFlagField == null) {
                log.LogWarning("ExpressionControl compatibility was not applied because its character list or H context field was not found.");
                return;
            }
            harmony.Patch(proc,
                prefix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(BeforeProc)),
                postfix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(AfterProc)),
                finalizer: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(FinalizeProc)));
            harmony.Patch(updateFemale,
                prefix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(BeforeUpdateFemale)),
                postfix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(AfterUpdateFemale)));
            // The native face hooks and eye/mouth blending coroutines also call
            // these methods outside Proc. Their conditions must use the actor's
            // team, including when the shared HSceneProc is focused elsewhere.
            string[] characterMethods = { "ShouldBlockExpressionPatternChange", "ShouldBlockExpressionOpenChange",
                "ShouldBlockHighlightChange", "ShouldBlockBlinkChange", "ShouldBlockHohoAkaChange" };
            for (int i = 0; i < characterMethods.Length; i++) {
                MethodInfo method = AccessTools.Method(type, characterMethods[i]);
                if (method == null || method.GetParameters().Length == 0 ||
                    method.GetParameters()[0].ParameterType != typeof(ChaControl)) continue;
                PatchCharacterMethod(harmony, method, nameof(BeforeCharacterCall));
            }
            MethodInfo setFace = AccessTools.Method(type, "IsIgnoreSetFace");
            if (setFace != null) {
                ParameterInfo[] parameters = setFace.GetParameters();
                if (setFace.ReturnType == typeof(bool) && parameters.Length >= 5 &&
                    parameters[1].ParameterType == typeof(ChaControl) && parameters[4].ParameterType == typeof(FaceListCtrl))
                    PatchCharacterMethod(harmony, setFace, nameof(BeforeSetFace));
            }
            string[] dataMethods = { "GetMatchedUnits", "ProcAllProgram", "RunCharacterAction",
                "GetCharacterOrgasmCount", "AdjustCharacterOrgasmCount" };
            for (int i = 0; i < dataMethods.Length; i++) {
                MethodInfo method = AccessTools.Method(type, dataMethods[i]);
                if (method != null && method.GetParameters().Length > 0)
                    PatchCharacterMethod(harmony, method, nameof(BeforeFemaleDataCall));
            }
            Type femaleData = type.GetNestedType("FemaleData", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo updateState = femaleData == null ? null : AccessTools.Method(femaleData, "UpdateState");
            if (updateState != null) {
                ParameterInfo[] parameters = updateState.GetParameters();
                if (parameters.Length == 3 && parameters[0].ParameterType == typeof(bool) &&
                    parameters[1].ParameterType == typeof(string) && parameters[2].ParameterType == typeof(bool))
                    PatchCharacterMethod(harmony, updateState, nameof(BeforeFemaleDataUpdate));
            }
            patched = true;
            log.LogInfo("MultiTeamH patched ExpressionControl at runtime with per-actor expression contexts.");
        }

        private static void BeforeProc(object __instance, ref ProcScope __state) {
            TeamSession session = FindActiveSession();
            if (session == null || __instance == null) return;
            int target = forcedTeam >= 0 ? forcedTeam : session.ActiveTeam;
            ChaControl active = session.GetFemaleObject(target);
            if (active == null) return;
            HFlag targetFlags = session.GetExpressionFlagsObject(target);
            if (targetFlags == null) return;
            DiagnosticProbe.OnceInfo("expression-target-" + active.GetInstanceID(), "expression-target female=" + active.gameObject.name +
                " flags=" + (targetFlags == null ? "null" : targetFlags.GetInstanceID().ToString()));

            // Resolve and filter first. Unsupported/temporarily missing data must
            // not change the process-wide H context without a restoration scope.
            if (femaleListField == null || hFlagField == null) return;
            object originalFlag = hFlagField.GetValue(null);
            IList original = femaleListField.GetValue(__instance) as IList;
            if (original == null) return;
            bool topLevel = forcedTeam < 0 && activeProcScope == null;
            IList filtered = FilterForTeam(original, session, target, topLevel);
            if (filtered == null) return;
            __state = new ProcScope { OriginalFemaleList = original, OriginalFlag = originalFlag, Session = session,
                Target = target, IsTopLevel = topLevel, Instance = __instance, PreviousScope = activeProcScope,
                PreviousFemale = lastActiveFemale, PreviousState = ExpressionState.Capture(__instance) };
            activeProcScope = __state;
            SwitchExpressionState(__instance, active);
            hFlagField.SetValue(null, targetFlags);
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
            RestoreProcContext(__instance, __state);
            if (__state == null || __state.SessionEnded || !__state.IsTopLevel || __originalMethod == null) return;
            int other = 1 - __state.Target;
            if (__state.Session.GetFemaleObject(other) == null || __state.Session.GetExpressionFlagsObject(other) == null) return;
            int previousForced = forcedTeam;
            try {
                forcedTeam = other;
                __originalMethod.Invoke(__instance, null);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("ExpressionControl parallel Proc failed: " + ex);
            } finally {
                forcedTeam = previousForced;
                if (hFlagField != null) hFlagField.SetValue(null, __state.OriginalFlag);
            }
        }

        private static Exception FinalizeProc(Exception __exception, object __instance, ProcScope __state) {
            try { RestoreProcContext(__instance, __state); }
            catch (Exception cleanupError) {
                DiagnosticProbe.Warning("ExpressionControl context cleanup failed: " + cleanupError);
                if (__exception == null) return cleanupError;
            }
            return __exception;
        }

        private static void RestoreProcContext(object instance, ProcScope state) {
            if (state == null || state.Restored) return;
            state.Restored = true;
            try {
                if (instance != null && state.OriginalFemaleList != null && femaleListField != null)
                    femaleListField.SetValue(instance, state.OriginalFemaleList);
            } finally {
                try { if (hFlagField != null) hFlagField.SetValue(null, state.OriginalFlag); }
                finally {
                    try {
                        if (!state.SessionEnded) {
                            SaveCurrentExpressionState(instance);
                            if (!state.IsTopLevel) RestoreExpressionState(instance, state.PreviousFemale, state.PreviousState);
                        }
                    } finally { if (ReferenceEquals(activeProcScope, state)) activeProcScope = state.PreviousScope; }
                }
            }
        }

        private static void PatchCharacterMethod(Harmony harmony, MethodInfo method, string prefix) {
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(ExpressionControlCompat), prefix),
                postfix: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(AfterCharacterCall)),
                finalizer: new HarmonyMethod(typeof(ExpressionControlCompat), nameof(FinalizeCharacterCall)));
        }

        private static void BeforeCharacterCall(object __instance, ChaControl __0, ref CharacterScope __state) {
            BeginCharacterCall(__instance, __0, ref __state);
        }

        private static void BeforeFemaleDataCall(object __instance, object __0, ref CharacterScope __state) {
            BeginCharacterCall(__instance, GetFemale(__0), ref __state);
        }

        private static void BeforeFemaleDataUpdate(object __instance, ref string __1, ref bool __2, ref CharacterScope __state) {
            ChaControl actor = GetFemale(__instance);
            TeamSession session = FindSession(actor);
            if (session == null) return;
            CacheFemaleDataFields(__instance.GetType());
            object host = femaleDataHostField == null ? null : femaleDataHostField.GetValue(__instance);
            if (host == null) return;
            HFlag flags = session.GetExpressionFlagsObject(GetActorTeam(session, actor));
            if (flags == null) return;
            BeginCharacterCall(host, actor, ref __state);
            __1 = flags.nowAnimStateName ?? string.Empty;
            if (flags.nowAnimationInfo != null) {
                string action = flags.nowAnimationInfo.nameAnimation ?? string.Empty;
                __2 = action.Contains("舐め") || action.Contains("フェラ") || action.Contains("咥え");
            } else if (feraField != null) __2 = Convert.ToBoolean(feraField.GetValue(host));
        }

        private static bool BeforeSetFace(object __instance, ChaControl __1, FaceListCtrl __4,
            ref bool __result, ref CharacterScope __state) {
            TeamSession session = FindSession(__1);
            // ExpressionControl's SetFace prefix writes the face itself. Reject
            // a foreign controller here as well as at the native entry, so the
            // result does not depend on the two plugins' Harmony patch order.
            if (session != null && !session.AllowFaceWrite(__4, __1)) {
                __result = true;
                return false;
            }
            BeginCharacterCall(__instance, __1, ref __state);
            return true;
        }

        private static void BeginCharacterCall(object instance, ChaControl actor, ref CharacterScope state) {
            if (instance == null || actor == null || hFlagField == null) return;
            TeamSession session = FindSession(actor);
            if (session == null) return;
            int team = GetActorTeam(session, actor);
            ChaControl owner = session.GetFemaleObject(team);
            HFlag flags = session.GetExpressionFlagsObject(team);
            if (owner == null || flags == null) return;
            object originalFlag = hFlagField.GetValue(null);
            // Nested per-part callbacks from the same face/program already have
            // the right state and need no extra scope or list allocation.
            if (lastActiveFemale == owner && ReferenceEquals(originalFlag, flags)) return;
            state = new CharacterScope { Instance = instance, Session = session, OriginalFlag = originalFlag,
                PreviousFemale = lastActiveFemale, PreviousState = ExpressionState.Capture(instance),
                PreviousScope = activeCharacterScope, Proc = activeProcScope };
            activeCharacterScope = state;
            SwitchExpressionState(instance, owner);
            hFlagField.SetValue(null, flags);
            if (state.Proc != null && ReferenceEquals(state.Proc.Instance, instance) && femaleListField != null)
                femaleListField.SetValue(instance, state.Proc.OriginalFemaleList);
        }

        private static void AfterCharacterCall(object __instance, CharacterScope __state) {
            RestoreCharacterContext(__state == null ? __instance : __state.Instance, __state);
        }

        private static Exception FinalizeCharacterCall(Exception __exception, object __instance, CharacterScope __state) {
            try { RestoreCharacterContext(__state == null ? __instance : __state.Instance, __state); }
            catch (Exception cleanupError) {
                DiagnosticProbe.Warning("ExpressionControl character context cleanup failed: " + cleanupError);
                if (__exception == null) return cleanupError;
            }
            return __exception;
        }

        private static void RestoreCharacterContext(object instance, CharacterScope state) {
            if (state == null || state.Restored) return;
            state.Restored = true;
            try {
                if (!state.SessionEnded) RestoreExpressionState(instance, state.PreviousFemale, state.PreviousState);
            } finally {
                try { if (hFlagField != null) hFlagField.SetValue(null, state.OriginalFlag); }
                finally {
                    if (ReferenceEquals(activeCharacterScope, state)) activeCharacterScope = state.PreviousScope;
                    ProcScope proc = state.Proc;
                    if (proc != null && !proc.Restored && ReferenceEquals(activeProcScope, proc) &&
                        activeCharacterScope == null && femaleListField != null) {
                        IList filtered = FilterForTeam(proc.OriginalFemaleList, proc.Session, proc.Target, proc.IsTopLevel);
                        if (filtered != null) femaleListField.SetValue(instance, filtered);
                    }
                }
            }
        }

        private static void SaveCurrentExpressionState(object instance) {
            if (instance != null && lastActiveFemale != null) States[lastActiveFemale] = ExpressionState.Capture(instance);
        }

        private static void RestoreExpressionState(object instance, ChaControl previous, ExpressionState fallback) {
            if (instance == null) return;
            if (previous != null) SwitchExpressionState(instance, previous);
            else {
                SaveCurrentExpressionState(instance);
                fallback.Restore(instance);
                lastActiveFemale = null;
            }
        }

        internal static void SessionEnded(TeamSession session) {
            if (session == null) return;
            for (int i = 0; i < 2; i++) {
                ChaControl female = session.GetFemaleObject(i);
                if (ReferenceEquals(female, null)) continue;
                States.Remove(female);
                if (ReferenceEquals(lastActiveFemale, female)) lastActiveFemale = null;
            }
            forcedTeam = -1;
            for (ProcScope scope = activeProcScope; scope != null; scope = scope.PreviousScope)
                if (ReferenceEquals(scope.Session, session)) scope.SessionEnded = true;
            for (CharacterScope scope = activeCharacterScope; scope != null; scope = scope.PreviousScope)
                if (ReferenceEquals(scope.Session, session)) scope.SessionEnded = true;
            Array.Clear(lastListProbe, 0, lastListProbe.Length);
        }

        private static void BeforeUpdateFemale(object __instance) {
            ProcScope scope = activeProcScope;
            if (scope == null || !ReferenceEquals(scope.Instance, __instance) || femaleListField == null) return;
            // UpdateHState can refresh the character list from inside Proc. It
            // must see all previous FemaleData objects to preserve each team's
            // locks/programs instead of recreating the temporarily filtered team.
            femaleListField.SetValue(__instance, scope.OriginalFemaleList);
        }

        private static void AfterUpdateFemale(object __instance) {
            if (__instance == null || femaleListField == null) return;
            IList list = femaleListField.GetValue(__instance) as IList;
            if (list == null) return;
            // Older character discovery can return repeated references after
            // the second character / shared-action slots are rebound. Keep one
            // managed entry before assigning its owning-team controllers.
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
            ProcScope scope = activeProcScope;
            if (scope != null && ReferenceEquals(scope.Instance, __instance)) {
                scope.OriginalFemaleList = list;
                if (activeCharacterScope != null && ReferenceEquals(activeCharacterScope.Instance, __instance)) return;
                IList filtered = FilterForTeam(list, scope.Session, scope.Target, scope.IsTopLevel);
                if (filtered != null) {
                    femaleListField.SetValue(__instance, filtered);
                }
            }
        }

        private static IList FilterForTeam(IList original, TeamSession session, int target, bool includeUnmanaged) {
            IList filtered = Activator.CreateInstance(original.GetType()) as IList;
            if (filtered == null) return null;
            ChaControl femaleOwner = session.GetFemaleObject(target);
            ChaControl maleOwner = session.GetMaleObject(target);
            bool femaleAdded = false, maleAdded = false;
            for (int i = 0; i < original.Count; i++) {
                object data = original[i];
                ChaControl female = GetFemale(data);
                int team = GetActorTeam(session, female);
                if (team < 0) {
                    if (includeUnmanaged) filtered.Add(data);
                } else if (team == target) {
                    if (female == femaleOwner && !femaleAdded) { filtered.Add(data); femaleAdded = true; }
                    else if (female == maleOwner && !maleAdded) { filtered.Add(data); maleAdded = true; }
                }
            }
            return filtered;
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
            object scene = hSceneProcField == null ? null : hSceneProcField.GetValue(null);
            if (hSceneProcField != null && scene == null) return null;
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i] != null && sessions[i].ActiveFemaleObject != null &&
                    (hSceneProcField == null || ReferenceEquals(sessions[i].Proc, scene))) return sessions[i];
            return null;
        }

        private static TeamSession FindSession(ChaControl female) {
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) if (sessions[i] != null && GetActorTeam(sessions[i], female) >= 0) return sessions[i];
            return null;
        }

        private static int GetActorTeam(TeamSession session, ChaControl actor) {
            if (session == null || actor == null) return -1;
            for (int i = 0; i < 2; i++)
                if (session.GetFemaleObject(i) == actor || session.GetMaleObject(i) == actor) return i;
            return -1;
        }

        private static ChaControl GetFemale(object femaleData) {
            if (femaleData == null) return null;
            CacheFemaleDataFields(femaleData.GetType());
            return femaleDataActorField == null ? null : femaleDataActorField.GetValue(femaleData) as ChaControl;
        }

        private static void CacheFemaleDataFields(Type type) {
            if (femaleDataType == type) return;
            femaleDataType = type;
            femaleDataActorField = AccessTools.Field(type, "female");
            femaleDataHostField = AccessTools.Field(type, "instMono") ?? AccessTools.Field(type, "<instMono>k__BackingField");
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
            internal bool Restored;
            internal object Instance;
            internal ProcScope PreviousScope;
            internal ChaControl PreviousFemale;
            internal ExpressionState PreviousState;
            internal bool SessionEnded;
        }

        private sealed class CharacterScope {
            internal object Instance;
            internal TeamSession Session;
            internal object OriginalFlag;
            internal ChaControl PreviousFemale;
            internal ExpressionState PreviousState;
            internal CharacterScope PreviousScope;
            internal ProcScope Proc;
            internal bool Restored, SessionEnded;
        }

        private struct ExpressionState {
            private int orgasmCount;
            private string motion;
            private string action;
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
                Set(motionField, instance, motion ?? string.Empty);
                Set(actionField, instance, action ?? string.Empty);
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
