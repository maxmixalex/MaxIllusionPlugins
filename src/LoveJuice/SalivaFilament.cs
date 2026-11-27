using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoveJuice
{
    // Volumetric saliva: a shaded tube of dynamic 3D vertices, not an
    // image ribbon. No CyuNoVR or BeroIto runtime coupling.
    internal sealed class SalivaFilament
    {
        private const int Rows = 24, Sides = 10, MaxDrops = 10;
        private Transform root;
        private GameObject strandObject;
        private Mesh strandMesh, tearMesh;
        private MeshRenderer renderer;
        private Material material;
        private readonly Vector3[] vertices = new Vector3[Rows * Sides];
        private readonly Vector3[] centres = new Vector3[Rows];
        private readonly List<Tear> tears = new List<Tear>();
        private float time, phase, opacity;

        private sealed class Tear {
            internal GameObject Object;
            internal Vector3 Position, Velocity;
            internal float Age, Lifetime, Radius;
            internal bool Active;
        }

        internal void Attach(Transform sceneRoot) {
            Dispose();
            root = sceneRoot;
        }

        private static Material NewFluidMaterial() {
            Shader shader = Shader.Find("Standard") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Particles/Alpha Blended");
            if (shader == null) return null;
            Material mat = new Material(shader);
            mat.name = "LoveJuice.LitViscousFluid";
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", new Color(.86f, .96f, 1f, .53f));
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", .94f);
            if (mat.HasProperty("_Mode")) {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            return mat;
        }

        private static Mesh TearShape() {
            const int rings = 11, radial = 10;
            Vector3[] v = new Vector3[rings * radial];
            int[] tr = new int[(rings - 1) * radial * 6];
            for (int y = 0; y < rings; y++) {
                float t = (float)y / (rings - 1);
                float rad = Mathf.Pow(Mathf.Max(.002f, Mathf.Sin(Mathf.PI * t)), .7f)
                    * Mathf.Lerp(.48f, 1.3f, t);
                for (int j = 0; j < radial; j++) {
                    float angle = Mathf.PI * 2f * j / radial;
                    int i = y * radial + j;
                    v[i] = new Vector3(Mathf.Cos(angle) * rad, 2f * t - 1f,
                        Mathf.Sin(angle) * rad);
                    if (y == rings - 1) continue;
                    int n = (y * radial + j) * 6;
                    int next = y * radial + (j + 1) % radial;
                    int lower = i + radial;
                    tr[n] = i; tr[n + 1] = lower; tr[n + 2] = next;
                    tr[n + 3] = next; tr[n + 4] = lower;
                    tr[n + 5] = lower + (j + 1) % radial;
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = "LoveJuice.3DFluidTear";
            mesh.vertices = v;
            mesh.triangles = tr;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Ensure() {
            if (strandMesh != null || root == null) return;
            material = NewFluidMaterial();
            if (material == null) return;
            strandObject = new GameObject("LoveJuice.3DFluidFilament");
            strandObject.transform.SetParent(root, false);
            MeshFilter filter = strandObject.AddComponent<MeshFilter>();
            renderer = strandObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            strandMesh = new Mesh();
            strandMesh.name = "LoveJuice.DynamicFluidVolume";
            strandMesh.MarkDynamic();
            int[] faces = new int[(Rows - 1) * Sides * 6];
            for (int y = 0; y < Rows - 1; y++)
                for (int j = 0; j < Sides; j++) {
                    int n = (y * Sides + j) * 6;
                    int i = y * Sides + j, b = i + Sides;
                    int a = y * Sides + (j + 1) % Sides;
                    faces[n] = i; faces[n+1] = b; faces[n+2] = a;
                    faces[n+3] = a; faces[n+4] = b; faces[n+5] = a+Sides;
                }
            strandMesh.vertices = vertices;
            strandMesh.triangles = faces;
            filter.sharedMesh = strandMesh;
            tearMesh = TearShape();
            renderer.enabled = false;
        }

        private Tear Acquire() {
            foreach (Tear d in tears) if (!d.Active) return d;
            if (tears.Count >= MaxDrops || root == null || tearMesh == null)
                return null;
            GameObject obj = new GameObject("LoveJuice.3DFluidDrop");
            obj.transform.SetParent(root, false);
            obj.AddComponent<MeshFilter>().sharedMesh = tearMesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            obj.SetActive(false);
            Tear result = new Tear { Object = obj };
            tears.Add(result);
            return result;
        }

        private void BreakOff(Vector3 position, Vector3 tangent) {
            Tear d = Acquire();
            if (d == null) return;
            d.Position = position;
            d.Velocity = Vector3.down * .07f + tangent * .023f;
            d.Age = 0f;
            d.Lifetime = 1.1f;
            d.Radius = .0023f;
            d.Active = true;
            d.Object.SetActive(true);
        }

        private void AdvanceDrops(float dt) {
            foreach (Tear d in tears) {
                if (!d.Active || d.Object == null) continue;
                d.Age += dt;
                if (d.Age >= d.Lifetime) {
                    d.Object.SetActive(false);
                    d.Active = false;
                    continue;
                }
                d.Velocity += Vector3.down * (2.4f * dt);
                d.Velocity *= Mathf.Exp(-.22f * dt);
                d.Position += d.Velocity * dt;
                d.Object.transform.position = d.Position;
                d.Object.transform.rotation = Quaternion.FromToRotation(
                    Vector3.down, d.Velocity.sqrMagnitude > .00001f ?
                        d.Velocity.normalized : Vector3.down);
                float stretch = Mathf.Clamp(d.Velocity.magnitude * 1.7f, .65f, 2.3f);
                d.Object.transform.localScale =
                    new Vector3(d.Radius, d.Radius * stretch, d.Radius);
            }
        }

        internal void Tick(bool enabled, bool activity, bool nativeKiss,
            Vector3 from, Vector3 to, float dt)
        {
            if (root == null) return;
            float delta = Mathf.Clamp(dt, 0f, .05f);
            time += delta;
            AdvanceDrops(delta);
            Ensure();
            if (strandMesh == null) return;
            float distance = Vector3.Distance(from, to);
            bool active = enabled && activity &&
                distance >= .001f && distance < .22f;
            opacity = Mathf.MoveTowards(opacity, active ? 1f : 0f,
                delta * (active ? 4f : 6f));
            if (!active || opacity < .02f) {
                renderer.enabled = false;
                phase = 0f;
                return;
            }
            float cycle = LiquidSourceTriggers.SalivaTearPeriod(distance);
            phase += delta;
            bool tear = phase >= cycle;
            if (tear) phase %= cycle;
            float tCycle = Mathf.Clamp01(phase / cycle);
            float sag = LiquidSourceTriggers.SalivaSag(distance, time, tCycle);
            Vector3 axis = (to - from).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < .00001f)
                side = Vector3.Cross(axis, Vector3.forward);
            side.Normalize();
            Vector3 up = Vector3.Cross(axis, side).normalized;

            for (int y = 0; y < Rows; y++) {
                float t = (float)y / (Rows - 1);
                float arch = 4f * t * (1f - t);
                centres[y] = Vector3.Lerp(from, to, t) +
                    Vector3.down * (sag * arch +
                        Mathf.Pow(t, 5f) * Mathf.Pow(tCycle, 2f) * .014f) +
                    side * (Mathf.Sin(time * 7f + t * 5f) * .003f * arch);
                float radius = LiquidSourceTriggers.SalivaRadius(t, tCycle, opacity);
                for (int j = 0; j < Sides; j++) {
                    float a = Mathf.PI * 2f * j / Sides;
                    vertices[y * Sides + j] = centres[y] +
                        radius * (side * Mathf.Cos(a) + up * Mathf.Sin(a));
                }
            }
            strandMesh.vertices = vertices;
            strandMesh.RecalculateNormals();
            strandMesh.RecalculateBounds();
            renderer.enabled = true;
            if (tear) BreakOff(centres[Rows - 2], axis);
        }

        internal void Dispose() {
            foreach (Tear d in tears)
                if (d.Object != null) UnityEngine.Object.Destroy(d.Object);
            tears.Clear();
            if (strandObject != null) UnityEngine.Object.Destroy(strandObject);
            if (strandMesh != null) UnityEngine.Object.Destroy(strandMesh);
            if (tearMesh != null) UnityEngine.Object.Destroy(tearMesh);
            if (material != null) UnityEngine.Object.Destroy(material);
            strandObject = null;
            strandMesh = tearMesh = null;
            renderer = null; material = null;
            root = null; time = phase = opacity = 0f;
        }
    }
}
