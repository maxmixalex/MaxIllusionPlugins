using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Adds model-specific shape controls to the female character editor.
    /// This extension is independent from the vanilla body sliders and never
    /// mutates the game's built-in sex field.
    /// </summary>
    public sealed partial class FemboyCharacterController
    {
        private Transform scaledBone;
        private Vector3 originalBoneScale;
        private Vector3 lastWrittenScale;
        private string scaledBoneName;
        private float appliedX = 1f, appliedY = 1f, appliedZ = 1f;
        private int lastLookupFrame = -1000;

        protected override void Update()
        {
            base.Update();
            if (Time.frameCount % 20 == 0)
                ApplyMaleGeometry();
        }

        // Apply after animation and native body-shape updates. In Free H,
        // KKABMX/UncensorSelector and pose loading can write bone scale
        // after the character card's OnReload callback.
        private void LateUpdate()
        {
            if (IsFemboy && FemboyModePlugin.EnableMaleBoneScale != null &&
                FemboyModePlugin.EnableMaleBoneScale.Value)
                ApplyMaleGeometry();
        }

        protected override void OnDestroy()
        {
            RestoreMaleGeometry();
            base.OnDestroy();
        }

        private void ApplyMaleGeometry()
        {
            if (ChaControl == null || ChaControl.sex != 1 || !IsFemboy ||
                FemboyModePlugin.EnableMaleBoneScale == null ||
                !FemboyModePlugin.EnableMaleBoneScale.Value ||
                (Mathf.Approximately(geometryScaleX, 1f) &&
                 Mathf.Approximately(geometryScaleY, 1f) &&
                 Mathf.Approximately(geometryScaleZ, 1f)))
            {
                RestoreMaleGeometry();
                return;
            }

            GameObject body = ChaControl.objBodyBone;
            if (body == null || FemboyModePlugin.MaleGeometryBone == null)
                return;

            string boneName = FemboyModePlugin.MaleGeometryBone.Value;
            if (string.IsNullOrEmpty(boneName))
                return;

            if (scaledBone == null || !scaledBone.IsChildOf(body.transform) ||
                scaledBoneName != boneName)
            {
                RestoreMaleGeometry();

                // Missing uncensor geometry may be populated a few frames later.
                if (Time.frameCount - lastLookupFrame < 60)
                    return;
                lastLookupFrame = Time.frameCount;

                Transform item = FemboyRigResolver.FindBone(body.transform, boneName);
                if (item != null)
                {
                    scaledBone = item;
                    originalBoneScale = item.localScale;
                    lastWrittenScale = item.localScale;
                    scaledBoneName = boneName;
                    if (FemboyModePlugin.VerboseLogging != null &&
                        FemboyModePlugin.VerboseLogging.Value)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Male geometry bone resolved: " + boneName + " -> " + item.name);
                }

                if (scaledBone == null)
                    return;
            }

            // A scene load or KKABMX may replace the base scale each frame.
            // Rebase onto that *current external scale*, then apply only our
            // stored per-card multiplier. Never compound our own previous
            // multiplier; the resulting baseline is restored when disabled.
            if (!SameScale(scaledBone.localScale, lastWrittenScale))
            {
                originalBoneScale = scaledBone.localScale;
                appliedX = appliedY = appliedZ = 1f;
            }

            if (Mathf.Approximately(appliedX, geometryScaleX) &&
                Mathf.Approximately(appliedY, geometryScaleY) &&
                Mathf.Approximately(appliedZ, geometryScaleZ))
                return;

            scaledBone.localScale = new Vector3(
                originalBoneScale.x * geometryScaleX,
                originalBoneScale.y * geometryScaleY,
                originalBoneScale.z * geometryScaleZ);
            lastWrittenScale = scaledBone.localScale;
            appliedX = geometryScaleX;
            appliedY = geometryScaleY;
            appliedZ = geometryScaleZ;
        }

        private static bool SameScale(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude < 0.00000001f;
        }

        private void RestoreMaleGeometry()
        {
            // Restore only values last written by this controller. If KKABMX
            // or another mod changed the scale, leave their change intact.
            if (scaledBone != null &&
                SameScale(scaledBone.localScale, lastWrittenScale))
                scaledBone.localScale = originalBoneScale;
            scaledBone = null;
            scaledBoneName = null;
            appliedX = appliedY = appliedZ = 1f;
            lastLookupFrame = -1000;
        }
    }
}
