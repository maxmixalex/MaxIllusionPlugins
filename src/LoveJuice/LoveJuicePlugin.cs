using BepInEx;
using BepInEx.Configuration;

[assembly: System.Reflection.AssemblyFileVersion(LoveJuice.LoveJuicePlugin.Version)]

namespace LoveJuice {
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
    [BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
    [BepInPlugin(GUID, Name, Version)]
    public sealed class LoveJuicePlugin : BaseUnityPlugin {
        public const string GUID = "MaxMixAlex.LoveJuice";
        public const string Name = "LoveJuice";
        public const string Version = "0.1.0";

        private ConfigEntry<bool> enabledSetting;

        public bool IsEnabled {
            get { return enabledSetting != null && enabledSetting.Value; }
        }

        private void Awake() {
            enabledSetting = Config.Bind("General", "Enabled", false,
                "Master enable setting. Version 0.1.0 is a scaffold and has no runtime effects.");
            Logger.LogInfo(Name + " " + Version + " loaded (scaffold).");
        }
    }
}
