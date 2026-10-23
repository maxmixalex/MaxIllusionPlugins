using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MultiTeamH {
    internal static class RuntimeModCompat {
        private static Type cyuType;
        private static FieldInfo cyuFemaleField;
        private static FieldInfo cyuMaleField;
        private static FieldInfo cyuFlagsField;
        private static FieldInfo cyuHandField;
        private static FieldInfo cyuProcField;
        private static FieldInfo cyuListField;
        private static FieldInfo cyuCameraControlField;
        private static FieldInfo cyuKissFlagField;
        private static FieldInfo cyuKissingField;
        private static MethodInfo cyuKissMethod;
        private static bool cyuPatched;
        private static bool hCtrlPatched;
        private static bool breastmilkPatched;
        private static bool replayingPregnancyEnd;
        private static readonly HashSet<int> loggedCyu = new HashSet<int>();
        private static readonly Dictionary<int, int> cyuKissReadyFrame = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> cyuExplicitKissUntilFrame = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> cyuKissBlockUntilFrame = new Dictionary<int, int>();
        private static readonly Dictionary<object, IEnumerator> cyuKissIterators = new Dictionary<object, IEnumerator>();
        private static readonly Dictionary<int, int> blockedKissSetPlayFrame = new Dictionary<int, int>();
        private static FieldInfo actionFemaleField;
        private static readonly HashSet<string> loggedCharacterCompat = new HashSet<string>();
        private static ManualLogSource logger;
        private static string lastHCtrlState;
        private static float lastHCtrlLog;
        private static float lastHCtrlSwitchProbe;
        private static int hCtrlSwitchCalls;
        private static int forcedHCtrlTarget = -1;
        private static int forcedAutoSpeedTarget = -1;
        private static bool suppressAutoSpeedOnEnable;
        private static FieldInfo[] hCtrlStateFields;
        private static FieldInfo hCtrlAutoSpeedField;
        private static FieldInfo[] hCtrlAutoSpeedStateFields;
        private static readonly Dictionary<object, HCtrlState[]> hCtrlStates = new Dictionary<object, HCtrlState[]>();
        private static readonly Dictionary<object, object> autoSpeedOwners = new Dictionary<object, object>();

        internal static void TryPatch(Harmony harmony, ManualLogSource log) {
            logger = log;
            TryOptionalCompat("CyuNoVR", delegate { TryPatchCyu(harmony, log); }, log);
            TryOptionalCompat("KK_HCtrl", delegate { TryPatchHCtrl(harmony, log); }, log);
            TryOptionalCompat("Breastmilk", delegate { TryPatchBreastmilk(harmony, log); }, log);
            TryOptionalCompat("KK_Pregnancy", delegate { TryPatchPregnancy(harmony, log); }, log);
            TryOptionalCompat("KK_PregnancyPlus", delegate { TryPatchPregnancyPlus(harmony, log); }, log);
        }

        private static void TryOptionalCompat(string name, Action patch, ManualLogSource log) {
            try { patch(); }
            catch (Exception ex) {
                // Optional integrations must never prevent the main plugin,
                // its Update loop, or the Free H entry from starting.
                log.LogWarning("MultiTeamH skipped " + name + " compatibility after a patch error: " + ex);
            }
        }

        private static void TryPatchCyu(Harmony harmony, ManualLogSource log) {
            cyuType = Type.GetType("Bero.CyuNoVR.Cyu, CyuNoVR", false);
            Type fixType = Type.GetType("Bero.CyuNoVR.FixCyu, CyuNoVR", false);
            if (cyuType == null || fixType == null) return;
            cyuFemaleField = AccessTools.Field(cyuType, "female");
            cyuMaleField = AccessTools.Field(cyuType, "male");
            cyuFlagsField = AccessTools.Field(cyuType, "flags");
            cyuHandField = AccessTools.Field(cyuType, "hand");
            cyuProcField = AccessTools.Field(cyuType, "hSceneProc");
            cyuCameraControlField = AccessTools.Field(cyuType, "cameraControl");
            cyuKissFlagField = AccessTools.Field(cyuType, "kissFlag");
            cyuKissingField = AccessTools.Field(cyuType, "kissing");
            cyuKissMethod = AccessTools.Method(cyuType, "Kiss", new[] { typeof(bool) });
            actionFemaleField = AccessTools.Field(typeof(HActionBase), "female");
            cyuListField = AccessTools.Field(fixType, "lstFemale");
            HarmonyMethod activeGuard = new HarmonyMethod(typeof(RuntimeModCompat), nameof(AllowCyuForActiveTeam));
            string[] guardedMethods = { "Update", "OnGUI", "Kiss", "LateUpdateHook" };
            for (int i = 0; i < guardedMethods.Length; i++) {
                MethodInfo method = AccessTools.Method(cyuType, guardedMethods[i]);
                if (method != null) harmony.Patch(method, prefix: activeGuard);
            }
            Type[] cyuNested = cyuType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < cyuNested.Length; i++) {
                MethodInfo moveNext = AccessTools.Method(cyuNested[i], "MoveNext");
                if (moveNext == null) continue;
                if (cyuNested[i].Name.StartsWith("<Waitfor>", StringComparison.Ordinal)) {
                    harmony.Patch(moveNext,
                        prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(BeforeCyuWaitfor)),
                        postfix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AfterCyuWaitfor)),
                        finalizer: new HarmonyMethod(typeof(RuntimeModCompat), nameof(FinalizeExternalControl)));
                } else if (cyuNested[i].Name.StartsWith("<BeroKiss>", StringComparison.Ordinal)) {
                    harmony.Patch(moveNext,
                        prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(BeforeCyuKissCoroutine)),
                        postfix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AfterCyuKissCoroutine)),
                        finalizer: new HarmonyMethod(typeof(RuntimeModCompat), nameof(FinalizeExternalControl)));
                }
            }
            Type fixClosure = fixType.GetNestedType("<>c", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo rebuildCharacter = fixClosure == null ? null : AccessTools.Method(fixClosure, "<Waitfor>b__1_2");
            if (rebuildCharacter != null) harmony.Patch(rebuildCharacter,
                prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(PreserveManagedCyuComponent)));
            // Suppressing IEnumerator.MoveNext completes and permanently kills
            // CyuNoVR's kiss coroutine. Keep its coroutines alive across focus
            // switches; the component entry points below still select the team.
            Type hooksType = Type.GetType("Bero.CyuNoVR.Hooks, CyuNoVR", false);
            MethodInfo getCyu = hooksType == null ? null : AccessTools.Method(hooksType, "GetCyu");
            if (getCyu != null) {
                try { harmony.Patch(getCyu, prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(GetActiveCyu))); }
                catch (Exception ex) { getCyu = null; log.LogWarning("Could not redirect CyuNoVR.GetCyu: " + ex.Message); }
            }
            MethodInfo setPlay = AccessTools.Method(typeof(HActionBase), "SetPlay", new[] { typeof(string), typeof(bool) });
            if (setPlay != null) harmony.Patch(setPlay,
                prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(BeforeHActionSetPlay)),
                postfix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AfterHActionSetPlay)));
            cyuPatched = getCyu != null;
            if (cyuPatched) log.LogInfo("MultiTeamH patched CyuNoVR character lookup for both teams.");
        }

        private static void TryPatchBreastmilk(Harmony harmony, ManualLogSource log) {
            Type type = Type.GetType("GOL_H_BreastMilk.ModMilkMgr, Breastmilk", false);
            if (type == null) return;
            HarmonyMethod redirect = new HarmonyMethod(typeof(RuntimeModCompat), nameof(RedirectBreastmilkContext));
            string[] names = { "Update", "ptInit", "cumsplash", "cumShot", "cumShot2", "cumShotaibu", "cumShotaibuEx",
                "cumShotEx", "cumShotEx2", "shotMilk", "shotMilk2", "shotMilkaibu", "shotMilkaibuEx", "shotMilkEx", "shotMilkEx2" };
            for (int i = 0; i < names.Length; i++) {
                MethodInfo method = AccessTools.Method(type, names[i]);
                if (method == null) continue;
                harmony.Patch(method, transpiler: redirect);
                breastmilkPatched = true;
            }
            Type[] nested = type.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < nested.Length; i++) {
                if (!nested[i].Name.StartsWith("<ProcEMode", StringComparison.Ordinal)) continue;
                MethodInfo moveNext = AccessTools.Method(nested[i], "MoveNext");
                if (moveNext == null) continue;
                harmony.Patch(moveNext, transpiler: redirect);
                breastmilkPatched = true;
            }
            if (breastmilkPatched) {
                log.LogInfo("MultiTeamH patched Breastmilk HFlag lookup for the bound team.");
                BreastmilkCompat.TryPatch(harmony, type, log);
            }
        }

        private static IEnumerable<CodeInstruction> RedirectBreastmilkContext(IEnumerable<CodeInstruction> instructions) {
            MethodInfo replacement = AccessTools.Method(typeof(RuntimeModCompat), nameof(GetBoundHFlag));
            foreach (CodeInstruction instruction in instructions) {
                MethodInfo called = instruction.operand as MethodInfo;
                if (called != null && called.IsGenericMethod && called.Name == "FindObjectOfType") {
                    Type[] args = called.GetGenericArguments();
                    if (args.Length == 1 && args[0] == typeof(HFlag)) {
                        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                        instruction.operand = replacement;
                    }
                }
                yield return instruction;
            }
        }

        private static HFlag GetBoundHFlag() {
            TeamSession session = FindActiveSession();
            if (session == null) return UnityEngine.Object.FindObjectOfType<HFlag>();
            int bound = session.GetBoundTeamIndex();
            HFlag flags = bound < 0 ? null : session.GetFlagsObject(bound);
            return flags ?? session.ActiveFlagsObject;
        }

        private static void TryPatchPregnancy(Harmony harmony, ManualLogSource log) {
            Type type = Type.GetType("KK_Pregnancy.PregnancyGameController, KK_Pregnancy", false);
            if (type == null) return;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            HarmonyMethod postfix = new HarmonyMethod(typeof(RuntimeModCompat), nameof(ReplayPregnancyEndForSecondTeam));
            int patched = 0;
            for (int i = 0; i < methods.Length; i++) {
                MethodInfo method = methods[i];
                if (method.Name != "OnEndH") continue;
                ParameterInfo[] parameters = method.GetParameters();
                bool hasHFlag = false;
                for (int p = 0; p < parameters.Length; p++) if (parameters[p].ParameterType == typeof(HFlag)) { hasHFlag = true; break; }
                if (!hasHFlag) continue;
                harmony.Patch(method, postfix: postfix);
                patched++;
            }
            if (patched != 0) log.LogInfo("MultiTeamH patched KK_Pregnancy end-of-H processing for both teams (" + patched + " overload)." );
        }

        private static void ReplayPregnancyEndForSecondTeam(object __instance, MethodBase __originalMethod, object[] __args) {
            if (replayingPregnancyEnd || __instance == null || __args == null) return;
            TeamSession session = FindActiveSession();
            if (session == null) return;
            HFlag second = session.GetFlagsObject(1);
            if (second == null) return;
            int flagIndex = -1;
            for (int i = 0; i < __args.Length; i++) if (__args[i] is HFlag) { flagIndex = i; break; }
            if (flagIndex < 0 || ReferenceEquals(__args[flagIndex], second)) return;
            object[] replayArgs = (object[])__args.Clone();
            replayArgs[flagIndex] = second;
            try {
                replayingPregnancyEnd = true;
                __originalMethod.Invoke(__instance, replayArgs);
            } catch (Exception ex) {
                if (logger != null) logger.LogWarning("KK_Pregnancy second-team processing failed: " + ex.Message);
            } finally { replayingPregnancyEnd = false; }
        }

        private static void TryPatchPregnancyPlus(Harmony harmony, ManualLogSource log) {
            Type type = Type.GetType("KK_PregnancyPlus.PregnancyPlusCharaController, KK_PregnancyPlus", false);
            MethodInfo method = type == null ? null : type.GetMethod("WatchForUserKeyPress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null) return;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AllowPregnancyPlusShortcutForActiveTeam)));
            log.LogInfo("MultiTeamH scoped PregnancyPlus live-inflation shortcuts to the focused team.");
        }

        private static bool AllowPregnancyPlusShortcutForActiveTeam(object __instance) {
            if (__instance == null) return true;
            PropertyInfo property = AccessTools.Property(__instance.GetType().BaseType, "ChaControl");
            ChaControl female = property == null ? null : property.GetValue(__instance, null) as ChaControl;
            if (female == null) return true;
            TeamSession session = FindSession(female);
            return session == null || session.IsActiveFemale(female);
        }

        private static void TryPatchHCtrl(Harmony harmony, ManualLogSource log) {
            Type type = Type.GetType("KK_HCtrl.HCtrl, KK_HCtrl", false);
            if (type == null) return;
            hCtrlStateFields = FindFields(type, new[] { "eMode", "maleGauge", "femaleGauge", "loopSpeed", "isSW",
                "autoSpeedType", "autoSpeedLinkageType", "autoSpeedHalfVal", "autoSpeedMin", "autoSpeedMax", "autoRestart",
                "autoSpeedAibuClickTimer", "autoSpeedAibuClickFlag", "autoRestartWait", "nowAnimStateName",
                "autoOrgaMale", "autoOrgaFemale", "isHookSpeed" });
            hCtrlAutoSpeedField = AccessTools.Field(type, "autoSpeed");
            Type autoSpeedType = Type.GetType("KK_HCtrl.AutoSpeed, KK_HCtrl", false);
            hCtrlAutoSpeedStateFields = autoSpeedType == null ? new FieldInfo[0] :
                FindFields(autoSpeedType, new[] { "dest", "dest2", "dest2Speed", "dest2Range", "<now>k__BackingField",
                    "<mode>k__BackingField", "<sw>k__BackingField", "isAutoSW",
                    "<min>k__BackingField", "<max>k__BackingField", "slideSpeed",
                    "destTimer", "swTimer", "destTimerMin", "destTimerMax", "swTimerMin", "swTimerMax",
                    "sliderSpeedMin", "sliderSpeedMax" });
            MethodInfo update = AccessTools.Method(type, "Update");
            MethodInfo gui = AccessTools.Method(type, "GUIFunc");
            HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeModCompat), nameof(BeforeHCtrl));
            HarmonyMethod postfix = new HarmonyMethod(typeof(RuntimeModCompat), nameof(AfterHCtrl));
            HarmonyMethod finalizer = new HarmonyMethod(typeof(RuntimeModCompat), nameof(FinalizeExternalControl));
            if (update != null) harmony.Patch(update, prefix: prefix, postfix: postfix, finalizer: finalizer);
            if (gui != null) harmony.Patch(gui, prefix: prefix, postfix: postfix, finalizer: finalizer);
            MethodInfo changeSW = AccessTools.Method(type, "ChangeSW", new[] { typeof(bool) });
            if (changeSW != null) harmony.Patch(changeSW,
                prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(ProbeHCtrlChangeSW)));
            MethodInfo autoUpdate = autoSpeedType == null ? null : AccessTools.Method(autoSpeedType, "Update");
            if (autoUpdate != null) harmony.Patch(autoUpdate,
                prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(BeforeAutoSpeed)),
                postfix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AfterAutoSpeed)),
                finalizer: finalizer);
            MethodInfo autoOnEnable = autoSpeedType == null ? null : AccessTools.Method(autoSpeedType, "OnEnable");
            if (autoOnEnable != null) harmony.Patch(autoOnEnable,
                prefix: new HarmonyMethod(typeof(RuntimeModCompat), nameof(AllowAutoSpeedOnEnable)));
            Type[] nested = type.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < nested.Length; i++) {
                if (!nested[i].Name.StartsWith("<CoAutoRestart", StringComparison.Ordinal)) continue;
                MethodInfo moveNext = AccessTools.Method(nested[i], "MoveNext");
                if (moveNext != null) harmony.Patch(moveNext, prefix: prefix, postfix: postfix, finalizer: finalizer);
            }
            hCtrlPatched = update != null || gui != null;
            if (hCtrlPatched) log.LogInfo("MultiTeamH patched KK_HCtrl with an independent target-team context.");
        }

        internal static void TeamBound(TeamSession session, TeamState team) {
            if (session == null || team == null) return;
            if (cyuPatched) SyncCyu(session, team.Female, team.Male, team.Flags, team.Controllers.Hand);
            VerifyCharacterController(team.Female, "KK_Pregnancy.PregnancyCharaController, KK_Pregnancy", "KK_Pregnancy");
            VerifyCharacterController(team.Female, "KK_PregnancyPlus.PregnancyPlusCharaController, KK_PregnancyPlus", "KK_PregnancyPlus");
        }

        internal static void TeamSwitching(TeamSession session, TeamState oldTeam, TeamState newTeam) {
            if (session == null) return;
            if (oldTeam != null && oldTeam.Female != null) {
                // A normal switch must not stop every CyNoVR coroutine. Tab is
                // held until the native animation and CyNoVR tail have both
                // been settled; only an unexpected leftover needs repair.
                if (IsKissBusy(oldTeam)) {
                    session.CancelNativeKiss(oldTeam);
                    CompleteKiss(oldTeam);
                    session.EndKissCamera(oldTeam.Female, false);
                    session.SetKissHeadOverride(oldTeam.Female, false);
                    session.RestoreMotionLookAfterKiss(oldTeam.Female);
                    DiagnosticProbe.Warning("kiss-leftover-cleared-before-switch female=" + oldTeam.Female.gameObject.name);
                }
                int oldId = oldTeam.Female.GetInstanceID();
                cyuKissReadyFrame.Remove(oldId);
                cyuExplicitKissUntilFrame.Remove(oldId);
                cyuKissBlockUntilFrame[oldId] = Time.frameCount + 12;
            }
            if (newTeam != null) {
                if (newTeam.Female != null) cyuKissBlockUntilFrame[newTeam.Female.GetInstanceID()] = Time.frameCount + 12;
                SyncCyu(session, newTeam.Female, newTeam.Male, newTeam.Flags, newTeam.Controllers.Hand);
            }
        }

        internal static void ActionChanging(TeamSession session, TeamState team) {
            if (session == null || team == null || team.Female == null) return;
            session.CancelNativeKiss(team);
            Component cyu = cyuType == null ? null : team.Female.GetComponent(cyuType);
            try {
                if (cyu != null && cyuKissFlagField != null && (bool)cyuKissFlagField.GetValue(cyu) && cyuKissMethod != null)
                    cyuKissMethod.Invoke(cyu, new object[] { false });
                StopCyuKissCoroutine(cyu);
                if (cyu != null && cyuKissFlagField != null) cyuKissFlagField.SetValue(cyu, false);
                if (cyu != null && cyuKissingField != null) cyuKissingField.SetValue(cyu, false);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("Could not finish CyNoVR kiss before action change: " + ex.Message);
            }
            int femaleId = team.Female.GetInstanceID();
            cyuKissReadyFrame.Remove(femaleId);
            cyuExplicitKissUntilFrame.Remove(femaleId);
            cyuKissBlockUntilFrame[femaleId] = Time.frameCount + 15;
            session.SetKissHeadOverride(team.Female, false);
        }

        internal static void CompleteKiss(TeamState team) {
            if (team == null || team.Female == null) return;
            int femaleId = team.Female.GetInstanceID();
            Component cyu = cyuType == null ? null : team.Female.GetComponent(cyuType);
            bool cyuFlag = cyu != null && cyuKissFlagField != null && (bool)cyuKissFlagField.GetValue(cyu);
            bool cyuTail = cyu != null && cyuKissingField != null && (bool)cyuKissingField.GetValue(cyu);
            bool handFlag = false;
            try {
                if (team.Controllers != null && team.Controllers.Hand != null)
                    handFlag = Traverse.Create(team.Controllers.Hand).Field("isKiss").GetValue<bool>();
                if (cyuFlag && cyuKissMethod != null) cyuKissMethod.Invoke(cyu, new object[] { false });
                StopCyuKissCoroutine(cyu);
                if (cyu != null && cyuKissFlagField != null) cyuKissFlagField.SetValue(cyu, false);
                if (cyu != null && cyuKissingField != null) cyuKissingField.SetValue(cyu, false);
                if (handFlag) Traverse.Create(team.Controllers.Hand).Field("isKiss").SetValue(false);
                cyuKissReadyFrame.Remove(femaleId);
                cyuExplicitKissUntilFrame.Remove(femaleId);
                cyuKissBlockUntilFrame[femaleId] = Time.frameCount + 30;
                DiagnosticProbe.Info("kiss-tail-closed female=" + team.Female.gameObject.name +
                    " cyuFlag=" + cyuFlag + " cyuTail=" + cyuTail + " handFlag=" + handFlag);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("kiss tail cleanup failed for " + team.Female.gameObject.name + ": " + ex.Message);
            }
        }

        private static void StopCyuKissCoroutine(Component cyu) {
            MonoBehaviour behaviour = cyu as MonoBehaviour;
            IEnumerator iterator;
            if (behaviour == null || !cyuKissIterators.TryGetValue(cyu, out iterator)) return;
            cyuKissIterators.Remove(cyu);
            // Cyu also starts permanent Waitfor/GetMouse loops in Start. A
            // StopAllCoroutines here kills those for the rest of the scene.
            // The native Kiss method allows only one active BeroKiss iterator.
            behaviour.StopCoroutine(iterator);
        }

        private static void ReleaseCyuKissIterator(ExternalControlScope state) {
            if (state == null || state.CyuOwner == null || state.CyuIterator == null) return;
            IEnumerator tracked;
            if (cyuKissIterators.TryGetValue(state.CyuOwner, out tracked) && ReferenceEquals(tracked, state.CyuIterator))
                cyuKissIterators.Remove(state.CyuOwner);
        }

        internal static bool ShouldBlockKissSetPlay(HActionBase action) {
            if (action == null || action.female == null) return false;
            TeamSession session = FindSession(action.female);
            if (session != null && !session.IsActiveFemale(action.female)) return true;
            int until;
            return cyuKissBlockUntilFrame.TryGetValue(action.female.GetInstanceID(), out until) && Time.frameCount <= until;
        }

        internal static bool IsKissBusy(TeamState team) {
            if (team == null || team.Female == null) return false;
            if (IsKissAnimationActive(team)) return true;
            try {
                bool hand = team.Controllers != null && team.Controllers.Hand != null &&
                    Traverse.Create(team.Controllers.Hand).Field("isKiss").GetValue<bool>();
                Component cyu = cyuType == null ? null : team.Female.GetComponent(cyuType);
                bool cyuFlag = cyu != null && cyuKissFlagField != null && (bool)cyuKissFlagField.GetValue(cyu);
                bool cyuTail = cyu != null && cyuKissingField != null && (bool)cyuKissingField.GetValue(cyu);
                return hand || cyuFlag || cyuTail;
            } catch { return false; }
        }

        internal static bool IsKissAnimationActive(TeamState team) {
            if (team == null || team.Female == null) return false;
            AnimatorStateInfo state = team.Female.getAnimatorStateInfo(0);
            if (state.IsName("K_Touch") || state.IsName("K_Loop")) return true;
            return false;
        }

        internal static void MarkKissSetPlayBlocked(HActionBase action) {
            if (action != null && action.female != null) blockedKissSetPlayFrame[action.female.GetInstanceID()] = Time.frameCount;
        }

        private static void VerifyCharacterController(ChaControl female, string typeName, string label) {
            if (female == null) return;
            Type type = Type.GetType(typeName, false);
            if (type == null) return;
            string key = label + ":" + female.GetInstanceID();
            if (!loggedCharacterCompat.Add(key)) return;
            Component controller = female.GetComponent(type);
            if (logger != null) {
                if (controller != null) logger.LogInfo(label + " controller bound to " + female.gameObject.name + ".");
                else logger.LogWarning(label + " controller missing on " + female.gameObject.name + ".");
            }
        }

        internal static void Update(TeamSession session) {
            if (!cyuPatched || session == null) return;
            for (int i = 0; i < 2; i++) {
                ChaControl female = session.GetFemaleObject(i);
                ChaControl male = session.GetMaleObject(i);
                HFlag flags = session.GetFlagsObject(i);
                HandCtrl hand = session.GetHandObject(i);
                SyncCyu(session, female, male, flags, hand);
                if (female == null || !session.IsActiveFemale(female) || cyuKissMethod == null || cyuKissFlagField == null) continue;
                int femaleId = female.GetInstanceID();
                int untilFrame;
                if (!cyuExplicitKissUntilFrame.TryGetValue(femaleId, out untilFrame)) continue;
                if (Time.frameCount > untilFrame) {
                    cyuExplicitKissUntilFrame.Remove(femaleId);
                    continue;
                }
                Component cyu = cyuType == null ? null : female.GetComponent(cyuType);
                if (cyu == null) continue;
                try {
                    if (!(bool)cyuKissFlagField.GetValue(cyu)) cyuKissMethod.Invoke(cyu, new object[] { true });
                    if ((bool)cyuKissFlagField.GetValue(cyu)) {
                        cyuExplicitKissUntilFrame.Remove(femaleId);
                        DiagnosticProbe.Info("cyu-setplay-kiss accepted female=" + female.gameObject.name);
                    }
                } catch (Exception ex) {
                    DiagnosticProbe.OnceInfo("cyu-setplay-error-" + femaleId, "Cyu SetPlay trigger failed: " + ex.Message);
                }
            }
        }

        private static void BeforeHActionSetPlay(HActionBase __instance, string _nextAnimation) {
            if (__instance == null || string.IsNullOrEmpty(_nextAnimation) || actionFemaleField == null) return;
            if (_nextAnimation != "K_Touch" && _nextAnimation != "K_Loop") return;
            ChaControl female = actionFemaleField.GetValue(__instance) as ChaControl;
            TeamSession session = FindSession(female);
            if (session == null || !session.IsActiveFemale(female)) return;
            // SetPlay itself loads the native kiss camera. Capturing in the
            // postfix records that kiss view as the team's ordinary camera.
            if (!ShouldBlockKissSetPlay(__instance)) session.CapturePreKissCamera(female);
        }

        private static void AfterHActionSetPlay(HActionBase __instance, string _nextAnimation) {
            if (__instance == null || string.IsNullOrEmpty(_nextAnimation) || actionFemaleField == null) return;
            if (_nextAnimation != "K_Touch" && _nextAnimation != "K_Loop") return;
            ChaControl female = actionFemaleField.GetValue(__instance) as ChaControl;
            int blockedFrame;
            if (female != null && blockedKissSetPlayFrame.TryGetValue(female.GetInstanceID(), out blockedFrame) && blockedFrame == Time.frameCount) {
                blockedKissSetPlayFrame.Remove(female.GetInstanceID());
                return;
            }
            TeamSession session = FindSession(female);
            if (session == null || !session.IsActiveFemale(female)) return;
            cyuExplicitKissUntilFrame[female.GetInstanceID()] = Time.frameCount + 90;
            DiagnosticProbe.Info("cyu-setplay-kiss queued female=" + female.gameObject.name + " animation=" + _nextAnimation);
        }

        private static void SyncCyu(TeamSession session, ChaControl female, ChaControl male, HFlag flags, HandCtrl hand) {
            if (female == null || cyuType == null) return;
            IList list = cyuListField == null ? null : cyuListField.GetValue(null) as IList;
            if (list != null) {
                int index = list.IndexOf(female);
                // FixCyu keeps stable character indices. Reordering this list on
                // every focus change invalidates CyuNoVR's own cached mapping.
                if (index < 0) list.Add(female);
            }
            Component cyu = female.GetComponent(cyuType);
            if (cyu == null && female.gameObject.name == "MultiTeamH_Female2") {
                try {
                    cyu = female.gameObject.AddComponent(cyuType);
                    if (logger != null) logger.LogInfo("CyuNoVR component added to " + female.gameObject.name + ".");
                } catch (Exception ex) {
                    if (logger != null) logger.LogWarning("Could not add CyuNoVR to " + female.gameObject.name + ": " + ex.Message);
                }
            }
            if (cyu == null) {
                int missingId = female.GetInstanceID();
                if (loggedCyu.Add(missingId) && logger != null) logger.LogWarning("CyuNoVR component was not found on " + female.gameObject.name + ".");
                return;
            }
            Behaviour cyuBehaviour = cyu as Behaviour;
            if (cyuBehaviour != null) cyuBehaviour.enabled = true;
            Set(cyuFemaleField, cyu, female);
            Set(cyuMaleField, cyu, male);
            Set(cyuFlagsField, cyu, flags);
            Set(cyuHandField, cyu, hand);
            Set(cyuProcField, cyu, session.Proc);
            Set(cyuCameraControlField, cyu, flags == null ? null : flags.ctrlCamera);
            int id = female.GetInstanceID();
            if (loggedCyu.Add(id) && logger != null) logger.LogInfo("CyuNoVR bound to " + female.gameObject.name + ".");
        }

        private static bool AllowCyuForActiveTeam(object __instance, MethodBase __originalMethod, object[] __args) {
            if (__instance == null || cyuFemaleField == null) return true;
            object cyu = __instance;
            if (!cyuType.IsInstanceOfType(cyu)) {
                FieldInfo[] fields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                cyu = null;
                for (int i = 0; i < fields.Length; i++) {
                    if (!cyuType.IsAssignableFrom(fields[i].FieldType)) continue;
                    cyu = fields[i].GetValue(__instance);
                    if (cyu != null) break;
                }
                if (cyu == null) return true;
            }
            ChaControl female = cyuFemaleField.GetValue(cyu) as ChaControl;
            if (female == null) return true;
            TeamSession session = FindSession(female);
            if (session == null) return true;
            ChaControl male;
            HFlag flags;
            HandCtrl hand;
            if (session.TryGetTeamBindings(female, out male, out flags, out hand)) SyncCyu(session, female, male, flags, hand);
            // CyuNoVR moves the shared H camera from Update/LateUpdateHook, so
            // only the focused component may run. Its coroutine remains alive
            // while focus is elsewhere and resumes against the same character.
            bool active = session.IsActiveFemale(female);
            if (__originalMethod != null && __originalMethod.Name == "Kiss") {
                bool requested = __args != null && __args.Length != 0 && __args[0] is bool && (bool)__args[0];
                int femaleId = female.GetInstanceID();
                int blockedUntil;
                if (requested && active && cyuKissBlockUntilFrame.TryGetValue(femaleId, out blockedUntil) &&
                    Time.frameCount <= blockedUntil) {
                    DiagnosticProbe.OnceInfo("cyu-kiss-tail-block-" + femaleId + "-" + blockedUntil,
                        "cyu kiss re-entry blocked during exit tail female=" + female.gameObject.name);
                    return false;
                }
                session.SetKissHeadOverride(female, requested && active);
                if (requested && !active) {
                    cyuKissReadyFrame.Remove(femaleId);
                    DiagnosticProbe.OnceInfo("cyu-kiss-block-inactive-" + femaleId,
                        "cyu Kiss(true) blocked for inactive female=" + female.gameObject.name);
                    return false;
                }
                if (requested && active) {
                    session.CapturePreKissCamera(female);
                    int readyFrame;
                    if (!cyuKissReadyFrame.TryGetValue(femaleId, out readyFrame)) {
                        cyuKissReadyFrame[femaleId] = Time.frameCount + 12;
                        DiagnosticProbe.Info("cyu-kiss-delayed female=" + female.gameObject.name + " untilFrame=" + (Time.frameCount + 12));
                        return false;
                    }
                    if (Time.frameCount < readyFrame) return false;
                    cyuKissReadyFrame.Remove(femaleId);
                    session.PrepareKissCamera(female);
                } else {
                    cyuKissReadyFrame.Remove(femaleId);
                    // BeroKiss continues its exit animation after Kiss(false).
                    // Restore only when that coroutine reports completion.
                }
                DiagnosticProbe.OnceInfo("cyu-kiss-" + female.GetInstanceID() + "-" + requested + "-" + active,
                    "cyu Kiss female=" + female.gameObject.name + " requested=" + requested + " active=" + active + " component=" + ((Component)cyu).GetInstanceID());
                // Each team's kiss flag is independent. The BeroKiss iterator
                // below is what pauses camera movement for an inactive team.
                return true;
            }
            return active;
        }

        private static void BeforeCyuWaitfor(object __instance, ref ExternalControlScope __state) {
            if (__instance == null || cyuType == null) return;
            FieldInfo ownerField = AccessTools.Field(__instance.GetType(), "<>4__this");
            object cyu = ownerField == null ? null : ownerField.GetValue(__instance);
            ChaControl female = cyu == null || cyuFemaleField == null ? null : cyuFemaleField.GetValue(cyu) as ChaControl;
            TeamSession session = FindSession(female);
            int team = session == null ? -1 : session.GetTeamIndex(female);
            if (team < 0) return;
            FieldInfo cachedActionsField = AccessTools.Field(__instance.GetType(), "<lstProc>5__2");
            object expectedActions = session.GetTeamActions(team);
            if (cachedActionsField != null && ReferenceEquals(cachedActionsField.GetValue(__instance), expectedActions)) return;
            int previousTarget = session.GetBoundTeamIndex();
            __state = new ExternalControlScope { Session = session, Target = team, PreviousTarget = previousTarget };
            session.PushControlContext(team);
            DiagnosticProbe.OnceInfo("cyu-waitfor-" + female.GetInstanceID(), "cyu Waitfor context female=" + female.gameObject.name + " team=" + (team + 1));
        }

        private static void AfterCyuWaitfor(ExternalControlScope __state) {
            RestoreExternalContext(__state);
        }

        private static bool BeforeCyuKissCoroutine(object __instance, ref bool __result, ref ExternalControlScope __state) {
            object cyu;
            ChaControl female;
            TeamSession session;
            int team;
            if (!TryGetCyuOwner(__instance, out cyu, out female, out session, out team)) return true;
            int previousTarget = session.GetBoundTeamIndex();
            bool inactive = !session.IsActiveFemale(female);
            HFlag flags = session.GetFlagsObject(team);
            IEnumerator iterator = __instance as IEnumerator;
            if (iterator != null) cyuKissIterators[cyu] = iterator;
            __state = new ExternalControlScope { Session = session, Target = team, PreviousTarget = previousTarget,
                Female = female, CyuOwner = cyu, CyuIterator = iterator,
                Camera = inactive ? CyuCameraSnapshot.Capture(flags == null ? null : flags.ctrlCamera) : null };
            session.PushControlContext(team);
            return true;
        }

        private static void AfterCyuKissCoroutine(bool __result, ExternalControlScope __state) {
            if (__state == null || __state.Session == null) return;
            if (!__result) ReleaseCyuKissIterator(__state);
            RestoreExternalContext(__state);
            if (!__result && __state.Female != null) {
                // The coroutine can finish while the native kiss animator is
                // still exiting. Keep the snapshot and Tab guard until the
                // session observes 30 consecutive non-kiss frames.
                DiagnosticProbe.Info("cyu-kiss-coroutine-finished female=" + __state.Female.gameObject.name);
            }
        }

        private static bool TryGetCyuOwner(object iterator, out object cyu, out ChaControl female, out TeamSession session, out int team) {
            cyu = null;
            female = null;
            session = null;
            team = -1;
            if (iterator == null || cyuType == null) return false;
            FieldInfo ownerField = AccessTools.Field(iterator.GetType(), "<>4__this");
            cyu = ownerField == null ? null : ownerField.GetValue(iterator);
            female = cyu == null || cyuFemaleField == null ? null : cyuFemaleField.GetValue(cyu) as ChaControl;
            session = FindSession(female);
            team = session == null ? -1 : session.GetTeamIndex(female);
            return team >= 0;
        }

        private static bool PreserveManagedCyuComponent(ChaControl __0) {
            TeamSession session = FindSession(__0);
            if (session == null || __0 == null) return true;
            ChaControl male;
            HFlag flags;
            HandCtrl hand;
            if (!session.TryGetTeamBindings(__0, out male, out flags, out hand)) return true;
            IList list = cyuListField == null ? null : cyuListField.GetValue(null) as IList;
            if (list != null && !list.Contains(__0)) list.Add(__0);
            SyncCyu(session, __0, male, flags, hand);
            return false;
        }

        private static bool GetActiveCyu(ref object __result) {
            TeamSession session = FindActiveSession();
            if (session == null || cyuType == null) return true;
            ChaControl female = session.ActiveFemaleObject;
            if (female == null) return true;
            SyncCyu(session, female, session.ActiveMaleObject, session.ActiveFlagsObject, session.ActiveHandObject);
            Component component = female.GetComponent(cyuType);
            if (component == null) return true;
            __result = component;
            return false;
        }

        private static bool BeforeHCtrl(object __instance, MethodBase __originalMethod, ref ExternalControlScope __state) {
            TeamSession session = FindActiveSession();
            if (session == null) return true;
            bool processFrame = __originalMethod != null && __originalMethod.Name == "Update";
            // GUI is intentionally shown once for the focused team. Update is
            // replayed under every checked team context so one HCtrl command can
            // advance both independent H simulations.
            bool guiFrame = __originalMethod != null && __originalMethod.Name == "GUIFunc";
            int[] targets = session.GetHCtrlTargets();
            if (targets.Length == 0) return !processFrame;
            int target = forcedHCtrlTarget;
            if (target < 0) {
                target = guiFrame ? session.ActiveTeam : targets[0];
                if (!guiFrame) for (int i = 0; i < targets.Length; i++) if (targets[i] == session.ActiveTeam) { target = targets[i]; break; }
            }
            int previousTarget = session.GetBoundTeamIndex();
            // Publish the cleanup scope before any operation can partially
            // rebind the shared scene or throw while restoring plugin state.
            __state = new ExternalControlScope { Session = session, ProcessFrame = processFrame, IsTopLevel = forcedHCtrlTarget < 0,
                Target = target, PreviousTarget = previousTarget, Started = DiagnosticProbe.Start(),
                Method = __originalMethod == null ? "unknown" : __originalMethod.DeclaringType.Name + "." + __originalMethod.Name,
                IsGui = guiFrame, CursorLocked = GameCursor.isLock,
                HCtrlOwner = IsHCtrlInstance(__instance) ? __instance : null };
            session.PushControlContext(target);
            __state.UiPass = processFrame && session.BeginExternalUiPass(target);
            if (__state.HCtrlOwner != null) {
                RegisterAutoSpeedOwner(__instance);
                RestoreHCtrlState(__instance, target);
            }
            if (processFrame && Time.realtimeSinceStartup - lastHCtrlLog >= 2f)
                __state.Before = session.DescribeControlState() + " plugin={" + DescribeHCtrlState(__instance) + "}";
            return true;
        }

        private static void AfterHCtrl(object __instance, MethodBase __originalMethod, ExternalControlScope __state) {
            if (__state == null || __state.Session == null) return;
            if (IsHCtrlInstance(__instance)) {
                CaptureHCtrlState(__instance, __state.Target);
            }
            double elapsed = DiagnosticProbe.ElapsedMs(__state.Started);
            float now = Time.realtimeSinceStartup;
            if (__state.ProcessFrame && (elapsed >= 20.0 || now - lastHCtrlLog >= 2f)) {
                string after = __state.Session.DescribeControlState();
                DiagnosticProbe.Info("hctrl-scope method=" + __state.Method + " elapsedMs=" + elapsed.ToString("0.0") +
                    " before={" + (__state.Before ?? "not-sampled") + "} after={" + after + "}");
                lastHCtrlState = after;
                lastHCtrlLog = now;
            }
            RestoreExternalContext(__state);
            if (!__state.ProcessFrame || !__state.IsTopLevel || __originalMethod == null) {
                RestoreExternalPluginState(__state);
                return;
            }
            int[] targets = __state.Session.GetHCtrlTargets();
            for (int i = 0; i < targets.Length; i++) {
                if (targets[i] == __state.Target) continue;
                try {
                    forcedHCtrlTarget = targets[i];
                    __originalMethod.Invoke(__instance, null);
                } catch (Exception ex) {
                    if (logger != null) logger.LogError("KK_HCtrl parallel update failed for team " + (targets[i] + 1) + ": " + ex);
                } finally {
                    forcedHCtrlTarget = -1;
                }
            }
            RestoreExternalPluginState(__state);
        }

        private static void RegisterAutoSpeedOwner(object hCtrl) {
            object auto = hCtrl == null || hCtrlAutoSpeedField == null ? null : hCtrlAutoSpeedField.GetValue(hCtrl);
            if (auto != null) autoSpeedOwners[auto] = hCtrl;
        }

        private static bool AllowAutoSpeedOnEnable() { return !suppressAutoSpeedOnEnable; }

        private static void ProbeHCtrlChangeSW(bool isOn) {
            TeamSession session = FindActiveSession();
            if (session == null) return;
            hCtrlSwitchCalls++;
            float now = Time.realtimeSinceStartup;
            if (now - lastHCtrlSwitchProbe < 2f) return;
            DiagnosticProbe.Info("hctrl-switch-request count=" + hCtrlSwitchCalls +
                " bound=" + (session.GetBoundTeamIndex() + 1) + " focus=" + (session.ActiveTeam + 1) +
                " requestedStrong=" + isOn);
            hCtrlSwitchCalls = 0;
            lastHCtrlSwitchProbe = now;
        }

        private static string DescribeHCtrlState(object instance) {
            if (!IsHCtrlInstance(instance)) return "unavailable";
            List<string> values = new List<string>();
            string[] names = { "eMode", "loopSpeed", "isSW", "autoSpeedType", "autoSpeedLinkageType", "autoRestart", "autoOrgaMale", "autoOrgaFemale", "isHookSpeed" };
            for (int n = 0; n < names.Length; n++) {
                for (int i = 0; i < hCtrlStateFields.Length; i++) {
                    if (hCtrlStateFields[i].Name != names[n]) continue;
                    values.Add(names[n] + "=" + hCtrlStateFields[i].GetValue(instance));
                    break;
                }
            }
            object auto = hCtrlAutoSpeedField == null ? null : hCtrlAutoSpeedField.GetValue(instance);
            Behaviour behaviour = auto as Behaviour;
            values.Add("autoEnabled=" + (behaviour != null && behaviour.enabled));
            if (auto != null) {
                for (int i = 0; i < hCtrlAutoSpeedStateFields.Length; i++) {
                    string name = hCtrlAutoSpeedStateFields[i].Name;
                    if (name == "<now>k__BackingField" || name == "dest" || name == "destTimer")
                        values.Add(name + "=" + hCtrlAutoSpeedStateFields[i].GetValue(auto));
                }
            }
            return string.Join(",", values.ToArray());
        }

        private static bool BeforeAutoSpeed(object __instance, MethodBase __originalMethod, ref ExternalControlScope __state) {
            object owner;
            if (__instance == null || !autoSpeedOwners.TryGetValue(__instance, out owner)) return true;
            TeamSession session = FindActiveSession();
            if (session == null) return true;
            int target = forcedAutoSpeedTarget >= 0 ? forcedAutoSpeedTarget : session.ActiveTeam;
            int previousTarget = session.GetBoundTeamIndex();
            __state = new ExternalControlScope { Session = session, Target = target, PreviousTarget = previousTarget,
                IsTopLevel = forcedAutoSpeedTarget < 0, Method = "AutoSpeed.Update",
                CursorLocked = GameCursor.isLock, HCtrlOwner = owner };
            session.PushControlContext(target);
            __state.UiPass = session.BeginExternalUiPass(target);
            RestoreHCtrlState(owner, target);
            return true;
        }

        private static void AfterAutoSpeed(object __instance, MethodBase __originalMethod, ExternalControlScope __state) {
            if (__state == null || __state.Session == null) return;
            object owner = __state.HCtrlOwner;
            CaptureHCtrlState(owner, __state.Target);
            RestoreExternalContext(__state);
            if (__state.IsTopLevel && __originalMethod != null) {
                int[] targets = __state.Session.GetHCtrlTargets();
                for (int i = 0; i < targets.Length; i++) {
                    if (targets[i] == __state.Target) continue;
                    if (!GetHCtrlStates(owner)[targets[i]].AutoSpeedEnabled) continue;
                    try {
                        forcedAutoSpeedTarget = targets[i];
                        __originalMethod.Invoke(__instance, null);
                    } catch (Exception ex) {
                        if (logger != null) logger.LogError("KK_HCtrl AutoSpeed parallel update failed for team " + (targets[i] + 1) + ": " + ex);
                    } finally { forcedAutoSpeedTarget = -1; }
                }
            }
            RestoreExternalPluginState(__state);
        }

        private static void RestoreExternalContext(ExternalControlScope state) {
            if (state == null || state.Session == null || state.ContextRestored) return;
            state.ContextRestored = true;
            // A failed camera/UI restore must not strand the shared HSceneProc
            // on the background team. Each subsequent cleanup still runs.
            try {
                if (state.Camera != null) state.Camera.Restore();
            } finally {
                try { state.Session.EndExternalUiPass(state.Target, state.UiPass); }
                finally {
                    try {
                        if (state.UiPass) TeamSession.RestoreSharedCursorLock(state.CursorLocked, "external-plugin");
                    } finally { state.Session.PopExternalControlContext(state.PreviousTarget); }
                }
            }
        }

        private static void RestoreExternalPluginState(ExternalControlScope state) {
            if (state == null || state.Session == null || state.PluginStateRestored) return;
            state.PluginStateRestored = true;
            int restoreTarget = state.PreviousTarget >= 0 ? state.PreviousTarget : state.Session.ActiveTeam;
            if (state.HCtrlOwner != null) RestoreHCtrlState(state.HCtrlOwner, restoreTarget);
        }

        private static Exception FinalizeExternalControl(Exception __exception, ExternalControlScope __state) {
            if (__exception != null) ReleaseCyuKissIterator(__state);
            Exception cleanupError = null;
            try { RestoreExternalContext(__state); }
            catch (Exception ex) { cleanupError = ex; }
            try { RestoreExternalPluginState(__state); }
            catch (Exception ex) { if (cleanupError == null) cleanupError = ex; }
            if (cleanupError != null) DiagnosticProbe.Warning("External plugin context cleanup failed: " + cleanupError);
            return __exception ?? cleanupError;
        }

        private static FieldInfo[] FindFields(Type type, string[] names) {
            List<FieldInfo> result = new List<FieldInfo>();
            for (int i = 0; i < names.Length; i++) {
                FieldInfo field = AccessTools.Field(type, names[i]);
                if (field != null) result.Add(field);
            }
            return result.ToArray();
        }

        private static bool IsHCtrlInstance(object instance) {
            return instance != null && hCtrlStateFields != null && hCtrlStateFields.Length != 0 &&
                hCtrlStateFields[0].DeclaringType.IsInstanceOfType(instance);
        }

        private static HCtrlState[] GetHCtrlStates(object instance) {
            HCtrlState[] result;
            if (!hCtrlStates.TryGetValue(instance, out result)) {
                result = new[] {
                    HCtrlState.Capture(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields),
                    HCtrlState.Capture(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields)
                };
                hCtrlStates.Add(instance, result);
            }
            return result;
        }

        internal static void SessionStarted() {
            hCtrlStates.Clear();
            autoSpeedOwners.Clear();
            forcedHCtrlTarget = -1;
            forcedAutoSpeedTarget = -1;
            cyuKissReadyFrame.Clear();
            cyuExplicitKissUntilFrame.Clear();
            cyuKissBlockUntilFrame.Clear();
            cyuKissIterators.Clear();
            blockedKissSetPlayFrame.Clear();
            hCtrlSwitchCalls = 0;
            lastHCtrlSwitchProbe = 0f;
            loggedCyu.Clear();
            loggedCharacterCompat.Clear();
        }

        internal static void SessionEnded() {
            SessionStarted();
            lastHCtrlState = null;
            replayingPregnancyEnd = false;
        }

        private static void SyncHCtrlConfiguration(object instance, int sourceTeam) {
            HCtrlState[] states = GetHCtrlStates(instance);
            if (sourceTeam < 0 || sourceTeam >= states.Length || states[sourceTeam] == null) return;
            for (int i = 0; i < states.Length; i++) {
                if (i == sourceTeam || states[i] == null) continue;
                states[i].CopyConfigurationFrom(states[sourceTeam], hCtrlStateFields, hCtrlAutoSpeedStateFields);
            }
        }

        private static void RestoreHCtrlState(object instance, int team) {
            if (instance == null || team < 0 || team > 1) return;
            HCtrlState[] states = GetHCtrlStates(instance);
            if (states[team] == null) {
                states[team] = HCtrlState.Capture(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields);
                return;
            }
            states[team].Restore(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields);
        }

        private static void CaptureHCtrlState(object instance, int team) {
            if (instance == null || team < 0 || team > 1) return;
            HCtrlState[] states = GetHCtrlStates(instance);
            if (states[team] == null) states[team] = HCtrlState.Capture(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields);
            else states[team].CaptureFrom(instance, hCtrlStateFields, hCtrlAutoSpeedField, hCtrlAutoSpeedStateFields);
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

        private static void Set(FieldInfo field, object target, object value) {
            if (field != null) field.SetValue(target, value);
        }

        private sealed class ExternalControlScope {
            internal TeamSession Session;
            internal bool ProcessFrame;
            internal bool IsTopLevel;
            internal int Target;
            internal int PreviousTarget;
            internal long Started;
            internal string Before;
            internal string Method;
            internal bool IsGui;
            internal bool UiPass;
            internal bool CursorLocked;
            internal ChaControl Female;
            internal CyuCameraSnapshot Camera;
            internal object HCtrlOwner;
            internal object CyuOwner;
            internal IEnumerator CyuIterator;
            internal bool ContextRestored;
            internal bool PluginStateRestored;
        }

        private sealed class HCtrlState {
            private object[] values;
            private object[] autoSpeedValues;
            private bool autoSpeedEnabled;
            internal bool AutoSpeedEnabled { get { return autoSpeedEnabled; } }

            internal static HCtrlState Capture(object instance, FieldInfo[] fields, FieldInfo autoField, FieldInfo[] autoFields) {
                HCtrlState result = new HCtrlState();
                result.values = new object[fields.Length];
                result.autoSpeedValues = new object[autoFields.Length];
                result.CaptureFrom(instance, fields, autoField, autoFields);
                return result;
            }

            internal void CaptureFrom(object instance, FieldInfo[] fields, FieldInfo autoField, FieldInfo[] autoFields) {
                for (int i = 0; i < fields.Length; i++) values[i] = fields[i].GetValue(instance);
                object auto = autoField == null ? null : autoField.GetValue(instance);
                Behaviour autoBehaviour = auto as Behaviour;
                autoSpeedEnabled = autoBehaviour != null && autoBehaviour.enabled;
                if (auto != null) for (int i = 0; i < autoFields.Length; i++) autoSpeedValues[i] = autoFields[i].GetValue(auto);
                else Array.Clear(autoSpeedValues, 0, autoSpeedValues.Length);
            }

            internal void Restore(object instance, FieldInfo[] fields, FieldInfo autoField, FieldInfo[] autoFields) {
                for (int i = 0; i < fields.Length && i < values.Length; i++) fields[i].SetValue(instance, values[i]);
                object auto = autoField == null ? null : autoField.GetValue(instance);
                if (auto != null) {
                    for (int i = 0; i < autoFields.Length && i < autoSpeedValues.Length; i++) autoFields[i].SetValue(auto, autoSpeedValues[i]);
                    Behaviour autoBehaviour = auto as Behaviour;
                    if (autoBehaviour != null && autoBehaviour.enabled != autoSpeedEnabled) {
                        // Unity calls OnEnable synchronously. HCtrl's handler
                        // starts a new strength cycle, so an internal team
                        // context swap must not run it every frame.
                        bool previous = suppressAutoSpeedOnEnable;
                        suppressAutoSpeedOnEnable = true;
                        try { autoBehaviour.enabled = autoSpeedEnabled; }
                        finally { suppressAutoSpeedOnEnable = previous; }
                    }
                }
            }

            internal void CopyConfigurationFrom(HCtrlState source, FieldInfo[] fields, FieldInfo[] autoFields) {
                if (source == null) return;
                for (int i = 0; i < fields.Length && i < values.Length && i < source.values.Length; i++) {
                    string name = fields[i].Name;
                    if (name == "isSW" || name == "autoSpeedType" || name == "autoSpeedLinkageType" ||
                        name == "autoSpeedHalfVal" || name == "autoSpeedMin" || name == "autoSpeedMax" ||
                        name == "autoRestart" || name == "autoOrgaMale" || name == "autoOrgaFemale") values[i] = source.values[i];
                }
                autoSpeedEnabled = source.autoSpeedEnabled;
                for (int i = 0; i < autoFields.Length && i < autoSpeedValues.Length && i < source.autoSpeedValues.Length; i++) {
                    string name = autoFields[i].Name;
                    if (name == "<min>k__BackingField" || name == "<max>k__BackingField" ||
                        name == "destTimerMin" || name == "destTimerMax" || name == "swTimerMin" ||
                        name == "swTimerMax" || name == "sliderSpeedMin" || name == "sliderSpeedMax") {
                        autoSpeedValues[i] = source.autoSpeedValues[i];
                    }
                }
            }
        }

        private sealed class CyuCameraSnapshot {
            private CameraControl_Ver2 control;
            private Vector3 targetPos;
            private Vector3 angle;
            private Vector3 direction;
            private float fov;
            private Transform targetObject;
            private Transform worldBase;
            private Vector3 worldBasePosition;
            private Quaternion worldBaseRotation;
            private bool restoreWorldBasePose;
            private Transform cameraTransform;
            private Vector3 cameraPosition;
            private Quaternion cameraRotation;

            internal static CyuCameraSnapshot Capture(CameraControl_Ver2 value) {
                CyuCameraSnapshot result = new CyuCameraSnapshot { control = value };
                if (value == null) return result;
                result.targetPos = value.TargetPos;
                result.angle = value.CameraAngle;
                result.direction = value.CameraDir;
                result.fov = value.CameraFov;
                result.targetObject = value.targetObj;
                result.worldBase = value.transBase;
                if (result.worldBase != null && result.worldBase.name.StartsWith("CameraBase", StringComparison.Ordinal)) {
                    result.worldBasePosition = result.worldBase.position;
                    result.worldBaseRotation = result.worldBase.rotation;
                    result.restoreWorldBasePose = true;
                }
                if (value.thisCmaera != null) {
                    result.cameraTransform = value.thisCmaera.transform;
                    result.cameraPosition = result.cameraTransform.position;
                    result.cameraRotation = result.cameraTransform.rotation;
                }
                return result;
            }

            internal void Restore() {
                if (control == null) return;
                control.targetObj = targetObject;
                control.transBase = worldBase;
                if (restoreWorldBasePose && worldBase != null)
                    worldBase.SetPositionAndRotation(worldBasePosition, worldBaseRotation);
                control.TargetPos = targetPos;
                control.CameraAngle = angle;
                control.CameraDir = direction;
                control.CameraFov = fov;
                if (cameraTransform != null) cameraTransform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
        }
    }
}
