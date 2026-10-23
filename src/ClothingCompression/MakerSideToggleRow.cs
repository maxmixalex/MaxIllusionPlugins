using BepInEx;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace ClothingCompression {
    // A single native Maker row containing two stock-styled checkboxes.
    internal sealed class MakerSideToggleRow : BaseEditableGuiEntry<int> {
        private static Transform toggleTemplate;
        private static readonly MethodInfo RemoveLocalisationMethod = typeof(BaseGuiEntry).GetMethod("RemoveLocalisation", BindingFlags.Static | BindingFlags.NonPublic);

        internal MakerSideToggleRow(MakerCategory category, BaseUnityPlugin owner) : base(category, 3, owner) { }

        protected override void Initialize() {
            if (toggleTemplate != null) return;
            Transform original = GetExistingControl("CustomScene/CustomRoot/FrontUIGroup/CustomUIGroup/CvsMenuTree/00_FaceTop/tglMouth", "tglCanine");
            toggleTemplate = Object.Instantiate(original, GuiCacheTransfrom, false);
            toggleTemplate.gameObject.SetActive(false);
            Toggle toggle = toggleTemplate.GetComponentInChildren<Toggle>();
            toggle.onValueChanged.RemoveAllListeners(); toggle.image.raycastTarget = true; toggle.graphic.raycastTarget = true;
            if (RemoveLocalisationMethod != null) RemoveLocalisationMethod.Invoke(null, new object[] { toggleTemplate.gameObject });
        }

        protected override GameObject OnCreateControl(Transform subCategoryList) {
            GameObject row = new GameObject("tglCompressionSides", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(subCategoryList, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 8f; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            LayoutElement rowLayout = row.GetComponent<LayoutElement>(); rowLayout.minHeight = 36f; rowLayout.flexibleWidth = 1f;
            Transform left = CreateToggle(row.transform, "左侧"); Transform right = CreateToggle(row.transform, "右侧");
            Toggle leftToggle = left.GetComponentInChildren<Toggle>(); Toggle rightToggle = right.GetComponentInChildren<Toggle>();
            leftToggle.onValueChanged.AddListener(v => SetValue((Value & 2) | (v ? 1 : 0)));
            rightToggle.onValueChanged.AddListener(v => SetValue((Value & 1) | (v ? 2 : 0)));
            BufferedValueChanged.Subscribe(v => { leftToggle.isOn = (v & 1) != 0; rightToggle.isOn = (v & 2) != 0; });
            return row;
        }

        private Transform CreateToggle(Transform parent, string text) {
            Transform copy = Object.Instantiate(toggleTemplate, parent, false); copy.gameObject.SetActive(true);
            TextMeshProUGUI label = copy.GetComponentInChildren<TextMeshProUGUI>(); if (label != null) { label.text = text; label.color = TextColor; }
            LayoutElement layout = copy.GetComponent<LayoutElement>(); if (layout != null) { layout.minWidth = 0f; layout.flexibleWidth = 1f; }
            return copy;
        }
    }
}
