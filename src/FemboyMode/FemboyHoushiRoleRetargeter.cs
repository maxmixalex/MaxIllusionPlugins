using System;
using System.Collections.Generic;
using UnityEngine;

namespace FemboyMode
{
    // Restore visual-only changes before the game's animator evaluates the
    // next frame. The late retarget pass never becomes the next native input.
    [DefaultExecutionOrder(-20000)]
    public sealed class FemboyHoushiRoleRestorer : MonoBehaviour
    {
        internal FemboyHoushiRoleRetargeter Owner;

        private void Update()
        {
            if (Owner != null) Owner.RestoreNativePose();
        }
    }

    /// <summary>
    /// Transfer only human FK joint poses across actor roles. Koikatsu uses
    /// the same cf_j_* BODY bone names for BOTH male and female actors;
    /// cm_J_dan* denotes additional genital geometry, not a separate male
    /// body. This component reads BOTH evaluated native pose streams and
    /// retargets the deltas
    /// relative to each character's neutral pose, then restores the inputs
    /// before the next evaluation. Facial blendshapes, IK controls, custom
    /// geometry, clothes, voice and original controllers are never replaced.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    public sealed class FemboyHoushiRoleRetargeter : MonoBehaviour
    {
        private sealed class Joint
        {
            internal Transform Female;
            internal Transform Male;
            internal Quaternion FemaleNeutral;
            internal Quaternion MaleNeutral;
            internal Quaternion NativeFemale;
            internal Quaternion NativeMale;
            internal Quaternion LastFemale;
            internal Quaternion LastMale;
        }

        private HSceneProc scene;
        private ChaControl female;
        private ChaControl male;
        private Transform femaleRoot;
        private Transform maleRoot;
        private Transform femaleHips;
        private Transform maleHips;
        private HSceneProc.AnimationListInfo activeAction;
        private int framesApplied;
        private float maxVisualDelta;
        private readonly List<Joint> joints = new List<Joint>();
        private FemboyHoushiRoleRestorer restorer;
        private bool active;
        private bool modified;
        private bool rootModified;
        private Vector3 originalFemaleRoot, originalMaleRoot;
        private Vector3 writtenFemaleRoot, writtenMaleRoot;
        private Quaternion originalFemaleRotation, originalMaleRotation;
        private Quaternion writtenFemaleRotation, writtenMaleRotation;
        private int lastLogPose = -1;

        internal void Attach(HSceneProc process)
        {
            if (process == null || process.lstFemale == null ||
                process.lstFemale.Count != 1 || process.male == null ||
                process.lstFemale[0] == null ||
                !FemboyModePlugin.IsFemboy(process.lstFemale[0]) ||
                process.lstFemale[0].objBodyBone == null ||
                process.male.objBodyBone == null)
                return;

            if (ReferenceEquals(scene, process) &&
                ReferenceEquals(female, process.lstFemale[0]) &&
                ReferenceEquals(male, process.male) && joints.Count > 0)
                return;

            Stop();
            scene = process;
            female = process.lstFemale[0];
            male = process.male;
            femaleRoot = female.objBodyBone.transform;
            maleRoot = male.objBodyBone.transform;
            femaleHips = null;
            maleHips = null;
            joints.Clear();
            var maleMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform bone in maleRoot.GetComponentsInChildren<Transform>(true))
            {
                string key = FemboyRules.SharedHoushiJointKey(bone.name, true);
                if (key != null && !maleMap.ContainsKey(key))
                    maleMap.Add(key, bone);
            }

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform bone in femaleRoot.GetComponentsInChildren<Transform>(true))
            {
                string key = FemboyRules.SharedHoushiJointKey(bone.name, false);
                if (key == null || !used.Add(key)) continue;
                Transform matching;
                if (!maleMap.TryGetValue(key, out matching) ||
                    ReferenceEquals(matching, bone)) continue;
                joints.Add(new Joint
                {
                    Female = bone,
                    Male = matching,
                    FemaleNeutral = bone.localRotation,
                    MaleNeutral = matching.localRotation
                });
                if (FemboyRules.IsHoushiRootJoint(key))
                {
                    femaleHips = bone;
                    maleHips = matching;
                }
            }

            if (restorer == null)
            {
                restorer = gameObject.GetComponent<FemboyHoushiRoleRestorer>();
                if (restorer == null)
                    restorer = gameObject.AddComponent<FemboyHoushiRoleRestorer>();
            }
            restorer.Owner = this;
        }

        internal bool Begin(HSceneProc process,
            HSceneProc.AnimationListInfo reversedAction)
        {
            Attach(process);
            if (joints.Count < 12 || femaleRoot == null || maleRoot == null ||
                femaleHips == null || maleHips == null)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Reversed Houshi cannot retarget this rig: only " +
                    joints.Count + " compatible human FK joints. " +
                    "No transforms or controller assets will be modified.");
                return false;
            }
            activeAction = reversedAction;
            active = true;
            framesApplied = 0;
            maxVisualDelta = 0f;
            int pose = reversedAction == null ? -1 : reversedAction.id;
            lastLogPose = pose;
            FemboyModePlugin.LogOptionalIntegrationProblem(
                "Reversed Houshi body retarget active: " + joints.Count +
                " real cf_j_* body joint pairs, hipPair=" +
                (femaleHips != null && maleHips != null) +
                ", sourcePose=" + pose +
                ", exchangeRenderRoots=" +
                (FemboyModePlugin.ReverseHoushiSwapVisualRoots != null &&
                 FemboyModePlugin.ReverseHoushiSwapVisualRoots.Value) +
                "; native controllers and facial channels retained.");
            return true;
        }

        internal void Stop()
        {
            RestoreNativePose();
            active = false;
            activeAction = null;
        }

        internal void RestoreNativePose()
        {
            if (!modified) return;
            for (int i = 0; i < joints.Count; i++)
            {
                Joint j = joints[i];
                if (j.Female != null &&
                    Quaternion.Angle(j.Female.localRotation, j.LastFemale) <= 1f)
                    j.Female.localRotation = j.NativeFemale;
                if (j.Male != null &&
                    Quaternion.Angle(j.Male.localRotation, j.LastMale) <= 1f)
                    j.Male.localRotation = j.NativeMale;
            }
            modified = false;
            if (!rootModified) return;
            if (femaleRoot != null &&
                Vector3.Distance(femaleRoot.position, writtenFemaleRoot) < 0.01f &&
                Quaternion.Angle(femaleRoot.rotation, writtenFemaleRotation) < 1f)
            {
                femaleRoot.SetPositionAndRotation(
                    originalFemaleRoot, originalFemaleRotation);
            }
            if (maleRoot != null &&
                Vector3.Distance(maleRoot.position, writtenMaleRoot) < 0.01f &&
                Quaternion.Angle(maleRoot.rotation, writtenMaleRotation) < 1f)
            {
                maleRoot.SetPositionAndRotation(
                    originalMaleRoot, originalMaleRotation);
            }
            rootModified = false;
        }

        private void LateUpdate()
        {
            if (!active) return;
            if (scene == null || scene.flags == null ||
                scene.flags.mode != HFlag.EMode.houshi ||
                (scene.flags.nowAnimationInfo == null ||
                 (!ReferenceEquals(scene.flags.nowAnimationInfo, activeAction) &&
                  !FemboyRules.IsReversedHoushiName(
                      scene.flags.nowAnimationInfo.nameAnimation))) ||
                female == null || male == null)
            {
                Stop();
                return;
            }

            // Native actor ROOTS may coincide even though their animated
            // pelvis (cf_j_hips) trajectories differ. Capture each hip's
            // WORLD position BEFORE swapping joints; use this for actual
            // giver/receiver location exchange below.
            Vector3 nativeFemaleHipWorld = femaleHips == null
                ? femaleRoot.position : femaleHips.position;
            Vector3 nativeMaleHipWorld = maleHips == null
                ? maleRoot.position : maleHips.position;
            // Capture the complete ORIGINAL pose for both actors before
            // touching a single transform. This prevents intra-frame feedback.
            for (int i = 0; i < joints.Count; i++)
            {
                Joint j = joints[i];
                if (j.Female == null || j.Male == null) { Stop(); return; }
                j.NativeFemale = j.Female.localRotation;
                j.NativeMale = j.Male.localRotation;
            }
            for (int i = 0; i < joints.Count; i++)
            {
                Joint j = joints[i];
                Quaternion femaleMotion =
                    Quaternion.Inverse(j.MaleNeutral) * j.NativeMale;
                Quaternion maleMotion =
                    Quaternion.Inverse(j.FemaleNeutral) * j.NativeFemale;
                j.LastFemale = j.FemaleNeutral * femaleMotion;
                j.LastMale = j.MaleNeutral * maleMotion;
                j.Female.localRotation = j.LastFemale;
                j.Male.localRotation = j.LastMale;
            }
            modified = true;
            framesApplied++;
            if (framesApplied == 1)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Reversed Houshi first rendered frame: joints=" +
                    joints.Count + " femaleHip=" + nativeFemaleHipWorld +
                    " maleHip=" + nativeMaleHipWorld);

            if (FemboyModePlugin.ReverseHoushiSwapVisualRoots == null ||
                !FemboyModePlugin.ReverseHoushiSwapVisualRoots.Value ||
                femaleRoot == null || maleRoot == null ||
                ReferenceEquals(femaleRoot, maleRoot))
                return;

            originalFemaleRoot = femaleRoot.position;
            originalMaleRoot = maleRoot.position;
            originalFemaleRotation = femaleRoot.rotation;
            originalMaleRotation = maleRoot.rotation;
            // Swapping objBodyBone positions alone was ineffective when
            // both parent roots share a native H position. Align the actual
            // ANIMATED PELVIS positions to the other actor's original pelvis
            // instead. The male now follows the original giver's location,
            // while the marked female follows the receiver's location.
            femaleRoot.SetPositionAndRotation(
                originalMaleRoot, originalMaleRotation);
            maleRoot.SetPositionAndRotation(
                originalFemaleRoot, originalFemaleRotation);
            if (femaleHips != null && maleHips != null)
            {
                femaleRoot.position += nativeMaleHipWorld - femaleHips.position;
                maleRoot.position += nativeFemaleHipWorld - maleHips.position;
            }
            writtenFemaleRoot = femaleRoot.position;
            writtenMaleRoot = maleRoot.position;
            writtenFemaleRotation = femaleRoot.rotation;
            writtenMaleRotation = maleRoot.rotation;
            rootModified = true;
        }

        private void OnDestroy()
        {
            Stop();
            if (restorer != null) restorer.Owner = null;
        }
    }
}
