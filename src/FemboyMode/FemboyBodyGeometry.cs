using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Bone-local, per-card transforms. Two independent bones are used:
    /// cm_J_dan100_00 (penis root) and cm_J_dan_f_top (balls root).
    /// Neutral transforms never touch the rig. A changed animator/ABMX baseline
    /// is rebased before writing our relative correction, avoiding compounding.
    /// </summary>
    public sealed partial class FemboyCharacterController
    {
        private sealed class BoneTransformState
        {
            internal Transform Bone;
            internal string Name;
            internal Vector3 BaseScale, BasePosition;
            internal Quaternion BaseRotation;
            internal Vector3 LastScale, LastPosition;
            internal Quaternion LastRotation;
            internal int LookupFrame = -1000;

            internal void Restore()
            {
                if (Bone != null)
                {
                    if (Near(Bone.localScale, LastScale)) Bone.localScale = BaseScale;
                    if (Near(Bone.localPosition, LastPosition)) Bone.localPosition = BasePosition;
                    if (Near(Bone.localRotation, LastRotation)) Bone.localRotation = BaseRotation;
                }
                Bone = null;
                Name = null;
                LookupFrame = -1000;
            }
        }

        private readonly BoneTransformState penisState = new BoneTransformState();
        private readonly BoneTransformState ballsState = new BoneTransformState();
        private static readonly Vector3 One = new Vector3(1f, 1f, 1f);

        internal bool HasAppliedMaleGeometryScale
        {
            get
            {
                return penisState.Bone != null &&
                       (!Mathf.Approximately(geometryScaleX, 1f) ||
                        !Mathf.Approximately(geometryScaleY, 1f) ||
                        !Mathf.Approximately(geometryScaleZ, 1f)) &&
                       Near(penisState.Bone.localScale, penisState.LastScale);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (Time.frameCount % 20 == 0) ApplyMaleGeometry();
        }

        private void LateUpdate()
        {
            if (IsFemboy) ApplyMaleGeometry();
        }

        protected override void OnDestroy()
        {
            RestoreMaleGeometry();
            base.OnDestroy();
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.00000001f;
        }
        private static bool Near(Quaternion a, Quaternion b)
        {
            return Quaternion.Angle(a, b) < 0.025f;
        }
        private static bool Neutral(Vector3 scale, Vector3 pos, Vector3 rot)
        {
            return Near(scale, One) && pos.sqrMagnitude < 0.00000001f &&
                   rot.sqrMagnitude < 0.00000001f;
        }

        private static void UpdateBone(BoneTransformState state, Transform root,
            string configuredName, Vector3 scale, Vector3 pos, Vector3 rot)
        {
            if (root == null || string.IsNullOrEmpty(configuredName) || Neutral(scale, pos, rot))
            {
                state.Restore();
                return;
            }
            if (state.Bone == null || !state.Bone.IsChildOf(root) || state.Name != configuredName)
            {
                if (state.Bone != null) state.Restore();
                if (Time.frameCount - state.LookupFrame < 60) return;
                state.LookupFrame = Time.frameCount;
                Transform found = FemboyRigResolver.FindBone(root, configuredName);
                if (found == null) return;
                state.Bone = found;
                state.Name = configuredName;
                state.BaseScale = state.LastScale = found.localScale;
                state.BasePosition = state.LastPosition = found.localPosition;
                state.BaseRotation = state.LastRotation = found.localRotation;
            }

            Transform bone = state.Bone;
            // Each property is rebased independently. Animations can overwrite
            // position/rotation every frame while ABMX updates scale less often.
            if (!Near(bone.localScale, state.LastScale)) state.BaseScale = bone.localScale;
            if (!Near(bone.localPosition, state.LastPosition)) state.BasePosition = bone.localPosition;
            if (!Near(bone.localRotation, state.LastRotation)) state.BaseRotation = bone.localRotation;

            bone.localScale = Vector3.Scale(state.BaseScale, scale);
            bone.localPosition = state.BasePosition + pos;
            bone.localRotation = state.BaseRotation * Quaternion.Euler(rot);
            state.LastScale = bone.localScale;
            state.LastPosition = bone.localPosition;
            state.LastRotation = bone.localRotation;
        }

        private void ApplyMaleGeometry()
        {
            if (ChaControl == null || !IsFemboy ||
                FemboyModePlugin.EnableMaleGeometryTransforms == null ||
                !FemboyModePlugin.EnableMaleGeometryTransforms.Value)
            {
                RestoreMaleGeometry();
                return;
            }
            GameObject body = ChaControl.objBodyBone;
            if (body == null) return;

            Vector3 scale = (FemboyModePlugin.EnableMaleBoneScale != null &&
                             FemboyModePlugin.EnableMaleBoneScale.Value)
                ? new Vector3(geometryScaleX, geometryScaleY, geometryScaleZ)
                : One;
            if (FemboyModePlugin.MaleGeometryBone != null)
                UpdateBone(penisState, body.transform,
                    FemboyModePlugin.MaleGeometryBone.Value, scale,
                    new Vector3(penisMove[0], penisMove[1], penisMove[2]),
                    new Vector3(penisRot[0], penisRot[1], penisRot[2]));
            if (FemboyModePlugin.MaleBallsBone != null)
                UpdateBone(ballsState, body.transform,
                    FemboyModePlugin.MaleBallsBone.Value,
                    new Vector3(ballsScale[0], ballsScale[1], ballsScale[2]),
                    new Vector3(ballsMove[0], ballsMove[1], ballsMove[2]),
                    new Vector3(ballsRot[0], ballsRot[1], ballsRot[2]));
        }

        internal Transform GetGeometryBone(int part)
        {
            if (ChaControl == null || ChaControl.objBodyBone == null ||
                !IsFemboy) return null;
            if (part == 0)
                return penisState.Bone != null
                    ? penisState.Bone
                    : FemboyRigResolver.FindBone(ChaControl.objBodyBone.transform,
                        FemboyModePlugin.MaleGeometryBone == null ? null : FemboyModePlugin.MaleGeometryBone.Value);
            return ballsState.Bone != null
                ? ballsState.Bone
                : FemboyRigResolver.FindBone(ChaControl.objBodyBone.transform,
                    FemboyModePlugin.MaleBallsBone == null ? null : FemboyModePlugin.MaleBallsBone.Value);
        }

        private void RestoreMaleGeometry()
        {
            penisState.Restore();
            ballsState.Restore();
        }
    }
}
