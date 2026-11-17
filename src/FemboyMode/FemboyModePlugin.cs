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
        public const string Version = "0.3.0";

        internal static ConfigEntry<bool> EnableFreeHRestrictions;
        internal static ConfigEntry<bool> EnableMakerAssetUnlock;
        internal static ConfigEntry<bool> EnableMaleBoneScale;
        internal static ConfigEntry<string> MaleGeometryBone;
        internal static ConfigEntry<bool> EnableMaleClimaxFX;
        internal static ConfigEntry<bool> SuppressOriginalFemaleFX;
        internal static ConfigEntry<float> ClimaxFXOffsetX, ClimaxFXOffsetY, ClimaxFXOffsetZ;
        internal static ConfigEntry<float> ClimaxFXRotationX, ClimaxFXRotationY, ClimaxFXRotationZ;
        internal static ConfigEntry<bool> VerboseLogging;
        internal static ConfigEntry<bool> EnableKokanAnchor;
        internal static ConfigEntry<string> KokanAnchorBone;
        internal static ConfigEntry<bool> EnableKokanHitCollider;
        internal static ConfigEntry<float> KokanHitRadius;
        internal static ConfigEntry<float> KokanOffsetX;
        internal static ConfigEntry<float> KokanOffsetY;
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
        private MakerSlider boneScaleX, boneScaleY, boneScaleZ;
        private static readonly FieldInfo MakerControlObjects =
            typeof(BaseGuiEntry).GetField("_controlObjects",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private bool refreshing;

        private void Awake()
        {
            Log = Logger;
            EnableFreeHRestrictions = Config.Bind("Free H", "Restrict front insertion", true,
                "Hide non-anal insert actions for marked female characters in vanilla two-person Free H.");
            EnableMakerAssetUnlock = Config.Bind("Maker", "Unlock male-listed assets", true,
                "Permit male-listed assets in a marked female character's Maker. Existing lists may need reopening.");
            EnableMaleBoneScale = Config.Bind("Maker", "Enable male geometry bone scaling", true,
                "Apply per-card X/Y/Z scaling to the configured male geometry bone when available.");
            MaleGeometryBone = Config.Bind("Maker", "Male geometry bone", "cm_j_dan100",
                "Bone for optional X/Y/Z body shape sliders; independent of Aibu target bone.");
            VerboseLogging = Config.Bind("Debug", "Verbose logs", false,
                "Log character marker changes and integration hook decisions.");
            EnableMaleClimaxFX = Config.Bind("Free H", "Use male-style finish FX", true,
                "On a marked female character's climax, clone the native male outside-finish particle at her model's target bone; original actor flags are preserved.");
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
            KokanAnchorBone = Config.Bind("Aibu alignment", "Target bone", "cm_j_dan100",
                "Exact transform name to attach the Kokan action item to. Does nothing if this bone is absent.");
            EnableKokanHitCollider = Config.Bind("Aibu alignment", "Enable target hit collider", true,
                "Provide a new Kokan raycast target around the re-anchored male geometry while leaving original colliders untouched.");
            KokanHitRadius = Config.Bind("Aibu alignment", "Target hit radius", 0.05f,
                "Radius of the new Kokan sphere collider (Unity units, clamped 0.005 to 0.25).");
            KokanOffsetX = Config.Bind("Aibu alignment", "Offset X", 0f,
                "Local X offset relative to the target bone (Unity units).");
            KokanOffsetY = Config.Bind("Aibu alignment", "Offset Y", 0f,
                "Local Y offset relative to the target bone (Unity units).");
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
            MakerAPI.ChaFileLoaded += OnMakerCardLoaded;
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(FemboyHSceneHooks));
            harmony.PatchAll(typeof(FemboyMakerAssetHooks));
            harmony.PatchAll(typeof(FemboyAibuAnchorHooks));
            harmony.PatchAll(typeof(FemboyClimaxHooks));
            UncensorSelectorCompat.TryPatch(harmony);
            Logger.LogInfo("Femboy Mode " + Version + ": character flag and optional integrations ready.");
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            Log = null;
            MakerAPI.RegisterCustomSubCategories -= RegisterMakerControls;
            MakerAPI.MakerFinishedLoading -= OnMakerFinishedLoading;
            MakerAPI.ReloadCustomInterface -= OnReloadCustomInterface;
            MakerAPI.ChaFileLoaded -= OnMakerCardLoaded;
        }

        private void RegisterMakerControls(object sender, RegisterSubCategoriesEvent e)
        {
            // Female cards only. No changes to the built-in sex field.
            if (MakerAPI.GetMakerSex() != 1)
                return;

            flagToggle = e.AddControl(new MakerToggle(
                MakerConstants.Parameter.Character, "伪娘角色标志 (Femboy)", this));
            bodyFlagToggle = e.AddControl(new MakerToggle(
                MakerConstants.Body.All, "伪娘角色标志 (Femboy)", this));
            flagToggle.ValueChanged.Subscribe(OnMakerFlagChanged);
            bodyFlagToggle.ValueChanged.Subscribe(OnMakerFlagChanged);
            // These controls are purpose-built for the optional male mesh.
            // They do not pretend to be missing native female Maker sliders.
            boneScaleX = e.AddControl(new MakerSlider(
                MakerConstants.Body.All, "男性骨骼 X 缩放", 0.5f, 2f, 1f, this));
            boneScaleY = e.AddControl(new MakerSlider(
                MakerConstants.Body.All, "男性骨骼 Y 缩放", 0.5f, 2f, 1f, this));
            boneScaleZ = e.AddControl(new MakerSlider(
                MakerConstants.Body.All, "男性骨骼 Z 缩放", 0.5f, 2f, 1f, this));
            boneScaleX.ValueChanged.Subscribe(v => OnMakerScaleChanged(0, v));
            boneScaleY.ValueChanged.Subscribe(v => OnMakerScaleChanged(1, v));
            boneScaleZ.ValueChanged.Subscribe(v => OnMakerScaleChanged(2, v));
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

        private void OnMakerScaleChanged(int axis, float value)
        {
            if (refreshing || !MakerAPI.InsideAndLoaded)
                return;

            FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
            if (controller == null)
                return;

            if (axis == 0) controller.GeometryScaleX = value;
            else if (axis == 1) controller.GeometryScaleY = value;
            else controller.GeometryScaleZ = value;
        }

        private void OnMakerFinishedLoading(object sender, EventArgs e)
        {
            RefreshMakerToggle();
        }

        private void OnReloadCustomInterface(object sender, EventArgs e)
        {
            RefreshMakerToggle();
        }

        private void OnMakerCardLoaded(object sender, EventArgs e)
        {
            StartCoroutine(RefreshAfterCardLoaded());
        }

        private IEnumerator RefreshAfterCardLoaded()
        {
            // Card-load and Maker-UI events can run in different frames.
            yield return null;
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
                if (boneScaleX != null)
                    boneScaleX.SetValue(controller == null ? 1f : controller.GeometryScaleX);
                if (boneScaleY != null)
                    boneScaleY.SetValue(controller == null ? 1f : controller.GeometryScaleY);
                if (boneScaleZ != null)
                    boneScaleZ.SetValue(controller == null ? 1f : controller.GeometryScaleZ);
                SetMakerControlVisible(boneScaleX, enabled && EnableMaleBoneScale.Value);
                SetMakerControlVisible(boneScaleY, enabled && EnableMaleBoneScale.Value);
                SetMakerControlVisible(boneScaleZ, enabled && EnableMaleBoneScale.Value);
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
