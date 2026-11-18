using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private static readonly FieldInfo FemaleField = typeof(HandCtrl).GetField(
            "female", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, GameObject> OwnedAnchors =
            new Dictionary<int, GameObject>();

        // This runs after all body/uncensor objects have been loaded, but
        // before vanilla collects its hit colliders. HandCtrl.Init is too early:
        // an optional male geometry bone may not exist at that point.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.InitHitReactionCollider))]
        private static void BeforeHitColliderDiscovery(HandCtrl __instance)
        {
            if (__instance == null || __instance.flags == null ||
                !__instance.flags.isFreeH ||
                FemboyModePlugin.EnableKokanAnchor == null ||
                !FemboyModePlugin.EnableKokanAnchor.Value ||
                FemaleField == null)
                return;

            ChaControl _female = FemaleField.GetValue(__instance) as ChaControl;
            if (_female == null || _female.objBodyBone == null ||
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

            Transform bone = FemboyRigResolver.FindBone(
                _female.objBodyBone.transform, boneName);
            if (bone == null)
            {
                // Female-body rigs may not have a native male appendage bone.
                // Fall back to the regular Kokan transform so offsets still
                // work and no item loses its parent.
                bone = FemboyRigResolver.FindBone(
                    _female.objBodyBone.transform, "cf_d_kokan");
                if (bone == null)
                {
                    FemboyModePlugin.LogMissingAibuBone(boneName);
                    return;
                }
                FemboyModePlugin.LogFallbackAibuBone(boneName);
            }

            int id = __instance.GetInstanceID();
            GameObject existing;
            if (OwnedAnchors.TryGetValue(id, out existing) && existing != null)
            {
                // InitHitReactionCollider can run again on the same HandCtrl.
                // Reuse the live anchor instead of destroying an active item
                // that was parented below it by vanilla HandCtrl.SetItem().
                if (existing.transform.IsChildOf(_female.objBodyBone.transform))
                {
                    BindKokanItemParents(layers[2], existing.transform);
                    return;
                }

                // The character's body was replaced. Do not destroy an anchor
                // with live item children; the old rig will clean it up.
                if (existing.transform.childCount > 0)
                    return;
                UnityEngine.Object.Destroy(existing);
            }
            OwnedAnchors.Remove(id);

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
            // Vanilla InitHitReactionCollider runs immediately after this prefix
            // and automatically discovers this tagged collider.
            // Keep vanilla colliders: the new one supplements hit detection,
            // rather than globally mutating the model's existing hit objects.
            anchor.tag = "H/Aibu/Hit/kokan";
            SphereCollider hit = anchor.AddComponent<SphereCollider>();
            hit.isTrigger = true;
            hit.radius = Mathf.Clamp(FemboyModePlugin.KokanHitRadius.Value, 0.005f, 0.25f);
            hit.enabled = FemboyModePlugin.EnableKokanHitCollider.Value &&
                FemboyModePlugin.EnableKokanAnchor.Value;
            anchor.AddComponent<FemboyAnchorUpdater>();

            int changed = BindKokanItemParents(layers[2], anchor.transform);

            if (changed == 0)
            {
                UnityEngine.Object.Destroy(anchor);
                return;
            }

            OwnedAnchors[id] = anchor;
            if (FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogAibuBound(boneName + " -> " + bone.name, changed);
        }

        private static int BindKokanItemParents(
            Dictionary<int, HandCtrl.LayerInfo> layers, Transform anchor)
        {
            int changed = 0;
            if (layers == null || anchor == null)
                return 0;

            foreach (HandCtrl.LayerInfo layer in layers.Values)
            {
                if (layer == null || layer.transParents == null)
                    continue;

                for (int i = 0; i < layer.transParents.Length; i++)
                {
                    // Preserve unsupported placements. Existing rebindings
                    // to this same anchor are idempotent.
                    if (layer.transParents[i] == null ||
                        layer.transParents[i] == anchor)
                        continue;
                    layer.transParents[i] = anchor;
                    changed++;
                }
            }
            return changed;
        }

        // The old female model hit capsule can be closer to the camera than
        // the new Kokan collider. Explicitly prioritize the configured target
        // rather than relying on arbitrary RaycastAll hit order.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HandCtrl), "GetOnMouseAibuCollider")]
        private static void CorrectKokanRaycast(
            HandCtrl __instance, ref HandCtrl.AibuColliderKind __result)
        {
            if (__instance == null || __instance.flags == null ||
                !__instance.flags.isFreeH || !__instance.flags.isAibuSelect ||
                FemboyModePlugin.EnableKokanAnchor == null ||
                !FemboyModePlugin.EnableKokanAnchor.Value ||
                FemboyModePlugin.EnableKokanHitCollider == null ||
                !FemboyModePlugin.EnableKokanHitCollider.Value)
                return;

            GameObject anchor;
            if (!OwnedAnchors.TryGetValue(__instance.GetInstanceID(), out anchor) ||
                anchor == null)
                return;

            SphereCollider collider = anchor.GetComponent<SphereCollider>();
            if (collider == null || !collider.enabled || __instance.nowMES == null ||
                __instance.nowMES.isTouchAreas == null ||
                __instance.nowMES.isTouchAreas.Length < 4)
                return;

            // Keep the original per-animation touch area restrictions intact.
            if (!__instance.nowMES.isTouchAreas[3])
                return;

            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            if (__instance.flags.ctrlCamera == null ||
                __instance.flags.ctrlCamera.thisCmaera == null)
                return;

            Ray ray = __instance.flags.ctrlCamera.thisCmaera.ScreenPointToRay(
                Input.mousePosition);
            RaycastHit hit;
            if (collider.Raycast(ray, out hit, 100f))
                __result = HandCtrl.AibuColliderKind.kokan;
            else if (__result == HandCtrl.AibuColliderKind.kokan)
                __result = HandCtrl.AibuColliderKind.none;
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

    }

    /// <summary>
    /// Applies position/rotation changes from BepInEx ConfigurationManager
    /// while a Free H scene remains open, allowing live calibration.
    /// </summary>
    public sealed class FemboyAnchorUpdater : MonoBehaviour
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
                hit.enabled = FemboyModePlugin.EnableKokanHitCollider.Value &&
                FemboyModePlugin.EnableKokanAnchor.Value;
                hit.radius = Mathf.Clamp(FemboyModePlugin.KokanHitRadius.Value, 0.005f, 0.25f);
            }

            hasValues = true;
            oldPosition = position;
            oldRotation = rotation;
        }
    }
}
