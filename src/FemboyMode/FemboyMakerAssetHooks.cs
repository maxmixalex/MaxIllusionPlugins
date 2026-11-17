using HarmonyLib;
using KKAPI.Maker;

namespace FemboyMode
{
    /// <summary>
    /// In the marked female Maker, allow assets marked male-only in the
    /// character-list metadata to pass the existing sex filter.
    /// This does not splice the male-body editor hierarchy into the
    /// female editor and does not replace the character mesh.
    /// </summary>
    internal static class FemboyMakerAssetHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ListInfoBase), nameof(ListInfoBase.GetInfoInt))]
        private static void GetInfoIntPostfix(ChaListDefine.KeyType keyType, ref int __result)
        {
            if (keyType != ChaListDefine.KeyType.Sex || !MakerAPI.InsideAndLoaded ||
                FemboyModePlugin.EnableMakerAssetUnlock == null ||
                !FemboyModePlugin.EnableMakerAssetUnlock.Value)
                return;

            ChaControl character = MakerAPI.GetCharacterControl();
            if (character != null && character.sex == 1 &&
                FemboyModePlugin.IsFemboy(character))
            {
                // The female Maker's native asset filter accepts sex=1.
                // Do not modify ListInfoBase outside the marked Maker.
                __result = 1;
            }
        }
    }
}
