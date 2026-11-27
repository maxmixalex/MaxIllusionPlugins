using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoveJuice
{
    /// <summary>
    /// True 3-D fluid through Koikatsu's own MetaballShoot + UpdateMeta seed.
    /// No billboard ribbons, sphere primitives, Studio shower emitters or
    /// borrowed particles. An isolated source root is created per actor
    /// and channel so MetaBallClear NEVER touches a native player source.
    /// </summary>
    internal sealed class NativeMetaballPool
    {
        private sealed class Emitter
        {
            internal ChaControl Character;
            internal LoveJuiceChannel Channel;
            internal GameObject Axis, Root;
            internal MetaballShoot Shoot;
            internal float ActivityEnds;
            internal float LastEmission;
        }

        private const BindingFlags Fields = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Action<string> log;
        private readonly List<Emitter> emitters = new List<Emitter>(32);
        private HSceneProc scene;
        private UpdateMeta host;
        private MetaballShoot template;
        private float nextProbe;
        private bool warnedMissing;
        private int maxActive = 48;

        internal NativeMetaballPool(Action<string> logger) { log = logger; }
        internal int EmitterCount { get { return emitters.Count; } }

        internal int ActiveCount {
            get {
                int count = 0;
                foreach (Emitter e in emitters)
                    if (e.Root != null) count += e.Root.transform.childCount;
                return count;
            }
        }

        internal void Attach(HSceneProc next) {
            Dispose();
            scene = next;
        }

        private bool Resolve() {
            if (scene == null || scene.flags == null ||
                scene.objMetaball == null) return false;
            if (host != null && host.metaseed != null &&
                host.metaseed.sourceRoot != null &&
                template != null) return true;
            if (Time.unscaledTime < nextProbe) return false;
            nextProbe = Time.unscaledTime + 1.5f;
            MetaballShoot candidate = null;
            try {
                FieldInfo metaField = typeof(HSceneProc).GetField("meta", Fields);
                MetaballCtrl ctrl = metaField == null ? null :
                    metaField.GetValue(scene) as MetaballCtrl;
                if (ctrl != null && ctrl.ctrlMetaball != null)
                    foreach (MetaballShoot shot in ctrl.ctrlMetaball)
                        if (shot != null && shot.ShootObj != null &&
                            shot.aInterval != null && shot.aInterval.Length > 0) {
                            candidate = shot;
                            break;
                        }
            } catch { }
            UpdateMeta[] hosts =
                scene.objMetaball.GetComponentsInChildren<UpdateMeta>(true);
            if (hosts != null)
                foreach (UpdateMeta h in hosts)
                    if (h != null && h.lstShoot != null &&
                        h.metaseed != null &&
                        h.metaseed.sourceRoot != null &&
                        (candidate == null || h.lstShoot.Contains(candidate))) {
                        host = h;
                        break;
                    }
            if (host == null && hosts != null)
                foreach (UpdateMeta h in hosts)
                    if (h != null && h.lstShoot != null &&
                        h.metaseed != null &&
                        h.metaseed.sourceRoot != null) {
                        host = h;
                        break;
                    }

            if (candidate == null) candidate = LoadNativePreset();
            if (host == null || candidate == null) {
                if (!warnedMissing && log != null) {
                    warnedMissing = true;
                    log("LoveJuice: true 3D native MetaballShoot/UpdateMeta " +
                        "is not ready in this H scene. No fake fallback will play.");
                }
                return false;
            }
            template = candidate;
            warnedMissing = false;
            return true;
        }

        private static MetaballShoot LoadNativePreset()
        {
            try {
                List<MetaballParam> presets =
                    GlobalMethod.LoadAllFolder<MetaballParam>(
                        "h/list/", "siru_khs_00");
                if (presets == null) return null;
                foreach (MetaballParam asset in presets)
                    if (asset != null && asset.param != null)
                        foreach (MetaballParam.Param p in asset.param)
                        {
                            if (p == null || p.ID != 1 || !p.Enable ||
                                string.IsNullOrEmpty(p.BulletAssetPath) ||
                                string.IsNullOrEmpty(p.ObjBullet) ||
                                p.Interval == null || p.Interval.Length == 0)
                                continue;
                            GameObject bullet = CommonLib.LoadAsset<GameObject>(
                                p.BulletAssetPath, p.ObjBullet, false,
                                p.BulletManifesto);
                            if (bullet == null) continue;
                            return new MetaballShoot {
                                ShootObj = bullet,
                                isEnable = true,
                                aInterval = (float[])p.Interval.Clone(),
                                speedSMin = p.S_Speed_Min,
                                speedSMax = p.S_Speed_Max,
                                speedMMin = p.M_Speed_Min,
                                speedMMax = p.M_Speed_Max,
                                speedLMin = p.L_Speed_Min,
                                speedLMax = p.L_Speed_Max,
                                randomRotationS = new Vector2(
                                    p.S_RandomDirX, p.S_RandomDirY),
                                randomRotationM = new Vector2(
                                    p.M_RandomDirX, p.M_RandomDirY),
                                randomRotationL = new Vector2(
                                    p.L_RandomDirX, p.L_RandomDirY)
                            };
                        }
            } catch { }
            return null;
        }

        private static MetaballShoot Copy(MetaballShoot source) {
            if (source == null || source.ShootObj == null) return null;
            return new MetaballShoot {
                comment = "LoveJuice - native individual viscous drops",
                isEnable = true,
                ShootObj = source.ShootObj,
                objCondom = source.objCondom,
                drag = source.drag,
                dragDropDown = source.dragDropDown,
                maxMetaball = source.maxMetaball,
                timeDropDown = source.timeDropDown,
                speedSMin = source.speedSMin,
                speedSMax = source.speedSMax,
                speedMMin = source.speedMMin,
                speedMMax = source.speedMMax,
                speedLMin = source.speedLMin,
                speedLMax = source.speedLMax,
                aInterval = source.aInterval == null ?
                    new float[] { .065f } :
                    (float[])source.aInterval.Clone(),
                randomRotationS = source.randomRotationS,
                randomRotationM = source.randomRotationM,
                randomRotationL = source.randomRotationL,
                shootForce = source.shootForce
            };
        }

        private Emitter Create(ChaControl actor, LoveJuiceChannel kind)
        {
            if (emitters.Count >= 32 || !Resolve() ||
                actor == null || host == null || template == null) return null;
            MetaballShoot shoot = Copy(template);
            if (shoot == null) return null;
            var emitter = new Emitter { Character = actor,
                Channel = kind, Shoot = shoot };
            try {
                emitter.Axis = new GameObject("LoveJuice.NativeAxis." + kind);
                emitter.Axis.transform.SetParent(scene.transform, false);
                emitter.Root = new GameObject("LoveJuice.NativeSource." + kind);
                // Do NOT share the game's main source root. Native
                // MetaBallClear recursively destroys only our descendants.
                emitter.Root.transform.SetParent(
                    host.metaseed.sourceRoot.transform, false);
                shoot.chaCustom = actor;
                shoot.ShootAxis = emitter.Axis;
                shoot.objSourceRoot = emitter.Root;
                shoot.parentTransform = null;
                shoot.isEnable = true;
                host.lstShoot.Add(shoot);
                emitters.Add(emitter);
                return emitter;
            } catch (Exception ex) {
                if (host != null && host.lstShoot != null)
                    host.lstShoot.Remove(shoot);
                if (emitter.Axis != null) UnityEngine.Object.Destroy(emitter.Axis);
                if (emitter.Root != null) UnityEngine.Object.Destroy(emitter.Root);
                if (log != null) log("LoveJuice native source: " + ex.Message);
                return null;
            }
        }

        internal bool Spawn(ChaControl actor, LoveJuiceChannel kind,
            Vector3 origin, Vector3 velocity, int limit, float speed)
        {
            if (actor == null || kind == LoveJuiceChannel.Saliva ||
                scene == null || scene.flags == null) return false;
            maxActive = Mathf.Clamp(limit, 1, 128);
            if (ActiveCount + 2 > maxActive) return false;
            if (!Resolve()) return false;

            Emitter e = null;
            foreach (Emitter item in emitters)
                if (ReferenceEquals(item.Character, actor) &&
                    item.Channel == kind) { e = item; break; }
            if (e == null) e = Create(actor, kind);
            if (e == null || e.Axis == null || e.Shoot == null) return false;
            float now = Time.time;
            // Each scene source can finish its current 2-drop native
            // sequence before the next trigger.
            if (now < e.ActivityEnds || now - e.LastEmission < .16f)
                return false;
            Vector3 direction = velocity.sqrMagnitude > .00001f ?
                velocity.normalized : Vector3.down;
            e.Axis.transform.position = origin;
            e.Axis.transform.rotation = Quaternion.LookRotation(
                direction, Vector3.forward);
            float launch = Mathf.Clamp(
                Mathf.Max(.07f, velocity.magnitude + speed * .15f),
                .07f, 1.4f);
            MetaballShoot shot = e.Shoot;
            shot.shootForce = ForceMode.VelocityChange;
            shot.speedSMin = shot.speedMMin = shot.speedLMin = launch * .9f;
            shot.speedSMax = shot.speedMMax = shot.speedLMax = launch * 1.1f;
            shot.randomRotationS = shot.randomRotationM =
                shot.randomRotationL = new Vector2(4f, 4f);
            shot.aInterval = new[] { .06f };
            shot.maxMetaball = Mathf.Max(shot.maxMetaball, 4);
            try {
                shot.ShootMetaBallStart(false);
                e.ActivityEnds = now + .45f;
                e.LastEmission = now;
                return true;
            } catch (Exception ex) {
                if (log != null) log("LoveJuice native metaball start: " + ex.Message);
                return false;
            }
        }

        internal void Tick(float delta) {
            if (scene == null || host == null) return;
            float now = Time.time;
            foreach (Emitter emitter in emitters)
            {
                if (emitter.Root == null || emitter.Shoot == null) continue;
                if (now > emitter.ActivityEnds) continue;
                if (ActiveCount >= maxActive) break;
                try { emitter.Shoot.ShootMetaBall(); }
                catch (Exception ex) {
                    emitter.ActivityEnds = 0f;
                    if (log != null)
                        log("LoveJuice native metaball update: " + ex.Message);
                }
            }
        }

        internal void Dispose() {
            foreach (Emitter emitter in emitters)
            {
                try {
                    if (host != null && host.lstShoot != null &&
                        emitter.Shoot != null)
                        host.lstShoot.Remove(emitter.Shoot);
                    if (emitter.Shoot != null)
                        emitter.Shoot.MetaBallClear();
                } catch { }
                if (emitter.Axis != null)
                    UnityEngine.Object.Destroy(emitter.Axis);
                if (emitter.Root != null)
                    UnityEngine.Object.Destroy(emitter.Root);
            }
            emitters.Clear();
            host = null;
            template = null;
            scene = null;
            warnedMissing = false;
            nextProbe = 0f;
        }
    }
}
