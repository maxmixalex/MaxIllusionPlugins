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

            // Do not fire on Sonyu SetPlay. Multi-callback / simultaneous
            // finish actions can enter SetPlay before the native voice Start;
            // firing there marks the NEXT actual climax as already emitted.
            // Sonyu emission is synchronized to consumed native VoiceProc.
        }

        private struct SonyuVoiceState
        {
            internal HFlag Flags;
            internal ChaControl Actor;
            internal int Channel;
            internal int Code;
            internal bool IsClimax;
            internal bool IsCombined;
        }

        // The RX18 log proves native Sonyu voice code 326 was processed
        // while the physical fluid emitter was still bound to Aibu.
        // The previous HFlag/SetPlay callbacks were not sufficient in
        // this particular installed H-mod combination.
        //
        // Capture the pending native voice code BEFORE VoiceProc consumes
        // it, but trigger AFTER consumption. VoiceProc can wait and return
        // without consuming a code; a naive prefix would emit on EVERY
        // waiting frame. The Postfix only emits once the original method
        // changes the pending code (normally back to -1).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
        private static void CaptureSonyuClimaxVoice(
            HVoiceCtrl __instance, ChaControl _female, int _main,
            out SonyuVoiceState __state)
        {
            __state = new SonyuVoiceState();

            if (__instance == null || _female == null ||
                _main < 0 || _main > 1)
                return;

            HFlag flags = __instance.flags;
            if (flags == null || !flags.isFreeH ||
                flags.mode != HFlag.EMode.sonyu ||
                flags.voice == null || flags.voice.playVoices == null ||
                flags.voice.playVoices.Length <= _main)
                return;

            int code = flags.voice.playVoices[_main];
            bool combined = FemboyRules.IsSonyuCombinedClimaxVoice(code) &&
                (flags.finish == HFlag.FinishKind.sameW ||
                 flags.finish == HFlag.FinishKind.sameS);
            if (!combined && !FemboyRules.IsSonyuClimaxStartVoice(code))
                return;

            var leading = flags.GetLeadingHeroine();
            if (leading == null || !ReferenceEquals(leading.chaCtrl, _female) ||
                !FemboyModePlugin.IsFemboy(_female))
                return;

            __state.Flags = flags;
            __state.Actor = _female;
            __state.Channel = _main;
            __state.Code = code;
            __state.IsClimax = true;
            __state.IsCombined = combined;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HVoiceCtrl), "VoiceProc")]
        private static void AfterSonyuClimaxVoice(
            SonyuVoiceState __state)
        {
            if (!__state.IsClimax || __state.Flags == null ||
                __state.Actor == null || __state.Flags.voice == null ||
                __state.Flags.voice.playVoices == null ||
                __state.Flags.voice.playVoices.Length <= __state.Channel)
                return;

            // VoiceLinkProc / voice-overwrite deferrals keep the original
            // pending code unchanged. Fire only after the native event
            // has truly been accepted or exhausted.
            int after = __state.Flags.voice.playVoices[__state.Channel];
            bool consumed = __state.IsCombined
                ? FemboyRules.ShouldEmitFromConsumedCombinedVoice(__state.Code, after)
                : FemboyRules.ShouldEmitFromConsumedSonyuVoice(__state.Code, after);
            if (!consumed || !FemboyModePlugin.IsFemboy(__state.Actor))
                return;

            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Sonyu native climax event consumed: code=" +
                    __state.Code + " channel=" + __state.Channel +
                    " state=" + __state.Flags.nowAnimStateName);

            // Never let an optional fluid emitter break the game's
            // voice or climax state machine.
            try
            {
                Trigger(__state.Flags, true);
            }
            catch (Exception ex)
            {
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Sonyu climax fluid fallback failed safely: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuOrg))]
        private static void OnSonyuClimax(HFlag __instance)
        {
            NoteNativeOrgasm(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.AddSonyuAnalOrg))]
        private static void OnSonyuAnalClimax(HFlag __instance)
        {
            NoteNativeOrgasm(__instance);
        }

        private static void NoteNativeOrgasm(HFlag flags)
        {
            if (flags == null || !flags.isFreeH ||
                flags.mode != HFlag.EMode.sonyu)
                return;
            var heroine = flags.GetLeadingHeroine();
            if (heroine == null || heroine.chaCtrl == null ||
                !FemboyModePlugin.IsFemboy(heroine.chaCtrl))
                return;
            HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
            for (int i = 0; i < scenes.Length; i++)
            {
                HSceneProc scene = scenes[i];
                if (scene == null || !ReferenceEquals(scene.flags, flags)) continue;
                FemboyPhysicalFluidEmitter emitter = heroine.chaCtrl.gameObject
                    .GetComponent<FemboyPhysicalFluidEmitter>();
                if (emitter == null)
                    emitter = heroine.chaCtrl.gameObject.AddComponent<FemboyPhysicalFluidEmitter>();
                emitter.NoteSonyuOrgasm(scene);
                return;
            }
        }

        private static void Trigger(HFlag flags, bool consumedSonyuVoice = false)
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
                emitter.Fire(scene, character, MetaField, consumedSonyuVoice);
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

    /// <summary>
    /// A native MetaballShoot applies its Rigidbody force in
    /// ShootAxis.transform.forward. Point the axis along the model's actual
    /// shaft direction projected into the horizontal plane, then tilt it
    /// upward by a configurable elevation angle. ForceMode.VelocityChange
    /// produces initial velocity; gravity supplies the visible parabola.
    /// </summary>
    public sealed class FemboyFluidAxisFollower : MonoBehaviour
    {
        private ChaControl actor;
        private Transform shaftRoot;
        private Transform shaftTip;
        public void Configure(ChaControl target, Transform root, Transform tip)
        {
            actor = target;
            shaftRoot = root;
            shaftTip = tip;
            AlignToModel();
        }

        private void LateUpdate()
        {
            AlignToModel();
        }

        public void AlignToModel()
        {
            if (shaftTip == null || actor == null ||
                FemboyModePlugin.FluidLocalPitch == null)
                return;

            // Follow the actual shaft in 3D, including the vertical
            // component. Never project onto the world's horizontal plane:
            // a reclining or upside-down character must launch along the
            // same direction relative to their own animated anatomy.
            Vector3 direction = shaftRoot != null
                ? shaftTip.position - shaftRoot.position
                : shaftTip.forward;
            if (direction.sqrMagnitude < 0.000001f)
                direction = shaftTip.forward;
            if (direction.sqrMagnitude < 0.000001f)
                return;
            direction.Normalize();

            // Use the root bone's rotated anatomical up to construct a
            // stable frame around the shaft. The pitch therefore follows
            // pelvic roll and character rotation; Unity's global up axis
            // is used ONLY as a last resort if model bones are degenerate.
            Vector3 anatomicalUp = shaftRoot != null
                ? shaftRoot.up : shaftTip.up;
            anatomicalUp = Vector3.ProjectOnPlane(anatomicalUp, direction);
            if (anatomicalUp.sqrMagnitude < 0.000001f)
                anatomicalUp = Vector3.ProjectOnPlane(
                    shaftRoot != null ? shaftRoot.right : shaftTip.right,
                    direction);
            if (anatomicalUp.sqrMagnitude < 0.000001f)
                anatomicalUp = Vector3.ProjectOnPlane(actor.transform.up, direction);
            if (anatomicalUp.sqrMagnitude < 0.000001f)
                anatomicalUp = Vector3.ProjectOnPlane(Vector3.up, direction);
            if (anatomicalUp.sqrMagnitude < 0.000001f)
                anatomicalUp = Vector3.ProjectOnPlane(Vector3.right, direction);
            if (anatomicalUp.sqrMagnitude < 0.000001f)
                return;

            Quaternion boneFrame = Quaternion.LookRotation(
                direction, anatomicalUp.normalized);
            float localPitch = FemboyRules.SafeLocalFluidPitch(
                FemboyModePlugin.FluidLocalPitch.Value);
            transform.rotation = boneFrame * Quaternion.Euler(
                -localPitch, 0f, 0f);

            // Existing model-specific manual correction is expressed in
            // this local shaft frame, never in world Euler angles.
            transform.rotation *= Quaternion.Euler(
                FemboyModePlugin.ClimaxFXRotationX.Value,
                FemboyModePlugin.ClimaxFXRotationY.Value,
                FemboyModePlugin.ClimaxFXRotationZ.Value);
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
        private bool waitingForFirstDroplet;
        private bool reportedAibu;
        private bool reportedSonyu;
        private readonly FemboySonyuCycleGate sonyuCycle =
            new FemboySonyuCycleGate();

        internal string DiagnosticStatus
        {
            get
            {
                return "mode=" + boundMode + " pose=" + boundPoseId +
                    " configured=" + (shooter != null) +
                    " host=" + (host != null) +
                    " seed=" + (host != null && host.metaseed != null) +
                    " latestSourceChildren=" +
                    (privateSourceRoot == null ? -1 :
                        privateSourceRoot.transform.childCount) +
                    " launchDirection=" +
                    (axis == null ? "(missing)" : axis.transform.forward.ToString()) +
                    " firedAt=" + lastFired +
                    " sonyuCycle=(" + sonyuCycle.DiagnosticState + ")";
            }
        }
        private HFlag.EMode boundMode = HFlag.EMode.none;
        private int boundPoseId = -1;
        private FemboyFluidAxisFollower trajectory;

        internal void NoteSonyuOrgasm(HSceneProc scene)
        {
            if (scene == null || scene.flags == null ||
                scene.flags.nowAnimationInfo == null) return;
            sonyuCycle.NoteNativeOrgasm(scene.flags.nowAnimationInfo.id,
                scene.flags.nowAnimStateName);
        }

        public void Fire(HSceneProc scene, ChaControl target, FieldInfo metaField,
            bool consumedSonyuVoice)
        {
            if (scene == null || target == null || target.objBodyBone == null ||
                scene.flags == null)
                return;

            HFlag.EMode mode = scene.flags.mode;
            int poseId = scene.flags.nowAnimationInfo == null
                ? -1 : scene.flags.nowAnimationInfo.id;
            string stateName = scene.flags.nowAnimStateName;
            if (mode == HFlag.EMode.sonyu)
            {
                // Recovery voices 327/329 can carry a climax-code number
                // while the native state is *_IN_A. Never discharge there.
                if (!consumedSonyuVoice ||
                    !FemboyRules.IsSonyuFluidEmissionPhase(stateName)) return;
                if (!sonyuCycle.CanFire(poseId, stateName,
                    Time.time - lastFired))
                {
                    if (FemboyModePlugin.VerboseLogging != null &&
                        FemboyModePlugin.VerboseLogging.Value)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Sonyu fluid deferred: state=" + stateName +
                            " " + sonyuCycle.DiagnosticState);
                    return;
                }
            }

            // A second legitimate climax uses a fresh native MetaballShoot.
            // Its interval index and projectile allowance can be exhausted
            // after the first shot even when ShootMetaBallStart is invoked.
            bool freshSonyuCycle = mode == HFlag.EMode.sonyu &&
                lastFired > -900f;
            if (freshSonyuCycle && Time.time - lastFired < 0.9f)
                return;

            // Original HSceneProc.ChangeAnimator swaps the native Metaball
            // preset and even its host when the scene changes from Aibu to
            // Sonyu or to another pose. Reusing the old Aibu shooter caused
            // the user's insertion scene to appear to emit nothing.
            bool changedPose = !ReferenceEquals(owner, scene) ||
                !ReferenceEquals(character, target) ||
                shooter == null || axis == null || host == null ||
                freshSonyuCycle ||
                FemboyRules.NeedsNewFluidPreset((int)boundMode, boundPoseId,
                    (int)mode, poseId);
            if (changedPose)
            {
                Cleanup();
                owner = scene;
                character = target;
                boundMode = mode;
                boundPoseId = poseId;
                if (!Initialize(metaField))
                {
                    if (!warnedUnavailable)
                    {
                        warnedUnavailable = true;
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "Native physical fluid unavailable for mode=" +
                            mode + " pose=" + poseId +
                            ": no compatible native projectile/seed/target bone.");
                    }
                    return;
                }
                warnedUnavailable = false;
            }

            if (Time.time - lastFired < 0.9f ||
                axis == null || shooter == null || shooter.aInterval == null)
                return;

            // Scale the *number of physical droplets* rather than visual
            // particle alpha. Never mutate the real male's source preset.
            ApplyFluidAmount();
            ApplyLaunchPhysics();
            if (trajectory != null)
                trajectory.AlignToModel();
            shooter.ShootMetaBallStart(false);
            if (mode == HFlag.EMode.sonyu)
                sonyuCycle.MarkFired(poseId, stateName);
            waitingForFirstDroplet = true;
            if ((mode == HFlag.EMode.aibu && !reportedAibu) ||
                (mode == HFlag.EMode.sonyu && !reportedSonyu))
            {
                if (mode == HFlag.EMode.aibu) reportedAibu = true;
                if (mode == HFlag.EMode.sonyu) reportedSonyu = true;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Physical fluid started mode=" + mode + " pose=" +
                    poseId + " anal=" + scene.flags.isAnalPlay +
                    " count=" + (shooter.aInterval.Length + 1) +
                    " direction=" + axis.transform.forward);
            }
            float duration = 0f;
            for (int i = 0; i < shooter.aInterval.Length; i++)
                duration += Mathf.Max(0f, shooter.aInterval[i]);
            activeUntil = Time.time + Mathf.Clamp(duration + 3f, 8f, 60f);
            lastFired = Time.time;
            if (FemboyModePlugin.VerboseLogging != null &&
                FemboyModePlugin.VerboseLogging.Value)
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Started native physical liquid in " + mode +
                    " pose=" + poseId + " anal=" +
                    scene.flags.isAnalPlay + " shotCount=" +
                    (shooter.aInterval.Length + 1) +
                    " launchDirection=" + axis.transform.forward);
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
            trajectory = axis.AddComponent<FemboyFluidAxisFollower>();
            Transform baseBone = FemboyRigResolver.FindBone(
                body.transform, FemboyModePlugin.MaleGeometryBone.Value);
            trajectory.Configure(character, baseBone, bone);
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

        private void ApplyLaunchPhysics()
        {
            if (shooter == null || FemboyModePlugin.FluidLaunchSpeed == null)
                return;

            float speed = Mathf.Clamp(FemboyModePlugin.FluidLaunchSpeed.Value,
                0.2f, 4f);
            float spread = Mathf.Clamp(FemboyModePlugin.FluidSpread.Value,
                0f, 30f);
            // Native MetaballShoot.SetRigidForce uses ShootAxis.forward,
            // then applies a relative force. Its original ForceMode.Force
            // is too weak with some modded Rigidbody masses. VelocityChange
            // supplies a true initial velocity independent of mass, after
            // which Unity gravity naturally draws a ballistic parabola.
            shooter.shootForce = ForceMode.VelocityChange;
            shooter.speedSMin = shooter.speedMMin = shooter.speedLMin =
                speed * 0.9f;
            shooter.speedSMax = shooter.speedMMax = shooter.speedLMax =
                speed * 1.1f;
            Vector2 dispersion = new Vector2(spread, spread);
            shooter.randomRotationS = dispersion;
            shooter.randomRotationM = dispersion;
            shooter.randomRotationL = dispersion;
        }

        private void Update()
        {
            // This is observed on every animation frame, not only when a
            // voice is delivered. Otherwise the next insertion loop can be
            // missed and the subsequent climax will stay suppressed.
            if (owner != null && owner.flags != null &&
                owner.flags.mode == HFlag.EMode.sonyu)
                sonyuCycle.Observe(owner.flags.nowAnimationInfo == null
                    ? -1 : owner.flags.nowAnimationInfo.id,
                    owner.flags.nowAnimStateName);
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
            {
                int before = privateSourceRoot == null ? -1 :
                    privateSourceRoot.transform.childCount;
                shooter.ShootMetaBall();
                int after = privateSourceRoot == null ? -1 :
                    privateSourceRoot.transform.childCount;

                if (waitingForFirstDroplet && after > before)
                {
                    waitingForFirstDroplet = false;
                    if (FemboyModePlugin.VerboseLogging != null &&
                        FemboyModePlugin.VerboseLogging.Value)
                        FemboyModePlugin.LogOptionalIntegrationProblem(
                            "First physical droplet spawned on " + boundMode +
                            ", source children=" + after);
                }
            }

            if (waitingForFirstDroplet && Time.time - lastFired > 1.5f)
            {
                waitingForFirstDroplet = false;
                FemboyModePlugin.LogOptionalIntegrationProblem(
                    "Physical fluid was armed, but native Metaball projectile " +
                    "objects did not appear under the source root. " +
                    "Check current-pose ShootObj and Rigidbody prefab.");
            }
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
            waitingForFirstDroplet = false;
            host = null;
            axis = null;
            trajectory = null;
            privateSourceRoot = null;
            boundMode = HFlag.EMode.none;
            boundPoseId = -1;
        }
    }
}
