using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Rebind only the Kokan (area index 2) item parents after vanilla HandCtrl.Init.
    /// The caller can choose the actual male-geometry anchor in config, avoiding
    /// a hardcoded spatial offset for every body replacement mod.
    /// </summary>
    internal static class FemboyAibuAnchorHooks
    {
        private static readonly FieldInfo AreaLayersField = typeof(HandCtrl).GetField(
            "dicAreaLayerInfos", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, GameObject> OwnedAnchors =
            new Dictionary<int, GameObject>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.Init))]
        private static void HandInitPostfix(HandCtrl __instance, ChaControl _female, bool __result)
        {
            if (!__result || __instance == null || _female == null ||
                _female.objBodyBone == null || __instance.flags == null ||
                !__instance.flags.isFreeH ||
                FemboyModePlugin.EnableKokanAnchor == null ||
                !FemboyModePlugin.EnableKokanAnchor.Value ||
                !FemboyModePlugin.IsFemboy(_female))
                return;

            if (AreaLayersField == null)
            {
                FemboyModePlugin.WarnMissingAibuField();
                return;
            }

            var layers = AreaLayersField.GetValue(__instance)
                as Dictionary<int, HandCtrl.LayerInfo>[];
            if (layers == null || layers.Length <= 2 || layers[2] == null)
                return;

            string boneName = FemboyModePlugin.KokanAnchorBone.Value;
            if (string.IsNullOrEmpty(boneName))
                return;

            Transform bone = FindDescendant(_female.objBodyBone.transform, boneName);
            if (bone == null)
            {
                FemboyModePlugin.LogMissingAibuBone(boneName);
                return;
            }

            int id = __instance.GetInstanceID();
            GameObject existing;
            if (OwnedAnchors.TryGetValue(id, out existing))
            {
                if (existing != null) UnityEngine.Object.Destroy(existing);
                OwnedAnchors.Remove(id);
            }

            GameObject anchor = new GameObject("FemboyMode_KokanActionAnchor");
            anchor.transform.SetParent(bone, false);
            anchor.transform.localPosition = new Vector3(
                FemboyModePlugin.KokanOffsetX.Value,
                FemboyModePlugin.KokanOffsetY.Value,
                FemboyModePlugin.KokanOffsetZ.Value);
            anchor.transform.localRotation = Quaternion.Euler(
                FemboyModePlugin.KokanRotationX.Value,
                FemboyModePlugin.KokanRotationY.Value,
                FemboyModePlugin.KokanRotationZ.Value);

            int changed = 0;
            foreach (HandCtrl.LayerInfo layer in layers[2].Values)
            {
                if (layer == null || layer.transParents == null)
                    continue;

                for (int i = 0; i < layer.transParents.Length; i++)
                {
                    // Keep absent/unsupported placements unavailable.
                    if (layer.transParents[i] == null) continue;
                    layer.transParents[i] = anchor.transform;
                    changed++;
                }
            }

            if (changed == 0)
            {
                UnityEngine.Object.Destroy(anchor);
                return;
            }

            OwnedAnchors[id] = anchor;
            if (FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogAibuBound(boneName, changed);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), "OnDestroy")]
        private static void HandDestroyPostfix(HandCtrl __instance)
        {
            if (__instance == null) return;

            int id = __instance.GetInstanceID();
            GameObject anchor;
            if (!OwnedAnchors.TryGetValue(id, out anchor))
                return;

            OwnedAnchors.Remove(id);
            if (anchor != null) UnityEngine.Object.Destroy(anchor);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, name, StringComparison.Ordinal))
                    return transforms[i];
            }
            return null;
        }
    }
}
