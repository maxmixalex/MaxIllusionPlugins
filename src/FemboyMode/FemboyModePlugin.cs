using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using HarmonyLib;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using UniRx;
using UnityEngine;

namespace FemboyMode
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID)]
    [BepInDependency("com.deathweasel.bepinex.uncensorselector", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    public sealed class FemboyModePlugin : BaseUnityPlugin
    {
        public const string Guid = "MaxMixAlex.FemboyMode";
        public const string PluginName = "Femboy Mode";
        public const string Version = "0.5.0";

        internal static ConfigEntry<bool> EnableFreeHRestrictions;
        internal static ConfigEntry<bool> EnableReverseHoushi;
        internal static ConfigEntry<bool> ReverseHoushiSwapVisualRoots;
        internal static ConfigEntry<bool> ReverseHoushiSwapMouthExpressions;
        internal static ConfigEntry<bool> ReverseHoushiRedirectSiru;
        internal static ConfigEntry<bool> ShowFreeHMarker;
        internal static ConfigEntry<bool> EnableMakerAssetUnlock;
        internal static ConfigEntry<bool> EnableMaleBoneScale;
        internal static ConfigEntry<bool> EnableMaleGeometryTransforms;
        internal static ConfigEntry<string> MaleGeometryBone;
        internal static ConfigEntry<string> MaleBallsBone;
        internal static ConfigEntry<bool> EnableMaleClimaxFX;
        internal static ConfigEntry<bool> EnablePhysicalFluid;
        internal static ConfigEntry<float> PhysicalFluidAmount;
        internal static ConfigEntry<float> FluidLaunchSpeed;
        internal static ConfigEntry<float> FluidLocalPitch;
        internal static ConfigEntry<float> FluidSpread;
        internal static ConfigEntry<bool> EnableMaleFinishAnimation;
        internal static ConfigEntry<bool> AllowExperimentalControllerSwap;
        internal static ConfigEntry<bool> EnableMaleBonePoseFollow;
        internal static ConfigEntry<bool> SuppressFemaleClimaxVoice;
        internal static ConfigEntry<string> MaleFinishBone;
        internal static ConfigEntry<bool> SuppressOriginalFemaleFX;
        internal static ConfigEntry<float> ClimaxFXOffsetX, ClimaxFXOffsetY, ClimaxFXOffsetZ;
        internal static ConfigEntry<float> ClimaxFXRotationX, ClimaxFXRotationY, ClimaxFXRotationZ;
        internal static ConfigEntry<bool> VerboseLogging;
        internal static ConfigEntry<KeyboardShortcut> DumpRigHotkey;
        internal static ConfigEntry<bool> EnableKokanAnchor;
        internal static ConfigEntry<bool> DisablePhallusToy;
        internal static ConfigEntry<string> KokanAnchorBone;
        internal static ConfigEntry<bool> EnableKokanHitCollider;
        internal static ConfigEntry<float> KokanHitRadius;
        internal static ConfigEntry<float> KokanTipFraction;
        internal static ConfigEntry<string> KokanTipBone;
        internal static ConfigEntry<float> KokanOffsetX;
        internal static ConfigEntry<float> KokanOffsetY;
        internal static ConfigEntry<float> KokanWorldHeightCorrection;
        internal static ConfigEntry<float> KokanHandOffsetX, KokanHandOffsetY, KokanHandOffsetZ;
        internal static ConfigEntry<float> KokanOffsetZ;
        internal static ConfigEntry<float> KokanRotationX;
        internal static ConfigEntry<float> KokanRotationY;
        internal static ConfigEntry<float> KokanRotationZ;
        internal static ManualLogSource Log;

        private static bool warnedMissingAibuField;
        private static bool warnedMissingAibuBone;

        private Harmony harmony;
        private MakerToggle flagToggle;
        private MakerToggle bodyFlagToggle;
        private readonly MakerSlider[,,] geometrySliders = new MakerSlider[2,3,3];
        private readonly MakerToggle[] axisToggles = new MakerToggle[2];
        private readonly bool[] axisVisible = new bool[2];
        private readonly FemboyMakerAxisPreview axisPreview = new FemboyMakerAxisPreview();
        private static readonly FieldInfo MakerControlObjects =
            typeof(BaseGuiEntry).GetField("_controlObjects",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private bool refreshing;

        private void Awake()
        {
            Log = Logger;
            EnableFreeHRestrictions = Config.Bind("Free H", "Restrict front insertion", true,
                "Hide non-anal insert actions for marked female characters in vanilla two-person Free H.");
            EnableReverseHoushi = Config.Bind("Free H", "Add reversed Houshi actions", true,
                "Clone native and imported service actions as [Reversed], exchange body roles and optionally exchange visual mouth expression channels while preserving the native animation controllers and imported assets.");
            ReverseHoushiSwapVisualRoots = Config.Bind("Free H",
                "Reversed Houshi exchange visible actor positions", true,
                "Visually exchange the two animated character body roots during a reversed service action, restoring native transforms before the next animation frame. Disable if third-party IK or camera mods rely on the original actor body positions.");
            ReverseHoushiSwapMouthExpressions = Config.Bind("Free H",
                "Reversed Houshi swap mouth expressions", true,
                "In reversed service actions, move the original heroine's mouth pattern and opening to the male performer, and apply the original male's mouth to the marked heroine. Restores native expression state before the next game update; turn off if a third-party face mod conflicts.");
            ReverseHoushiRedirectSiru = Config.Bind("Free H",
                "Reversed Houshi redirect Siru stains", true,
                "When a marked heroine visually switches into the giver role, transfer new native Siru overlay writes to the original male instead. Keeps native cleanup and ordinary H unchanged.");
            ShowFreeHMarker = Config.Bind("Free H", "Show top-left Femboy marker", true,
                "Display a distinctive mark at the upper-left of Free H for the currently selected marked female character.");
            EnableMakerAssetUnlock = Config.Bind("Maker", "Unlock male-listed assets", true,
                "Permit male-listed assets in a marked female character's Maker. Existing lists may need reopening.");
            EnableMaleBoneScale = Config.Bind("Maker", "Enable male geometry bone scaling", true,
                "Apply saved per-card Penis X/Y/Z scaling. Position and rotation remain independent.");
            EnableMaleGeometryTransforms = Config.Bind("Maker", "Enable male genital bone transforms", true,
                "Apply per-card Penis/Balls position, rotation and scale. Disable to restore original bone transforms.");
            MaleGeometryBone = Config.Bind("Maker", "Male geometry bone", "cm_j_dan100",
                "Penis root bone (usually cm_J_dan100_00), independent of Aibu anchor.");
            MaleBallsBone = Config.Bind("Maker", "Male balls bone", "cm_J_dan_f_top",
                "Independent balls root. If missing, skip transformation; never fall back to the pelvic root.");
            VerboseLogging = Config.Bind("Debug", "Verbose logs", false,
                "Log character marker changes and integration hook decisions.");
            DumpRigHotkey = Config.Bind("Diagnostics", "Dump rig and animation names",
                new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl, KeyCode.LeftShift),
                "Explicitly log the current Maker/Free H character rig, finish clip names and installed body GUIDs. Does not log cards or personal names.");
            EnableMaleClimaxFX = Config.Bind("Free H", "Use male-style finish FX", true,
                "On a marked female character's climax, clone the native male outside-finish particle at her model's target bone; original actor flags are preserved.");
            EnablePhysicalFluid = Config.Bind("Free H", "Generate native physical male fluid", true,
                "For a marked character's Aibu/Sonyu climax, trigger the game's real MetaballShoot physics and UpdateMeta mesh instead of only the LiquidSiru particle. Respects the game's SemenType=off setting; no effect if required assets are unavailable.");
            PhysicalFluidAmount = Config.Bind("Male fluid", "Amount multiplier", 1f,
                new ConfigDescription(
                    "Multiplier for native physical fluid droplets in Aibu and insertion. 1.0 uses the original preset count; 2.0 doubles it (maximum 80 droplets). Affects real MetaballShoot objects, not just visual particles.",
                    new AcceptableValueRange<float>(0.25f, 5f)));
            // New key names deliberately do not inherit the 0.4.3
            // absolute-world configuration saved in the user's BepInEx cfg.
            // Old 2.5 speed + 18 degree world elevation overshot the target
            // and failed to rotate with reclining poses.
            FluidLaunchSpeed = Config.Bind("Male fluid", "Model-relative launch speed",
                FemboyRules.DefaultFluidLaunchSpeed,
                new ConfigDescription(
                    "Initial native Metaball projectile velocity along the character's actual shaft direction (not global horizontal). 0.85 is the conservative RX18 default; larger values increase range.",
                    new AcceptableValueRange<float>(0.2f, 4f)));
            FluidLocalPitch = Config.Bind("Male fluid", "Local pitch relative to shaft degrees",
                FemboyRules.DefaultFluidLocalPitch,
                new ConfigDescription(
                    "Additional pitch in the shaft bone's LOCAL frame, following the character when reclining, rotating, or turning. 0 follows the actual root-to-tip direction.",
                    new AcceptableValueRange<float>(-30f, 30f)));
            FluidSpread = Config.Bind("Male fluid", "Launch spread degrees", 6f,
                new ConfigDescription(
                    "Maximum random launch-axis deviation. Applied to native MetaballShoot rotations.",
                    new AcceptableValueRange<float>(0f, 30f)));
            EnableMaleFinishAnimation = Config.Bind("Free H", "Retarget compatible male climax animation", true,
                "During the marked heroine's climax, use the native male controller only if both avatars are Humanoid and the male controller contains the exact state. Generic/unsupported rigs keep vanilla animation.");
            AllowExperimentalControllerSwap = Config.Bind("Free H", "Allow experimental full-controller swap", false,
                "SAFETY: off by default. Existing full-controller retargeting can truncate the heroine's finish sequence. Leave this disabled until the exact pose transition has been verified; the add-on bone animation works independently.");
            EnableMaleBonePoseFollow = Config.Bind("Free H", "Copy native male geometry climax motion", true,
                "For Generic rigs or imported male geometry, copy only matching native cm_J_dan100..109 bone rotations from the animated male to the marked actor during heroine climax. Keep the female full-body animation, bone positions/scales and all unmarked characters unchanged.");
            SuppressFemaleClimaxVoice = Config.Bind("Free H", "Suppress female climax voice", true,
                "Suppress only marked heroine's native female orgasm dialogue cues. Koikatsu does not provide an equivalent male voice channel; other dialogue and characters are unchanged.");
            MaleFinishBone = Config.Bind("Male finish FX", "Target bone",
                FemboyRules.DefaultMaleFinishBone,
                "Native LiquidSiru particle parent bone. Independent of Aibu alignment Target bone.");
            SuppressOriginalFemaleFX = Config.Bind("Free H", "Suppress female Aibu squirt FX", true,
                "When the marked heroine has alternate finish FX enabled, suppress the original female particle (Aibu only).");
            ClimaxFXOffsetX = Config.Bind("Male finish FX", "Offset X", 0f, "Local FX offset X in Unity units.");
            ClimaxFXOffsetY = Config.Bind("Male finish FX", "Offset Y", 0f, "Local FX offset Y in Unity units.");
            ClimaxFXOffsetZ = Config.Bind("Male finish FX", "Offset Z", 0f, "Local FX offset Z in Unity units.");
            ClimaxFXRotationX = Config.Bind("Male finish FX", "Rotation X", 0f, "Local FX rotation X in degrees.");
            ClimaxFXRotationY = Config.Bind("Male finish FX", "Rotation Y", 0f, "Local FX rotation Y in degrees.");
            ClimaxFXRotationZ = Config.Bind("Male finish FX", "Rotation Z", 0f, "Local FX rotation Z in degrees.");

            EnableKokanAnchor = Config.Bind("Aibu alignment", "Enable Kokan action anchor", true,
                "For a marked female character, place Kokan hand items at a named male-geometry bone in Free H.");
            DisablePhallusToy = Config.Bind("Aibu alignment", "Hide phallus toy after vibrator", true,
                "Remove the final base-game p_item_vibe (fake phallus, icon25, IDs7/8) from marked Kokan item cycles. Keep the preceding denma vibrator (IDs5/6). Normal characters are unchanged.");
            KokanAnchorBone = Config.Bind("Aibu alignment", "Target bone", "cm_j_dan100",
                "Exact transform name to attach the Kokan action item to. Does nothing if this bone is absent.");
            EnableKokanHitCollider = Config.Bind("Aibu alignment", "Enable target hit collider", true,
                "Provide a new Kokan raycast target around the re-anchored male geometry while leaving original colliders untouched.");
            KokanHitRadius = Config.Bind("Aibu alignment", "Target hit radius", 0.05f,
                "Radius of the new Kokan sphere collider (Unity units, clamped 0.005 to 0.25).");
            KokanTipFraction = Config.Bind("Aibu alignment", "Aim along shaft (0 root, 1 tip)", 0.50f,
                "Compute the real scaled contact point between the base cm_J_dan100 bone and end cm_J_dan109 bone. Clamp 0..1. Local X/Y/Z offsets still work.");
            KokanTipBone = Config.Bind("Aibu alignment", "Shaft tip bone", FemboyRules.DefaultMaleFinishBone,
                "Name of the actual tip bone used for scale-aware Kokan center placement; defaults to cm_J_dan109_00.");
            KokanOffsetX = Config.Bind("Aibu alignment", "Offset X", 0f,
                "Local X offset relative to the target bone (Unity units).");
            KokanOffsetY = Config.Bind("Aibu alignment", "Offset Y", 0f,
                "Local Y offset relative to the target bone (Unity units).");
            KokanWorldHeightCorrection = Config.Bind("Aibu alignment", "World Y correction", -0.012f,
                "Extra vertical adjustment in world units, independent of shaft orientation. Default lowers the Free H Kokan touch marker by 0.012 units; adjust in ConfigurationManager for different models.");
            KokanHandOffsetX = Config.Bind("Aibu alignment", "Hand correction world X", 0f,
                "Additional WORLD-space shift applied only to native hand item animations, not their body bones or mouse collider.");
            KokanHandOffsetY = Config.Bind("Aibu alignment", "Hand correction world Y", 0f,
                "Additional WORLD-space vertical hand shift beyond the corrected Kokan touch target.");
            KokanHandOffsetZ = Config.Bind("Aibu alignment", "Hand correction world Z", 0f,
                "Additional WORLD-space depth shift applied only to native hand item animations.");
            KokanOffsetZ = Config.Bind("Aibu alignment", "Offset Z", 0f,
                "Local Z offset relative to the target bone (Unity units).");
            KokanRotationX = Config.Bind("Aibu alignment", "Rotation X", 0f,
                "Local X rotation of the action anchor (degrees).");
            KokanRotationY = Config.Bind("Aibu alignment", "Rotation Y", 0f,
                "Local Y rotation of the action anchor (degrees).");
            KokanRotationZ = Config.Bind("Aibu alignment", "Rotation Z", 0f,
                "Local Z rotation of the action anchor (degrees).");
            CharacterApi.RegisterExtraBehaviour<FemboyCharacterController>(Guid);
            MakerAPI.RegisterCustomSubCategories += RegisterMakerControls;
            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MakerAPI.ReloadCustomInterface += OnReloadCustomInterface;
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(FemboyHSceneHooks));
            harmony.PatchAll(typeof(FemboyHoushiReverseHooks));
            harmony.PatchAll(typeof(FemboyReversedSiruHooks));
            harmony.PatchAll(typeof(FemboyHMarkerHooks));
            harmony.PatchAll(typeof(FemboyMakerAssetHooks));
            harmony.PatchAll(typeof(FemboyAibuAnchorHooks));
            harmony.PatchAll(typeof(FemboyClimaxHooks));
            harmony.PatchAll(typeof(FemboyPhysicalFluidHooks));
            harmony.PatchAll(typeof(FemboyVoiceHooks));
            harmony.PatchAll(typeof(FemboyMaleAnimationHooks));
            UncensorSelectorCompat.TryPatch(harmony);
            Logger.LogInfo("Femboy Mode " + Version + ": character flag and optional integrations ready.");
        }

        private void Update()
        {
            if (DumpRigHotkey != null && DumpRigHotkey.Value.IsDown())
                FemboyDiagnostics.DumpCurrent();
            bool active = MakerAPI.InsideAndLoaded &&
                          EnableMaleGeometryTransforms != null && EnableMaleGeometryTransforms.Value;
            FemboyCharacterController controller = active ?
                GetController(MakerAPI.GetCharacterControl()) : null;
            axisPreview.Tick(controller != null && controller.IsFemboy
                ? controller : null, axisVisible);
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            FemboyMaleAnimationHooks.ResetAll();
            FemboyAibuAnchorHooks.ResetAll();
            axisPreview.Dispose();
            Log = null;
            MakerAPI.RegisterCustomSubCategories -= RegisterMakerControls;
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.ReloadCustomInterface -= OnReloadCustomInterface;
        }

        private void RegisterMakerControls(object sender, RegisterSubCategoriesEvent e)
        {
            if (MakerAPI.GetMakerSex() != 1) return;

            flagToggle = e.AddControl(new MakerToggle(
                MakerConstants.Parameter.Character, "伪娘角色标志 (Femboy)", this));
            bodyFlagToggle = e.AddControl(new MakerToggle(
                MakerConstants.Body.All, "伪娘角色标志 (Femboy)", this));
            flagToggle.ValueChanged.Subscribe(OnMakerFlagChanged);
            bodyFlagToggle.ValueChanged.Subscribe(OnMakerFlagChanged);

            var penisPage = new MakerCategory(
                MakerConstants.Body.All.CategoryName, "FemboyPenis", int.MaxValue - 2, "男性器调整");
            var ballsPage = new MakerCategory(
                MakerConstants.Body.All.CategoryName, "FemboyBalls", int.MaxValue - 1, "睾丸调整");
            e.AddSubCategory(penisPage);
            e.AddSubCategory(ballsPage);
            for (int part = 0; part < 2; part++)
            {
                int selectedPart = part;
                MakerCategory page = part == 0 ? penisPage : ballsPage;
                axisVisible[part] = false;
                for (int mode = 0; mode < 3; mode++)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int selectedMode = mode, selectedAxis = axis;
                        string modeLabel = mode == 0 ? "缩放" :
                            mode == 1 ? "位移（米）" : "旋转（°）";
                        string axisLabel = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
                        float min = mode == 0 ? 0.5f : mode == 1 ? -0.1f : -180f;
                        float max = mode == 0 ? 2f : mode == 1 ? 0.1f : 180f;
                        geometrySliders[part,mode,axis] = e.AddControl(new MakerSlider(
                            page, modeLabel + " " + axisLabel, min, max, mode == 0 ? 1f : 0f, this));
                        geometrySliders[part,mode,axis].ValueChanged.Subscribe(
                            v => OnMakerGeometryChanged(selectedPart, selectedMode, selectedAxis, v));
                    }
                axisToggles[part] = e.AddControl(new MakerToggle(
                    page, "显示轴（预览）", this));
                axisToggles[part].ValueChanged.Subscribe(v =>
                {
                    if (!refreshing && MakerAPI.InsideAndLoaded)
                        axisVisible[selectedPart] = v;
                });
            }
        }

        private void OnMakerFlagChanged(bool enabled)
        {
            if (refreshing || !MakerAPI.InsideAndLoaded)
                return;

            FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
            if (controller == null) return;

            controller.IsFemboy = enabled;
            RefreshMakerToggle();
            if (VerboseLogging.Value)
                Logger.LogInfo("Maker character Femboy flag changed to " + enabled);
        }

        private void OnMakerGeometryChanged(int part, int mode, int axis, float value)
        {
            if (refreshing || !MakerAPI.InsideAndLoaded) return;
            FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
            if (controller != null && controller.IsFemboy)
                controller.SetGeometryValue(part, mode, axis, value);
        }

        private void OnMakerFinishedLoading(object sender, EventArgs e)
        {
            RefreshMakerToggle();
        }

        private void OnReloadCustomInterface(object sender, EventArgs e)
        {
            RefreshMakerToggle();
        }

        private static void SetMakerControlVisible(BaseGuiEntry control, bool visible)
        {
            if (control == null || MakerControlObjects == null)
                return;
            IList objects = MakerControlObjects.GetValue(control) as IList;
            if (objects == null) return;

            foreach (object entry in objects)
            {
                UnityEngine.GameObject widget = entry as UnityEngine.GameObject;
                if (widget != null && widget.activeSelf != visible)
                    widget.SetActive(visible);
            }
        }

        private void RefreshMakerToggle()
        {
            if (!MakerAPI.InsideAndLoaded)
                return;

            FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
            bool enabled = controller != null && controller.IsFemboy;
            refreshing = true;
            try
            {
                if (flagToggle != null) flagToggle.SetValue(enabled);
                if (bodyFlagToggle != null) bodyFlagToggle.SetValue(enabled);
                for (int part = 0; part < 2; part++)
                {
                    for (int mode = 0; mode < 3; mode++)
                        for (int axis = 0; axis < 3; axis++)
                        {
                            MakerSlider slider = geometrySliders[part,mode,axis];
                            if (slider == null) continue;
                            slider.SetValue(controller == null
                                ? (mode == 0 ? 1f : 0f)
                                : controller.GetGeometryValue(part, mode, axis));
                            bool visible = enabled && EnableMaleGeometryTransforms.Value &&
                                (part != 0 || mode != 0 || EnableMaleBoneScale.Value);
                            SetMakerControlVisible(slider, visible);
                        }
                    if (axisToggles[part] != null)
                    {
                        axisToggles[part].SetValue(axisVisible[part]);
                        SetMakerControlVisible(axisToggles[part],
                            enabled && EnableMaleGeometryTransforms.Value);
                    }
                }
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// Stable integration entry point for external plugins.
        /// </summary>
        public static bool IsFemboy(ChaControl character)
        {
            if (character == null || character.sex != 1)
                return false;

            FemboyCharacterController controller = GetController(character);
            if (controller != null && controller.IsInitialized)
            {
                // The in-memory Maker toggle wins over stale saved card data,
                // including when the user just unchecked it without saving.
                return controller.IsFemboy;
            }

            // Early-scene checks may precede KKAPI's first controller reload.
            return character.chaFile != null && IsFemboy(character.chaFile);
        }

        /// <summary>
        /// Inspect a saved character card even when no ChaControl is instantiated.
        /// Useful for card selectors, external plugins and scene preflight checks.
        /// </summary>
        public static bool IsFemboy(ChaFileControl card)
        {
            if (card == null || card.parameter == null || card.parameter.sex != 1)
                return false;

            PluginData data = ExtendedSave.GetExtendedDataById(card, Guid);
            object savedValue;
            return data != null && data.data != null &&
                   data.data.TryGetValue("IsFemboy", out savedValue) &&
                   savedValue is bool && (bool)savedValue;
        }

        internal static void LogOptionalIntegrationProblem(string message)
        {
            if (Log != null) Log.LogInfo(message);
        }

        internal static void WarnMissingAibuField()
        {
            if (warnedMissingAibuField || Log == null) return;
            warnedMissingAibuField = true;
            Log.LogWarning("HandCtrl.dicAreaLayerInfos was not found. Kokan anchor integration disabled.");
        }

        internal static void LogMissingAibuBone(string boneName)
        {
            if (warnedMissingAibuBone || Log == null) return;
            warnedMissingAibuBone = true;
            Log.LogWarning("Kokan target bone '" + boneName +
                "' not found on marked character. Vanilla action anchor was preserved.");
        }

        internal static void LogFallbackAibuBone(string missingBone)
        {
            if (Log != null && VerboseLogging != null && VerboseLogging.Value)
                Log.LogInfo("Aibu target bone '" + missingBone +
                    "' unavailable; using cf_d_kokan with configurable offsets.");
        }

        internal static void LogAibuBound(string boneName, int count)
        {
            if (Log != null)
                Log.LogInfo("Bound " + count + " Kokan item parents to " + boneName + ".");
        }

        public static FemboyCharacterController GetController(ChaControl character)
        {
            return character == null ? null :
                character.gameObject.GetComponent<FemboyCharacterController>();
        }
    }
}
