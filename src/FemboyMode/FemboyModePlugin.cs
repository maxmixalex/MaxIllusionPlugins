using System;
using System.Collections;
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
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInProcess(KoikatuAPI.GameProcessNameSteam)]
    public sealed class FemboyModePlugin : BaseUnityPlugin
    {
        public const string Guid = "MaxMixAlex.FemboyMode";
        public const string PluginName = "Femboy Mode";
        public const string Version = "0.2.0";

        internal static ConfigEntry<bool> EnableFreeHRestrictions;
        internal static ConfigEntry<bool> EnableMakerAssetUnlock;
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
        private bool refreshing;

        private void Awake()
        {
            Log = Logger;
            EnableFreeHRestrictions = Config.Bind("Free H", "Restrict front insertion", true,
                "Hide non-anal insert actions for marked female characters in vanilla two-person Free H.");
            EnableMakerAssetUnlock = Config.Bind("Maker", "Unlock male-listed assets", true,
                "Permit male-listed assets in a marked female character's Maker. Existing lists may need reopening.");
            VerboseLogging = Config.Bind("Debug", "Verbose logs", false,
                "Log character marker changes and integration hook decisions.");

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
                MakerConstants.Parameter.Character,
                "伪娘角色标志 (Femboy)",
                this));
            flagToggle.ValueChanged.Subscribe(delegate(bool enabled)
            {
                if (refreshing || !MakerAPI.InsideAndLoaded)
                    return;

                FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
                if (controller != null)
                {
                    controller.IsFemboy = enabled;
                    if (VerboseLogging.Value)
                        Logger.LogInfo("Maker character Femboy flag changed to " + enabled);
                }
            });
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

        private void RefreshMakerToggle()
        {
            if (flagToggle == null || !MakerAPI.InsideAndLoaded)
                return;

            FemboyCharacterController controller = GetController(MakerAPI.GetCharacterControl());
            refreshing = true;
            try
            {
                flagToggle.SetValue(controller != null && controller.IsFemboy);
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
            FemboyCharacterController controller = GetController(character);
            return controller != null && controller.IsFemboy;
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
