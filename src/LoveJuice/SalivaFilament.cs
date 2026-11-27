using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveJuice
{
    /// <summary>
    /// Dynamic saliva: continuously stretching, oscillating and necking
    /// filaments, with independently falling droplets when they break.
    /// Drawn with the installed CyuNoVR/Studio liquid material rather than
    /// a static sprite or the game's continuous shower ParticleSystem.
    ///
    /// The original CyuNoVR BeroIto filament always takes precedence while
    /// the native kiss is active. Our renderer never mutates native effects.
    /// </summary>
    internal sealed class SalivaFilament
    {
        private sealed class DetachedDroplet
        {
            internal GameObject Object;
            internal LineRenderer Renderer;
            internal Vector3 Position;
            internal Vector3 Velocity;
            internal float Age;
            internal float Life;
            internal bool Active;
        }

        private Transform sceneRoot;
        private GameObject node;
        private LineRenderer line;
        private Material ownedMaterial;
        private readonly Vector3[] points = new Vector3[25];
        private readonly List<DetachedDroplet> droplets =
            new List<DetachedDroplet>(8);
        private float strength;
        private float elapsed;
        private float phase;
        private float nextNativeProbe;
        private bool nativeAvailable;
        private LineRenderer nativeFilament;
        private int tearSequence;
        private const int MaxDroplets = 8;

        internal void Attach(Transform root)
        {
            Dispose();
            sceneRoot = root;
        }

        private void ProbeNative()
        {
            if (Time.unscaledTime < nextNativeProbe) return;
            nextNativeProbe = Time.unscaledTime + .5f;
            nativeAvailable = false;
            nativeFilament = null;
            LineRenderer[] lines =
                UnityEngine.Object.FindObjectsOfType<LineRenderer>();
            foreach (LineRenderer candidate in lines)
            {
                if (candidate != null && candidate != line &&
                    candidate.enabled && candidate.gameObject.activeInHierarchy &&
                    candidate.positionCount >= 2 &&
                    string.Equals(candidate.gameObject.name, "BeroIto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    nativeAvailable = true;
                    nativeFilament = candidate;
                    break;
                }
            }
        }

        private Material CloneNativeSalivaMaterial()
        {
            foreach (LineRenderer original in
                UnityEngine.Object.FindObjectsOfType<LineRenderer>())
                if (original != null && original != line &&
                    original.name == "BeroIto" &&
                    original.sharedMaterial != null)
                    return new Material(original.sharedMaterial);

            try
            {
                GameObject studio = CommonLib.LoadAsset<GameObject>(
                    StudioLiquidCatalog.Bundle,
                    StudioLiquidCatalog.Prefab(LoveJuiceChannel.Body),
                    true, StudioLiquidCatalog.Manifest);
                if (studio == null)
                    studio = CommonLib.LoadAsset<GameObject>(
                        StudioLiquidCatalog.Bundle,
                        StudioLiquidCatalog.Prefab(LoveJuiceChannel.Body),
                        true, string.Empty);
                if (studio != null)
                    foreach (ParticleSystemRenderer renderer in
                        studio.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        if (renderer != null && renderer.sharedMaterial != null)
                            return new Material(renderer.sharedMaterial);
            }
            catch { }

            Shader shader = Shader.Find("Particles/Alpha Blended") ??
                            Shader.Find("Unlit/Transparent") ??
                            Shader.Find("Sprites/Default");
            return shader == null ? null : new Material(shader);
        }

        private bool EnsureLine()
        {
            if (line != null) return true;
            if (sceneRoot == null) return false;
            if (ownedMaterial == null)
                ownedMaterial = CloneNativeSalivaMaterial();
            if (ownedMaterial == null) return false;
            node = new GameObject("LoveJuice.DynamicSalivaThread");
            node.transform.SetParent(sceneRoot, false);
            line = node.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.numCapVertices = 6;
            line.numCornerVertices = 6;
            line.material = ownedMaterial;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.enabled = false;
            return true;
        }

        private DetachedDroplet AcquireDroplet()
        {
            foreach (DetachedDroplet item in droplets)
                if (!item.Active) return item;
            if (droplets.Count >= MaxDroplets ||
                sceneRoot == null || ownedMaterial == null) return null;

            GameObject obj = new GameObject("LoveJuice.SalivaTear");
            obj.transform.SetParent(sceneRoot, false);
            LineRenderer renderer = obj.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.positionCount = 4;
            renderer.numCapVertices = 7;
            renderer.numCornerVertices = 5;
            renderer.material = ownedMaterial;
            renderer.startColor = Color.white;
            renderer.endColor = Color.white;
            renderer.enabled = false;
            var drop = new DetachedDroplet { Object = obj, Renderer = renderer };
            droplets.Add(drop);
            return drop;
        }

        private void ReleaseDroplet(Vector3 position, Vector3 tangent)
        {
            DetachedDroplet drop = AcquireDroplet();
            if (drop == null) return;
            float side = Mathf.Sin(tearSequence++ * 2.37f) * .06f;
            drop.Position = position;
            drop.Velocity = Vector3.down * .06f + tangent * side;
            drop.Age = 0f;
            drop.Life = 1.1f + .18f * Mathf.Abs(side) / .06f;
            drop.Active = true;
            drop.Renderer.enabled = true;
        }

        private void TickDroplets(float delta)
        {
            foreach (DetachedDroplet drop in droplets)
            {
                if (!drop.Active || drop.Renderer == null) continue;
                drop.Age += delta;
                if (drop.Age >= drop.Life)
                {
                    drop.Active = false;
                    drop.Renderer.enabled = false;
                    continue;
                }
                // Gravity pulls a detached tear through the world rather
                // than keeping it attached to either animated character.
                drop.Velocity += Vector3.down * (1.7f * delta);
                drop.Position += drop.Velocity * delta;
                float t = drop.Age / drop.Life;
                float width = Mathf.Lerp(.0033f, .00065f, t);
                float tail = Mathf.Lerp(.006f, .012f, Mathf.Clamp01(
                    drop.Velocity.magnitude * 1.2f));
                Vector3 backward = drop.Velocity.sqrMagnitude > .0001f
                    ? -drop.Velocity.normalized : Vector3.up;
                // Small rounded head and stretched trailing fluid filament,
                // NOT a sphere, static decal or shower particle.
                Vector3[] shape = {
                    drop.Position + backward * tail,
                    drop.Position + backward * tail * .43f,
                    drop.Position,
                    drop.Position - backward * .002f
                };
                drop.Renderer.SetPositions(shape);
                drop.Renderer.widthCurve = new AnimationCurve(
                    new Keyframe(0f, width * .18f),
                    new Keyframe(.52f, width * .45f),
                    new Keyframe(.86f, width),
                    new Keyframe(1f, width * .25f));
            }
        }

        internal void Tick(bool enabled, bool activity, bool nativeKiss,
            Vector3 from, Vector3 to, float delta)
        {
            if (sceneRoot == null) return;
            float dt = Mathf.Clamp(delta, 0f, .05f);
            elapsed += dt;
            TickDroplets(dt);
            ProbeNative();

            float distance = Vector3.Distance(from, to);
            bool suppressNative = nativeKiss && nativeAvailable;
            bool draw = LiquidSourceTriggers.DrawSalivaFilament(
                enabled, activity, distance, suppressNative);
            strength = Mathf.MoveTowards(strength, draw ? 1f : 0f,
                dt * (draw ? 4f : 6f));

            if (suppressNative)
            {
                // The installed BeroIto thread owns its own shape. Never
                // draw a second copy, but still permit small detached tears
                // from the native thread's live endpoint.
                if (line != null) line.enabled = false;
                if (ownedMaterial == null)
                    ownedMaterial = CloneNativeSalivaMaterial();
                float cycle = LiquidSourceTriggers.SalivaTearPeriod(distance);
                phase += dt;
                if (phase >= cycle && nativeFilament != null &&
                    ownedMaterial != null) {
                    phase %= cycle;
                    int last = nativeFilament.positionCount - 1;
                    Vector3 tip = nativeFilament.GetPosition(last);
                    Vector3 previous = nativeFilament.GetPosition(last - 1);
                    if (!nativeFilament.useWorldSpace) {
                        tip = nativeFilament.transform.TransformPoint(tip);
                        previous = nativeFilament.transform.TransformPoint(previous);
                    }
                    Vector3 tangent = tip - previous;
                    ReleaseDroplet(tip, tangent.sqrMagnitude > .00001f
                        ? tangent.normalized : Vector3.down);
                }
                return;
            }
            if (strength < .025f ||
                distance >= .15f || !enabled)
            {
                if (line != null) line.enabled = false;
                phase = 0f;
                return;
            }
            if (!EnsureLine()) return;

            float cycle = LiquidSourceTriggers.SalivaTearPeriod(distance);
            phase += dt;
            if (phase >= cycle)
            {
                phase %= cycle;
                Vector3 nearEnd = points[points.Length - 2];
                Vector3 tangent = Vector3.Distance(from, to) > .002f
                    ? (to - from).normalized : Vector3.down;
                if (strength > .3f)
                    ReleaseDroplet(nearEnd, tangent);
            }
            float progress = Mathf.Clamp01(phase / cycle);
            float sag = LiquidSourceTriggers.SalivaSag(distance,
                elapsed, progress);
            Vector3 axis = (to - from).normalized;
            Vector3 sideAxis = Vector3.Cross(axis, Vector3.up);
            if (sideAxis.sqrMagnitude < .0001f)
                sideAxis = Vector3.Cross(axis, Vector3.forward);
            sideAxis.Normalize();

            for (int i = 0; i < points.Length; i++)
            {
                float t = (float)i / (points.Length - 1);
                float arch = 4f * t * (1f - t);
                float flutter = Mathf.Sin(elapsed * 7.4f + t * 5.6f)
                    * (.003f + .002f * progress) * arch;
                // Neck and fall visibly develop near the tip just before
                // a tear separates. Anchors remain attached to the mouths.
                float fallingTip = Mathf.Pow(t, 5f) *
                    Mathf.Pow(progress, 2.2f) * .012f;
                points[i] = Vector3.Lerp(from, to, t) +
                    Vector3.down * (sag * arch + fallingTip) +
                    sideAxis * flutter;
            }

            float rhythmicWidth = .75f +
                .25f * Mathf.Sin(elapsed * 9.1f);
            float neck = 1f - .72f * Mathf.Pow(progress, 4f);
            float width = .0041f * strength * rhythmicWidth;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, width * .35f),
                new Keyframe(.22f, width),
                new Keyframe(.65f, width * .78f),
                new Keyframe(.88f, width * neck),
                new Keyframe(1f, width * .18f * neck));
            line.enabled = true;
            line.SetPositions(points);
        }

        internal void Dispose()
        {
            strength = elapsed = phase = 0f;
            nativeAvailable = false;
            nativeFilament = null;
            nextNativeProbe = 0f;
            tearSequence = 0;
            foreach (DetachedDroplet drop in droplets)
                if (drop.Object != null)
                    UnityEngine.Object.Destroy(drop.Object);
            droplets.Clear();
            if (node != null) UnityEngine.Object.Destroy(node);
            if (ownedMaterial != null) UnityEngine.Object.Destroy(ownedMaterial);
            node = null;
            line = null;
            ownedMaterial = null;
            sceneRoot = null;
        }
    }
}
