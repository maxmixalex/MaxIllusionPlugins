using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
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

        private Harmony harmony;
        private MakerToggle flagToggle;
        private bool refreshing;

        private void Awake()
        {
            EnableFreeHRestrictions = Config.Bind("Free H", "Restrict front insertion", true,
                "Hide non-anal insert actions for marked female characters in vanilla two-person Free H.");
            EnableMakerAssetUnlock = Config.Bind("Maker", "Unlock male-listed assets", true,
                "Permit male-listed assets in a marked female character's Maker. Existing lists may need reopening.");
            VerboseLogging = Config.Bind("Debug", "Verbose logs", false,
                "Log character marker changes and integration hook decisions.");
            CharacterApi.RegisterExtraBehaviour<FemboyCharacterController>(Guid);
            MakerAPI.RegisterCustomSubCategories += RegisterMakerControls;
            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MakerAPI.ReloadCustomInterface += OnReloadCustomInterface;
            MakerAPI.ChaFileLoaded += OnMakerCardLoaded;
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(FemboyHSceneHooks));
            harmony.PatchAll(typeof(FemboyMakerAssetHooks));
            Logger.LogInfo("Femboy Mode " + Version + ": character flag and optional integrations ready.");
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
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

        public static FemboyCharacterController GetController(ChaControl character)
        {
            return character == null ? null :
                character.gameObject.GetComponent<FemboyCharacterController>();
        }
    }
}
