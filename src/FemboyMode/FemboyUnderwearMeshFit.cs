using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Per-instance, reversible underwear clearance correction. Only the
    /// character's live underwear slot is considered; body meshes, saved
    /// cards and shared AssetBundle meshes are never modified.
    /// </summary>
    public sealed partial class FemboyCharacterController
    {
        private sealed class UnderwearFitRecord
        {
            internal SkinnedMeshRenderer Renderer;
            internal Mesh Original;
            internal Mesh Copy;
        }

        private static readonly FieldInfo ClothingObjectsField =
            typeof(ChaControl).GetField("objClothes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo ClothingObjectsProperty =
            typeof(ChaControl).GetProperty("objClothes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly List<UnderwearFitRecord> underwearFits =
            new List<UnderwearFitRecord>();
        private GameObject fittedUnderwear;
        private int lastUnderwearProbe = -1000;

        private static GameObject UnderwearSlot(ChaControl character)
        {
            if (character == null) return null;
            try
            {
                object found = ClothingObjectsField != null
                    ? ClothingObjectsField.GetValue(character)
                    : ClothingObjectsProperty == null ? null :
                      ClothingObjectsProperty.GetValue(character, null);
                IList objects = found as IList;
                return objects != null && objects.Count > 3
                    ? objects[3] as GameObject : null;
            }
            catch { return null; }
        }

        private int CurrentUnderwearState()
        {
            if (ChaControl == null || ChaControl.fileStatus == null ||
                ChaControl.fileStatus.clothesState == null ||
                ChaControl.fileStatus.clothesState.Length <= 3)
                return -1;
            return ChaControl.fileStatus.clothesState[3];
        }

        private void RestoreUnderwearMeshes()
        {
            for (int i = 0; i < underwearFits.Count; i++)
            {
                UnderwearFitRecord item = underwearFits[i];
                if (item.Renderer != null && item.Renderer.sharedMesh == item.Copy)
                    item.Renderer.sharedMesh = item.Original;
                if (item.Copy != null) UnityEngine.Object.Destroy(item.Copy);
            }
            underwearFits.Clear();
            fittedUnderwear = null;
        }

        private void SyncUnderwearMeshFit()
        {
            if (Time.frameCount - lastUnderwearProbe < 20) return;
            lastUnderwearProbe = Time.frameCount;
            bool enabled = FemboyModePlugin.EnableUnderwearMeshFit != null &&
                           FemboyModePlugin.EnableUnderwearMeshFit.Value;
            GameObject garment = UnderwearSlot(ChaControl);
            int liveState = CurrentUnderwearState();
            if (!FemboyRules.ShouldFitUnderwear(
                    IsFemboy, enabled, garment != null, liveState))
            {
                if (underwearFits.Count > 0 || fittedUnderwear != null)
                    RestoreUnderwearMeshes();
                return;
            }

            if (garment == fittedUnderwear)
            {
                // An already-adapted or nonmatching mesh stays untouched,
                // without rescanning all vertices every 20 frames.
                if (underwearFits.Count == 0) return;
                bool allValid = true;
                foreach (UnderwearFitRecord item in underwearFits)
                    if (item.Renderer == null || item.Renderer.sharedMesh != item.Copy)
                        allValid = false;
                if (allValid) return;
            }
            RestoreUnderwearMeshes();

            Transform baseBone = GetGeometryBone(0);
            if (baseBone == null) return;
            Transform tipBone = ChaControl != null && ChaControl.objBodyBone != null
                ? FemboyRigResolver.FindBone(ChaControl.objBodyBone.transform,
                    FemboyModePlugin.MaleFinishBone == null ? null :
                    FemboyModePlugin.MaleFinishBone.Value)
                : null;
            Vector3 shaftDirection = tipBone != null
                ? tipBone.position - baseBone.position : baseBone.forward * .12f;
            float shaftLength = shaftDirection.magnitude;
            if (shaftLength < .025f) return;
            shaftDirection.Normalize();

            // One clothing GameObject can contain several skinned meshes.
            // Each gets its own private clone, with no mutation of shared
            // game meshes or other installed modifications.
            SkinnedMeshRenderer[] renderers =
                garment.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == null || renderer.sharedMesh == null ||
                    !renderer.sharedMesh.isReadable)
                    continue;
                try { FitRenderer(renderer, baseBone.position,
                    shaftDirection, shaftLength); }
                catch (Exception ex) {
                    if (FemboyModePlugin.VerboseLogging != null &&
                        FemboyModePlugin.VerboseLogging.Value)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Underwear mesh correction skipped: " + ex.Message);
                }
            }
            fittedUnderwear = garment;
        }

        private void FitRenderer(SkinnedMeshRenderer renderer,
            Vector3 shaftRoot, Vector3 shaftDirection, float shaftLength)
        {
            Mesh original = renderer.sharedMesh;
            Vector3[] source = original.vertices;
            if (source == null || source.Length < 24) return;

            Transform space = renderer.transform;
            Vector3 origin = space.InverseTransformPoint(shaftRoot);
            Vector3 forward = space.InverseTransformDirection(
                shaftDirection).normalized;
            Vector3 up = Vector3.ProjectOnPlane(
                space.InverseTransformDirection(ChaControl.transform.up),
                forward);
            if (up.sqrMagnitude < .0001f)
                up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < .0001f) return;
            up.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;

            // The measured cloth silhouette tells us whether a garment
            // already has sufficient room for the native male geometry.
            float required = Mathf.Clamp(shaftLength * .8f + .035f, .10f, .20f);
            float frontMost = float.NegativeInfinity;
            int candidates = 0;
            for (int i = 0; i < source.Length; i++)
            {
                Vector3 relative = source[i] - origin;
                float lateral = Mathf.Abs(Vector3.Dot(relative, right));
                float vertical = Mathf.Abs(Vector3.Dot(relative, up));
                float depth = Vector3.Dot(relative, forward);
                if (lateral < .08f && vertical < .08f &&
                    depth > -.02f && depth < .27f)
                {
                    candidates++;
                    frontMost = Mathf.Max(frontMost, depth);
                }
            }
            if (candidates < 12) return;
            float deficit = FemboyRules.UnderwearClearanceDeficit(
                frontMost, required);
            if (deficit <= .001f) return; // Already adapted / roomy.

            Mesh copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name + "_FemboyClearance";
            Vector3[] vertices = copy.vertices;
            int changed = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 relative = vertices[i] - origin;
                float lateral = Mathf.Abs(Vector3.Dot(relative, right));
                float vertical = Mathf.Abs(Vector3.Dot(relative, up));
                float depth = Vector3.Dot(relative, forward);
                if (lateral >= .13f || vertical >= .13f ||
                    depth < -.045f || depth > .27f)
                    continue;
                float lateralWeight = 1f - lateral / .13f;
                float verticalWeight = 1f - vertical / .13f;
                float depthWeight = 1f - Mathf.Clamp01(
                    Mathf.Abs(depth - frontMost) / .13f);
                float weight = lateralWeight * lateralWeight *
                    verticalWeight * verticalWeight * depthWeight;
                if (weight < .001f) continue;
                vertices[i] += forward * (deficit * weight);
                changed++;
            }
            if (changed < 8)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }

            copy.vertices = vertices;
            copy.RecalculateBounds();
            renderer.sharedMesh = copy;
            underwearFits.Add(new UnderwearFitRecord {
                Renderer = renderer, Original = original, Copy = copy
            });
        }
    }
}
