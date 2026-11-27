using System;
using System.Collections.Generic;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Non-destructive fit for pants, underwear and one-piece garments.
    /// Detect on the current posed (baked) mesh, then map a small displacement
    /// back to the mesh's bind pose. No card, source bundle or shared mesh
    /// is ever edited.
    /// </summary>
    public sealed partial class FemboyCharacterController
    {
        private sealed class ClothingFitRecord
        {
            internal SkinnedMeshRenderer Renderer;
            internal Mesh Original;
            internal Mesh Copy;
        }

        private static readonly int[] FitSlots = { 0, 1, 3 };
        private readonly List<ClothingFitRecord> clothingFits =
            new List<ClothingFitRecord>();
        private string lastFitSignature;
        private int nextFitProbe;
        private bool fitWarned;

        // Same narrow mesh-access technique as the existing
        // ClothingCompression plugin. The runtime detour is applied only
        // while reading/cloning a garment and always undone in finally.
        // Keep the proven instance-method detour signature used by
        // ClothingCompression.MeshAccessDetour; a static no-argument hook
        // does not match the original Mesh.canAccess instance getter.
        private sealed class MeshReadAccess
        {
            private readonly NativeDetour detour;
            public MeshReadAccess()
            {
                detour = new NativeDetour(
                    AccessTools.Property(typeof(Mesh), "canAccess")
                        .GetGetMethod(true),
                    AccessTools.Method(typeof(MeshReadAccess), "CanAccess"));
                detour.Undo();
            }
            public bool CanAccess() { return true; }
            public void Apply() { detour.Apply(); }
            public void Undo() { detour.Undo(); }
        }

        private static MeshReadAccess meshReadDetour;
        private static bool EnterReadableScope()
        {
            try
            {
                if (meshReadDetour == null)
                    meshReadDetour = new MeshReadAccess();
                meshReadDetour.Apply();
                return true;
            }
            catch { return false; }
        }
        private static void LeaveReadableScope()
        {
            try { if (meshReadDetour != null) meshReadDetour.Undo(); }
            catch { }
        }

        internal static bool SupportsGarmentSlot(int slot)
        {
            // Full-body / one-piece clothing frequently lives in top or
            // bottom instead of underwear. Never touch body/accessories.
            return FemboyRules.IsPotentialCrotchGarmentSlot(slot);
        }

        private GameObject GarmentAt(int slot)
        {
            if (ChaControl == null || ChaControl.objClothes == null ||
                slot < 0 || slot >= ChaControl.objClothes.Length ||
                ChaControl.fileStatus == null ||
                ChaControl.fileStatus.clothesState == null ||
                slot >= ChaControl.fileStatus.clothesState.Length ||
                ChaControl.fileStatus.clothesState[slot] >= 3)
                return null;
            return ChaControl.objClothes[slot];
        }

        private string FitSignature()
        {
            var parts = new List<string>();
            for (int i = 0; i < FitSlots.Length; i++)
            {
                int slot = FitSlots[i];
                GameObject root = GarmentAt(slot);
                if (root == null) continue;
                parts.Add("slot:" + slot + "|root:" + root.GetInstanceID());
                foreach (SkinnedMeshRenderer renderer in
                    root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer != null && renderer.sharedMesh != null)
                        parts.Add(renderer.GetInstanceID() + ":" +
                            renderer.sharedMesh.GetInstanceID());
                }
            }
            return string.Join("/", parts.ToArray());
        }

        private void RestoreUnderwearMeshes()
        {
            foreach (ClothingFitRecord item in clothingFits)
            {
                if (item.Renderer != null && item.Renderer.sharedMesh == item.Copy)
                    item.Renderer.sharedMesh = item.Original;
                if (item.Copy != null) UnityEngine.Object.Destroy(item.Copy);
            }
            clothingFits.Clear();
            lastFitSignature = null;
        }

        private void SyncUnderwearMeshFit()
        {
            if (Time.frameCount < nextFitProbe) return;
            nextFitProbe = Time.frameCount + 20;
            bool enabled = IsFemboy &&
                FemboyModePlugin.EnableUnderwearMeshFit != null &&
                FemboyModePlugin.EnableUnderwearMeshFit.Value;
            if (!enabled)
            {
                if (clothingFits.Count > 0 || lastFitSignature != null)
                    RestoreUnderwearMeshes();
                return;
            }

            string signature = FitSignature();
            if (signature == lastFitSignature) return;
            RestoreUnderwearMeshes();

            Transform shaftRoot = GetGeometryBone(0);
            if (shaftRoot == null) return;
            Transform shaftTip = ChaControl.objBodyBone == null ? null :
                FemboyRigResolver.FindBone(
                    ChaControl.objBodyBone.transform,
                    FemboyModePlugin.MaleFinishBone == null ? null :
                    FemboyModePlugin.MaleFinishBone.Value);
            Vector3 origin = shaftRoot.position;
            Vector3 direction = shaftTip == null
                ? shaftRoot.forward * .12f
                : shaftTip.position - origin;
            float length = direction.magnitude;
            if (length < .035f) return;
            direction.Normalize();

            int adjusted = 0;
            bool accessEnabled = EnterReadableScope();
            if (!accessEnabled && !fitWarned)
            {
                fitWarned = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Underwear fit: mesh access scope unavailable; only readable clothing can be corrected.");
            }
            try
            {
                var seen = new HashSet<int>();
                foreach (int slot in FitSlots)
                {
                    GameObject garment = GarmentAt(slot);
                    if (garment == null) continue;
                    foreach (SkinnedMeshRenderer renderer in
                        garment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (renderer == null || renderer.sharedMesh == null ||
                            !seen.Add(renderer.GetInstanceID())) continue;
                        try
                        {
                            if (FitRenderer(renderer, origin, direction, length))
                                adjusted++;
                        }
                        catch (Exception ex)
                        {
                            if (FemboyModePlugin.VerboseLogging != null &&
                                FemboyModePlugin.VerboseLogging.Value)
                                FemboyModePlugin.LogOptionalIntegrationProblem(
                                    "Clothing fit skipped " + renderer.name + ": " + ex.Message);
                        }
                    }
                }
            }
            finally { if (accessEnabled) LeaveReadableScope(); }

            lastFitSignature = FitSignature();
            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Clothing fit scanned top/bottom/underwear; modified renderers=" +
                    adjusted + ", active clones=" + clothingFits.Count);
        }

        private static int DominantBone(BoneWeight weight)
        {
            float best = weight.weight0;
            int index = weight.boneIndex0;
            if (weight.weight1 > best) { best = weight.weight1; index = weight.boneIndex1; }
            if (weight.weight2 > best) { best = weight.weight2; index = weight.boneIndex2; }
            if (weight.weight3 > best) index = weight.boneIndex3;
            return index;
        }

        private static float Smooth(float ratio)
        {
            float x = Mathf.Clamp01(1f - ratio);
            return x * x * (3f - 2f * x);
        }

        private bool FitRenderer(SkinnedMeshRenderer renderer,
            Vector3 shaftOrigin, Vector3 shaftForward, float shaftLength)
        {
            Mesh original = renderer.sharedMesh;
            if (original == null || original.vertexCount < 24) return false;
            // Do not reject before a scoped Mesh.canAccess detour can expose
            // optimized game assets. Unreadable meshes fail in the guarded
            // vertex read below without touching the original renderer.

            Mesh posed = new Mesh();
            try
            {
                renderer.BakeMesh(posed);
                Vector3[] animated = posed.vertices;
                if (animated == null || animated.Length != original.vertexCount)
                    return false;

                Transform space = renderer.transform;
                Vector3 origin = space.InverseTransformPoint(shaftOrigin);
                Vector3 forward = space.InverseTransformDirection(shaftForward).normalized;
                Vector3 up = Vector3.ProjectOnPlane(
                    space.InverseTransformDirection(ChaControl.transform.up), forward);
                if (up.sqrMagnitude < 1e-5f)
                    up = Vector3.ProjectOnPlane(Vector3.up, forward);
                if (up.sqrMagnitude < 1e-5f) return false;
                up.Normalize();
                Vector3 right = Vector3.Cross(up, forward).normalized;

                float requiredDepth = Mathf.Clamp(shaftLength * .9f + .035f,
                    .09f, .23f);
                float frontMost = -1f;
                int candidates = 0;
                for (int i = 0; i < animated.Length; i++)
                {
                    Vector3 relative = animated[i] - origin;
                    float side = Mathf.Abs(Vector3.Dot(relative, right));
                    float vertical = Mathf.Abs(Vector3.Dot(relative, up));
                    float depth = Vector3.Dot(relative, forward);
                    if (side < .115f && vertical < .13f &&
                        depth > -.035f && depth < .30f)
                    {
                        candidates++;
                        frontMost = Mathf.Max(frontMost, depth);
                    }
                }
                if (candidates < 8) return false;
                float deficit = FemboyRules.UnderwearClearanceDeficit(
                    frontMost, requiredDepth);
                if (deficit <= .001f) return false;

                Mesh copy = UnityEngine.Object.Instantiate(original);
                copy.name = original.name + "_FemboyClearance";
                Vector3[] baseVertices = copy.vertices;
                BoneWeight[] weights = original.boneWeights;
                Matrix4x4[] bind = original.bindposes;
                Transform[] bones = renderer.bones;
                bool skinned = weights != null && weights.Length == animated.Length &&
                    bind != null && bones != null;
                int modified = 0;
                for (int i = 0; i < animated.Length; i++)
                {
                    Vector3 relative = animated[i] - origin;
                    float lateral = Mathf.Abs(Vector3.Dot(relative, right));
                    float vertical = Mathf.Abs(Vector3.Dot(relative, up));
                    float depth = Vector3.Dot(relative, forward);
                    if (lateral > .15f || vertical > .16f ||
                        depth < -.06f || depth > .30f) continue;
                    float weight = Smooth(lateral / .15f) *
                        Smooth(vertical / .16f) *
                        Smooth(Mathf.Abs(depth - frontMost) / .17f);
                    if (weight < .015f) continue;

                    Vector3 delta = forward * (deficit * weight);
                    if (skinned)
                    {
                        int bone = DominantBone(weights[i]);
                        if (bone >= 0 && bone < bones.Length &&
                            bone < bind.Length && bones[bone] != null)
                        {
                            Matrix4x4 skin = space.worldToLocalMatrix *
                                bones[bone].localToWorldMatrix * bind[bone];
                            delta = skin.inverse.MultiplyVector(delta);
                        }
                    }
                    baseVertices[i] += delta;
                    modified++;
                }
                if (modified < 8)
                {
                    UnityEngine.Object.Destroy(copy);
                    return false;
                }
                copy.vertices = baseVertices;
                copy.RecalculateBounds();
                renderer.sharedMesh = copy;
                clothingFits.Add(new ClothingFitRecord {
                    Renderer = renderer, Original = original, Copy = copy
                });
                return true;
            }
            finally { UnityEngine.Object.Destroy(posed); }
        }
    }
}
