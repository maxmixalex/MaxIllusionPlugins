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

        private readonly List<KeyValuePair<Renderer, Material>> applied = new List<KeyValuePair<Renderer, Material>>();
        private readonly List<Material> overlays = new List<Material>();
        private readonly Func<Material> getBaseMaterial;
        private string signature = string.Empty;

        internal AccessoryHighlighter() : this(delegate {
            return BaseMaterialField == null ? null : BaseMaterialField.GetValue(null) as Material;
        }) { }

        internal AccessoryHighlighter(Func<Material> getBaseMaterial) {
            if (getBaseMaterial == null) throw new ArgumentNullException("getBaseMaterial");
            this.getBaseMaterial = getBaseMaterial;
        }

        internal void Update(ChaControl character, int accentSlot, IEnumerable<int> selectedSlots) {
            int[] selected = selectedSlots == null ? new int[0] : selectedSlots.Where(delegate(int slot) { return slot >= 0; }).Distinct().OrderBy(delegate(int slot) { return slot; }).ToArray();
            string nextSignature = (character == null ? "none" : character.GetInstanceID().ToString()) + ":" + accentSlot + ":" +
                string.Join(",", selected.Concat(new[] { accentSlot }).Distinct().Select(slot => {
                    GameObject root = AccessoryRoot(character, slot);
                    return slot + "@" + (root == null ? 0 : root.GetInstanceID());
                }).ToArray());
            if (nextSignature == signature) return;

            Clear();
            signature = nextSignature;
            if (character == null) return;
            Material baseMaterial = getBaseMaterial();
            if (baseMaterial == null) {
                signature = string.Empty;
                return;
            }

            var kinds = new Dictionary<int, Color>();
            foreach (int slot in selected) kinds[slot] = SelectionColor;
            if (accentSlot >= 0) kinds[accentSlot] = AccentColor;
            var seen = new HashSet<Renderer>();
            foreach (KeyValuePair<int, Color> item in kinds)
                HighlightSlot(character, item.Key, item.Value, baseMaterial, seen);
        }

        internal void Clear() {
            foreach (KeyValuePair<Renderer, Material> item in applied) {
                if (item.Key == null) continue;
                Material[] current = item.Key.sharedMaterials;
                if (current == null || !current.Contains(item.Value)) continue;
                // Remove only our overlay. Other plugins may have changed the base
                // materials while this selection was highlighted.
                item.Key.sharedMaterials = current.Where(material => material != item.Value).ToArray();
            }
            applied.Clear();
            foreach (Material material in overlays) {
                if (material != null) Object.Destroy(material);
            }
            overlays.Clear();
            signature = string.Empty;
        }

        private static GameObject AccessoryRoot(ChaControl character, int slot) {
            return character != null && character.objAccessory != null && slot >= 0 && slot < character.objAccessory.Length ? character.objAccessory[slot] : null;
        }

        private void HighlightSlot(ChaControl character, int slot, Color color, Material baseMaterial, HashSet<Renderer> seen) {
            GameObject accessory = AccessoryRoot(character, slot);
            if (accessory == null) return;
            foreach (Renderer renderer in accessory.GetComponentsInChildren<Renderer>()) {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer) || !seen.Add(renderer)) continue;
                Material[] original = renderer.sharedMaterials;
                if (original == null || original.Length == 0 || original[original.Length - 1] == null) continue;

                Material source = original[original.Length - 1];
                // SliderHighlight owns its shared material. Mutating its textures or
                // flags also changes every other renderer using that material.
                Material overlay = new Material(baseMaterial);
                overlays.Add(overlay);
                overlay.SetInt("_UseMaterialColor", 1);
                overlay.SetColor("_Color", color);
                if (source.HasProperty("_AlphaMask")) overlay.SetTexture("_AlphaMask", source.GetTexture("_AlphaMask"));
                if (source.HasProperty("_MainTex")) overlay.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                var highlighted = new Material[original.Length + 1];
                Array.Copy(original, highlighted, original.Length);
                highlighted[highlighted.Length - 1] = overlay;
                applied.Add(new KeyValuePair<Renderer, Material>(renderer, overlay));
                renderer.sharedMaterials = highlighted;
            }
        }

        public void Dispose() { Clear(); }
    }
}
