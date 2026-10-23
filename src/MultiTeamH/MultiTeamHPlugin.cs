using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: System.Reflection.AssemblyFileVersion(MultiTeamH.MultiTeamHPlugin.Version)]

namespace MultiTeamH {
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    [BepInDependency("ExpressionControl", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("bero.cyu.cyunovr", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("KK_HCtrl", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
#if KK
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
#endif
    [BepInPlugin(GUID, Name, Version)]
    public sealed class MultiTeamHPlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.MultiTeamH";
        public const string Name = "MultiTeamH";
        public const string Version = "0.11.62";

        internal static MultiTeamHPlugin Instance { get; private set; }
        internal static ConfigEntry<bool> Enabled { get; private set; }
        internal static ConfigEntry<bool> ShowInactiveTeam { get; private set; }
        internal static ConfigEntry<float> InitialSpacing { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> SwitchTeamKey { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> ToggleWindowKey { get; private set; }
        internal static ConfigEntry<bool> DebugLogging { get; private set; }
        internal static ConfigEntry<bool> PreventMiddleClickAuto { get; private set; }
        internal static ConfigEntry<string> RememberedFemaleCard { get; private set; }
        internal static ConfigEntry<string> RememberedMaleCard { get; private set; }
        internal static ConfigEntry<int> RememberedStage { get; private set; }
        internal static ConfigEntry<int> RememberedStatus { get; private set; }

        private Harmony harmony;
        private TeamSession session;
        private FreeHModeController freeHMode;
        private bool showWindow;
        private bool windowMinimized;
        private Rect windowRect = new Rect(20f, 180f, 300f, 370f);
        private GUISkin panelSkin;
        private GUIStyle foldedWindowStyle;
        private bool mouseDownOnWindow;

        private void Awake() {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Enable two-team Free H support.");
            ShowInactiveTeam = Config.Bind("General", "Show inactive team", true, "Keep both teams visible while only the selected team receives H controls.");
            InitialSpacing = Config.Bind("General", "Initial team spacing", 1.75f, new ConfigDescription("Initial world-space X offset of team 2.", new AcceptableValueRange<float>(0.5f, 5f)));
            SwitchTeamKey = Config.Bind("Shortcuts", "Switch team", new KeyboardShortcut(KeyCode.Tab), "Switch the team controlled by the vanilla H UI.");
            ToggleWindowKey = Config.Bind("Shortcuts", "Show window", new KeyboardShortcut(KeyCode.Minus), "Show or hide the MultiTeamH control panel.");
            if (ToggleWindowKey.Value.MainKey == KeyCode.F8) ToggleWindowKey.Value = new KeyboardShortcut(KeyCode.Minus);
            DebugLogging = Config.Bind("Debug", "Logging", false, "Write detailed rebinding messages to the BepInEx log.");
            PreventMiddleClickAuto = Config.Bind("Controls", "Prevent middle-click auto", true, "Prevent the native speed panel from enabling automatic motion through an accidental middle click.");
            RememberedFemaleCard = Config.Bind("Team 2 cards", "Female card", string.Empty, "Last selected team 2 female card.");
            RememberedMaleCard = Config.Bind("Team 2 cards", "Male card", string.Empty, "Last selected team 2 male card.");
            RememberedStage = Config.Bind("Team 2 cards", "H stage", 0, "Last selected team 2 H stage.");
            RememberedStatus = Config.Bind("Team 2 cards", "Pregnancy status", 0, "Last selected team 2 pregnancy status.");
            MultiTeamSelection.Configure(RememberedFemaleCard, RememberedMaleCard, RememberedStage, RememberedStatus, Config);
            DiagnosticProbe.Initialize(Logger);

            harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(CardPathHooks));
            harmony.PatchAll(typeof(Hooks));
            harmony.PatchAll(typeof(SecondaryControllerLoadHooks));
            harmony.PatchAll(typeof(HSceneUpdateProbe));
            harmony.PatchAll(typeof(TeamFaceWriteHooks));
            harmony.PatchAll(typeof(NativeThreePPortraitHook));
            harmony.PatchAll(typeof(BackgroundSpriteBoolWriteHooks));
            harmony.PatchAll(typeof(BackgroundSpriteVoidWriteHooks));
            harmony.PatchAll(typeof(BackgroundHandUiWriteHooks));
            harmony.PatchAll(typeof(SpecialActionLabelHooks));
            harmony.PatchAll(typeof(SpecialActionMenuBuildHooks));
            harmony.PatchAll(typeof(SpecialActionCategoryClickHooks));
            harmony.PatchAll(typeof(SpecialActionMotionClickHooks));
            ExpressionControlCompat.TryPatch(harmony, Logger);
            RuntimeModCompat.TryPatch(harmony, Logger);
            Logger.LogInfo("MultiTeamH startup completed; Free H entry discovery is active.");
        }

        private void OnDestroy() {
            DisposeSession();
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }

        private void Update() {
            if (!Enabled.Value) return;
            // The Free H selection screen and the running H session have
            // separate lifetimes.  A session can remain alive briefly while
            // the selection screen is already back, so never gate discovery
            // of its entry button on session == null.
            if (freeHMode == null) DiscoverFreeHScene();
            if (ToggleWindowKey.Value.IsDown() || Input.GetKeyDown(KeyCode.KeypadMinus)) showWindow = !showWindow;
            if (session != null && SwitchTeamKey.Value.IsDown()) session.SwitchTo(1 - session.ActiveTeam);
        }

        private void OnGUI() {
            if (!Enabled.Value) return;
            if (showWindow && session != null) {
                if (panelSkin == null) {
                    panelSkin = MultiTeamGuiSkin.Create(GUI.skin);
                    foldedWindowStyle = MultiTeamGuiSkin.CreateFoldedWindowStyle(panelSkin.window);
                }
                GUISkin oldSkin = GUI.skin;
                Color oldColor = GUI.color;
                Color oldBackground = GUI.backgroundColor;
                Color oldContent = GUI.contentColor;
                GUI.skin = panelSkin;
                GUI.color = Color.white;
                GUI.backgroundColor = new Color(1f, 1f, 1f, 0.82f);
                GUI.contentColor = Color.black;
                windowRect.height = windowMinimized ? 20f : 420f;
                windowRect = GUI.Window(68150240, windowRect, DrawWindow, Name + " " + Version,
                    windowMinimized ? foldedWindowStyle : panelSkin.window);
                GUI.skin = oldSkin;
                GUI.color = oldColor;
                GUI.backgroundColor = oldBackground;
                GUI.contentColor = oldContent;
                windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
                windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - 24f));
                HandleWindowMouseInput();
            }
        }

        private void HandleWindowMouseInput() {
            bool anyDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool anyHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (anyDown) mouseDownOnWindow = windowRect.Contains(mouse);
            if (mouseDownOnWindow && anyHeld && windowRect.Contains(mouse)) Input.ResetInputAxes();
            else if (!anyHeld) mouseDownOnWindow = false;
        }

        private void DrawWindow(int id) {
            float buttonSize = panelSkin.window.fontSize * 1.4f;
            float closeX = windowRect.width - buttonSize;
            float foldX = closeX - buttonSize;
            if (GUI.Button(new Rect(foldX, 0f, buttonSize, buttonSize), windowMinimized ? "+" : "-", panelSkin.button))
                windowMinimized = !windowMinimized;
            if (GUI.Button(new Rect(closeX, 0f, buttonSize, buttonSize), "×", panelSkin.button)) showWindow = false;
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, foldX), 20f));
            if (windowMinimized) return;
            GUILayout.Space(18f);
            GUILayout.Label("一般自由 H · 2F2M");
            GUILayout.BeginHorizontal();
            GUI.enabled = !session.IsSwitching;
            if (GUILayout.Toggle(session.ActiveTeam == 0, "队伍 1", GUI.skin.button)) session.SwitchTo(0);
            if (GUILayout.Toggle(session.ActiveTeam == 1, "队伍 2", GUI.skin.button)) session.SwitchTo(1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (GUILayout.Button("镜头对准当前队伍")) session.FocusCameraOnActiveTeam();

            GUILayout.Label(session.Status);
            GUILayout.Space(4f);
            GUILayout.Label("视线与头部跟随相机");
            for (int team = 0; team < 2; team++) {
                GUILayout.BeginHorizontal();
                GUILayout.Label("女" + (team + 1), GUILayout.Width(38f));
                bool eyes = GUILayout.Toggle(session.GetFollowEyes(team, false), "视线", GUILayout.Width(72f));
                bool head = GUILayout.Toggle(session.GetFollowHead(team, false), "头部", GUILayout.Width(72f));
                session.SetFollowEyes(team, false, eyes);
                session.SetFollowHead(team, false, head);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("男" + (team + 1), GUILayout.Width(38f));
                eyes = GUILayout.Toggle(session.GetFollowEyes(team, true), "视线", GUILayout.Width(72f));
                head = GUILayout.Toggle(session.GetFollowHead(team, true), "头部", GUILayout.Width(72f));
                session.SetFollowEyes(team, true, eyes);
                session.SetFollowHead(team, true, head);
                GUILayout.EndHorizontal();
            }

            bool showBoth = GUILayout.Toggle(ShowInactiveTeam.Value, "显示非活动队");
            if (showBoth != ShowInactiveTeam.Value) {
                ShowInactiveTeam.Value = showBoth;
                session.ApplyVisibility();
            }

            PreventMiddleClickAuto.Value = GUILayout.Toggle(PreventMiddleClickAuto.Value, "防止中键误触自动模式");

            GUILayout.Label("切换键: " + SwitchTeamKey.Value);
            if (GUILayout.Button("关闭窗口 (" + ToggleWindowKey.Value + ")")) showWindow = false;
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, foldX), 20f));
        }

        internal void SceneReady(HSceneProc proc) {
            if (!Enabled.Value || proc == null || !MultiTeamSelection.HasPending) return;
            if (session != null && session.Proc == proc) return;
            DisposeSession();
            session = gameObject.AddComponent<TeamSession>();
            session.Initialize(proc, Logger);
        }

        private void DiscoverFreeHScene() {
            if (freeHMode != null) return;
            FreeHScene[] scenes = Resources.FindObjectsOfTypeAll<FreeHScene>();
            for (int i = 0; i < scenes.Length; i++) {
                FreeHScene scene = scenes[i];
                if (scene == null || !scene.gameObject.scene.IsValid() || !scene.gameObject.scene.isLoaded) continue;
                AttachFreeHScene(scene, "resource scan");
                if (freeHMode != null) return;
            }
        }

        internal void AttachFreeHScene(FreeHScene scene, string source) {
            if (!Enabled.Value || scene == null || freeHMode != null) return;
            bool ready;
            try { ready = Traverse.Create(scene).Field("isInit").GetValue<bool>(); }
            catch (Exception ex) {
                Logger.LogError("MultiTeamH found FreeHScene through " + source + " but could not read isInit: " + ex);
                return;
            }
            if (!ready) return;
            FreeHModeController existing = scene.GetComponent<FreeHModeController>();
            if (existing != null) {
                freeHMode = existing;
                return;
            }
            Logger.LogInfo("MultiTeamH found the initialized Free H selection screen through " + source + ".");
            FreeHModeController candidate = scene.gameObject.AddComponent<FreeHModeController>();
            try {
                if (!candidate.Initialize(scene, Logger)) {
                    Destroy(candidate);
                    return;
                }
                freeHMode = candidate;
            } catch (Exception ex) {
                Logger.LogError("MultiTeamH could not create the 多P Free H entry; it will retry: " + ex);
                Destroy(candidate);
            }
        }

        internal void FreeHModeEnded(FreeHModeController ended) {
            if (freeHMode == ended) freeHMode = null;
        }

        internal void SessionEnded(TeamSession ended) {
            if (session == ended) session = null;
        }

        private void DisposeSession() {
            if (session == null) return;
            TeamSession old = session;
            session = null;
            old.Shutdown();
            Destroy(old);
        }

        internal static void DebugLog(string message) {
            if (Instance != null && DebugLogging.Value) Instance.Logger.LogDebug(message);
        }
        internal static void LogInfo(string message) { if (Instance != null) Instance.Logger.LogInfo(message); }
        internal static void LogWarning(string message) { if (Instance != null) Instance.Logger.LogWarning(message); }
    }

    [HarmonyPatch]
    internal static class Hooks {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HMotionEyeNeckFemale), "Proc", new Type[] { typeof(AnimatorStateInfo), typeof(int), typeof(GameObject), typeof(bool), typeof(bool) })]
        private static void ApplyTeamFemaleLookSettings(HMotionEyeNeckFemale __instance, ref bool _isConfigEyes, ref bool _isConfigNeck) {
            bool eyes, head;
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) {
                if (sessions[i] == null || !sessions[i].TryGetLookSettings(__instance, out eyes, out head)) continue;
                _isConfigEyes = eyes;
                _isConfigNeck = head;
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "LateUpdate")]
        private static void ApplyConfiguredCameraLook(HSceneProc __instance) {
            TeamSession session = TeamSession.Find(__instance);
            if (session != null) session.ApplyCameraLookOverrides();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HMotionEyeNeck), "SetEyeNeckPtn", new Type[] { typeof(int), typeof(GameObject), typeof(bool), typeof(bool) })]
        private static bool BlockDisabledCameraLook(HMotionEyeNeck __instance, int _id, GameObject _objCamera,
            bool _isConfigEyeDisregard, bool _isConfigNeckDisregard, ref bool __result) {
            bool eyes, head;
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) {
                if (_id < 1000 && sessions[i] != null && sessions[i].RecordNativeLook(__instance, _id, _objCamera,
                    _isConfigEyeDisregard, _isConfigNeckDisregard)) return true;
                if (_id != 1000 && _id != 1001 && _id != 1002) continue;
                if (sessions[i] == null || !sessions[i].TryGetLookSettings(__instance, out eyes, out head)) continue;
                DiagnosticProbe.OnceInfo("look-controller-" + __instance.GetHashCode(), "look controller=" + __instance.GetType().Name + " id=" + _id + " eyes=" + eyes + " head=" + head);
                if (_id == 1002) {
                    MethodInfo setLook = AccessTools.Method(typeof(HMotionEyeNeck), "SetEyeNeckPtn",
                        new Type[] { typeof(int), typeof(GameObject), typeof(bool), typeof(bool) });
                    if (eyes) setLook.Invoke(__instance, new object[] { 1000, _objCamera, _isConfigEyeDisregard, _isConfigNeckDisregard });
                    if (head) setLook.Invoke(__instance, new object[] { 1001, _objCamera, _isConfigEyeDisregard, _isConfigNeckDisregard });
                    __result = true;
                    return false;
                }
                if ((_id == 1000 && !eyes) || (_id == 1001 && !head)) {
                    __result = true;
                    return false;
                }
                break;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HMotionEyeNeck), "SetEyeNeckPtn", new Type[] { typeof(int), typeof(GameObject), typeof(bool), typeof(bool) })]
        private static void KeepNativeMotionHead(HMotionEyeNeck __instance, int _id) {
            if (_id >= 1000) return;
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i] != null && sessions[i].RestoreMotionHeadIfCameraDriven(__instance)) return;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FreeHScene), "Start")]
        private static void ObserveFreeHStart(FreeHScene __instance, ref IEnumerator __result) {
            if (__result != null) __result = ObserveFreeHStartRoutine(__result, __instance);
        }

        private static IEnumerator ObserveFreeHStartRoutine(IEnumerator original, FreeHScene scene) {
            while (original.MoveNext()) yield return original.Current;
            if (MultiTeamHPlugin.Instance != null) MultiTeamHPlugin.Instance.AttachFreeHScene(scene, "FreeHScene.Start completion");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlobalMethod), "LoadCamera")]
        private static bool AllowCameraLoad(string _strfile) {
            NotifyCameraLoad("LoadCamera", _strfile);
            return !TeamSession.SuppressCameraLoads;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlobalMethod), "LoadResetCamera")]
        private static bool AllowResetCameraLoad(string _strfile) {
            NotifyCameraLoad("LoadResetCamera", _strfile);
            return !TeamSession.SuppressCameraLoads;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "ShortCut")]
        private static bool SuppressBackgroundShortcuts() { return !TeamSession.IsParallelPass; }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), "OnSpeedUpClick")]
        private static void RememberClickBeforeSpeedPanel(HSprite __instance, ref HFlag.ClickKind __state) {
            __state = __instance == null || !TeamSession.IsManagedSprite(__instance) ? HFlag.ClickKind.none :
                Traverse.Create(__instance).Field("flags").GetValue<HFlag>().click;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), "OnSpeedUpClick")]
        private static void PreventAccidentalMiddleAuto(HSprite __instance, HFlag.ClickKind __state) {
            if (!MultiTeamHPlugin.PreventMiddleClickAuto.Value || !Input.GetMouseButtonDown(2) || !TeamSession.IsManagedSprite(__instance)) return;
            HFlag flags = Traverse.Create(__instance).Field("flags").GetValue<HFlag>();
            if (flags == null || (int)flags.click != 28) return;
            flags.click = __state;
            DiagnosticProbe.Info("middle-auto-blocked restoredClick=" + __state);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), "OnSpeedUpClick")]
        private static void ProbeSpeedPanelClick(HSprite __instance) {
            TeamSession.ProbeSpeedClick(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), "OnInsideClick")]
        private static void ProbeInsideBefore(HSprite __instance) {
            TeamSession.ProbeActionButtonClick(__instance, "inside-before");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), "OnInsideClick")]
        private static void ProbeInsideAfter(HSprite __instance) {
            TeamSession.ProbeActionButtonClick(__instance, "inside-after");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSprite), "OnOutsideClick")]
        private static void ProbeOutsideBefore(HSprite __instance) {
            TeamSession.ProbeActionButtonClick(__instance, "outside-before");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), "OnOutsideClick")]
        private static void ProbeOutsideAfter(HSprite __instance) {
            TeamSession.ProbeActionButtonClick(__instance, "outside-after");
        }

        private static void NotifyCameraLoad(string method, string cameraName) {
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) if (sessions[i] != null) sessions[i].LogCameraLoad(method, cameraName);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HActionBase), "SetPlay")]
        private static bool SetPlayForSingleFemale(HActionBase __instance, object[] __args, ref bool __result) {
            TeamSession.PrepareActionSetPlay(__instance);
            string nextAnimation = (string)__args[0];
            bool kiss = nextAnimation == "K_Touch" || nextAnimation == "K_Loop";
            if (kiss && TeamSession.IsParallelPass) {
                RuntimeModCompat.MarkKissSetPlayBlocked(__instance);
                __result = false;
                DiagnosticProbe.OnceInfo("background-kiss-block-" + (__instance.female == null ? 0 : __instance.female.GetInstanceID()),
                    "blocked background kiss transition female=" + (__instance.female == null ? "null" : __instance.female.gameObject.name));
                return false;
            }
            if (kiss && RuntimeModCompat.ShouldBlockKissSetPlay(__instance)) {
                RuntimeModCompat.MarkKissSetPlayBlocked(__instance);
                __result = false;
                return false;
            }
            // Managed actions have all of the mandatory secondary compatibility
            // slots populated. Let the game's complete implementation update IK,
            // layer, parent, collision, item and hand data; the former shortened
            // replacement left special-mode rigs only partially initialized.
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HLesbian), "Proc")]
        private static void FlushLesbianPartnerFace(HLesbian __instance) {
            TeamSession.FlushLesbianVoiceAfterAction(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HandCtrl), "SetIconTexture")]
        private static bool SuppressBackgroundAibuCursor() { return !TeamSession.IsParallelPass; }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), "IsDontTouchAnalAndMassager")]
        private static void ProbeDeniedAibuTouch(HandCtrl __instance, HandCtrl.AibuColliderKind _kindTouch, int __result) {
            if (__instance == null || __result < 0 || __instance.flags == null) return;
            HFlag flags = __instance.flags;
            SaveData.Heroine actual = flags.lstHeroine == null || flags.lstHeroine.Count == 0 ? null : flags.lstHeroine[0];
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int s = 0; s < sessions.Count; s++) {
                TeamSession session = sessions[s];
                if (session == null) continue;
                for (int team = 0; team < 2; team++) {
                    if (session.GetFlagsObject(team) != flags) continue;
                    SaveData.Heroine expected = session.GetHeroineObject(team);
                    string key = "touch-denied-" + flags.GetInstanceID() + "-" + _kindTouch + "-" + __result +
                        "-" + (actual == null ? 0 : actual.GetHashCode());
                    DiagnosticProbe.OnceInfo(key, "touch-denied team=" + (team + 1) +
                        " kind=" + _kindTouch + " result=" + __result +
                        " heroineMatch=" + ReferenceEquals(actual, expected) +
                        " heroineCount=" + (flags.lstHeroine == null ? -1 : flags.lstHeroine.Count) +
                        " analExp=" + (actual == null || actual.hAreaExps == null || actual.hAreaExps.Length < 4 ? "null" : actual.hAreaExps[3].ToString("0.0")) +
                        " massageExp=" + (actual == null || actual.massageExps == null ? "null" : string.Join(",", Array.ConvertAll(actual.massageExps, x => x.ToString("0.0")))) +
                        " denial=" + (actual == null || actual.denial == null ? "null" : actual.denial.anal + "/" + actual.denial.massage));
                    return;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), "GetOnMouseAibuCollider")]
        private static void RepairManagedAibuHit(HandCtrl __instance, ref HandCtrl.AibuColliderKind __result) {
            if (__result != HandCtrl.AibuColliderKind.none || __instance == null) return;
            ChaControl female = null;
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) {
                if (sessions[i] != null && sessions[i].TryGetActiveFemaleForHand(__instance, out female)) break;
            }
            if (female == null || female.gameObject.name != "MultiTeamH_Female2") return;
            if (__instance.flags == null || __instance.flags.ctrlCamera == null) { DiagnosticProbe.AibuGate(__instance, female, "camera-or-flags-null"); return; }
            if (!__instance.flags.isAibuSelect) { DiagnosticProbe.AibuGate(__instance, female, "isAibuSelect=false"); return; }
            // The native EventSystem reports an invisible full-screen H canvas as
            // being under the pointer. The ray still has to hit a managed female
            // collider below, so that flag is not a safe rejection criterion here.
            if (__instance.nowMES == null || __instance.nowMES.isTouchAreas == null) { DiagnosticProbe.AibuGate(__instance, female, "nowMES-null"); return; }
            Ray ray = __instance.flags.ctrlCamera.thisCmaera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, float.PositiveInfinity, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
            for (int i = 0; i < hits.Length; i++) {
                Collider collider = hits[i].collider;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || !collider.transform.IsChildOf(female.transform)) continue;
                int area;
                HandCtrl.AibuColliderKind kind;
                if (!TryMapAibuTag(collider.tag, out area, out kind)) continue;
                if (area >= 0 && area < __instance.nowMES.isTouchAreas.Length && __instance.nowMES.isTouchAreas[area]) {
                    __result = kind;
                    DiagnosticProbe.AibuMiss(__instance, female, __result);
                    return;
                }
            }
            DiagnosticProbe.AibuMiss(__instance, female, __result);
        }

        private static bool TryMapAibuTag(string tag, out int area, out HandCtrl.AibuColliderKind kind) {
            area = -1;
            kind = HandCtrl.AibuColliderKind.none;
            switch (tag) {
                case "H/Aibu/Hit/mouth": area = 0; kind = HandCtrl.AibuColliderKind.mouth; return true;
                case "H/Aibu/Hit/muneL": area = 1; kind = HandCtrl.AibuColliderKind.muneL; return true;
                case "H/Aibu/Hit/muneR": area = 2; kind = HandCtrl.AibuColliderKind.muneR; return true;
                case "H/Aibu/Hit/kokan": area = 3; kind = HandCtrl.AibuColliderKind.kokan; return true;
                case "H/Aibu/Hit/anal": area = 4; kind = HandCtrl.AibuColliderKind.anal; return true;
                case "H/Aibu/Hit/siriL": area = 5; kind = HandCtrl.AibuColliderKind.siriL; return true;
                case "H/Aibu/Hit/siriR": area = 6; kind = HandCtrl.AibuColliderKind.siriR; return true;
                default: return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "MapSameObjectDisable")]
        private static void HSceneReady(HSceneProc __instance) {
            if (MultiTeamHPlugin.Instance != null) MultiTeamHPlugin.Instance.SceneReady(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void HSceneDestroyed(HSceneProc __instance) {
            TeamSession.NotifySceneDestroyed(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void HSceneAnimationChanged(HSceneProc __instance, HSceneProc.AnimationListInfo _nextAinmInfo) {
            TeamSession session = TeamSession.Find(__instance);
            if (session != null) session.AnimationChanged(_nextAinmInfo);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void HSceneAnimationChanging(HSceneProc __instance, HSceneProc.AnimationListInfo _nextAinmInfo, ref long __state) {
            __state = DiagnosticProbe.Start();
            TeamSession session = TeamSession.Find(__instance);
            DiagnosticProbe.Info("ChangeAnimator-enter activeSession=" + (session != null));
            if (session != null && _nextAinmInfo != null) {
                session.BeforeAnimationChange(_nextAinmInfo);
                TeamSession.BeginAnimatorChange(__instance, _nextAinmInfo);
                HSceneProc.AnimationListInfo previous = __instance.flags == null ? null : __instance.flags.nowAnimationInfo;
                session.PrepareAnimationContext(_nextAinmInfo);
                DiagnosticProbe.Info("ChangeAnimator-context previous=" + (previous == null ? "null" : previous.mode + "/" + previous.nameAnimation) +
                    " retained=" + ReferenceEquals(previous, __instance.flags == null ? null : __instance.flags.nowAnimationInfo) +
                    " incoming=" + _nextAinmInfo.mode + "/" + _nextAinmInfo.nameAnimation);
                // Preserve the native actor layout. Masturbation deliberately
                // has no female1 resource; filling that slot loads a controller
                // into the wrong rig and deforms the visible character.
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static Exception HSceneAnimationChangeFinished(HSceneProc __instance, HSceneProc.AnimationListInfo _nextAinmInfo, long __state, Exception __exception) {
            TeamSession.EndAnimatorChange(__instance);
            DiagnosticProbe.Info("ChangeAnimator-exit elapsedMs=" + DiagnosticProbe.ElapsedMs(__state).ToString("0.0") + " exception=" + (__exception == null ? "none" : __exception.GetType().Name));
            if (__exception != null) {
                try { DiagnosticProbe.Error("ChangeAnimator failed: " + __exception); } catch { }
                // HSceneProc retries selectAnimationListInfo every Update. A failed
                // transition otherwise becomes a permanent 1.3 second/frame loop.
                if (__instance.flags != null) __instance.flags.selectAnimationListInfo = null;
                TeamSession session = TeamSession.Find(__instance);
                if (session != null) session.AnimationChangeFailed(_nextAinmInfo, __exception);
                if (session != null) return null;
            }
            return __exception;
        }
    }

    [HarmonyPatch]
    internal static class SecondaryControllerLoadHooks {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() {
            Type[] types = { typeof(HandCtrl), typeof(YureCtrl), typeof(SiruPasteCtrl), typeof(HMotionEyeNeckFemale),
                typeof(DynamicBoneReferenceCtrl), typeof(AnimatorLayerCtrl), typeof(ParentObjectCtrl) };
            string[] names = { "LoadMotionEnableState", "LoadReactionList", "LoadAllExcel", "Load", "LoadText" };
            for (int i = 0; i < types.Length; i++) {
                System.Reflection.MethodInfo[] methods = types[i].GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                for (int j = 0; j < methods.Length; j++) {
                    for (int k = 0; k < names.Length; k++) {
                        if (methods[j].Name != names[k]) continue;
                        yield return methods[j];
                        break;
                    }
                }
            }
        }

        private static bool Prefix(object __instance, MethodBase __originalMethod, ref long __state) {
            bool allow = !TeamSession.ShouldSkipSecondaryControllerLoad(__instance, __originalMethod);
            if (allow) __state = DiagnosticProbe.Start();
            else DiagnosticProbe.OnceInfo("skip-" + __instance.GetType().FullName + "." + __originalMethod.Name,
                "controller-load skipped " + __instance.GetType().Name + "." + __originalMethod.Name);
            return allow;
        }

        private static void Postfix(object __instance, MethodBase __originalMethod, long __state) {
            if (__state == 0) return;
            double elapsed = DiagnosticProbe.ElapsedMs(__state);
            if (elapsed >= 20.0) DiagnosticProbe.Warning("controller-load slow " + __instance.GetType().Name + "." + __originalMethod.Name + " elapsedMs=" + elapsed.ToString("0.0"));
        }
    }

    [HarmonyPatch(typeof(HSceneProc), "Update")]
    internal static class HSceneUpdateProbe {
        private static void Prefix(HSceneProc __instance, ref long __state) { __state = DiagnosticProbe.Start(); }
        private static void Postfix(HSceneProc __instance, MethodBase __originalMethod, long __state) {
            double elapsed = DiagnosticProbe.ElapsedMs(__state);
            if (elapsed >= 50.0) DiagnosticProbe.Warning("HSceneProc.Update slow elapsedMs=" + elapsed.ToString("0.0") +
                " mode=" + (__instance.flags == null ? "null" : __instance.flags.mode.ToString()) +
                " animation=" + (__instance.flags == null ? "null" : __instance.flags.nowAnimStateName));
            if (!TeamSession.IsParallelPass) {
                TeamSession session = TeamSession.Find(__instance);
                if (session != null) {
                    try { session.RunParallelUpdate(__originalMethod); }
                    catch (Exception ex) { DiagnosticProbe.Error("parallel-update postfix failed: " + ex); }
                    finally { session.AfterNativeUpdate(); }
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class TeamFaceWriteHooks {
        private static IEnumerable<MethodBase> TargetMethods() {
            MethodInfo[] methods = typeof(FaceListCtrl).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) {
                if (methods[i].Name != "SetFace" || methods[i].ReturnType != typeof(bool)) continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                for (int p = 0; p < parameters.Length; p++) {
                    if (parameters[p].ParameterType != typeof(ChaControl)) continue;
                    yield return methods[i];
                    break;
                }
            }
        }

        private static bool Prefix(FaceListCtrl __instance, object[] __args, ref bool __result) {
            ChaControl female = null;
            if (__args != null) for (int a = 0; a < __args.Length; a++) {
                female = __args[a] as ChaControl;
                if (female != null) break;
            }
            IList<TeamSession> sessions = TeamSession.Snapshot;
            for (int i = 0; i < sessions.Count; i++) {
                TeamSession session = sessions[i];
                if (session == null || !session.ContainsFemale(female)) continue;
                bool allow = session.AllowFaceWrite(__instance, female);
                int faceId = __args.Length > 0 && __args[0] is int ? (int)__args[0] : -1;
                int voiceKind = __args.Length > 2 && __args[2] is int ? (int)__args[2] : -1;
                int action = __args.Length > 3 && __args[3] is int ? (int)__args[3] : -1;
                session.TraceFaceWrite(__instance, female, faceId, voiceKind, action, allow);
                if (allow) return true;
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(HSprite), "InitHeroine")]
    internal static class NativeThreePPortraitHook {
        [HarmonyPrefix]
        private static void PreserveTeamPortrait(HSprite __instance) {
            TeamSession.DetachManagedPortraitBeforeNative3P(__instance);
        }

        [HarmonyPostfix]
        private static void RestoreTeamPortrait(HSprite __instance) {
            TeamSession.RestoreManagedPortraitAfterNative3P(__instance);
        }
    }

    [HarmonyPatch]
    internal static class BackgroundSpriteBoolWriteHooks {
        private static IEnumerable<MethodBase> TargetMethods() {
            string[] names = { "SetHoushiStart", "SetHoushiActionButtonActive", "SetHoushiAutoFinish",
                "SetSonyuStart", "SetSonyuActionButtonActive", "SetCondom" };
            MethodInfo[] methods = typeof(HSprite).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) {
                if (methods[i].ReturnType != typeof(bool)) continue;
                for (int n = 0; n < names.Length; n++) if (methods[i].Name == names[n]) { yield return methods[i]; break; }
            }
        }

        private static bool Prefix(HSprite __instance, MethodBase __originalMethod, object[] __args, ref bool __result) {
            if (!TeamSession.RecordModeUiCall(__instance, __originalMethod, __args)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class BackgroundSpriteVoidWriteHooks {
        private static IEnumerable<MethodBase> TargetMethods() {
            string[] names = { "MainSpriteChange", "CreateActionList", "SetHelpSprite" };
            MethodInfo[] methods = typeof(HSprite).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) {
                if (methods[i].ReturnType != typeof(void)) continue;
                for (int n = 0; n < names.Length; n++) if (methods[i].Name == names[n]) { yield return methods[i]; break; }
            }
        }

        private static bool Prefix(HSprite __instance, MethodBase __originalMethod, object[] __args) {
            return !TeamSession.RecordModeUiCall(__instance, __originalMethod, __args);
        }
    }

    [HarmonyPatch]
    internal static class BackgroundHandUiWriteHooks {
        private static IEnumerable<MethodBase> TargetMethods() {
            string[] names = { "SetItem", "DetachItem", "DetachItemByUseItem", "DetachItemByUseAreaItem", "DetachAllItem", "ForceFinish" };
            MethodInfo[] methods = typeof(HandCtrl).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) {
                for (int n = 0; n < names.Length; n++) if (methods[i].Name == names[n]) { yield return methods[i]; break; }
            }
        }

        private static void Postfix() { TeamSession.MarkBackgroundHandUiDirty(); }
    }

    [HarmonyPatch(typeof(HSprite), "LoadMotionList")]
    internal static class SpecialActionLabelHooks {
        private static void Postfix(HSprite __instance) {
            if (TeamSession.IsManagedSprite(__instance)) TeamSession.DecorateSpecialActionLabels(__instance);
        }
    }

    [HarmonyPatch(typeof(HSprite), "CreateMotionList")]
    internal static class SpecialActionMenuBuildHooks {
        private static void Prefix(HSprite __instance, ref HFlag.EMode __state) {
            if (TeamSession.IsManagedSprite(__instance)) DiagnosticProbe.Info("CreateMotionList-enter");
            if (!TeamSession.BeginSpecialMenuBuild(__instance, out __state)) __state = (HFlag.EMode)(-1);
        }

        private static void Postfix(HSprite __instance, HFlag.EMode __state) {
            if ((int)__state >= 0) TeamSession.EndSpecialMenuBuild(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(HSprite), "OnActionMenu")]
    internal static class SpecialActionCategoryClickHooks {
        private static bool Prefix(HSprite __instance, int _kind) {
            if (TeamSession.IsManagedSprite(__instance)) DiagnosticProbe.Info("OnActionMenu kind=" + _kind);
            // The action-category icons are EventTrigger callbacks. Vanilla's
            // OnActionMenu rejects lesbian/masturbation before CreateMotionList.
            return !TeamSession.HandleSpecialActionMenu(__instance, _kind);
        }
    }

    [HarmonyPatch(typeof(HSprite), "OnChangePlaySelect")]
    internal static class SpecialActionMotionClickHooks {
        private static bool Prefix(HSprite __instance, GameObject objClick) {
            if (TeamSession.IsManagedSprite(__instance)) DiagnosticProbe.Info("OnChangePlaySelect object=" + (objClick == null ? "null" : objClick.name));
            return !TeamSession.HandleSpecialMotionSelection(__instance, objClick);
        }
    }
}
