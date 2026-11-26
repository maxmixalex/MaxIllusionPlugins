using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveJuice {
    // A bounded pool of actual Studio item prefabs. The Unity runtime owns
    // collisions, emission and object lifetime; there are no synthesized
    // billboard sprites or substitute particle packs in this implementation.
    internal sealed class StudioLiquidPool {
        private sealed class Drop {
            internal GameObject Node;
            internal LoveJuiceChannel Kind;
            internal DropletMotion Motion;
            internal float Lifetime, Scale;
            internal Vector3 OriginalScale;
        }

        private readonly Dictionary<LoveJuiceChannel, GameObject> templates =
            new Dictionary<LoveJuiceChannel, GameObject>();
        private readonly Dictionary<LoveJuiceChannel, Stack<GameObject>> inactive =
            new Dictionary<LoveJuiceChannel, Stack<GameObject>>();
        private readonly List<Drop> active = new List<Drop>();
        private readonly HashSet<LoveJuiceChannel> failed = new HashSet<LoveJuiceChannel>();
        private readonly Action<string> warn;
        private GameObject host;
        private int collisions;

        internal int ActiveCount { get { return active.Count; } }
        internal int Collisions { get { return collisions; } }

        internal StudioLiquidPool(Action<string> warn) { this.warn = warn; }

        internal void Attach(Transform scene) {
            Dispose();
            if (scene == null) return;
            host = new GameObject("LoveJuice.StudioLiquidEffects");
            host.transform.SetParent(scene, false);
        }

        private GameObject Template(LoveJuiceChannel kind) {
            if (host == null || failed.Contains(kind)) return null;
            GameObject template;
            if (templates.TryGetValue(kind, out template) && template != null) return template;
            string name = StudioLiquidCatalog.Prefab(kind);
            if (name == null) return null;
            try {
                // CommonLib's clone flag returns a live Unity object. Keep the
                // one template disabled; instantiate pooled instances from it.
                template = CommonLib.LoadAsset<GameObject>(StudioLiquidCatalog.Bundle,
                    name, true, StudioLiquidCatalog.Manifest);
                if (template == null)
                    template = CommonLib.LoadAsset<GameObject>(StudioLiquidCatalog.Bundle, name, true, "");
                if (template == null) throw new InvalidOperationException("Studio prefab not found: " + name);
                template.name = "LoveJuice.Template." + name;
                template.transform.SetParent(host.transform, false);
                template.SetActive(false);
                templates[kind] = template;
                return template;
            } catch (Exception ex) {
                failed.Add(kind);
                if (warn != null) warn("Native Studio liquid unavailable (" + name + "): " + ex.Message);
                return null;
            }
        }

        internal bool Spawn(LoveJuiceChannel kind, Vector3 position, Vector3 velocity,
            float size, float duration, int limit) {
            if (host == null || active.Count >= limit) return false;
            GameObject template = Template(kind);
            if (template == null) return false;
            GameObject node = null;
            Stack<GameObject> recycled;
            if (inactive.TryGetValue(kind, out recycled)) {
                while (recycled.Count > 0 && node == null) node = recycled.Pop();
            }
            if (node == null) {
                node = UnityEngine.Object.Instantiate(template);
                if (node == null) return false;
                node.transform.SetParent(host.transform, false);
                node.name = "LoveJuice." + kind;
            }
            node.transform.position = position;
            node.transform.rotation = Quaternion.identity;
            node.transform.localScale = template.transform.localScale * size;
            node.SetActive(true);
            // These components, when present in the game's prefab, must
            // start their original native lifetime rather than a fake effect.
            ParticleSystem[] systems = node.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems) {
                if (system != null) { system.Clear(); system.Play(); }
            }
            active.Add(new Drop {
                Node = node, Kind = kind, OriginalScale = template.transform.localScale,
                Scale = size, Lifetime = duration,
                Motion = new DropletMotion {
                    X = position.x, Y = position.y, Z = position.z,
                    VX = velocity.x, VY = velocity.y, VZ = velocity.z
                }
            });
            return true;
        }

        private void Recycle(int index) {
            Drop drop = active[index];
            if (drop.Node != null) {
                drop.Node.SetActive(false);
                Stack<GameObject> pool;
                if (!inactive.TryGetValue(drop.Kind, out pool)) {
                    pool = new Stack<GameObject>();
                    inactive[drop.Kind] = pool;
                }
                pool.Push(drop.Node);
            }
            active.RemoveAt(index);
        }

        internal void Tick(float frameTime, float gravity, float damping, bool collide) {
            // The game's frame rate can spike during load. Bound the catch-up
            // interval; never teleport pooled liquids through the world.
            float time = DropletPhysics.Clamp(frameTime, 0f, .15f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(time / .03f));
            float dt = time / steps;
            for (int i = active.Count - 1; i >= 0; i--) {
                Drop d = active[i];
                if (d.Node == null) { active.RemoveAt(i); continue; }
                bool hitGround = false;
                for (int j = 0; j < steps; j++) {
                    Vector3 previous = d.Node.transform.position;
                    d.Motion = DropletPhysics.Step(d.Motion, dt, gravity, damping);
                    Vector3 next = new Vector3(d.Motion.X, d.Motion.Y, d.Motion.Z);
                    Vector3 distance = next - previous;
                    if (collide && d.Motion.Age > .1f && distance.sqrMagnitude > .000001f &&
                        Physics.Raycast(previous, distance.normalized, distance.magnitude)) {
                        hitGround = true;
                        collisions++;
                        break;
                    }
                    d.Node.transform.position = next;
                }
                if (hitGround || d.Motion.Age >= d.Lifetime) { Recycle(i); continue; }
                float life = Mathf.Clamp01(d.Motion.Age / d.Lifetime);
                d.Node.transform.localScale = d.OriginalScale * d.Scale * (1f - .5f * life);
            }
        }

        internal void Dispose() {
            active.Clear();
            inactive.Clear();
            templates.Clear();
            failed.Clear();
            if (host != null) UnityEngine.Object.Destroy(host);
            host = null;
            collisions = 0;
        }
    }
}
