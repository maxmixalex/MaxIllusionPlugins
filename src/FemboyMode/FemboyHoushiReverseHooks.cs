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

        // The original OnChangePlaySelect discards actions whose id equals
        // the current donor pose. Cloned reversals intentionally retain the
        // donor's ids and asset information: handle the selected object by
        // exact action reference and request one native ChangeAnimator.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(HSprite), nameof(HSprite.OnChangePlaySelect))]
        private static bool BeforeSelect(HSprite __instance, object[] __args)
        {
            if (__instance == null || __args == null || __args.Length == 0)
                return true;
            var controller = __instance.GetComponent<FemboyHoushiReverseController>();
            return controller == null || !controller.TrySelectReversed(
                __args[0] as GameObject);
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
        [HarmonyPriority(Priority.Last)]
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
        // A donor can be copied by kPlug/AnimationLoader and by HSprite list
        // rebuilding. Key by the actual underlying animation, never by the
        // temporary AnimationListInfo reference.
        private readonly Dictionary<string, HSceneProc.AnimationListInfo> clones =
            new Dictionary<string, HSceneProc.AnimationListInfo>(StringComparer.Ordinal);
        private readonly HashSet<HSceneProc.AnimationListInfo> reversed =
            new HashSet<HSceneProc.AnimationListInfo>();
        private HSprite sprite;
        private HSceneProc scene;
        private InstalledOverride femaleOverride;
        private InstalledOverride maleOverride;
        private int nextScanFrame;
        private int nextLabelFrame;
        private bool rebuilding;
        private bool inDirectSelection;
        private int missingMappingWarned;
        private bool alreadyReported;
        private bool active;
        private int repairAttempts;
        private int nextRepairFrame;

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
                    "Houshi [Reversed] controller replaced during playback; checking native action.");
                RestoreControllers();
                HSceneProc.AnimationListInfo selected = scene != null &&
                    scene.flags != null ? scene.flags.nowAnimationInfo : null;
                Animator currentHeroine, currentPartner;
                if (repairAttempts < 2 && Time.frameCount >= nextRepairFrame &&
                    selected != null && selected.mode == HFlag.EMode.houshi &&
                    FemboyRules.IsReversedHoushiName(selected.nameAnimation) &&
                    GetMarkedActors(out currentHeroine, out currentPartner))
                {
                    repairAttempts++;
                    nextRepairFrame = Time.frameCount + 30;
                    InstallControllers(currentHeroine, currentPartner);
                }
            }
        }

        internal bool TrySelectReversed(GameObject clicked)
        {
            if (clicked == null || inDirectSelection) return false;
            HSprite.AnimationInfoComponent component =
                clicked.GetComponent<HSprite.AnimationInfoComponent>();
            HSceneProc.AnimationListInfo selected =
                component == null ? null : component.info;
            if (selected == null || !reversed.Contains(selected))
                return false;

            FindScene();
            Animator heroine, partner;
            if (!GetMarkedActors(out heroine, out partner) || scene == null)
                return false;
            inDirectSelection = true;
            try
            {
                // Do not run OnChangePlaySelect, which compares the duplicate's
                // donor id and can silently ignore an otherwise valid click.
                scene.flags.selectAnimationListInfo = null;
                scene.ChangeAnimator(selected, true);
                scene.flags.selectAnimationListInfo = null;
                scene.flags.voiceWait = false;
                Toggle toggled = clicked.GetComponent<Toggle>();
                if (toggled != null) toggled.isOn = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi reversed action selected directly: id=" +
                    selected.id + " kind=" + selected.kindHoushi +
                    " name=" + selected.nameAnimation);
                return true;
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Houshi reversed native selection failed safely: " +
                    ex.GetType().Name + ": " + ex.Message);
                return true;
            }
            finally { inDirectSelection = false; }
        }

        private static IDictionary GetImportMapping()
        {
            Type loader = AccessTools.TypeByName("AnimationLoader.SwapAnim");
            if (loader == null) return null;
            PropertyInfo property = AccessTools.Property(loader, "SwapAnimationMapping");
            object map = property == null ? null : property.GetValue(null, null);
            if (map == null)
            {
                FieldInfo field = AccessTools.Field(loader, "swapAnimationMapping");
                map = field == null ? null : field.GetValue(null);
            }
            return map as IDictionary;
        }

        private static string ReadImportString(object metadata, string field)
        {
            if (metadata == null) return "";
            FieldInfo info = AccessTools.Field(metadata.GetType(), field);
            if (info != null) return info.GetValue(metadata) as string ?? "";
            PropertyInfo property = AccessTools.Property(metadata.GetType(), field);
            return property == null ? "" :
                property.GetValue(metadata, null) as string ?? "";
        }

        private static string ImportSourceKey(object metadata)
        {
            if (metadata == null) return null;
            string name = ReadImportString(metadata, "AnimationName");
            string female = ReadImportString(metadata, "PathFemale");
            string male = ReadImportString(metadata, "PathMale");
            string femaleClip = ReadImportString(metadata, "ControllerFemale");
            string maleClip = ReadImportString(metadata, "ControllerMale");
            string guid = ReadImportString(metadata, "Guid");
            return "import:" + guid + "|" + name + "|" + female + "|" +
                femaleClip + "|" + male + "|" + maleClip;
        }

        // AnimationLoader's two Harmony hooks read the SAME mapping:
        // LoadMotionList uses AnimationName for its UI, while ChangeAnimator
        // resolves imported female/male controllers from its path fields.
        // Register the clone with a copied metadata object or the button
        // displays the donor's label and plays the donor without imports.
        private bool BindImportedClone(IDictionary mapping,
            HSceneProc.AnimationListInfo original,
            HSceneProc.AnimationListInfo clone, object metadata,
            string localizedLabel)
        {
            if (mapping == null || metadata == null) return true;
            try
            {
                object replacement = CloneMethod.Invoke(metadata, null);
                FieldInfo label = AccessTools.Field(replacement.GetType(), "AnimationName");
                if (label == null || label.FieldType != typeof(string))
                    return false;
                label.SetValue(replacement, localizedLabel);
                mapping[clone] = replacement;
                return true;
            }
            catch (Exception ex)
            {
                if (missingMappingWarned++ < 3)
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Houshi clone could not retain AnimationLoader mapping: " +
                        ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private bool SceneAllowsImport(HSceneProc.AnimationListInfo donor)
        {
            if (donor == null || donor.lstCategory == null ||
                donor.lstCategory.Count == 0) return true;
            List<int> current = null;
            try
            {
                current = Traverse.Create(scene).Field("categorys")
                    .GetValue<List<int>>();
            }
            catch (Exception) { }
            if (current == null || current.Count == 0) return true;
            for (int i = 0; i < donor.lstCategory.Count; i++)
                if (donor.lstCategory[i] != null &&
                    current.Contains(donor.lstCategory[i].category))
                    return true;
            return false;
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

            // Scan only CURRENTLY usable native actions and imported actions
            // that the current H-point category actually permits. Appending
            // every imported and donor pose unconditionally was producing
            // hundreds of duplicate-looking buttons.
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

            IDictionary mapping = GetImportMapping();
            var imports = new Dictionary<HSceneProc.AnimationListInfo, object>();
            int seenImports = 0;
            if (mapping != null)
            {
                foreach (DictionaryEntry entry in mapping)
                {
                    HSceneProc.AnimationListInfo key =
                        entry.Key as HSceneProc.AnimationListInfo;
                    if (key == null || key.mode != HFlag.EMode.houshi ||
                        !SceneAllowsImport(key)) continue;
                    imports[key] = entry.Value;
                    AppendUnique(donors, seen, key);
                    seenImports++;
                }
            }

            int newCount = 0, nativeCreated = 0, importedCreated = 0;
            var uniqueThisPass = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < donors.Count; i++)
            {
                HSceneProc.AnimationListInfo donor = donors[i];
                if (string.IsNullOrEmpty(donor.nameAnimation) ||
                    donor.paramFemale == null || donor.paramMale == null ||
                    (donor.pathFemaleBase1 != null &&
                     (!string.IsNullOrEmpty(donor.pathFemaleBase1.assetpath) ||
                      !string.IsNullOrEmpty(donor.pathFemaleBase1.file))))
                    continue;

                object metadata;
                imports.TryGetValue(donor, out metadata);
                if (metadata == null && mapping != null && mapping.Contains(donor))
                    metadata = mapping[donor];
                string importedKey = ImportSourceKey(metadata);
                string key = importedKey ??
                    ("native:" + donor.id + "|" + donor.kindHoushi +
                     "|" + donor.nameAnimation);
                if (!uniqueThisPass.Add(key)) continue;

                string name = metadata == null ? donor.nameAnimation :
                    ReadImportString(metadata, "AnimationName");
                if (string.IsNullOrEmpty(name)) name = donor.nameAnimation;
                string label = FemboyHoushiLocalizer.LocalizeReversed(
                    "[Reversed] " + name);

                HSceneProc.AnimationListInfo clone;
                if (!clones.TryGetValue(key, out clone))
                {
                    clone = CloneMethod.Invoke(donor, null)
                        as HSceneProc.AnimationListInfo;
                    if (clone == null) continue;
                    clone.nameAnimation = label;
                    if (!BindImportedClone(mapping, donor, clone, metadata, label))
                        continue;
                    clones.Add(key, clone);
                    reversed.Add(clone);
                    if (metadata == null) nativeCreated++;
                    else importedCreated++;
                }
                else
                {
                    // AnimationLoader resets its map when a new catalogue is
                    // created. Repair the existing clone mapping, not another
                    // menu row, if this happens inside the same Free H session.
                    if (metadata != null && mapping != null &&
                        !mapping.Contains(clone))
                        if (!BindImportedClone(mapping, donor, clone, metadata, label))
                            continue;
                    clone.nameAnimation = label;
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
                    " sourceNative=" + nativeCreated +
                    " sourceImported=" + importedCreated +
                    " importedEligible=" + seenImports +
                    " mappingAvailable=" + (mapping != null) +
                    ". Native/kPlug labels and controller bindings preserved.");
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
            repairAttempts = 0;
            nextRepairFrame = Time.frameCount + 2;
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
            var originalsByName = new Dictionary<string, List<AnimationClip>>(
                StringComparer.Ordinal);
            for (int j = 0; j < baseClips.Length; j++)
            {
                AnimationClip clip = baseClips[j];
                if (clip == null) continue;
                snapshot.EffectiveByOriginal[clip] = clip;
                List<AnimationClip> group;
                if (!originalsByName.TryGetValue(clip.name, out group))
                {
                    group = new List<AnimationClip>();
                    originalsByName.Add(clip.name, group);
                }
                group.Add(clip);
            }
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
                    List<AnimationClip> matching;
                    if (!originalsByName.TryGetValue(entry.originalClip.name,
                        out matching)) continue;
                    for (int k = 0; k < matching.Count; k++)
                    {
                        snapshot.EffectiveByOriginal[matching[k]] = entry.overrideClip;
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
            var changes = new List<KeyValuePair<AnimationClip, AnimationClip>>();
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
                    changes.Add(new KeyValuePair<AnimationClip, AnimationClip>(
                        baseClip, effective));
            }
            // Unity 5.6 API: applying each indexer replacement individually
            // reallocates all clip bindings; batch them once instead.
            if (changes.Count > 0) result.ApplyOverrides(changes);
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