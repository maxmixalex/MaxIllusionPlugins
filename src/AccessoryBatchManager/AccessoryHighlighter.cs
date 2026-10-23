using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KKAPI.Maker;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccessoryBatchManager {
    internal sealed class AccessoryHighlighter : IDisposable {
        private static readonly Type SliderHighlightType = Type.GetType("SliderHighlight.SliderHighlightPlugin, KK_SliderHighlight", false);
        private static readonly FieldInfo BaseMaterialField = SliderHighlightType == null ? null : SliderHighlightType.GetField("_matSolid", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly Color AccentColor = new Color(1f, 0.72f, 0.08f, 1f);
        private static readonly Color SelectionColor = new Color(0.1f, 0.8f, 1f, 1f);

        private readonly List<KeyValuePair<Renderer, Material[]>> originals = new List<KeyValuePair<Renderer, Material[]>>();
        private readonly List<Material> overlays = new List<Material>();
        private string signature = string.Empty;
        private bool baseColorCaptured;
        private Color baseColor;

        internal void Update(ChaControl character, int accentSlot, IEnumerable<int> selectedSlots) {
            int[] selected = selectedSlots == null ? new int[0] : selectedSlots.Where(delegate(int slot) { return slot >= 0; }).Distinct().OrderBy(delegate(int slot) { return slot; }).ToArray();
            string nextSignature = (character == null ? "none" : character.GetInstanceID().ToString()) + ":" + accentSlot + ":" + string.Join(",", selected.Select(delegate(int slot) { return slot.ToString(); }).ToArray());
            if (nextSignature == signature) return;

            Clear();
            signature = nextSignature;
            if (character == null) return;
            Material baseMaterial = BaseMaterialField == null ? null : BaseMaterialField.GetValue(null) as Material;
            if (baseMaterial == null) {
                signature = string.Empty;
                return;
            }

            var kinds = new Dictionary<int, Color>();
            foreach (int slot in selected) kinds[slot] = SelectionColor;
            if (accentSlot >= 0) kinds[accentSlot] = AccentColor;
            foreach (KeyValuePair<int, Color> item in kinds)
                HighlightSlot(character, item.Key, item.Value, baseMaterial, item.Key == accentSlot);
        }

        internal void Clear() {
            foreach (KeyValuePair<Renderer, Material[]> item in originals) {
                if (item.Key != null) item.Key.sharedMaterials = item.Value;
            }
            originals.Clear();
            foreach (Material material in overlays) {
                if (material != null) Object.Destroy(material);
            }
            overlays.Clear();
            Material shared = BaseMaterialField == null ? null : BaseMaterialField.GetValue(null) as Material;
            if (baseColorCaptured && shared != null) shared.SetColor("_Color", baseColor);
            baseColorCaptured = false;
            signature = string.Empty;
        }

        private void HighlightSlot(ChaControl character, int slot, Color color, Material baseMaterial, bool useSharedMaterial) {
            GameObject accessory = character.objAccessory != null && slot >= 0 && slot < character.objAccessory.Length ? character.objAccessory[slot] : null;
            if (accessory == null) return;
            foreach (Renderer renderer in accessory.GetComponentsInChildren<Renderer>()) {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                Material[] original = renderer.sharedMaterials;
                if (original == null || original.Length == 0 || original[original.Length - 1] == null) continue;

                Material source = original[original.Length - 1];
                Material overlay;
                if (useSharedMaterial) {
                    if (!baseColorCaptured) {
                        baseColor = baseMaterial.GetColor("_Color");
                        baseColorCaptured = true;
                    }
                    overlay = baseMaterial;
                } else {
                    overlay = new Material(baseMaterial);
                    overlays.Add(overlay);
                }
                overlay.SetInt("_UseMaterialColor", 1);
                overlay.SetColor("_Color", color);
                if (source.HasProperty("_AlphaMask")) overlay.SetTexture("_AlphaMask", source.GetTexture("_AlphaMask"));
                if (source.HasProperty("_MainTex")) overlay.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                var highlighted = new Material[original.Length + 1];
                Array.Copy(original, highlighted, original.Length);
                highlighted[highlighted.Length - 1] = overlay;
                originals.Add(new KeyValuePair<Renderer, Material[]>(renderer, original));
                renderer.sharedMaterials = highlighted;
            }
        }

        public void Dispose() { Clear(); }
    }
}
