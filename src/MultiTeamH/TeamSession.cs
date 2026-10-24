using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Logging;
using HarmonyLib;
using Manager;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace MultiTeamH {
    [DefaultExecutionOrder(10000)]
    internal sealed class TeamSession : MonoBehaviour {
        private static readonly List<TeamSession> Sessions = new List<TeamSession>();
        internal static bool SuppressCameraLoads { get; private set; }
        internal static bool IsParallelPass { get; private set; }
        internal static bool BackgroundHandUiDirty { get; private set; }
        internal static bool ReplayingModeUi { get; private set; }
        internal static HSceneProc ChangingAnimator { get; private set; }
        private static HSceneProc.AnimationListInfo incomingAnimation;
        private static readonly Dictionary<string, int> animatorControllerLoadCounts = new Dictionary<string, int>();
        internal static IList<TeamSession> Snapshot { get { return Sessions; } }
        private readonly TeamState[] teams = new TeamState[2];
        private readonly Lookat_dan[] teamLookDans = new Lookat_dan[2];
        private readonly CameraSnapshot[] teamCameras = new CameraSnapshot[2];
        private readonly CameraSnapshot[] preKissCameras = new CameraSnapshot[2];
        private readonly CameraSnapshot[] settledKissCameras = new CameraSnapshot[2];
        private readonly int[] kissInactiveFrames = new int[2];
        private readonly bool[] kissCameraMovedByUser = new bool[2];
        private readonly int[] kissCameraSettlingUntilFrame = new int[2];
        private readonly object[] lastLesbianVoiceInfo = new object[2];
        private readonly int[] lastLesbianVoiceFace = { -1, -1 };
        private readonly HashSet<HSceneProc.AnimationListInfo> lesbianAibuEntries = new HashSet<HSceneProc.AnimationListInfo>();
        private readonly HashSet<HSceneProc.AnimationListInfo> masturbationAibuEntries = new HashSet<HSceneProc.AnimationListInfo>();
        private readonly HashSet<HSceneProc.AnimationListInfo> threePEntries = new HashSet<HSceneProc.AnimationListInfo>();
        private readonly HashSet<int> specialMotionBindings = new HashSet<int>();
        private readonly HVoiceCtrl.Voice[] voiceByTeam = new HVoiceCtrl.Voice[2];
        private static readonly FieldInfo[] VoiceCharacterArrayFields = {
            AccessTools.Field(typeof(HVoiceCtrl), "dicBreathVoiceIntos"),
            AccessTools.Field(typeof(HVoiceCtrl), "dicVoiceIntos"),
            AccessTools.Field(typeof(HVoiceCtrl), "dicShortBreathIntos"),
            AccessTools.Field(typeof(HVoiceCtrl), "dicBreathPtn"),
            AccessTools.Field(typeof(HVoiceCtrl), "dicVoicePtn"),
            AccessTools.Field(typeof(HVoiceCtrl), "linkUseBreathPtn"),
            AccessTools.Field(typeof(HVoiceCtrl), "linkUseVoicePtn"),
            AccessTools.Field(typeof(HVoiceCtrl), "blendEyes"),
            AccessTools.Field(typeof(HVoiceCtrl), "blendMouths")
        };
        private int voiceCataloguePrimaryTeam;
        private readonly int[] lastFlushedVoiceRequests = { -1, -1 };
        private ManualLogSource log;
        private bool initialized;
        private bool shuttingDown;
        private bool initializationCompleted;
        private int launchRevision;
        private ChaControl createdFemale;
        private ChaControl createdMale;
        private Button avatarButton;
        private bool addedAvatarButton;
        private GameObject parallelFlagObject;
        private float lastProbeUpdate;
        private float lastProbeHeartbeat;
        private float lastParallelLog;
        private float lastParallelCameraRestoreLog;
        private CameraSnapshot postSwitchCameraProbe;
        private int postSwitchCameraProbeTeam = -1;
        private int postSwitchCameraProbeUntilFrame;
        private int postSwitchCameraProbeLastFrame = -1;
        private int postSwitchCameraRepairCount;
        private int speedClickProbeUntilFrame = -1;
        private float lastExternalUiWriteLog;
        private int lastCameraBaseRepairLogFrame = -1000;
        private int lesbianOwner = -1;
        private List<ChaControl> lesbianFemales;
        private List<SaveData.Heroine> lesbianHeroines;
        private List<MotionIK> lesbianMotionIKs;
        private InactiveHeroinePanel inactiveHeroinePanel;
        private bool hasLesbianPartnerPose;
        private HSceneProc.AnimationListInfo lesbianPartnerRigAnimation;
        private Vector3 lesbianPartnerFemalePosition;
        private Quaternion lesbianPartnerFemaleRotation;
        private Vector3 lesbianPartnerMalePosition;
        private Quaternion lesbianPartnerMaleRotation;
        private int lastSpecialTransitionFrame = -1;
        private HSceneProc.AnimationListInfo lastSpecialTransition;
        private bool specialTransitionRunning;
        private readonly bool[] specialRigActive = new bool[2];
        private readonly string[] lastFaceWriteProbe = new string[2];

        internal HSceneProc Proc { get; private set; }
        internal int ActiveTeam { get; private set; }
        internal bool IsSwitching { get; private set; }
        internal string Status { get; private set; }
        private readonly bool[] followEyes = { true, false, true, false };
        private readonly bool[] followHead = { false, false, false, false };
        private readonly bool[] kissHeadOverride = { false, false };
        private readonly Quaternion[] lastMaleGenitalRotation = new Quaternion[2];
        private readonly Transform[] maleGenitalBones = new Transform[2];
        private readonly int[] lastMaleGenitalFrame = { -1, -1 };
        private readonly int[] lastMaleAnimationHash = new int[2];
        private readonly float[] lastMaleGenitalProbe = new float[2];

        private static bool IsThreePMode(HFlag.EMode mode) {
            return mode == HFlag.EMode.houshi3P || mode == HFlag.EMode.sonyu3P;
        }

        private static bool IsTwoFemaleMode(HFlag.EMode mode) {
            return mode == HFlag.EMode.lesbian || IsThreePMode(mode);
        }

        private static bool IsAppendedActionMode(HFlag.EMode mode) {
            return IsTwoFemaleMode(mode) || mode == HFlag.EMode.masturbation;
        }

        internal static TeamSession Find(HSceneProc proc) {
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session != null && session.initialized && session.Proc == proc) return session;
            }
            return null;
        }

        internal static void BeginAnimatorChange(HSceneProc proc, HSceneProc.AnimationListInfo animation) {
            ChangingAnimator = proc;
            incomingAnimation = animation;
            animatorControllerLoadCounts.Clear();
        }
        internal static void EndAnimatorChange(HSceneProc proc) {
            if (ChangingAnimator != proc) return;
            ChangingAnimator = null;
            incomingAnimation = null;
            animatorControllerLoadCounts.Clear();
        }
        internal static void PrepareActionSetPlay(HActionBase action) {
            if (ChangingAnimator == null || incomingAnimation == null || action == null || action.flags != ChangingAnimator.flags) return;
            // ChangeAnimator reads nowAnimationInfo.mode at entry to clean up
            // the previous rig. Only expose the incoming info after that read,
            // when MotionChange invokes SetPlay for the new rig.
            action.flags.nowAnimationInfo = incomingAnimation;
            DiagnosticProbe.OnceInfo("incoming-setplay-" + incomingAnimation.mode,
                "incoming-action-setplay mode=" + incomingAnimation.mode + " name=" + incomingAnimation.nameAnimation);
        }
        internal static void MarkBackgroundHandUiDirty() { if (IsParallelPass) BackgroundHandUiDirty = true; }

        internal static bool RecordModeUiCall(HSprite sprite, MethodBase method, object[] args) {
            if (ReplayingModeUi || sprite == null || method == null) return false;
            for (int s = 0; s < Sessions.Count; s++) {
                TeamSession session = Sessions[s];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                int teamIndex = session.GetBoundTeamIndex();
                if (teamIndex < 0 || session.teams[teamIndex] == null) return false;
                session.teams[teamIndex].RecordModeUiCall(method, args);
                // The mode actions also read and write HSprite fields directly.
                // Suppressing only these public methods leaves those fields on
                // the focused team's values and produces an impossible mixed UI.
                // Run every native write inside the per-team UI snapshot instead.
                return false;
            }
            return false;
        }

        internal static bool ShouldSkipSecondaryControllerLoad(object controller, MethodBase method) {
            HSceneProc proc = ChangingAnimator;
            if (proc == null || controller == null || Find(proc) == null) return false;
            // In native lesbian mode female1 is a real actor. Her controller
            // data must be loaded; suppressing it leaves the partner on stale
            // face/neck/IK data from her former 1F1M action.
            // ChangeAnimator assigns flags.mode to the incoming mode before
            // any of these loads, while nowAnimationInfo still points at the
            // outgoing animation until the end of the method.
            if (proc.flags != null && IsTwoFemaleMode(proc.flags.mode)) return false;
            bool secondary = ReferenceEquals(controller, proc.hand1) || ReferenceEquals(controller, proc.yure1) ||
                ReferenceEquals(controller, proc.siru1) || ReferenceEquals(controller, proc.eyeneckFemale1) ||
                ReferenceEquals(controller, proc.dynamicCtrl1) || ReferenceEquals(controller, proc.alCtrl1) ||
                ReferenceEquals(controller, proc.parentObjectFemale1) ||
                ReferenceEquals(controller, proc.parentObjectBaseFemale1);
            bool primary = ReferenceEquals(controller, proc.hand) || ReferenceEquals(controller, proc.yure) ||
                ReferenceEquals(controller, proc.siru) || ReferenceEquals(controller, proc.eyeneckFemale) ||
                ReferenceEquals(controller, proc.dynamicCtrl) || ReferenceEquals(controller, proc.alCtrl) ||
                ReferenceEquals(controller, proc.parentObjectFemale) ||
                ReferenceEquals(controller, proc.parentObjectBaseFemale);
            if (!secondary) return false;
            if (!primary) return true;

            // Vanilla calls the female1 load path even when a mode has only
            // one woman. MultiTeamH supplies the primary controller in those
            // mandatory slots to avoid null dereferences, so the second call
            // would otherwise overwrite the just-loaded primary data with the
            // empty paramFemale1 block (notably AnimatorLayerCtrl in
            // masturbation). Allow the first primary call and suppress the
            // duplicate for the same controller/method during this transition.
            string key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(controller) + ":" +
                (method == null ? "unknown" : method.Module.ModuleVersionId + ":" + method.MetadataToken);
            int count;
            animatorControllerLoadCounts.TryGetValue(key, out count);
            animatorControllerLoadCounts[key] = count + 1;
            return count > 0;
        }

        internal static bool IsManagedSprite(HSprite sprite) {
            if (sprite == null) return false;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session != null && session.initialized && session.Proc != null && ReferenceEquals(session.Proc.sprite, sprite)) return true;
            }
            return false;
        }

        internal static bool BeginSpecialMenuBuild(HSprite sprite, out HFlag.EMode previousMode) {
            previousMode = HFlag.EMode.aibu;
            if (sprite == null) return false;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite || session.teams[session.ActiveTeam] == null) continue;
                TeamState team = session.teams[session.ActiveTeam];
                if (!IsAppendedActionMode(team.Flags.mode)) return false;
                previousMode = team.Flags.mode;
                // CreateMotionList branches on the current mode instead of the
                // category the user clicked. Temporarily present the appended
                // special action as Aibu so the ordinary Aibu/Houshi/Sonyu
                // category buttons rebuild their native lists normally.
                team.Flags.mode = HFlag.EMode.aibu;
                DiagnosticProbe.Info("special-menu build-as-aibu previous=" + previousMode + " team=" + (session.ActiveTeam + 1));
                return true;
            }
            return false;
        }

        internal static void EndSpecialMenuBuild(HSprite sprite, HFlag.EMode previousMode) {
            if (sprite == null) return;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite || session.teams[session.ActiveTeam] == null) continue;
                session.teams[session.ActiveTeam].Flags.mode = previousMode;
                return;
            }
        }

        internal void Initialize(HSceneProc proc, ManualLogSource logger) {
            Proc = proc;
            log = logger;
            Status = "正在创建独立的第二队…";
            Sessions.Add(this);
            launchRevision = MultiTeamSelection.Revision;
            StartCoroutine(CoroutineGuard.Run(InitializeAfterFrame(), delegate(Exception ex) {
                Status = "创建第二队失败，请查看日志";
                log.LogError("MultiTeamH initialization failed: " + ex);
            }, delegate {
                if (initializationCompleted || shuttingDown) return;
                Shutdown(false);
                Destroy(this);
            }));
        }

        private IEnumerator InitializeAfterFrame() {
            long initializationStarted = DiagnosticProbe.Start();
            if (!IsSupportedScene()) {
                Status = "仅支持一般自由 H（1F1M）";
                MultiTeamSelection.Complete(launchRevision);
                yield break;
            }

            SaveData.Heroine secondFemaleData = MultiTeamSelection.SecondaryFemale;
            SaveData.Player secondMaleData = MultiTeamSelection.SecondaryMale;
            int secondStage = MultiTeamSelection.SecondaryStage;
            int secondStatus = MultiTeamSelection.SecondaryStatus;
            if (secondFemaleData == null || secondMaleData == null) {
                Status = "多P选角数据已丢失";
                yield break;
            }

            teams[0] = new TeamState(Proc.lstFemale[0], Proc.male, Proc.flags.lstHeroine[0], Proc.flags.player, Proc.lstMotionIK, Proc.flags, Proc.lstProc, false,
                TeamControllers.CapturePrimary(Proc));
            teamLookDans[0] = Proc.lookDan;
            GameObject parent = Manager.Scene.GetRootGameObjects("H")[0];
            ChaFileControl femaleFile = new ChaFileControl();
            ChaFileControl maleFile = new ChaFileControl();
            ChaFile.CopyChaFile(femaleFile, secondFemaleData.chaCtrl ? secondFemaleData.chaCtrl.chaFile : secondFemaleData.charFile);
            ChaFile.CopyChaFile(maleFile, secondMaleData.chaCtrl ? secondMaleData.chaCtrl.chaFile : secondMaleData.charFile);
            Manager.Character manager = Singleton<Manager.Character>.Instance;
            long characterLoadStarted = DiagnosticProbe.Start();
            ChaControl female = createdFemale = manager.CreateFemale(parent, 0, femaleFile, true);
            // BetterPenetration indexes its DanAgents by ChaInfo.chaID.
            // Reusing the first man's ID makes team 2's Lookat_dan drive the
            // first man's agent, even though the native controllers differ.
            ChaControl male = createdMale = manager.CreateMale(0, parent, 1, maleFile, true);
            if (female == null || male == null) {
                Status = "创建第二队失败，请查看日志";
                log.LogError("MultiTeamH could not create the independent second pair.");
                yield break;
            }
            female.gameObject.name = "MultiTeamH_Female2";
            male.gameObject.name = "MultiTeamH_Male2";
            DiagnosticProbe.Info("male-identity primary=" + teams[0].Male.chaID +
                " secondary=" + male.chaID);
            manager.loading.Load(0, female, false);
            manager.loading.Load(0, male, false);
            // Poll inside the guarded iterator so loading exceptions are caught
            // by the same rollback path instead of escaping a WaitUntil callback.
            while (!manager.loading.IsEnd(female) || !manager.loading.IsEnd(male)) yield return null;
            DiagnosticProbe.Info("initial-load charactersMs=" + DiagnosticProbe.ElapsedMs(characterLoadStarted).ToString("0.0"));
            secondFemaleData.SetRoot(female.gameObject);
            secondMaleData.SetRoot(male.gameObject);

            List<MotionIK> secondIK = new List<MotionIK> { new MotionIK(female, null), new MotionIK(male, null) };
            for (int i = 0; i < secondIK.Count; i++) secondIK[i].SetPartners(secondIK);
            TeamControllers secondaryControllers = TeamControllers.CaptureSecondary(Proc);
            if (!secondaryControllers.IsUsable) {
                Status = "游戏未创建第二套 H 控制器";
                log.LogError("MultiTeamH could not find the native secondary H controller set.");
                yield break;
            }
            teams[1] = new TeamState(female, male, secondFemaleData, secondMaleData, secondIK, null, null, true, secondaryControllers);
            Vector3 offset = Vector3.right * MultiTeamHPlugin.InitialSpacing.Value;
            female.transform.SetPositionAndRotation(teams[0].Female.transform.position + offset, teams[0].Female.transform.rotation);
            male.transform.SetPositionAndRotation(teams[0].Male.transform.position + offset, teams[0].Male.transform.rotation);
            teams[1].Offset = offset;
            teams[0].CaptureCenter();
            teams[1].CaptureCenter();

            ActiveTeam = 0;
            initialized = true;
            teamCameras[0] = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
            RuntimeModCompat.SessionStarted();
            long controllerStarted = DiagnosticProbe.Start();
            CreateParallelState(teams[1]);
            InitializeSecondaryControllers();
            ApplySecondaryFreeHSettings(secondFemaleData, secondStage, secondStatus);
            HSceneOptionsCompat.Bind(Proc, teams[0], 0);
            teams[0].CaptureHandUi(Proc.sprite);
            PrimeSecondTeam();
            DiagnosticProbe.Info("initial-load controllersMs=" + DiagnosticProbe.ElapsedMs(controllerStarted).ToString("0.0"));
            AppendSpecialAibuEntries();
            DiagnosticProbe.Info("inactive-panel create-enter");
            inactiveHeroinePanel = InactiveHeroinePanel.Create(Proc.sprite, teams[ActiveTeam], teams[1 - ActiveTeam], log);
            DiagnosticProbe.Info("inactive-panel create-exit success=" + (inactiveHeroinePanel != null));
            teams[1].EnsureAibuHitObjects(log);
            BindAvatarSwitch();
            ApplyVisibility();
            UpdateStatus();
            initializationCompleted = true;
            MultiTeamSelection.Complete(launchRevision);
            log.LogInfo("MultiTeamH initialized two independent 1F1M teams.");
            DiagnosticProbe.Info("initial-load totalMs=" + DiagnosticProbe.ElapsedMs(initializationStarted).ToString("0.0"));
            LogRuntimeState("initialized");
            StartCoroutine(LogDelayedInitializationState());
        }

        private IEnumerator LogDelayedInitializationState() {
            yield return new WaitForSeconds(0.5f);
            RefreshInteractionColliders();
            LogRuntimeState("initialized+0.5s");
            yield return new WaitForSeconds(1.5f);
            RefreshInteractionColliders();
            LogRuntimeState("initialized+2.0s");
        }

        private void RefreshInteractionColliders() {
            for (int i = 0; i < teams.Length; i++) if (teams[i] != null) teams[i].RefreshAibuColliders();
            if (initialized && teams[ActiveTeam] != null) SetActiveInteractionTeam(teams[ActiveTeam]);
        }

        private void AppendSpecialAibuEntries() {
            if (Proc == null || Proc.lstUseAnimInfo == null) return;
            List<HSceneProc.AnimationListInfo> aibu = Proc.lstUseAnimInfo[(int)HFlag.EMode.aibu];
            if (aibu == null) return;
            List<HSceneProc.AnimationListInfo> houshi = Proc.lstUseAnimInfo[(int)HFlag.EMode.houshi];
            List<HSceneProc.AnimationListInfo> sonyu = Proc.lstUseAnimInfo[(int)HFlag.EMode.sonyu];
            int migratedThreeP = 0;
            // A previous scene (or another action injector) may already have
            // placed native 3P entries in Aibu. Move those entries to their
            // actual category before the sprite builds its action rows.
            for (int i = aibu.Count - 1; i >= 0; i--) {
                HSceneProc.AnimationListInfo info = aibu[i];
                if (info == null || !IsThreePMode(info.mode)) continue;
                List<HSceneProc.AnimationListInfo> destination = info.mode == HFlag.EMode.houshi3P ? houshi : sonyu;
                if (destination != null && !destination.Contains(info)) destination.Add(info);
                List<HSceneProc.AnimationListInfo> nativeThreeP = Proc.lstUseAnimInfo[(int)info.mode];
                if (nativeThreeP != null && !nativeThreeP.Contains(info)) nativeThreeP.Add(info);
                aibu.RemoveAt(i);
                migratedThreeP++;
            }
            int nativeAibuCount = aibu.Count;
            // Undo labels left on the process-wide animation objects by an
            // older MultiTeamH scene before classifying them again.
            for (int i = 0; i < nativeAibuCount; i++) StripSpecialActionPrefix(aibu[i]);
            int lesbianAdded = AppendModeEntries(aibu, HFlag.EMode.lesbian, "[百合] ");
            int masturbationAdded = AppendModeEntries(aibu, HFlag.EMode.masturbation, "[自慰] ");
            int importedThreeP = MergeAnimationLoaderThreePEntries();
            int threePHoushiAdded = houshi == null ? 0 : AppendModeEntries(houshi, HFlag.EMode.houshi3P, "[3P] ");
            int threePSonyuAdded = sonyu == null ? 0 : AppendModeEntries(sonyu, HFlag.EMode.sonyu3P, "[3P] ");
            if (houshi != null && Proc.sprite != null) SyncThreePHoushiSubmenus(houshi);
            // kPlug can place two-female actions directly in the Aibu bucket
            // with mode=aibu. AnimationListInfo constructs paramFemale1 for
            // every entry, including ordinary caress, so only a populated
            // second-female base animation identifies the actor layout.
            for (int i = 0; i < nativeAibuCount; i++) {
                HSceneProc.AnimationListInfo info = aibu[i];
                if (info == null || lesbianAibuEntries.Contains(info) || masturbationAibuEntries.Contains(info) || threePEntries.Contains(info)) continue;
                if (HasAnimationPath(info.pathFemaleBase1)) {
                    lesbianAibuEntries.Add(info);
                    PrefixSpecialActionName(info, "[百合] ");
                    DiagnosticProbe.Info("special-aibu injected-lesbian name=" + info.nameAnimation + " secondFemale=" +
                        info.pathFemaleBase1.assetpath + "/" + info.pathFemaleBase1.file);
                }
            }
            if (Proc.sprite != null) {
                Proc.sprite.lstUseAnimInfo = Proc.lstUseAnimInfo;
                Proc.sprite.CreateActionList();
                DecorateSpecialActionLabels(Proc.sprite);
            }
            DiagnosticProbe.Info("special-aibu lesbian=" + lesbianAdded + " masturbation=" + masturbationAdded +
                " threePHoushi=" + threePHoushiAdded + " threePSonyu=" + threePSonyuAdded +
                " importedThreeP=" + importedThreeP +
                " migratedThreeP=" + migratedThreeP +
                " totalAibu=" + aibu.Count + " totalHoushi=" + (houshi == null ? 0 : houshi.Count) +
                " totalSonyu=" + (sonyu == null ? 0 : sonyu.Count));
        }

        private static bool HasAnimationPath(HSceneProc.PathName path) {
            return path != null && (!string.IsNullOrEmpty(path.assetpath) || !string.IsNullOrEmpty(path.file));
        }

        private static void StripSpecialActionPrefix(HSceneProc.AnimationListInfo info) {
            if (info == null || string.IsNullOrEmpty(info.nameAnimation)) return;
            if (info.nameAnimation.StartsWith("[百合] ", StringComparison.Ordinal) || info.nameAnimation.StartsWith("[自慰] ", StringComparison.Ordinal) ||
                info.nameAnimation.StartsWith("[3P] ", StringComparison.Ordinal))
                info.nameAnimation = info.nameAnimation.Substring(5);
        }

        private int MergeAnimationLoaderThreePEntries() {
            // Animation Loader adds its imports to the full catalogue after
            // CreateAllAnimationList. General Free H keeps a separate usable
            // list, which can already contain native 3P actions while still
            // omitting every imported 3P action. Its mapping contains exactly
            // the imported catalogue entries, so merge those by identity.
            Type loader = AccessTools.TypeByName("AnimationLoader.SwapAnim");
            FieldInfo field = loader == null ? null : AccessTools.Field(loader, "swapAnimationMapping");
            IDictionary mapping = field == null ? null : field.GetValue(null) as IDictionary;
            if (mapping == null) {
                DiagnosticProbe.Info("three-p-animation-loader mapping=unavailable");
                return 0;
            }
            int added = 0;
            int available = 0;
            foreach (DictionaryEntry entry in mapping) {
                HSceneProc.AnimationListInfo info = entry.Key as HSceneProc.AnimationListInfo;
                if (info == null || !IsThreePMode(info.mode)) continue;
                available++;
                if (entry.Value != null) {
                    FieldInfo nameField = AccessTools.Field(entry.Value.GetType(), "AnimationName");
                    string importedName = nameField == null ? null : nameField.GetValue(entry.Value) as string;
                    if (!string.IsNullOrEmpty(importedName)) info.nameAnimation = importedName;
                }
                int index = (int)info.mode;
                List<HSceneProc.AnimationListInfo> source = Proc.lstUseAnimInfo[index];
                if (source == null || source.Contains(info)) continue;
                source.Add(info);
                added++;
                DiagnosticProbe.Info("three-p-animation-loader-added mode=" + info.mode + " name=" + info.nameAnimation);
            }
            DiagnosticProbe.Info("three-p-animation-loader available=" + available + " added=" + added);
            return added;
        }

        private void SyncThreePHoushiSubmenus(List<HSceneProc.AnimationListInfo> houshi) {
            // HSprite.Init copied the Houshi list into three private submenus
            // before this session appended special actions. CreateActionList
            // does not rebuild that cache.
            List<HSceneProc.AnimationListInfo>[] submenus =
                Traverse.Create(Proc.sprite).Field("lstHoushiAnimInfo").GetValue<List<HSceneProc.AnimationListInfo>[]>();
            if (submenus == null || submenus.Length < 3) {
                DiagnosticProbe.Warning("three-p-houshi-submenus unavailable");
                return;
            }
            int[] added = new int[3];
            for (int i = 0; i < houshi.Count; i++) {
                HSceneProc.AnimationListInfo info = houshi[i];
                if (info == null || info.mode != HFlag.EMode.houshi3P) continue;
                int kind = ClassifyThreePHoushi(info);
                info.kindHoushi = kind;
                if (submenus[kind] == null) submenus[kind] = new List<HSceneProc.AnimationListInfo>();
                if (submenus[kind].Contains(info)) continue;
                submenus[kind].Add(info);
                added[kind]++;
            }
            DiagnosticProbe.Info("three-p-houshi-submenus hand=" + added[0] + " mouth=" + added[1] + " breast=" + added[2]);
        }

        private static int ClassifyThreePHoushi(HSceneProc.AnimationListInfo info) {
            string name = info.nameAnimation ?? string.Empty;
            if (name.IndexOf("口", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("舌", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("舔", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("fellatio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("blow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("suck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("oral", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (name.IndexOf("胸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("乳交", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("breast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("titjob", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            return info.kindHoushi >= 0 && info.kindHoushi <= 2 ? info.kindHoushi : 0;
        }

        private int AppendModeEntries(List<HSceneProc.AnimationListInfo> destination, HFlag.EMode mode, string prefix) {
            int index = (int)mode;
            if (index < 0 || index >= Proc.lstUseAnimInfo.Length || Proc.lstUseAnimInfo[index] == null) return 0;
            List<HSceneProc.AnimationListInfo> source = Proc.lstUseAnimInfo[index];
            if (IsThreePMode(mode) && source.Count == 0) {
                // General Free H filters its usable list by the selected
                // scene category. The native 2F1M catalogue can therefore be
                // present in lstAnimInfo while absent from lstUseAnimInfo.
                // Keep the native 3P bucket populated as well: CreateMotionList
                // reads it after the first 3P action changes flags.mode.
                List<HSceneProc.AnimationListInfo>[] catalogue =
                    Traverse.Create(Proc).Field("lstAnimInfo").GetValue<List<HSceneProc.AnimationListInfo>[]>();
                if (catalogue != null && index < catalogue.Length && catalogue[index] != null) {
                    for (int i = 0; i < catalogue[index].Count; i++) {
                        HSceneProc.AnimationListInfo info = catalogue[index][i];
                        if (info != null && !source.Contains(info)) source.Add(info);
                    }
                    DiagnosticProbe.Info("three-p-catalogue-fallback mode=" + mode + " entries=" + source.Count);
                }
            }
            int added = 0;
            for (int i = 0; i < source.Count; i++) {
                HSceneProc.AnimationListInfo info = source[i];
                if (info == null) continue;
                if (mode == HFlag.EMode.lesbian) lesbianAibuEntries.Add(info);
                else if (mode == HFlag.EMode.masturbation) masturbationAibuEntries.Add(info);
                else if (IsThreePMode(mode)) threePEntries.Add(info);
                PrefixSpecialActionName(info, prefix);
                DiagnosticProbe.Info("special-aibu source mode=" + mode + " name=" + info.nameAnimation);
                if (destination.Contains(info)) continue;
                destination.Add(info);
                added++;
            }
            // kPlug can inject special-mode entries directly into a destination
            // list instead of the native source list.
            for (int i = 0; i < destination.Count; i++) {
                HSceneProc.AnimationListInfo info = destination[i];
                if (info != null && info.mode == mode) PrefixSpecialActionName(info, prefix);
            }
            return added;
        }

        private static void PrefixSpecialActionName(HSceneProc.AnimationListInfo info, string prefix) {
            if (info == null || string.IsNullOrEmpty(info.nameAnimation)) return;
            string source = info.nameAnimation;
            if (source.StartsWith("[百合] ", StringComparison.Ordinal) || source.StartsWith("[自慰] ", StringComparison.Ordinal) ||
                source.StartsWith("[3P] ", StringComparison.Ordinal)) return;
            info.nameAnimation = prefix + SpecialActionLocalizer.Localize(source);
        }

        private bool IsSupportedScene() {
            return Proc != null && Proc.flags != null && Proc.flags.isFreeH && Proc.lstFemale != null &&
                   Proc.lstFemale.Count == 1 && Proc.lstFemale[0] != null && Proc.male != null &&
                   Proc.flags.lstHeroine != null && Proc.flags.lstHeroine.Count == 1;
        }

        private void PrimeSecondTeam() {
            TeamState first = teams[0];
            TeamState second = teams[1];
            HSceneProc.AnimationListInfo animation = first.Animation;
            if (animation == null) return;
            HSceneProc.AnimationListInfo previous = first.Animation;
            BindTeam(second);
            ActiveTeam = 1;
            second.Flags.nowAnimationInfo = previous;
            second.Flags.selectAnimationListInfo = previous;
            EnsureBaseAnimations(second, animation);
            // Use the complete native transition here.  The shortened 0.11.1
            // path skipped male animator/controller setup and left male 2 in a
            // T-pose with the pair facing the wrong direction.
            SuppressCameraLoads = true;
            try {
                Proc.ChangeAnimator(animation, true);
            } finally {
                SuppressCameraLoads = false;
            }
            // This was a direct priming call rather than a selection queued by
            // HSceneProc.Update. Leaving it populated makes the first frame after
            // switching to team 2 run ChangeAnimator a second time.
            second.Flags.selectAnimationListInfo = null;
            second.Capture(Proc.flags);
            second.RestoreCenter();
            teamCameras[1] = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
            BindTeam(first);
            ActiveTeam = 0;
            first.RestoreCenter();
            if (teamCameras[0] != null) teamCameras[0].Restore(first.Female == null || first.Female.objTop == null ? null : first.Female.objTop.transform);
            first.SetAnimatorSpeed(1f);
            RestoreCameraForTeam(first);
            for (int i = 0; i < teams.Length; i++) if (teams[i] != null) teams[i].SetAnimatorSpeed(1f);
        }

        private void RestoreCameraForTeam(TeamState team) {
            if (team == null || team.Animation == null || team.Flags == null || team.Flags.ctrlCamera == null) return;
            string cameraName = team.Animation.paramFemale == null ? null : team.Animation.paramFemale.nameCamera;
            if (team.Flags.mode == HFlag.EMode.houshi && !string.IsNullOrEmpty(team.Animation.nameCameraIdle)) cameraName = team.Animation.nameCameraIdle;
            if (string.IsNullOrEmpty(cameraName)) return;
            if (Manager.Config.EtcData.HInitCamera) GlobalMethod.LoadCamera(team.Flags.ctrlCamera, "h/list/", cameraName, false);
            else GlobalMethod.LoadResetCamera(team.Flags.ctrlCamera, "h/list/", cameraName, false);
        }

        private void InitializeSecondaryControllers() {
            TeamState first = teams[0];
            TeamState second = teams[1];
            TeamControllers c = second.Controllers;
            c.HitReaction.ik = second.Female.animBody.GetComponent<FullBodyBipedIK>();
            c.Hand.Init(second.Female, second.Male.chaFile.custom.body.skinMainColor, 0);
            c.Hand.hitReaction = c.HitReaction;
            c.Yure.Init(second.Female);
            c.Hand.yure = c.Yure;
            c.Siru.Init(second.Female);
            c.Face.LoadText(second.FemaleData.ChaName);
            c.Particle.Init("h/list/", second.Female.objBodyBone, second.Flags);
            c.Particle.Load(second.Male.objBodyBone, 0);
            c.Meta = new MetaballCtrl(Proc.objMetaball, second.Male.objBodyBone, second.Female);
            c.Meta.SetParticle(new[] { c.Particle });
            c.EyeFemale.Init(second.Female, second.Male, second.FemaleData);
            c.EyeFemale.SetPartner(second.Male.objBodyBone);
            c.EyeMale.Init(second.Male, second.Female);
            c.EyeMale.SetPartner(second.Female.objBodyBone);
            c.Dynamic.Init(second.Female);
            c.AnimatorLayer.Init(second.Female.animBody);
            c.ParentBaseFemale.Init("h/list/", "parent_object_base_female", second.Female.objBodyBone, second.Female, Proc.flags.hashAssetBundle, true);
            c.ParentFemale.Init("h/list/", "parent_object_female", second.Female.objBodyBone, second.Female, Proc.flags.hashAssetBundle, false);
            c.ParentBaseMale.Init("h/list/", "parent_object_base_male", second.Male.objBodyBone, second.Male, Proc.flags.hashAssetBundle, true);
            c.ParentMale.Init("h/list/", "parent_object_male", second.Male.objBodyBone, second.Male, Proc.flags.hashAssetBundle, false);
            c.Hand.InitHitReactionCollider();

            Proc.voice.Init(first.FemaleData.ChaName, second.FemaleData.ChaName, "h/list/");
            voiceCataloguePrimaryTeam = 0;
            if (Proc.voice.nowVoices != null && Proc.voice.nowVoices.Length >= 2) {
                voiceByTeam[0] = Proc.voice.nowVoices[0];
                voiceByTeam[1] = Proc.voice.nowVoices[1];
            }
            Proc.flags.transVoiceMouth[0] = GetMouth(first.Female);
            Proc.flags.transVoiceMouth[1] = GetMouth(second.Female);
            Proc.voice.voiceTrans[0] = Proc.flags.transVoiceMouth[0];
            Proc.voice.voiceTrans[1] = Proc.flags.transVoiceMouth[1];
            NotifyExpressionControlCharactersChanged();
        }

        private void BindVoiceSlots(TeamState primary, TeamState secondary = null) {
            if (Proc == null || Proc.voice == null || primary == null) return;
            int primaryIndex = primary == teams[1] ? 1 : 0;
            int secondaryIndex = secondary == null ? 1 - primaryIndex : (secondary == teams[1] ? 1 : 0);
            TeamState secondaryTeam = teams[secondaryIndex];
            if (secondaryTeam == null) return;
            if (voiceCataloguePrimaryTeam != primaryIndex) {
                for (int i = 0; i < VoiceCharacterArrayFields.Length; i++) {
                    FieldInfo field = VoiceCharacterArrayFields[i];
                    Array slots = field == null ? null : field.GetValue(Proc.voice) as Array;
                    if (slots == null || slots.Length < 2) continue;
                    object first = slots.GetValue(0);
                    slots.SetValue(slots.GetValue(1), 0);
                    slots.SetValue(first, 1);
                }
                voiceCataloguePrimaryTeam = primaryIndex;
                DiagnosticProbe.OnceInfo("voice-catalogue-swapped-" + primaryIndex,
                    "voice-catalogue-swapped primary=" + (primaryIndex + 1));
            }
            bool twoFemaleAction = secondary != null;
            if (Proc.voice.faceLists != null && Proc.voice.faceLists.Length >= 2) {
                Proc.voice.faceLists[0] = primary.Controllers.Face;
                // A 1F1M action has no female1. Point its compatibility slot
                // at the current woman so a voice still finishing from 3P
                // cannot animate the other team's face.
                Proc.voice.faceLists[1] = twoFemaleAction ? secondaryTeam.Controllers.Face : primary.Controllers.Face;
            }
            if (Proc.voice.nowVoices != null && Proc.voice.nowVoices.Length >= 2 && voiceByTeam[0] != null && voiceByTeam[1] != null) {
                Proc.voice.nowVoices[0] = voiceByTeam[primaryIndex];
                Proc.voice.nowVoices[1] = voiceByTeam[secondaryIndex];
            }
            Transform primaryMouth = GetMouth(primary.Female);
            Transform secondaryMouth = twoFemaleAction ? GetMouth(secondaryTeam.Female) : primaryMouth;
            if (Proc.voice.voiceTrans != null && Proc.voice.voiceTrans.Length >= 2) {
                Proc.voice.voiceTrans[0] = primaryMouth;
                Proc.voice.voiceTrans[1] = secondaryMouth;
            }
            if (primary.Flags.transVoiceMouth == null || primary.Flags.transVoiceMouth.Length < 2) primary.Flags.transVoiceMouth = new Transform[2];
            primary.Flags.transVoiceMouth[0] = primaryMouth;
            primary.Flags.transVoiceMouth[1] = secondaryMouth;
            DiagnosticProbe.OnceInfo("voice-slots-" + primaryIndex + "-" + secondaryIndex,
                "voice-slots primary=" + (primaryIndex + 1) + " secondary=" + (secondaryIndex + 1) +
                " faces=" + (Proc.voice.faceLists != null && Proc.voice.faceLists.Length >= 2) +
                " voices=" + (voiceByTeam[0] != null && voiceByTeam[1] != null));
        }

        private void CreateParallelState(TeamState team) {
            parallelFlagObject = new GameObject("MultiTeamH_Team2_HFlag");
            parallelFlagObject.hideFlags = HideFlags.HideAndDontSave;
            parallelFlagObject.transform.SetParent(transform, false);
            HFlag clone = parallelFlagObject.AddComponent<HFlag>();
            clone.enabled = false;
            CopyFlagState(teams[0].Flags, clone);
            clone.lstHeroine = new List<SaveData.Heroine> { team.FemaleData };
            clone.player = team.MaleData;
            clone.transVoiceMouth = new Transform[2];
            clone.transVoiceMouth[0] = GetMouth(team.Female);
            team.Flags = clone;
            team.Controllers.ParentBaseMale = parallelFlagObject.AddComponent<ParentObjectCtrl>();
            team.Controllers.ParentMale = parallelFlagObject.AddComponent<ParentObjectCtrl>();
            // Lookat_dan has a LateUpdate of its own. Sharing the native
            // component lets either team's ChangeAnimator replace the other's
            // genital target list and makes its bone twitch on the next frame.
            teamLookDans[1] = parallelFlagObject.AddComponent<Lookat_dan>();
            teamLookDans[1].SetMale(team.Male);
            teamLookDans[1].SetFemale(new[] { team.Female });
            team.Actions = CreateActions(team);
            team.Capture(clone);
        }

        private List<HActionBase> CreateActions(TeamState team) {
            HFlag.DeliveryMember delivery = new HFlag.DeliveryMember();
            SetField(delivery, "ctrlFlag", team.Flags);
            SetField(delivery, "chaFemale", team.Female);
            SetField(delivery, "chaFemale1", null);
            SetField(delivery, "chaMale", team.Male);
            SetField(delivery, "chaMale1", null);
            SetField(delivery, "fade", Proc.crossfade);
            SetField(delivery, "sprite", Proc.sprite);
            SetField(delivery, "lstMotionIK", team.MotionIKs);
            SetField(delivery, "hand", team.Controllers.Hand);
            // HActionBase.SetPlay invokes these secondary controller slots even
            // in normal 1F1M mode. They must be valid objects when female1 is null.
            SetField(delivery, "hand1", team.Controllers.Hand);
            SetField(delivery, "yure", team.Controllers.Yure);
            SetField(delivery, "yure1", null);
            SetField(delivery, "item", Proc.item);
            SetField(delivery, "hitcolFemale", team.Controllers.HitCollision);
            SetField(delivery, "hitcolFemale1", team.Controllers.HitCollision);
            SetField(delivery, "hitcolMale", Proc.hitcollisionMale);
            SetField(delivery, "meta", team.Controllers.Meta);
            SetField(delivery, "parentObjectFemale", team.Controllers.ParentFemale);
            SetField(delivery, "parentObjectFemale1", null);
            SetField(delivery, "parentObjectMale", team.Controllers.ParentMale);
            SetField(delivery, "voice", Proc.voice);
            SetField(delivery, "se", Proc.se);
            SetField(delivery, "particle", team.Controllers.Particle);
            SetField(delivery, "alCtrl", team.Controllers.AnimatorLayer);
            SetField(delivery, "alCtrl1", team.Controllers.AnimatorLayer);

            List<HActionBase> result = new List<HActionBase>(teams[0].Actions.Count);
            for (int i = 0; i < teams[0].Actions.Count; i++) {
                HActionBase source = teams[0].Actions[i];
                Type type = source.GetType();
                HActionBase action = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new object[] { delivery }, null) as HActionBase;
                // HSceneProc.Start performs extra initialization after constructing
                // several action types. In particular HHoushi.rely is dereferenced
                // by every MotionChange, including kPlug animation entries.
                MethodInfo setRely = AccessTools.Method(type, "SetRely");
                FieldInfo rely = AccessTools.Field(type, "rely");
                if (action != null && source != null && setRely != null && rely != null) {
                    object value = rely.GetValue(source);
                    if (value != null) setRely.Invoke(action, new[] { value });
                }
                result.Add(action);
            }
            return result;
        }

        private static void SetField(object target, string name, object value) {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) field.SetValue(target, value);
        }

        private static void CopyFlagState(HFlag source, HFlag target) {
            Type type = typeof(HFlag);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++) {
                FieldInfo field = fields[i];
                if (field.IsInitOnly) continue;
                try { field.SetValue(target, CloneFlagValue(field.GetValue(source))); }
                catch { }
            }
        }

        private static object CloneFlagValue(object value) {
            if (value == null) return null;
            Type type = value.GetType();
            if (type.IsValueType || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type)) return value;
            Array array = value as Array;
            if (array != null) {
                Array clone = (Array)array.Clone();
                for (int i = 0; i < clone.Length; i++) clone.SetValue(CloneFlagValue(clone.GetValue(i)), i);
                return clone;
            }
            if (type.DeclaringType == typeof(HFlag)) {
                object clone = FormatterServices.GetUninitializedObject(type);
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++) if (!fields[i].IsInitOnly) fields[i].SetValue(clone, CloneFlagValue(fields[i].GetValue(value)));
                return clone;
            }
            return value;
        }

        private void ApplySecondaryFreeHSettings(SaveData.Heroine heroine, int stage, int status) {
            try { Traverse.Create(Proc).Method("SetState", new object[] { heroine, stage }).GetValue(); }
            catch (Exception ex) { log.LogWarning("Could not apply team 2 H stage: " + ex.Message); }
            if (status == 0) heroine.SetMenstruationsDay(0);
            else if (status == 1) heroine.SetMenstruationsDay(4);
        }

        internal void SwitchTo(int index) {
            if (!initialized || IsSwitching || index < 0 || index >= teams.Length || index == ActiveTeam) return;
            if (preKissCameras[ActiveTeam] != null || RuntimeModCompat.IsKissBusy(teams[ActiveTeam]) ||
                Time.frameCount <= kissCameraSettlingUntilFrame[ActiveTeam]) {
                Status = "亲吻期间无法切换队伍";
                AnimatorStateInfo kissState = teams[ActiveTeam].Female.getAnimatorStateInfo(0);
                DiagnosticProbe.Info("kiss-switch-blocked team=" + (ActiveTeam + 1) +
                    " frame=" + Time.frameCount + " animator=" + kissState.shortNameHash +
                    " flagState=" + (teams[ActiveTeam].Flags == null ? "null" : teams[ActiveTeam].Flags.nowAnimStateName));
                return;
            }
            if (settledKissCameras[ActiveTeam] != null && HasManualCameraInput()) {
                settledKissCameras[ActiveTeam] = null;
                teamCameras[ActiveTeam] = CameraSnapshot.Capture(Proc.flags.ctrlCamera);
            }
            if (lesbianOwner >= 0) {
                Status = "请先结束多人动作再切换队伍";
                return;
            }
            int previous = ActiveTeam;
            StartCoroutine(CoroutineGuard.Run(SwitchRoutine(index), delegate(Exception ex) {
                log.LogError("MultiTeamH failed to switch to team " + (index + 1) + ": " + ex);
                Status = "切换失败，请查看日志";
                // Binding may fail before or after ActiveTeam is changed. Keep
                // the logical focus and the native shared context consistent.
                int failedTeam = ActiveTeam;
                ActiveTeam = previous;
                try {
                    BindTeam(teams[previous]);
                    BreastmilkCompat.FocusChanged(this, failedTeam, previous);
                    if (teamCameras[previous] != null) teamCameras[previous].Restore();
                    ApplyVisibility();
                } catch (Exception recoveryError) {
                    log.LogError("MultiTeamH could not fully restore the previous team: " + recoveryError);
                }
            }, delegate { IsSwitching = false; }));
        }

        private IEnumerator SwitchRoutine(int index) {
            IsSwitching = true;
            Status = "正在切换…";
            TeamState oldTeam = teams[ActiveTeam];
            TeamState newTeam = teams[index];
            // K_Loop can have ended while its pre-kiss camera is still waiting
            // for the 30-frame stable-exit check. Finish that pending restore
            // before saving the outgoing team's ordinary camera.
            if (preKissCameras[ActiveTeam] != null) {
                bool manualView = kissCameraMovedByUser[ActiveTeam] || HasManualCameraInput();
                CameraSnapshot viewBeforeCleanup = manualView ? CameraSnapshot.Capture(Proc.flags.ctrlCamera) : null;
                RuntimeModCompat.CompleteKiss(oldTeam);
                SetKissHeadOverride(oldTeam.Female, false);
                EndKissCamera(oldTeam.Female, !manualView);
                if (viewBeforeCleanup != null) viewBeforeCleanup.Restore();
                DiagnosticProbe.Info("kiss-switch-finalized team=" + (ActiveTeam + 1) + " manualView=" + manualView);
            }
            int oldIndex = ActiveTeam;
            CameraSnapshot outgoingCamera = settledKissCameras[oldIndex] ??
                CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
            DiagnosticProbe.Info("team-switch-outgoing-camera team=" + (oldIndex + 1) +
                " kissSnapshot=" + (settledKissCameras[oldIndex] != null) + " " + outgoingCamera.Describe() +
                " liveDrift={" + outgoingCamera.DescribeDrift() + "}");
            RuntimeModCompat.TeamSwitching(this, oldTeam, newTeam);
            // CyNoVR/native kiss cleanup can still write the shared camera
            // after the animator has returned to Idle. Preserve the actual
            // view the player had when pressing Tab.
            outgoingCamera.Restore();
            oldTeam.CaptureGuide(Proc);
            oldTeam.Capture(oldTeam.Flags);
            oldTeam.CaptureHandUi(Proc.sprite);
            oldTeam.CaptureModeUi(Proc.sprite);
            ProbeActionBubble(oldTeam.Flags.mode, "switch-saved-team-" + (oldIndex + 1));
            newTeam.Capture(newTeam.Flags);
            {
                // TeamSwitching finishes an in-progress CyNoVR kiss and restores
                // the camera from before it started. Save that stable camera,
                // rather than the unfinished kiss camera.
                teamCameras[ActiveTeam] = outgoingCamera;
                settledKissCameras[oldIndex] = null;
                BindTeam(newTeam);
                ActiveTeam = index;
                BreastmilkCompat.FocusChanged(this, 1 - index, index);
                DiagnosticProbe.Info("team-switch-target-camera team=" + (index + 1) +
                    " saved=" + (teamCameras[index] == null ? "null" : teamCameras[index].Describe()));
                if (teamCameras[index] != null)
                    teamCameras[index].Restore(newTeam.Female == null || newTeam.Female.objTop == null ? null : newTeam.Female.objTop.transform);
                else teamCameras[index] = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
                Transform switchedBase = Proc.flags == null || Proc.flags.ctrlCamera == null ? null : Proc.flags.ctrlCamera.transBase;
                DiagnosticProbe.Info("team-switch-camera-bound team=" + (index + 1) +
                    " basePos=" + (switchedBase == null ? "null" : switchedBase.position.ToString()) +
                    " actorPos=" + (newTeam.Female == null || newTeam.Female.objTop == null
                        ? "null" : newTeam.Female.objTop.transform.position.ToString()));
                newTeam.RefreshAibuColliders();
                SetActiveInteractionTeam(newTeam);
                newTeam.SetAnimatorSpeed(1f);
                ApplyVisibility();
                if (inactiveHeroinePanel != null) inactiveHeroinePanel.Refresh(teams[ActiveTeam], teams[1 - ActiveTeam]);
                for (int i = 0; i < teams.Length; i++) if (teams[i] != null) teams[i].SetAnimatorSpeed(1f);
                ProbeActionBubble(newTeam.Flags.mode, "switch-bound-team-" + (index + 1));
                // Binding the target's hand/UI can run native camera setup in
                // the same frame. Restore the saved view after those side
                // effects, before choosing the view guarded on the next frame.
                if (teamCameras[index] != null && teamCameras[index].HasVisibleDrift()) {
                    DiagnosticProbe.Info("team-switch-bind-camera-write team=" + (index + 1) +
                        " " + teamCameras[index].DescribeDrift());
                    teamCameras[index].Restore(newTeam.Female == null || newTeam.Female.objTop == null
                        ? null : newTeam.Female.objTop.transform);
                }
                // The first native frame after a kiss may still apply its old
                // camera parameters. Start guarding the restored view before
                // yielding; arming the guard on the next frame is too late.
                postSwitchCameraProbe = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
                postSwitchCameraProbeTeam = ActiveTeam;
                postSwitchCameraProbeUntilFrame = Time.frameCount + 30;
                postSwitchCameraProbeLastFrame = -1;
                postSwitchCameraRepairCount = 0;
            }
            yield return null;
            if (teamCameras[index] != null) DiagnosticProbe.Info("team-switch-target-next-frame team=" + (index + 1) +
                " " + teamCameras[index].DescribeDrift());
            // This is still the switch coroutine's first frame. A mouse delta
            // here can be leftover from the kiss and must not veto restoring
            // the newly focused team's saved view.
            if (postSwitchCameraProbe != null && postSwitchCameraProbe.HasVisibleDrift()) {
                DiagnosticProbe.Info("team-switch-next-frame-camera-repaired team=" + (index + 1) +
                    " " + postSwitchCameraProbe.DescribeDrift());
                postSwitchCameraProbe.Restore();
            }
            ProbeActionBubble(newTeam.Flags.mode, "switch-next-frame-team-" + (index + 1));
            EnsureActiveCameraBase();
            PositionMoveAxis(newTeam.Female);
            ClearUiFocusAndRestoreCamera();
            UpdateStatus();
            IsSwitching = false;
            LogRuntimeState("switched-to-team-" + (ActiveTeam + 1));
            if (ActiveTeam == 1 && Proc.flags != null && Proc.flags.ctrlCamera != null) DiagnosticProbe.Info(newTeam.DescribeAibuColliders(Proc.flags.ctrlCamera.thisCmaera));
        }

        private static void EnsureBaseAnimations(TeamState team, HSceneProc.AnimationListInfo animation) {
            if (team == null || animation == null) return;
            if (animation.pathFemaleBase != null && !string.IsNullOrEmpty(animation.pathFemaleBase.assetpath)) {
                team.Female.LoadAnimation(animation.pathFemaleBase.assetpath, animation.pathFemaleBase.file, string.Empty);
            }
            if (animation.pathMaleBase != null && !string.IsNullOrEmpty(animation.pathMaleBase.assetpath)) {
                team.Male.LoadAnimation(animation.pathMaleBase.assetpath, animation.pathMaleBase.file, string.Empty);
            }
        }

        private void BindTeam(TeamState team) {
            ChaControl female = team.Female;
            ChaControl male = team.Male;
            SaveData.Heroine heroine = team.FemaleData;
            TeamControllers c = team.Controllers;
            Proc.flags = team.Flags;
            Proc.lstProc = team.Actions;
            Proc.lstFemale = team.ContextFemales;
            Proc.male = male;
            Proc.male1 = null;
            Proc.flags.lstHeroine = team.ContextHeroines;
            Proc.flags.player = team.MaleData;
            Proc.lstMotionIK = team.MotionIKs;
            for (int i = 0; i < team.MotionIKs.Count; i++) team.MotionIKs[i].SetPartners(team.MotionIKs);
            Proc.hitreaction = c.HitReaction;
            Proc.hitcollisionFemale = c.HitCollision;
            Proc.hand = c.Hand;
            Proc.yure = c.Yure;
            Proc.siru = c.Siru;
            Proc.face = c.Face;
            Proc.particle = c.Particle;
            Proc.eyeneckFemale = c.EyeFemale;
            Proc.eyeneckMale = c.EyeMale;
            Proc.dynamicCtrl = c.Dynamic;
            Proc.alCtrl = c.AnimatorLayer;
            Proc.parentObjectBaseFemale = c.ParentBaseFemale;
            Proc.parentObjectFemale = c.ParentFemale;
            Proc.parentObjectBaseMale = c.ParentBaseMale;
            Proc.parentObjectMale = c.ParentMale;
            Proc.meta = c.Meta;
            Proc.lookDan = teamLookDans[team == teams[1] ? 1 : 0];
            BindSecondaryControllerSlots(team);
            Proc.sprite.hand = c.Hand;
            Traverse.Create(Proc.sprite).Field("flags").SetValue(team.Flags);
            Traverse.Create(Proc.voice).Field("flags").SetValue(team.Flags);
            Proc.voice.hand = c.Hand;
            BindVoiceSlots(team);
            c.Hand.flags = team.Flags;
            c.Hand.sprite = Proc.sprite;
            c.Hand.voice = Proc.voice;
            SetActiveInteractionTeam(team);
            Proc.lookDan.SetMale(male);
            Proc.lookDan.SetFemale(new[] { female });
            Proc.subjective.SetSubjectiveObject(Proc.parentObjectBaseMale.GetObject("SubjectiveBase"));
            Proc.subjective.SetFemale(female);
            Proc.subjective.SetMale(male);
            // ChangeAnimator.SetLocalPosition binds the shared camera world base
            // to the woman being initialized. Rebind it on every team switch;
            // loading camera parameters alone does not replace this transform.
            if (Proc.flags.ctrlCamera != null && female.objTop != null) Proc.flags.ctrlCamera.SetWorldBase(female.objTop.transform);
            if (Proc.item != null && female.objTop != null) Proc.item.SetTransform(female.objTop.transform);
            RefreshActionBindings(Proc);
            HSceneOptionsCompat.Bind(Proc, team, team == teams[1] ? 1 : 0);
            RefreshNativeUi(team);
            team.RestoreHandUi(Proc.sprite);
            team.RestoreHandMotionState(log);
            RuntimeModCompat.TeamBound(this, team);
            // The background H update already captured this team's live action
            // buttons (including pending finish bubbles). Rebuilding from the
            // animation here loses their transition state on every Tab press.
            if (!team.RestoreModeUi(Proc.sprite)) RefreshFocusedModeUi(team);

            team.RestoreGuide(Proc);
            PositionMoveAxis(female);
        }

        private void BindSecondaryControllerSlots(TeamState active) {
            if (active == null || active.Controllers == null) return;
            TeamControllers c = active.Controllers;
            // ChangeAnimator updates the secondary slots even in a 1F1M mode.
            // A normal/kPlug 1F1M entry has no real second actor. Point these
            // mandatory compatibility slots at the current team's controllers
            // so the transition cannot overwrite the other independent team.
            Proc.hitreaction1 = c.HitReaction;
            Proc.hitcollisionFemale1 = c.HitCollision;
            Proc.hand1 = c.Hand;
            Proc.yure1 = c.Yure;
            Proc.siru1 = c.Siru;
            Proc.face1 = c.Face;
            Proc.particle1 = c.Particle;
            Proc.eyeneckFemale1 = c.EyeFemale;
            Proc.eyeneckMale1 = c.EyeMale;
            Proc.dynamicCtrl1 = c.Dynamic;
            Proc.alCtrl1 = c.AnimatorLayer;
            Proc.parentObjectBaseFemale1 = c.ParentBaseFemale;
            Proc.parentObjectFemale1 = c.ParentFemale;
        }

        private static Transform GetMouth(ChaControl female) {
            GameObject mouth = female == null ? null : female.GetReferenceInfo(ChaReference.RefObjKey.a_n_mouth);
            return mouth == null ? null : mouth.transform;
        }

        private void SetActiveInteractionTeam(TeamState active) {
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null) continue;
                bool selected = team == active;
                if (team.Controllers != null && team.Controllers.Hand != null) team.Controllers.Hand.enabled = selected;
                team.SetAibuCollidersEnabled(selected);
            }
        }

        private void PositionMoveAxis(ChaControl female) {
            if (female != null && Proc.objMoveAxis != null) Proc.objMoveAxis.transform.SetPositionAndRotation(female.transform.position, female.transform.rotation);
        }

        internal static void RefreshActionBindings(HSceneProc proc, HFlag.EMode? incomingMode = null) {
            if (proc == null || proc.lstProc == null || proc.lstFemale == null || proc.lstFemale.Count == 0) return;
            TeamSession session = Find(proc);
            int bound = session == null ? -1 : session.GetBoundTeamIndex();
            HFlag.EMode mode = incomingMode ?? (proc.flags != null && proc.flags.nowAnimationInfo != null
                ? proc.flags.nowAnimationInfo.mode : HFlag.EMode.aibu);
            bool lesbian = IsTwoFemaleMode(mode) &&
                bound >= 0 && session.teams[1 - bound] != null;
            TeamState partner = lesbian ? session.teams[1 - bound] : null;
            for (int i = 0; i < proc.lstProc.Count; i++) {
                HActionBase action = proc.lstProc[i];
                if (action == null) continue;
                action.female = proc.lstFemale[0];
                action.female1 = lesbian ? partner.Female : null;
                action.male = proc.male;
                action.male1 = null;
                action.lstMotionIK = proc.lstMotionIK;
                action.hand = proc.hand;
                action.hand1 = lesbian ? partner.Controllers.Hand : proc.hand;
                action.yure = proc.yure;
                action.yure1 = lesbian ? partner.Controllers.Yure : null;
                action.hitcolFemale = proc.hitcollisionFemale;
                action.hitcolFemale1 = lesbian ? partner.Controllers.HitCollision : proc.hitcollisionFemale;
                action.hitcolMale = proc.hitcollisionMale;
                action.meta = proc.meta;
                action.parentObjectFemale = proc.parentObjectFemale;
                action.parentObjectFemale1 = lesbian ? partner.Controllers.ParentFemale : null;
                action.parentObjectMale = proc.parentObjectMale;
                action.voice = proc.voice;
                action.particle = proc.particle;
                action.alCtrl = proc.alCtrl;
                action.alCtrl1 = lesbian ? partner.Controllers.AnimatorLayer : proc.alCtrl;
                action.flags = proc.flags;
            }
        }

        internal void PrepareAnimationContext(HSceneProc.AnimationListInfo animation) {
            if (!initialized || animation == null) return;
            int bound = GetBoundTeamIndex();
            if (bound < 0) bound = ActiveTeam;
            TeamState owner = teams[bound];
            if (owner == null) return;
            if (!IsTwoFemaleMode(animation.mode)) {
                bool leavingTwoFemaleRig = lesbianOwner == bound;
                TeamState formerPartner = leavingTwoFemaleRig ? teams[1 - bound] : null;
                // The two-actor context ends at this transition boundary. A
                // later lightweight bind must not put the partner back into
                // the outgoing lesbian action graph.
                if (lesbianOwner == bound) lesbianOwner = -1;
                if (formerPartner != null) ResetTransitionMouth(formerPartner.Female);
                ApplyVisibility();
                BindLightweightContext(owner);
                if (Proc.lookDan != null) Proc.lookDan.SetFemale(new[] { owner.Female });
                if (Proc.sprite != null) Proc.sprite.females = owner.ContextFemales;
                RefreshActionBindings(Proc, animation.mode);
                NotifyExpressionControlCharactersChanged();
                return;
            }
            TeamState partner = teams[1 - bound];
            if (partner == null) return;
            if (Proc.lookDan1 != null && !Proc.lookDan1.enabled) Proc.lookDan1.enabled = true;
            bool enteringLesbian = lesbianOwner != bound || !hasLesbianPartnerPose;
            lesbianOwner = bound;
            if (enteringLesbian) {
                partner.NeedsFullAnimationRestore = true;
                lesbianPartnerRigAnimation = animation;
                lesbianPartnerFemalePosition = partner.Female.transform.position;
                lesbianPartnerFemaleRotation = partner.Female.transform.rotation;
                lesbianPartnerMalePosition = partner.Male.transform.position;
                lesbianPartnerMaleRotation = partner.Male.transform.rotation;
                hasLesbianPartnerPose = true;
            }
            partner.Female.transform.SetPositionAndRotation(owner.Female.transform.position, owner.Female.transform.rotation);
            lesbianFemales = new List<ChaControl> { owner.Female, partner.Female };
            lesbianHeroines = new List<SaveData.Heroine> { owner.FemaleData, partner.FemaleData };
            Proc.lstFemale = lesbianFemales;
            if (Proc.lookDan != null) Proc.lookDan.SetFemale(lesbianFemales.ToArray());
            owner.Flags.lstHeroine = lesbianHeroines;
            // Both lesbian and native 2F1M actions use [female0, male,
            // female1]. Keep this separate from either team's 1F1M IK list:
            // native 3P ChangeAnimator clears and rebuilds its input list.
            lesbianMotionIKs = new List<MotionIK> { owner.MotionIKs[0], owner.MotionIKs[1], partner.MotionIKs[0] };
            Proc.lstMotionIK = lesbianMotionIKs;
            for (int i = 0; i < Proc.lstMotionIK.Count; i++) Proc.lstMotionIK[i].SetPartners(Proc.lstMotionIK);
            Proc.hitreaction1 = partner.Controllers.HitReaction;
            Proc.hitcollisionFemale1 = partner.Controllers.HitCollision;
            Proc.hand1 = partner.Controllers.Hand;
            Proc.yure1 = partner.Controllers.Yure;
            Proc.siru1 = partner.Controllers.Siru;
            Proc.face1 = partner.Controllers.Face;
            Proc.particle1 = partner.Controllers.Particle;
            Proc.eyeneckFemale1 = partner.Controllers.EyeFemale;
            Proc.eyeneckMale1 = partner.Controllers.EyeMale;
            Proc.dynamicCtrl1 = partner.Controllers.Dynamic;
            Proc.alCtrl1 = partner.Controllers.AnimatorLayer;
            Proc.parentObjectBaseFemale1 = partner.Controllers.ParentBaseFemale;
            Proc.parentObjectFemale1 = partner.Controllers.ParentFemale;
            BindVoiceSlots(owner, partner);
            // The voice slots are reordered for the lesbian owner. Snapshot
            // their identities only after that reorder, otherwise team 2 as
            // owner compares each woman's voice against the other's old slot.
            for (int slot = 0; slot < lastLesbianVoiceInfo.Length; slot++) {
                object voice = Proc.voice != null && Proc.voice.nowVoices != null && Proc.voice.nowVoices.Length > slot
                    ? Proc.voice.nowVoices[slot] : null;
                object info = voice == null ? null : Traverse.Create(voice).Field("voiceInfo").GetValue();
                lastLesbianVoiceInfo[slot] = info;
                lastLesbianVoiceFace[slot] = info == null ? -1 : Traverse.Create(info).Field("face").GetValue<int>();
            }
            NotifyExpressionControlCharactersChanged();
            if (Proc.sprite != null) Proc.sprite.females = Proc.lstFemale;
            RefreshActionBindings(Proc, animation.mode);
            lastFlushedVoiceRequests[0] = -1;
            lastFlushedVoiceRequests[1] = -1;
            DiagnosticProbe.Info("two-female-context owner=" + (bound + 1) + " partner=" + (2 - bound) +
                " mode=" + animation.mode + " animation=" + animation.nameAnimation);
        }

        internal void BeforeAnimationChange(HSceneProc.AnimationListInfo animation) {
            if (!initialized || animation == null) return;
            int bound = GetBoundTeamIndex();
            if (bound < 0) bound = ActiveTeam;
            TeamState team = teams[bound];
            if (team == null || team.Female == null) return;
            ProbeMaleRig(1 - bound, "other-before-" + animation.mode);
            // A background ChangeAnimator briefly writes the shared camera
            // too. Its parallel pass restores the focused view; an older
            // post-switch guard must not replay its stale camera afterwards.
            postSwitchCameraProbe = null;
            // A new action replaces the post-kiss camera. Do not preserve the
            // old view as this team's ordinary camera through that transition.
            settledKissCameras[bound] = null;
            kissCameraSettlingUntilFrame[bound] = 0;
            if (preKissCameras[bound] == null && !RuntimeModCompat.IsKissBusy(team)) return;
            // A new action owns its own camera. The pre-kiss snapshot must be
            // retired before ChangeAnimator loads that action's camera; a
            // later team switch must never restore the older kiss snapshot.
            RuntimeModCompat.ActionChanging(this, team);
            EndKissCamera(team.Female, false);
            kissCameraSettlingUntilFrame[bound] = 0;
            kissInactiveFrames[bound] = 0;
            DiagnosticProbe.Info("kiss-camera-discarded-for-action team=" + (bound + 1) +
                " incoming=" + animation.mode + "/" + animation.nameAnimation);
        }

        private void RefreshNativeUi(TeamState team) {
            if (Proc.sprite == null) return;
            Proc.sprite.females = Proc.lstFemale;
            Proc.sprite.male = Proc.male;
            Proc.sprite.heroine = team.FemaleData;
            object cloth = Traverse.Create(Proc.sprite).Field("clothCusutomCtrl").GetValue();
            if (cloth != null) Traverse.Create(cloth).Field("chaCtrl").SetValue(Proc.lstFemale[0]);
            if (Proc.sprite.rawFace != null) Proc.sprite.rawFace.texture = team.EnsurePortrait();
            if (Proc.sprite.categoryMenstruation != null) {
                int menstruation = (int)HFlag.GetMenstruation(team.FemaleData.MenstruationDay) == 0 ? 0 : 1;
                Proc.sprite.categoryMenstruation.SetActiveToggle(menstruation);
            }
            if (Proc.sprite.categoryExperience != null) Proc.sprite.categoryExperience.SetActiveToggle((int)team.FemaleData.HExperience);
        }

        private void RefreshFocusedModeUi(TeamState team) {
            if (Proc.sprite == null || team == null || team.Animation == null) return;
            try {
                ReplayingModeUi = true;
                HFlag.EMode actualMode = team.Animation.mode;
                bool specialAibu = actualMode == HFlag.EMode.lesbian || actualMode == HFlag.EMode.masturbation;
                if (specialAibu) team.Animation.mode = HFlag.EMode.aibu;
                try { Proc.sprite.MainSpriteChange(team.Animation); }
                finally { if (specialAibu) team.Animation.mode = actualMode; }
                team.ReplayModeUi(Proc.sprite);
                team.CaptureModeUi(Proc.sprite);
            } catch (Exception ex) {
                log.LogWarning("Could not refresh the focused H UI: " + ex.Message);
            } finally {
                ReplayingModeUi = false;
            }
        }

        private void ProbeActionBubble(HFlag.EMode mode, string phase) {
            if (Proc == null || Proc.sprite == null) return;
            HSceneSpriteCategory category = mode == HFlag.EMode.houshi && Proc.sprite.houshi != null ? Proc.sprite.houshi.categoryActionButton :
                mode == HFlag.EMode.sonyu && Proc.sprite.sonyu != null ? Proc.sprite.sonyu.categoryActionButton :
                mode == HFlag.EMode.houshi3P && Proc.sprite.houshi3P != null ? Proc.sprite.houshi3P.categoryActionButton :
                mode == HFlag.EMode.sonyu3P && Proc.sprite.sonyu3P != null ? Proc.sprite.sonyu3P.categoryActionButton : null;
            if (category == null) return;
            List<string> states = new List<string>();
            for (int i = 0; i < category.GetCount() && i < 8; i++) {
                GameObject item = category.GetObject(i);
                if (item == null) continue;
                Button button = item.GetComponentInChildren<Button>(true);
                Graphic hit = button == null ? null : button.targetGraphic;
                CanvasGroup group = item.GetComponentInParent<CanvasGroup>();
                Text label = item.GetComponentInChildren<Text>(true);
                Image image = hit as Image;
                states.Add(i + ":" + item.activeInHierarchy + "/" +
                    (button == null ? "no-button" : button.interactable.ToString()) + "/" +
                    (hit == null ? "no-hit" : hit.raycastTarget.ToString()) + "/" +
                    (group == null ? "no-group" : group.blocksRaycasts.ToString()) +
                    "/" + (label == null ? "-" : label.text) +
                    "/" + (image == null || image.sprite == null ? "-" : image.sprite.name) +
                    "/override=" + (image == null || image.overrideSprite == null ? "-" : image.overrideSprite.name) +
                    "/render=" + (hit == null ? "-" : hit.canvasRenderer.GetColor().ToString()));
            }
            DiagnosticProbe.Info("action-bubble-input " + phase + " mode=" + mode +
                " click=" + Proc.flags.click + " finish=" + Proc.flags.finish +
                " [" + string.Join(",", states.ToArray()) + "]");
        }

        internal static void ProbeActionButtonClick(HSprite sprite, string phase) {
            if (sprite == null) return;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                session.ProbeActionBubble(session.teams[session.ActiveTeam].Flags.mode,
                    phase + "-team-" + (session.ActiveTeam + 1));
                break;
            }
        }

        private void BindAvatarSwitch() {
            if (Proc.sprite == null || Proc.sprite.rawFace == null) return;
            avatarButton = Proc.sprite.rawFace.GetComponent<Button>();
            if (avatarButton == null) {
                avatarButton = Proc.sprite.rawFace.gameObject.AddComponent<Button>();
                addedAvatarButton = true;
            }
            avatarButton.onClick.AddListener(SwitchFromAvatar);
        }

        private void SwitchFromAvatar() { SwitchTo(1 - ActiveTeam); }

        private void Update() {
            if (!initialized || Proc == null || Proc.flags == null) return;
            float probeNow = Time.realtimeSinceStartup;
            if (lastProbeUpdate > 0f && probeNow - lastProbeUpdate >= 0.25f) {
                DiagnosticProbe.Warning("frame-stall deltaMs=" + ((probeNow - lastProbeUpdate) * 1000f).ToString("0") + " " + DescribeControlState());
            }
            lastProbeUpdate = probeNow;
            if (probeNow - lastProbeHeartbeat >= 2f) {
                lastProbeHeartbeat = probeNow;
                DiagnosticProbe.Info("heartbeat " + DescribeControlState() + " managedMB=" + (GC.GetTotalMemory(false) / 1048576f).ToString("0.0"));
            }
            RuntimeModCompat.Update(this);
            BreastmilkCompat.Update(this);
            if (teams[ActiveTeam] != null && teams[ActiveTeam].ClearHandForCurrentState()) teams[ActiveTeam].CaptureHandUi(Proc.sprite);
            // HandCtrl/native UI owns the camera-move condition during Aibu.
            // Forcing it on for every click outside EventSystem steals clicks
            // from world-space touch targets and can trap the mouse in camera
            // movement when an action bubble briefly misses its raycast.
        }

        private void LateUpdate() {
            if (!initialized || postSwitchCameraProbe == null || ActiveTeam != postSwitchCameraProbeTeam ||
                Time.frameCount > postSwitchCameraProbeUntilFrame) return;
            // Tab often arrives with a mouse delta left over from the kiss.
            // That input must not disarm the guard during the switch frame;
            // otherwise a camera write later in this frame becomes visible.
            if (!IsSwitching &&
                (HasManualCameraInput() || preKissCameras[ActiveTeam] != null || RuntimeModCompat.IsKissBusy(teams[ActiveTeam]))) {
                DiagnosticProbe.Info("post-switch-camera-guard-ended team=" + (ActiveTeam + 1) +
                    " manual=" + HasManualCameraInput() + " kiss=" + RuntimeModCompat.IsKissBusy(teams[ActiveTeam]));
                postSwitchCameraProbe = null;
                return;
            }
            // Native/CyNoVR kiss cleanup can finish one frame after Tab and
            // write into the single shared camera. Keep the newly focused
            // team's captured view until those deferred writes have drained.
            if (postSwitchCameraProbe.HasVisibleDrift()) {
                if (postSwitchCameraRepairCount++ < 3) {
                    DiagnosticProbe.Info("post-switch-camera-repaired team=" + (ActiveTeam + 1) +
                        " frame=" + Time.frameCount + " " + postSwitchCameraProbe.DescribeDrift());
                }
                postSwitchCameraProbe.Restore();
                return;
            }
            if (postSwitchCameraProbeLastFrame >= 0 && Time.frameCount - postSwitchCameraProbeLastFrame < 10) return;
            postSwitchCameraProbeLastFrame = Time.frameCount;
            DiagnosticProbe.Info("post-switch-camera team=" + (ActiveTeam + 1) +
                " frame=" + Time.frameCount + " manual=" + HasManualCameraInput() +
                " kiss=" + RuntimeModCompat.IsKissBusy(teams[ActiveTeam]) +
                " " + postSwitchCameraProbe.DescribeDrift());
        }

        internal void AfterNativeUpdate() {
            if (!initialized || IsSwitching || Proc == null || Proc.sprite == null || teams[ActiveTeam] == null) {
                DiagnosticProbe.OnceInfo("after-native-update-skipped", "after-native-update-skipped initialized=" + initialized +
                    " switching=" + IsSwitching + " proc=" + (Proc != null) +
                    " sprite=" + (Proc != null && Proc.sprite != null) + " activeTeam=" + ActiveTeam);
                return;
            }
            DiagnosticProbe.OnceInfo("after-native-update-active", "after-native-update-active team=" + (ActiveTeam + 1));
            try {
                EnsureSpecialActionMenu();
                DecorateSpecialActionLabels(Proc.sprite);
                FlushLesbianVoiceAndFace();
                ApplyCameraLookOverrides();
                RestoreCompletedKissCameraIfStable();
                if (!EnsureActiveCameraBase()) TrackStableCamera();
                if (Time.frameCount <= speedClickProbeUntilFrame)
                    DiagnosticProbe.Info("speed-click-followup frame=" + Time.frameCount + " " + DescribeControlState());
                ProbeMaleGenitalAnimation();
                if (inactiveHeroinePanel != null) inactiveHeroinePanel.Refresh(teams[ActiveTeam], teams[1 - ActiveTeam]);
            } catch (Exception ex) {
                DiagnosticProbe.Error("after-native-update failed: " + ex);
            }
        }

        private void RestoreCompletedKissCameraIfStable() {
            int teamIndex = ActiveTeam;
            if (teams[teamIndex] == null) {
                kissInactiveFrames[teamIndex] = 0;
                return;
            }
            TeamState team = teams[teamIndex];
            bool hasCameraSnapshot = preKissCameras[teamIndex] != null;
            if (!hasCameraSnapshot && !RuntimeModCompat.IsKissBusy(team)) {
                kissInactiveFrames[teamIndex] = 0;
                return;
            }
            if (RuntimeModCompat.IsKissAnimationActive(team)) {
                kissInactiveFrames[teamIndex] = 0;
                return;
            }
            // K_Loop may end while CyNoVR's exit tail still owns the camera.
            // Mouse movement during that tail is part of the kiss interaction,
            // not a deliberate change of the post-kiss view.
            if (!RuntimeModCompat.IsKissBusy(team) && HasManualCameraInput() && !kissCameraMovedByUser[teamIndex]) {
                kissCameraMovedByUser[teamIndex] = true;
                DiagnosticProbe.Info("kiss-camera manual-view team=" + (teamIndex + 1) + " frame=" + Time.frameCount);
            }
            // K_Touch briefly leaves a non-kiss animator state before K_Loop.
            // Wait long enough to distinguish that gap from a completed kiss.
            if (++kissInactiveFrames[teamIndex] < 30) return;
            kissInactiveFrames[teamIndex] = 0;
            RuntimeModCompat.CompleteKiss(team);
            SetKissHeadOverride(team.Female, false);
            bool restore = hasCameraSnapshot && !kissCameraMovedByUser[teamIndex];
            EndKissCamera(team.Female, restore);
            if (hasCameraSnapshot && !restore) teamCameras[teamIndex] = CameraSnapshot.Capture(Proc.flags.ctrlCamera);
            DiagnosticProbe.Info("kiss-camera stable-end team=" + (teamIndex + 1) +
                " snapshot=" + hasCameraSnapshot + " restored=" + restore);
        }

        internal static void ProbeSpeedClick(HSprite sprite) {
            if (sprite == null ||
                (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))) return;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                HFlag spriteFlags = Traverse.Create(sprite).Field("flags").GetValue<HFlag>();
                ChaControl spriteFemale = sprite.females == null || sprite.females.Count == 0 ? null : sprite.females[0];
                session.speedClickProbeUntilFrame = Time.frameCount + 5;
                int mode = (int)session.teams[session.ActiveTeam].Flags.mode;
                HActionBase action = session.Proc.lstProc != null && mode >= 0 && mode < session.Proc.lstProc.Count
                    ? session.Proc.lstProc[mode] : null;
                DiagnosticProbe.Info("speed-click team=" + (session.ActiveTeam + 1) +
                    " spriteFlagTeam=" + (spriteFlags == session.teams[0].Flags ? 1 : spriteFlags == session.teams[1].Flags ? 2 : 0) +
                    " procFlagTeam=" + (session.GetBoundTeamIndex() + 1) +
                    " spriteFemale=" + (spriteFemale == null ? "null" : spriteFemale.gameObject.name) +
                    " action=" + (action == null ? "null" : action.GetType().Name) +
                    " actionFlagMatch=" + (action != null && action.flags == session.teams[session.ActiveTeam].Flags) +
                    " actionFemaleMatch=" + (action != null && action.female == session.teams[session.ActiveTeam].Female) +
                    " hsceneOptions={" + HSceneOptionsCompat.Describe(session.teams[session.ActiveTeam]) + "}" +
                    " mouse=" + (Input.GetMouseButtonDown(0) ? "left" : Input.GetMouseButtonDown(1) ? "right" : Input.GetMouseButtonDown(2) ? "middle" : "none") +
                    " " + session.DescribeControlState());
                return;
            }
        }

        internal static void DetachManagedPortraitBeforeNative3P(HSprite sprite) {
            if (sprite == null || sprite.rawFace == null) return;
            for (int s = 0; s < Sessions.Count; s++) {
                TeamSession session = Sessions[s];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                for (int i = 0; i < session.teams.Length; i++) {
                    TeamState team = session.teams[i];
                    if (team == null || !ReferenceEquals(sprite.rawFace.texture, team.Portrait)) continue;
                    // HSprite.InitHeroine destroys rawFace.texture before it
                    // installs its 3P portrait. That texture is our persistent
                    // card for the other half of the dual portrait panel.
                    sprite.rawFace.texture = null;
                    DiagnosticProbe.Info("3p-portrait-detached team=" + (i + 1) +
                        " portrait=" + team.Portrait.width + "x" + team.Portrait.height);
                    return;
                }
            }
        }

        internal static void RestoreManagedPortraitAfterNative3P(HSprite sprite) {
            if (sprite == null || sprite.rawFace == null) return;
            for (int s = 0; s < Sessions.Count; s++) {
                TeamSession session = Sessions[s];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                TeamState focused = session.teams[session.ActiveTeam];
                TeamState background = session.teams[1 - session.ActiveTeam];
                if (focused == null || background == null) return;
                // InitHeroine replaces the native RawImage with a temporary
                // portrait. The dual panel uses it for the background team,
                // so restore both halves before this frame can be rendered.
                sprite.rawFace.texture = background.EnsurePortrait();
                if (session.inactiveHeroinePanel != null)
                    session.inactiveHeroinePanel.Refresh(focused, background);
                DiagnosticProbe.Info("3p-portrait-restored-inline focus=" + (session.ActiveTeam + 1));
                return;
            }
        }

        internal static void FlushLesbianVoiceAfterAction(HActionBase action) {
            if (action == null || action.female == null) return;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session != null && session.ContainsFemale(action.female)) {
                    session.FlushLesbianVoiceAndFace();
                    return;
                }
            }
        }

        private void FlushLesbianVoiceAndFace() {
            if (lesbianOwner < 0 || Proc.voice == null || Proc.flags == null || teams[lesbianOwner] == null || teams[1 - lesbianOwner] == null) return;
            TeamState owner = teams[lesbianOwner];
            TeamState partner = teams[1 - lesbianOwner];
            if (owner.Flags == null || owner.Flags.voice == null) return;
            int[] pending = owner.Flags.voice.playVoices;
            try {
                // ChangeAnimator and the background pass both rewrite the
                // shared HVoiceCtrl slots. Rebind immediately before native
                // voice/face processing so slot 1 targets the partner.
                BindVoiceSlots(owner, partner);
                List<ChaControl> lesbianFemales = this.lesbianFemales;
                if (lesbianFemales == null) return;
                string pendingText = pending == null ? "null" :
                    (pending.Length == 0 ? "empty" : pending[0] + (pending.Length > 1 ? "/" + pending[1] : string.Empty));
                DiagnosticProbe.OnceInfo("lesbian-face-path-" + lesbianOwner,
                    "lesbian-face-path owner=" + (lesbianOwner + 1) + " females=" + lesbianFemales.Count +
                    " pending=" + pendingText +
                    " face0=" + (Proc.voice.faceLists == null || Proc.voice.faceLists.Length < 1 || Proc.voice.faceLists[0] == null ? "null" : Proc.voice.faceLists[0].GetHashCode().ToString()) +
                    " face1=" + (Proc.voice.faceLists == null || Proc.voice.faceLists.Length < 2 || Proc.voice.faceLists[1] == null ? "null" : Proc.voice.faceLists[1].GetHashCode().ToString()));
                if (pending != null && pending.Length >= 2 &&
                    (pending[0] != lastFlushedVoiceRequests[0] || pending[1] != lastFlushedVoiceRequests[1])) {
                    AnimatorStateInfo state = owner.Female.getAnimatorStateInfo(0);
                    int first = pending[0];
                    int second = pending[1];
                    lastFlushedVoiceRequests[0] = first;
                    lastFlushedVoiceRequests[1] = second;
                    if (first >= 0 || second >= 0) {
                        Proc.voice.Proc(state, lesbianFemales);
                        DiagnosticProbe.Info("two-female-voice-flush owner=" + (lesbianOwner + 1) +
                            " requests=" + first + "/" + second + " remaining=" + pending[0] + "/" + pending[1]);
                    }
                }
                ApplyLesbianVoiceFaces();
            } catch (Exception ex) {
                DiagnosticProbe.Warning("lesbian voice/face flush failed: " + ex.Message);
            }
        }

        private void ApplyLesbianVoiceFaces() {
            if (lesbianOwner < 0 || Proc.voice == null || Proc.voice.nowVoices == null || Proc.voice.nowVoices.Length < 2) return;
            for (int slot = 0; slot < 2; slot++) {
                object voice = Proc.voice.nowVoices[slot];
                object info = voice == null ? null : Traverse.Create(voice).Field("voiceInfo").GetValue();
                if (info == null) continue;
                int face = Traverse.Create(info).Field("face").GetValue<int>();
                if (ReferenceEquals(lastLesbianVoiceInfo[slot], info) && lastLesbianVoiceFace[slot] == face) continue;
                lastLesbianVoiceInfo[slot] = info;
                lastLesbianVoiceFace[slot] = face;
                int teamIndex = slot == 0 ? lesbianOwner : 1 - lesbianOwner;
                TeamState team = teams[teamIndex];
                if (team == null || team.Controllers == null || team.Controllers.Face == null) continue;
                bool applied = team.Controllers.Face.SetFace(face, team.Female, 1, 6);
                DiagnosticProbe.Info("lesbian-face-applied team=" + (teamIndex + 1) + " slot=" + slot +
                    " face=" + face + " applied=" + applied);
            }
        }

        private void TrackStableCamera() {
            TeamState team = teams[ActiveTeam];
            if (team == null || team.Female == null || team.Flags == null || team.Flags.ctrlCamera == null ||
                preKissCameras[ActiveTeam] != null || RuntimeModCompat.IsKissBusy(team)) return;
            if (postSwitchCameraProbe != null && ActiveTeam == postSwitchCameraProbeTeam &&
                Time.frameCount <= postSwitchCameraProbeUntilFrame && !HasManualCameraInput()) return;
            if (settledKissCameras[ActiveTeam] != null) {
                if (HasManualCameraInput() || Time.frameCount > kissCameraSettlingUntilFrame[ActiveTeam] + 40)
                    settledKissCameras[ActiveTeam] = null;
                else return;
            }
            AnimatorStateInfo state = team.Female.getAnimatorStateInfo(0);
            if (state.IsName("K_Touch") || state.IsName("K_Loop")) return;
            teamCameras[ActiveTeam] = CameraSnapshot.Capture(team.Flags.ctrlCamera);
        }

        private void ProbeMaleGenitalAnimation() {
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Male == null || team.Male.animBody == null ||
                    (team.Flags.mode != HFlag.EMode.houshi && team.Flags.mode != HFlag.EMode.sonyu && !IsThreePMode(team.Flags.mode))) continue;
                Transform bone = maleGenitalBones[i];
                if (bone == null) {
                    GameObject reference = team.Male.GetReferenceInfo(ChaReference.RefObjKey.a_n_dan);
                    bone = reference == null ? null : reference.transform;
                    maleGenitalBones[i] = bone;
                }
                if (bone == null) continue;
                AnimatorStateInfo state = team.Male.getAnimatorStateInfo(0);
                Quaternion rotation = bone.rotation;
                if (lastMaleGenitalFrame[i] == Time.frameCount - 1) {
                    float jump = Quaternion.Angle(lastMaleGenitalRotation[i], rotation);
                    if (jump >= 30f && Time.realtimeSinceStartup - lastMaleGenitalProbe[i] >= 2f) {
                        lastMaleGenitalProbe[i] = Time.realtimeSinceStartup;
                        DiagnosticProbe.Warning("male-genital-snap team=" + (i + 1) + " focus=" + (ActiveTeam + 1) +
                            " angle=" + jump.ToString("0.0") + " bone=" + bone.position +
                            " maleRoot=" + team.Male.transform.position + "/" + team.Male.transform.eulerAngles +
                            " previousState=" + lastMaleAnimationHash[i] + " state=" + state.shortNameHash +
                            " femaleState=" + team.Female.getAnimatorStateInfo(0).shortNameHash +
                            " mode=" + team.Flags.mode + " action=" + (team.Animation == null ? "null" : team.Animation.nameAnimation));
                    }
                }
                lastMaleGenitalRotation[i] = rotation;
                lastMaleGenitalFrame[i] = Time.frameCount;
                lastMaleAnimationHash[i] = state.shortNameHash;
            }
        }

        private bool EnsureActiveCameraBase() {
            TeamState team = teams[ActiveTeam];
            if (team == null || team.Female == null || team.Female.objTop == null || team.Flags == null ||
                team.Flags.mode != HFlag.EMode.aibu || team.Flags.ctrlCamera == null ||
                preKissCameras[ActiveTeam] != null || RuntimeModCompat.IsKissBusy(team)) return false;
            Transform cameraBase = team.Flags.ctrlCamera.transBase;
            Transform actorBase = team.Female.objTop.transform;
            if (cameraBase == null || !cameraBase.name.StartsWith("CameraBase", StringComparison.Ordinal)) return false;
            float ownDistance = Vector3.Distance(cameraBase.position, actorBase.position);
            if (ownDistance < 0.75f) return false;
            TeamState other = teams[1 - ActiveTeam];
            Transform otherBase = other == null || other.Female == null || other.Female.objTop == null
                ? null : other.Female.objTop.transform;
            if (otherBase != null && Vector3.Distance(cameraBase.position, otherBase.position) + 0.35f >= ownDistance)
                return false;
            Vector3 previous = cameraBase.position;
            team.Flags.ctrlCamera.SetWorldBase(actorBase);
            if (Time.frameCount - lastCameraBaseRepairLogFrame >= 120) {
                lastCameraBaseRepairLogFrame = Time.frameCount;
                DiagnosticProbe.Warning("camera-base-cross-team-repaired team=" + (ActiveTeam + 1) +
                    " previous=" + previous + " actor=" + actorBase.position +
                    " other=" + (otherBase == null ? "null" : otherBase.position.ToString()) +
                    " current=" + cameraBase.position);
            }
            // CameraControl recomputes its world view in LateUpdate. Do not
            // capture a mixed old-camera/new-base state during this frame.
            return true;
        }

        internal static void DecorateSpecialActionLabels(HSprite sprite) {
            if (sprite == null) return;
            TeamSession owner = null;
            for (int i = 0; i < Sessions.Count; i++) if (Sessions[i] != null && Sessions[i].Proc != null && Sessions[i].Proc.sprite == sprite) { owner = Sessions[i]; break; }
            if (owner == null) return;
            HSprite.AnimationInfoComponent[] entries = sprite.GetComponentsInChildren<HSprite.AnimationInfoComponent>(true);
            TeamState active = owner.teams[owner.ActiveTeam];
            bool leavingSpecial = active != null && active.Animation != null && IsAppendedActionMode(active.Animation.mode);
            bool physicalSpecial = owner.specialRigActive[owner.ActiveTeam];
            for (int i = 0; i < entries.Length; i++) {
                HSceneProc.AnimationListInfo info = entries[i] == null ? null : entries[i].info;
                if (info == null) continue;
                SpecialMotionClickRelay relay = entries[i].GetComponent<SpecialMotionClickRelay>();
                if (relay != null) relay.Bind(owner, sprite, info, leavingSpecial || physicalSpecial);
                // OnChangePlaySelect returns immediately while flags.mode is a
                // terminal special mode. Every visible entry therefore needs
                // the direct transition relay, including ordinary Aibu,
                // Houshi and Sonyu entries used to leave that mode.
                if (leavingSpecial || physicalSpecial) {
                    if (relay == null) relay = entries[i].gameObject.AddComponent<SpecialMotionClickRelay>();
                    relay.Bind(owner, sprite, info, true);
                }
                // The native list can hand the UI a copied AnimationListInfo,
                // so reference membership alone is insufficient. Mode is the
                // canonical classification; the sets cover kPlug entries that
                // are injected into Aibu while retaining mode=aibu.
                string prefix = info.mode == HFlag.EMode.lesbian || owner.lesbianAibuEntries.Contains(info) ? "[百合] " :
                    info.mode == HFlag.EMode.masturbation || owner.masturbationAibuEntries.Contains(info) ? "[自慰] " :
                    IsThreePMode(info.mode) || owner.threePEntries.Contains(info) ? "[3P] " : null;
                if (prefix == null) continue;
                // Animation rows are pooled and rebound by HSprite. Never append
                // a delegate that captures the current info: after rebuilding a
                // category that delegate points at a stale special action. The
                // relay below is rebound on every pass and always uses current data.
                if (relay == null) relay = entries[i].gameObject.AddComponent<SpecialMotionClickRelay>();
                relay.Bind(owner, sprite, info, leavingSpecial || physicalSpecial);
                TMP_Text label = entries[i].GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                string source = info.nameAnimation ?? string.Empty;
                if (source.StartsWith("[百合] ", StringComparison.Ordinal)) source = source.Substring(5);
                else if (source.StartsWith("[自慰] ", StringComparison.Ordinal)) source = source.Substring(5);
                else if (source.StartsWith("[3P] ", StringComparison.Ordinal)) source = source.Substring(5);
                string desired = prefix + SpecialActionLocalizer.Localize(source);
                if (label.text != desired) label.text = desired;
            }
        }

        private void EnsureSpecialActionMenu() {
            TeamState team = teams[ActiveTeam];
            if (team == null || team.Animation == null) return;
            HFlag.EMode mode = team.Animation.mode;
            if (!IsAppendedActionMode(mode)) return;

            // Vanilla treats these as terminal one-category modes and hides the
            // normal action category menu.  They are deliberately exposed from
            // our Aibu list, so keep only that lightweight menu root visible.
            // Do not call MainSpriteChange here: that would restart the special
            // mode and rebuild the whole action UI every frame.
            GameObject actionMenu = Proc.sprite.menuAction == null ? null : Proc.sprite.menuAction.gameObject;
            if (actionMenu != null && !actionMenu.activeInHierarchy) {
                Transform stop = Proc.sprite.objBaseMainMenu == null ? null : Proc.sprite.objBaseMainMenu.transform;
                Transform current = actionMenu.transform;
                while (current != null) {
                    if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
                    if (current == stop) break;
                    current = current.parent;
                }
                DiagnosticProbe.Info("special-action-menu-restored mode=" + mode + " team=" + (ActiveTeam + 1) +
                    " active=" + actionMenu.activeInHierarchy);
            }
            if (Proc.sprite.menuAction != null) {
                for (int i = 0; i < Proc.sprite.menuAction.GetCount(); i++) {
                    Proc.sprite.menuAction.SetActive(true, i);
                    Proc.sprite.menuAction.SetEnable(true, i);
                }
            }
            if (Proc.sprite.menuMain != null && Proc.sprite.menuMain.GetCount() > 2) Proc.sprite.menuMain.SetEnable(true, 2);
        }

        internal static bool HandleSpecialActionMenu(HSprite sprite, int category) {
            if (sprite == null) return false;
            TeamSession session = null;
            for (int i = 0; i < Sessions.Count; i++) {
                if (Sessions[i] != null && Sessions[i].initialized && Sessions[i].Proc != null && Sessions[i].Proc.sprite == sprite) {
                    session = Sessions[i];
                    break;
                }
            }
            if (session == null || session.teams[session.ActiveTeam] == null || session.teams[session.ActiveTeam].Animation == null) return false;
            return session.OpenCategoryFromSpecialAction(category);
        }

        internal static bool HandleSpecialMotionSelection(HSprite sprite, GameObject clicked) {
            if (sprite == null || clicked == null) return false;
            HSprite.AnimationInfoComponent component = clicked.GetComponent<HSprite.AnimationInfoComponent>();
            HSceneProc.AnimationListInfo selected = component == null ? null : component.info;
            if (selected == null) return false;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                TeamState team = session.teams[session.ActiveTeam];
                if (team == null || team.Animation == null) return false;
                HFlag.EMode current = team.Animation.mode;
                if (!IsAppendedActionMode(current)) return false;
                DiagnosticProbe.Info("special-motion-click from=" + current + " to=" + selected.mode + " name=" + selected.nameAnimation +
                    " team=" + (session.ActiveTeam + 1));
                // OnChangePlaySelect mutates the shared menu and queues an
                // ordinary action change, neither of which is valid while the
                // physical rig is in a special mode. Run one direct transition
                // and suppress the native handler; the pointer relay sees the
                // same frame and deduplicates it.
                session.StartSpecialMotionTransition(session.ActiveTeam, selected, current);
                return true;
            }
            return false;
        }

        private static void ForceSpecialMotionSelection(HSprite sprite, HSceneProc.AnimationListInfo selected) {
            if (sprite == null || selected == null) return;
            for (int i = 0; i < Sessions.Count; i++) {
                TeamSession session = Sessions[i];
                if (session == null || !session.initialized || session.Proc == null || session.Proc.sprite != sprite) continue;
                TeamState team = session.teams[session.ActiveTeam];
                if (team == null || team.Animation == null) return;
                HFlag.EMode current = team.Animation.mode;
                if (!IsAppendedActionMode(current) && !session.specialRigActive[session.ActiveTeam]) return;
                session.StartSpecialMotionTransition(session.ActiveTeam, selected, current);
                return;
            }
        }

        private void StartSpecialMotionTransition(int teamIndex, HSceneProc.AnimationListInfo selected, HFlag.EMode current) {
            if (selected == null || specialTransitionRunning) return;
            if (lastSpecialTransitionFrame == Time.frameCount && ReferenceEquals(lastSpecialTransition, selected)) return;
            lastSpecialTransitionFrame = Time.frameCount;
            lastSpecialTransition = selected;
            DiagnosticProbe.Info("special-motion-pointer from=" + current + " to=" + selected.mode + " name=" + selected.nameAnimation +
                " team=" + (teamIndex + 1) + " direct=true");
            ChangeSpecialMotion(teamIndex, selected);
        }

        private void ChangeSpecialMotion(int teamIndex, HSceneProc.AnimationListInfo selected) {
            specialTransitionRunning = true;
            try {
                TeamState team = teamIndex >= 0 && teamIndex < teams.Length ? teams[teamIndex] : null;
                if (!initialized || team == null || selected == null) return;
                HFlag.EMode current = team.Animation == null ? team.Flags.mode : team.Animation.mode;
                if (!IsAppendedActionMode(current) && !specialRigActive[teamIndex]) return;

                // Native special-mode action objects never consume a normal
                // list selection. Enter ChangeAnimator directly with the
                // clicked team's cloned context bound.
                BindLightweightContext(team);
                team.Flags.selectAnimationListInfo = null;
                bool voiceWaitBefore = team.Flags.voiceWait;
                Proc.ChangeAnimator(selected, true);
                team.Flags.selectAnimationListInfo = null;
                // HSprite sets voiceWait while queuing a native selection. This
                // direct path consumes that selection itself, so there is no
                // queued native update left to clear the latch.
                team.Flags.voiceWait = false;
                DiagnosticProbe.Info("special-motion-transition-complete team=" + (teamIndex + 1) +
                    " mode=" + team.Flags.mode + " animation=" + (team.Animation == null ? "null" : team.Animation.nameAnimation) +
                    " voiceWait=" + voiceWaitBefore + "->" + team.Flags.voiceWait);
            } catch (Exception ex) {
                DiagnosticProbe.Error("special-motion-transition failed team=" + (teamIndex + 1) + " error=" + ex);
            } finally {
                specialTransitionRunning = false;
            }
        }

        private bool OpenCategoryFromSpecialAction(int category) {
            if (!initialized || Proc == null || Proc.sprite == null || teams[ActiveTeam] == null || teams[ActiveTeam].Animation == null) return false;
            HFlag.EMode mode = teams[ActiveTeam].Animation.mode;
            if (!IsAppendedActionMode(mode)) return false;
            HFlag.EMode previous;
            if (!BeginSpecialMenuBuild(Proc.sprite, out previous)) return false;
            try {
                // CreateMotionList is a toggle. A category change while a
                // submenu is already active closes it, which made all three
                // top-level category buttons appear dead in special modes.
                if (Proc.sprite.menuActionSub != null) {
                    for (int i = 0; i < Proc.sprite.menuActionSub.GetCount(); i++)
                        Proc.sprite.menuActionSub.SetActive(false, i);
                }
                Traverse.Create(Proc.sprite).Method("CreateMotionList", new object[] { category }).GetValue();
                DiagnosticProbe.Info("special-menu forced-category category=" + category + " previous=" + previous);
            } catch (Exception ex) {
                DiagnosticProbe.Warning("special-menu category failed category=" + category + " error=" + ex.Message);
            } finally {
                EndSpecialMenuBuild(Proc.sprite, previous);
            }
            return true;
        }

        internal void AnimationChanged(HSceneProc.AnimationListInfo animation) {
            if (!initialized || IsSwitching || animation == null) return;
            int boundIndex = Proc.flags == teams[0].Flags ? 0 : Proc.flags == teams[1].Flags ? 1 : -1;
            if (boundIndex < 0) return;
            TeamState active = teams[boundIndex];
            HFlag.EMode previousMode = active.Animation == null ? active.Flags.mode : active.Animation.mode;
            active.Animation = animation;
            active.Mode = animation.mode;
            if (IsTwoFemaleMode(animation.mode)) {
                TeamState partner = teams[1 - boundIndex];
                if (!IsTwoFemaleMode(previousMode) && partner != null) {
                    // Houshi can leave its forced wide-open mouth on either
                    // participant. The lesbian action supplies its own face
                    // after the transition; begin it from a neutral mouth.
                    ResetTransitionMouth(active.Female);
                    ResetTransitionMouth(partner.Female);
                }
                if (partner != null && partner.Female != null && partner.Female.objBodyBone != null &&
                    active.Controllers != null && partner.Controllers != null) {
                    active.Controllers.EyeFemale.SetPartner(partner.Female.objBodyBone);
                    partner.Controllers.EyeFemale.SetPartner(active.Female.objBodyBone);
                    DiagnosticProbe.Info("lesbian-look-partners owner=" + (boundIndex + 1));
                }
            } else if (IsTwoFemaleMode(previousMode)) {
                for (int i = 0; i < teams.Length; i++)
                    if (teams[i] != null && teams[i].Controllers != null && teams[i].Male != null)
                        teams[i].Controllers.EyeFemale.SetPartner(teams[i].Male.objBodyBone);
            }
            // ChangeAnimator's postfix runs before every caller reliably updates
            // HFlag.mode.  Leaving it on aibu while a houshi animation is active
            // makes HSceneProc call HAibu.Proc every frame and produces an NRE
            // storm. Keep the cloned context internally consistent here.
            active.Flags.mode = animation.mode;
            active.Flags.nowAnimationInfo = animation;
            // ChangeAnimator has consumed this request. Keeping it non-null makes
            // HSceneProc.Update execute the same transition again next frame.
            active.Flags.selectAnimationListInfo = null;
            // A motion change starts a fresh native action UI. Calls recorded
            // for the outgoing special action must not be replayed over the
            // newly built Aibu/Houshi/Sonyu buttons.
            if (IsAppendedActionMode(previousMode) || IsAppendedActionMode(animation.mode))
                active.ClearModeUiCalls();
            // Secondary controller writes are suppressed during ChangeAnimator,
            // so no expensive post-transition asset reload is needed.
            active.RefreshAibuColliders();
            active.EnsureAibuHitObjects(log);
            RefreshActionBindings(Proc);
            if (boundIndex == ActiveTeam) {
                SetActiveInteractionTeam(active);
                RefreshFocusedModeUi(active);
                if (IsAppendedActionMode(previousMode) &&
                    (animation.mode == HFlag.EMode.houshi || animation.mode == HFlag.EMode.sonyu))
                    ProbeActionBubble(animation.mode, "special-exit");
            }
            bool incomingSpecial = IsAppendedActionMode(animation.mode);
            specialRigActive[boundIndex] = incomingSpecial;
            // Lesbian SetPlay loads and drives both female animators, so both
            // rigs require an explicit native MotionChange when restored.
            if (IsTwoFemaleMode(animation.mode)) specialRigActive[1 - boundIndex] = true;
            if (!IsTwoFemaleMode(animation.mode)) ReleaseDuplicateMaleLookController();
            ApplyVisibility();
            if (animation.mode == HFlag.EMode.masturbation) StartCoroutine(LogMasturbationRig(boundIndex, animation));
            if (incomingSpecial || IsAppendedActionMode(previousMode)) {
                HActionBase action = active.Actions != null && (int)animation.mode < active.Actions.Count ? active.Actions[(int)animation.mode] : null;
                AnimatorStateInfo state = active.Female.getAnimatorStateInfo(0);
                DiagnosticProbe.Info("native-special-transition team=" + (boundIndex + 1) +
                    " from=" + previousMode + " to=" + animation.mode + " action=" + (action == null ? "null" : action.GetType().Name) +
                    " flagState=" + active.Flags.nowAnimStateName + " animatorState=" + state.shortNameHash);
            }
            LogRuntimeState("animation-changed-team-" + (boundIndex + 1));
            ProbeMaleRig(boundIndex, "changed-after-" + animation.mode);
            ProbeMaleRig(1 - boundIndex, "other-after-" + animation.mode);
            StartCoroutine(ProbeMaleRigNextFrame(boundIndex, animation.mode, "changed"));
            StartCoroutine(ProbeMaleRigNextFrame(1 - boundIndex, animation.mode, "other"));
            if (IsThreePMode(previousMode) || IsThreePMode(animation.mode) ||
                animation.mode == HFlag.EMode.houshi || animation.mode == HFlag.EMode.sonyu)
                StartCoroutine(ProbeMaleRigAfterTransition(boundIndex, animation.mode));
        }

        private IEnumerator ProbeMaleRigNextFrame(int index, HFlag.EMode incoming, string label) {
            yield return null;
            ProbeMaleRig(index, label + "-next-frame-" + incoming);
        }

        private IEnumerator ProbeMaleRigAfterTransition(int changedIndex, HFlag.EMode incoming) {
            yield return new WaitForSeconds(1f);
            ProbeMaleRig(changedIndex, "changed-one-second-" + incoming);
            ProbeMaleRig(1 - changedIndex, "other-one-second-" + incoming);
            yield return new WaitForSeconds(4f);
            ProbeMaleRig(changedIndex, "changed-five-seconds-" + incoming);
            ProbeMaleRig(1 - changedIndex, "other-five-seconds-" + incoming);
        }

        private void ProbeMaleRig(int index, string phase) {
            if (index < 0 || index >= teams.Length) return;
            TeamState team = teams[index];
            if (team == null || team.Flags == null ||
                (team.Flags.mode != HFlag.EMode.houshi && team.Flags.mode != HFlag.EMode.sonyu && !IsThreePMode(team.Flags.mode))) return;
            Lookat_dan look = teamLookDans[index];
            ChaControl lookMale = look == null ? null : Traverse.Create(look).Field("male").GetValue<ChaControl>();
            ChaControl[] lookFemales = look == null ? null : Traverse.Create(look).Field("females").GetValue<ChaControl[]>();
            Transform target = look == null ? null : Traverse.Create(look).Field("transLookAtNull").GetValue<Transform>();
            GameObject baseBone = look == null ? null : Traverse.Create(look).Field("objDanBase").GetValue<GameObject>();
            Lookat_dan nativeSecondary = Proc == null ? null : Proc.lookDan1;
            ChaControl secondaryMale = nativeSecondary == null ? null : Traverse.Create(nativeSecondary).Field("male").GetValue<ChaControl>();
            Transform bone = maleGenitalBones[index];
            if (bone == null && team.Male != null) {
                GameObject reference = team.Male.GetReferenceInfo(ChaReference.RefObjKey.a_n_dan);
                bone = reference == null ? null : reference.transform;
                maleGenitalBones[index] = bone;
            }
            Animator maleAnimator = team.Male == null ? null : team.Male.animBody;
            AnimatorStateInfo maleState = maleAnimator == null ? default(AnimatorStateInfo) : maleAnimator.GetCurrentAnimatorStateInfo(0);
            DiagnosticProbe.Info("male-rig-transition " + phase + " team=" + (index + 1) +
                " mode=" + team.Flags.mode + " motion=" + team.Flags.nowAnimStateName +
                " lookEnabled=" + (look != null && look.enabled) +
                " lookMotion=" + (look == null ? "null" : look.strPlayMotion) +
                " lookShapes=" + (look == null || look.lstShape == null ? -1 : look.lstShape.Count) +
                " lookId=" + (look == null ? 0 : look.GetInstanceID()) +
                " activeLookMatch=" + (Proc != null && ReferenceEquals(Proc.lookDan, look)) +
                " secondaryLookMatch=" + ReferenceEquals(nativeSecondary, look) +
                " maleMatch=" + ReferenceEquals(lookMale, team.Male) +
                " femaleMatch=" + (lookFemales != null && lookFemales.Length > 0 && ReferenceEquals(lookFemales[0], team.Female)) +
                " maleRoot=" + (team.Male == null ? "null" : team.Male.transform.position + "/" + team.Male.transform.eulerAngles) +
                " femaleRoot=" + (team.Female == null ? "null" : team.Female.transform.position + "/" + team.Female.transform.eulerAngles) +
                " maleAnim=" + (maleAnimator == null ? "null" : maleState.shortNameHash + "/" +
                    (maleAnimator.runtimeAnimatorController == null ? "null" : maleAnimator.runtimeAnimatorController.name)) +
                " target=" + (target == null ? "null" : target.position.ToString()) +
                " targetOwnDistance=" + (target == null || team.Female == null ? "null" : Vector3.Distance(target.position, team.Female.transform.position).ToString("0.00")) +
                " targetOtherDistance=" + (target == null || teams[1 - index] == null || teams[1 - index].Female == null
                    ? "null" : Vector3.Distance(target.position, teams[1 - index].Female.transform.position).ToString("0.00")) +
                " bone=" + (bone == null ? "null" : bone.position + "/" + bone.localEulerAngles) +
                " baseBone=" + (baseBone == null ? "null" : baseBone.transform.position + "/" +
                    baseBone.transform.localEulerAngles + "/world=" + baseBone.transform.eulerAngles +
                    "/parent=" + (baseBone.transform.parent == null ? "null" : baseBone.transform.parent.name)));
            if (nativeSecondary != null && secondaryMale == team.Male)
                DiagnosticProbe.Info("male-rig-secondary-owner " + phase + " team=" + (index + 1) +
                    " enabled=" + nativeSecondary.enabled +
                    " isDedicated=" + ReferenceEquals(nativeSecondary, look));
        }

        private void ReleaseDuplicateMaleLookController() {
            Lookat_dan secondary = Proc == null ? null : Proc.lookDan1;
            if (secondary == null || ReferenceEquals(secondary, teamLookDans[0]) || ReferenceEquals(secondary, teamLookDans[1])) return;
            ChaControl male = Traverse.Create(secondary).Field("male").GetValue<ChaControl>();
            for (int i = 0; i < teams.Length; i++) {
                if (teams[i] == null || male != teams[i].Male) continue;
                secondary.enabled = false;
                secondary.Release();
                DiagnosticProbe.Info("duplicate-male-look-controller-released team=" + (i + 1));
                return;
            }
        }

        private static void ResetTransitionMouth(ChaControl female) {
            if (female == null) return;
            female.ChangeMouthFixed(false);
            female.ChangeMouthPtn(0, true);
            female.ChangeMouthOpenMax(0f);
            DiagnosticProbe.Info("lesbian-mouth-reset female=" + female.gameObject.name);
        }

        private void EnsureAnimatorStateApplied(int teamIndex) {
            TeamState team = teamIndex >= 0 && teamIndex < teams.Length ? teams[teamIndex] : null;
            if (team == null || team.Female == null || team.Female.animBody == null || team.Flags == null ||
                string.IsNullOrEmpty(team.Flags.nowAnimStateName)) return;
            string expected = team.Flags.nowAnimStateName;
            Animator animator = team.Female.animBody;
            try {
                animator.Update(0f);
                AnimatorStateInfo before = animator.GetCurrentAnimatorStateInfo(0);
                int expectedHash = Animator.StringToHash(expected);
                if (before.shortNameHash == expectedHash) return;

                animator.Rebind();
                int mode = (int)team.Flags.mode;
                HActionBase action = team.Actions != null && mode >= 0 && mode < team.Actions.Count ? team.Actions[mode] : null;
                bool applied = action != null ? action.SetPlay(expected, false) : team.Female.setPlay(expected, 0);
                animator.Update(0f);
                if (IsTwoFemaleMode(team.Flags.mode) && action != null && action.female1 != null && action.female1.animBody != null)
                    action.female1.animBody.Update(0f);
                AnimatorStateInfo after = animator.GetCurrentAnimatorStateInfo(0);
                DiagnosticProbe.Warning("animator-state-reapplied team=" + (teamIndex + 1) + " expected=" + expected +
                    " before=" + before.shortNameHash + " after=" + after.shortNameHash + " expectedHash=" + expectedHash +
                    " hasState=" + animator.HasState(0, expectedHash) + " applied=" + applied +
                    " controller=" + (animator.runtimeAnimatorController == null ? "null" : animator.runtimeAnimatorController.name));
            } catch (Exception ex) {
                DiagnosticProbe.Warning("animator-state reapply failed team=" + (teamIndex + 1) + ": " + ex.Message);
            }
        }

        private IEnumerator LogMasturbationRig(int teamIndex, HSceneProc.AnimationListInfo animation) {
            yield return null;
            TeamState team = teamIndex >= 0 && teamIndex < teams.Length ? teams[teamIndex] : null;
            if (team == null || team.Female == null) yield break;
            Animator animator = team.Female.animBody;
            RuntimeAnimatorController controller = animator == null ? null : animator.runtimeAnimatorController;
            AnimatorStateInfo state = animator == null ? default(AnimatorStateInfo) : animator.GetCurrentAnimatorStateInfo(0);
            DiagnosticProbe.Info("masturbation-rig team=" + (teamIndex + 1) + " action=" + animation.nameAnimation +
                " femalePath=" + (animation.pathFemaleBase == null ? "null" : animation.pathFemaleBase.assetpath + "/" + animation.pathFemaleBase.file) +
                " female=" + team.Female.gameObject.name + " controller=" + (controller == null ? "null" : controller.name) +
                " clips=" + (controller == null || controller.animationClips == null ? -1 : controller.animationClips.Length) +
                " state=" + state.fullPathHash + " normalized=" + state.normalizedTime.ToString("0.000") +
                " root=" + team.Female.transform.position + " maleVisible=" + (team.Male != null && team.Male.visibleAll));
        }

        internal void LogCameraLoad(string method, string cameraName) {
            if (!initialized) return;
            log.LogWarning("camera-load method=" + method + " name=" + cameraName + " activeTeam=" + (ActiveTeam + 1) + " suppressed=" + SuppressCameraLoads);
            LogRuntimeState("camera-load");
        }

        private void LogRuntimeState(string reason) {
            if (!initialized || Proc == null) return;
            CameraControl_Ver2 camera = Proc.flags == null ? null : Proc.flags.ctrlCamera;
            string cameraState = camera == null ? "null" :
                "targetPos=" + camera.TargetPos + " targetObj=" + ObjectName(camera.targetObj) + " base=" + ObjectName(camera.transBase);
            log.LogInfo("runtime-state reason=" + reason + " activeTeam=" + (ActiveTeam + 1) + " procFemale=" + ObjectName(Proc.lstFemale == null || Proc.lstFemale.Count == 0 ? null : Proc.lstFemale[0]) +
                " procMale=" + ObjectName(Proc.male) + " procHand=" + ObjectName(Proc.hand) + " camera{" + cameraState + "}");
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null) continue;
                ChaControl handFemale = Traverse.Create(team.Controllers.Hand).Field("female").GetValue<ChaControl>();
                int touchAreas = 0;
                if (team.Controllers.Hand.nowMES != null && team.Controllers.Hand.nowMES.isTouchAreas != null) {
                    for (int j = 0; j < team.Controllers.Hand.nowMES.isTouchAreas.Length; j++) if (team.Controllers.Hand.nowMES.isTouchAreas[j]) touchAreas++;
                }
                log.LogInfo("runtime-team index=" + (i + 1) + " female=" + ObjectName(team.Female) + " male=" + ObjectName(team.Male) +
                    " handFemale=" + ObjectName(handFemale) + " handEnabled=" + team.Controllers.Hand.enabled +
                    " aibu=" + team.EnabledAibuColliderCount + "/" + team.AibuColliderCount + " mode=" + team.Flags.mode +
                    " pendingAnimation=" + (team.Flags.selectAnimationListInfo != null) + " touchAreas=" + touchAreas +
                    " isAibuSelect=" + team.Flags.isAibuSelect + " lookCfg=" +
                    (i == 0 ? Manager.Config.EtcData.FemaleEyesCamera + "/" + Manager.Config.EtcData.FemaleNeckCamera : Manager.Config.EtcData.FemaleEyesCamera1 + "/" + Manager.Config.EtcData.FemaleNeckCamera1));
            }
        }

        private static string ObjectName(UnityEngine.Object value) { return value == null ? "null" : value.name; }

        private static int LookIndex(int team, bool male) { return team * 2 + (male ? 1 : 0); }
        internal bool GetFollowEyes(int team, bool male) { int index = LookIndex(team, male); return index >= 0 && index < followEyes.Length && followEyes[index]; }
        internal bool GetFollowHead(int team, bool male) { int index = LookIndex(team, male); return index >= 0 && index < followHead.Length && followHead[index]; }
        internal void SetFollowEyes(int team, bool male, bool value) {
            int index = LookIndex(team, male);
            if (index < 0 || index >= followEyes.Length || followEyes[index] == value) return;
            followEyes[index] = value;
            ApplyCameraLookOverrides();
            DiagnosticProbe.Info("look-setting team=" + (team + 1) + " actor=" + (male ? "male" : "female") + " eyes=" + value + " head=" + followHead[index]);
        }
        internal void SetFollowHead(int team, bool male, bool value) {
            int index = LookIndex(team, male);
            if (index < 0 || index >= followHead.Length || followHead[index] == value) return;
            followHead[index] = value;
            ApplyCameraLookOverrides();
            DiagnosticProbe.Info("look-setting team=" + (team + 1) + " actor=" + (male ? "male" : "female") + " eyes=" + followEyes[index] + " head=" + value);
        }
        internal int[] GetHCtrlTargets() {
            // A native two-female action already owns the partner's rig. The
            // partner's independent 1F1M HCtrl pass must not reopen its Aibu
            // widgets or advance a second action while 3P is active.
            if (lesbianOwner >= 0) return new[] { lesbianOwner };
            List<int> result = new List<int>();
            for (int i = 0; i < teams.Length; i++) if (teams[i] != null) result.Add(i);
            return result.ToArray();
        }

        internal bool ContainsFemale(ChaControl female) {
            return initialized && female != null && ((teams[0] != null && teams[0].Female == female) || (teams[1] != null && teams[1].Female == female));
        }

        internal int GetTeamIndex(ChaControl female) {
            if (!initialized || female == null) return -1;
            for (int i = 0; i < teams.Length; i++) if (teams[i] != null && teams[i].Female == female) return i;
            return -1;
        }

        internal List<HActionBase> GetTeamActions(int index) {
            return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Actions : null;
        }

        internal bool IsSecondaryAction(HActionBase action) {
            return initialized && action != null && teams[1] != null && teams[1].Actions != null && teams[1].Actions.Contains(action);
        }

        internal bool IsManagedAction(HActionBase action) {
            return initialized && action != null &&
                ((teams[0] != null && teams[0].Actions != null && teams[0].Actions.Contains(action)) ||
                 (teams[1] != null && teams[1].Actions != null && teams[1].Actions.Contains(action)));
        }

        internal bool IsActiveFemale(ChaControl female) {
            return initialized && teams[ActiveTeam] != null && teams[ActiveTeam].Female == female;
        }

        internal bool TryGetLookSettings(object controller, out bool eyes, out bool head) {
            eyes = false;
            head = false;
            if (!initialized || controller == null) return false;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Controllers == null) continue;
                bool male = ReferenceEquals(controller, team.Controllers.EyeMale);
                if (!ReferenceEquals(controller, team.Controllers.EyeFemale) && !male) continue;
                int lookIndex = LookIndex(i, male);
                eyes = followEyes[lookIndex];
                head = followHead[lookIndex] || (!male && kissHeadOverride[i]);
                return true;
            }
            return false;
        }

        internal void ApplyCameraLookOverrides() {
            if (!initialized || Proc == null || Proc.flags == null || Proc.flags.ctrlCamera == null) return;
            GameObject camera = Proc.flags.ctrlCamera.thisCmaera == null ? null : Proc.flags.ctrlCamera.thisCmaera.gameObject;
            if (camera == null) return;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Controllers == null) continue;
                team.ApplyCameraLook(false, GetFollowEyes(i, false), GetFollowHead(i, false) || kissHeadOverride[i], camera);
                team.ApplyCameraLook(true, GetFollowEyes(i, true), GetFollowHead(i, true), camera);
            }
        }

        internal void SetKissHeadOverride(ChaControl female, bool active) {
            int team = GetTeamIndex(female);
            if (team < 0 || kissHeadOverride[team] == active) return;
            // CyNoVR calls Kiss(false) before its exit tail and the native
            // K_Loop head turn have finished. Releasing the camera-neck pattern
            // at that point makes the head snap to neutral for one frame.
            if (!active && preKissCameras[team] != null && RuntimeModCompat.IsKissBusy(teams[team])) return;
            kissHeadOverride[team] = active;
            if (!active && !GetFollowHead(team, false)) teams[team].ReleaseKissHeadOverride();
            ApplyCameraLookOverrides();
            DiagnosticProbe.Info("kiss-head-override team=" + (team + 1) + " active=" + active);
        }

        internal void PrepareKissCamera(ChaControl female) {
            if (!initialized || female == null || !IsActiveFemale(female) || Proc == null || Proc.flags == null || Proc.flags.ctrlCamera == null || female.objTop == null) return;
            // CyNoVR does not choose the H camera base itself.  Changing the
            // shared CameraControl base here made a kiss on team 2 inherit team
            // 1's saved camera and could permanently overwrite team 1. Leave the
            // native HandCtrl kiss camera untouched; this method is now a probe
            // for the head-turn override only.
            Transform camera = Proc.flags.ctrlCamera.thisCmaera == null ? null : Proc.flags.ctrlCamera.thisCmaera.transform;
            Transform head = female.objHead == null ? null : female.objHead.transform;
            int teamIndex = GetTeamIndex(female);
            ChaControl other = teamIndex < 0 ? null : teams[1 - teamIndex].Female;
            Transform otherHead = other == null || other.objHead == null ? null : other.objHead.transform;
            if (camera != null && head != null && otherHead != null &&
                Vector3.Distance(camera.position, otherHead.position) + 0.5f < Vector3.Distance(camera.position, head.position) &&
                teamIndex >= 0 && teamCameras[teamIndex] != null) {
                teamCameras[teamIndex].Restore();
                DiagnosticProbe.Warning("kiss-camera repaired cross-team ownership team=" + (teamIndex + 1));
            }
            string geometry = camera == null || head == null ? " unavailable" :
                " camera=" + camera.position + " head=" + head.position +
                " faceDot=" + Vector3.Dot(camera.forward, (head.position - camera.position).normalized).ToString("0.000") +
                " headFrontDot=" + Vector3.Dot(head.forward, (camera.position - head.position).normalized).ToString("0.000");
            DiagnosticProbe.Info("kiss-camera female=" + female.gameObject.name + " base=" + female.objTop.name + geometry);
        }

        internal void CapturePreKissCamera(ChaControl female) {
            int team = GetTeamIndex(female);
            if (team < 0 || preKissCameras[team] != null || Proc == null || Proc.flags == null) return;
            teams[team].CaptureNativeFemaleLookBeforeKiss();
            // HandCtrl can load K_Touch's camera before HActionBase.SetPlay.
            // The continuously tracked non-kiss snapshot is therefore the only
            // reliable pre-interaction camera at this point.
            preKissCameras[team] = teamCameras[team] ?? CameraSnapshot.Capture(Proc.flags.ctrlCamera);
            settledKissCameras[team] = null;
            kissCameraMovedByUser[team] = false;
            DiagnosticProbe.Info("kiss-camera captured team=" + (team + 1) + " " + preKissCameras[team].Describe());
        }

        private static bool HasManualCameraInput() {
            return (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) &&
                    (Mathf.Abs(Input.GetAxis("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f) ||
                Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.001f;
        }

        internal void EndKissCamera(ChaControl female, bool restore) {
            int team = GetTeamIndex(female);
            if (team < 0) return;
            CameraSnapshot snapshot = preKissCameras[team];
            if (restore && snapshot != null && team == ActiveTeam && kissCameraMovedByUser[team]) {
                restore = false;
                teamCameras[team] = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
                DiagnosticProbe.Info("kiss-camera end-kept-manual-view team=" + (team + 1));
            }
            preKissCameras[team] = null;
            kissCameraMovedByUser[team] = false;
            if (snapshot != null) kissCameraSettlingUntilFrame[team] = Time.frameCount + 20;
            if (restore && snapshot != null) {
                teamCameras[team] = snapshot;
                settledKissCameras[team] = snapshot;
                // CyNoVR's exit coroutine can finish after focus has moved.
                // Its saved camera belongs to that team, not the shared live
                // camera currently showing the other team.
                if (team == ActiveTeam) {
                    snapshot.Restore();
                    Transform liveBase = Proc == null || Proc.flags == null || Proc.flags.ctrlCamera == null
                        ? null : Proc.flags.ctrlCamera.transBase;
                    DiagnosticProbe.Info("kiss-camera restored team=" + (team + 1) + " " + snapshot.Describe() +
                        " liveBasePos=" + (liveBase == null ? "null" : liveBase.position.ToString()));
                } else {
                    DiagnosticProbe.Info("kiss-camera deferred team=" + (team + 1) + " active=" + (ActiveTeam + 1));
                }
            } else {
                settledKissCameras[team] = null;
            }
        }

        internal void RestoreMotionLookAfterKiss(ChaControl female) {
            int team = GetTeamIndex(female);
            if (team >= 0 && teams[team] != null) teams[team].RestoreNativeFemaleLookAfterKiss();
        }

        internal void CancelNativeKiss(TeamState team) {
            if (team == null || team.Female == null || team.Controllers == null || team.Controllers.Hand == null) return;
            AnimatorStateInfo state = team.Female.getAnimatorStateInfo(0);
            bool kissState = state.IsName("K_Touch") || state.IsName("K_Loop") ||
                string.Equals(team.Flags == null ? null : team.Flags.nowAnimStateName, "K_Touch", StringComparison.Ordinal) ||
                string.Equals(team.Flags == null ? null : team.Flags.nowAnimStateName, "K_Loop", StringComparison.Ordinal);
            bool handKiss = false;
            try { handKiss = Traverse.Create(team.Controllers.Hand).Field("isKiss").GetValue<bool>(); } catch { }
            if (!kissState && !handKiss) return;
            try {
                team.Controllers.Hand.ForceFinish(false);
                try { Traverse.Create(team.Controllers.Hand).Field("isKiss").SetValue(false); } catch { }
                int aibu = (int)HFlag.EMode.aibu;
                if (team.Actions != null && aibu >= 0 && aibu < team.Actions.Count && team.Actions[aibu] != null)
                    team.Actions[aibu].SetPlay("Idle", false);
                if (team.Flags != null) team.Flags.nowAnimStateName = "Idle";
                DiagnosticProbe.Info("native-kiss-cancelled female=" + team.Female.gameObject.name +
                    " state=" + state.shortNameHash + " flagState=" + (team.Flags == null ? "null" : team.Flags.nowAnimStateName));
            } catch (Exception ex) {
                DiagnosticProbe.Warning("native kiss cancellation failed for " + team.Female.gameObject.name + ": " + ex.Message);
            }
        }

        internal bool RecordNativeLook(object controller, int id, GameObject target, bool eyeDisregard, bool neckDisregard) {
            if (!initialized || controller == null || id >= 1000) return false;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Controllers == null) continue;
                if (ReferenceEquals(controller, team.Controllers.EyeFemale)) {
                    team.RecordNativeLook(false, id, target, eyeDisregard, neckDisregard);
                    DiagnosticProbe.OnceInfo("native-look-" + team.Female.GetInstanceID() + "-f-" + id,
                        "native-look team=" + (i + 1) + " actor=female id=" + id + " target=" + ObjectName(target));
                    return true;
                }
                if (ReferenceEquals(controller, team.Controllers.EyeMale)) {
                    team.RecordNativeLook(true, id, target, eyeDisregard, neckDisregard);
                    DiagnosticProbe.OnceInfo("native-look-" + team.Male.GetInstanceID() + "-m-" + id,
                        "native-look team=" + (i + 1) + " actor=male id=" + id + " target=" + ObjectName(target));
                    return true;
                }
            }
            return false;
        }

        internal bool RestoreMotionHeadIfCameraDriven(object controller) {
            if (!initialized || controller == null) return false;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Controllers == null) continue;
                bool male = ReferenceEquals(controller, team.Controllers.EyeMale);
                if (!male && !ReferenceEquals(controller, team.Controllers.EyeFemale)) continue;
                ChaControl actor = male ? team.Male : team.Female;
                if (actor == null || GetFollowHead(i, male) || !male && kissHeadOverride[i]) return true;
                // Motion data can issue a camera-neck pattern even while the
                // panel's head-follow option is off (notably id 310 when
                // Houshi changes to lesbian). Pattern 5 drives the head toward
                // a target; pattern 0 leaves the head to the action animation.
                if (actor.GetLookNeckPtn() == 5) {
                    actor.ChangeLookNeckPtn(0, 0f);
                    DiagnosticProbe.OnceInfo("motion-head-" + actor.GetInstanceID() + "-" + team.Flags.mode,
                        "motion-head-restored team=" + (i + 1) + " actor=" + actor.gameObject.name +
                        " mode=" + team.Flags.mode);
                }
                return true;
            }
            return false;
        }

        internal ChaControl ActiveFemaleObject { get { return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Female : null; } }
        internal ChaControl ActiveMaleObject { get { return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Male : null; } }
        internal HandCtrl ActiveHandObject { get { return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Controllers.Hand : null; } }
        internal HFlag ActiveFlagsObject { get { return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Flags : null; } }
        internal ChaControl GetFemaleObject(int index) { return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Female : null; }
        internal SaveData.Heroine GetHeroineObject(int index) { return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].FemaleData : null; }
        internal ChaControl GetMaleObject(int index) { return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Male : null; }
        internal HandCtrl GetHandObject(int index) { return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Controllers.Hand : null; }
        internal HFlag GetFlagsObject(int index) { return initialized && index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Flags : null; }
        internal HFlag GetExpressionFlagsObject(int index) {
            if (!initialized) return null;
            if (lesbianOwner >= 0 && lesbianOwner < teams.Length && teams[lesbianOwner] != null) return teams[lesbianOwner].Flags;
            return index >= 0 && index < teams.Length && teams[index] != null ? teams[index].Flags : null;
        }
        internal bool IsLesbianActive { get { return initialized && lesbianOwner >= 0; } }
        internal ChaControl ExpressionFemaleObject {
            get {
                return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Female : null;
            }
        }
        internal HFlag ExpressionFlagsObject {
            get {
                return initialized && teams[ActiveTeam] != null ? teams[ActiveTeam].Flags : null;
            }
        }

        internal bool TryGetExpressionBindings(ChaControl female, out FaceListCtrl face, out HandCtrl hand, out HVoiceCtrl.Voice voice, out int voiceIndex) {
            face = null;
            hand = null;
            voice = null;
            voiceIndex = 0;
            if (!initialized || female == null) return false;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Female != female) continue;
                face = team.Controllers.Face;
                hand = team.Controllers.Hand;
                voiceIndex = ExpressionVoiceBinding.FlagVoiceIndex(i, lesbianOwner);
                voice = ExpressionVoiceBinding.Resolve(i, voiceByTeam,
                    Proc == null || Proc.voice == null ? null : Proc.voice.nowVoices, GetBoundTeamIndex());
                return true;
            }
            return false;
        }

        internal bool TryGetTeamBindings(ChaControl female, out ChaControl male, out HFlag flags, out HandCtrl hand) {
            male = null;
            flags = null;
            hand = null;
            if (!initialized || female == null) return false;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Female != female) continue;
                male = team.Male;
                flags = team.Flags;
                hand = team.Controllers == null ? null : team.Controllers.Hand;
                return true;
            }
            return false;
        }

        internal bool TryGetActiveFemaleForHand(HandCtrl hand, out ChaControl female) {
            female = null;
            if (!initialized || hand == null || teams[ActiveTeam] == null || teams[ActiveTeam].Controllers.Hand != hand) return false;
            female = teams[ActiveTeam].Female;
            return female != null;
        }

        internal bool PushControlContext(int index) {
            if (!initialized || index < 0 || index >= teams.Length || teams[index] == null) return false;
            BindLightweightContext(teams[index]);
            return index != ActiveTeam;
        }

        internal int GetBoundTeamIndex() {
            if (!initialized || Proc == null) return -1;
            for (int i = 0; i < teams.Length; i++) if (teams[i] != null && ReferenceEquals(Proc.flags, teams[i].Flags)) return i;
            return -1;
        }

        internal void PopExternalControlContext(int previousIndex) {
            if (!initialized) return;
            int restore = previousIndex >= 0 && previousIndex < teams.Length && teams[previousIndex] != null ? previousIndex : ActiveTeam;
            if (teams[restore] != null) BindLightweightContext(teams[restore]);
        }

        internal bool BeginExternalUiPass(int target) {
            if (!initialized || IsParallelPass || target == ActiveTeam || Proc == null || Proc.sprite == null ||
                teams[ActiveTeam] == null || target < 0 || target >= teams.Length || teams[target] == null) return false;
            TeamState focused = teams[ActiveTeam];
            focused.CaptureModeUi(Proc.sprite);
            focused.CaptureHandUi(Proc.sprite);
            // HCtrl runs outside HSceneProc.Update too. Its background pass
            // must begin with that team's widgets, otherwise its finish-button
            // writes are applied on top of the focused team's bubble state.
            try {
                teams[target].RestoreModeUi(Proc.sprite, false);
                teams[target].RestoreHandUi(Proc.sprite);
                return true;
            } catch {
                // The caller cannot mark its UI scope as entered until we
                // return. Roll back a partially applied snapshot here.
                try { focused.RestoreModeUi(Proc.sprite); }
                finally { focused.RestoreHandUi(Proc.sprite); }
                throw;
            }
        }

        internal void EndExternalUiPass(int target, bool started) {
            if (!started || !initialized || Proc == null || Proc.sprite == null || target < 0 || target >= teams.Length ||
                teams[target] == null || teams[ActiveTeam] == null) return;
            try {
                string change = teams[ActiveTeam].DescribeModeUiDifference();
                if (change != null && Time.realtimeSinceStartup - lastExternalUiWriteLog >= 2f) {
                    lastExternalUiWriteLog = Time.realtimeSinceStartup;
                    DiagnosticProbe.Info("background-external-ui-write team=" + (target + 1) + " " + change);
                }
                teams[target].CaptureModeUi(Proc.sprite);
                teams[target].CaptureHandUi(Proc.sprite);
            } finally {
                try { teams[ActiveTeam].RestoreModeUi(Proc.sprite); }
                finally { teams[ActiveTeam].RestoreHandUi(Proc.sprite); }
            }
        }

        internal static void RestoreSharedCursorLock(bool wasLocked, string source) {
            if (GameCursor.isLock == wasLocked || !Singleton<GameCursor>.IsInstance()) return;
            bool afterBackground = GameCursor.isLock;
            Singleton<GameCursor>.Instance.SetCursorLock(wasLocked);
            DiagnosticProbe.Info("cursor-lock-restored source=" + source +
                " background=" + afterBackground + " focused=" + wasLocked);
        }

        internal void RunParallelUpdate(MethodBase updateMethod) {
            if (updateMethod == null || IsParallelPass) return;
            if (!initialized || IsSwitching) return;
            int backgroundIndex = 1 - ActiveTeam;
            TeamState team = teams[backgroundIndex];
            if (team == null || team.Actions == null || team.Flags == null) return;
            if (lesbianOwner >= 0 || (teams[ActiveTeam] != null && IsTwoFemaleMode(teams[ActiveTeam].Flags.mode)) || IsTwoFemaleMode(team.Flags.mode)) return;
            int mode = (int)team.Flags.mode;
            if (mode < 0 || mode >= team.Actions.Count || team.Actions[mode] == null) return;
            long started = DiagnosticProbe.Start();
            // Describing both action graphs allocates strings every frame.
            // Keep a slow-frame probe without that steady allocation.
            TeamState focus = teams[ActiveTeam];
            CameraSnapshot camera = CameraSnapshot.Capture(Proc.flags == null ? null : Proc.flags.ctrlCamera);
            bool focusedCursorLock = GameCursor.isLock;
            if (focus != null) {
                focus.Capture(focus.Flags);
                focus.CaptureHandUi(Proc.sprite);
                focus.CaptureModeUi(Proc.sprite);
            }
            BackgroundHandUiDirty = false;
            bool restoredBackgroundAnimation = false;
            try {
                BindLightweightContext(team);
                BreastmilkCompat.EnterParallelTeam(this, backgroundIndex);
                // HSprite is a single native object shared by both normal 1F1M
                // action graphs. Swap all of its mode state at the action-frame
                // boundary so direct reads (click pads, finish buttons, condom
                // state) see the owning team rather than the focused team.
                team.RestoreModeUi(Proc.sprite, false);
                team.RestoreHandUi(Proc.sprite);
                IsParallelPass = true;
                SuppressCameraLoads = true;
                if (team.NeedsFullAnimationRestore && team.Animation != null) {
                    team.NeedsFullAnimationRestore = false;
                    if (hasLesbianPartnerPose) {
                        team.Female.transform.SetPositionAndRotation(lesbianPartnerFemalePosition, lesbianPartnerFemaleRotation);
                        team.Male.transform.SetPositionAndRotation(lesbianPartnerMalePosition, lesbianPartnerMaleRotation);
                        hasLesbianPartnerPose = false;
                    }
                    for (int i = 0; i < team.MotionIKs.Count; i++) team.MotionIKs[i].SetPartners(team.MotionIKs);
                    // The partner's physical rig was last driven by the lesbian
                    // action. Native ChangeAnimator compares nowAnimationInfo.mode
                    // with the incoming mode to decide whether to load the base
                    // controller. Its logical team state still describes the old
                    // action, so present the physical previous mode for this one
                    // transition and let the native action initialize its pose.
                    HSceneProc.AnimationListInfo previousInfo = team.Flags.nowAnimationInfo;
                    if (lesbianPartnerRigAnimation != null) team.Flags.nowAnimationInfo = lesbianPartnerRigAnimation;
                    DiagnosticProbe.Info("lesbian-partner-rig-reload team=" + (backgroundIndex + 1) +
                        " physical=" + (lesbianPartnerRigAnimation == null ? "unknown" : lesbianPartnerRigAnimation.mode.ToString()) +
                        " logical=" + (previousInfo == null ? "null" : previousInfo.mode.ToString()) +
                        " incoming=" + team.Animation.mode);
                    Proc.ChangeAnimator(team.Animation, true);
                    lesbianPartnerRigAnimation = null;
                    restoredBackgroundAnimation = true;
                    team.Flags.selectAnimationListInfo = null;
                    team.RestoreCenter();
                    ResetTransitionMouth(team.Female);
                    DiagnosticProbe.Info("lesbian-partner-restored team=" + (backgroundIndex + 1));
                }
                // The background UI has been restored, so its finish preference
                // belongs to this team. Submit a native click before its action
                // runs; the normal Update consumes it and clears it afterward.
                if (MultiTeamHPlugin.AutoBackgroundClimax.Value && Proc.sprite != null && Proc.sprite.IsSpriteAciotn() &&
                    team.Female != null && team.Female.animBody != null) {
                    bool outsideOnly = team.Flags.mode == HFlag.EMode.houshi && Proc.sprite != null &&
                        Proc.sprite.houshi != null && Proc.sprite.houshi.tglAutoFinish != null && Proc.sprite.IsHoushiAutoFinish();
                    BackgroundClimax.TryRequest(team.Flags, team.Female.getAnimatorStateInfo(0),
                        team.Female.animBody.IsInTransition(0), true, backgroundIndex != ActiveTeam,
                        MultiTeamHPlugin.BackgroundFinish.Value, outsideOnly);
                }
                updateMethod.Invoke(Proc, null);
                team.Capture(team.Flags);
                if (team.ClearHandForCurrentState()) MarkBackgroundHandUiDirty();
                // Background Update owns the shared native widgets while it is
                // executing. Snapshot the completed transition before restoring
                // the focused team's widgets, otherwise pending action bubbles
                // and finish buttons are lost when teams overlap transitions.
                team.CaptureModeUi(Proc.sprite);
                team.CaptureHandUi(Proc.sprite);
                double elapsed = DiagnosticProbe.ElapsedMs(started);
                if (elapsed >= 20.0 && Time.realtimeSinceStartup - lastParallelLog >= 2f) {
                    lastParallelLog = Time.realtimeSinceStartup;
                    DiagnosticProbe.Info("parallel-update elapsedMs=" + elapsed.ToString("0.0") +
                        " team=" + (backgroundIndex + 1) + " after={" + DescribeTeamState(team) + "}");
                }
            } catch (Exception ex) {
                if (!team.ExternalProcErrorLogged) {
                    team.ExternalProcErrorLogged = true;
                    Exception detail = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    log.LogError("Parallel HSceneProc.Update failed for team " + (backgroundIndex + 1) + ": " + detail);
                }
            } finally {
                IsParallelPass = false;
                SuppressCameraLoads = false;
                BreastmilkCompat.LeaveParallelTeam(this, backgroundIndex);
                RestoreSharedCursorLock(focusedCursorLock, "parallel-h-scene");
                if (focus != null) {
                    BindLightweightContext(focus);
                    // The full restore above temporarily reuses HSceneProc's
                    // shared action graph. Revalidate the focused actor after
                    // rebinding so it cannot retain the other team's state.
                    if (restoredBackgroundAnimation) EnsureAnimatorStateApplied(ActiveTeam);
                    focus.RestoreModeUi(Proc.sprite);
                    focus.RestoreHandUi(Proc.sprite);
                    SetActiveInteractionTeam(focus);
                    // A background action usually leaves the shared camera
                    // untouched. Replaying its transform every frame fights
                    // CameraControl's manual rotation in Houshi.
                    if (camera.NeedsRestore()) {
                        if (Time.realtimeSinceStartup - lastParallelCameraRestoreLog >= 3f) {
                            lastParallelCameraRestoreLog = Time.realtimeSinceStartup;
                            DiagnosticProbe.Info("parallel-camera-restore focus=" + (ActiveTeam + 1) +
                                " " + camera.DescribeDrift());
                        }
                        camera.Restore();
                    }
                    if (restoredBackgroundAnimation && Proc.flags != null && Proc.flags.ctrlCamera != null)
                        DiagnosticProbe.Info("lesbian-partner-camera-restored focus=" + (ActiveTeam + 1) +
                            " " + camera.Describe() + " liveBasePos=" +
                            (Proc.flags.ctrlCamera.transBase == null ? "null" : Proc.flags.ctrlCamera.transBase.position.ToString()));
                    RestoreActiveAibuCursor(focus);
                }
                BackgroundHandUiDirty = false;
            }
        }

        internal bool AllowFaceWrite(FaceListCtrl controller, ChaControl female) {
            if (!initialized || controller == null || female == null) return true;
            int femaleTeam = GetTeamIndex(female);
            if (femaleTeam < 0) return true;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null || team.Controllers == null || !ReferenceEquals(team.Controllers.Face, controller)) continue;
                bool allow = i == femaleTeam;
                if (!allow) DiagnosticProbe.OnceInfo("face-write-block-" + controller.GetHashCode() + "-" + female.GetInstanceID(),
                    "face-write blocked controllerTeam=" + (i + 1) + " femaleTeam=" + (femaleTeam + 1));
                return allow;
            }
            return true;
        }

        internal void TraceFaceWrite(FaceListCtrl controller, ChaControl female, int faceId, int voiceKind, int action, bool allowed) {
            int team = GetTeamIndex(female);
            if (team < 0) return;
            int controllerTeam = -1;
            for (int i = 0; i < teams.Length; i++) {
                if (teams[i] != null && teams[i].Controllers != null && ReferenceEquals(teams[i].Controllers.Face, controller)) {
                    controllerTeam = i;
                    break;
                }
            }
            string state = controllerTeam + ":" + faceId + ":" + voiceKind + ":" + action + ":" + allowed + ":" + lesbianOwner;
            if (lastFaceWriteProbe[team] == state) return;
            lastFaceWriteProbe[team] = state;
            DiagnosticProbe.Info("face-write femaleTeam=" + (team + 1) + " controllerTeam=" + (controllerTeam + 1) +
                " faceId=" + faceId + " voiceKind=" + voiceKind + " action=" + action +
                " allowed=" + allowed + " lesbianOwner=" + (lesbianOwner + 1));
        }

        private static readonly MethodInfo GetOnMouseAibuColliderMethod = AccessTools.Method(typeof(HandCtrl), "GetOnMouseAibuCollider");
        private static readonly MethodInfo SetIconTextureMethod = AccessTools.Method(typeof(HandCtrl), "SetIconTexture");

        private static void RestoreActiveAibuCursor(TeamState team) {
            if (team == null || team.Flags == null || team.Flags.mode != HFlag.EMode.aibu || team.Controllers == null || team.Controllers.Hand == null) return;
            try {
                object kind = GetOnMouseAibuColliderMethod.Invoke(team.Controllers.Hand, null);
                object[] args = { kind };
                SetIconTextureMethod.Invoke(team.Controllers.Hand, args);
            } catch { }
        }

        private sealed class InactiveHeroinePanel {
            private static readonly MethodInfo SetToggleMethod = AccessTools.Method(typeof(Toggle), "Set", new[] { typeof(bool), typeof(bool) });
            private static readonly FieldInfo ToggleValueField = AccessTools.Field(typeof(Toggle), "m_IsOn");
            private static readonly MethodInfo TogglePlayEffect = AccessTools.Method(typeof(Toggle), "PlayEffect");
            private GameObject root;
            private RawImage portrait;
            private Image femaleGauge;
            private Image maleGauge;
            private Toggle femaleLock;
            private Toggle maleLock;
            private Toggle nativeFemaleLock;
            private Toggle nativeMaleLock;
            private Transform nativeFemaleLockParent;
            private Transform nativeMaleLockParent;
            private Vector3 nativeFemaleLockPosition;
            private Vector3 nativeMaleLockPosition;
            private TeamState focusedTeam;
            private TeamState backgroundTeam;
            private bool focusedFemaleLocked;
            private bool focusedMaleLocked;
            private UnityAction<bool> focusedFemaleLockChanged;
            private UnityAction<bool> focusedMaleLockChanged;
            private UnityAction<bool> backgroundFemaleLockChanged;
            private UnityAction<bool> backgroundMaleLockChanged;
            private GameObject[] menstruation;
            private GameObject[] experience;
            private RawImage nativePortrait;
            private Image nativeFemaleGauge;
            private Image nativeMaleGauge;
            private GameObject[] nativeMenstruation;
            private GameObject[] nativeExperience;
            private HFlag.EMode lastPortraitMode = (HFlag.EMode)(-1);

            internal static InactiveHeroinePanel Create(HSprite sprite, TeamState focused, TeamState background, ManualLogSource log) {
                if (sprite == null || sprite.rawFace == null) {
                    DiagnosticProbe.Warning("inactive-panel missing HSprite/rawFace");
                    return null;
                }
                Image sourceFemaleGauge = sprite.gauge != null && sprite.gauge.Length > 0 && sprite.gauge[0] != null ? sprite.gauge[0].imgBar : null;
                Image sourceMaleGauge = sprite.gauge != null && sprite.gauge.Length > 1 && sprite.gauge[1] != null ? sprite.gauge[1].imgBar : null;
                Canvas canvas = sprite.rawFace.GetComponentInParent<Canvas>();
                if (canvas == null) {
                    DiagnosticProbe.Warning("inactive-panel missing Canvas");
                    return null;
                }
                GameObject clone = new GameObject("MultiTeamH_InactiveHeroinePanel", typeof(RectTransform));
                RectTransform rootRect = clone.GetComponent<RectTransform>();
                rootRect.SetParent(canvas.transform, false);
                rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero; rootRect.offsetMax = Vector2.zero;
                // Keep the copied panel in the native H UI layer. A modal
                // shade is a later Canvas sibling; putting this at the very
                // end drew the copy above that shade.
                PlaceBesideNativePanel(rootRect, canvas.transform, sprite.rawFace.transform,
                    sourceFemaleGauge == null ? null : sourceFemaleGauge.transform);
                List<Transform> gaugeTargets = new List<Transform>();
                if (sourceFemaleGauge != null) gaugeTargets.Add(sourceFemaleGauge.transform);
                if (sourceMaleGauge != null) gaugeTargets.Add(sourceMaleGauge.transform);
                Transform gaugeSourceRoot = FindCommonAncestor(gaugeTargets);
                Transform portraitSourceRoot = FindPortraitVisualRoot(sprite, canvas.transform, gaugeSourceRoot);
                List<Transform> panelSources = new List<Transform> { portraitSourceRoot, gaugeSourceRoot };
                AddCategoryTargets(panelSources, sprite.categoryMenstruation);
                AddCategoryTargets(panelSources, sprite.categoryExperience);
                RectTransform canvasRect = canvas.transform as RectTransform;
                float canvasLeft = canvasRect == null ? -Screen.width * 0.5f : canvasRect.rect.xMin;
                float panelLeft = FindLeftEdge(canvas.transform, gaugeSourceRoot);
                ShiftNativePanel(canvas.transform, panelSources, canvasLeft - panelLeft);
                panelLeft = FindLeftEdge(canvas.transform, gaugeSourceRoot);
                float panelRight = FindRightEdge(canvas.transform, new List<Transform> { gaugeSourceRoot });
                float panelWidth = panelRight - panelLeft;
                // The cloned focused panel is the right-bending half on the
                // left. The native unmirrored panel moves one panel width to
                // the right and displays the background team.
                float mirrorAxis = (panelLeft + panelRight) * 0.5f;
                float toolbarShift = MoveToolbarPastCircle(sprite, canvas.transform, gaugeSourceRoot,
                    portraitSourceRoot, panelLeft + 2f * panelWidth - 12f);
                Transform portraitCloneRoot = CloneVisual(portraitSourceRoot, rootRect);
                Transform gaugeCloneRoot = CloneVisual(gaugeSourceRoot, rootRect);
                InactiveHeroinePanel result = new InactiveHeroinePanel {
                    root = clone,
                    portrait = portraitCloneRoot == null ? null : ResolveCloneComponent<RawImage>(portraitSourceRoot, portraitCloneRoot, sprite.rawFace.transform),
                    femaleGauge = sourceFemaleGauge == null || gaugeCloneRoot == null ? null : ResolveCloneComponent<Image>(gaugeSourceRoot, gaugeCloneRoot, sourceFemaleGauge.transform),
                    maleGauge = sourceMaleGauge == null || gaugeCloneRoot == null ? null : ResolveCloneComponent<Image>(gaugeSourceRoot, gaugeCloneRoot, sourceMaleGauge.transform),
                    menstruation = CloneCategory(sprite.categoryMenstruation, rootRect),
                    experience = CloneCategory(sprite.categoryExperience, rootRect),
                    nativePortrait = sprite.rawFace,
                    nativeFemaleGauge = sourceFemaleGauge,
                    nativeMaleGauge = sourceMaleGauge,
                    nativeMenstruation = GetCategoryObjects(sprite.categoryMenstruation),
                    nativeExperience = GetCategoryObjects(sprite.categoryExperience)
                };
                ReflectClonedVisuals(canvas.transform, mirrorAxis, portraitCloneRoot, gaugeCloneRoot,
                    result.menstruation);
                PlaceStageAtInnerArc(canvas.transform, result.experience, panelLeft + panelWidth * 0.5f);
                ShiftNativePanel(canvas.transform, panelSources, panelWidth);
                DisableInteraction(clone);
                if (sprite.gauge != null && sprite.gauge.Length >= 2) {
                    Toggle sourceFemaleLock = sprite.gauge[0] == null ? null : sprite.gauge[0].tglLock;
                    Toggle sourceMaleLock = sprite.gauge[1] == null ? null : sprite.gauge[1].tglLock;
                    result.femaleLock = sourceFemaleLock == null || gaugeCloneRoot == null ? null : ResolveCloneComponent<Toggle>(gaugeSourceRoot, gaugeCloneRoot, sourceFemaleLock.transform);
                    result.maleLock = sourceMaleLock == null || gaugeCloneRoot == null ? null : ResolveCloneComponent<Toggle>(gaugeSourceRoot, gaugeCloneRoot, sourceMaleLock.transform);
                    result.nativeFemaleLock = sourceFemaleLock;
                    result.nativeMaleLock = sourceMaleLock;
                    if (result.femaleLock != null) result.femaleLock.gameObject.SetActive(true);
                    if (result.maleLock != null) result.maleLock.gameObject.SetActive(true);
                }
                result.nativeFemaleLockParent = result.nativeFemaleLock == null ? null : result.nativeFemaleLock.transform.parent;
                result.nativeMaleLockParent = result.nativeMaleLock == null ? null : result.nativeMaleLock.transform.parent;
                result.nativeFemaleLockPosition = result.nativeFemaleLock == null ? Vector3.zero : result.nativeFemaleLock.transform.position;
                result.nativeMaleLockPosition = result.nativeMaleLock == null ? Vector3.zero : result.nativeMaleLock.transform.position;
                // The visible bounds differ from transform.position after
                // mirroring. Move the native, game-owned toggles to the left
                // focused gauge and the copied toggles to the right gauge.
                PinLockToPanel(result.nativeFemaleLock, rootRect);
                PinLockToPanel(result.nativeMaleLock, rootRect);
                PinLockToPanel(result.femaleLock, rootRect);
                PinLockToPanel(result.maleLock, rootRect);
                SwapLockVisualBounds(canvas.transform, result.nativeFemaleLock, result.femaleLock);
                SwapLockVisualBounds(canvas.transform, result.nativeMaleLock, result.maleLock);
                result.BindLocks();
                result.Refresh(focused, background);
                float focusedGaugeRight = FindRightEdge(canvas.transform, new List<Transform> { gaugeCloneRoot });
                float focusedPortraitRight = FindRightEdge(canvas.transform, new List<Transform> { portraitCloneRoot });
                float focusedFaceLeft = FindLeftEdge(canvas.transform, result.portrait == null ? null : result.portrait.transform);
                float focusedFaceRight = FindRightEdge(canvas.transform, new List<Transform> { result.portrait == null ? null : result.portrait.transform });
                float backgroundGaugeLeft = FindLeftEdge(canvas.transform, gaugeSourceRoot);
                float backgroundPortraitLeft = FindLeftEdge(canvas.transform, portraitSourceRoot);
                DiagnosticProbe.Info("inactive-panel built portrait=" + (result.portrait != null) + " femaleGauge=" + (result.femaleGauge != null) +
                    " maleGauge=" + (result.maleGauge != null) + " femaleLock=" + (result.femaleLock != null) + " maleLock=" + (result.maleLock != null) +
                    " menstruation=" + result.menstruation.Length + " experience=" + result.experience.Length +
                    " left=" + panelLeft.ToString("0.0") + " focusedGaugeRight=" + focusedGaugeRight.ToString("0.0") +
                    " focusedPortraitRight=" + focusedPortraitRight.ToString("0.0") +
                    " focusedFace=" + focusedFaceLeft.ToString("0.0") + ".." + focusedFaceRight.ToString("0.0") +
                    " backgroundGaugeLeft=" + backgroundGaugeLeft.ToString("0.0") +
                    " backgroundPortraitLeft=" + backgroundPortraitLeft.ToString("0.0") +
                    " mirrorAxis=" + mirrorAxis.ToString("0.0") + " panelWidth=" + panelWidth.ToString("0.0") +
                    " period0=" + DescribeBounds(canvas.transform, result.menstruation.Length > 0 ? result.menstruation[0] : null) +
                    " stage0=" + DescribeBounds(canvas.transform, result.experience.Length > 0 ? result.experience[0] : null) +
                    " nativeFemaleLock=" + DescribeBounds(canvas.transform, result.nativeFemaleLock == null ? null : result.nativeFemaleLock.gameObject) +
                    " femaleLock=" + DescribeBounds(canvas.transform, result.femaleLock == null ? null : result.femaleLock.gameObject) +
                    " maleLock=" + DescribeBounds(canvas.transform, result.maleLock == null ? null : result.maleLock.gameObject) +
                    " portraitRoot=" + (portraitSourceRoot == null ? "null" : portraitSourceRoot.name) +
                    " portraitHasMask=" + (portraitSourceRoot != null && (portraitSourceRoot.GetComponent<Mask>() != null || portraitSourceRoot.GetComponent<RectMask2D>() != null)) +
                    " toolbarShift=" + toolbarShift.ToString("0.0"));
                return result;
            }

            private static Transform FindPortraitVisualRoot(HSprite sprite, Transform canvas, Transform gaugeRoot) {
                RawImage portrait = sprite.rawFace;
                Transform fallback = portrait.transform.parent == null ? portrait.transform : portrait.transform.parent;
                Transform current = portrait.transform;
                while (current != null && current != canvas) {
                    bool masked = current.GetComponent<Mask>() != null || current.GetComponent<RectMask2D>() != null;
                    bool containsToolbar = false;
                    if (sprite.menuMain != null) {
                        for (int i = 0; i < sprite.menuMain.GetCount(); i++) {
                            GameObject item = sprite.menuMain.GetObject(i);
                            if (item != null && item.transform.IsChildOf(current)) { containsToolbar = true; break; }
                        }
                    }
                    if (masked && !containsToolbar && (gaugeRoot == null || !gaugeRoot.IsChildOf(current))) return current;
                    current = current.parent;
                }
                DiagnosticProbe.Info("inactive-panel portrait-mask-not-found path=" + GetTransformPath(portrait.transform, canvas));
                return fallback;
            }

            private static string GetTransformPath(Transform value, Transform stop) {
                List<string> parts = new List<string>();
                while (value != null && value != stop) {
                    parts.Add(value.name + "[" + (value.GetComponent<Mask>() != null ? "Mask" : value.GetComponent<RectMask2D>() != null ? "RectMask2D" : "-") + "]");
                    value = value.parent;
                }
                parts.Reverse();
                return string.Join("/", parts.ToArray());
            }

            private static void ReflectClonedVisuals(Transform canvas, float axis, Transform portraitRoot, Transform gaugeRoot,
                GameObject[] menstruation) {
                List<Transform> sources = new List<Transform> { portraitRoot, gaugeRoot };
                AddObjects(sources, menstruation);
                // H-stage art contains readable text. Keep the native left
                // position beside the focused half instead of mirroring it to
                // the center seam.
                HashSet<Transform> visited = new HashSet<Transform>();
                for (int i = 0; i < sources.Count; i++) {
                    Transform source = sources[i];
                    if (source == null || !visited.Add(source)) continue;
                    bool nested = false;
                    for (int j = 0; j < sources.Count; j++) {
                        if (i != j && sources[j] != null && source != sources[j] && source.IsChildOf(sources[j])) { nested = true; break; }
                    }
                    if (nested) continue;
                    if (source == portraitRoot || source == gaugeRoot)
                        ReflectVisual(source, canvas, axis, source == portraitRoot);
                    else ReflectPositionByBounds(source, canvas, axis);
                }
            }

            private static void AddObjects(List<Transform> targets, GameObject[] objects) {
                if (objects == null) return;
                for (int i = 0; i < objects.Length; i++) if (objects[i] != null) targets.Add(objects[i].transform);
            }

            private static void PlaceStageAtInnerArc(Transform canvas, GameObject[] stages, float targetCenter) {
                if (stages == null || stages.Length == 0 || stages[0] == null) return;
                float left, right;
                FindHorizontalEdges(canvas, new List<Transform> { stages[0].transform }, out left, out right);
                float shift = targetCenter - (left + right) * 0.5f;
                ShiftNativePanel(canvas, ToTransforms(stages), shift);
            }

            private static List<Transform> ToTransforms(GameObject[] objects) {
                List<Transform> result = new List<Transform>();
                AddObjects(result, objects);
                return result;
            }

            private static void ReflectPositionByBounds(Transform source, Transform canvas, float axis) {
                float left, right;
                FindHorizontalEdges(canvas, new List<Transform> { source }, out left, out right);
                source.position += canvas.TransformVector(Vector3.right * (2f * axis - left - right));
            }

            private static string DescribeBounds(Transform canvas, GameObject value) {
                if (value == null) return "null";
                float left, right;
                FindHorizontalEdges(canvas, new List<Transform> { value.transform }, out left, out right);
                return left.ToString("0.0") + ".." + right.ToString("0.0");
            }

            private static void ReflectVisual(Transform source, Transform canvas, float axis, bool portraitArt) {
                Vector3 position = canvas.InverseTransformPoint(source.position);
                position.x = 2f * axis - position.x;
                source.position = canvas.TransformPoint(position);
                FlipReadable(source);
                RawImage[] pictures = source.GetComponentsInChildren<RawImage>(true);
                for (int i = 0; i < pictures.Length; i++) {
                    if (pictures[i].transform == source) continue;
                    if (portraitArt) {
                        // Mirror texture coordinates instead of the child's
                        // transform. A negative child scale moved the portrait
                        // outside the native stencil mask.
                        Rect uv = pictures[i].uvRect;
                        pictures[i].uvRect = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);
                    } else FlipReadable(pictures[i].transform);
                }
                Text[] labels = source.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < labels.Length; i++)
                    if (labels[i].transform != source) FlipReadable(labels[i].transform);
                TMP_Text[] tmpLabels = source.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < tmpLabels.Length; i++)
                    if (tmpLabels[i].transform != source) FlipReadable(tmpLabels[i].transform);
            }

            private static float MoveToolbarPastCircle(HSprite sprite, Transform canvas, Transform gaugeRoot,
                Transform portraitRoot, float targetLeft) {
                if (sprite.menuMain == null) return 0f;
                GameObject menuBase = Traverse.Create(sprite).Field("objBaseMainMenu").GetValue<GameObject>();
                GameObject menuOpen = Traverse.Create(sprite).Field("objMainMenuOpen").GetValue<GameObject>();
                List<Transform> items = new List<Transform>();
                for (int i = 0; i < sprite.menuMain.GetCount(); i++) {
                    GameObject item = sprite.menuMain.GetObject(i);
                    if (item == null || item.transform == gaugeRoot || item.transform == portraitRoot ||
                        gaugeRoot != null && gaugeRoot.IsChildOf(item.transform) ||
                        portraitRoot != null && portraitRoot.IsChildOf(item.transform)) continue;
                    items.Add(item.transform);
                }
                if (items.Count == 0) return 0f;
                float left = FindLeftEdge(canvas, items);
                float right = FindRightEdge(canvas, items);
                if (right <= left || left >= targetLeft) return 0f;
                float toolbarBottom, toolbarTop, gaugeBottom, gaugeTop;
                FindVerticalEdges(canvas, items, out toolbarBottom, out toolbarTop);
                FindVerticalEdges(canvas, new List<Transform> { gaugeRoot }, out gaugeBottom, out gaugeTop);
                if (toolbarTop < gaugeBottom || toolbarBottom > gaugeTop) return 0f;
                float amount = targetLeft - left;
                bool safeBase = menuBase != null && menuBase.transform != canvas &&
                    (gaugeRoot == null || !gaugeRoot.IsChildOf(menuBase.transform)) &&
                    (portraitRoot == null || !portraitRoot.IsChildOf(menuBase.transform));
                List<Transform> targets = new List<Transform>();
                if (safeBase) targets.Add(menuBase.transform);
                else {
                    Transform root = FindCommonAncestor(items);
                    if (root != null && root != canvas &&
                        (gaugeRoot == null || !gaugeRoot.IsChildOf(root)) &&
                        (portraitRoot == null || !portraitRoot.IsChildOf(root))) targets.Add(root);
                    else targets.AddRange(items);
                }
                if (menuOpen != null) targets.Add(menuOpen.transform);
                // menuAction/menuActionSub and objMotionListNode belong to the
                // three action buttons at the right edge. They already have
                // their own layout and must stay in their native positions.
                // Only the toolbar's ordinary submenu follows menuMain.
                AddCategoryTargets(targets, sprite.menuSub);
                if (sprite.objMenuSubSubs != null)
                    for (int i = 0; i < sprite.objMenuSubSubs.Length; i++)
                        if (sprite.objMenuSubSubs[i] != null) targets.Add(sprite.objMenuSubSubs[i].transform);
                targets.RemoveAll(delegate(Transform target) {
                    return target == null || target == canvas ||
                        gaugeRoot != null && gaugeRoot.IsChildOf(target) ||
                        portraitRoot != null && portraitRoot.IsChildOf(target);
                });
                ShiftNativePanel(canvas, targets, amount);
                DiagnosticProbe.Info("inactive-panel toolbar base=" + (menuBase == null ? "null" : menuBase.name) +
                    " safeBase=" + safeBase + " open=" + (menuOpen == null ? "null" : menuOpen.name) +
                    " targets=" + targets.Count + " shift=" + amount.ToString("0.0"));
                return amount;
            }

            private static void PlaceBesideNativePanel(RectTransform copy, Transform canvas, params Transform[] originals) {
                int lastNativeSibling = -1;
                for (int i = 0; i < originals.Length; i++) {
                    Transform source = originals[i];
                    while (source != null && source.parent != canvas) source = source.parent;
                    if (source != null && source.parent == canvas)
                        lastNativeSibling = Mathf.Max(lastNativeSibling, source.GetSiblingIndex());
                }
                if (lastNativeSibling >= 0) copy.SetSiblingIndex(lastNativeSibling + 1);
                DiagnosticProbe.Info("inactive-panel canvas sibling=" + copy.GetSiblingIndex() +
                    " nativeLast=" + lastNativeSibling + " canvasChildren=" + canvas.childCount);
            }

            private static void ShiftNativePanel(Transform canvas, List<Transform> sources, float amount) {
                Vector3 offset = canvas.TransformVector(Vector3.right * amount);
                HashSet<Transform> shifted = new HashSet<Transform>();
                for (int i = 0; i < sources.Count; i++) {
                    Transform source = sources[i];
                    if (source == null || !shifted.Add(source)) continue;
                    bool insideAnotherSource = false;
                    for (int j = 0; j < sources.Count; j++) {
                        if (i == j || sources[j] == null || source == sources[j]) continue;
                        if (!source.IsChildOf(sources[j])) continue;
                        insideAnotherSource = true;
                        break;
                    }
                    if (!insideAnotherSource) source.position += offset;
                }
            }

            private static float FindLeftEdge(Transform canvas, Transform source) {
                return FindLeftEdge(canvas, new List<Transform> { source });
            }

            private static float FindLeftEdge(Transform canvas, List<Transform> sources) {
                float left, right;
                FindHorizontalEdges(canvas, sources, out left, out right);
                return left;
            }

            private static float FindRightEdge(Transform canvas, List<Transform> sources) {
                float left, right;
                FindHorizontalEdges(canvas, sources, out left, out right);
                return right;
            }

            private static void FindHorizontalEdges(Transform canvas, List<Transform> sources, out float left, out float right) {
                left = float.PositiveInfinity;
                right = float.NegativeInfinity;
                Vector3[] corners = new Vector3[4];
                for (int i = 0; i < sources.Count; i++) {
                    if (sources[i] == null) continue;
                    Graphic[] graphics = sources[i].GetComponentsInChildren<Graphic>(true);
                    for (int j = 0; j < graphics.Length; j++) {
                        Graphic graphic = graphics[j];
                        if (graphic == null || !graphic.gameObject.activeInHierarchy) continue;
                        RectTransform rect = graphic.rectTransform;
                        if (rect == null) continue;
                        rect.GetWorldCorners(corners);
                        for (int corner = 0; corner < corners.Length; corner++) {
                            float x = canvas.InverseTransformPoint(corners[corner]).x;
                            left = Mathf.Min(left, x);
                            right = Mathf.Max(right, x);
                        }
                    }
                }
                if (float.IsPositiveInfinity(left)) left = 0f;
                if (float.IsNegativeInfinity(right)) right = 0f;
            }

            private static void FindVerticalEdges(Transform canvas, List<Transform> sources, out float bottom, out float top) {
                bottom = float.PositiveInfinity;
                top = float.NegativeInfinity;
                Vector3[] corners = new Vector3[4];
                for (int i = 0; i < sources.Count; i++) {
                    if (sources[i] == null) continue;
                    Graphic[] graphics = sources[i].GetComponentsInChildren<Graphic>(true);
                    for (int j = 0; j < graphics.Length; j++) {
                        Graphic graphic = graphics[j];
                        if (graphic == null || !graphic.gameObject.activeInHierarchy || graphic.rectTransform == null) continue;
                        graphic.rectTransform.GetWorldCorners(corners);
                        for (int corner = 0; corner < corners.Length; corner++) {
                            float y = canvas.InverseTransformPoint(corners[corner]).y;
                            bottom = Mathf.Min(bottom, y);
                            top = Mathf.Max(top, y);
                        }
                    }
                }
                if (float.IsPositiveInfinity(bottom)) bottom = 0f;
                if (float.IsNegativeInfinity(top)) top = 0f;
            }

            private static Transform CloneVisual(Transform source, Transform parent) {
                if (source == null) return null;
                GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, parent, true);
                copy.name = "MultiTeamH_" + source.name;
                copy.transform.position = source.position;
                copy.SetActive(true);
                return copy.transform;
            }

            private static void FlipReadable(Transform value) {
                Vector3 scale = value.localScale;
                scale.x = -scale.x;
                value.localScale = scale;
            }

            private static GameObject[] CloneCategory(HSceneSpriteObjectCategory category, Transform parent) {
                if (category == null) return new GameObject[0];
                GameObject[] result = new GameObject[category.GetCount()];
                for (int i = 0; i < result.Length; i++) {
                    GameObject source = category.GetObject(i);
                    Transform copy = CloneVisual(source == null ? null : source.transform, parent);
                    result[i] = copy == null ? null : copy.gameObject;
                }
                return result;
            }

            private static GameObject[] GetCategoryObjects(HSceneSpriteObjectCategory category) {
                if (category == null) return new GameObject[0];
                GameObject[] result = new GameObject[category.GetCount()];
                for (int i = 0; i < result.Length; i++) result[i] = category.GetObject(i);
                return result;
            }

            private static void DisableInteraction(GameObject root) {
                CanvasGroup[] groups = root.GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < groups.Length; i++) { groups[i].alpha = 1f; groups[i].interactable = false; groups[i].blocksRaycasts = false; }
                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++) graphics[i].raycastTarget = false;
                // Keep Selectable.interactable unchanged so cloned Toggle lock
                // icons retain their normal visual state. CanvasGroup already
                // prevents the duplicate panel from receiving input.
            }

            internal void Refresh(TeamState focused, TeamState background) {
                if (root == null || focused == null || focused.Flags == null || background == null || background.Flags == null) return;
                bool portraitProbe = focused.Flags.mode != lastPortraitMode &&
                    (IsThreePMode(focused.Flags.mode) || IsThreePMode(lastPortraitMode));
                if (portraitProbe) DiagnosticProbe.Info("panel-portrait-before mode=" + focused.Flags.mode +
                    " left=" + DescribePortrait(portrait, focused) +
                    " right=" + DescribePortrait(nativePortrait, background));
                lastPortraitMode = focused.Flags.mode;
                focusedTeam = focused;
                backgroundTeam = background;
                focusedFemaleLocked = focused.Flags.lockGugeFemale;
                focusedMaleLocked = focused.Flags.lockGugeMale;
                if (!root.activeSelf) root.SetActive(true);
                RefreshSide(portrait, femaleGauge, maleGauge, menstruation, experience, focused);
                RefreshSide(nativePortrait, nativeFemaleGauge, nativeMaleGauge, nativeMenstruation, nativeExperience, background);
                if (portraitProbe) DiagnosticProbe.Info("panel-portrait-after mode=" + focused.Flags.mode +
                    " left=" + DescribePortrait(portrait, focused) +
                    " right=" + DescribePortrait(nativePortrait, background));
                SetToggleSilently(nativeFemaleLock, focusedFemaleLocked);
                SetToggleSilently(nativeMaleLock, focusedMaleLocked);
                SetToggleSilently(femaleLock, background.Flags.lockGugeFemale);
                SetToggleSilently(maleLock, background.Flags.lockGugeMale);
            }

            private static string DescribePortrait(RawImage image, TeamState team) {
                if (image == null || team == null) return "missing";
                Texture texture = image.texture;
                return "active=" + image.gameObject.activeInHierarchy +
                    ",texture=" + (texture == null ? "null" : texture.name + ":" + texture.width + "x" + texture.height) +
                    ",teamAlive=" + (team.Portrait != null) +
                    ",teamTexture=" + ReferenceEquals(texture, team.Portrait) +
                    ",color=" + image.color +
                    ",material=" + (image.material == null ? "null" : image.material.name);
            }

            private void BindLocks() {
                EnableCloneLock(femaleLock);
                EnableCloneLock(maleLock);
                focusedFemaleLockChanged = delegate(bool value) {
                    focusedFemaleLocked = value;
                    if (focusedTeam != null && focusedTeam.Flags != null) focusedTeam.Flags.lockGugeFemale = value;
                    DiagnosticProbe.Info("gauge-lock side=focused actor=female value=" + value +
                        " native=" + LockProbe(nativeFemaleLock) + " copy=" + LockProbe(femaleLock));
                };
                focusedMaleLockChanged = delegate(bool value) {
                    focusedMaleLocked = value;
                    if (focusedTeam != null && focusedTeam.Flags != null) focusedTeam.Flags.lockGugeMale = value;
                    DiagnosticProbe.Info("gauge-lock side=focused actor=male value=" + value);
                };
                backgroundFemaleLockChanged = delegate(bool value) {
                    if (backgroundTeam != null && backgroundTeam.Flags != null) backgroundTeam.Flags.lockGugeFemale = value;
                    if (focusedTeam != null && focusedTeam.Flags != null) focusedTeam.Flags.lockGugeFemale = focusedFemaleLocked;
                    DiagnosticProbe.Info("gauge-lock side=background actor=female value=" + value +
                        " focusedPreserved=" + focusedFemaleLocked +
                        " native=" + LockProbe(nativeFemaleLock) + " copy=" + LockProbe(femaleLock));
                };
                backgroundMaleLockChanged = delegate(bool value) {
                    if (backgroundTeam != null && backgroundTeam.Flags != null) backgroundTeam.Flags.lockGugeMale = value;
                    if (focusedTeam != null && focusedTeam.Flags != null) focusedTeam.Flags.lockGugeMale = focusedMaleLocked;
                    DiagnosticProbe.Info("gauge-lock side=background actor=male value=" + value +
                        " focusedPreserved=" + focusedMaleLocked);
                };
                if (nativeFemaleLock != null) nativeFemaleLock.onValueChanged.AddListener(focusedFemaleLockChanged);
                if (nativeMaleLock != null) nativeMaleLock.onValueChanged.AddListener(focusedMaleLockChanged);
                if (femaleLock != null) femaleLock.onValueChanged.AddListener(backgroundFemaleLockChanged);
                if (maleLock != null) maleLock.onValueChanged.AddListener(backgroundMaleLockChanged);
            }

            private static void SwapLockVisualBounds(Transform canvas, Toggle native, Toggle copy) {
                if (canvas == null || native == null || copy == null) return;
                float nativeLeft, nativeRight, copyLeft, copyRight;
                FindHorizontalEdges(canvas, new List<Transform> { native.transform }, out nativeLeft, out nativeRight);
                FindHorizontalEdges(canvas, new List<Transform> { copy.transform }, out copyLeft, out copyRight);
                float delta = (copyLeft + copyRight - nativeLeft - nativeRight) * 0.5f;
                native.transform.position += canvas.TransformVector(Vector3.right * delta);
                copy.transform.position -= canvas.TransformVector(Vector3.right * delta);
            }

            private static void PinLockToPanel(Toggle value, RectTransform panel) {
                if (value == null || panel == null) return;
                value.transform.SetParent(panel, true);
            }

            private string LockProbe(Toggle value) {
                return value == null ? "null" : value.isOn + "@" +
                    DescribeBounds(root == null ? null : root.transform.parent, value.gameObject);
            }

            private static void EnableCloneLock(Toggle value) {
                if (value == null) return;
                value.onValueChanged = new Toggle.ToggleEvent();
                CanvasGroup group = value.GetComponent<CanvasGroup>();
                if (group == null) group = value.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
                group.ignoreParentGroups = true;
                value.interactable = true;
                if (value.targetGraphic != null) value.targetGraphic.raycastTarget = true;
            }

            private static void SetToggleSilently(Toggle toggle, bool value) {
                if (toggle == null || toggle.isOn == value) return;
                if (SetToggleMethod != null) SetToggleMethod.Invoke(toggle, new object[] { value, false });
                else if (ToggleValueField != null) {
                    ToggleValueField.SetValue(toggle, value);
                    if (TogglePlayEffect != null) TogglePlayEffect.Invoke(toggle, new object[] { true });
                }
            }

            private static void RefreshSide(RawImage face, Image female, Image male, GameObject[] period, GameObject[] xp, TeamState team) {
                Texture2D portraitTexture = team.EnsurePortrait();
                if (face != null && face.texture != portraitTexture) face.texture = portraitTexture;
                if (female != null) female.fillAmount = Mathf.Clamp01(team.Flags.gaugeFemale / 100f);
                if (male != null) male.fillAmount = Mathf.Clamp01(team.Flags.gaugeMale / 100f);
                int menstruationIndex = (int)HFlag.GetMenstruation(team.FemaleData.MenstruationDay) == 0 ? 0 : 1;
                SetOnlyActive(period, menstruationIndex);
                SetOnlyActive(xp, (int)team.FemaleData.HExperience);
            }

            internal void Dispose() {
                if (nativeFemaleLock != null && focusedFemaleLockChanged != null) nativeFemaleLock.onValueChanged.RemoveListener(focusedFemaleLockChanged);
                if (nativeMaleLock != null && focusedMaleLockChanged != null) nativeMaleLock.onValueChanged.RemoveListener(focusedMaleLockChanged);
                if (femaleLock != null && backgroundFemaleLockChanged != null) femaleLock.onValueChanged.RemoveListener(backgroundFemaleLockChanged);
                if (maleLock != null && backgroundMaleLockChanged != null) maleLock.onValueChanged.RemoveListener(backgroundMaleLockChanged);
                if (nativeFemaleLock != null && nativeFemaleLockParent != null) {
                    nativeFemaleLock.transform.SetParent(nativeFemaleLockParent, true);
                    nativeFemaleLock.transform.position = nativeFemaleLockPosition;
                }
                if (nativeMaleLock != null && nativeMaleLockParent != null) {
                    nativeMaleLock.transform.SetParent(nativeMaleLockParent, true);
                    nativeMaleLock.transform.position = nativeMaleLockPosition;
                }
                if (root != null) UnityEngine.Object.Destroy(root);
                root = null;
            }

            private static void AddCategoryTargets(List<Transform> targets, HSceneSpriteObjectCategory category) {
                if (category == null) return;
                for (int i = 0; i < category.GetCount(); i++) {
                    GameObject value = category.GetObject(i);
                    if (value != null) targets.Add(value.transform);
                }
            }

            private static void AddCategoryTargets(List<Transform> targets, HSceneSpriteCategory category) {
                if (category == null) return;
                for (int i = 0; i < category.GetCount(); i++) {
                    GameObject value;
                    try {
                        value = category.GetObject(i);
                    } catch (NullReferenceException) {
                        // Some action menus are populated only when opened. Their
                        // category can report a slot before its object exists.
                        DiagnosticProbe.Warning("inactive-panel menu category not ready index=" + i);
                        break;
                    }
                    if (value != null) targets.Add(value.transform);
                }
            }

            private static void AddCategoryTargets(List<Transform> targets, HSceneSpriteToggleCategory category) {
                if (category == null) return;
                for (int i = 0; i < category.GetCount(); i++) {
                    GameObject value = category.GetObject(i);
                    if (value != null) targets.Add(value.transform);
                }
            }

            private static Transform FindCommonAncestor(List<Transform> targets) {
                if (targets == null || targets.Count == 0) return null;
                Transform candidate = targets[0];
                while (candidate != null) {
                    bool containsAll = true;
                    for (int i = 1; i < targets.Count; i++) {
                        if (targets[i] != candidate && !targets[i].IsChildOf(candidate)) { containsAll = false; break; }
                    }
                    if (containsAll) return candidate;
                    candidate = candidate.parent;
                }
                return null;
            }

            private static T ResolveCloneComponent<T>(Transform originalRoot, Transform cloneRoot, Transform target) where T : Component {
                Transform clone = ResolveCloneTransform(originalRoot, cloneRoot, target);
                return clone == null ? null : clone.GetComponent<T>();
            }

            private static GameObject[] ResolveCategory(Transform originalRoot, Transform cloneRoot, HSceneSpriteObjectCategory category) {
                if (category == null) return new GameObject[0];
                GameObject[] result = new GameObject[category.GetCount()];
                for (int i = 0; i < result.Length; i++) {
                    GameObject original = category.GetObject(i);
                    Transform clone = original == null ? null : ResolveCloneTransform(originalRoot, cloneRoot, original.transform);
                    result[i] = clone == null ? null : clone.gameObject;
                }
                return result;
            }

            private static Transform ResolveCloneTransform(Transform originalRoot, Transform cloneRoot, Transform target) {
                if (target == originalRoot) return cloneRoot;
                List<int> path = new List<int>();
                Transform current = target;
                while (current != null && current != originalRoot) { path.Add(current.GetSiblingIndex()); current = current.parent; }
                if (current != originalRoot) return null;
                Transform result = cloneRoot;
                for (int i = path.Count - 1; i >= 0; i--) {
                    if (path[i] < 0 || path[i] >= result.childCount) return null;
                    result = result.GetChild(path[i]);
                }
                return result;
            }

            private static void SetOnlyActive(GameObject[] values, int activeIndex) {
                if (values == null) return;
                for (int i = 0; i < values.Length; i++) if (values[i] != null && values[i].activeSelf != (i == activeIndex)) values[i].SetActive(i == activeIndex);
            }
        }


        private sealed class CameraSnapshot {
            private CameraControl_Ver2 control;
            private Vector3 targetPos;
            private Vector3 angle;
            private Vector3 direction;
            private float fov;
            private Transform targetObject;
            private Vector3 targetObjectPosition;
            private bool restoreTargetObjectPosition;
            private Transform worldBase;
            private Vector3 worldBasePosition;
            private Quaternion worldBaseRotation;
            private bool restoreWorldBasePose;
            private Transform cameraTransform;
            private Vector3 cameraPosition;
            private Quaternion cameraRotation;

            internal static CameraSnapshot Capture(CameraControl_Ver2 value) {
                CameraSnapshot result = new CameraSnapshot { control = value };
                if (value == null) return result;
                result.targetPos = value.TargetPos;
                result.angle = value.CameraAngle;
                result.direction = value.CameraDir;
                result.fov = value.CameraFov;
                result.targetObject = value.targetObj;
                if (result.targetObject != null) {
                    result.targetObjectPosition = result.targetObject.position;
                    result.restoreTargetObjectPosition = result.targetObject.name.StartsWith("CameraTarget", StringComparison.Ordinal);
                }
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

            internal bool NeedsRestore() {
                if (control == null) return false;
                return control.targetObj != targetObject || control.transBase != worldBase ||
                    restoreTargetObjectPosition && targetObject != null &&
                    Vector3.Distance(targetObject.position, targetObjectPosition) > 0.0001f ||
                    Vector3.Distance(control.TargetPos, targetPos) > 0.0001f ||
                    Vector3.Distance(control.CameraAngle, angle) > 0.0001f ||
                    Vector3.Distance(control.CameraDir, direction) > 0.0001f ||
                    Mathf.Abs(control.CameraFov - fov) > 0.0001f ||
                    cameraTransform != null &&
                    (Vector3.Distance(cameraTransform.position, cameraPosition) > 0.0001f ||
                     Quaternion.Angle(cameraTransform.rotation, cameraRotation) > 0.01f) ||
                    restoreWorldBasePose && worldBase != null &&
                    (Vector3.Distance(worldBase.position, worldBasePosition) > 0.0001f ||
                     Quaternion.Angle(worldBase.rotation, worldBaseRotation) > 0.01f);
            }

            internal bool HasVisibleDrift() {
                if (control == null) return false;
                // CameraTarget is also driven by the scene after a team bind.
                // Moving that helper alone does not move the rendered camera;
                // treating it as drift made the post-switch guard pull it
                // back for several frames and visibly fight the scene.
                return control.transBase != worldBase || control.targetObj != targetObject ||
                    Vector3.Distance(control.TargetPos, targetPos) > 0.2f ||
                    Vector3.Distance(control.CameraAngle, angle) > 0.4f ||
                    Vector3.Distance(control.CameraDir, direction) > 0.4f ||
                    cameraTransform != null &&
                    (Vector3.Distance(cameraTransform.position, cameraPosition) > 0.25f ||
                     Quaternion.Angle(cameraTransform.rotation, cameraRotation) > 2f);
            }

            internal string DescribeDrift() {
                if (control == null) return "camera=null";
                return "baseChanged=" + (control.transBase != worldBase) +
                    " targetChanged=" + (control.targetObj != targetObject) +
                    " targetWorldDelta=" + (targetObject == null ? "null" : Vector3.Distance(targetObject.position, targetObjectPosition).ToString("0.000")) +
                    " posDelta=" + Vector3.Distance(control.TargetPos, targetPos).ToString("0.000") +
                    " angleDelta=" + Vector3.Distance(control.CameraAngle, angle).ToString("0.000") +
                    " dirDelta=" + Vector3.Distance(control.CameraDir, direction).ToString("0.000") +
                    " fovDelta=" + Mathf.Abs(control.CameraFov - fov).ToString("0.000") +
                    " worldPosDelta=" + (cameraTransform == null ? "null" : Vector3.Distance(cameraTransform.position, cameraPosition).ToString("0.000")) +
                    " worldAngleDelta=" + (cameraTransform == null ? "null" : Quaternion.Angle(cameraTransform.rotation, cameraRotation).ToString("0.000"));
            }

            internal void Restore(Transform worldBaseOverride = null) {
                if (control == null) return;
                if (worldBaseOverride != null) control.SetWorldBase(worldBaseOverride);
                control.targetObj = targetObject;
                if (worldBaseOverride == null) {
                    control.transBase = worldBase;
                    if (restoreWorldBasePose && worldBase != null)
                        worldBase.SetPositionAndRotation(worldBasePosition, worldBaseRotation);
                }
                if (restoreTargetObjectPosition && targetObject != null) targetObject.position = targetObjectPosition;
                control.TargetPos = targetPos;
                control.CameraAngle = angle;
                control.CameraDir = direction;
                control.CameraFov = fov;
                if (cameraTransform != null) cameraTransform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }

            internal string Describe() {
                return "base=" + (worldBase == null ? "null" : worldBase.name) +
                    " baseId=" + (worldBase == null ? 0 : worldBase.GetInstanceID()) +
                    " basePos=" + (restoreWorldBasePose ? worldBasePosition.ToString() : "actor") +
                    " target=" + (targetObject == null ? "null" : targetObject.name) +
                    " targetId=" + (targetObject == null ? 0 : targetObject.GetInstanceID()) +
                    " targetWorld=" + targetObjectPosition +
                    " targetPos=" + targetPos + " camera=" + cameraPosition;
            }
        }

        internal void AnimationChangeFailed(HSceneProc.AnimationListInfo animation, Exception exception) {
            if (!initialized || animation == null) return;
            int boundIndex = Proc.flags == teams[0].Flags ? 0 : Proc.flags == teams[1].Flags ? 1 : ActiveTeam;
            TeamState team = teams[boundIndex];
            team.Animation = animation;
            team.Mode = animation.mode;
            team.Flags.mode = animation.mode;
            team.Flags.nowAnimationInfo = animation;
            team.Flags.selectAnimationListInfo = null;
            EnsureBaseAnimations(team, animation);
            team.RestoreControllerAssets(log);
            team.EnsureAibuHitObjects(log);
            RefreshActionBindings(Proc);
            try {
                int actionIndex = (int)animation.mode;
                if (actionIndex >= 0 && actionIndex < team.Actions.Count && team.Actions[actionIndex] != null) {
                    team.Actions[actionIndex].MotionChange(0);
                    DiagnosticProbe.Info("recovery MotionChange succeeded team=" + (boundIndex + 1) + " animation=" + animation.nameAnimation);
                }
            } catch (Exception retryException) {
                log.LogError("Recovery MotionChange failed for team " + (boundIndex + 1) + ": " + retryException);
            }
            if (boundIndex == ActiveTeam) {
                RefreshNativeUi(team);
                RefreshFocusedModeUi(team);
                SetActiveInteractionTeam(team);
            }
            DiagnosticProbe.Warning("recovered failed animation change team=" + (boundIndex + 1) + " mode=" + animation.mode + " animation=" + animation.nameAnimation);
        }

        internal string DescribeControlState() {
            if (!initialized) return "session=uninitialized";
            return "active=" + (ActiveTeam + 1) +
                " focus={" + DescribeTeamState(teams[ActiveTeam]) + "} background={" + DescribeTeamState(teams[1 - ActiveTeam]) + "}";
        }

        private static string DescribeTeamState(TeamState team) {
            if (team == null || team.Flags == null) return "null";
            AnimatorStateInfo state = team.Female == null ? default(AnimatorStateInfo) : team.Female.getAnimatorStateInfo(0);
            return "mode=" + team.Flags.mode + ",anim=" + team.Flags.nowAnimStateName + ",state=" + state.shortNameHash +
                ",norm=" + state.normalizedTime.ToString("0.00") + ",speed=" + team.Flags.speed.ToString("0.000") +
                ",speedCalc=" + team.Flags.speedCalc.ToString("0.000") + ",motion=" + team.Flags.motion +
                ",click=" + team.Flags.click + ",finish=" + team.Flags.finish +
                ",voiceWait=" + team.Flags.voiceWait +
                ",voice=" + (team.Flags.voice == null || team.Flags.voice.playVoices == null ? "null" :
                    string.Join("/", Array.ConvertAll(team.Flags.voice.playVoices, value => value.ToString()))) +
                ",gF=" + team.Flags.gaugeFemale.ToString("0.0") + ",gM=" + team.Flags.gaugeMale.ToString("0.0");
        }

        private void BindLightweightContext(TeamState team) {
            TeamControllers c = team.Controllers;
            int teamIndex = GetTeamIndex(team.Female);
            bool lesbianContext = teamIndex == lesbianOwner && team.Animation != null &&
                IsTwoFemaleMode(team.Animation.mode) && lesbianFemales != null && lesbianHeroines != null && lesbianMotionIKs != null;
            if (lesbianContext) DiagnosticProbe.OnceInfo("lesbian-context-rebound-" + teamIndex,
                "lesbian-context-rebound owner=" + (teamIndex + 1) + " femaleSlots=" + lesbianFemales.Count);
            Proc.flags = team.Flags;
            Proc.lstProc = team.Actions;
            Proc.lstFemale = lesbianContext ? lesbianFemales : team.ContextFemales;
            Proc.male = team.Male;
            Proc.male1 = null;
            Proc.flags.lstHeroine = lesbianContext ? lesbianHeroines : team.ContextHeroines;
            Proc.flags.player = team.MaleData;
            Proc.lstMotionIK = lesbianContext ? lesbianMotionIKs : team.MotionIKs;
            Proc.hitreaction = c.HitReaction;
            Proc.hitcollisionFemale = c.HitCollision;
            Proc.hand = c.Hand;
            Proc.yure = c.Yure;
            Proc.siru = c.Siru;
            Proc.face = c.Face;
            Proc.particle = c.Particle;
            Proc.eyeneckFemale = c.EyeFemale;
            Proc.eyeneckMale = c.EyeMale;
            Proc.dynamicCtrl = c.Dynamic;
            Proc.alCtrl = c.AnimatorLayer;
            Proc.parentObjectBaseFemale = c.ParentBaseFemale;
            Proc.parentObjectFemale = c.ParentFemale;
            Proc.parentObjectBaseMale = c.ParentBaseMale;
            Proc.parentObjectMale = c.ParentMale;
            Proc.meta = c.Meta;
            Proc.lookDan = teamLookDans[teamIndex];
            BindSecondaryControllerSlots(team);
            if (lesbianContext) {
                TeamControllers partner = teams[1 - teamIndex].Controllers;
                Proc.hitreaction1 = partner.HitReaction;
                Proc.hitcollisionFemale1 = partner.HitCollision;
                Proc.hand1 = partner.Hand;
                Proc.yure1 = partner.Yure;
                Proc.siru1 = partner.Siru;
                Proc.face1 = partner.Face;
                Proc.particle1 = partner.Particle;
                Proc.eyeneckFemale1 = partner.EyeFemale;
                Proc.eyeneckMale1 = partner.EyeMale;
                Proc.dynamicCtrl1 = partner.Dynamic;
                Proc.alCtrl1 = partner.AnimatorLayer;
                Proc.parentObjectBaseFemale1 = partner.ParentBaseFemale;
                Proc.parentObjectFemale1 = partner.ParentFemale;
            }
            Proc.sprite.hand = c.Hand;
            Proc.sprite.females = Proc.lstFemale;
            Proc.sprite.male = team.Male;
            Proc.sprite.heroine = team.FemaleData;
            Traverse.Create(Proc.sprite).Field("flags").SetValue(team.Flags);
            Traverse.Create(Proc.voice).Field("flags").SetValue(team.Flags);
            Proc.voice.hand = c.Hand;
            if (lesbianContext) BindVoiceSlots(team, teams[1 - teamIndex]);
            else BindVoiceSlots(team);
            c.Hand.flags = team.Flags;
            c.Hand.sprite = Proc.sprite;
            c.Hand.voice = Proc.voice;
            // HActionBase instances cache every actor/controller reference.
            // Merely swapping HSceneProc.lstProc leaves a background action
            // pointing at the previously focused pair, which makes team 2's
            // SetPlay animate team 1 and corrupts both camera and expressions.
            RefreshActionBindings(Proc, lesbianContext ? team.Animation.mode : (HFlag.EMode?)null);
            HSceneOptionsCompat.Bind(Proc, team, teamIndex);
        }

        private sealed class SpecialMotionClickRelay : MonoBehaviour, IPointerClickHandler {
            private TeamSession owner;
            private HSprite sprite;
            private HSceneProc.AnimationListInfo info;
            private bool forceTransition;

            internal void Bind(TeamSession value, HSprite targetSprite, HSceneProc.AnimationListInfo targetInfo, bool force) {
                owner = value;
                sprite = targetSprite;
                info = targetInfo;
                forceTransition = force;
            }

            public void OnPointerClick(PointerEventData eventData) {
                if (owner == null || sprite == null || info == null) return;
                DiagnosticProbe.Info("special-motion-relay name=" + info.nameAnimation + " team=" + (owner.ActiveTeam + 1));
                if (forceTransition || owner.specialRigActive[owner.ActiveTeam])
                    owner.StartSpecialMotionTransition(owner.ActiveTeam, info,
                        owner.teams[owner.ActiveTeam] == null || owner.teams[owner.ActiveTeam].Animation == null
                            ? HFlag.EMode.aibu : owner.teams[owner.ActiveTeam].Animation.mode);
                else ForceSpecialMotionSelection(sprite, info);
            }
        }

        private static void NotifyExpressionControlCharactersChanged() {
            try {
                Type plugin = Type.GetType("ExpressionControl.ExpressionControlPlugin, KK_ExpressionControl", false);
                if (plugin == null) return;
                object instance = Traverse.Create(plugin).Field("inst").GetValue();
                if (instance != null) Traverse.Create(instance).Method("UpdateFemale").GetValue();
            } catch { }
        }

        private void ClearUiFocusAndRestoreCamera() {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (Proc != null && Proc.flags != null && Proc.flags.ctrlCamera != null) GlobalMethod.SetCameraMoveFlag(Proc.flags.ctrlCamera, true);
        }

        private void UpdateStatus() { Status = "当前控制队伍 " + (ActiveTeam + 1) + "（点击头像或按 Tab 切换）"; }

        internal void FocusCameraOnActiveTeam() {
            if (!initialized || IsSwitching || Proc == null || Proc.flags == null) return;
            TeamState team = teams[ActiveTeam];
            if (team == null || team.Female == null || team.Female.objTop == null ||
                team.Flags == null || team.Flags.ctrlCamera == null) return;
            HSceneProc.AnimationListInfo animation = team.Animation ?? team.Flags.nowAnimationInfo;
            string cameraName = animation == null || animation.paramFemale == null ? null : animation.paramFemale.nameCamera;
            if (animation != null && animation.mode == HFlag.EMode.houshi && !string.IsNullOrEmpty(animation.nameCameraIdle))
                cameraName = animation.nameCameraIdle;
            if (string.IsNullOrEmpty(cameraName)) {
                Status = "当前动作没有可用的原生镜头预设";
                return;
            }
            try {
                EndKissCamera(team.Female, false);
                CameraControl_Ver2 camera = team.Flags.ctrlCamera;
                camera.SetWorldBase(team.Female.objTop.transform);
                GlobalMethod.LoadCamera(camera, "h/list/", cameraName, false);
                teamCameras[ActiveTeam] = CameraSnapshot.Capture(camera);
                Status = "镜头已定位到当前队伍 " + (ActiveTeam + 1);
                DiagnosticProbe.Info("focus-camera team=" + (ActiveTeam + 1) + " preset=" + cameraName);
            } catch (Exception ex) {
                Status = "镜头定位失败，请查看日志";
                log.LogWarning("Could not focus camera on team " + (ActiveTeam + 1) + ": " + ex);
            }
        }

        internal void MoveActive(Vector3 delta) {
            if (!initialized || IsSwitching) return;
            teams[ActiveTeam].Move(delta);
            teams[ActiveTeam].Offset += delta;
            if (Proc.objMoveAxis != null) Proc.objMoveAxis.transform.position += delta;
        }

        internal void ApplyVisibility() {
            if (!initialized) return;
            for (int i = 0; i < teams.Length; i++) {
                TeamState team = teams[i];
                if (team == null) continue;
                bool visible = MultiTeamHPlugin.ShowInactiveTeam.Value || i == ActiveTeam ||
                    (lesbianOwner >= 0 && i == 1 - lesbianOwner);
                if (team.Female != null && team.Female.gameObject.activeSelf != visible) team.Female.gameObject.SetActive(visible);
                // Native lesbian mode already handles the owner's man. The
                // other team's man is outside that native visibility path.
                bool maleVisible = visible && (lesbianOwner < 0 || i == lesbianOwner);
                if (team.Male != null && team.Male.gameObject.activeSelf != maleVisible) team.Male.gameObject.SetActive(maleVisible);
            }
        }

        internal void Shutdown(bool stopCoroutines = true) {
            if (shuttingDown) return;
            shuttingDown = true;
            try { BreastmilkCompat.SessionEnded(this); }
            catch (Exception ex) { if (log != null) log.LogWarning("Could not restore Breastmilk state on exit: " + ex.Message); }
            if (stopCoroutines) StopAllCoroutines();
            IsSwitching = false;
            ExpressionControlCompat.SessionEnded(this);
            Sessions.Remove(this);
            RestoreOriginalContextForExit();
            HSceneOptionsCompat.Reset();
            if (Sessions.Count == 0) RuntimeModCompat.SessionEnded();
            if (avatarButton != null) avatarButton.onClick.RemoveListener(SwitchFromAvatar);
            if (addedAvatarButton && avatarButton != null) Destroy(avatarButton);
            if (parallelFlagObject != null) Destroy(parallelFlagObject);
            if (inactiveHeroinePanel != null) { inactiveHeroinePanel.Dispose(); inactiveHeroinePanel = null; }
            initialized = false;
            if (!initializationCompleted) {
                // A previous session can finish while the next selection is
                // already pending. It must never clear the newer launch.
                MultiTeamSelection.Complete(launchRevision);
                RemoveFailedCharacter(createdFemale);
                RemoveFailedCharacter(createdMale);
            }
        }

        private void RemoveFailedCharacter(ChaControl character) {
            if (character == null) return;
            try {
                if (!Singleton<Manager.Character>.IsInstance() ||
                    !Singleton<Manager.Character>.Instance.DeleteChara(character)) Destroy(character.gameObject);
            } catch (Exception ex) {
                if (log != null) log.LogWarning("Could not remove a partially created MultiTeamH character: " + ex.Message);
                Destroy(character.gameObject);
            }
        }

        private void RestoreOriginalContextForExit() {
            TeamState first = teams[0];
            if (Proc == null || first == null || first.Flags == null) return;
            try {
                TeamControllers c = first.Controllers;
                Proc.flags = first.Flags;
                Proc.lstProc = first.Actions;
                Proc.lstFemale = new List<ChaControl> { first.Female };
                Proc.male = first.Male;
                Proc.male1 = null;
                Proc.lstMotionIK = first.MotionIKs;
                Proc.hitreaction = c.HitReaction;
                Proc.hitcollisionFemale = c.HitCollision;
                Proc.hand = c.Hand;
                Proc.yure = c.Yure;
                Proc.siru = c.Siru;
                Proc.face = c.Face;
                Proc.particle = c.Particle;
                Proc.eyeneckFemale = c.EyeFemale;
                Proc.eyeneckMale = c.EyeMale;
                Proc.dynamicCtrl = c.Dynamic;
                Proc.alCtrl = c.AnimatorLayer;
                Proc.parentObjectBaseFemale = c.ParentBaseFemale;
                Proc.parentObjectFemale = c.ParentFemale;
                Proc.parentObjectBaseMale = c.ParentBaseMale;
                Proc.parentObjectMale = c.ParentMale;
                Proc.meta = c.Meta;
                Proc.lookDan = teamLookDans[0];
                if (Proc.sprite != null) {
                    Proc.sprite.hand = c.Hand;
                    Proc.sprite.females = Proc.lstFemale;
                    Proc.sprite.male = first.Male;
                    Proc.sprite.heroine = first.FemaleData;
                    Traverse.Create(Proc.sprite).Field("flags").SetValue(first.Flags);
                }
                if (Proc.voice != null) Traverse.Create(Proc.voice).Field("flags").SetValue(first.Flags);
                if (Proc.voice != null) Proc.voice.hand = c.Hand;
                HSceneOptionsCompat.Bind(Proc, first, 0);
            } catch (Exception ex) {
                if (log != null) log.LogWarning("Could not fully restore the original H context during scene exit: " + ex.Message);
            }
        }

        private void OnDestroy() {
            Shutdown();
            if (MultiTeamHPlugin.Instance != null) MultiTeamHPlugin.Instance.SessionEnded(this);
        }

        internal static void NotifySceneDestroyed(HSceneProc proc) {
            TeamSession[] snapshot = Sessions.ToArray();
            for (int i = 0; i < snapshot.Length; i++) {
                if (snapshot[i] == null || snapshot[i].Proc != proc) continue;
                snapshot[i].Shutdown();
                if (MultiTeamHPlugin.Instance != null) MultiTeamHPlugin.Instance.SessionEnded(snapshot[i]);
                Destroy(snapshot[i]);
            }
        }
    }

        internal sealed class TeamState {
        internal readonly ChaControl Female;
        internal readonly ChaControl Male;
        internal readonly SaveData.Heroine FemaleData;
        internal readonly SaveData.Player MaleData;
        internal readonly List<MotionIK> MotionIKs;
        internal readonly List<ChaControl> ContextFemales;
        internal readonly List<ChaControl> ContextMales;
        internal readonly ChaControl[] ContextFemaleArray;
        internal readonly List<SaveData.Heroine> ContextHeroines;
        internal readonly bool IsClone;
        internal readonly TeamControllers Controllers;
        internal Texture2D Portrait;
        internal bool ContinueWhileInactive = true;
        internal float PlaybackSpeed = 1f;
        internal HFlag Flags;
        internal List<HActionBase> Actions;
        private readonly Animator[] animators;
        private float appliedAnimatorSpeed = float.NaN;
        internal HSceneProc.AnimationListInfo Animation;
        internal HFlag.EMode Mode;
        internal float GaugeFemale;
        internal float GaugeMale;
        internal float Speed;
        internal float Motion;
        internal HFlag.ClickKind Click;
        internal HFlag.FinishKind Finish;
        internal HSceneProc.AnimationListInfo PendingAnimation;
        internal Vector3 Center;
        internal Vector3 Offset;
        internal Vector3 GuidePosition;
        internal Vector3 GuideRotation;
        private bool[] handIconEnabled;
        private bool[] handIconActive;
        private Texture[] handIconTextures;
        private Color[] handIconColors;
        private Collider[] aibuColliders;
        private ModeUiSnapshot modeUi;
        private string lastHandClearKey;
        private readonly LookState femaleLook = new LookState();
        private readonly LookState maleLook = new LookState();
        private readonly List<ModeUiCall> modeUiCalls = new List<ModeUiCall>();
        internal bool ExternalProcErrorLogged;
        internal bool NeedsFullAnimationRestore;
        internal int AibuColliderCount { get { return aibuColliders == null ? 0 : aibuColliders.Length; } }
        internal int EnabledAibuColliderCount {
            get {
                if (aibuColliders == null) return 0;
                int count = 0;
                for (int i = 0; i < aibuColliders.Length; i++) if (aibuColliders[i] != null && aibuColliders[i].enabled) count++;
                return count;
            }
        }

        internal TeamState(ChaControl female, ChaControl male, SaveData.Heroine femaleData, SaveData.Player maleData, List<MotionIK> motionIKs, HFlag flags, List<HActionBase> actions, bool isClone, TeamControllers controllers) {
            Female = female;
            Male = male;
            FemaleData = femaleData;
            MaleData = maleData;
            MotionIKs = motionIKs;
            ContextFemales = new List<ChaControl> { female };
            ContextMales = new List<ChaControl> { male };
            ContextFemaleArray = new[] { female };
            ContextHeroines = new List<SaveData.Heroine> { femaleData };
            IsClone = isClone;
            Controllers = controllers;
            Flags = flags;
            Actions = actions;
            EnsurePortrait();
            List<Animator> animatorList = new List<Animator>();
            AddMainAnimator(animatorList, female);
            AddMainAnimator(animatorList, male);
            animators = animatorList.ToArray();
            CacheAibuColliders();
            Capture(flags);
        }

        internal Texture2D EnsurePortrait() {
            if (Portrait != null) return Portrait;
            Portrait = new Texture2D(240, 320);
            byte[] png = FemaleData == null || FemaleData.charFile == null ? null : FemaleData.charFile.facePngData;
            bool loaded = png != null && png.Length != 0 && Portrait.LoadImage(png);
            DiagnosticProbe.Info("team-portrait-created female=" + (Female == null ? "null" : Female.gameObject.name) +
                " pngBytes=" + (png == null ? 0 : png.Length) + " loaded=" + loaded +
                " texture=" + Portrait.width + "x" + Portrait.height);
            return Portrait;
        }

        private void CacheAibuColliders() {
            if (Female == null) {
                aibuColliders = new Collider[0];
                return;
            }
            Collider[] all = Female.GetComponentsInChildren<Collider>(true);
            List<Collider> result = new List<Collider>();
            for (int i = 0; i < all.Length; i++) {
                Collider collider = all[i];
                if (collider != null && collider.tag != null && collider.tag.Contains("H/Aibu/Hit/")) result.Add(collider);
            }
            aibuColliders = result.ToArray();
        }

        internal void SetAibuCollidersEnabled(bool enabled) {
            if (aibuColliders == null || aibuColliders.Length == 0) CacheAibuColliders();
            for (int i = 0; i < aibuColliders.Length; i++) if (aibuColliders[i] != null) aibuColliders[i].enabled = enabled;
        }

        internal void RestoreControllerAssets(ManualLogSource log) {
            if (Animation == null || Controllers == null) return;
            try {
                HSceneProc.FemaleParameter p = Animation.paramFemale;
                Controllers.Hand.LoadMotionEnableState(p.fileAibuEnableMotion);
                Controllers.Hand.LoadReactionList(p.fileReaction);
                if (!string.IsNullOrEmpty(Flags.nowAnimStateName)) Controllers.Hand.SetAnimation(Flags.nowAnimStateName);
                Controllers.Yure.LoadAllExcel("h/list/", p.fileYure);
                Controllers.EyeFemale.Load("h/list/", p.fileMotionNeck);
                if (Animation.paramMale != null) Controllers.EyeMale.Load("h/list/", Animation.paramMale.fileMotionNeck);
                if (p.path != null) Controllers.AnimatorLayer.Load("h/list/", p.path.file);
                if (p.isHitObject) {
                    Female.LoadHitObject();
                    Controllers.HitCollision.Init(Female, Female.objHitHead);
                    Controllers.HitCollision.LoadText("h/list/", p.fileHit);
                } else {
                    Female.ReleaseHitObject();
                    Controllers.HitCollision.Release();
                }
                // LoadHitObject and animation changes can create additional Aibu
                // colliders after the secondary HandCtrl was first initialized.
                // Refresh both the hand raycast whitelist and our enable mask.
                RefreshAibuColliders();
            } catch (Exception ex) {
                log.LogWarning("Could not restore team controller assets: " + ex);
            }
        }

        internal void RestoreHandMotionState(ManualLogSource log) {
            if (Controllers == null || Controllers.Hand == null || Flags == null || string.IsNullOrEmpty(Flags.nowAnimStateName)) return;
            try {
                Controllers.Hand.SetAnimation(Flags.nowAnimStateName);
                int enabledAreas = CountTouchAreas();
                if (enabledAreas == 0 && Flags.mode == HFlag.EMode.aibu && Animation != null && Animation.paramFemale != null) {
                    Controllers.Hand.LoadMotionEnableState(Animation.paramFemale.fileAibuEnableMotion);
                    Controllers.Hand.LoadReactionList(Animation.paramFemale.fileReaction);
                    Controllers.Hand.SetAnimation(Flags.nowAnimStateName);
                    enabledAreas = CountTouchAreas();
                    log.LogInfo("Restored hand motion state for " + Female.gameObject.name + ": animation=" + Flags.nowAnimStateName + " touchAreas=" + enabledAreas);
                }
            } catch (Exception ex) {
                log.LogWarning("Could not restore hand motion state for " + Female.gameObject.name + ": " + ex.Message);
            }
        }

        private int CountTouchAreas() {
            if (Controllers.Hand.nowMES == null || Controllers.Hand.nowMES.isTouchAreas == null) return 0;
            int count = 0;
            for (int i = 0; i < Controllers.Hand.nowMES.isTouchAreas.Length; i++) if (Controllers.Hand.nowMES.isTouchAreas[i]) count++;
            return count;
        }

        internal void RefreshAibuColliders() {
            CacheAibuColliders();
            if (Controllers != null && Controllers.Hand != null) Controllers.Hand.InitHitReactionCollider();
        }

        internal void EnsureAibuHitObjects(ManualLogSource log) {
            if (Female == null || Controllers == null) return;
            bool left = false;
            bool right = false;
            Collider[] current = Female.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < current.Length; i++) {
                string tag;
                try { tag = current[i].tag; } catch { continue; }
                if (tag == "H/Aibu/Hit/muneL") left = true;
                if (tag == "H/Aibu/Hit/muneR") right = true;
            }
            if (left && right) return;
            try {
                if (Animation != null && Animation.paramFemale != null && Animation.paramFemale.isHitObject) {
                    Female.LoadHitObject();
                    Controllers.HitCollision.Init(Female, Female.objHitHead);
                    Controllers.HitCollision.LoadText("h/list/", Animation.paramFemale.fileHit);
                }
                EnsureBreastCollider("L", "H/Aibu/Hit/muneL");
                EnsureBreastCollider("R", "H/Aibu/Hit/muneR");
                RefreshAibuColliders();
                log.LogInfo("Loaded missing native Aibu hit objects for " + Female.gameObject.name + ": colliders=" + AibuColliderCount);
            } catch (Exception ex) {
                log.LogWarning("Could not load native Aibu hit objects for " + Female.gameObject.name + ": " + ex.Message);
            }
        }

        private void EnsureBreastCollider(string side, string tag) {
            Collider[] colliders = Female.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) {
                try { if (colliders[i].tag == tag) return; } catch { }
            }
            string[] candidates = { "cf_j_bust02_" + side, "cf_j_bust01_" + side, "cf_s_bust00_" + side };
            Transform parent = null;
            Transform[] transforms = Female.GetComponentsInChildren<Transform>(true);
            for (int c = 0; c < candidates.Length && parent == null; c++) {
                for (int i = 0; i < transforms.Length; i++) if (transforms[i].name == candidates[c]) { parent = transforms[i]; break; }
            }
            if (parent == null) return;
            GameObject hit = new GameObject("MultiTeamH_aibu_hit_mune" + side);
            hit.layer = 10;
            hit.tag = tag;
            hit.transform.SetParent(parent, false);
            hit.transform.localPosition = Vector3.zero;
            hit.transform.localRotation = Quaternion.identity;
            SphereCollider sphere = hit.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.16f;
        }

        internal void CaptureModeUi(HSprite sprite) {
            if (sprite == null) return;
            if (modeUi == null || modeUi.Sprite != sprite) modeUi = new ModeUiSnapshot(sprite);
            modeUi.Capture(Mode);
        }

        internal void RecordModeUiCall(MethodBase method, object[] args) {
            if (method == null) return;
            if (method.Name == "MainSpriteChange") {
                modeUiCalls.Clear();
                return;
            }
            string key = method.Name;
            if ((method.Name == "SetHoushiActionButtonActive" || method.Name == "SetSonyuActionButtonActive") && args != null && args.Length > 1)
                key += ":" + args[1];
            for (int i = modeUiCalls.Count - 1; i >= 0; i--) {
                if (modeUiCalls[i].Key != key) continue;
                modeUiCalls.RemoveAt(i);
                break;
            }
            modeUiCalls.Add(new ModeUiCall(key, method, args));
            if (modeUiCalls.Count > 48) modeUiCalls.RemoveAt(0);
            DiagnosticProbe.OnceInfo("mode-ui-" + Female.GetInstanceID() + "-" + key + "-" + ModeUiCall.DescribeArgs(args),
                "mode-ui teamFemale=" + Female.gameObject.name + " call=" + key + " args=" + ModeUiCall.DescribeArgs(args));
        }

        internal void ClearModeUiCalls() { modeUiCalls.Clear(); }

        internal void ReplayModeUi(HSprite sprite) {
            if (sprite == null) return;
            for (int i = 0; i < modeUiCalls.Count; i++) modeUiCalls[i].Invoke(sprite);
        }

        internal bool RestoreModeUi(HSprite sprite, bool visible = true) {
            if (modeUi == null || sprite == null || modeUi.Sprite != sprite) return false;
            modeUi.Restore(visible);
            return true;
        }

        internal string DescribeModeUiDifference() { return modeUi == null ? null : modeUi.FirstDifference(); }

        internal string DescribeAibuColliders(Camera camera) {
            if (aibuColliders == null) return "aibu-colliders null";
            List<string> result = new List<string>();
            for (int i = 0; i < aibuColliders.Length; i++) {
                Collider c = aibuColliders[i];
                if (c == null) continue;
                Vector3 screen = camera == null ? Vector3.zero : camera.WorldToScreenPoint(c.bounds.center);
                result.Add(c.name + "{tag=" + c.tag + ",layer=" + c.gameObject.layer + ",enabled=" + c.enabled +
                    ",trigger=" + c.isTrigger + ",center=" + c.bounds.center + ",size=" + c.bounds.size + ",screen=" + screen + "}");
            }
            return "aibu-colliders female=" + Female.gameObject.name + " count=" + result.Count + " [" + string.Join(";", result.ToArray()) + "]";
        }

        internal void CaptureHandUi(HSprite sprite) {
            if (sprite == null || sprite.commonAibuIcons == null) return;
            int count = sprite.commonAibuIcons.Length;
            if (handIconEnabled == null || handIconEnabled.Length != count) {
                handIconEnabled = new bool[count];
                handIconActive = new bool[count];
                handIconTextures = new Texture[count];
                handIconColors = new Color[count];
            }
            for (int i = 0; i < count; i++) {
                RawImage icon = sprite.commonAibuIcons[i];
                if (icon == null) continue;
                handIconEnabled[i] = icon.enabled;
                handIconActive[i] = icon.gameObject.activeSelf;
                handIconTextures[i] = icon.texture;
                handIconColors[i] = icon.color;
            }
        }

        internal void RestoreHandUi(HSprite sprite) {
            if (sprite == null || sprite.commonAibuIcons == null) return;
            int count = sprite.commonAibuIcons.Length;
            for (int i = 0; i < count; i++) {
                RawImage icon = sprite.commonAibuIcons[i];
                if (icon == null) continue;
                bool hasState = handIconEnabled != null && i < handIconEnabled.Length;
                if (hasState && handIconActive != null && i < handIconActive.Length && icon.gameObject.activeSelf != handIconActive[i]) icon.gameObject.SetActive(handIconActive[i]);
                icon.enabled = hasState && handIconEnabled[i];
                if (hasState && handIconTextures != null && i < handIconTextures.Length && handIconTextures[i] != null) icon.texture = handIconTextures[i];
                if (hasState && handIconColors != null && i < handIconColors.Length) icon.color = handIconColors[i];
            }
        }

        internal void RecordNativeLook(bool male, int id, GameObject target, bool eyeDisregard, bool neckDisregard) {
            LookState state = male ? maleLook : femaleLook;
            if (!male && Female != null) {
                AnimatorStateInfo motion = Female.getAnimatorStateInfo(0);
                string flagState = Flags == null ? null : Flags.nowAnimStateName;
                bool kissing = motion.IsName("K_Touch") || motion.IsName("K_Loop") ||
                    string.Equals(flagState, "K_Touch", StringComparison.Ordinal) ||
                    string.Equals(flagState, "K_Loop", StringComparison.Ordinal);
                if (!kissing && id != 0) {
                    state.LastMotionId = id;
                    state.LastMotionTarget = target;
                    state.LastMotionEyeDisregard = eyeDisregard;
                    state.LastMotionNeckDisregard = neckDisregard;
                    state.HasLastMotion = true;
                }
            }
            state.NativeId = id;
            state.NativeTarget = target;
            state.EyeDisregard = eyeDisregard;
            state.NeckDisregard = neckDisregard;
            state.HasNative = true;
        }

        internal void RestoreNativeFemaleLookAfterKiss() {
            if (Controllers == null || Controllers.EyeFemale == null ||
                !femaleLook.HasLastMotion) return;
            Controllers.EyeFemale.SetEyeNeckPtn(femaleLook.LastMotionId,
                femaleLook.LastMotionTarget, femaleLook.LastMotionEyeDisregard,
                femaleLook.LastMotionNeckDisregard);
            DiagnosticProbe.Info("kiss-look-motion-restored female=" + Female.gameObject.name +
                " id=" + femaleLook.LastMotionId);
        }

        internal void ReleaseKissHeadOverride() {
            // A normal head-follow toggle may clear the old pattern to 0. At
            // the end of a kiss that creates a visible twitch: return directly
            // to the H motion saved before K_Touch instead.
            femaleLook.AppliedHead = false;
            RestoreNativeFemaleLookAfterKiss();
        }

        internal void CaptureNativeFemaleLookBeforeKiss() {
            if (!femaleLook.HasNative || femaleLook.NativeId == 0) return;
            femaleLook.LastMotionId = femaleLook.NativeId;
            femaleLook.LastMotionTarget = femaleLook.NativeTarget;
            femaleLook.LastMotionEyeDisregard = femaleLook.EyeDisregard;
            femaleLook.LastMotionNeckDisregard = femaleLook.NeckDisregard;
            femaleLook.HasLastMotion = true;
        }

        internal void ApplyCameraLook(bool male, bool eyes, bool head, GameObject camera) {
            HMotionEyeNeck controller = male ? (HMotionEyeNeck)Controllers.EyeMale : Controllers.EyeFemale;
            if (controller == null) return;
            ChaControl actor = male ? Male : Female;
            if (actor == null) return;
            LookState state = male ? maleLook : femaleLook;
            // Save the actor's actual ChaControl patterns before applying a
            // camera override. A native HMotion id is not a safe restore value:
            // vanilla passes Camera as the target for many ordinary ids too.
            if (eyes && !state.AppliedEyes) {
                state.EyesPattern = actor.GetLookEyesPtn();
                state.HasEyesPattern = true;
            }
            if (head && !state.AppliedHead) {
                state.NeckPattern = actor.GetLookNeckPtn();
                state.HasNeckPattern = true;
            }
            bool restoreEyes = !eyes && state.AppliedEyes;
            bool restoreHead = !head && state.AppliedHead;
            if (restoreEyes && state.HasEyesPattern) actor.ChangeLookEyesPtn(state.EyesPattern);
            // The stored pre-override pattern belongs to the previous action.
            // Clear the forced camera neck once and let the current H motion
            // assign its own look target on the next native update.
            if (restoreHead) actor.ChangeLookNeckPtn(0, 0f);
            if (eyes) controller.SetEyeNeckPtn(1000, camera, false, false);
            if (head) controller.SetEyeNeckPtn(1001, camera, false, false);
            // With head-follow disabled, leave native neck animation alone.
            // Writing a saved pattern on every LateUpdate made the head snap
            // back after each team bind and fight the motion's turn animation.
            state.AppliedEyes = eyes;
            state.AppliedHead = head;
            if (restoreEyes || restoreHead) DiagnosticProbe.Info("look-restore actor=" + actor.gameObject.name +
                " eyePattern=" + (state.HasEyesPattern ? state.EyesPattern.ToString() : "missing") +
                " neckPattern=" + (state.HasNeckPattern ? state.NeckPattern.ToString() : "missing") +
                " eyes=" + eyes + " head=" + head);
        }

        internal void Capture(HFlag flags) {
            if (flags != null) {
                Animation = flags.nowAnimationInfo;
                Mode = flags.mode;
                GaugeFemale = flags.gaugeFemale;
                GaugeMale = flags.gaugeMale;
                Speed = flags.speed;
                Motion = flags.motion;
                Click = flags.click;
                Finish = flags.finish;
                PendingAnimation = flags.selectAnimationListInfo;
            }
            CaptureCenter();
        }

        internal void CaptureGuide(HSceneProc proc) {
            if (proc == null || proc.guideObject == null) return;
            GuidePosition = proc.guideObject.amount.position;
            GuideRotation = proc.guideObject.amount.rotation;
        }

        internal void RestoreGuide(HSceneProc proc) {
            if (proc == null || proc.guideObject == null) return;
            proc.guideObject.amount.position = GuidePosition;
            proc.guideObject.amount.rotation = GuideRotation;
        }

        internal bool ClearHandForCurrentState() {
            if (Controllers == null || Controllers.Hand == null || Flags == null) return false;
            string state = Flags.nowAnimStateName ?? string.Empty;
            bool climax = Flags.gaugeFemale >= 99f || state.IndexOf("Orgasm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                state.IndexOf("Climax", StringComparison.OrdinalIgnoreCase) >= 0;
            // A weak/strong loop can change animator state every few frames
            // near orgasm. Clearing HandCtrl for each state transition calls
            // ForceFinish repeatedly in the middle of Sonyu playback.
            string clearKey = climax ? "climax" : Flags.mode != HFlag.EMode.aibu ? "mode:" + Flags.mode : null;
            if (clearKey == null) {
                lastHandClearKey = null;
                return false;
            }
            if (lastHandClearKey == clearKey) return false;
            lastHandClearKey = clearKey;
            Controllers.Hand.ForceFinish(false);
            Controllers.Hand.DetachAllItem();
            DiagnosticProbe.Info("hand-clear teamFemale=" + Female.gameObject.name + " mode=" + Flags.mode + " state=" + state + " gauge=" + Flags.gaugeFemale.ToString("0.0"));
            return true;
        }

        internal void RestoreFlags(HFlag flags) {
            if (flags == null) return;
            flags.mode = Mode;
            flags.gaugeFemale = GaugeFemale;
            flags.gaugeMale = GaugeMale;
            flags.speed = Speed;
            flags.motion = Motion;
            flags.click = Click;
            flags.finish = Finish;
            flags.nowAnimationInfo = Animation;
            flags.selectAnimationListInfo = PendingAnimation;
        }

        internal void CaptureCenter() {
            if (Female == null || Male == null) return;
            Center = (Female.transform.position + Male.transform.position) * 0.5f;
        }

        internal void RestoreCenter() {
            if (Female == null || Male == null) return;
            Vector3 current = (Female.transform.position + Male.transform.position) * 0.5f;
            Vector3 delta = Center - current;
            Female.transform.position += delta;
            Male.transform.position += delta;
        }

        internal void Move(Vector3 delta) {
            if (Female != null) Female.transform.position += delta;
            if (Male != null) Male.transform.position += delta;
            Center += delta;
        }

        internal void SetVisible(bool visible) {
            if (Female != null && Female.gameObject.activeSelf != visible) Female.gameObject.SetActive(visible);
            if (Male != null && Male.gameObject.activeSelf != visible) Male.gameObject.SetActive(visible);
        }

        internal void SetAnimatorSpeed(float speed) {
            if (Mathf.Abs(appliedAnimatorSpeed - speed) < 0.001f) return;
            for (int i = 0; i < animators.Length; i++) if (animators[i] != null) animators[i].speed = speed;
            appliedAnimatorSpeed = speed;
        }

        private static void AddMainAnimator(List<Animator> result, ChaControl character) {
            if (character == null || character.animBody == null) return;
            Animator animator = character.animBody.GetComponent<Animator>();
            if (animator != null) result.Add(animator);
        }

        private sealed class LookState {
            internal bool HasNative;
            internal int NativeId;
            internal GameObject NativeTarget;
            internal bool EyeDisregard;
            internal bool NeckDisregard;
            internal bool HasLastMotion;
            internal int LastMotionId;
            internal GameObject LastMotionTarget;
            internal bool LastMotionEyeDisregard;
            internal bool LastMotionNeckDisregard;
            internal bool AppliedEyes;
            internal bool AppliedHead;
            internal bool HasEyesPattern;
            internal int EyesPattern;
            internal bool HasNeckPattern;
            internal int NeckPattern;
        }

        private sealed class ModeUiCall {
            internal readonly string Key;
            private readonly MethodBase method;
            private readonly object[] args;

            internal ModeUiCall(string key, MethodBase source, object[] sourceArgs) {
                Key = key;
                method = source;
                args = sourceArgs == null ? new object[0] : (object[])sourceArgs.Clone();
            }

            internal void Invoke(HSprite sprite) {
                try { method.Invoke(sprite, args); }
                catch (Exception ex) { DiagnosticProbe.OnceInfo("mode-ui-replay-error-" + Key, "mode-ui replay failed call=" + Key + " error=" + ex.Message); }
            }

            internal static string DescribeArgs(object[] values) {
                if (values == null || values.Length == 0) return "-";
                string[] text = new string[values.Length];
                for (int i = 0; i < values.Length; i++) text[i] = values[i] == null ? "null" : values[i].ToString();
                return string.Join(",", text);
            }
        }

        private sealed class ModeUiSnapshot {
            private static readonly FieldInfo SliderValueField = AccessTools.Field(typeof(Slider), "m_Value");
            private static readonly MethodInfo SliderUpdateVisuals = AccessTools.Method(typeof(Slider), "UpdateVisuals");
            private static readonly FieldInfo ToggleValueField = AccessTools.Field(typeof(Toggle), "m_IsOn");
            private static readonly MethodInfo TogglePlayEffect = AccessTools.Method(typeof(Toggle), "PlayEffect");
            private static readonly FieldInfo SelectableInteractableField = AccessTools.Field(typeof(Selectable), "m_Interactable");
            private static readonly PropertyInfo SelectableSelectionState = AccessTools.Property(typeof(Selectable), "currentSelectionState");
            private static readonly MethodInfo SelectableDoStateTransition = AccessTools.Method(typeof(Selectable), "DoStateTransition");
            internal readonly HSprite Sprite;
            private readonly GameObject[] objects;
            private readonly bool[] active;
            private readonly Graphic[] graphics;
            private readonly bool[] graphicEnabled;
            private readonly bool[] graphicRaycasts;
            private readonly Color[] graphicColors;
            private readonly Image[] images;
            private readonly Sprite[] imageSprites;
            private readonly Text[] texts;
            private readonly string[] textValues;
            private readonly TMP_Text[] tmpTexts;
            private readonly string[] tmpTextValues;
            private readonly Slider[] sliders;
            private readonly float[] sliderValues;
            private readonly Toggle[] toggles;
            private readonly bool[] toggleValues;
            private readonly Selectable[] selectables;
            private readonly bool[] interactableValues;
            private readonly SpriteState[] selectableSpriteStates;
            private readonly CanvasGroup[] canvasGroups;
            private readonly float[] canvasAlpha;
            private readonly bool[] canvasInteractable;
            private readonly bool[] canvasRaycasts;
            private readonly HSprite.SonyuSprite sonyuData;
            private readonly RectTransform speedRect;
            private readonly GameObject speedAibuBase;
            private readonly GameObject[] actionButtons;
            private readonly HFlag.EMode[] actionButtonModes;
            private readonly Button[] actionHitButtons;
            private readonly bool[] actionInteractable;
            private bool actionProbeInitialized;
            private int lastActionCaptureProbeFrame = -10000;
            private int lastActionRestoreProbeFrame = -10000;
            private int lastRootMismatchProbeFrame = -10000;
            private int lastOrphanActionProbeFrame = -10000;
            private int lastSpeedLayoutProbeFrame = -10000;
            private bool sonyuCondom;
            private readonly GameObject[] modeRoots = new GameObject[5];
            private readonly int[] modeRootObjectIndices = new int[5];
            private HFlag.EMode capturedMode;

            internal ModeUiSnapshot(HSprite sprite) {
                Sprite = sprite;
                sonyuData = sprite == null ? null : sprite.sonyu;
                speedRect = sprite == null || sprite.objSpeed == null ? null : sprite.objSpeed.GetComponent<RectTransform>();
                speedAibuBase = sprite == null ? null : sprite.objSpeedAibuBase;
                List<GameObject> result = new List<GameObject>();
                string[] fields = { "aibu", "houshi", "sonyu", "houshi3P", "sonyu3P" };
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++) {
                    object data = Traverse.Create(sprite).Field(fields[fieldIndex]).GetValue();
                    GameObject root = data == null ? null : Traverse.Create(data).Field("mainObj").GetValue<GameObject>();
                    if (root == null) continue;
                    modeRoots[fieldIndex] = root;
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < transforms.Length; i++) if (!result.Contains(transforms[i].gameObject)) result.Add(transforms[i].gameObject);
                }
                // Finish / start bubbles are not guaranteed to live below a
                // mode's mainObj in every HSprite prefab. Snapshot their
                // actual button trees as part of the owning team's UI.
                HSceneSpriteCategory[] buttonCategories = {
                    sprite.houshi == null ? null : sprite.houshi.categoryActionButton,
                    sprite.sonyu == null ? null : sprite.sonyu.categoryActionButton,
                    sprite.houshi3P == null ? null : sprite.houshi3P.categoryActionButton,
                    sprite.sonyu3P == null ? null : sprite.sonyu3P.categoryActionButton
                };
                List<GameObject> actionButtonList = new List<GameObject>();
                List<HFlag.EMode> actionButtonModeList = new List<HFlag.EMode>();
                List<Button> actionHitList = new List<Button>();
                for (int c = 0; c < buttonCategories.Length; c++) {
                    HSceneSpriteCategory category = buttonCategories[c];
                    if (category == null) continue;
                    for (int j = 0; j < category.GetCount(); j++) {
                        GameObject button = category.GetObject(j);
                        if (button == null) continue;
                        actionButtonList.Add(button);
                        actionButtonModeList.Add(c == 0 ? HFlag.EMode.houshi : c == 1 ? HFlag.EMode.sonyu :
                            c == 2 ? HFlag.EMode.houshi3P : HFlag.EMode.sonyu3P);
                        actionHitList.Add(button.GetComponentInChildren<Button>(true));
                        Transform[] parts = button.GetComponentsInChildren<Transform>(true);
                        for (int k = 0; k < parts.Length; k++) if (!result.Contains(parts[k].gameObject)) result.Add(parts[k].gameObject);
                    }
                }
                actionButtons = actionButtonList.ToArray();
                actionButtonModes = actionButtonModeList.ToArray();
                actionHitButtons = actionHitList.ToArray();
                actionInteractable = new bool[actionHitButtons.Length];
                objects = result.ToArray();
                active = new bool[objects.Length];
                for (int i = 0; i < modeRoots.Length; i++) modeRootObjectIndices[i] = Array.IndexOf(objects, modeRoots[i]);
                List<Graphic> graphicList = new List<Graphic>();
                List<Image> imageList = new List<Image>();
                List<Text> textList = new List<Text>();
                List<TMP_Text> tmpList = new List<TMP_Text>();
                List<Slider> sliderList = new List<Slider>();
                List<Toggle> toggleList = new List<Toggle>();
                List<Selectable> selectableList = new List<Selectable>();
                List<CanvasGroup> canvasList = new List<CanvasGroup>();
                for (int i = 0; i < objects.Length; i++) {
                    Graphic graphic = objects[i].GetComponent<Graphic>(); if (graphic != null) graphicList.Add(graphic);
                    Image image = objects[i].GetComponent<Image>(); if (image != null) imageList.Add(image);
                    Text label = objects[i].GetComponent<Text>(); if (label != null) textList.Add(label);
                    TMP_Text tmp = objects[i].GetComponent<TMP_Text>(); if (tmp != null) tmpList.Add(tmp);
                    Slider slider = objects[i].GetComponent<Slider>(); if (slider != null) sliderList.Add(slider);
                    Toggle toggle = objects[i].GetComponent<Toggle>(); if (toggle != null) toggleList.Add(toggle);
                    Selectable selectable = objects[i].GetComponent<Selectable>(); if (selectable != null) selectableList.Add(selectable);
                    CanvasGroup canvas = objects[i].GetComponent<CanvasGroup>(); if (canvas != null) canvasList.Add(canvas);
                }
                graphics = graphicList.ToArray(); graphicEnabled = new bool[graphics.Length]; graphicRaycasts = new bool[graphics.Length]; graphicColors = new Color[graphics.Length];
                images = imageList.ToArray(); imageSprites = new Sprite[images.Length];
                texts = textList.ToArray(); textValues = new string[texts.Length];
                tmpTexts = tmpList.ToArray(); tmpTextValues = new string[tmpTexts.Length];
                sliders = sliderList.ToArray(); sliderValues = new float[sliders.Length];
                toggles = toggleList.ToArray(); toggleValues = new bool[toggles.Length];
                selectables = selectableList.ToArray(); interactableValues = new bool[selectables.Length]; selectableSpriteStates = new SpriteState[selectables.Length];
                canvasGroups = canvasList.ToArray(); canvasAlpha = new float[canvasGroups.Length];
                canvasInteractable = new bool[canvasGroups.Length]; canvasRaycasts = new bool[canvasGroups.Length];
                DiagnosticProbe.Info("mode-ui-snapshot objects=" + objects.Length + " graphics=" + graphics.Length +
                    " images=" + images.Length + " toggles=" + toggles.Length + " selectables=" + selectables.Length);
            }

            internal void Capture(HFlag.EMode mode) {
                capturedMode = mode;
                for (int i = 0; i < actionHitButtons.Length; i++) {
                    Button button = actionHitButtons[i];
                    if (button == null) continue;
                    bool live = button.interactable;
                    if (actionProbeInitialized && actionInteractable[i] != live &&
                        actionButtons[i] != null && actionButtons[i].activeInHierarchy &&
                        Time.frameCount - lastActionCaptureProbeFrame >= 120) {
                        lastActionCaptureProbeFrame = Time.frameCount;
                        DiagnosticProbe.Info("action-ui-captured frame=" + Time.frameCount + " mode=" + mode +
                            " button=" + i + " previous=" + actionInteractable[i] + " live=" + live +
                            " caller=" + new System.Diagnostics.StackTrace(1, false));
                    }
                    actionInteractable[i] = live;
                }
                actionProbeInitialized = true;
                for (int i = 0; i < objects.Length; i++) if (objects[i] != null) active[i] = objects[i].activeSelf;
                // An external controller can change the shared root during a
                // background pass. The team's mode, rather than that transient
                // live root, owns which action UI should be visible on focus.
                for (int i = 0; i < modeRoots.Length; i++)
                    if (modeRootObjectIndices[i] >= 0) active[modeRootObjectIndices[i]] = IsModeRootSelected(i, mode);
                for (int i = 0; i < graphics.Length; i++) if (graphics[i] != null) {
                    graphicEnabled[i] = graphics[i].enabled;
                    graphicRaycasts[i] = graphics[i].raycastTarget;
                    graphicColors[i] = graphics[i].color;
                }
                for (int i = 0; i < images.Length; i++) if (images[i] != null) imageSprites[i] = images[i].sprite;
                for (int i = 0; i < texts.Length; i++) if (texts[i] != null) textValues[i] = texts[i].text;
                for (int i = 0; i < tmpTexts.Length; i++) if (tmpTexts[i] != null) tmpTextValues[i] = tmpTexts[i].text;
                for (int i = 0; i < sliders.Length; i++) if (sliders[i] != null) sliderValues[i] = sliders[i].value;
                for (int i = 0; i < toggles.Length; i++) if (toggles[i] != null) toggleValues[i] = toggles[i].isOn;
                for (int i = 0; i < selectables.Length; i++) if (selectables[i] != null) { interactableValues[i] = selectables[i].interactable; selectableSpriteStates[i] = selectables[i].spriteState; }
                for (int i = 0; i < canvasGroups.Length; i++) if (canvasGroups[i] != null) {
                    canvasAlpha[i] = canvasGroups[i].alpha;
                    canvasInteractable[i] = canvasGroups[i].interactable;
                    canvasRaycasts[i] = canvasGroups[i].blocksRaycasts;
                }
                if (sonyuData != null) sonyuCondom = sonyuData.isCondom;
            }

            internal void Restore(bool visible) {
                for (int i = 0; i < objects.Length; i++) if (objects[i] != null && objects[i].activeSelf != active[i]) objects[i].SetActive(active[i]);
                for (int i = 0; i < graphics.Length; i++) if (graphics[i] != null) {
                    if (graphics[i].enabled != graphicEnabled[i]) graphics[i].enabled = graphicEnabled[i];
                    if (graphics[i].raycastTarget != graphicRaycasts[i]) graphics[i].raycastTarget = graphicRaycasts[i];
                    if (graphics[i].color != graphicColors[i]) graphics[i].color = graphicColors[i];
                }
                for (int i = 0; i < images.Length; i++) if (images[i] != null && images[i].sprite != imageSprites[i]) images[i].sprite = imageSprites[i];
                for (int i = 0; i < texts.Length; i++) if (texts[i] != null && texts[i].text != textValues[i]) texts[i].text = textValues[i];
                for (int i = 0; i < tmpTexts.Length; i++) if (tmpTexts[i] != null && tmpTexts[i].text != tmpTextValues[i]) tmpTexts[i].text = tmpTextValues[i];
                for (int i = 0; i < sliders.Length; i++) if (sliders[i] != null && sliders[i].value != sliderValues[i]) {
                    if (SliderValueField != null) SliderValueField.SetValue(sliders[i], sliderValues[i]);
                    if (SliderUpdateVisuals != null) SliderUpdateVisuals.Invoke(sliders[i], null);
                }
                for (int i = 0; i < toggles.Length; i++) if (toggles[i] != null && toggles[i].isOn != toggleValues[i]) {
                    if (ToggleValueField != null) ToggleValueField.SetValue(toggles[i], toggleValues[i]);
                    if (TogglePlayEffect != null) TogglePlayEffect.Invoke(toggles[i], new object[] { true });
                }
                for (int i = 0; i < selectables.Length; i++) if (selectables[i] != null) {
                    if (!selectables[i].spriteState.Equals(selectableSpriteStates[i])) selectables[i].spriteState = selectableSpriteStates[i];
                    if (selectables[i].interactable != interactableValues[i] &&
                        selectables[i].gameObject.activeInHierarchy &&
                        Array.IndexOf(actionHitButtons, selectables[i]) >= 0 &&
                        Time.frameCount - lastActionRestoreProbeFrame >= 120) {
                        lastActionRestoreProbeFrame = Time.frameCount;
                        DiagnosticProbe.Info("action-ui-restored frame=" + Time.frameCount + " mode=" + capturedMode +
                            " button=" + selectables[i].name + " live=" + selectables[i].interactable +
                            " saved=" + interactableValues[i] + " caller=" + new System.Diagnostics.StackTrace(1, false));
                    }
                    // Background state only needs the value for native reads.
                    // The visible team must use Unity's setter so its disabled
                    // sprite/color is actually refreshed after a button click.
                    if (selectables[i].interactable != interactableValues[i]) {
                        int actionIndex = Array.IndexOf(actionHitButtons, selectables[i]);
                        if (!visible && SelectableInteractableField != null && actionIndex >= 0)
                            SelectableInteractableField.SetValue(selectables[i], interactableValues[i]);
                        else {
                            selectables[i].interactable = interactableValues[i];
                            // End the setter's tint tween on this frame; a
                            // background pass may swap the button again before
                            // the tween would otherwise finish.
                            if (actionIndex >= 0) SetActionButtonVisualInstantly(selectables[i]);
                        }
                    }
                }
                for (int i = 0; i < canvasGroups.Length; i++) if (canvasGroups[i] != null) {
                    if (canvasGroups[i].alpha != canvasAlpha[i]) canvasGroups[i].alpha = canvasAlpha[i];
                    if (canvasGroups[i].interactable != canvasInteractable[i]) canvasGroups[i].interactable = canvasInteractable[i];
                    if (canvasGroups[i].blocksRaycasts != canvasRaycasts[i]) canvasGroups[i].blocksRaycasts = canvasRaycasts[i];
                }
                if (sonyuData != null) sonyuData.isCondom = sonyuCondom;
                // MainSpriteChange places the one shared speed panel at x=200
                // for Aibu and x=0 for Houshi/Sonyu. Its RectTransform is
                // outside the per-mode roots above, so a background pass can
                // leave an Aibu panel on top of the focused Houshi controls.
                if (speedRect != null) {
                    Vector2 position = speedRect.anchoredPosition;
                    float expectedX = capturedMode == HFlag.EMode.aibu ? 200f : 0f;
                    if (Mathf.Abs(position.x - expectedX) > 0.01f) {
                        if (visible && Time.frameCount - lastSpeedLayoutProbeFrame >= 120) {
                            lastSpeedLayoutProbeFrame = Time.frameCount;
                            DiagnosticProbe.Info("mode-ui-speed-layout mode=" + capturedMode +
                                " previousX=" + position.x.ToString("0.0") + " restoredX=" + expectedX.ToString("0.0"));
                        }
                        position.x = expectedX;
                        speedRect.anchoredPosition = position;
                    }
                }
                if (speedAibuBase != null && speedAibuBase.activeSelf != (capturedMode == HFlag.EMode.aibu))
                    speedAibuBase.SetActive(capturedMode == HFlag.EMode.aibu);
                // A background transition can activate its new mode root while
                // the focused team's old root is still visible. Never persist
                // that transient overlap as a valid per-team UI state.
                for (int i = 0; i < modeRoots.Length; i++) {
                    if (modeRoots[i] == null) continue;
                    bool selected = IsModeRootSelected(i, capturedMode);
                    if (modeRoots[i].activeSelf != selected) modeRoots[i].SetActive(selected);
                }
                // Native finish buttons can live outside their mode's mainObj.
                // A snapshot taken while the other team ran must never show
                // Houshi/Sonyu buttons over the focused team's Aibu controls.
                for (int i = 0; i < actionButtons.Length; i++) {
                    GameObject item = actionButtons[i];
                    if (item == null || actionButtonModes[i] == capturedMode || !item.activeSelf) continue;
                    // Some prefab variants reuse one GameObject in more than
                    // one category. Keep it when the selected category owns
                    // that very same instance as well.
                    bool selectedElsewhere = false;
                    for (int j = 0; j < actionButtons.Length; j++)
                        if (ReferenceEquals(actionButtons[j], item) && actionButtonModes[j] == capturedMode) {
                            selectedElsewhere = true;
                            break;
                        }
                    if (selectedElsewhere) continue;
                    if (visible && Time.frameCount - lastOrphanActionProbeFrame >= 120) {
                        lastOrphanActionProbeFrame = Time.frameCount;
                        DiagnosticProbe.Info("mode-ui-orphan-action-hidden mode=" + capturedMode +
                            " owner=" + actionButtonModes[i] + " button=" + item.name);
                    }
                    item.SetActive(false);
                }
                if (visible && Time.frameCount - lastRootMismatchProbeFrame >= 120) {
                    int activeRoots = 0;
                    for (int i = 0; i < modeRoots.Length; i++) if (modeRoots[i] != null && modeRoots[i].activeSelf) activeRoots++;
                    if (activeRoots != 1) {
                        lastRootMismatchProbeFrame = Time.frameCount;
                        DiagnosticProbe.Info("mode-ui-root-mismatch mode=" + capturedMode + " activeRoots=" + activeRoots +
                            " roots=" + string.Join(",", Array.ConvertAll(modeRoots, root => root == null ? "null" : root.name + ":" + root.activeSelf + "/" + root.activeInHierarchy)));
                    }
                }
                // A transient background state can snapshot all action-button
                // hit graphics with raycastTarget=false. The native category
                // only toggles GameObject activity, so those buttons would
                // remain visible but unclickable after focus returns.
                for (int i = 0; i < actionButtons.Length; i++) {
                    GameObject item = actionButtons[i];
                    if (item == null || !item.activeInHierarchy) continue;
                    Button button = actionHitButtons[i];
                    if (button == null || button.targetGraphic == null) continue;
                    if (button.interactable) button.targetGraphic.raycastTarget = true;
                    if (visible && !button.interactable) EnsureDisabledActionVisual(button);
                }
            }

            private static void EnsureDisabledActionVisual(Button button) {
                Graphic graphic = button == null ? null : button.targetGraphic;
                if (graphic == null) return;
                ColorBlock colors = button.colors;
                Color expected = colors.disabledColor * colors.colorMultiplier;
                Color rendered = graphic.canvasRenderer.GetColor();
                if (rendered == expected) return;
                graphic.CrossFadeColor(expected, 0f, true, true);
                DiagnosticProbe.OnceInfo("action-disabled-visual-" + button.GetInstanceID(),
                    "action disabled visual repaired button=" + button.name +
                    " transition=" + button.transition + " expected=" + expected + " previous=" + rendered);
            }

            private static void SetActionButtonVisualInstantly(Selectable button) {
                if (button == null) return;
                object state = null;
                try {
                    if (SelectableSelectionState != null) state = SelectableSelectionState.GetValue(button, null);
                    if (state != null && SelectableDoStateTransition != null)
                        SelectableDoStateTransition.Invoke(button, new[] { state, (object)true });
                } catch (Exception ex) {
                    DiagnosticProbe.OnceInfo("action-ui-instant-transition-failed",
                        "action UI instant transition unavailable: " + ex.Message);
                }
                if (button.targetGraphic == null) return;
                ColorBlock colors = button.colors;
                string selection = state == null ? null : state.ToString();
                Color color = !button.IsInteractable() ? colors.disabledColor :
                    selection == "Pressed" ? colors.pressedColor :
                    selection == "Highlighted" ? colors.highlightedColor : colors.normalColor;
                button.targetGraphic.CrossFadeColor(color * colors.colorMultiplier, 0f, true, true);
            }

            private static bool IsModeRootSelected(int index, HFlag.EMode mode) {
                return (index == 0 && (mode == HFlag.EMode.aibu || mode == HFlag.EMode.lesbian || mode == HFlag.EMode.masturbation)) ||
                    (index == 1 && mode == HFlag.EMode.houshi) ||
                    (index == 2 && mode == HFlag.EMode.sonyu) ||
                    (index == 3 && mode == HFlag.EMode.houshi3P) ||
                    (index == 4 && mode == HFlag.EMode.sonyu3P);
            }

            internal string FirstDifference() {
                for (int i = 0; i < objects.Length; i++)
                    if (objects[i] != null && objects[i].activeSelf != active[i])
                        return "active=" + objects[i].name + " focused=" + active[i] + " live=" + objects[i].activeSelf;
                for (int i = 0; i < selectables.Length; i++)
                    if (selectables[i] != null && selectables[i].interactable != interactableValues[i])
                        return "interactable=" + selectables[i].name + " focused=" + interactableValues[i] +
                            " live=" + selectables[i].interactable;
                for (int i = 0; i < graphics.Length; i++)
                    if (graphics[i] != null && graphics[i].raycastTarget != graphicRaycasts[i])
                        return "raycast=" + graphics[i].name + " focused=" + graphicRaycasts[i] +
                            " live=" + graphics[i].raycastTarget;
                return null;
            }
        }
    }

    internal sealed class TeamControllers {
        internal HandCtrl Hand;
        internal YureCtrl Yure;
        internal HitReaction HitReaction;
        internal HitCollisionEnableCtrl HitCollision;
        internal SiruPasteCtrl Siru;
        internal FaceListCtrl Face;
        internal HParticleCtrl Particle;
        internal HMotionEyeNeckFemale EyeFemale;
        internal HMotionEyeNeckMale EyeMale;
        internal DynamicBoneReferenceCtrl Dynamic;
        internal AnimatorLayerCtrl AnimatorLayer;
        internal ParentObjectCtrl ParentBaseFemale;
        internal ParentObjectCtrl ParentFemale;
        internal ParentObjectCtrl ParentBaseMale;
        internal ParentObjectCtrl ParentMale;
        internal MetaballCtrl Meta;

        internal bool IsUsable {
            get { return Hand != null && Yure != null && HitReaction != null && HitCollision != null && Siru != null && Face != null &&
                         Particle != null && EyeFemale != null && EyeMale != null && Dynamic != null && AnimatorLayer != null &&
                         ParentBaseFemale != null && ParentFemale != null; }
        }

        internal static TeamControllers CapturePrimary(HSceneProc proc) {
            return new TeamControllers { Hand = proc.hand, Yure = proc.yure, HitReaction = proc.hitreaction, HitCollision = proc.hitcollisionFemale,
                Siru = proc.siru, Face = proc.face, Particle = proc.particle, EyeFemale = proc.eyeneckFemale, EyeMale = proc.eyeneckMale,
                Dynamic = proc.dynamicCtrl, AnimatorLayer = proc.alCtrl, ParentBaseFemale = proc.parentObjectBaseFemale,
                ParentFemale = proc.parentObjectFemale, ParentBaseMale = proc.parentObjectBaseMale,
                ParentMale = proc.parentObjectMale, Meta = proc.meta };
        }

        internal static TeamControllers CaptureSecondary(HSceneProc proc) {
            return new TeamControllers { Hand = proc.hand1, Yure = proc.yure1, HitReaction = proc.hitreaction1, HitCollision = proc.hitcollisionFemale1,
                Siru = proc.siru1, Face = proc.face1, Particle = proc.particle1, EyeFemale = proc.eyeneckFemale1, EyeMale = proc.eyeneckMale1,
                Dynamic = proc.dynamicCtrl1, AnimatorLayer = proc.alCtrl1, ParentBaseFemale = proc.parentObjectBaseFemale1,
                ParentFemale = proc.parentObjectFemale1 };
        }
    }

    public static class MultiTeamHApi {
        public static bool IsManagedFemale(ChaControl female) {
            for (int i = 0; i < TeamSession.Snapshot.Count; i++) if (TeamSession.Snapshot[i].ContainsFemale(female)) return true;
            return false;
        }

        public static bool IsActiveFemale(ChaControl female) {
            for (int i = 0; i < TeamSession.Snapshot.Count; i++) if (TeamSession.Snapshot[i].IsActiveFemale(female)) return true;
            return false;
        }
    }
}
