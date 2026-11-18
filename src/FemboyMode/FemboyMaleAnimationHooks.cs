using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Optional real male-controller playback for the marked heroine's
    /// finish states. Unity can retarget between Humanoid avatars; a Generic
    /// male controller must NOT be assigned to a female Generic skeleton.
    /// Never swap the actual male actor's controller or HFlag.finish.
    /// </summary>
    internal static class FemboyMaleAnimationHooks
    {
        private sealed class ControllerSwap
        {
            internal Animator Actor;
            internal RuntimeAnimatorController Original;
            internal RuntimeAnimatorController Installed;
            internal HFlag SceneFlags;
        }

        private static readonly FieldInfo FlagsField =
            typeof(HActionBase).GetField("flags",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FemaleField =
            typeof(HActionBase).GetField("female",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MaleField =
            typeof(HActionBase).GetField("male",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<int, ControllerSwap> Swaps =
            new Dictionary<int, ControllerSwap>();
        private static bool warnedIncompatibleAvatar;
        private static bool warnedMissingState;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HActionBase), nameof(HActionBase.SetPlay))]
        private static void BeforeStatePlay(HActionBase __instance, string _nextAnimation)
        {
            if (__instance == null || FlagsField == null ||
                FemaleField == null || MaleField == null)
                return;

            HFlag flags = FlagsField.GetValue(__instance) as HFlag;
            ChaControl female = FemaleField.GetValue(__instance) as ChaControl;
            if (female == null || female.animBody == null)
                return;

            bool femaleFinish = flags != null && flags.isFreeH &&
                FemboyModePlugin.IsFemboy(female) &&
                FemboyRules.IsFemaleFinishAnimation(
                    (int)flags.mode, (int)flags.finish, _nextAnimation);

            ChaControl male = MaleField.GetValue(__instance) as ChaControl;
            if (femaleFinish && male != null &&
                FemboyModePlugin.EnableMaleBonePoseFollow != null &&
                FemboyModePlugin.EnableMaleBonePoseFollow.Value)
            {
                // Generic Unity rigs cannot safely share AnimatorControllers.
                // Instead synchronize only their matching male add-on bones
                // in LateUpdate while the female finish state is active.
                FemboyMalePoseFollower follower =
                    female.gameObject.GetComponent<FemboyMalePoseFollower>();
                if (follower == null)
                    follower = female.gameObject.AddComponent<FemboyMalePoseFollower>();
                follower.Configure(flags, male, female);
            }

            bool retarget = femaleFinish &&
                FemboyModePlugin.EnableMaleFinishAnimation != null &&
                FemboyModePlugin.EnableMaleFinishAnimation.Value;

            if (!retarget)
            {
                Restore(female.animBody);
                return;
            }

            Animator maleAnimator = male == null ? null : male.animBody;
            Animator femaleAnimator = female.animBody;
            if (maleAnimator == null ||
                maleAnimator.runtimeAnimatorController == null ||
                femaleAnimator.runtimeAnimatorController == null ||
                maleAnimator.avatar == null || femaleAnimator.avatar == null ||
                !maleAnimator.isHuman || !femaleAnimator.isHuman)
            {
                if (!warnedIncompatibleAvatar)
                {
                    warnedIncompatibleAvatar = true;
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Climax controller retarget skipped: male/female avatars are not both Humanoid. " +
                        "Generic rigs require explicit per-bone mapping; native animation preserved.");
                }
                return;
            }

            if (!maleAnimator.HasState(0, Animator.StringToHash(_nextAnimation)))
            {
                if (!warnedMissingState)
                {
                    warnedMissingState = true;
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Male animation controller lacks a requested finish state. " +
                        "Native female animation preserved.");
                }
                return;
            }

            int id = femaleAnimator.GetInstanceID();
            ControllerSwap existing;
            if (Swaps.TryGetValue(id, out existing))
            {
                if (ReferenceEquals(existing.Installed, maleAnimator.runtimeAnimatorController))
                    return;
                Restore(femaleAnimator);
            }

            RuntimeAnimatorController original = femaleAnimator.runtimeAnimatorController;
            RuntimeAnimatorController replacement = maleAnimator.runtimeAnimatorController;
            if (ReferenceEquals(original, replacement))
                return;

            Swaps[id] = new ControllerSwap
            {
                Actor = femaleAnimator,
                Original = original,
                Installed = replacement,
                SceneFlags = flags
            };
            femaleAnimator.runtimeAnimatorController = replacement;

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Retargeted marked Humanoid actor to native male controller for " +
                    _nextAnimation + ".");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void BeforePoseChange(HSceneProc __instance)
        {
            if (__instance != null)
                RestoreForScene(__instance.flags);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void BeforeSceneDestroyed(HSceneProc __instance)
        {
            if (__instance != null)
                RestoreForScene(__instance.flags);
        }

        private static void RestoreForScene(HFlag flags)
        {
            if (flags == null) return;
            var keys = new List<int>();
            foreach (KeyValuePair<int, ControllerSwap> entry in Swaps)
                if (ReferenceEquals(entry.Value.SceneFlags, flags))
                    keys.Add(entry.Key);

            foreach (int key in keys)
            {
                ControllerSwap swap;
                if (!Swaps.TryGetValue(key, out swap))
                    continue;
                Restore(swap.Actor);
            }
        }

        private static void Restore(Animator actor)
        {
            if (actor == null) return;

            int key = actor.GetInstanceID();
            ControllerSwap swap;
            if (!Swaps.TryGetValue(key, out swap))
                return;
            Swaps.Remove(key);

            // A third-party mod may have changed the controller after us.
            // Only undo the exact controller we installed.
            if (swap.Actor != null &&
                ReferenceEquals(swap.Actor.runtimeAnimatorController, swap.Installed))
                swap.Actor.runtimeAnimatorController = swap.Original;
        }
    }
}
