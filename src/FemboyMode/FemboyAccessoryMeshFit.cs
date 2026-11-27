using System;
using System.Collections.Generic;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Accessories used as underwear are absent from ChaControl.objClothes.
    /// Inspect objAccessory by visibility and actual crotch coverage; support
    /// both skinned and rigid accessory meshes. The original materials,
    /// clothing cards and imported shared meshes are never modified.
    /// </summary>
    public sealed partial class FemboyCharacterController
    {
        private bool ShortsPartiallyMoved()
        {
            if (ChaControl == null || ChaControl.fileStatus == null ||
                ChaControl.fileStatus.clothesState == null ||
                ChaControl.fileStatus.clothesState.Length <= 3)
                return false;
            int state = ChaControl.fileStatus.clothesState[3];
            return state == 1 || state == 2;
        }

        private GameObject AccessoryAt(int slot)
        {
            if (ChaControl == null || ChaControl.objAccessory == null ||
                slot < 0 || slot >= ChaControl.objAccessory.Length ||
                ChaControl.fileStatus == null ||
                ChaControl.fileStatus.showAccessory == null ||
                slot >= ChaControl.fileStatus.showAccessory.Length ||
                !ChaControl.fileStatus.showAccessory[slot] ||
                ShortsPartiallyMoved())
                return null;
            GameObject root = ChaControl.objAccessory[slot];
            return root != null && root.activeInHierarchy ? root : null;
        }

        private static string QuantizedPosition(Vector3 position)
        {
            // If an accessory is pulled aside by a maker transform, a changed
            // 2 cm local position invalidates the previous mesh clone.
            return Mathf.RoundToInt(position.x * 50f) + ":" +
                   Mathf.RoundToInt(position.y * 50f) + ":" +
                   Mathf.RoundToInt(position.z * 50f);
        }

        private void AppendAccessoryFitSignature(List<string> parts)
        {
            if (ChaControl == null || ChaControl.objAccessory == null)
                return;
            for (int slot = 0; slot < ChaControl.objAccessory.Length; slot++)
            {
                GameObject root = AccessoryAt(slot);
                if (root == null) continue;
                parts.Add("A:" + slot + ":" + root.GetInstanceID() + ":" +
                    QuantizedPosition(
                        ChaControl.transform.InverseTransformPoint(root.transform.position)));
                foreach (SkinnedMeshRenderer skinned in
                    root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skinned != null && skinned.sharedMesh != null)
                        parts.Add(skinned.GetInstanceID() + ":" +
                            skinned.sharedMesh.GetInstanceID());
                foreach (MeshFilter filter in
                    root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter != null && filter.sharedMesh != null)
                        parts.Add(filter.GetInstanceID() + ":" +
                            filter.sharedMesh.GetInstanceID());
            }
        }

        private static bool IsAccessoryUnderwearName(string name)
        {
            return FemboyRules.IsUnderwearAccessoryName(name);
        }

        // A ring, hairpin or pendant can share the clothing rig, but cannot
        // pass the symmetric panty-panel spatial coverage test.
        private static bool CoversCrotchPanel(Vector3[] points,
            Transform space, Vector3 shaftOrigin, Vector3 forward,
            bool recognizedName)
        {
            if (points == null || points.Length < 16) return false;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < .01f) up = Vector3.right;
            up.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;

            int near = 0;
            float left = float.MaxValue, rightMost = float.MinValue;
            float low = float.MaxValue, high = float.MinValue;
            // Limit analysis cost even with large imported meshes.
            int step = Math.Max(1, points.Length / 2400);
            for (int i = 0; i < points.Length; i += step)
            {
                Vector3 delta = space.TransformPoint(points[i]) - shaftOrigin;
                float lateral = Vector3.Dot(delta, right);
                float vertical = Vector3.Dot(delta, up);
                float depth = Vector3.Dot(delta, forward);
                if (Mathf.Abs(lateral) > .13f ||
                    Mathf.Abs(vertical) > .14f ||
                    depth < -.055f || depth > .25f)
                    continue;
                near++;
                left = Mathf.Min(left, lateral);
                rightMost = Mathf.Max(rightMost, lateral);
                low = Mathf.Min(low, vertical);
                high = Mathf.Max(high, vertical);
            }

            return FemboyRules.AccessoryCoverageQualified(
                near, left, rightMost, low, high, recognizedName);
        }

        private void ScanAccessoryUnderwear(Vector3 origin, Vector3 direction,
            float length, HashSet<int> seen, ref int modified)
        {
            if (ChaControl == null || ChaControl.objAccessory == null ||
                ShortsPartiallyMoved()) return;
            for (int slot = 0; slot < ChaControl.objAccessory.Length; slot++)
            {
                GameObject root = AccessoryAt(slot);
                if (root == null) continue;
                bool recognized = IsAccessoryUnderwearName(root.name);
                foreach (SkinnedMeshRenderer renderer in
                    root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || !renderer.enabled ||
                        renderer.sharedMesh == null ||
                        !seen.Add(renderer.GetInstanceID())) continue;
                    try
                    {
                        Mesh posed = new Mesh();
                        bool covers = false;
                        try {
                            renderer.BakeMesh(posed);
                            covers = CoversCrotchPanel(posed.vertices,
                                renderer.transform, origin, direction,
                                recognized || IsAccessoryUnderwearName(renderer.name));
                        } finally { UnityEngine.Object.Destroy(posed); }
                        if (!covers) continue;
                        if (FitRenderer(renderer, slot, origin, direction,
                            length, true)) modified++;
                    }
                    catch (Exception ex) {
                        if (FemboyModePlugin.VerboseLogging != null &&
                            FemboyModePlugin.VerboseLogging.Value)
                            FemboyModePlugin.LogOptionalIntegrationProblem(
                                "Accessory skinned underwear fit skipped " +
                                slot + ": " + ex.Message);
                    }
                }

                foreach (MeshFilter filter in
                    root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter == null || filter.sharedMesh == null ||
                        !seen.Add(filter.GetInstanceID())) continue;
                    MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();
                    if (meshRenderer == null || !meshRenderer.enabled) continue;
                    try
                    {
                        if (FitRigidAccessory(filter, slot, origin,
                            direction, length, recognized ||
                            IsAccessoryUnderwearName(filter.name)))
                            modified++;
                    } catch (Exception ex) {
                        if (FemboyModePlugin.VerboseLogging != null &&
                            FemboyModePlugin.VerboseLogging.Value)
                            FemboyModePlugin.LogOptionalIntegrationProblem(
                                "Accessory rigid underwear fit skipped " +
                                slot + ": " + ex.Message);
                    }
                }
            }
        }

        private bool FitRigidAccessory(MeshFilter filter, int slot,
            Vector3 shaftOrigin, Vector3 shaftForward, float shaftLength,
            bool recognized)
        {
            Mesh original = filter.sharedMesh;
            if (original == null || original.vertexCount < 16) return false;
            Vector3[] vertices = original.vertices;
            Transform space = filter.transform;
            if (!CoversCrotchPanel(vertices, space, shaftOrigin,
                shaftForward, recognized)) return false;

            Vector3 origin = space.InverseTransformPoint(shaftOrigin);
            Vector3 forward = space.InverseTransformDirection(
                shaftForward).normalized;
            Vector3 up = Vector3.ProjectOnPlane(
                space.InverseTransformDirection(ChaControl.transform.up), forward);
            if (up.sqrMagnitude < .00001f)
                up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < .00001f) return false;
            up.Normalize();
            Vector3 right = Vector3.Cross(up, forward).normalized;

            float front = -1f;
            int nearby = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 r = vertices[i] - origin;
                float lateral = Mathf.Abs(Vector3.Dot(r, right));
                float vertical = Mathf.Abs(Vector3.Dot(r, up));
                float depth = Vector3.Dot(r, forward);
                if (lateral < .095f && vertical < .11f &&
                    depth > -.035f && depth < .19f)
                { nearby++; front = Mathf.Max(front, depth); }
            }
            if (nearby < 8) return false;
            float needed = Mathf.Clamp(shaftLength * .65f + .012f,
                .055f, .12f);
            float deficit = FemboyRules.UnderwearClearanceDeficit(front, needed);
            if (deficit <= .001f) return false;

            Mesh copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name + "_FemboyAccessoryClearance";
            Vector3[] edited = copy.vertices;
            int changed = 0;
            float strength = FemboyModePlugin.UnderwearFitStrength == null
                ? .4f : Mathf.Clamp01(FemboyModePlugin.UnderwearFitStrength.Value);
            for (int i = 0; i < edited.Length; i++)
            {
                Vector3 r = edited[i] - origin;
                float side = Mathf.Abs(Vector3.Dot(r, right));
                float vertical = Mathf.Abs(Vector3.Dot(r, up));
                float depth = Vector3.Dot(r, forward);
                if (side > .095f || vertical > .105f ||
                    depth < -.035f || depth > .19f) continue;
                float weight = Smooth(side / .095f) *
                    Smooth(vertical / .105f) *
                    Smooth(Mathf.Abs(depth - front) / .12f);
                if (weight < .015f) continue;
                edited[i] += forward * (deficit * strength * weight);
                changed++;
            }
            if (changed < 8) {
                UnityEngine.Object.Destroy(copy);
                return false;
            }
            copy.vertices = edited;
            copy.RecalculateBounds();
            filter.sharedMesh = copy;
            clothingFits.Add(new ClothingFitRecord {
                RigidFilter = filter, Original = original, Copy = copy,
                Slot = slot, Accessory = true
            });
            return true;
        }
    }
}
