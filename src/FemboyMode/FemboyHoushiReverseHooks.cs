using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Expose native Houshi motions twice: the original action and an actor-
    /// reversed version. The donor action remains completely native; the
    /// second entry swaps the two animated body poses late in the frame.
    /// Never alter character sex, HFlag.player, voice queues or card data.
    /// </summary>
    internal static class FemboyHoushiReverseHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void SpriteInitialized(HSprite __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            FemboyHoushiReverseController controller =
                __instance.gameObject.GetComponent<FemboyHoushiReverseController>();
            if (controller == null)
                controller = __instance.gameObject.AddComponent<FemboyHoushiReverseController>();
            controller.Bind(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void AnimationChanged(HSceneProc __instance,
            HSceneProc.AnimationListInfo _nextAinmInfo)
        {
            if (__instance == null || __instance.sprite == null) return;
            FemboyHoushiReverseController controller =
                __instance.sprite.GetComponent<FemboyHoushiReverseController>();
            if (controller != null)
                controller.AnimationChanged(__instance, _nextAinmInfo);
        }
    }

    // Restore the native poses before the next animation evaluation. A single
    // late-update script without this step would feed its own swapped bones
    // back into the next frame and accumulate incorrect transforms.
    [DefaultExecutionOrder(-9000)]
    public sealed class FemboyHoushiReverseRestorer : MonoBehaviour
    {
        internal FemboyHoushiReverseController Controller;

        private void Update()
        {
            if (Controller != null) Controller.RestoreNativePose();
        }
    }

    [DefaultExecutionOrder(9000)]
    public sealed class FemboyHoushiReverseController : MonoBehaviour
    {
        private sealed class BonePair
        {
            internal Transform Female;
            internal Transform Male;
            internal Quaternion FemaleRotation;
            internal Quaternion MaleRotation;
            internal Vector3 FemalePosition;
            internal Vector3 MalePosition;
            internal bool SwapPosition;
        }

        private static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<BonePair> pairs = new List<BonePair>();
        private readonly HashSet<HSceneProc.AnimationListInfo> reversed =
            new HashSet<HSceneProc.AnimationListInfo>();
        private HSprite sprite;
        private HSceneProc scene;
        private ChaControl female;
        private ChaControl male;
        private bool injected;
        private bool active;
        private bool applied;
        private int nextAttemptFrame;
        private bool warnedInsufficientRig;
        private FemboyHoushiReverseRestorer restorer;

        internal void Bind(HSprite sceneSprite)
        {
            sprite = sceneSprite;
            if (restorer == null)
            {
                restorer = gameObject.GetComponent<FemboyHoushiReverseRestorer>();
                if (restorer == null)
                    restorer = gameObject.AddComponent<FemboyHoushiReverseRestorer>();
                restorer.Controller = this;
            }
            nextAttemptFrame = Time.frameCount;
        }

        private bool GetMarkedActors(out ChaControl targetFemale,
            out ChaControl targetMale)
        {
            targetFemale = null;
            targetMale = null;
            if (scene == null || scene.flags == null || !scene.flags.isFreeH ||
                scene.lstFemale == null || scene.lstFemale.Count != 1 ||
                scene.male == null ||
                FemboyModePlugin.EnableReverseHoushi == null ||
                !FemboyModePlugin.EnableReverseHoushi.Value)
                return false;

            targetFemale = scene.lstFemale[0];
            targetMale = scene.male;
            return targetFemale != null &&
                FemboyModePlugin.IsFemboy(targetFemale) &&
                targetFemale.objBodyBone != null &&
                targetMale.objBodyBone != null;
        }

        private void FindScene()
        {
            if (scene != null || sprite == null) return;
            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] == null || scenes[i].sprite != sprite) continue;
                scene = scenes[i];
                return;
            }
        }

        private void LateUpdate()
        {
            if (sprite == null) return;
            if (!injected && Time.frameCount >= nextAttemptFrame)
            {
                nextAttemptFrame = Time.frameCount + 45;
                TryInject();
            }
            if (!active) return;

            ChaControl currentFemale, currentMale;
            if (!GetMarkedActors(out currentFemale, out currentMale) ||
                scene.flags.mode != HFlag.EMode.houshi)
            {
                RestoreNativePose();
                active = false;
                return;
            }

            if (!ReferenceEquals(currentFemale, female) ||
                !ReferenceEquals(currentMale, male))
            {
                RestoreNativePose();
                if (!CacheMatchedBones(currentFemale, currentMale))
                {
                    active = false;
                    return;
                }
            }

            // The animator has just evaluated the normal native action for
            // both participants. Save both poses before writing either one.
            for (int i = 0; i < pairs.Count; i++)
            {
                BonePair pair = pairs[i];
                pair.FemaleRotation = pair.Female.localRotation;
                pair.MaleRotation = pair.Male.localRotation;
                if (!pair.SwapPosition) continue;
                pair.FemalePosition = pair.Female.localPosition;
                pair.MalePosition = pair.Male.localPosition;
            }
            for (int i = 0; i < pairs.Count; i++)
            {
                BonePair pair = pairs[i];
                pair.Female.localRotation = pair.MaleRotation;
                pair.Male.localRotation = pair.FemaleRotation;
                if (!pair.SwapPosition) continue;
                pair.Female.localPosition = pair.MalePosition;
                pair.Male.localPosition = pair.FemalePosition;
            }
            applied = true;
        }

        internal void RestoreNativePose()
        {
            if (!applied) return;
            for (int i = 0; i < pairs.Count; i++)
            {
                BonePair pair = pairs[i];
                if (pair.Female == null || pair.Male == null) continue;
                pair.Female.localRotation = pair.FemaleRotation;
                pair.Male.localRotation = pair.MaleRotation;
                if (!pair.SwapPosition) continue;
                pair.Female.localPosition = pair.FemalePosition;
                pair.Male.localPosition = pair.MalePosition;
            }
            applied = false;
        }

        private bool CacheMatchedBones(ChaControl targetFemale,
            ChaControl targetMale)
        {
            RestoreNativePose();
            pairs.Clear();
            female = targetFemale;
            male = targetMale;
            if (female == null || male == null ||
                female.objBodyBone == null || male.objBodyBone == null)
                return false;

            var maleBones = new Dictionary<string, Transform>();
            foreach (Transform t in male.objBodyBone.GetComponentsInChildren<Transform>(true))
            {
                string key = FemboyRules.ReversedBodyBoneKey(t.name);
                if (key != null && !maleBones.ContainsKey(key))
                    maleBones.Add(key, t);
            }
            var femaleSeen = new HashSet<string>();
            foreach (Transform t in female.objBodyBone.GetComponentsInChildren<Transform>(true))
            {
                string key = FemboyRules.ReversedBodyBoneKey(t.name);
                if (key == null || !femaleSeen.Add(key)) continue;
                Transform other;
                if (!maleBones.TryGetValue(key, out other) || other == t) continue;
                pairs.Add(new BonePair
                {
                    Female = t,
                    Male = other,
                    SwapPosition = FemboyRules.IsReversedRootMotionBone(key)
                });
            }
            if (pairs.Count >= 20)
            {
                if (FemboyModePlugin.VerboseLogging != null &&
                    FemboyModePlugin.VerboseLogging.Value)
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Reversed Houshi mapped " + pairs.Count +
                        " shared body bones (female/male roles).");
                warnedInsufficientRig = false;
                return true;
            }

            if (!warnedInsufficientRig)
            {
                warnedInsufficientRig = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Reversed Houshi disabled: only " + pairs.Count +
                    " compatible body bones were found. Native roles remain unchanged.");
            }
            pairs.Clear();
            return false;
        }

        private void TryInject()
        {
            FindScene();
            ChaControl currentFemale, currentMale;
            if (!GetMarkedActors(out currentFemale, out currentMale) ||
                scene.lstUseAnimInfo == null || sprite.lstUseAnimInfo == null ||
                MemberwiseCloneMethod == null)
                return;

            int index = (int)HFlag.EMode.houshi;
            if (index < 0 || index >= scene.lstUseAnimInfo.Length ||
                index >= sprite.lstUseAnimInfo.Length)
                return;

            List<HSceneProc.AnimationListInfo> original =
                scene.lstUseAnimInfo[index];
            List<HSceneProc.AnimationListInfo> visible =
                sprite.lstUseAnimInfo[index];
            if (original == null || visible == null || original.Count == 0 ||
                !CacheMatchedBones(currentFemale, currentMale))
                return;

            // HSprite.Init has already partitioned Houshi into hand/mouth/
            // breast submenus, just as MultiTeamH observes. Rebuild both
            // those caches and the visible source list without altering a
            // donor's id, asset paths or native animation/controller data.
            List<HSceneProc.AnimationListInfo>[] submenus = null;
            try
            {
                submenus = Traverse.Create(sprite).Field("lstHoushiAnimInfo")
                    .GetValue<List<HSceneProc.AnimationListInfo>[]>();
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Reversed Houshi submenu lookup failed: " + ex.Message);
            }
            if (submenus == null || submenus.Length < 3)
                return;

            HSceneProc.AnimationListInfo[] donors = original.ToArray();
            int added = 0;
            for (int i = 0; i < donors.Length; i++)
            {
                HSceneProc.AnimationListInfo donor = donors[i];
                if (donor == null || donor.mode != HFlag.EMode.houshi ||
                    string.IsNullOrEmpty(donor.nameAnimation) ||
                    FemboyRules.IsReversedHoushiName(donor.nameAnimation))
                    continue;
                HSceneProc.AnimationListInfo clone =
                    MemberwiseCloneMethod.Invoke(donor, null)
                        as HSceneProc.AnimationListInfo;
                if (clone == null) continue;
                clone.nameAnimation = "[Reversed] " + donor.nameAnimation;
                reversed.Add(clone);
                original.Add(clone);
                if (!ReferenceEquals(original, visible)) visible.Add(clone);
                int kind = Math.Max(0, Math.Min(2, clone.kindHoushi));
                if (submenus[kind] == null)
                    submenus[kind] = new List<HSceneProc.AnimationListInfo>();
                submenus[kind].Add(clone);
                added++;
            }
            if (added == 0) return;
            injected = true;
            sprite.CreateActionList();
            FemboyModePlugin.LogOptionalIntegrationProblem(
                "Added " + added + " native Houshi [Reversed] actions; " +
                "shared-body animation pose swap is enabled for marked actors.");
        }

        internal void AnimationChanged(HSceneProc proc,
            HSceneProc.AnimationListInfo next)
        {
            if (!ReferenceEquals(scene, proc)) return;
            RestoreNativePose();
            active = next != null &&
                (reversed.Contains(next) ||
                 (next.mode == HFlag.EMode.houshi &&
                  FemboyRules.IsReversedHoushiName(next.nameAnimation))) &&
                proc.flags != null && proc.flags.isFreeH;
        }

        private void OnDestroy()
        {
            RestoreNativePose();
            if (restorer != null) restorer.Controller = null;
            reversed.Clear();
            pairs.Clear();
        }
    }
}
