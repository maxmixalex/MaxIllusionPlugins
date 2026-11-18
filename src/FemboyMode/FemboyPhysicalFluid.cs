using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KKAPI.Utilities;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Native MetaballShoot, not an HParticleCtrl visual imitation.
    /// The same projectile prefab and UpdateMeta renderer used by vanilla
    /// Sonyu are reused for the marked heroine's climax in Aibu/Sonyu.
    /// </summary>
    internal static class FemboyPhysicalFluidHooks
    {
        private static readonly FieldInfo MetaField = typeof(HSceneProc).GetField(
            "meta", BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool warnedMissingScene;

        private static readonly FieldInfo ActionFlagsField =
            typeof(HActionBase).GetField("flags",
                BindingFlags.Instance | BindingFlags.NonPublic);

        // The Aibu counter increments when Orgasm_Start begins. The
        // actual native climax effect follows only when its animation
        // advances to Orgasm_Loop. Attach our physical fluid to that
        // transition instead of shooting prematurely or twice.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HActionBase), nameof(HActionBase.SetPlay))]
        private static void OnAibuOrgasmLoop(HActionBase __instance,
            string _nextAnimation)
        {
            if (ActionFlagsField == null) return;

            HFlag flags = ActionFlagsField.GetValue(__instance) as HFlag;
            if (__instance is HAibu && _nextAnimation == "Orgasm_Loop")
            {
                Trigger(flags);
                return;
            }

            // Sonyu calls AddSonyuOrg/AddSonyuAnalOrg *before* the actual
            // WF/WS/SF/SS start state. Other Harmony patches may intercept
            // those counters. Catch the real state transition as a fallback.
            if (__instance is HSonyu && flags != null &&
                (int)flags.finish >= 3 && (int)flags.finish <= 6 &&
                FemboyRules.IsFemaleInsertionFinishStart(_nextAnimation))
                Trigger(flags);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuOrg))]
        private static void OnSonyuClimax(HFlag __instance)
        {
            Trigger(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuAnalOrg))]
        private static void OnSonyuAnalClimax(HFlag __instance)
        {
            Trigger(__instance);
        }

        private static void Trigger(HFlag flags)
        {
            if (flags == null || !flags.isFreeH ||
                FemboyModePlugin.EnablePhysicalFluid == null ||
                !FemboyModePlugin.EnablePhysicalFluid.Value ||
                Manager.Config.EtcData.SemenType == 0)
                return;

            var heroine = flags.GetLeadingHeroine();
            if (heroine == null || heroine.chaCtrl == null ||
                !FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return;

            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                HSceneProc scene = scenes[i];
                if (scene == null || !ReferenceEquals(scene.flags, flags))
                    continue;

                ChaControl character = heroine.chaCtrl;
                FemboyPhysicalFluidEmitter emitter =
                    character.gameObject.GetComponent<FemboyPhysicalFluidEmitter>();
                if (emitter == null)
                    emitter = character.gameObject.AddComponent<FemboyPhysicalFluidEmitter>();
                emitter.Fire(scene, character, MetaField);
                return;
            }

            if (!warnedMissingScene)
            {
                warnedMissingScene = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Unable to locate HSceneProc for marked character's native fluid.");
            }
        }
    }

    public sealed class FemboyPhysicalFluidEmitter : MonoBehaviour
    {
        private HSceneProc owner;
        private ChaControl character;
        private MetaballShoot shooter;
        private UpdateMeta host;
        private GameObject axis;
        private GameObject privateSourceRoot;
        private float lastFired = -999f;
        private float activeUntil;
        private float[] nativeIntervals;
        private bool warnedUnavailable;

        public void Fire(HSceneProc scene, ChaControl target, FieldInfo metaField)
        {
            if (scene == null || target == null || target.objBodyBone == null)
                return;
            if (Time.time - lastFired < 1f)
                return;

            if (!ReferenceEquals(owner, scene) ||
                !ReferenceEquals(character, target) || shooter == null)
            {
                Cleanup();
                owner = scene;
                character = target;
                if (!Initialize(metaField))
                {
                    if (!warnedUnavailable)
                    {
                        warnedUnavailable = true;
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Native liquid fluid setup unavailable: no UpdateMeta source, " +
                            "metaball projectile or compatible target bone. " +
                            "Vanilla visual particle retained.");
                    }
                    return;
                }
                warnedUnavailable = false;
            }

            if (axis == null || shooter == null || shooter.aInterval == null)
                return;

            // Scale the *number of physical droplets* rather than visual
            // particle alpha. Never mutate the real male's source preset.
            ApplyFluidAmount();
            shooter.ShootMetaBallStart(false);
            float duration = 0f;
            for (int i = 0; i < shooter.aInterval.Length; i++)
                duration += Mathf.Max(0f, shooter.aInterval[i]);
            activeUntil = Time.time + Mathf.Clamp(duration + 3f, 8f, 60f);
            lastFired = Time.time;
            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Started native physical liquid for marked actor.");
        }

        private bool Initialize(FieldInfo metaField)
        {
            GameObject body = character.objBodyBone;
            if (body == null || owner == null || owner.objMetaball == null)
                return false;

            Transform bone = FemboyRigResolver.FindBone(body.transform,
                FemboyModePlugin.MaleFinishBone.Value);
            if (bone == null)
                bone = FemboyRigResolver.FindBone(body.transform,
                    FemboyModePlugin.KokanAnchorBone.Value);
            if (bone == null)
                return false;

            UpdateMeta[] hosts =
                owner.objMetaball.GetComponentsInChildren<UpdateMeta>(true);
            if (hosts == null || hosts.Length == 0)
                return false;

            MetaballShoot native = null;
            MetaballCtrl meta = metaField == null ? null :
                metaField.GetValue(owner) as MetaballCtrl;
            if (meta != null && meta.ctrlMetaball != null)
            {
                // Vanilla uses index 1 for ordinary outside fluid and
                // index 4 for ANAL outside fluid (MetaballCtrl.Proc).
                // Previously forcing [1] broke the user's anal pose n14.
                int targetIndex = owner.flags != null && owner.flags.isAnalPlay
                    ? 4 : 1;
                if (meta.ctrlMetaball.Length > targetIndex)
                    native = meta.ctrlMetaball[targetIndex];

                if ((native == null || native.ShootObj == null) &&
                    meta.ctrlMetaball.Length > 1)
                    native = meta.ctrlMetaball[1];
            }

            // Prefer the host already tracking the native outside-shoot
            // object. Its StaticMetaballSeed will process our added shoot.
            for (int i = 0; i < hosts.Length; i++)
            {
                UpdateMeta candidate = hosts[i];
                if (candidate == null || candidate.lstShoot == null)
                    continue;
                if (native != null && candidate.lstShoot.Contains(native) &&
                    candidate.metaseed != null &&
                    candidate.metaseed.sourceRoot != null)
                {
                    host = candidate;
                    break;
                }
            }
            if (host == null)
            {
                for (int i = 0; i < hosts.Length; i++)
                {
                    if (hosts[i] != null && hosts[i].lstShoot != null &&
                        hosts[i].metaseed != null &&
                        hosts[i].metaseed.sourceRoot != null)
                    {
                        host = hosts[i];
                        break;
                    }
                }
            }
            if (host == null)
                return false;

            shooter = CopyNativeShoot(native);
            if (shooter == null)
                shooter = LoadVanillaPreset();

            if (shooter == null || shooter.ShootObj == null ||
                shooter.aInterval == null || shooter.aInterval.Length == 0)
            {
                shooter = null;
                return false;
            }
            nativeIntervals = (float[])shooter.aInterval.Clone();

            axis = new GameObject("FemboyMode_PhysicalFluidAxis");
            axis.transform.SetParent(bone, false);
            axis.transform.localPosition = new Vector3(
                FemboyModePlugin.ClimaxFXOffsetX.Value,
                FemboyModePlugin.ClimaxFXOffsetY.Value,
                FemboyModePlugin.ClimaxFXOffsetZ.Value);
            axis.transform.localRotation = Quaternion.Euler(
                FemboyModePlugin.ClimaxFXRotationX.Value,
                FemboyModePlugin.ClimaxFXRotationY.Value,
                FemboyModePlugin.ClimaxFXRotationZ.Value);
            shooter.ShootAxis = axis;
            shooter.chaCustom = character;
            shooter.isEnable = true;
            // Do not change the shared UpdateMeta static-mesh parenting
            // decision. Only our liquid should be driven by this emitter.
            shooter.parentTransform = null;

            // MetaballShoot.MetaBallClear destroys *all children* of its
            // objSourceRoot. Sharing the male player's root would erase
            // the other participant's real fluid on cleanup or pose change.
            privateSourceRoot = new GameObject("FemboyMode_PhysicalFluidSource");
            // StaticMetaballSeed recursively enumerates MetaballNode objects
            // BELOW its sourceRoot. Parent our isolated object there, or the
            // physical blobs would not contribute to the game's mesh.
            Transform seedRoot = host.metaseed.sourceRoot.transform;
            privateSourceRoot.transform.SetParent(seedRoot, false);
            shooter.objSourceRoot = privateSourceRoot;

            host.lstShoot.Add(shooter);
            return true;
        }

        private static MetaballShoot CopyNativeShoot(MetaballShoot native)
        {
            if (native == null || native.ShootObj == null ||
                native.aInterval == null || native.aInterval.Length == 0)
                return null;

            return new MetaballShoot
            {
                comment = "FemboyMode outside physical fluid",
                isEnable = true,
                ShootObj = native.ShootObj,
                objCondom = native.objCondom,
                objSourceRoot = native.objSourceRoot,
                drag = native.drag,
                dragDropDown = native.dragDropDown,
                maxMetaball = native.maxMetaball,
                timeDropDown = native.timeDropDown,
                speedSMin = native.speedSMin,
                speedSMax = native.speedSMax,
                speedMMin = native.speedMMin,
                speedMMax = native.speedMMax,
                speedLMin = native.speedLMin,
                speedLMax = native.speedLMax,
                aInterval = (float[])native.aInterval.Clone(),
                randomRotationS = native.randomRotationS,
                randomRotationM = native.randomRotationM,
                randomRotationL = native.randomRotationL,
                shootForce = native.shootForce
            };
        }

        private MetaballShoot LoadVanillaPreset()
        {
            // Reuse the CURRENT Sonyu pose's Metaball preset first. This
            // matters for anal positions like khs_m_n14. Aibu has no male
            // animation preset, so fall back to a standard loaded one.
            string current = owner.flags != null &&
                owner.flags.nowAnimationInfo != null &&
                owner.flags.nowAnimationInfo.paramMale != null
                ? owner.flags.nowAnimationInfo.paramMale.fileMetaballCtrl
                : null;
            int wantedId = owner.flags != null && owner.flags.isAnalPlay
                ? 4 : 1;
            MetaballShoot result = string.IsNullOrEmpty(current)
                ? null : LoadPresetNamed(current, wantedId);
            if (result == null && wantedId != 1 &&
                !string.IsNullOrEmpty(current))
                result = LoadPresetNamed(current, 1);
            if (result == null)
                result = LoadPresetNamed("siru_khs_00", wantedId);
            return result ?? LoadPresetNamed("siru_khs_00", 1);
        }

        private MetaballShoot LoadPresetNamed(string name, int wantedId)
        {
            List<MetaballParam> presets = GlobalMethod.LoadAllFolder<MetaballParam>(
                "h/list/", name);
            if (presets == null)
                return null;

            foreach (MetaballParam preset in presets)
            {
                if (preset == null || preset.param == null)
                    continue;
                foreach (MetaballParam.Param p in preset.param)
                {
                    if (p == null || p.ID != wantedId || !p.Enable ||
                        string.IsNullOrEmpty(p.BulletAssetPath) ||
                        string.IsNullOrEmpty(p.ObjBullet) ||
                        p.Interval == null || p.Interval.Length == 0)
                        continue;

                    GameObject projectile = CommonLib.LoadAsset<GameObject>(
                        p.BulletAssetPath, p.ObjBullet, false, p.BulletManifesto);
                    if (projectile == null)
                        continue;

                    if (owner.flags != null && owner.flags.hashAssetBundle != null)
                        owner.flags.hashAssetBundle.Add(p.BulletAssetPath);
                    return new MetaballShoot
                    {
                        comment = "FemboyMode native " + name + " outside fluid",
                        isEnable = true,
                        ShootObj = projectile,
                        aInterval = (float[])p.Interval.Clone(),
                        speedSMin = p.S_Speed_Min, speedSMax = p.S_Speed_Max,
                        speedMMin = p.M_Speed_Min, speedMMax = p.M_Speed_Max,
                        speedLMin = p.L_Speed_Min, speedLMax = p.L_Speed_Max,
                        randomRotationS = new Vector2(p.S_RandomDirX, p.S_RandomDirY),
                        randomRotationM = new Vector2(p.M_RandomDirX, p.M_RandomDirY),
                        randomRotationL = new Vector2(p.L_RandomDirX, p.L_RandomDirY)
                    };
                }
            }
            return null;
        }

        private void ApplyFluidAmount()
        {
            if (shooter == null || nativeIntervals == null)
                return;

            float amount = FemboyModePlugin.PhysicalFluidAmount == null
                ? 1f : FemboyModePlugin.PhysicalFluidAmount.Value;
            int intervals = FemboyRules.FluidIntervalCount(
                nativeIntervals.Length, amount);
            float[] resized = new float[intervals];
            for (int i = 0; i < resized.Length; i++)
            {
                float native = nativeIntervals[Mathf.Min(
                    i, nativeIntervals.Length - 1)];
                resized[i] = float.IsNaN(native) || float.IsInfinity(native)
                    ? 0.08f : Mathf.Max(0.01f, native);
            }

            shooter.aInterval = resized;
            shooter.maxMetaball = Mathf.Max(shooter.maxMetaball,
                Mathf.Min(80, intervals + 1));
            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Physical fluid amount=" + amount +
                    " effective droplets=" + (intervals + 1) +
                    " currentPoseAnal=" + (owner != null &&
                        owner.flags != null && owner.flags.isAnalPlay));
        }

        private void Update()
        {
            if (shooter == null) return;

            if (owner == null || owner.flags == null ||
                !owner.flags.isFreeH || character == null ||
                !FemboyModePlugin.IsFemboy(character) ||
                FemboyModePlugin.EnablePhysicalFluid == null ||
                !FemboyModePlugin.EnablePhysicalFluid.Value)
            {
                Cleanup();
                return;
            }

            if (Time.time <= activeUntil)
                shooter.ShootMetaBall();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (host != null && shooter != null && host.lstShoot != null)
                host.lstShoot.Remove(shooter);
            if (shooter != null)
                shooter.MetaBallClear();
            if (axis != null)
                UnityEngine.Object.Destroy(axis);
            if (privateSourceRoot != null)
                UnityEngine.Object.Destroy(privateSourceRoot);
            shooter = null;
            nativeIntervals = null;
            host = null;
            axis = null;
            privateSourceRoot = null;
        }
    }
}
