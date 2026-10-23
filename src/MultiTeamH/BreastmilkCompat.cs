using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MultiTeamH {
    // Breastmilk 1.4.6 has one H coroutine and one set of mutable static
    // counters. Give each team its own counters and coroutine while a managed
    // Free H scene is alive. The original coroutine remains suspended and can
    // resume normally after the scene ends.
    internal static class BreastmilkCompat {
        private static readonly string[] StateFieldNames = {
            "MilkCharge_Speed", "setNum", "spMilkKeep", "spmassage", "spPosition",
            "spnonKeep", "z", "spMilk", "boReset", "timerSiru", "preNum"
        };
        private static FieldInfo[] stateFields;
        private static MethodInfo updateMethod;
        private static MethodInfo procMethod;
        private static MonoBehaviour plugin;
        private static ConfigEntry<bool> enabled;
        private static TeamSession session;
        private static readonly MilkState[] states = new MilkState[2];
        private static readonly IEnumerator[] iterators = new IEnumerator[2];
        private static readonly object[] pendingYield = new object[2];
        private static bool backgroundUpdate;
        private static bool managedIteratorDrive;
        private static bool parallelStateBound;
        private static bool backgroundFailed;
        private static bool disabled;
        private static int lastDriveFrame = -1;
        private static bool loggedError;

        internal static void TryPatch(Harmony harmony, Type type, ManualLogSource log) {
            if (type == null) return;
            updateMethod = AccessTools.Method(type, "Update");
            procMethod = AccessTools.Method(type, "ProcEModeSonyu");
            Type iteratorType = type.GetNestedType("<ProcEModeSonyu>d__61", BindingFlags.NonPublic);
            MethodInfo moveNext = iteratorType == null ? null : AccessTools.Method(iteratorType, "MoveNext");
            stateFields = new FieldInfo[StateFieldNames.Length];
            for (int i = 0; i < stateFields.Length; i++) stateFields[i] = AccessTools.Field(type, StateFieldNames[i]);
            if (updateMethod == null || procMethod == null || moveNext == null || Array.Exists(stateFields, field => field == null)) {
                log.LogWarning("MultiTeamH could not enable parallel Breastmilk processing: unsupported plugin layout.");
                stateFields = null;
                return;
            }
            harmony.Patch(updateMethod,
                prefix: new HarmonyMethod(typeof(BreastmilkCompat), nameof(BeforeUpdate)),
                finalizer: new HarmonyMethod(typeof(BreastmilkCompat), nameof(AfterUpdate)),
                transpiler: new HarmonyMethod(typeof(BreastmilkCompat), nameof(RedirectHotkey)));
            harmony.Patch(moveNext, prefix: new HarmonyMethod(typeof(BreastmilkCompat), nameof(SuspendOriginalIterator)));
            disabled = false;
            log.LogInfo("MultiTeamH enabled separate Breastmilk H processing for both teams.");
        }

        private static IEnumerable<CodeInstruction> RedirectHotkey(IEnumerable<CodeInstruction> instructions) {
            MethodInfo replacement = AccessTools.Method(typeof(BreastmilkCompat), nameof(HotkeyIsDown));
            foreach (CodeInstruction instruction in instructions) {
                MethodInfo called = instruction.operand as MethodInfo;
                if (called != null && called.DeclaringType == typeof(KeyboardShortcut) && called.Name == "IsDown" && called.GetParameters().Length == 0) {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = replacement;
                }
                yield return instruction;
            }
        }

        private static bool HotkeyIsDown(ref KeyboardShortcut shortcut) {
            return !backgroundUpdate && shortcut.IsDown();
        }

        private static bool SuspendOriginalIterator(ref bool __result) {
            if (disabled || session == null || managedIteratorDrive) return true;
            __result = true;
            return false;
        }

        private static void BeforeUpdate(object __instance, ref bool __state) {
            __state = false;
            if (disabled || backgroundUpdate || session == null || stateFields == null) return;
            try {
                if (!EnsureAttached(session, __instance as MonoBehaviour)) return;
                states[session.ActiveTeam].Apply();
                __state = true;
            } catch (Exception ex) { Disable("Breastmilk focused Update setup failed", ex); }
        }

        private static Exception AfterUpdate(Exception __exception, bool __state) {
            if (!__state || disabled || session == null) return __exception;
            try {
                int focus = session.ActiveTeam;
                states[focus].Capture();
                if (__exception == null && !backgroundFailed && !session.IsSwitching && !session.IsLesbianActive)
                    RunBackgroundUpdate(session, 1 - focus);
                states[session.ActiveTeam].Apply();
            } catch (Exception ex) { Disable("Breastmilk focused Update cleanup failed", ex); }
            return __exception;
        }

        private static bool EnsureAttached(TeamSession target, MonoBehaviour instance = null) {
            if (target == null || target.Proc == null || stateFields == null || procMethod == null) return false;
            if (ReferenceEquals(session, target)) return iterators[0] != null && iterators[1] != null;
            if (instance == null) instance = UnityEngine.Object.FindObjectOfType(updateMethod.DeclaringType) as MonoBehaviour;
            if (instance == null) return false;
            plugin = instance;
            MethodInfo getEnabled = AccessTools.Method(updateMethod.DeclaringType, "get_Enabled");
            enabled = getEnabled == null ? null : getEnabled.Invoke(null, null) as ConfigEntry<bool>;
            session = target;
            states[0] = MilkState.FromCurrent(stateFields, true);
            states[1] = MilkState.FromCurrent(stateFields, true);
            for (int i = 0; i < iterators.Length; i++) {
                iterators[i] = procMethod.Invoke(null, null) as IEnumerator;
                pendingYield[i] = null;
            }
            loggedError = false;
            backgroundFailed = false;
            lastDriveFrame = -1;
            DiagnosticProbe.Info("breastmilk-parallel-attached focus=" + (target.ActiveTeam + 1) +
                " iterators=" + (iterators[0] != null) + "/" + (iterators[1] != null));
            if (iterators[0] != null && iterators[1] != null) return true;
            SessionEnded(target);
            disabled = true;
            return false;
        }

        private static void RunBackgroundUpdate(TeamSession target, int background) {
            if (plugin == null || updateMethod == null || states[background] == null) return;
            int previous = target.GetBoundTeamIndex();
            try {
                target.PushControlContext(background);
                states[background].Apply();
                backgroundUpdate = true;
                updateMethod.Invoke(plugin, null);
                states[background].Capture();
            } catch (Exception ex) {
                LogError("Breastmilk background Update failed", ex);
                backgroundFailed = true;
            } finally {
                backgroundUpdate = false;
                target.PopExternalControlContext(previous);
            }
        }

        internal static void Update(TeamSession target) {
            if (disabled) return;
            try {
                if (!EnsureAttached(target) || target.IsSwitching || lastDriveFrame == Time.frameCount) return;
                lastDriveFrame = Time.frameCount;
                for (int i = 0; i < iterators.Length; i++) {
                    if (target.IsLesbianActive && i != target.ActiveTeam) continue;
                    Advance(target, i);
                }
            } catch (Exception ex) { Disable("Breastmilk per-team processing failed", ex); }
        }

        private static void Advance(TeamSession target, int team) {
            IEnumerator iterator = iterators[team];
            if (iterator == null || states[team] == null) return;
            CustomYieldInstruction pending = pendingYield[team] as CustomYieldInstruction;
            if (pending != null && !states[team].MayResume(enabled)) return;
            int previous = target.GetBoundTeamIndex();
            bool contextBound = false;
            try {
                states[team].Apply();
                if (pending != null && pending.keepWaiting) return;
                bool processingCycle = pending != null;
                target.PushControlContext(team);
                contextBound = true;
                managedIteratorDrive = true;
                if (iterator.MoveNext()) {
                    pendingYield[team] = iterator.Current;
                    DiagnosticProbe.OnceInfo("breastmilk-iterator-active-" + team,
                        "breastmilk-iterator-active team=" + (team + 1) + " mode=" + target.GetFlagsObject(team).mode);
                } else {
                    iterators[team] = procMethod.Invoke(null, null) as IEnumerator;
                    pendingYield[team] = null;
                }
                states[team].Capture();
                if (processingCycle) DiagnosticProbe.OnceInfo("breastmilk-cycle-" + team + "-" + target.GetFlagsObject(team).mode,
                    "breastmilk-cycle team=" + (team + 1) + " mode=" + target.GetFlagsObject(team).mode +
                    " state=" + target.GetFlagsObject(team).nowAnimStateName);
            } catch (Exception ex) {
                LogError("Breastmilk H coroutine failed for team " + (team + 1), ex);
                iterators[team] = null;
                pendingYield[team] = null;
            } finally {
                managedIteratorDrive = false;
                if (contextBound) target.PopExternalControlContext(previous);
                if (states[target.ActiveTeam] != null) states[target.ActiveTeam].Apply();
            }
        }

        internal static void EnterParallelTeam(TeamSession target, int team) {
            if (!ReferenceEquals(session, target) || states[team] == null) return;
            states[team].Apply();
            parallelStateBound = true;
        }

        internal static void FocusChanged(TeamSession target, int previous, int current) {
            if (!ReferenceEquals(session, target) || previous == current ||
                states[previous] == null || states[current] == null) return;
            states[previous].Capture();
            states[current].Apply();
        }

        internal static void LeaveParallelTeam(TeamSession target, int team) {
            if (!parallelStateBound || !ReferenceEquals(session, target)) return;
            states[team].Capture();
            states[target.ActiveTeam].Apply();
            parallelStateBound = false;
        }

        internal static void SessionEnded(TeamSession target) {
            if (!ReferenceEquals(session, target)) return;
            if (states[target.ActiveTeam] != null) states[target.ActiveTeam].Apply();
            session = null;
            plugin = null;
            enabled = null;
            for (int i = 0; i < states.Length; i++) {
                states[i] = null;
                iterators[i] = null;
                pendingYield[i] = null;
            }
            backgroundUpdate = false;
            managedIteratorDrive = false;
            parallelStateBound = false;
            lastDriveFrame = -1;
            loggedError = false;
            backgroundFailed = false;
        }

        private static void LogError(string message, Exception error) {
            if (loggedError) return;
            loggedError = true;
            Exception detail = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
            DiagnosticProbe.Error(message + ": " + detail);
        }

        private static void Disable(string message, Exception error) {
            LogError(message, error);
            TeamSession target = session;
            if (target != null) SessionEnded(target);
            disabled = true;
        }

        private sealed class MilkState {
            private readonly FieldInfo[] fields;
            private readonly object[] values;

            private MilkState(FieldInfo[] source) {
                fields = source;
                values = new object[source.Length];
            }

            internal static MilkState FromCurrent(FieldInfo[] fields, bool cloneArrays) {
                MilkState result = new MilkState(fields);
                result.Capture();
                if (cloneArrays) {
                    for (int i = 0; i < result.values.Length; i++) {
                        Array array = result.values[i] as Array;
                        if (array != null) result.values[i] = array.Clone();
                    }
                }
                return result;
            }

            internal void Capture() {
                for (int i = 0; i < fields.Length; i++) values[i] = fields[i].GetValue(null);
            }

            internal bool MayResume(ConfigEntry<bool> enabledEntry) {
                if (enabledEntry == null) return true;
                return values[1] is int && (int)values[1] >= 100 &&
                    values[2] is float && (float)values[2] >= 20f && enabledEntry.Value;
            }

            internal void Apply() {
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, values[i]);
            }
        }
    }
}
