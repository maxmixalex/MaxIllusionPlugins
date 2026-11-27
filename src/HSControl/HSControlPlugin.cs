using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI;
using MaximumUI;
using UnityEngine;

[assembly: AssemblyFileVersion(HSControl.HSControlPlugin.Version)]

namespace HSControl
{
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInIncompatibility("KK_HCtrl")]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    [BepInPlugin(GUID, "HSControl", Version)]
    public sealed class HSControlPlugin : BaseUnityPlugin
    {
        public const string GUID = "MaxMixAlex.HSControl";
        public const string Version = "0.1.2";
        private const string MountId = GUID + ".window";
        internal static HSControlPlugin Instance;
        private readonly HSControlMotionEngine motion = new HSControlMotionEngine(Environment.TickCount);
        private ConfigEntry<bool> master, manual, autoStrength, autoMaleFinish, autoFemaleFinish, autoReplay;
        private ConfigEntry<int> mode, band, linkage;
        private ConfigEntry<float> manualSpeed, rangeMin, rangeMax, strengthMin, strengthMax;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private HSControlView view;
        private HSceneProc scene;
        private NativeSceneButton sceneButton;
        private float nextButtonProbe;
        private Harmony harmony;
        private bool visible, didMaleFinish, strengthPending;
        private readonly HSControlReplayGate replayGate = new HSControlReplayGate();
        private float nextViewPoll;
        private int generation, sceneCheck;
        private string status = "就绪";

        private void Awake() {
            Instance = this;
            HSControlSettings ini = HSLegacyIni.Read(Path.Combine(Paths.PluginPath, "KK_HCtrl.ini"));
            master = Config.Bind("General", "Enabled", true, "Enable the native scene controller.");
            hotkey = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Ctrl+F8 opens HSControl during a running scene; F8 is used by ClothingCompression.");
            mode = Config.Bind("Motion", "Auto speed mode", ini.Mode, "0 off; 1 slow/full/fast; 2 range; 3 gauge linked.");
            band = Config.Bind("Motion", "Speed band", ini.Band, "0 slow, 1 alternating full, 2 fast.");
            linkage = Config.Bind("Motion", "Gauge linkage", ini.Linkage, "0 average, 1 larger gauge.");
            rangeMin = Config.Bind("Motion", "Random range min", ini.RangeMin, "Minimum speed for random range mode.");
            rangeMax = Config.Bind("Motion", "Random range max", ini.RangeMax, "Maximum speed for random range mode.");
            manual = Config.Bind("Motion", "Manual override", false, "Use a manual speed until disabled.");
            manualSpeed = Config.Bind("Motion", "Manual speed", .5f, "Manual normalized speed 0 to 1.");
            autoStrength = Config.Bind("Motion", "Auto strength", ini.AutoStrength, "Alternate native motion strength on an interval.");
            strengthMin = Config.Bind("Motion", "Strength timer minimum", ini.StrengthMinSeconds, "Minimum seconds between changes.");
            strengthMax = Config.Bind("Motion", "Strength timer maximum", ini.StrengthMaxSeconds, "Maximum seconds between changes.");
            autoMaleFinish = Config.Bind("Scene", "Auto male finish", ini.AutoMaleFinish, "Request the native finish action at a full gauge.");
            // Retain the legacy setting on disk, but never cap the game's native
            // female gauge: disabling HSControl automation cannot block gameplay.
            autoFemaleFinish = Config.Bind("Scene", "Auto female finish", ini.AutoFemaleFinish,
                "Legacy setting (no longer suppresses native female climax).");
            autoReplay = Config.Bind("Scene", "Auto replay", ini.AutoReplay, "Request native repeat after the final state.");
            try { harmony = Harmony.CreateAndPatchAll(typeof(HSControlHooks), GUID); }
            catch (Exception ex) { Logger.LogError("HSControl native hooks could not be installed: " + ex); }
        }

        private HSControlSettings Settings() {
            HSControlSettings value = new HSControlSettings {
                Mode = mode.Value, Band = band.Value, Linkage = linkage.Value,
                RangeMin = rangeMin.Value, RangeMax = rangeMax.Value,
                ManualOverride = manual.Value, ManualSpeed = manualSpeed.Value,
                AutoStrength = autoStrength.Value, StrengthMinSeconds = strengthMin.Value,
                StrengthMaxSeconds = strengthMax.Value, AutoMaleFinish = autoMaleFinish.Value,
                AutoFemaleFinish = autoFemaleFinish.Value, AutoReplay = autoReplay.Value
            };
            value.Sanitize();
            return value;
        }

        private void Update() {
            if (scene == null || ++sceneCheck >= 30) {
                sceneCheck = 0;
                HSceneProc observed = UnityEngine.Object.FindObjectOfType<HSceneProc>();
                if (!ReferenceEquals(scene, observed)) SwapScene(observed);
            }
            if (scene != null && scene.flags != null && Time.unscaledTime >= nextButtonProbe) {
                nextButtonProbe = Time.unscaledTime + .5f;
                if (sceneButton == null || !sceneButton.Matches(scene)) {
                    if (sceneButton != null) sceneButton.Dispose();
                    sceneButton = NativeSceneButton.TryInstall(scene, delegate { ToggleFromNativeButton(scene); }, Logger.LogWarning);
                }
            }
            if (hotkey != null && hotkey.Value.IsDown() &&
                !MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUIRuntime.IsModalOpen) {
                if (visible) Close();
                else if (scene != null && scene.flags != null) Open();
            }

            if (scene == null || scene.flags == null) {
                if (visible) Close();
                return;
            }
            if (!master.Value) {
                // An enabled/disabled master switch must be recoverable from
                // the same window; do not freeze the UI on the disabled branch.
                if (visible && view != null && Time.unscaledTime >= nextViewPoll) {
                    nextViewPoll = Time.unscaledTime + .15f;
                    RefreshView();
                }
                return;
            }
            HFlag flags = scene.flags;
            HSControlSettings config = Settings();
            int hmode = (int)flags.mode;
            string state = flags.nowAnimStateName ?? string.Empty;
            bool canDrive = motion.Step(Time.deltaTime, hmode, state, flags.rely,
                flags.gaugeMale, flags.gaugeFemale, config);
            // Aibu input and both hands belong to the game's own drag controller.
            // Only a deliberate manual speed override may write to this idle path.
            if (canDrive && hmode == 0 && config.ManualOverride)
                flags.speed = motion.Current;
            if (motion.StrengthToggleRequested && flags.click == HFlag.ClickKind.none &&
                hmode != 0 && HSControlMotionEngine.CanDrive(hmode, state, flags.rely)) {
                flags.click = HFlag.ClickKind.motionchange;
                strengthPending = true;
            }
            if (strengthPending && flags.click == HFlag.ClickKind.none) strengthPending = false;
            HandleFinish(flags, config);
            HandleReplay(flags, config);
            if (visible && view != null && Time.unscaledTime >= nextViewPoll) {
                nextViewPoll = Time.unscaledTime + .15f;
                RefreshView();
            }
        }

        internal void ToggleFromNativeButton(HSceneProc owner) {
            if (!ReferenceEquals(owner, scene) || scene == null || scene.flags == null) return;
            if (visible) Close(); else Open();
        }

        private void SwapScene(HSceneProc next) {
            if (sceneButton != null) sceneButton.Dispose();
            sceneButton = null;
            nextButtonProbe = 0f;
            scene = next;
            didMaleFinish = false;
            replayGate.Reset();
            strengthPending = false;
            generation++;
            if (next != null && next.flags != null) {
                motion.Reset(next.flags.speedCalc);
                status = "已连接当前场景";
            } else {
                status = "场景已结束";
                if (visible) Close();
            }
        }

        private bool SessionCurrent(int version, HSceneProc owner) {
            return master != null && visible && scene != null &&
                ReferenceEquals(scene, owner) && owner.flags != null && version == generation;
        }

        private void HandleFinish(HFlag flags, HSControlSettings settings) {
            if (flags.gaugeMale < 95f) didMaleFinish = false;
            if (!settings.AutoMaleFinish || didMaleFinish || flags.gaugeMale < 100f ||
                flags.click != HFlag.ClickKind.none ||
                !HSControlMotionEngine.CanDrive((int)flags.mode, flags.nowAnimStateName, flags.rely) ||
                scene.sprite == null) return;
            string method = null;
            int kind = (int)flags.mode;
            if (kind == 1) method = "IsHoushiAutoFinish";
            else if (kind == 2) method = "IsSonyuAutoFinish";
            else if (kind == 6) method = "Is3PHoushiAutoFinish";
            else if (kind == 7) method = "IsSonyu3PAutoFinish";
            else if (kind == 8) method = "IsHoushi3PDarkAutoFinish";
            else if (kind == 9) method = "IsSonyu3PDarkAutoFinish";
            if (method == null) return;
            try {
                MethodInfo getter = scene.sprite.GetType().GetMethod(method,
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (getter == null || getter.ReturnType != typeof(bool)) return;
                bool automatic = (bool)getter.Invoke(scene.sprite, null);
                flags.click = automatic ? HFlag.ClickKind.outside : HFlag.ClickKind.inside;
                didMaleFinish = true;
            } catch (Exception ex) {
                Logger.LogWarning("Native finish state unavailable: " + ex.GetBaseException().Message);
            }
        }

        private bool ReplayVoiceReady() {
            if (scene == null || scene.voice == null || scene.voice.nowVoices == null ||
                scene.voice.nowVoices.Length == 0 || scene.voice.nowVoices[0] == null ||
                scene.voice.nowVoices[0].state != HVoiceCtrl.VoiceKind.breath) return false;
            if (scene.flags != null && (int)scene.flags.mode >= 6 &&
                scene.voice.nowVoices.Length > 1 && scene.voice.nowVoices[1] != null &&
                scene.voice.nowVoices[1].state != HVoiceCtrl.VoiceKind.breath) return false;
            return true;
        }

        private void HandleReplay(HFlag flags, HSControlSettings settings) {
            ReplayCommand command = replayGate.Step(Time.deltaTime, (int)flags.mode,
                flags.nowAnimStateName, flags.rely, ReplayVoiceReady(),
                flags.click == HFlag.ClickKind.none, settings.AutoReplay);
            if (command == ReplayCommand.None) return;
            if (command == ReplayCommand.Again) flags.click = HFlag.ClickKind.again;
            else if (command == ReplayCommand.Insert)
                flags.click = flags.isAnalPlay && flags.isAnalInsertOK
                    ? HFlag.ClickKind.insert_anal : HFlag.ClickKind.insert;
            else if (command == ReplayCommand.Resume)
                flags.click = HFlag.ClickKind.speedup;
            status = "自动恢复：" + command;
        }

        internal void AfterWaitSpeedProc(HFlag flags) {
            if (master == null || !master.Value || scene == null || scene.flags == null ||
                !ReferenceEquals(scene.flags, flags) || !motion.Driving || (int)flags.mode == 0) return;
            flags.speedCalc = motion.Current;
        }

        private void Open() {
            if (scene == null || scene.flags == null) return;
            if (view == null) {
                MaximumUIRuntime.EnsureHost();
                view = new HSControlView();
                MaximumUIRuntime.Mount(MountId, view, new MountOptions {
                    Visible = false, BuildEveryFrame = false, SoftMouseCapture = true,
                    SoftKeyboardCapture = true, SortOrder = 130
                });
            }
            visible = true;
            generation++;
            RefreshView();
            MaximumUIRuntime.SetVisible(MountId, true);
            MaximumUIRuntime.Focus(MountId);
        }

        private void Close() {
            visible = false;
            generation++;
            if (view != null) MaximumUIRuntime.SetVisible(MountId, false);
        }

        private bool CanNativeCommands(HFlag flags, bool needsLoop) {
            if (flags == null || flags.nowAnimationInfo == null ||
                flags.click != HFlag.ClickKind.none ||
                !HSControlActionRules.CanStartLoop((int)flags.mode, flags.rely, true)) return false;
            return !needsLoop || HSControlActionRules.IsLoop(flags.nowAnimStateName);
        }

        private static bool CanStageGauge(HFlag flags) {
            return flags != null && HSControlActionRules.CanStageGauge(
                (int)flags.mode, flags.nowAnimationInfo != null,
                flags.click != HFlag.ClickKind.none);
        }

        private void PerformSceneCommand(int version, HSceneProc owner, int kind) {
            if (!SessionCurrent(version, owner) || !master.Value || owner.flags == null) return;
            HFlag flags = owner.flags;
            if (kind <= 1 && !CanNativeCommands(flags, false)) {
                status = "当前场景无法切换原生动作。";
                RefreshView();
                return;
            }
            if (kind >= 2 && !CanStageGauge(flags)) {
                status = "当前场景正在执行其他动作，暂不可修改状态。";
                RefreshView();
                return;
            }
            try {
                bool okay;
                if (kind <= 1) {
                    okay = HSControlNativeActions.TryStartLoop(owner, kind == 1);
                    if (okay) motion.Reset(flags.speedCalc);
                } else {
                    // Staging a native gauge is a data update only. Never
                    // clear a touch/drag click to make this button usable.
                    if (kind == 2 || kind == 4) flags.gaugeMale = Math.Max(flags.gaugeMale, 70f);
                    if (kind == 3 || kind == 4) flags.gaugeFemale = Math.Max(flags.gaugeFemale, 70f);
                    if (kind >= 5) {
                        if (kind == 6 || kind == 7) {
                            // Manual female climax is not coupled to the auto setting.
                            // The native game processes the full gauge itself.
                            flags.gaugeFemale = 100f;
                        }
                        if (kind == 5 || kind == 7) {
                            flags.gaugeMale = 100f;
                            didMaleFinish = false;
                            HSControlSettings forced = Settings();
                            forced.AutoMaleFinish = true;
                            HandleFinish(flags, forced);
                        }
                    }
                    okay = true;
                }
                status = okay ? "已向原生场景发送操作：" + kind : "当前动作暂不可执行。";
            } catch (Exception ex) {
                status = "原生操作失败：" + ex.GetBaseException().Message;
                Logger.LogWarning(ex);
            }
            RefreshView();
        }

        private void RefreshView() {
            if (view == null || !visible) return;
            HSceneProc owner = scene;
            HFlag flags = owner == null ? null : owner.flags;
            int token = generation;
            HSControlSettings cfg = Settings();
            HSControlViewModel model = new HSControlViewModel {
                Version = Version, Status = status,
                Available = flags != null, Enabled = master.Value,
                AnimationState = flags == null ? string.Empty : flags.nowAnimStateName,
                SceneMode = flags == null ? -1 : (int)flags.mode,
                CanForceLoop = master.Value && CanNativeCommands(flags, false),
                CanStage = master.Value && CanStageGauge(flags),
                CanFemaleFinish = master.Value && CanStageGauge(flags),
                Driving = motion.Driving, CurrentSpeed = motion.Current, TargetSpeed = motion.Target,
                GaugeMale = flags == null ? 0f : flags.gaugeMale,
                GaugeFemale = flags == null ? 0f : flags.gaugeFemale,
                Mode = cfg.Mode, Band = cfg.Band, Linkage = cfg.Linkage,
                RangeMin = cfg.RangeMin, RangeMax = cfg.RangeMax,
                ManualOverride = cfg.ManualOverride, ManualSpeed = cfg.ManualSpeed,
                AutoStrength = cfg.AutoStrength, StrengthMinSeconds = cfg.StrengthMinSeconds,
                StrengthMaxSeconds = cfg.StrengthMaxSeconds,
                AutoMaleFinish = cfg.AutoMaleFinish, AutoFemaleFinish = cfg.AutoFemaleFinish,
                AutoReplay = cfg.AutoReplay, Close = Close
            };
            Func<bool> live = () => SessionCurrent(token, owner);
            model.ChangeEnabled = value => { if (live()) { master.Value = value; nextViewPoll = 0f; RefreshView(); } };
            model.ChangeMode = value => { if (live()) { mode.Value = value; motion.Reset(flags.speedCalc); RefreshView(); } };
            model.ChangeBand = value => { if (live()) { band.Value = value; RefreshView(); } };
            model.ChangeLinkage = value => { if (live()) { linkage.Value = value; RefreshView(); } };
            model.ChangeRangeMin = value => { if (live()) { rangeMin.Value = value; RefreshView(); } };
            model.ChangeRangeMax = value => { if (live()) { rangeMax.Value = value; RefreshView(); } };
            model.ChangeManualOverride = value => { if (live()) { manual.Value = value; RefreshView(); } };
            model.ChangeManualSpeed = value => { if (live()) { manualSpeed.Value = value; RefreshView(); } };
            model.ChangeAutoStrength = value => { if (live()) { autoStrength.Value = value; RefreshView(); } };
            model.ChangeStrengthMin = value => { if (live()) { strengthMin.Value = value; RefreshView(); } };
            model.ChangeStrengthMax = value => { if (live()) { strengthMax.Value = value; RefreshView(); } };
            model.ChangeAutoMaleFinish = value => { if (live()) { autoMaleFinish.Value = value; RefreshView(); } };
            model.ChangeAutoFemaleFinish = value => { if (live()) { autoFemaleFinish.Value = value; RefreshView(); } };
            model.ChangeAutoReplay = value => { if (live()) { autoReplay.Value = value; RefreshView(); } };
            model.ChangeGaugeMale = value => { if (live()) { flags.gaugeMale = Mathf.Clamp(value, 0f, 100f); RefreshView(); } };
            model.ChangeGaugeFemale = value => { if (live()) {
                flags.gaugeFemale = Mathf.Clamp(value, 0f, 100f); RefreshView();
            }};
            model.Reroll = () => { if (live()) { motion.Reset(flags.speedCalc); RefreshView(); } };
            model.PlayWeak = () => PerformSceneCommand(token, owner, 0);
            model.PlayStrong = () => PerformSceneCommand(token, owner, 1);
            model.StageMale = () => PerformSceneCommand(token, owner, 2);
            model.StageFemale = () => PerformSceneCommand(token, owner, 3);
            model.StageBoth = () => PerformSceneCommand(token, owner, 4);
            model.FinishMale = () => PerformSceneCommand(token, owner, 5);
            model.FinishFemale = () => PerformSceneCommand(token, owner, 6);
            model.FinishBoth = () => PerformSceneCommand(token, owner, 7);
            if (!view.Model.SameDisplay(model)) {
                view.Model = model;
                MaximumUIRuntime.MarkDirty(view);
            }
        }

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup() {
            if (sceneButton != null) sceneButton.Dispose();
            sceneButton = null;
            if (view != null) MaximumUIRuntime.Unmount(MountId);
            view = null;
            visible = false;
            scene = null;
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null;
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }

    internal static class HSControlHooks
    {
        [HarmonyPostfix, HarmonyPatch(typeof(HFlag), "WaitSpeedProc",
            new Type[] { typeof(bool), typeof(AnimationCurve) })]
        private static void OnWaitSpeed(HFlag __instance) {
            HSControlPlugin plugin = HSControlPlugin.Instance;
            if (plugin != null) plugin.AfterWaitSpeedProc(__instance);
        }

    }
}
