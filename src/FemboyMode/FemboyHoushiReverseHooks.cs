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
            if (active && (femaleOverride == null || maleOverride == null ||
                femaleOverride.Animator == null || maleOverride.Animator == null ||
                !ReferenceEquals(femaleOverride.Animator.runtimeAnimatorController,
                    femaleOverride.Override) ||
                !ReferenceEquals(maleOverride.Animator.runtimeAnimatorController,
                    maleOverride.Override)))
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] controller replaced during playback; releasing role layers.");
                RestoreControllers();
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
                    // Native labels must be correct even before pooled UI
                    // rows have been decorated.
                    clone.nameAnimation = FemboyHoushiLocalizer.LocalizeReversed(
                        "[Reversed] " + donor.nameAnimation);
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


        // Koikatsu and kPlug legitimately create their own AnimatorOverrideController.
        // Unity 5.6 does not support nested roots: preserve the old effective
        // bindings and put our temporary overrides on the original controller.
        private sealed class ClipSnapshot
        {
            internal RuntimeAnimatorController Root;
            internal readonly Dictionary<AnimationClip, AnimationClip> EffectiveByOriginal =
                new Dictionary<AnimationClip, AnimationClip>();
            internal readonly Dictionary<string, AnimationClip> EffectiveByName =
                new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            internal int ExistingOverrides;
        }

        private static ClipSnapshot CaptureClips(RuntimeAnimatorController current)
        {
            if (current == null) return null;
            var chain = new List<AnimatorOverrideController>();
            RuntimeAnimatorController root = current;
            for (int depth = 0; depth < 8; depth++)
            {
                AnimatorOverrideController overlay =
                    root as AnimatorOverrideController;
                if (overlay == null) break;
                chain.Add(overlay);
                RuntimeAnimatorController next = overlay.runtimeAnimatorController;
                if (next == null || ReferenceEquals(next, root)) return null;
                root = next;
            }
            if (root == null || root is AnimatorOverrideController) return null;
            var snapshot = new ClipSnapshot { Root = root };
            AnimationClip[] baseClips = root.animationClips;
            if (baseClips == null) return null;
            for (int j = 0; j < baseClips.Length; j++)
                if (baseClips[j] != null)
                    snapshot.EffectiveByOriginal[baseClips[j]] = baseClips[j];
            // Applying inner layers first makes the outermost mod's override
            // authoritative, while retaining all unrelated clip mappings.
            for (int layer = chain.Count - 1; layer >= 0; layer--)
            {
                AnimationClipPair[] entries = chain[layer].clips;
                if (entries == null) continue;
                foreach (AnimationClipPair entry in entries)
                {
                    if (entry == null || entry.originalClip == null ||
                        entry.overrideClip == null) continue;
                    foreach (AnimationClip original in baseClips)
                    {
                        if (original == null || original.name != entry.originalClip.name)
                            continue;
                        snapshot.EffectiveByOriginal[original] = entry.overrideClip;
                        snapshot.ExistingOverrides++;
                    }
                }
            }
            foreach (var pair in snapshot.EffectiveByOriginal)
                if (pair.Key != null && pair.Value != null &&
                    !snapshot.EffectiveByName.ContainsKey(pair.Key.name))
                    snapshot.EffectiveByName.Add(pair.Key.name, pair.Value);
            return snapshot;
        }

        private static AnimatorOverrideController CreateLayer(
            ClipSnapshot own, ClipSnapshot other, out int roleClips)
        {
            roleClips = 0;
            if (own == null || other == null) return null;
            AnimatorOverrideController result =
                new AnimatorOverrideController(own.Root);
            foreach (var pair in own.EffectiveByOriginal)
            {
                AnimationClip baseClip = pair.Key;
                AnimationClip effective = pair.Value;
                if (baseClip == null || effective == null) continue;
                AnimationClip partnerClip;
                if (FemboyRules.IsHoushiBodyMotionClip(baseClip.name) &&
                    other.EffectiveByName.TryGetValue(baseClip.name, out partnerClip) &&
                    partnerClip != null && !ReferenceEquals(partnerClip, effective))
                {
                    effective = partnerClip;
                    roleClips++;
                }
                if (!ReferenceEquals(baseClip, effective))
                    result[baseClip] = effective;
            }
            return result;
        }

        private void InstallControllers(Animator heroine, Animator partner)
        {
            if (heroine == null || partner == null ||
                heroine.runtimeAnimatorController == null ||
                partner.runtimeAnimatorController == null) return;
            RuntimeAnimatorController previousHeroine =
                heroine.runtimeAnimatorController;
            RuntimeAnimatorController previousPartner =
                partner.runtimeAnimatorController;
            AnimatorOverrideController overlayHeroine = null;
            AnimatorOverrideController overlayPartner = null;
            try
            {
                ClipSnapshot own = CaptureClips(previousHeroine);
                ClipSnapshot other = CaptureClips(previousPartner);
                if (own == null || other == null)
                {
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Houshi reversal cannot resolve the existing AnimatorOverrideController chain.");
                    return;
                }
                int heroineClips, partnerClips;
                overlayHeroine = CreateLayer(own, other, out heroineClips);
                overlayPartner = CreateLayer(other, own, out partnerClips);
                if (overlayHeroine == null || overlayPartner == null ||
                    heroineClips < 3 || partnerClips < 3)
                {
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Houshi reversal lacks actor-compatible paired clips: heroine=" +
                        heroineClips + " partner=" + partnerClips + ".");
                    if (overlayHeroine != null) UnityEngine.Object.Destroy(overlayHeroine);
                    if (overlayPartner != null) UnityEngine.Object.Destroy(overlayPartner);
                    return;
                }
                femaleOverride = new InstalledOverride
                { Animator = heroine, Original = previousHeroine, Override = overlayHeroine };
                maleOverride = new InstalledOverride
                { Animator = partner, Original = previousPartner, Override = overlayPartner };
                heroine.runtimeAnimatorController = overlayHeroine;
                partner.runtimeAnimatorController = overlayPartner;
                active = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi [Reversed] installed matched motion-clip overrides=" +
                    Math.Min(heroineClips, partnerClips) +
                    " (heroine=" + heroineClips + " partner=" + partnerClips +
                    ", preserved prior overrides=" + own.ExistingOverrides +
                    "/" + other.ExistingOverrides + ").");
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi reversal clip-layer error: " +
                    ex.GetType().Name + ": " + ex.Message);
                RestoreControllers();
                if (overlayHeroine != null &&
                    (femaleOverride == null ||
                     !ReferenceEquals(femaleOverride.Override, overlayHeroine)))
                    UnityEngine.Object.Destroy(overlayHeroine);
                if (overlayPartner != null &&
                    (maleOverride == null ||
                     !ReferenceEquals(maleOverride.Override, overlayPartner)))
                    UnityEngine.Object.Destroy(overlayPartner);
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