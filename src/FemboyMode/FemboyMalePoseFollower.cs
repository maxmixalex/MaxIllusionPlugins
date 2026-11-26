using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Generic-rig fallback for the marked actor's male-style climax motion.
    /// It copies only the native cm_J_dan100_00 .. 109_00 local rotations
    /// from the animated male actor to matching male add-on bones on the
    /// marked female. The partner, female full-body animation, local position
    /// and model-specific bone scale remain untouched.
    /// </summary>
    public sealed class FemboyMalePoseFollower : MonoBehaviour
    {
        private const int SegmentCount = 10;
        private HFlag flags;
        private ChaControl sourceMale;
        private ChaControl targetFemale;
        private readonly Transform[] source = new Transform[SegmentCount];
        private readonly Transform[] target = new Transform[SegmentCount];
        private readonly Quaternion[] original = new Quaternion[SegmentCount];
        private readonly Quaternion[] originalSource = new Quaternion[SegmentCount];
        private readonly Quaternion[] lastWritten = new Quaternion[SegmentCount];
        private readonly bool[] modified = new bool[SegmentCount];
        private int lastLookupFrame = -1000;
        private bool configured;
        private bool loggedNoBones;

        internal void Configure(HFlag sceneFlags, ChaControl male, ChaControl female)
        {
            if (ReferenceEquals(flags, sceneFlags) &&
                ReferenceEquals(sourceMale, male) &&
                ReferenceEquals(targetFemale, female))
                return;

            Restore();
            flags = sceneFlags;
            sourceMale = male;
            targetFemale = female;
            configured = true;
            loggedNoBones = false;
            lastLookupFrame = -1000;
            CacheBones();
        }

        private void LateUpdate()
        {
            if (!configured) return;

            if (flags == null || !flags.isFreeH ||
                FemboyModePlugin.EnableMaleBonePoseFollow == null ||
                !FemboyModePlugin.EnableMaleBonePoseFollow.Value ||
                targetFemale == null || sourceMale == null ||
                !FemboyModePlugin.IsFemboy(targetFemale) ||
                flags.mode != HFlag.EMode.sonyu ||
                flags.finish < HFlag.FinishKind.orgW ||
                flags.finish > HFlag.FinishKind.sameS)
            {
                Restore();
                return;
            }

            if (target[0] == null && Time.frameCount - lastLookupFrame > 60)
                CacheBones();

            for (int i = 0; i < SegmentCount; i++)
            {
                Transform from = source[i];
                Transform to = target[i];
                if (from == null || to == null || from == to)
                    continue;

                // Preserve third-party animation controllers and physics
                // that have overwritten our previous frame's result.
                if (modified[i] &&
                    Quaternion.Angle(to.localRotation, lastWritten[i]) > 0.25f)
                    continue;

                if (!modified[i])
                {
                    original[i] = to.localRotation;
                    originalSource[i] = from.localRotation;
                }

                // Transfer the *delta* from the male actor's initial bone
                // rotation, not its absolute rotation. The replacement
                // geometry can have its own bind/rest orientation.
                Quaternion delta =
                    Quaternion.Inverse(originalSource[i]) * from.localRotation;
                Quaternion motion = original[i] * delta;
                to.localRotation = motion;
                lastWritten[i] = motion;
                modified[i] = true;
            }
        }

        private void CacheBones()
        {
            lastLookupFrame = Time.frameCount;
            if (sourceMale == null || targetFemale == null ||
                sourceMale.objBodyBone == null || targetFemale.objBodyBone == null)
                return;

            int matched = 0;
            for (int i = 0; i < SegmentCount; i++)
            {
                string name = FemboyRules.MaleSegmentBoneName(100 + i);
                source[i] = FemboyRigResolver.FindBone(
                    sourceMale.objBodyBone.transform, name);
                target[i] = FemboyRigResolver.FindBone(
                    targetFemale.objBodyBone.transform, name);
                if (source[i] != null && target[i] != null) matched++;
            }

            if (matched == 0 && !loggedNoBones)
            {
                loggedNoBones = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Native male finish pose follower has no matching male geometry bones " +
                    "on this character; the original pose is retained.");
            }
            else if (matched > 0 && FemboyModePlugin.VerboseLogging != null &&
                     FemboyModePlugin.VerboseLogging.Value)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Matched " + matched + " male geometry bone segments for actor-local finish.");
            }
        }

        private void Restore()
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                Transform to = target[i];
                if (modified[i] && to != null &&
                    Quaternion.Angle(to.localRotation, lastWritten[i]) <= 0.25f)
                    to.localRotation = original[i];
                modified[i] = false;
            }
        }

        private void OnDestroy()
        {
            Restore();
        }
    }
}
