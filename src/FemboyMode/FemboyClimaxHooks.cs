using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Reuses the native male *outside* finish particle (HParticleCtrl ID 0,
    /// also used by MetaballCtrl.Proc) at a marked female character's body
    /// instead of triggering an effect at the male player's position.
    /// Does not rewrite the native male actor's finish flags/counters.
    /// </summary>
    internal static class FemboyClimaxHooks
    {
        private static readonly FieldInfo ParticleEntriesField =
            typeof(HParticleCtrl).GetField("dicParticle",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo OwnerFlagField =
            typeof(HParticleCtrl).GetField("flags",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AibuRandomField =
            typeof(HAibu).GetField("randSio",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ActionFlagsField =
            typeof(HActionBase).GetField("flags",
                BindingFlags.Instance | BindingFlags.NonPublic);
        // Captured once per scene. ShuffleRand.Get is used throughout the game:
        // never search the Unity scene inside its hot-path Harmony prefix.
        private static readonly Dictionary<GlobalMethod.ShuffleRand, HFlag> AibuOwners =
            new Dictionary<GlobalMethod.ShuffleRand, HFlag>();
        // A Free H scene can have two heroines. Never deduplicate separate
        // characters just because they share the same HFlag instance.
        private static readonly Dictionary<long, float> LastEffectByCharacter =
            new Dictionary<long, float>();

        private static long EffectKey(HFlag flags)
        {
            return FemboyRules.EffectKey(flags.GetInstanceID(), flags.GetLeadingHeroineId());
        }

        private static bool HasRecentMaleEffect(HFlag flags)
        {
            float playedAt;
            return LastEffectByCharacter.TryGetValue(EffectKey(flags), out playedAt)
                && Time.time >= playedAt && Time.time - playedAt < 8f;
        }
        private static bool warnedUnavailable;
        private static bool warnedMissingBone;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HAibu), MethodType.Constructor,
            new[] { typeof(HFlag.DeliveryMember) })]
        private static void CaptureAibuOwner(HAibu __instance)
        {
            if (__instance == null || AibuRandomField == null ||
                ActionFlagsField == null)
                return;

            var random = AibuRandomField.GetValue(__instance)
                as GlobalMethod.ShuffleRand;
            var flags = ActionFlagsField.GetValue(__instance) as HFlag;
            if (random != null && flags != null)
                AibuOwners[random] = flags;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void CleanupSceneState(HSceneProc __instance)
        {
            if (__instance == null || __instance.flags == null)
                return;

            HFlag flags = __instance.flags;
            var expired = new List<GlobalMethod.ShuffleRand>();
            foreach (var pair in AibuOwners)
                if (ReferenceEquals(pair.Value, flags))
                    expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++)
                AibuOwners.Remove(expired[i]);

            int id = flags.GetInstanceID();
            LastEffectByCharacter.Remove(FemboyRules.EffectKey(id, 0));
            LastEffectByCharacter.Remove(FemboyRules.EffectKey(id, 1));
        }

        // Vanilla HAibu.Proc's randSio.Get decides the female-specific
        // particle AND its accompanying sound. Block only this exact
        // random instance, only after the alternate visual was played.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(GlobalMethod.ShuffleRand), nameof(GlobalMethod.ShuffleRand.Get))]
        private static bool FilterAibuFemaleEffectDecision(
            GlobalMethod.ShuffleRand __instance, ref int __result)
        {
            if (__instance == null ||
                FemboyModePlugin.SuppressOriginalFemaleFX == null ||
                !FemboyModePlugin.SuppressOriginalFemaleFX.Value ||
                FemboyModePlugin.EnableMaleClimaxFX == null ||
                !FemboyModePlugin.EnableMaleClimaxFX.Value)
                return true;

            HFlag flags;
            if (!AibuOwners.TryGetValue(__instance, out flags) ||
                flags == null || !flags.isFreeH ||
                flags.mode != HFlag.EMode.aibu ||
                !HasRecentMaleEffect(flags))
                return true;

            var heroine = flags.GetLeadingHeroine();
            if (heroine == null || !FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return true;

            __result = 0;
            return false;
        }

        // The vanilla Aibu particle #2 is the female squirt effect. When
        // selecting the alternate male effect on the first heroine, suppress
        // only this visual; preserve the original orgasm counters and states.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HParticleCtrl), nameof(HParticleCtrl.Play))]
        private static bool FilterFemaleSquirt(HParticleCtrl __instance,
            int _particle, ref bool __result)
        {
            if (_particle != 2 || __instance == null ||
                FemboyModePlugin.SuppressOriginalFemaleFX == null ||
                !FemboyModePlugin.SuppressOriginalFemaleFX.Value ||
                OwnerFlagField == null)
                return true;

            HFlag flags = OwnerFlagField.GetValue(__instance) as HFlag;
            if (flags == null || !flags.isFreeH ||
                !FemboyModePlugin.EnableMaleClimaxFX.Value)
                return true;

            var heroine = flags.GetLeadingHeroine();
            if (heroine == null || !(FemboyModePlugin.IsFemboy(heroine.chaCtrl)))
                return true;

            // In a two-heroine scene both HParticleCtrl instances share flags.
            // Suppress only the *active heroine's* particle, never her partner's.
            if (!ReferenceEquals(__instance, FindHParticleCtrl(flags)) ||
                !HasRecentMaleEffect(flags))
                return true;

            // If the replacement effect failed or its bone was unavailable,
            // preserve the original effect rather than producing nothing.
            __result = false;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddAibuOrg))]
        private static void AfterAibuOrg(HFlag __instance)
        {
            PlayNativeMaleEffect(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuOrg))]
        private static void AfterSonyuOrg(HFlag __instance)
        {
            PlayNativeMaleEffect(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuAnalOrg))]
        private static void AfterSonyuAnalOrg(HFlag __instance)
        {
            PlayNativeMaleEffect(__instance);
        }

        private static void PlayNativeMaleEffect(HFlag flags)
        {
            if (flags == null || !flags.isFreeH ||
                FemboyModePlugin.EnableMaleClimaxFX == null ||
                !FemboyModePlugin.EnableMaleClimaxFX.Value)
                return;

            var heroine = flags.GetLeadingHeroine();
            if (heroine == null ||
                !(FemboyModePlugin.IsFemboy(heroine.chaCtrl)))
                return;

            ChaControl character = heroine.chaCtrl;
            if (character == null || character.objBodyBone == null ||
                ParticleEntriesField == null)
                return;

            HParticleCtrl source = FindHParticleCtrl(flags);
            if (source == null)
                return;

            long key = EffectKey(flags);
            float now = Time.time;
            float last;
            if (LastEffectByCharacter.TryGetValue(key, out last) &&
                now >= last && now - last < 1.5f)
                return;

            var entries = ParticleEntriesField.GetValue(source)
                as Dictionary<int, HParticleCtrl.ParticleInfo>;
            HParticleCtrl.ParticleInfo maleEffect;
            if (entries == null || !entries.TryGetValue(0, out maleEffect) ||
                maleEffect == null || maleEffect.numParent != 0 ||
                maleEffect.particle == null)
            {
                if (!warnedUnavailable)
                {
                    warnedUnavailable = true;
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "Native male particle 0 was not loaded; no alternate finish FX.");
                }
                return;
            }

            Transform bone = FemboyRigResolver.FindBone(character.objBodyBone.transform,
                FemboyModePlugin.KokanAnchorBone.Value);
            if (bone == null)
                bone = FemboyRigResolver.FindBone(character.objBodyBone.transform, "cf_d_kokan");
            if (bone == null)
            {
                if (!warnedMissingBone)
                {
                    warnedMissingBone = true;
                    FemboyModePlugin.LogOptionalIntegrationProblem(
                        "No usable finish-FX target bone found on marked character. " +
                        "Check Maker/Male geometry bone and Aibu alignment/Target bone settings.");
                }
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(
                maleEffect.particle.gameObject);
            clone.name = "FemboyMode_NativeMaleFinishFX";
            clone.SetActive(false);
            clone.transform.SetParent(bone, false);
            clone.transform.localPosition = new Vector3(
                FemboyModePlugin.ClimaxFXOffsetX.Value,
                FemboyModePlugin.ClimaxFXOffsetY.Value,
                FemboyModePlugin.ClimaxFXOffsetZ.Value);
            clone.transform.localRotation = Quaternion.Euler(
                FemboyModePlugin.ClimaxFXRotationX.Value,
                FemboyModePlugin.ClimaxFXRotationY.Value,
                FemboyModePlugin.ClimaxFXRotationZ.Value);

            ParticleSystem[] particles = clone.GetComponentsInChildren<ParticleSystem>(true);
            if (particles.Length == 0)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            clone.SetActive(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].Simulate(0f);
                particles[i].Play();
            }

            LastEffectByCharacter[key] = now;
            if (LastEffectByCharacter.Count > 32)
                LastEffectByCharacter.Clear();

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Played actor-local male-style FX on " + bone.name + ".");

            UnityEngine.Object.Destroy(clone, 12f);
        }

        private static HParticleCtrl FindHParticleCtrl(HFlag flags)
        {
            if (flags == null) return null;

            // The exact original Assembly-CSharp.dll exposes both scene
            // particle fields. They are ordered by heroine index, unlike
            // FindObjectsOfType<HParticleCtrl>() which has no stable order.
            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                HSceneProc scene = scenes[i];
                if (scene == null || !ReferenceEquals(scene.flags, flags))
                    continue;
                return flags.GetLeadingHeroineId() == 0 ?
                    scene.particle : scene.particle1;
            }

            // Fallback for custom scenes: select the only matching instance.
            // When more than one exists, guessing would affect another actor.
            if (OwnerFlagField == null) return null;

            HParticleCtrl result = null;
            HParticleCtrl[] all = UnityEngine.Object.FindObjectsOfType<HParticleCtrl>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!ReferenceEquals(OwnerFlagField.GetValue(all[i]), flags))
                    continue;
                if (result != null) return null;
                result = all[i];
            }
            return result;
        }

    }
}
