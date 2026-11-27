using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveJuice {
    // Each verified Studio Liquid prefab is a LOOPING ParticleSystem, not a
    // single drop. Instead of cloning the whole emitter for every droplet,
    // keep at most one native emitter per channel, stop its automatic emission
    // and explicitly emit a bounded number of native particles.
    internal sealed class StudioLiquidPool {
        private sealed class Emitter {
            internal GameObject Source;
            internal GameObject Node;
            internal ParticleSystem[] Systems;
            internal readonly List<Material> OwnedMaterials = new List<Material>();
            internal bool Available;
        }

        private readonly Dictionary<LoveJuiceChannel, Emitter> emitters =
            new Dictionary<LoveJuiceChannel, Emitter>();
        private readonly HashSet<LoveJuiceChannel> unavailable = new HashSet<LoveJuiceChannel>();
        private readonly Action<string> warn;
        private readonly NativeParticleBudget budget = new NativeParticleBudget();
        private GameObject host;
        private int lastParticleCount;

        internal int ActiveCount { get { return CountParticles(); } }
        internal int EmitterCount { get { return emitters.Count; } }

        internal StudioLiquidPool(Action<string> warn) { this.warn = warn; }

        internal void Attach(Transform scene) {
            Dispose();
            if (scene == null) return;
            host = new GameObject("LoveJuice.NativeStudioEmitters");
            host.transform.SetParent(scene, false);
        }

        private static void TintMilk(Emitter emitter) {
            Color milk = new Color(.96f, .95f, .91f, 1f);
            foreach (Renderer renderer in emitter.Node.GetComponentsInChildren<Renderer>(true)) {
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;
                Material[] copy = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++) {
                    Material shared = materials[i];
                    if (shared == null) continue;
                    Material material = new Material(shared);
                    // Keep original alpha and all non-color shader properties.
                    if (material.HasProperty("_Color")) {
                        Color current = material.GetColor("_Color");
                        material.SetColor("_Color", new Color(milk.r, milk.g, milk.b, current.a));
                    }
                    // Unity 5.6 particle shaders often use _TintColor.
                    if (material.HasProperty("_TintColor")) {
                        Color current = material.GetColor("_TintColor");
                        material.SetColor("_TintColor", new Color(milk.r, milk.g, milk.b, current.a));
                    }
                    emitter.OwnedMaterials.Add(material);
                    copy[i] = material;
                }
                renderer.sharedMaterials = copy;
            }
        }

        private Emitter GetEmitter(LoveJuiceChannel kind) {
            Emitter cached;
            if (host == null || unavailable.Contains(kind)) return null;
            if (emitters.TryGetValue(kind, out cached) && cached.Node != null) return cached;
            string name = StudioLiquidCatalog.Prefab(kind);
            if (name == null) return null;

            GameObject node = null;
            Emitter pending = null;
            try {
                // LoadAsset returns a BORROWED asset-bundle prefab, not an
                // instance owned by this plugin. Do not SetActive, rename,
                // reparent or Destroy it: Studio/other mods may share it.
                GameObject template = CommonLib.LoadAsset<GameObject>(
                    StudioLiquidCatalog.Bundle, name, true, StudioLiquidCatalog.Manifest);
                if (template == null)
                    template = CommonLib.LoadAsset<GameObject>(
                        StudioLiquidCatalog.Bundle, name, true, string.Empty);
                if (template == null)
                    throw new InvalidOperationException("Cannot load Studio prefab: " + name);

                node = UnityEngine.Object.Instantiate(template);
                if (node == null)
                    throw new InvalidOperationException("Cannot instantiate Studio prefab: " + name);
                node.SetActive(false);
                node.name = "LoveJuice.NativeEmitter." + kind;
                node.transform.SetParent(host.transform, false);
                pending = new Emitter {
                    Source = template, Node = node,
                    Systems = node.GetComponentsInChildren<ParticleSystem>(true),
                    Available = true
                };
                if (pending.Systems == null || pending.Systems.Length == 0)
                    throw new InvalidOperationException("Studio prefab has no native ParticleSystem: " + name);

                foreach (ParticleSystem system in pending.Systems) {
                    if (system == null) continue;
                    // Disable native looping emissions, keeping explicit Emit
                    // supported by Unity's EmissionModule.
                    var main = system.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var emission = system.emission;
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
                    emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);
                    emission.SetBursts(new ParticleSystem.Burst[0]);
                    emission.enabled = true;
                    system.Stop();
                    system.Clear();
                }
                if (kind == LoveJuiceChannel.Milk) TintMilk(pending);
                node.SetActive(true);
                emitters[kind] = pending;
                return pending;
            } catch (Exception ex) {
                unavailable.Add(kind);
                // A partially initialized clone and its material copies are
                // ours to destroy. The borrowed template is never destroyed.
                if (pending != null)
                    foreach (Material material in pending.OwnedMaterials)
                        if (material != null) UnityEngine.Object.Destroy(material);
                if (node != null) UnityEngine.Object.Destroy(node);
                if (warn != null)
                    warn("Native Studio emitter unavailable (" + name + "): " + ex.Message);
                return null;
            }
        }

        private int CountParticles() {
            int count = 0;
            foreach (Emitter emitter in emitters.Values)
                if (emitter.Systems != null)
                    foreach (ParticleSystem particle in emitter.Systems)
                        if (particle != null) count += particle.particleCount;
            lastParticleCount = count;
            return count;
        }

        internal bool Spawn(LoveJuiceChannel kind, Vector3 position, Vector3 velocity,
            float size, float duration, int limit, float gravity, float damping, bool collision) {
            if (host == null || ActiveCount >= limit) return false;
            Emitter emitter = GetEmitter(kind);
            if (emitter == null || !emitter.Available) return false;
            Transform transform = emitter.Node.transform;
            transform.position = position;
            if (velocity.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
            transform.localScale = emitter.Source.transform.localScale * size;
            bool sent = false;
            foreach (ParticleSystem system in emitter.Systems) {
                if (system == null) continue;
                // Count reservations made this frame too; particleCount may
                // not update immediately after Emit() on Unity 5.6.
                if (!budget.TryReserve(Time.frameCount, ActiveCount, limit)) break;
                var main = system.main;
                main.maxParticles = Math.Max(1, Math.Min(128, limit));
                main.gravityModifier = new ParticleSystem.MinMaxCurve(
                    DropletPhysics.Clamp(gravity, 0f, 30f) / 9.81f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(
                    DropletPhysics.Clamp(duration, .2f, 8f));
                var collisions = system.collision;
                collisions.enabled = collision;
                if (collision) collisions.type = ParticleSystemCollisionType.World;
                var limitVelocity = system.limitVelocityOverLifetime;
                if (damping > 0f) {
                    limitVelocity.enabled = true;
                    limitVelocity.dampen = DropletPhysics.Clamp(damping / 10f, 0f, 1f);
                } else limitVelocity.enabled = false;
                if (!system.isPlaying) system.Play();
                // Preserve authored native particle renderer, shape and size.
                // Emit exactly one native particle, not an entire looping jet.
                ParticleSystem.EmitParams sample = new ParticleSystem.EmitParams();
                // Preserve the Studio prefab's native shape/emitter origin.
                // Explicit position=zero would place particles at world origin
                // when simulationSpace is World.
                sample.velocity = velocity;
                sample.startLifetime = DropletPhysics.Clamp(duration, .2f, 8f);
                if (kind == LoveJuiceChannel.Milk)
                    sample.startColor = new Color32(245, 243, 235, 255);
                system.Emit(sample, 1);
                sent = true;
            }
            return sent;
        }

        internal void Tick(float time, float gravity, float damping, bool collision) {
            // Unity's native particle simulation owns world-space motion and
            // hit testing. This lightweight Tick performs no per-frame clones.
            // Update the observed count for diagnostics and the global budget.
            lastParticleCount = CountParticles();
        }

        internal void Dispose() {
            foreach (Emitter emitter in emitters.Values) {
                foreach (Material material in emitter.OwnedMaterials)
                    if (material != null) UnityEngine.Object.Destroy(material);
            }
            emitters.Clear();
            unavailable.Clear();
            budget.Reset();
            if (host != null) UnityEngine.Object.Destroy(host);
            host = null;
            lastParticleCount = 0;
        }
    }
}
