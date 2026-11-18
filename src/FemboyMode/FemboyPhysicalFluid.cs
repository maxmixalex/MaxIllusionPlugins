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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddAibuOrg))]
        private static void OnAibuClimax(HFlag __instance)
        {
            Trigger(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuOrg))]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuAnalOrg))]
        private static void OnSonyuClimax(HFlag __instance)
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

            // This drives the original MetaballShoot physics and seed mesh,
            // rather than playing a copied particle prefab.
            shooter.ShootMetaBallStart(false);
            activeUntil = Time.time + 8f;
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
            if (meta != null && meta.ctrlMetaball != null &&
                meta.ctrlMetaball.Length > 1)
                native = meta.ctrlMetaball[1];

            // Prefer the host already tracking the native outside-shoot
            // object. Its StaticMetaballSeed will process our added shoot.
            for (int i = 0; i < hosts.Length; i++)
            {
                UpdateMeta candidate = hosts[i];
                if (candidate == null || candidate.lstShoot == null)
                    continue;
                if (native != null && candidate.lstShoot.Contains(native))
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
                        hosts[i].metaseed != null)
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
            shooter.parentTransform = body.transform;

            if (shooter.objSourceRoot == null)
            {
                foreach (MetaballShoot candidate in host.lstShoot)
                {
                    if (candidate != null && candidate.objSourceRoot != null)
                    {
                        shooter.objSourceRoot = candidate.objSourceRoot;
                        break;
                    }
                }
            }
            if (shooter.objSourceRoot == null)
            {
                privateSourceRoot = new GameObject("FemboyMode_PhysicalFluidSource");
                privateSourceRoot.transform.SetParent(host.transform, false);
                shooter.objSourceRoot = privateSourceRoot;
            }

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
            // Aibu scenes have no active male motion and may not have a
            // configured MetaballShoot. Resolve the native normal-position
            // Sonyu outside-fluid preset from the game's own h/list.
            List<MetaballParam> presets = GlobalMethod.LoadAllFolder<MetaballParam>(
                "h/list/", "siru_khs_00");
            if (presets == null)
                return null;

            foreach (MetaballParam preset in presets)
            {
                if (preset == null || preset.param == null)
                    continue;
                foreach (MetaballParam.Param p in preset.param)
                {
                    if (p == null || p.ID != 1 || !p.Enable ||
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
                        comment = "FemboyMode native siru_khs_00 outside fluid",
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

        private void Update()
        {
            if (shooter == null || Time.time > activeUntil)
                return;
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
            host = null;
            axis = null;
            privateSourceRoot = null;
        }
    }
}
