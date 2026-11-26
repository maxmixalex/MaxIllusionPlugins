using System;
using System.Collections.Generic;
using ChaCustom;
using UnityEngine;
using UnityEngine.UI;
using UniRx.Triggers;
using Object = UnityEngine.Object;

namespace FemboyMode
{
    /// <summary>
    /// Non-invasive scrolling for FemboyMode-owned extension rows only.
    /// Native Body > All rows, including any third-party widgets, stay
    /// under the original parent with their layout and scripts untouched.
    /// </summary>
    internal sealed class MakerPageScroll : MonoBehaviour
    {
        private const string TemplatePath = "03_ClothesTop/tglSocks/SocksTop/Scroll View";
        private const string HolderName = "MaxMixAlex.NativeMakerPageScroll";
        private readonly List<Row> rows = new List<Row>();
        private readonly List<ObservableScrollTrigger> wheelHandlers = new List<ObservableScrollTrigger>();
         private RectTransform host, slot;
        private VerticalLayoutGroup hostLayout;
        private LayoutElement slotLayout;
        private ScrollRect scroll;
        private bool oldForceExpand, installed, resetPosition = true;
        private bool validated;
        private int installedFrame;
         private float templateHeight, appliedHeight;

        private sealed class Row
        {
            internal readonly RectTransform Rect;
            internal readonly int Index;
            private readonly Vector2 anchorMin, anchorMax, pivot, size, position;
            private readonly Vector3 scale;
            private readonly Quaternion rotation;

            internal Row(RectTransform rect)
            {
                Rect = rect;
                Index = rect.GetSiblingIndex();
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
                pivot = rect.pivot;
                size = rect.sizeDelta;
                position = rect.anchoredPosition;
                scale = rect.localScale;
                rotation = rect.localRotation;
            }

            internal void Restore(RectTransform parent)
            {
                if (Rect == null) return;
                Rect.SetParent(parent, false);
                Rect.SetSiblingIndex(Math.Min(Index, parent.childCount - 1));
                Rect.anchorMin = anchorMin;
                Rect.anchorMax = anchorMax;
                Rect.pivot = pivot;
                Rect.sizeDelta = size;
                Rect.anchoredPosition = position;
                Rect.localScale = scale;
                Rect.localRotation = rotation;
            }
        }

        private static Transform FindMenu(Transform from)
        {
            bool bodyAll = false;
            for (Transform t = from; t != null; t = t.parent)
            {
                if (t.name == "tglAll" && t.parent != null && t.parent.name == "01_BodyTop")
                    bodyAll = true;
                if (t.name == "CvsMenuTree") return bodyAll ? t : null;
            }
            return null;
        }

        private static bool AlreadyScrollable(RectTransform page)
        {
            for (Transform t = page; t != null; t = t.parent)
            {
                ScrollRect native = t.GetComponent<ScrollRect>();
                if (native != null && native.vertical && native.content != null &&
                    (native.content == page || page.IsChildOf(native.content))) return true;
            }
            return false;
        }

        internal static MakerPageScroll Install(Transform anchor, IList<Transform> pluginWidgets)
        {
            RectTransform page = anchor == null ? null : anchor.parent as RectTransform;
            if (page == null) return null;
            Transform menu = FindMenu(page);
            if (menu == null) throw new InvalidOperationException("Could not verify native Body > All hierarchy.");
            if (AlreadyScrollable(page)) return null;

            VerticalLayoutGroup layout = page.GetComponent<VerticalLayoutGroup>();
            if (layout == null || !layout.enabled)
                throw new InvalidOperationException("Native page is not a vertical layout.");
            Transform template = menu.Find(TemplatePath);
            ScrollRect source = template == null ? null : template.GetComponent<ScrollRect>();
            if (source == null || !source.vertical || source.content == null ||
                source.viewport == null || source.verticalScrollbar == null)
                throw new InvalidOperationException("Native Maker scroll template is unavailable.");

            // The 0.5.6-0.5.8 regression moved EVERY child of the
            // native AllTop root into a cloned ScrollRect. The game still
            // expects the original hierarchy, so this could make the whole
            // page disappear without raising any exception.
            // Never move anything except our 24 explicitly registered
            // geometry widgets. The Femboy flag remains visible at the top.
            if (pluginWidgets == null || pluginWidgets.Count != 24)
                throw new InvalidOperationException("Expected exactly 24 owned geometry widgets.");
            var used = new HashSet<Transform>();
            List<Row> original = new List<Row>();
            foreach (Transform child in pluginWidgets)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null || rect.parent != page || !used.Add(rect))
                    throw new InvalidOperationException("An owned widget was missing or moved.");
                original.Add(new Row(rect));
            }
            original.Sort((left, right) => left.Index.CompareTo(right.Index));

            GameObject holder = null;
            MakerPageScroll instance = null;
            try
            {
                holder = new GameObject(HolderName, typeof(RectTransform), typeof(LayoutElement));
                holder.SetActive(false);
                RectTransform slotRect = (RectTransform)holder.transform;
                slotRect.SetParent(page, false);
                slotRect.SetAsLastSibling();
                slotRect.anchorMin = new Vector2(0f, 1f);
                slotRect.anchorMax = new Vector2(1f, 1f);
                slotRect.pivot = new Vector2(.5f, 1f);
                slotRect.anchoredPosition = Vector2.zero;
                slotRect.sizeDelta = Vector2.zero;

                // KKAPI's SubCategoryCreator uses the same stock SocksTop
                // scroll view. Clone only inside an inactive, owned holder.
                GameObject clone = Object.Instantiate(source.gameObject, slotRect, false);
                clone.name = "Scroll View";
                ScrollRect nativeScroll = clone.GetComponent<ScrollRect>();
                RectTransform content = nativeScroll == null ? null : nativeScroll.content;
                RectTransform viewport = nativeScroll == null ? null : nativeScroll.viewport;
                Scrollbar vertical = nativeScroll == null ? null : nativeScroll.verticalScrollbar;
                if (content == null || viewport == null || vertical == null ||
                    !content.IsChildOf(clone.transform) || !viewport.IsChildOf(clone.transform) ||
                    !vertical.transform.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Native scroll clone did not remap its references.");

                // Delete copied template rows, never rows from the live page.
                while (content.childCount != 0) Object.DestroyImmediate(content.GetChild(content.childCount - 1).gameObject);
                // Avoid retaining native page-specific behaviour from the copied
                // template. Its Unity UI graphics, masking and scrollbar are kept.
                foreach (MonoBehaviour component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    if (component is ScrollRect || component is Scrollbar || component is Graphic ||
                        component is Mask || component is RectMask2D || component is LayoutGroup ||
                        component is ContentSizeFitter || component is LayoutElement ||
                        component is Selectable) continue;
                    Object.DestroyImmediate(component);
                }
                nativeScroll.onValueChanged = new ScrollRect.ScrollRectEvent();
                nativeScroll.verticalScrollbar = null;
                vertical.onValueChanged = new Scrollbar.ScrollEvent();
                nativeScroll.verticalScrollbar = vertical;
                Scrollbar horizontal = nativeScroll.horizontalScrollbar;
                nativeScroll.horizontalScrollbar = null;
                if (horizontal != null) horizontal.gameObject.SetActive(false);
                nativeScroll.horizontal = false;
                nativeScroll.vertical = true;
                nativeScroll.movementType = ScrollRect.MovementType.Clamped;
                // Always keep the bar visible: AutoHide previously hid it
                // when an inactive Maker tab reported zero content height.
                nativeScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

                RectTransform root = (RectTransform)clone.transform;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
                root.localScale = Vector3.one;
                root.localRotation = Quaternion.identity;

                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = Vector2.zero;
                foreach (LayoutGroup other in content.GetComponents<LayoutGroup>())
                    if (!(other is VerticalLayoutGroup)) Object.DestroyImmediate(other);
                VerticalLayoutGroup inner = content.GetComponent<VerticalLayoutGroup>();
                if (inner == null) inner = content.gameObject.AddComponent<VerticalLayoutGroup>();
                inner.enabled = true;
                // The outer layout already owns the original padding.
                inner.padding = new RectOffset();
                inner.spacing = layout.spacing;
                inner.childAlignment = layout.childAlignment;
                inner.childControlWidth = layout.childControlWidth;
                inner.childControlHeight = layout.childControlHeight;
                inner.childForceExpandWidth = layout.childForceExpandWidth;
                inner.childForceExpandHeight = false;
                ContentSizeFitter fit = content.GetComponent<ContentSizeFitter>();
                if (fit == null) fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.enabled = true;
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                Image contentImage = content.GetComponent<Image>();
                if (contentImage != null) contentImage.raycastTarget = true;
                Image barImage = vertical.GetComponent<Image>();
                if (barImage != null) barImage.raycastTarget = true;

                instance = holder.AddComponent<MakerPageScroll>();
                instance.host = page;
                instance.hostLayout = layout;
                instance.oldForceExpand = layout.childForceExpandHeight;
                instance.slot = slotRect;
                instance.slotLayout = holder.GetComponent<LayoutElement>();
                instance.slotLayout.flexibleWidth = 1f;
                instance.slotLayout.flexibleHeight = 0f;
                instance.scroll = nativeScroll;
                // Stable, bounded extension viewport. Its height must NOT
                // be inferred from the content-sized native host.
                instance.templateHeight = MakerPageScrollMath.ScopedViewportHeight(
                    ((RectTransform)source.transform).rect.height);
                instance.rows.AddRange(original);
                instance.installed = true;
                instance.installedFrame = Time.frameCount;
                // No modification to the native layout's force-expand policy.
                instance.SetHeight(instance.templateHeight);

                foreach (Row row in original) row.Rect.SetParent(content, false);
                // As in KKAPI's scrollable sidebar, a slider's wheel should
                // scroll the page rather than alter a numeric value. Restore
                // exactly the handlers we changed when removing the wrapper.
                foreach (Row row in original)
                    foreach (Slider slider in row.Rect.GetComponentsInChildren<Slider>(true))
                    {
                        ObservableScrollTrigger trigger = slider.GetComponent<ObservableScrollTrigger>();
                        if (trigger == null || !trigger.enabled) continue;
                        instance.wheelHandlers.Add(trigger);
                        trigger.enabled = false;
                    }
                // A copied scrollbar may already be inactive because the
                // Socks template had no overflow when KKAPI built its UI.
                // Keep the owned track visible regardless of current rows.
                vertical.gameObject.SetActive(true);
                clone.SetActive(true);
                holder.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                LayoutRebuilder.MarkLayoutForRebuild(page);
                ResetRaycasts(page);
                Debug.Log("Femboy Mode: scoped native scrollbar installed (" +
                    original.Count + " owned extension widgets; native Maker hierarchy preserved).");
                return instance;
            }
            catch
            {
                if (instance != null && instance.installed) instance.Uninstall();
                else if (holder != null) Object.Destroy(holder);
                throw;
            }
        }

        private static void ResetRaycasts(RectTransform page)
        {
            // Recollect the native image targets after the hierarchy changes.
            for (Transform t = page; t != null; t = t.parent)
            {
                UI_RaycastCtrl ctrl = t.GetComponent<UI_RaycastCtrl>();
                if (ctrl != null) ctrl.Reset();
                if (t.name == "CvsMenuTree") break;
            }
        }

        private void SetHeight(float height)
        {
            appliedHeight = height;
            slotLayout.minHeight = height;
            slotLayout.preferredHeight = height;
            slot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void OnEnable() { resetPosition = true; }

        private void LateUpdate()
        {
            if (!installed || host == null || slot == null || scroll == null || scroll.content == null) return;
            if (resetPosition)
            {
                resetPosition = false;
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.verticalNormalizedPosition = 1f;
            }
            // Runtime fail-open. Never leave a corrupted extension scroll
            // installed when the live Unity canvas reports invalid geometry.
            if (!validated && Time.frameCount - installedFrame >= 3 &&
                host.gameObject.activeInHierarchy)
            {
                validated = true;
                RectTransform viewport = scroll.viewport;
                if (viewport == null || viewport.rect.height < 96f ||
                    viewport.rect.width < 140f)
                {
                    Debug.LogWarning("Femboy Mode: invalid scoped viewport; restoring original extension rows.");
                    Uninstall();
                }
            }
        }

        internal void Uninstall()
        {
            if (!installed) return;
            installed = false;
            // Do not destroy live page rows along with the wrapper.
            gameObject.SetActive(false);
            if (host != null)
            {
                foreach (Row row in rows) row.Restore(host);
                // Any rows appended by another plugin while the page was
                // wrapped also belong to the Maker, not to the scroll clone.
                if (scroll != null && scroll.content != null)
                    while (scroll.content.childCount > 0)
                        scroll.content.GetChild(0).SetParent(host, false);
                if (hostLayout != null) hostLayout.childForceExpandHeight = oldForceExpand;
                foreach (ObservableScrollTrigger trigger in wheelHandlers)
                    if (trigger != null) trigger.enabled = true;
                LayoutRebuilder.MarkLayoutForRebuild(host);
                try { ResetRaycasts(host); }
                catch (Exception ex) { Debug.LogWarning("Maker raycast cache reset failed: " + ex.Message); }
            }
            rows.Clear();
            wheelHandlers.Clear();
            Object.Destroy(gameObject);
        }
    }
}
