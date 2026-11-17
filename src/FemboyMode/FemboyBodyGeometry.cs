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
        private string scaledBoneName;
        private float appliedX = 1f, appliedY = 1f, appliedZ = 1f;
        private int lastLookupFrame = -1000;

        protected override void Update()
        {
            base.Update();
            if (Time.frameCount % 20 == 0)
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
            if (body == null || FemboyModePlugin.KokanAnchorBone == null)
                return;

            string boneName = FemboyModePlugin.KokanAnchorBone.Value;
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

                Transform[] transforms = body.GetComponentsInChildren<Transform>(true);
                foreach (Transform item in transforms)
                {
                    if (item.name != boneName) continue;
                    scaledBone = item;
                    originalBoneScale = item.localScale;
                    scaledBoneName = boneName;
                    break;
                }

                if (scaledBone == null)
                    return;
            }

            if (Mathf.Approximately(appliedX, geometryScaleX) &&
                Mathf.Approximately(appliedY, geometryScaleY) &&
                Mathf.Approximately(appliedZ, geometryScaleZ))
                return;

            scaledBone.localScale = new Vector3(
                originalBoneScale.x * geometryScaleX,
                originalBoneScale.y * geometryScaleY,
                originalBoneScale.z * geometryScaleZ);
            appliedX = geometryScaleX;
            appliedY = geometryScaleY;
            appliedZ = geometryScaleZ;
        }

        private void RestoreMaleGeometry()
        {
            if (scaledBone != null)
                scaledBone.localScale = originalBoneScale;
            scaledBone = null;
            scaledBoneName = null;
            appliedX = appliedY = appliedZ = 1f;
        }
    }
}
