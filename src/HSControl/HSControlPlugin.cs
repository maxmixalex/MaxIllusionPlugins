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
        public const string Version = "0.1.7";
        private const string MountId = GUID + ".window";
        internal static HSControlPlugin Instance;
        private readonly HSControlMotionEngine motion = new HSControlMotionEngine(Environment.TickCount);
        private readonly HSControlAibuDriver aibu = new HSControlAibuDriver();
        private ConfigEntry<bool> master, manual, autoStrength, autoMaleFinish, autoFemaleFinish, autoReplay;
        private ConfigEntry<int> mode, band, linkage;
        private ConfigEntry<float> manualSpeed, rangeMin, rangeMax, strengthMin, strengthMax;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private HSControlView view;
        private HSceneProc scene;
        private Harmony harmony;
        private bool visible, didMaleFinish, strengthPending, strongMotionSelected;
        // Imported/previous auto-speed config describes a preference, NOT
        // authorization to take over hand input on scene start.
        private bool autoMotionArmed;
        private readonly HSControlReplayGate replayGate = new HSControlReplayGate();
        private float nextViewPoll;
        private int generation, sceneCheck;
        private string status = "就绪";

        private void Awake() {
            Instance = this;
            HSControlSettings ini = HSLegacyIni.Read(Path.Combine(Paths.PluginPath, "KK_HCtrl.ini"));
            master = Config.Bind("General", "Enabled", true, "Enable the native scene controller.");
            hotkey = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Ctrl+F8 toggles HSControl. Does not alter any native H scene menu button.");
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
            autoFemaleFinish = Config.Bind("Scene", "Auto female finish", ini.AutoFemaleFinish,
                "Enable native Aibu climax transition after a successful automatic caress. Never caps the game's own gauge.");
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
            if (hotkey != null && hotkey.Value.IsDown() &&
                !MaximumUIRuntime.IsAnyTextFieldFocused && !MaximumUIRuntime.IsModalOpen) {
                if (visible) Close();
                else if (scene != null && scene.flags != null) Open();
            }

            if (scene == null || scene.flags == null) {
                aibu.Clear(scene);
                if (visible) Close();
                return;
            }
            if (!master.Value) {
                aibu.Clear(scene);
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
            // No automatic speed or synthetic hand action merely because a
            // saved setting or old HCtrl.ini specifies a mode. The user must
            // change the speed controls within this H scene first.
            bool autoAllowed = HSControlActionRules.CanApplyAutoMotion(
                autoMotionArmed, config.Mode, config.ManualOverride);
            bool canDrive = autoAllowed && motion.Step(Time.deltaTime,
                hmode, state, flags.rely,
                flags.gaugeMale, flags.gaugeFemale, config);
            // Original KK_HCtrl drives native Aibu using both speed and
            // HFlag.FemaleGaugeUp. Previously we only updated speed (and only
            // for manual override), so the visible gauge stayed frozen.
            // The scene-local driver yields to either active manual hand.
            if (canDrive && hmode == 0 &&
                (config.Mode != 0 || config.ManualOverride)) {
                // Exactly as original HCtrl: turning on an automatic speed
                // mode also enables Aibu pulses. No independent opt-in.
                // Pulses last ONE update, never a multi-second input lock.
                aibu.Tick(scene, flags, motion.Current, Time.deltaTime);
                if (aibu.TakeCompleted() && autoFemaleFinish.Value) {
                    bool entered = HSControlNativeActions.TryStartAibuFemaleFinish(scene);
                    status = entered ? "已进入原生爱抚高潮动作" :
                        "自动爱抚结束，原生高潮动作暂不可切换";
                }
            } else aibu.Clear(scene);
            if (motion.StrengthToggleRequested && flags.click == HFlag.ClickKind.none &&
                hmode != 0 && HSControlMotionEngine.CanDrive(hmode, state, flags.rely)) {
                flags.click = HFlag.ClickKind.motionchange;
                strengthPending = true;
            }
            if (strengthPending && flags.click == HFlag.ClickKind.none) strengthPending = false;
            // Automatically triggering finish/replay from loaded defaults
            // before the user touched a control is also unsafe.
            if (autoMotionArmed) {
                HandleFinish(flags, config);
                HandleReplay(flags, config);
            }
            if (visible && view != null && Time.unscaledTime >= nextViewPoll) {
                nextViewPoll = Time.unscaledTime + .15f;
                RefreshView();
            }
        }

        private void SwapScene(HSceneProc next) {
            aibu.Clear(scene);
            scene = next;
            didMaleFinish = false;
            replayGate.Reset();
            strengthPending = false;
            autoMotionArmed = false;
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
            // HCtrl.CanProc does not impose an invented "currently
            // unavailable" gate based on loop, click, or animation metadata.
            // Only the actual native action and original mode matter.
            return flags != null && scene != null &&
                HSControlNativeActions.HasAction(scene) &&
                HSControlActionRules.OriginalCanForce(
                    (int)flags.mode, needsLoop ? 8 : 0);
        }

        private void PerformSceneCommand(int version, HSceneProc owner, int kind) {
            if (!SessionCurrent(version, owner) || !master.Value ||
                owner.flags == null) return;
            HFlag flags = owner.flags;
            // Original HCtrl buttons use CanProc(mode) and dispatch directly.
            // Do not produce fabricated "current action disallowed" banners.
            if (!HSControlActionRules.OriginalCanForce((int)flags.mode, kind))
                return;
            try {
                bool performed;
                if (kind == 0 || kind == 1) {
                    strongMotionSelected = kind == 1;
                    performed = HSControlNativeActions.TryChangeStrength(
                        owner, strongMotionSelected);
                } else if (kind == 8) {
                    performed = HSControlNativeActions.TryStartLoop(
                        owner, strongMotionSelected);
                    if (performed) motion.Reset(flags.speedCalc);
                } else {
                    if (kind == 6 && (int)flags.mode == 0)
                        aibu.Clear(owner);
                    performed = HSControlNativeActions.TryForceAction(owner, kind);
                }
                if (performed) {
                    status = "原生操作已发送";
                    RefreshView();
                }
            } catch (Exception ex) {
                Logger.LogWarning("HCtrl native action adapter: " + ex);
            }
        }

        private bool NativeButtonAvailable(HSceneProc owner, int kind) {
            return master != null && master.Value &&
                owner != null && owner.flags != null &&
                HSControlNativeActions.HasAction(owner) &&
                HSControlActionRules.OriginalCanForce(
                    (int)owner.flags.mode, kind);
        }

        private bool canDriveSafe(HFlag flags) {
            return autoMotionArmed && flags != null && master != null && master.Value;
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
                CanForceLoop = master.Value && flags != null && owner != null &&
                    HSControlNativeActions.HasAction(owner) &&
                    HSControlActionRules.OriginalCanForce((int)flags.mode, 8),
                CanStageMale = NativeButtonAvailable(owner, 2),
                CanStageFemale = NativeButtonAvailable(owner, 3),
                CanStageBoth = NativeButtonAvailable(owner, 4),
                CanFinishMale = NativeButtonAvailable(owner, 5),
                CanFemaleFinish = NativeButtonAvailable(owner, 6),
                CanFinishBoth = NativeButtonAvailable(owner, 7),
                FemaleLocked = flags != null && flags.lockGugeFemale,
                MaleLocked = flags != null && flags.lockGugeMale,
                Driving = canDriveSafe(flags) && motion.Driving,
                AutoArmed = autoMotionArmed,
                CurrentSpeed = motion.Current, TargetSpeed = motion.Target,
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
            model.ChangeMode = value => { if (live()) {
                mode.Value = value;
                autoMotionArmed = value != 0;
                if (!autoMotionArmed) aibu.Clear(owner);
                motion.Reset(flags.speedCalc);
                RefreshView();
            } };
            model.ChangeBand = value => { if (live()) { band.Value = value; autoMotionArmed = true; RefreshView(); } };
            model.ChangeLinkage = value => { if (live()) { linkage.Value = value; autoMotionArmed = true; RefreshView(); } };
            model.ChangeRangeMin = value => { if (live()) { rangeMin.Value = value; autoMotionArmed = true; RefreshView(); } };
            model.ChangeRangeMax = value => { if (live()) { rangeMax.Value = value; autoMotionArmed = true; RefreshView(); } };
            model.ChangeManualOverride = value => { if (live()) {
                manual.Value = value;
                autoMotionArmed = value || mode.Value != 0;
                if (!autoMotionArmed) aibu.Clear(owner);
                RefreshView();
            } };
            model.ChangeManualSpeed = value => { if (live()) {
                manualSpeed.Value = value;
                autoMotionArmed = true;
                RefreshView();
            } };
            model.ChangeAutoStrength = value => { if (live()) {
                autoStrength.Value = value;
                if (value) autoMotionArmed = true;
                RefreshView();
            } };
            model.ChangeStrengthMin = value => { if (live()) { strengthMin.Value = value; RefreshView(); } };
            model.ChangeStrengthMax = value => { if (live()) { strengthMax.Value = value; RefreshView(); } };
            model.ChangeAutoMaleFinish = value => { if (live()) { autoMaleFinish.Value = value; RefreshView(); } };
            model.ChangeAutoFemaleFinish = value => { if (live()) { autoFemaleFinish.Value = value; RefreshView(); } };
            model.ChangeAutoReplay = value => { if (live()) { autoReplay.Value = value; RefreshView(); } };
            model.ToggleFemaleLock = () => { if (live() && owner.sprite != null) {
                if (!owner.sprite.IsSpriteAciotn()) return;
                flags.lockGugeFemale = !flags.lockGugeFemale;
                owner.sprite.OnFemaleGaugeLockOnGauge();
                RefreshView();
            } };
            model.ToggleMaleLock = () => { if (live() && owner.sprite != null) {
                if (!owner.sprite.IsSpriteAciotn()) return;
                flags.lockGugeMale = !flags.lockGugeMale;
                owner.sprite.OnMaleGaugeLockOnGauge();
                RefreshView();
            } };
            model.ChangeGaugeMale = value => { if (live()) { flags.gaugeMale = Mathf.Clamp(value, 0f, 100f); RefreshView(); } };
            model.ChangeGaugeFemale = value => { if (live()) {
                flags.gaugeFemale = Mathf.Clamp(value, 0f, 100f); RefreshView();
            }};
            model.Reroll = () => { if (live()) { motion.Reset(flags.speedCalc); RefreshView(); } };
            model.PlayLoop = () => PerformSceneCommand(token, owner, 8);
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
            aibu.Clear(scene);
            autoMotionArmed = false;
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
