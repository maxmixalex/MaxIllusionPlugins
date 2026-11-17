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
        private static readonly Dictionary<int, float> LastEffectByFlag =
            new Dictionary<int, float>();
        private static bool warnedUnavailable;

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
                !(FemboyModePlugin.IsFemboy(heroine.chaCtrl) ||
                  FemboyModePlugin.IsFemboy(heroine.charFile)))
                return;

            ChaControl character = heroine.chaCtrl;
            if (character == null || character.objBodyBone == null ||
                flags.particle == null || ParticleEntriesField == null)
                return;

            int key = flags.GetInstanceID();
            float now = Time.time;
            float last;
            if (LastEffectByFlag.TryGetValue(key, out last) && now - last < 1.5f)
                return;

            var entries = ParticleEntriesField.GetValue(flags.particle)
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

            Transform bone = FindBone(character.objBodyBone.transform,
                FemboyModePlugin.KokanAnchorBone.Value);
            if (bone == null)
                bone = FindBone(character.objBodyBone.transform, "cf_d_kokan");
            if (bone == null)
                return;

            GameObject clone = UnityEngine.Object.Instantiate(
                maleEffect.particle.gameObject);
            clone.name = "FemboyMode_NativeMaleFinishFX";
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
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Simulate(0f);
                particles[i].Play();
            }

            LastEffectByFlag[key] = now;
            if (LastEffectByFlag.Count > 20)
                LastEffectByFlag.Clear();

            UnityEngine.Object.Destroy(clone, 12f);
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root == null || string.IsNullOrEmpty(boneName))
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, boneName, StringComparison.Ordinal))
                    return all[i];
            }
            return null;
        }
    }
}
