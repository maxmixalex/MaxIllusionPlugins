using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FemboyMode
{
    // Keep the *native* male/female AnimatorControllers, avatar bindings and
    // facial layers.  Exchange matching motion clips only for the selected
    // Houshi action.  Never copy raw cf_*/cm_* bone rotations across rigs.
    internal static class FemboyHoushiReverseHooks
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.Init))]
        private static void OnSpriteInitialized(HSprite __instance)
        {
            if (__instance == null) return;
            var controller = __instance.GetComponent<FemboyHoushiReverseController>();
            if (controller == null)
                controller = __instance.gameObject.AddComponent<FemboyHoushiReverseController>();
            controller.Bind(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.CreateActionList))]
        private static void OnActionListBuilt(HSprite __instance)
        {
            if (__instance == null) return;
            var controller = __instance.GetComponent<FemboyHoushiReverseController>();
            if (controller != null) controller.MenuRebuilt();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSprite), "CreateMotionList")]
        private static void OnMotionListBuilt(HSprite __instance)
        {
            if (__instance == null) return;
            var controller = __instance.GetComponent<FemboyHoushiReverseController>();
            if (controller != null) controller.DecorateRows();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void BeforeChangeAnimator(HSceneProc __instance)
        {
            if (__instance == null || __instance.sprite == null) return;
            var controller = __instance.sprite.GetComponent<FemboyHoushiReverseController>();
            if (controller != null) controller.BeforeAnimationChanged();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        private static void AfterChangeAnimator(HSceneProc __instance,
            HSceneProc.AnimationListInfo _nextAinmInfo)
        {
            if (__instance == null || __instance.sprite == null) return;
            var controller = __instance.sprite.GetComponent<FemboyHoushiReverseController>();
            if (controller != null) controller.AnimationChanged(__instance, _nextAinmInfo);
        }
    }

    public sealed class FemboyHoushiReverseController : MonoBehaviour
    {
        private sealed class InstalledOverride
        {
            internal Animator Animator;
            internal RuntimeAnimatorController Original;
            internal AnimatorOverrideController Override;
        }

        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<HSceneProc.AnimationListInfo,
            HSceneProc.AnimationListInfo> clones =
            new Dictionary<HSceneProc.AnimationListInfo, HSceneProc.AnimationListInfo>();
        private readonly HashSet<HSceneProc.AnimationListInfo> reversed =
            new HashSet<HSceneProc.AnimationListInfo>();
        private HSprite sprite;
        private HSceneProc scene;
        private InstalledOverride femaleOverride;
        private InstalledOverride maleOverride;
        private int nextScanFrame;
        private int nextLabelFrame;
        private bool rebuilding;
        private bool alreadyReported;
        private bool active;

        internal void Bind(HSprite sceneSprite)
        {
            sprite = sceneSprite;
            nextScanFrame = Time.frameCount;
        }

        internal void MenuRebuilt()
        {
            if (rebuilding) return;
            nextScanFrame = Time.frameCount + 1;
            nextLabelFrame = Time.frameCount + 1;
        }

        private void Update()
        {
            if (sprite == null) return;
            if (Time.frameCount >= nextScanFrame)
            {
                nextScanFrame = Time.frameCount + 45;
                RefreshMenu();
            }
            if (Time.frameCount >= nextLabelFrame)
            {
                nextLabelFrame = Time.frameCount + 30;
                DecorateRows();
            }
            if (active && (scene == null || scene.flags == null ||
                scene.flags.mode != HFlag.EMode.houshi ||
                FemboyModePlugin.EnableReverseHoushi == null ||
                !FemboyModePlugin.EnableReverseHoushi.Value))
            {
                RestoreControllers();
                active = false;
            }
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

        private bool GetMarkedActors(out Animator heroineAnimator,
            out Animator partnerAnimator)
        {
            heroineAnimator = null;
            partnerAnimator = null;
            if (scene == null || scene.flags == null || !scene.flags.isFreeH ||
                scene.lstFemale == null || scene.lstFemale.Count != 1 ||
                scene.male == null ||
                FemboyModePlugin.EnableReverseHoushi == null ||
                !FemboyModePlugin.EnableReverseHoushi.Value)
                return false;

            ChaControl heroine = scene.lstFemale[0];
            if (heroine == null || !FemboyModePlugin.IsFemboy(heroine) ||
                heroine.animBody == null || scene.male.animBody == null)
                return false;
            heroineAnimator = heroine.animBody;
            partnerAnimator = scene.male.animBody;
            return true;
        }

        private static void AppendUnique(List<HSceneProc.AnimationListInfo> list,
            HashSet<HSceneProc.AnimationListInfo> seen,
            HSceneProc.AnimationListInfo candidate)
        {
            if (candidate == null || candidate.mode != HFlag.EMode.houshi ||
                FemboyRules.IsReversedHoushiName(candidate.nameAnimation) ||
                !seen.Add(candidate))
                return;
            list.Add(candidate);
        }

        private static int CategoryFor(HSceneProc.AnimationListInfo donor,
            List<HSceneProc.AnimationListInfo>[] submenus)
        {
            for (int i = 0; i < 3; i++)
                if (submenus[i] != null && submenus[i].Contains(donor)) return i;
            string name = donor.nameAnimation ?? string.Empty;
            if (name.IndexOf("口", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("舌", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("blow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("suck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("oral", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            if (name.IndexOf("胸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("乳交", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("breast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("titjob", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            return donor.kindHoushi >= 0 && donor.kindHoushi <= 2
                ? donor.kindHoushi : 0;
        }

        private void RefreshMenu()
        {
            FindScene();
            Animator heroine, partner;
            if (!GetMarkedActors(out heroine, out partner) ||
                scene.lstUseAnimInfo == null || sprite.lstUseAnimInfo == null ||
                CloneMethod == null || rebuilding)
                return;
            int index = (int)HFlag.EMode.houshi;
            if (index >= scene.lstUseAnimInfo.Length ||
                index >= sprite.lstUseAnimInfo.Length)
                return;
            List<HSceneProc.AnimationListInfo> source = scene.lstUseAnimInfo[index];
            List<HSceneProc.AnimationListInfo> visible = sprite.lstUseAnimInfo[index];
            if (source == null || visible == null) return;
            List<HSceneProc.AnimationListInfo>[] submenus;
            try
            {
                submenus = Traverse.Create(sprite).Field("lstHoushiAnimInfo")
                    .GetValue<List<HSceneProc.AnimationListInfo>[]>();
            }
            catch (Exception) { return; }
            if (submenus == null || submenus.Length < 3) return;

            // Native and kPlug/AnimationLoader can install entries into
            // *different* live collections. Use their union, including all
            // three Houshi submenus, and revisit when those lists are rebuilt.
            var donors = new List<HSceneProc.AnimationListInfo>();
            var seen = new HashSet<HSceneProc.AnimationListInfo>();
            for (int i = 0; i < source.Count; i++)
                AppendUnique(donors, seen, source[i]);
            if (!ReferenceEquals(source, visible))
                for (int i = 0; i < visible.Count; i++)
                    AppendUnique(donors, seen, visible[i]);
            for (int k = 0; k < 3; k++)
                if (submenus[k] != null)
                    for (int i = 0; i < submenus[k].Count; i++)
                        AppendUnique(donors, seen, submenus[k][i]);

            // Animation Loader keeps its imported kPlug-compatible motions
            // in the same map that MultiTeamH uses. Only entries belonging
            // to Houshi are eligible; the donor must provide a full 1F1M rig.
            Type loader = AccessTools.TypeByName("AnimationLoader.SwapAnim");
            FieldInfo field = loader == null ? null :
                AccessTools.Field(loader, "swapAnimationMapping");
            IDictionary mapping = field == null ? null : field.GetValue(null) as IDictionary;
            if (mapping != null)
                foreach (DictionaryEntry entry in mapping)
                    AppendUnique(donors, seen, entry.Key as HSceneProc.AnimationListInfo);

            int newCount = 0;
            for (int i = 0; i < donors.Count; i++)
            {
                HSceneProc.AnimationListInfo donor = donors[i];
                if (string.IsNullOrEmpty(donor.nameAnimation) ||
                    donor.paramFemale == null || donor.paramMale == null ||
                    (donor.pathFemaleBase1 != null &&
                     (!string.IsNullOrEmpty(donor.pathFemaleBase1.assetpath) ||
                      !string.IsNullOrEmpty(donor.pathFemaleBase1.file))))
                    continue;

                HSceneProc.AnimationListInfo clone;
                if (!clones.TryGetValue(donor, out clone))
                {
                    clone = CloneMethod.Invoke(donor, null)
                        as HSceneProc.AnimationListInfo;
                    if (clone == null) continue;
                    clone.nameAnimation = "[Reversed] " + donor.nameAnimation;
                    clones.Add(donor, clone);
                    reversed.Add(clone);
                }
                int category = CategoryFor(donor, submenus);
                clone.kindHoushi = category;
                bool justAdded = false;
                if (!source.Contains(clone)) { source.Add(clone); justAdded = true; }
                if (!ReferenceEquals(source, visible) && !visible.Contains(clone))
                { visible.Add(clone); justAdded = true; }
                if (submenus[category] == null)
                    submenus[category] = new List<HSceneProc.AnimationListInfo>();
                if (!submenus[category].Contains(clone))
                { submenus[category].Add(clone); justAdded = true; }
                if (justAdded) newCount++;
            }
            if (newCount > 0)
            {
                rebuilding = true;
                try { sprite.CreateActionList(); }
                finally { rebuilding = false; }
                DecorateRows();
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] catalog synchronized: newly added=" +
                    newCount + " total=" + reversed.Count +
                    " (native + kPlug/AnimationLoader). In-game labels localized.");
                alreadyReported = true;
            }
            else if (!alreadyReported && donors.Count > 0)
            {
                alreadyReported = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] catalog already synchronized: " +
                    reversed.Count + " actions.");
            }
        }

        internal void DecorateRows()
        {
            if (sprite == null || reversed.Count == 0) return;
            HSprite.AnimationInfoComponent[] entries =
                sprite.GetComponentsInChildren<HSprite.AnimationInfoComponent>(true);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.info == null ||
                    (!reversed.Contains(entry.info) &&
                     !FemboyRules.IsReversedHoushiName(entry.info.nameAnimation)))
                    continue;
                string text = FemboyHoushiLocalizer.LocalizeReversed(
                    entry.info.nameAnimation);
                TMP_Text tmp = entry.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null && tmp.text != text) tmp.text = text;
                Text ui = entry.GetComponentInChildren<Text>(true);
                if (ui != null && ui.text != text) ui.text = text;
            }
        }

        internal void BeforeAnimationChanged()
        {
            RestoreControllers();
            active = false;
        }

        internal void AnimationChanged(HSceneProc proc,
            HSceneProc.AnimationListInfo next)
        {
            if (proc == null || next == null) return;
            if (scene == null) scene = proc;
            if (scene != proc) return;
            if (next.mode != HFlag.EMode.houshi ||
                (!reversed.Contains(next) &&
                 !FemboyRules.IsReversedHoushiName(next.nameAnimation)))
                return;

            Animator heroine, partner;
            if (!GetMarkedActors(out heroine, out partner)) return;
            InstallControllers(heroine, partner);
        }

        private void InstallControllers(Animator heroine, Animator partner)
        {
            if (heroine == null || partner == null ||
                heroine.runtimeAnimatorController == null ||
                partner.runtimeAnimatorController == null)
                return;
            RuntimeAnimatorController originalFemale = heroine.runtimeAnimatorController;
            RuntimeAnimatorController originalMale = partner.runtimeAnimatorController;
            if (originalFemale is AnimatorOverrideController ||
                originalMale is AnimatorOverrideController)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi reversal skipped: another plugin already controls " +
                    "the actor's AnimatorOverrideController. Native rigs preserved.");
                return;
            }

            var femaleClips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            var maleClips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (AnimationClip clip in originalFemale.animationClips)
                if (clip != null && FemboyRules.IsHoushiBodyMotionClip(clip.name) &&
                    !femaleClips.ContainsKey(clip.name))
                    femaleClips.Add(clip.name, clip);
            foreach (AnimationClip clip in originalMale.animationClips)
                if (clip != null && FemboyRules.IsHoushiBodyMotionClip(clip.name) &&
                    !maleClips.ContainsKey(clip.name))
                    maleClips.Add(clip.name, clip);

            int matched = 0;
            foreach (var pair in femaleClips)
                if (maleClips.ContainsKey(pair.Key)) matched++;
            // Missing body clips means there is no compatible native role
            // pair for this imported action. Never fall back to 278 raw bones.
            if (matched < 3)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] skipped: no compatible paired motion " +
                    "clips for this native/kPlug action (matched=" + matched + ").");
                return;
            }

            AnimatorOverrideController forFemale = null;
            AnimatorOverrideController forMale = null;
            try
            {
                forFemale = new AnimatorOverrideController(originalFemale);
                forMale = new AnimatorOverrideController(originalMale);
                foreach (var pair in femaleClips)
                {
                    AnimationClip other;
                    if (!maleClips.TryGetValue(pair.Key, out other)) continue;
                    forFemale[pair.Key] = other;
                    forMale[pair.Key] = pair.Value;
                }
                femaleOverride = new InstalledOverride
                { Animator = heroine, Original = originalFemale, Override = forFemale };
                maleOverride = new InstalledOverride
                { Animator = partner, Original = originalMale, Override = forMale };
                heroine.runtimeAnimatorController = forFemale;
                partner.runtimeAnimatorController = forMale;
                active = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] installed matched motion-clip overrides=" +
                    matched + "; both native AnimatorControllers/avatars retained; " +
                    "no cross-rig bone or facial transform writes.");
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] native clip retarget failed safely: " +
                    ex.GetType().Name + ": " + ex.Message);
                RestoreControllers();
            }
        }

        private static void RestoreOne(InstalledOverride value)
        {
            if (value == null) return;
            if (value.Animator != null &&
                ReferenceEquals(value.Animator.runtimeAnimatorController, value.Override))
                value.Animator.runtimeAnimatorController = value.Original;
            if (value.Override != null)
                UnityEngine.Object.Destroy(value.Override);
        }

        private void RestoreControllers()
        {
            RestoreOne(femaleOverride);
            RestoreOne(maleOverride);
            femaleOverride = null;
            maleOverride = null;
            active = false;
        }

        private void OnDestroy()
        {
            RestoreControllers();
            clones.Clear();
            reversed.Clear();
        }
    }
}