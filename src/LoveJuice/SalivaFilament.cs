using System;
using UnityEngine;

namespace LoveJuice
{
    /// <summary>
    /// Kiss-only saliva thread. Prefer the already-installed CyuNoVR
    /// BeroIto LineRenderer instead of drawing a second string. If that
    /// component is absent, create an independent Bézier thread from the
    /// same category of native liquid material, never a shower emitter.
    /// </summary>
    internal sealed class SalivaFilament
    {
        private Transform sceneRoot;
        private GameObject node;
        private LineRenderer line;
        private Material ownedMaterial;
        private readonly Vector3[] points = new Vector3[25];
        private float strength;
        private float nextNativeProbe;
        private bool nativeAvailable;

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
            // Sabakan's CyuNoVR is installed independently. Its original
            // "BeroIto" ribbon must not be duplicated, recoloured or deleted.
            LineRenderer[] lines =
                UnityEngine.Object.FindObjectsOfType<LineRenderer>();
            foreach (LineRenderer candidate in lines)
            {
                if (candidate != null && candidate != line &&
                    string.Equals(candidate.gameObject.name, "BeroIto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    nativeAvailable = true;
                    break;
                }
            }
        }

        private Material CloneNativeSalivaMaterial()
        {
            // Prefer the exact installed CyuNoVR BeroIto ribbon material.
            foreach (LineRenderer original in
                UnityEngine.Object.FindObjectsOfType<LineRenderer>())
                if (original != null && original != line &&
                    original.name == "BeroIto" &&
                    original.sharedMaterial != null)
                    return new Material(original.sharedMaterial);

            // Fallback to the installed game's viscous LiquidSiru item;
            // never substitute a shower stream for saliva.
            try {
                GameObject studio = CommonLib.LoadAsset<GameObject>(
                    StudioLiquidCatalog.Bundle,
                    StudioLiquidCatalog.Prefab(LoveJuiceChannel.Body),
                    true, StudioLiquidCatalog.Manifest);
                if (studio == null)
                    studio = CommonLib.LoadAsset<GameObject>(
                        StudioLiquidCatalog.Bundle,
                        StudioLiquidCatalog.Prefab(LoveJuiceChannel.Body),
                        true, string.Empty);
                if (studio != null) {
                    foreach (ParticleSystemRenderer renderer in
                        studio.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        if (renderer != null && renderer.sharedMaterial != null)
                            return new Material(renderer.sharedMaterial);
                }
            } catch { }

            // Last resort is a transparent thread, never the native water
            // or shower ParticleSystem effect.
            Shader shader = Shader.Find("Particles/Alpha Blended") ??
                            Shader.Find("Unlit/Transparent") ??
                            Shader.Find("Sprites/Default");
            return shader == null ? null : new Material(shader);
        }

        private bool EnsureLine()
        {
            if (line != null) return true;
            if (sceneRoot == null) return false;
            ownedMaterial = CloneNativeSalivaMaterial();
            if (ownedMaterial == null) return false;
            node = new GameObject("LoveJuice.SalivaThread");
            node.transform.SetParent(sceneRoot, false);
            line = node.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.numCapVertices = 8;
            line.numCornerVertices = 8;
            line.material = ownedMaterial;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, .25f), new Keyframe(.25f, 1f),
                new Keyframe(.75f, .8f), new Keyframe(1f, .15f));
            line.enabled = false;
            return true;
        }

        internal void Tick(bool enabled, bool kissSignal,
            Vector3 leftMouth, Vector3 rightMouth, float delta)
        {
            if (sceneRoot == null) return;
            ProbeNative();
            float distance = Vector3.Distance(leftMouth, rightMouth);
            bool draw = LiquidSourceTriggers.DrawSalivaFilament(
                enabled, kissSignal, distance, nativeAvailable);
            strength = Mathf.MoveTowards(strength, draw ? 1f : 0f,
                Mathf.Clamp(delta, 0f, .05f) * (draw ? 3f : 5f));
            if (nativeAvailable || strength < .03f ||
                distance >= .15f || !enabled)
            {
                if (line != null) line.enabled = false;
                return;
            }
            if (!EnsureLine()) return;

            // The same visual principle as the native Cyu kiss string:
            // a tapered, gravity-bent ribbon following both mouths, with
            // thickness decaying when contact is broken.
            Vector3 centre = (leftMouth + rightMouth) * .5f;
            Vector3 control = centre + Vector3.down *
                (.15f * distance * distance);
            for (int i = 0; i < points.Length; i++)
            {
                float t = (float)i / (points.Length - 1);
                float u = 1f - t;
                points[i] = u * u * leftMouth +
                    2f * u * t * control + t * t * rightMouth;
            }
            float width = .0045f * strength;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, width * .25f),
                new Keyframe(.25f, width),
                new Keyframe(.75f, width * .8f),
                new Keyframe(1f, width * .15f));
            line.enabled = true;
            line.SetPositions(points);
        }

        internal void Dispose()
        {
            strength = 0f;
            nativeAvailable = false;
            nextNativeProbe = 0f;
            if (node != null) UnityEngine.Object.Destroy(node);
            if (ownedMaterial != null) UnityEngine.Object.Destroy(ownedMaterial);
            node = null;
            line = null;
            ownedMaterial = null;
            sceneRoot = null;
        }
    }
}
