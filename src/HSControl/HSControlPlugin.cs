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
        public const string Version = "0.1.0";
        private const string MountId = GUID + ".window";
        internal static HSControlPlugin Instance;
        private readonly HSControlMotionEngine motion = new HSControlMotionEngine(Environment.TickCount);
        private ConfigEntry<bool> master, manual, autoStrength, autoMaleFinish, autoFemaleFinish, autoReplay;
        private ConfigEntry<int> mode, band, linkage;
        private ConfigEntry<float> manualSpeed, rangeMin, rangeMax, strengthMin, strengthMax;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private HSControlView view;
        private HSceneProc scene;
        private Harmony harmony;
        private bool visible, didMaleFinish, replayPending, strengthPending;
        private float replayElapsed, nextViewPoll;
        private int generation, sceneCheck;
        private string status = "就绪";

        private void Awake() {
            Instance = this;
            HSControlSettings ini = HSLegacyIni.Read(Path.Combine(Paths.PluginPath, "KK_HCtrl.ini"));
            master = Config.Bind("General", "Enabled", true, "Enable the native scene controller.");
            hotkey = Config.Bind("General", "Show menu", new KeyboardShortcut(KeyCode.F8),
                "Open HSControl during a running scene.");
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
            autoFemaleFinish = Config.Bind("Scene", "Auto female finish", ini.AutoFemaleFinish, "Allow native female finish at full gauge.");
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
            bool canDrive = motion.Step(Time.deltaTime, hmode, state, flags.rely != null,
                flags.gaugeMale, flags.gaugeFemale, config);
            if (canDrive && hmode == 0) flags.speed = motion.Current;
            if (!config.AutoFemaleFinish && flags.gaugeFemale > 99f) flags.gaugeFemale = 99f;
            if (motion.StrengthToggleRequested && flags.click == HFlag.ClickKind.none &&
                hmode != 0 && HSControlMotionEngine.CanDrive(hmode, state, flags.rely != null)) {
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

        private void SwapScene(HSceneProc next) {
            scene = next;
            didMaleFinish = false;
            replayPending = false;
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
                !HSControlMotionEngine.CanDrive((int)flags.mode, flags.nowAnimStateName, flags.rely != null) ||
                scene.sprite == null) return;
            string method = null;
            int kind = (int)flags.mode;
            if (kind == 1) method = "IsHoushiAutoFinish";
            else if (kind == 2) method = "IsSonyuAutoFinish";
            else if (kind == 6) method = "Is3PHoushiAutoFinish";
            else if (kind == 7) method = "IsSonyu3PAutoFinish";
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

        private void HandleReplay(HFlag flags, HSControlSettings settings) {
            if (!settings.AutoReplay || !HSControlMotionEngine.IsSupportedMode((int)flags.mode)) {
                replayPending = false;
                replayElapsed = 0f;
                return;
            }
            string state = flags.nowAnimStateName ?? string.Empty;
            if (state.IndexOf("Loop", StringComparison.Ordinal) >= 0) {
                replayPending = false;
                replayElapsed = 0f;
                return;
            }
            if (!state.EndsWith("_A", StringComparison.Ordinal)) return;
            if (!replayPending) { replayPending = true; replayElapsed = 0f; }
            replayElapsed += Time.deltaTime;
            if (replayElapsed >= 1.5f && flags.click == HFlag.ClickKind.none) {
                flags.click = HFlag.ClickKind.again;
                replayPending = false;
                replayElapsed = -100f;
            }
        }

        internal void AfterWaitSpeedProc(HFlag flags) {
            if (master == null || !master.Value || scene == null || scene.flags == null ||
                !ReferenceEquals(scene.flags, flags) || !motion.Driving || (int)flags.mode == 0) return;
            flags.speedCalc = motion.Current;
        }

        internal void AfterFemaleGaugeUp(HFlag flags) {
            if (master == null || !master.Value || scene == null || scene.flags == null ||
                !ReferenceEquals(scene.flags, flags) || autoFemaleFinish.Value) return;
            if (flags.gaugeFemale >= 100f) flags.gaugeFemale = 99f;
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
                flags.gaugeFemale = Mathf.Clamp(value, 0f, autoFemaleFinish.Value ? 100f : 99f); RefreshView();
            }};
            model.Reroll = () => { if (live()) { motion.Reset(flags.speedCalc); RefreshView(); } };
            if (!view.Model.SameDisplay(model)) {
                view.Model = model;
                MaximumUIRuntime.MarkDirty(view);
            }
        }

        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup() {
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

        [HarmonyPostfix, HarmonyPatch(typeof(HFlag), "FemaleGaugeUp")]
        private static void OnFemaleGauge(HFlag __instance) {
            HSControlPlugin plugin = HSControlPlugin.Instance;
            if (plugin != null) plugin.AfterFemaleGaugeUp(__instance);
        }
    }
}
