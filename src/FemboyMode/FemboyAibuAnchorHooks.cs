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
        private sealed class HandParentBinding
        {
            internal HandCtrl.LayerInfo Layer;
            internal int Index;
            internal Transform Original;
            internal GameObject Proxy;
        }

        private static readonly Dictionary<int, List<HandParentBinding>> HandParents =
            new Dictionary<int, List<HandParentBinding>>();
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
            {
                // HandCtrl may be reused after switching heroines; restore
                // original item parents before an ordinary card is shown.
                ResetHand(__instance.GetInstanceID());
                return;
            }

            if (AreaLayersField == null)
            {
                FemboyModePlugin.WarnMissingAibuField();
                return;
            }

            var layers = AreaLayersField.GetValue(__instance)
                as Dictionary<int, HandCtrl.LayerInfo>[];
            if (layers == null || layers.Length <= 2 || layers[2] == null)
                return;

            // The base game's aibulayer_02 ends with the phallus toy.
            // Mods can replace/rebuild the item layers *after* this setup call.
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
                    BindKokanItemParents(__instance, layers[2], existing.transform);
                    FemboyAnchorUpdater oldUpdater =
                        existing.GetComponent<FemboyAnchorUpdater>();
                    if (oldUpdater != null)
                        oldUpdater.Configure(bone,
                            FemboyRigResolver.FindBone(_female.objBodyBone.transform,
                                FemboyModePlugin.KokanTipBone.Value), _female);
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
                    FemboyModePlugin.KokanTipBone.Value), _female);

            int changed = BindKokanItemParents(__instance, layers[2], anchor.transform);

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

            // IMPORTANT: native p_item_denma (IDs 5/6, row 3) is the
            // vibrator the user wants to KEEP. Native p_item_vibe
            // (IDs 7/8, row 4) is the phallus-shaped toy to disable.
            // Do not search for an imaginary sixth row after it.
            int skipKey = -1;
            foreach (int key in keys)
            {
                HandCtrl.LayerInfo layer = kokanLayers[key];
                if (layer == null || layer.idObjects == null ||
                    layer.idObjects.Length < 2)
                    continue;

                if (FemboyRules.IsPhallusToyLayer(
                    layer.idIocn, layer.idObjects[0], layer.idObjects[1]))
                {
                    skipKey = key;
                    break;
                }
            }

            if (skipKey < 0) return false;

            int[] selected = SelectedItemsField == null ? null :
                SelectedItemsField.GetValue(hand) as int[];
            int oldSelection = selected != null && selected.Length > 2
                ? selected[2] : 0;

            // HandCtrl.SetIconTexture expects contiguous 0..Count-1
            // dictionary indices. Preserve every non-target row and
            // normalize keys only within this marked H scene.
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

            FemboyModePlugin.LogOptionalIntegrationProblem(
                "Removed Kokan phallus toy (p_item_vibe ID7/8) row " + skipKey +
                "; preserved denma vibrator ID5/6.");
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

            int frame;
            if (LastToyScanFrame.TryGetValue(id, out frame) &&
                Time.frameCount - frame < 20)
                return;
            LastToyScanFrame[id] = Time.frameCount;

            RemovePhallusToy(hand, layers);
        }

        // A late-loaded H-control mod can recreate the unwanted toy after
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

        /// <summary>
        /// Keep the exact vanilla parent rotation, rig hierarchy and local
        /// hand animation coordinate system. The old implementation set
        /// transParents directly to the penile bone and caused the entire
        /// hand model to float far away (RX18 screenshot). A translating
        /// proxy UNDER each original parent retains all animation offsets.
        /// </summary>
        private static int BindKokanItemParents(
            HandCtrl hand, Dictionary<int, HandCtrl.LayerInfo> layers,
            Transform contactAnchor)
        {
            if (hand == null || layers == null || contactAnchor == null)
                return 0;

            int handId = hand.GetInstanceID();
            List<HandParentBinding> old;
            if (HandParents.TryGetValue(handId, out old))
            {
                bool reusable = true;
                foreach (HandParentBinding binding in old)
                {
                    if (binding.Proxy == null || binding.Original == null ||
                        binding.Proxy.transform.parent != binding.Original)
                    {
                        reusable = false;
                        break;
                    }
                }
                if (reusable && old.Count > 0)
                {
                    // Re-check after HSceneProc.ChangeAnimator: HandCtrl.Init
                    // replaces the LayerInfo parent arrays on pose reload.
                    var present = new HashSet<HandCtrl.LayerInfo>(layers.Values);
                    foreach (HandParentBinding binding in old)
                    {
                        if (!present.Contains(binding.Layer))
                        {
                            reusable = false;
                            break;
                        }
                    }

                    // Another H-control mod can add item layers after our
                    // first binding. Do not forget to proxy those additions.
                    int currentSlots = 0;
                    foreach (HandCtrl.LayerInfo layer in layers.Values)
                    {
                        if (layer == null || layer.transParents == null)
                            continue;
                        for (int i = 0; i < layer.transParents.Length; i++)
                            if (layer.transParents[i] != null)
                                currentSlots++;
                    }
                    if (currentSlots != old.Count)
                        reusable = false;
                }
                if (reusable && old.Count > 0)
                {
                    Transform native = FindOriginalKokanContact(hand);
                    foreach (HandParentBinding binding in old)
                    {
                        if (binding.Layer != null &&
                            binding.Layer.transParents != null &&
                            binding.Index < binding.Layer.transParents.Length)
                            binding.Layer.transParents[binding.Index] =
                                binding.Proxy.transform;

                        FemboyKokanHandProxy updater =
                            binding.Proxy.GetComponent<FemboyKokanHandProxy>();
                        if (updater != null)
                            updater.Configure(binding.Original, native,
                                contactAnchor, GetMarkedHandOwner(hand));
                    }
                    return old.Count;
                }
                RestoreHandParents(handId);
            }

            Transform originalContact = FindOriginalKokanContact(hand);
            if (originalContact == null)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Original HandCtrl Kokan reference missing; hand parents " +
                    "left in native positions to avoid detached models.");
                return 0;
            }

            var bindings = new List<HandParentBinding>();
            foreach (HandCtrl.LayerInfo layer in layers.Values)
            {
                if (layer == null || layer.transParents == null) continue;
                for (int i = 0; i < layer.transParents.Length; i++)
                {
                    Transform original = layer.transParents[i];
                    if (original == null) continue;

                    GameObject proxy = new GameObject(
                        "FemboyMode_KokanHandOffset_" + i);
                    proxy.transform.SetParent(original, false);
                    var updater = proxy.AddComponent<FemboyKokanHandProxy>();
                    updater.Configure(original, originalContact, contactAnchor,
                        GetMarkedHandOwner(hand));

                    layer.transParents[i] = proxy.transform;
                    bindings.Add(new HandParentBinding
                    {
                        Layer = layer,
                        Index = i,
                        Original = original,
                        Proxy = proxy
                    });
                }
            }

            if (bindings.Count > 0)
                HandParents[handId] = bindings;
            return bindings.Count;
        }

        private static ChaControl GetMarkedHandOwner(HandCtrl hand)
        {
            return hand == null || FemaleField == null ? null :
                FemaleField.GetValue(hand) as ChaControl;
        }

        private static Transform FindOriginalKokanContact(HandCtrl hand)
        {
            // HandCtrl.Init explicitly creates cf_d_kokan_REF at index 2
            // and applies the vanilla 270/180 degree reference rotation.
            // Its position is the appropriate native touch source.
            if (hand.objReferences != null &&
                hand.objReferences.Length > 2 &&
                hand.objReferences[2] != null)
                return hand.objReferences[2].transform;

            ChaControl female = FemaleField == null ? null :
                FemaleField.GetValue(hand) as ChaControl;
            return female == null || female.objBodyBone == null ? null :
                FemboyRigResolver.FindBone(female.objBodyBone.transform,
                    "cf_d_kokan");
        }

        private static void RestoreHandParents(int handId)
        {
            List<HandParentBinding> bindings;
            if (!HandParents.TryGetValue(handId, out bindings))
                return;
            HandParents.Remove(handId);

            foreach (HandParentBinding binding in bindings)
            {
                if (binding.Layer != null &&
                    binding.Layer.transParents != null &&
                    binding.Index < binding.Layer.transParents.Length &&
                    binding.Proxy != null &&
                    binding.Layer.transParents[binding.Index] ==
                        binding.Proxy.transform)
                    binding.Layer.transParents[binding.Index] = binding.Original;

                if (binding.Proxy == null) continue;
                Transform parent = binding.Original;
                // HandCtrl.SetItem parents live GameObjects here. Move them
                // back first so destroying the proxy cannot delete items
                // owned by vanilla HandCtrl or another plugin.
                while (binding.Proxy.transform.childCount > 0)
                {
                    Transform child = binding.Proxy.transform.GetChild(0);
                    if (parent != null)
                        child.SetParent(parent, false);
                    else
                        child.SetParent(null, true);
                }
                UnityEngine.Object.Destroy(binding.Proxy);
            }
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

            // While panties still cover the groin, keep the native clothing
            // hit target and do not select the hidden penile collider.
            ChaControl wearer = GetMarkedHandOwner(__instance);
            if (wearer != null &&
                FemboyRules.UseNativeUnderwearContact(wearer.notShorts)) return;

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

            ResetHand(__instance.GetInstanceID());
        }

        private static void ResetHand(int handId)
        {
            LastToyScanFrame.Remove(handId);
            RestoreHandParents(handId);
            GameObject anchor;
            if (!OwnedAnchors.TryGetValue(handId, out anchor))
                return;
            OwnedAnchors.Remove(handId);
            if (anchor != null)
                UnityEngine.Object.Destroy(anchor);
        }

        internal static void ResetAll()
        {
            var ids = new List<int>(HandParents.Keys);
            foreach (int id in ids)
                ResetHand(id);
            ids = new List<int>(OwnedAnchors.Keys);
            foreach (int id in ids)
                ResetHand(id);
            LastToyScanFrame.Clear();
        }

    }

    /// <summary>
    /// Preserves the H hand item's original animated parent rotation,
    /// orientation and scale. Only transfers its *world position* by the
    /// displacement between the native female Kokan reference and the
    /// model-specific male touch target. Full-body animation remains native.
    /// </summary>
    public sealed class FemboyKokanHandProxy : MonoBehaviour
    {
        private Transform original;
        private Transform nativeContact;
        private Transform targetContact;
        private ChaControl wearer;
        private bool? lastExposed;

        public void Configure(Transform sourceParent, Transform nativeTouch,
            Transform targetTouch, ChaControl markedActor)
        {
            original = sourceParent;
            nativeContact = nativeTouch;
            targetContact = targetTouch;
            wearer = markedActor;
            Align();
        }

        private void LateUpdate()
        {
            Align();
        }

        private void Align()
        {
            if (original == null || nativeContact == null)
                return;

            // Disabled integration should not detach an item whose native
            // parent is currently this proxy.
            Vector3 shift = Vector3.zero;
            // notShorts is Koikatsu's live visual underwear status. When
            // panties are worn, original HandCtrl parents already target
            // the cloth-covered crotch; any penile displacement floats.
            bool exposed = wearer == null ||
                !FemboyRules.UseNativeUnderwearContact(wearer.notShorts);
            if (lastExposed != exposed)
            {
                lastExposed = exposed;
                if (FemboyModePlugin.VerboseLogging != null &&
                    FemboyModePlugin.VerboseLogging.Value)
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Kokan hand target switched to " +
                        (exposed ? "exposed male geometry" : "native underwear surface"));
            }
            if (exposed && targetContact != null &&
                FemboyModePlugin.EnableKokanAnchor != null &&
                FemboyModePlugin.EnableKokanAnchor.Value)
            {
                shift = targetContact.position - nativeContact.position;
                if (FemboyModePlugin.KokanHandOffsetX != null)
                    shift += new Vector3(
                        FemboyModePlugin.KokanHandOffsetX.Value,
                        FemboyModePlugin.KokanHandOffsetY.Value,
                        FemboyModePlugin.KokanHandOffsetZ.Value);
            }

            transform.position = original.position + shift;
            transform.rotation = original.rotation;
            transform.localScale = Vector3.one;
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
        private ChaControl wearer;

        public void Configure(Transform root, Transform tip, ChaControl markedActor)
        {
            shaftRoot = root;
            wearer = markedActor;
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
                          FemboyModePlugin.EnableKokanAnchor.Value &&
                          (wearer == null ||
                           !FemboyRules.UseNativeUnderwearContact(wearer.notShorts));

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
