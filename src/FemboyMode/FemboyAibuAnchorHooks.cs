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
                // Female-body rigs may not have a native male appendage bone.
                // Fall back to the regular Kokan transform so offsets still
                // work and no item loses its parent.
                bone = FindDescendant(_female.objBodyBone.transform, "cf_d_kokan");
                if (bone == null)
                {
                    FemboyModePlugin.LogMissingAibuBone(boneName);
                    return;
                }
                FemboyModePlugin.LogFallbackAibuBone(boneName);
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
            // HandCtrl.InitHitReactionCollider runs later during HSceneProc/HTest
            // initialization and will automatically pick up this tagged collider.
            // Keep vanilla colliders: the new one supplements hit detection,
            // rather than globally mutating the model's existing hit objects.
            anchor.tag = "H/Aibu/Hit/kokan";
            SphereCollider hit = anchor.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.radius = Mathf.Clamp(FemboyModePlugin.KokanHitRadius.Value, 0.005f, 0.25f);
            hit.enabled = FemboyModePlugin.EnableKokanHitCollider.Value;
            anchor.AddComponent<FemboyAnchorUpdater>();

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

    /// <summary>
    /// Applies position/rotation changes from BepInEx ConfigurationManager
    /// while a Free H scene remains open, allowing live calibration.
    /// </summary>
    internal sealed class FemboyAnchorUpdater : MonoBehaviour
    {
        private Vector3 oldPosition;
        private Vector3 oldRotation;
        private bool hasValues;

        private void LateUpdate()
        {
            if (FemboyModePlugin.KokanOffsetX == null) return;

            Vector3 position = new Vector3(
                FemboyModePlugin.KokanOffsetX.Value,
                FemboyModePlugin.KokanOffsetY.Value,
                FemboyModePlugin.KokanOffsetZ.Value);
            Vector3 rotation = new Vector3(
                FemboyModePlugin.KokanRotationX.Value,
                FemboyModePlugin.KokanRotationY.Value,
                FemboyModePlugin.KokanRotationZ.Value);
            if (!hasValues || position != oldPosition)
                transform.localPosition = position;
            if (!hasValues || rotation != oldRotation)
                transform.localRotation = Quaternion.Euler(rotation);

            SphereCollider hit = GetComponent<SphereCollider>();
            if (hit != null)
            {
                hit.enabled = FemboyModePlugin.EnableKokanHitCollider.Value;
                hit.radius = Mathf.Clamp(FemboyModePlugin.KokanHitRadius.Value, 0.005f, 0.25f);
            }

            hasValues = true;
            oldPosition = position;
            oldRotation = rotation;
        }
    }
}
