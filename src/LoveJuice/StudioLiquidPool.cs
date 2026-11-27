using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveJuice
{
    /// <summary>
    /// Viscoelastic, discrete drops rather than an entire Studio spray or
    /// shower prefab. The renderer/material comes from an installed Studio
    /// Liquid item. We borrow the source asset, clone only its material and
    /// create bounded, pooled droplets and short trailing threads.
    /// </summary>
    internal sealed class StudioLiquidPool
    {
        private sealed class Drop
        {
            internal GameObject Object;
            internal TrailRenderer Thread;
            internal Vector3 Velocity;
            internal float Age, Lifetime, Gravity, Damping;
            internal bool Collision, Active;
        }

        private readonly Action<string> warn;
        private readonly NativeParticleBudget budget = new NativeParticleBudget();
        private readonly Dictionary<LoveJuiceChannel, Material> materials =
            new Dictionary<LoveJuiceChannel, Material>();
        private readonly HashSet<LoveJuiceChannel> unavailable =
            new HashSet<LoveJuiceChannel>();
        private readonly List<Drop> drops = new List<Drop>();
        private GameObject host;
        private int activeCount;
        private int reservedFrame = -1;

        internal int ActiveCount { get { return activeCount; } }
        internal int EmitterCount { get { return materials.Count; } }

        internal StudioLiquidPool(Action<string> warn)
        {
            this.warn = warn;
        }

        internal void Attach(Transform scene)
        {
            Dispose();
            if (scene == null) return;
            host = new GameObject("LoveJuice.StudioMaterialDrops");
            host.transform.SetParent(scene, false);
        }

        private static void TintMilk(Material material)
        {
            if (material == null) return;
            Color milk = new Color(.96f, .95f, .91f, 1f);
            if (material.HasProperty("_Color"))
            {
                Color c = material.GetColor("_Color");
                material.SetColor("_Color", new Color(milk.r, milk.g, milk.b, c.a));
            }
            if (material.HasProperty("_TintColor"))
            {
                Color c = material.GetColor("_TintColor");
                material.SetColor("_TintColor", new Color(milk.r, milk.g, milk.b, c.a));
            }
        }

        private Material MaterialFor(LoveJuiceChannel channel)
        {
            Material existing;
            if (host == null || unavailable.Contains(channel)) return null;
            if (materials.TryGetValue(channel, out existing) && existing != null)
                return existing;
            string prefab = StudioLiquidCatalog.Prefab(channel);
            if (prefab == null) return null;
            try
            {
                GameObject source = CommonLib.LoadAsset<GameObject>(
                    StudioLiquidCatalog.Bundle, prefab, true,
                    StudioLiquidCatalog.Manifest);
                if (source == null)
                    source = CommonLib.LoadAsset<GameObject>(
                        StudioLiquidCatalog.Bundle, prefab, true, string.Empty);
                if (source == null)
                    throw new InvalidOperationException(
                        "Studio liquid item not found: " + prefab);
                Material shared = null;
                foreach (ParticleSystemRenderer renderer in
                    source.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        shared = renderer.sharedMaterial;
                        break;
                    }
                }
                if (shared == null)
                    throw new InvalidOperationException(
                        "No Studio liquid material: " + prefab);
                // Never rename, activate, reparent or destroy the borrowed
                // Studio asset or its material.
                Material material = new Material(shared);
                material.name = "LoveJuice." + channel + ".StudioMaterial";
                if (channel == LoveJuiceChannel.Milk) TintMilk(material);
                materials[channel] = material;
                return material;
            }
            catch (Exception ex)
            {
                unavailable.Add(channel);
                if (warn != null) warn("Studio droplet material unavailable: " +
                    channel + ": " + ex.Message);
                return null;
            }
        }

        private Drop CreateDrop(Material material, float diameter)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = "LoveJuice.PhysicalDrop";
            node.SetActive(false);
            node.transform.SetParent(host.transform, true);
            Collider solid = node.GetComponent<Collider>();
            if (solid != null) UnityEngine.Object.Destroy(solid);
            MeshRenderer mesh = node.GetComponent<MeshRenderer>();
            mesh.sharedMaterial = material;

            TrailRenderer thread = node.AddComponent<TrailRenderer>();
            thread.material = material;
            thread.time = .24f;
            thread.minVertexDistance = .003f;
            thread.startWidth = diameter * .38f;
            thread.endWidth = diameter * .05f;
            thread.enabled = true;

            var item = new Drop { Object = node, Thread = thread };
            drops.Add(item);
            return item;
        }

        internal bool Spawn(LoveJuiceChannel channel, Vector3 position,
            Vector3 velocity, float size, float lifetime, int maximum,
            float gravity, float damping, bool collision)
        {
            if (host == null || activeCount >= maximum ||
                !budget.TryReserve(Time.frameCount, activeCount, maximum))
                return false;
            // Saliva is a separate Bézier string; never send it through the
            // generic drop renderer. None of these channels emits a shower.
            if (channel == LoveJuiceChannel.Saliva) return false;

            Material material = MaterialFor(channel);
            if (material == null) return false;
            float diameter = Mathf.Clamp(size * .20f, .002f, .017f);
            Drop drop = null;
            foreach (Drop candidate in drops)
                if (!candidate.Active)
                {
                    drop = candidate;
                    break;
                }
            if (drop == null) drop = CreateDrop(material, diameter);

            MeshRenderer renderer = drop.Object.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            drop.Thread.material = material;
            drop.Thread.startWidth = diameter * .38f;
            drop.Thread.endWidth = diameter * .05f;
            drop.Thread.Clear();
            drop.Object.transform.position = position;
            drop.Object.transform.localScale = Vector3.one * diameter;
            drop.Object.SetActive(true);
            drop.Active = true;
            drop.Velocity = velocity;
            drop.Age = 0f;
            drop.Lifetime = Mathf.Clamp(lifetime, .15f, 7f);
            drop.Gravity = Mathf.Clamp(gravity, 0f, 30f);
            drop.Damping = Mathf.Clamp(damping, 0f, 10f);
            drop.Collision = collision;
            activeCount++;
            reservedFrame = Time.frameCount;
            return true;
        }

        internal void Tick(float deltaSeconds, float gravity,
            float damping, bool collision)
        {
            float dt = Mathf.Clamp(deltaSeconds, 0f, .05f);
            for (int i = 0; i < drops.Count; i++)
            {
                Drop drop = drops[i];
                if (!drop.Active || drop.Object == null) continue;
                drop.Age += dt;
                Vector3 previous = drop.Object.transform.position;
                drop.Velocity = (drop.Velocity + Vector3.down *
                    drop.Gravity * dt) * Mathf.Exp(-drop.Damping * dt);
                Vector3 movement = drop.Velocity * dt;
                bool collided = false;
                if (drop.Collision && drop.Age > .05f &&
                    movement.sqrMagnitude > 1e-8f)
                {
                    RaycastHit hit;
                    collided = Physics.Raycast(previous, movement.normalized,
                        out hit, movement.magnitude);
                }
                if (collided || drop.Age >= drop.Lifetime)
                {
                    drop.Active = false;
                    drop.Object.SetActive(false);
                    drop.Thread.Clear();
                    activeCount--;
                }
                else drop.Object.transform.position = previous + movement;
            }
        }

        internal void Dispose()
        {
            foreach (Drop drop in drops)
                if (drop.Object != null) UnityEngine.Object.Destroy(drop.Object);
            drops.Clear();
            foreach (Material material in materials.Values)
                if (material != null) UnityEngine.Object.Destroy(material);
            materials.Clear();
            unavailable.Clear();
            budget.Reset();
            activeCount = 0;
            reservedFrame = -1;
            if (host != null) UnityEngine.Object.Destroy(host);
            host = null;
        }
    }
}
