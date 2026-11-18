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
        private static readonly FieldInfo SelectedItemsField = typeof(HandCtrl).GetField(
            "areaItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, GameObject> OwnedAnchors =
            new Dictionary<int, GameObject>();
        private static readonly HashSet<int> ToyFilterAppliedHands =
            new HashSet<int>();
        private static readonly Dictionary<int, int> LastToyScanFrame =
            new Dictionary<int, int>();

        // This runs after all body/uncensor objects have been loaded, but
        // before vanilla collects its hit colliders. HandCtrl.Init is too early:
        // an optional male geometry bone may not exist at that point.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HandCtrl), nameof(HandCtrl.InitHitReactionCollider))]
        private static void BeforeHitColliderDiscovery(HandCtrl __instance)
        {
            if (__instance == null || __instance.flags == null ||
                !__instance.flags.isFreeH || FemaleField == null)
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

            // The base game's aibulayer_02 ends with the vibrator.
            // Some mods append the following toy *after* this setup call.
            // Only mark filtering complete when a row was actually removed.
            TryRemovePhallusToy(__instance, layers[2]);

            if (FemboyModePlugin.EnableKokanAnchor == null ||
                !FemboyModePlugin.EnableKokanAnchor.Value)
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
                    FemboyAnchorUpdater oldUpdater =
                        existing.GetComponent<FemboyAnchorUpdater>();
                    if (oldUpdater != null)
                        oldUpdater.Configure(bone,
                            FemboyRigResolver.FindBone(_female.objBodyBone.transform,
                                FemboyModePlugin.KokanTipBone.Value));
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
            FemboyAnchorUpdater updater = anchor.AddComponent<FemboyAnchorUpdater>();
            updater.Configure(bone,
                FemboyRigResolver.FindBone(_female.objBodyBone.transform,
                    FemboyModePlugin.KokanTipBone.Value));

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

        private static bool RemovePhallusToy(
            HandCtrl hand, Dictionary<int, HandCtrl.LayerInfo> kokanLayers)
        {
            var keys = new List<int>(kokanLayers.Keys);
            keys.Sort();

            // The base row with icon25 and objects 7/8 is the vibrator
            // itself. In a modded H list, the phallus-shaped toy the user
            // identified is the *following* row. Preserve the vibrator.
            int vibratorKey = -1;
            foreach (int key in keys)
            {
                HandCtrl.LayerInfo layer = kokanLayers[key];
                if (layer == null || layer.idObjects == null ||
                    layer.idObjects.Length < 2)
                    continue;

                if (FemboyRules.IsVibratorLayer(
                    layer.idIocn, layer.idObjects[0], layer.idObjects[1]))
                {
                    vibratorKey = key;
                    break;
                }
            }

            int skipKey = FemboyRules.NextLayerKeyAfterVibrator(
                keys.ToArray(), vibratorKey);
            if (skipKey < 0)
                return false; // No extra toy installed: check again if a mod adds one.

            int[] selected = SelectedItemsField == null ? null :
                SelectedItemsField.GetValue(hand) as int[];
            int oldSelection = selected != null && selected.Length > 2
                ? selected[2] : 0;

            // The vanilla wheel indexes 0..Count-1. Rebuild sequential
            // keys so skipping a middle row cannot cause a missing-key
            // exception if other plugins have added even more entries.
            var retained = new List<HandCtrl.LayerInfo>();
            foreach (int key in keys)
                if (key != skipKey)
                    retained.Add(kokanLayers[key]);

            kokanLayers.Clear();
            for (int i = 0; i < retained.Count; i++)
            {
                retained[i].id = i;
                kokanLayers.Add(i, retained[i]);
            }

            if (selected != null && selected.Length > 2)
            {
                if (oldSelection == skipKey)
                    selected[2] = 0;
                else if (oldSelection > skipKey)
                    selected[2] = oldSelection - 1;
                else
                    selected[2] = oldSelection;

                if (!kokanLayers.ContainsKey(selected[2]))
                    selected[2] = 0;
            }

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Removed only the post-vibrator Kokan item " + skipKey +
                    "; vibrator row " + vibratorKey + " retained.");
            return true;
        }

        private static void TryRemovePhallusToy(
            HandCtrl hand, Dictionary<int, HandCtrl.LayerInfo> layers)
        {
            if (hand == null || layers == null ||
                FemboyModePlugin.DisablePhallusToy == null ||
                !FemboyModePlugin.DisablePhallusToy.Value)
                return;

            int id = hand.GetInstanceID();
            if (ToyFilterAppliedHands.Contains(id))
                return;

            int frame;
            if (LastToyScanFrame.TryGetValue(id, out frame) &&
                Time.frameCount - frame < 20)
                return;
            LastToyScanFrame[id] = Time.frameCount;

            if (RemovePhallusToy(hand, layers))
            {
                ToyFilterAppliedHands.Add(id);
                LastToyScanFrame.Remove(id);
            }
        }

        // A late-loaded H-control mod can append its extra toy after
        // InitHitReactionCollider. Run again just before the vanilla wheel
        // reads aibulayer_02, without repeatedly deleting new items.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HandCtrl), "SetIconTexture")]
        private static void BeforeKokanWheel(
            HandCtrl __instance, ref HandCtrl.AibuColliderKind _kindTouch)
        {
            if (__instance == null ||
                _kindTouch != HandCtrl.AibuColliderKind.kokan ||
                __instance.flags == null || !__instance.flags.isFreeH ||
                FemaleField == null || AreaLayersField == null)
                return;

            ChaControl female = FemaleField.GetValue(__instance) as ChaControl;
            if (female == null || !FemboyModePlugin.IsFemboy(female))
                return;

            var layers = AreaLayersField.GetValue(__instance)
                as Dictionary<int, HandCtrl.LayerInfo>[];
            if (layers != null && layers.Length > 2)
                TryRemovePhallusToy(__instance, layers[2]);
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
            ToyFilterAppliedHands.Remove(id);
            LastToyScanFrame.Remove(id);
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
        private Transform shaftRoot;
        private Transform shaftTip;

        public void Configure(Transform root, Transform tip)
        {
            shaftRoot = root;
            // Prefer a tip within the same attached male-bone chain.
            // Never interpolate to an unrelated limb in a replacement rig.
            shaftTip = tip != null && root != null &&
                tip.IsChildOf(root) ? tip : null;
            SyncPosition();
        }

        private void LateUpdate()
        {
            SyncPosition();
        }

        private void SyncPosition()
        {
            if (FemboyModePlugin.KokanOffsetX == null ||
                FemboyModePlugin.KokanTipFraction == null)
                return;

            Vector3 offset = new Vector3(
                FemboyModePlugin.KokanOffsetX.Value,
                FemboyModePlugin.KokanOffsetY.Value,
                FemboyModePlugin.KokanOffsetZ.Value);

            // Both bone transforms include all ancestor scales and native
            // animation. The inverse transform converts the actual scaled
            // world-space shaft center into the parent's local coordinates.
            if (shaftRoot != null && shaftTip != null)
            {
                float fraction = FemboyRules.SafeShaftFraction(FemboyModePlugin.KokanTipFraction.Value);
                Vector3 worldCenter = Vector3.Lerp(
                    shaftRoot.position, shaftTip.position, fraction);
                offset += shaftRoot.InverseTransformPoint(worldCenter);
            }

            transform.localPosition = offset;
            transform.localRotation = Quaternion.Euler(
                FemboyModePlugin.KokanRotationX.Value,
                FemboyModePlugin.KokanRotationY.Value,
                FemboyModePlugin.KokanRotationZ.Value);
            if (FemboyModePlugin.KokanWorldHeightCorrection != null)
                transform.position += Vector3.up *
                    FemboyModePlugin.KokanWorldHeightCorrection.Value;

            SphereCollider hit = GetComponent<SphereCollider>();
            if (hit == null) return;

            hit.enabled = FemboyModePlugin.EnableKokanHitCollider.Value &&
                          FemboyModePlugin.EnableKokanAnchor.Value;

            // A SphereCollider's world-space radius is multiplied by the
            // parent transform's largest lossy-scale component. Normalize
            // it here so enlarging the model doesn't also enlarge the
            // mouse hit area or make calibration unpredictable.
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x),
                Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float desiredWorldRadius = Mathf.Clamp(
                FemboyModePlugin.KokanHitRadius.Value, 0.005f, 0.25f);
            hit.radius = desiredWorldRadius / Mathf.Max(0.001f, maxScale);
        }
    }
}
