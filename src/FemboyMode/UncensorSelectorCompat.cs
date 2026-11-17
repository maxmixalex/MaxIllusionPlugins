using System;
using System.Reflection;
using HarmonyLib;
using KKAPI.Maker;

namespace FemboyMode
{
    /// <summary>
    /// Optional integration with KK_UncensorSelector. Its "GenderBender"
    /// setting would unlock cross-sex bodies globally; this postfix enables
    /// the selected male body entry only while building a marked female Maker.
    /// </summary>
    internal static class UncensorSelectorCompat
    {
        private static bool installed;

        internal static void TryPatch(Harmony harmony)
        {
            Type selectorType = AccessTools.TypeByName("KK_Plugins.UncensorSelector");
            if (selectorType == null)
                return;

            MethodInfo original = AccessTools.Method(selectorType, "BodyAllowedInMaker",
                new[] { typeof(byte), typeof(byte) });
            MethodInfo postfix = AccessTools.Method(typeof(UncensorSelectorCompat),
                nameof(BodyAllowedInMakerPostfix));

            if (original == null || postfix == null)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "UncensorSelector API BodyAllowedInMaker(byte, byte) was not found.");
                return;
            }

            try
            {
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                installed = true;
                if (FemboyModePlugin.VerboseLogging.Value)
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "UncensorSelector BodyAllowedInMaker integration installed.");
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Failed to patch UncensorSelector: " + ex.Message);
            }
        }

        private static void BodyAllowedInMakerPostfix(
            byte bodySex, byte characterSex, ref bool __result)
        {
            if (!installed || __result || bodySex != 0 || characterSex != 1 ||
                FemboyModePlugin.EnableMakerAssetUnlock == null ||
                !FemboyModePlugin.EnableMakerAssetUnlock.Value ||
                !MakerAPI.InsideMaker)
                return;

            ChaControl character = MakerAPI.GetCharacterControl();
            if (character != null &&
                FemboyModePlugin.IsFemboy(character))
                __result = true;
        }
    }
}
