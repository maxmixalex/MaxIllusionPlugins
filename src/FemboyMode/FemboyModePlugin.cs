using System;
using System.Collections;
using BepInEx;
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
        public const string Version = "0.1.0";

        private MakerToggle flagToggle;
        private bool refreshing;

        private void Awake()
        {
            CharacterApi.RegisterExtraBehaviour<FemboyCharacterController>(Guid);
            MakerAPI.RegisterCustomSubCategories += RegisterMakerControls;
            MakerAPI.MakerFinishedLoading += OnMakerFinishedLoading;
            MakerAPI.ReloadCustomInterface += OnReloadCustomInterface;
            MakerAPI.ChaFileLoaded += OnMakerCardLoaded;
            Logger.LogInfo("Femboy Mode: per-character flag and card persistence enabled.");
        }

        private void OnDestroy()
        {
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
                    controller.IsFemboy = enabled;
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
