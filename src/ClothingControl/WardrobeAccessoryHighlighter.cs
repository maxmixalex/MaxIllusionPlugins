using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ClothingControl
{
    // Uses the same *private material overlay* approach as
    // AccessoriesBatchManager.AccessoryHighlighter. Never changes a
    // garment's textures or material properties in place and never calls
    // another MaxIllusion plugin at runtime.
    internal sealed class WardrobeAccessoryHighlighter : IDisposable
    {
        private sealed class AddedOverlay {
            internal Renderer Renderer;
            internal Material Material;
        }

        private static readonly Color Accent = new Color(1f, .72f, .08f, .85f);
        private readonly List<AddedOverlay> applied = new List<AddedOverlay>();
        private ChaControl owner;
        private GameObject root;
        private int slot = -1;
        private bool fallbackWarned;
        private readonly Action<string> warning;

        internal WardrobeAccessoryHighlighter(Action<string> warning) {
            this.warning = warning;
        }

        private static Material SliderHighlightMaterial()
        {
            Type pluginType = Type.GetType(
                "SliderHighlight.SliderHighlightPlugin, KK_SliderHighlight", false);
            FieldInfo field = pluginType == null ? null :
                pluginType.GetField("_matSolid",
                    BindingFlags.Static | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(null) as Material;
        }

        private Material NewOverlay(Material source)
        {
            Material baseMaterial = SliderHighlightMaterial();
            Material result;
            if (baseMaterial != null) {
                result = new Material(baseMaterial);
                if (result.HasProperty("_UseMaterialColor"))
                    result.SetInt("_UseMaterialColor", 1);
            } else {
                // A graceful independent fallback if SliderHighlight is
                // missing. Its alpha blended shader does not depend on
                // AccessoriesBatchManager or any installed asset path.
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) {
                    if (!fallbackWarned && warning != null) {
                        fallbackWarned = true;
                        warning("ClothingControl: no native overlay shader available for accessory highlighting.");
                    }
                    return null;
                }
                result = new Material(shader);
                result.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                result.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                result.SetInt("_ZWrite", 0);
                result.renderQueue = (int)RenderQueue.Transparent;
            }
            result.name = "ClothingControl.HoverOverlay";
            if (result.HasProperty("_Color"))
                result.SetColor("_Color", Accent);
            if (source != null) {
                if (result.HasProperty("_AlphaMask") && source.HasProperty("_AlphaMask"))
                    result.SetTexture("_AlphaMask", source.GetTexture("_AlphaMask"));
                if (result.HasProperty("_MainTex") && source.HasProperty("_MainTex"))
                    result.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            }
            return result;
        }

        internal void Update(ChaControl character, int accessorySlot)
        {
            GameObject nextRoot = character != null && character.objAccessory != null &&
                accessorySlot >= 0 && accessorySlot < character.objAccessory.Length
                ? character.objAccessory[accessorySlot] : null;
            if (nextRoot != null && !nextRoot.activeInHierarchy)
                nextRoot = null;
            if (ReferenceEquals(owner, character) &&
                ReferenceEquals(root, nextRoot) && slot == accessorySlot)
                return;
            Clear();
            owner = character;
            root = nextRoot;
            slot = accessorySlot;
            if (root == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers) {
                if (renderer == null || !renderer.enabled ||
                    (!(renderer is MeshRenderer) &&
                     !(renderer is SkinnedMeshRenderer))) continue;
                Material[] current = renderer.sharedMaterials;
                if (current == null || current.Length == 0 ||
                    current[current.Length - 1] == null) continue;
                Material overlay = NewOverlay(current[current.Length - 1]);
                if (overlay == null) continue;
                Material[] withOverlay = new Material[current.Length + 1];
                Array.Copy(current, withOverlay, current.Length);
                withOverlay[current.Length] = overlay;
                renderer.sharedMaterials = withOverlay;
                applied.Add(new AddedOverlay {
                    Renderer = renderer, Material = overlay
                });
            }
        }

        internal void Clear()
        {
            foreach (AddedOverlay item in applied) {
                if (item.Renderer == null || item.Material == null) continue;
                Material[] old = item.Renderer.sharedMaterials;
                if (old == null || old.Length == 0) continue;
                List<Material> remaining = new List<Material>(old.Length);
                foreach (Material m in old)
                    if (!ReferenceEquals(m, item.Material)) remaining.Add(m);
                if (remaining.Count != old.Length)
                    item.Renderer.sharedMaterials = remaining.ToArray();
            }
            foreach (AddedOverlay item in applied)
                if (item.Material != null) Object.Destroy(item.Material);
            applied.Clear();
            owner = null;
            root = null;
            slot = -1;
        }

        public void Dispose() { Clear(); }
    }
}
