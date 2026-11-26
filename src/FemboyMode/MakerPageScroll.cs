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
    /// Whole-page Maker scrolling using an existing game-native Scroll View.
    /// Native controls keep their original instances and event handlers.
    /// </summary>
    internal sealed class MakerPageScroll : MonoBehaviour
    {
        private const string TemplatePath = "03_ClothesTop/tglSocks/SocksTop/Scroll View";
        private const string HolderName = "MaxMixAlex.NativeMakerPageScroll";
        private readonly List<Row> rows = new List<Row>();
        private readonly List<ObservableScrollTrigger> wheelHandlers = new List<ObservableScrollTrigger>();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform host, slot;
        private VerticalLayoutGroup hostLayout;
        private LayoutElement slotLayout;
        private ScrollRect scroll;
        private bool oldForceExpand, installed, resetPosition = true;
        private bool missingStableViewportReported;
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

        internal static MakerPageScroll Install(Transform anchor)
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

            List<Row> original = new List<Row>();
            foreach (Transform child in page)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null) throw new InvalidOperationException("Unsupported child in native page.");
                original.Add(new Row(rect));
            }
            if (original.Count == 0) return null;

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
                nativeScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

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
                instance.templateHeight = MakerPageScrollMath.ResolveHeight(
                    float.NaN, float.NaN, ((RectTransform)source.transform).rect.height, 0f);
                instance.rows.AddRange(original);
                instance.installed = true;
                layout.childForceExpandHeight = false;
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
                clone.SetActive(true);
                holder.SetActive(true);
                LayoutRebuilder.MarkLayoutForRebuild(page);
                ResetRaycasts(page);
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
            slot.GetWorldCorners(corners);
            float top = host.InverseTransformPoint(corners[1]).y;
            float bottom = float.NegativeInfinity;
            // The host is itself sized by the scroll slot. Treating the
            // host as a viewport creates a height = height - margin feedback
            // loop every frame, ultimately collapsing the entire Maker page.
            // Only use an actual stable clipping ancestor outside that host.
            for (Transform t = host.parent; t != null; t = t.parent)
            {
                RectTransform rect = t as RectTransform;
                RectMask2D rectMask = t.GetComponent<RectMask2D>();
                Mask mask = t.GetComponent<Mask>();
                bool activeClip = (rectMask != null && rectMask.enabled) ||
                    (mask != null && mask.enabled);
                ContentSizeFitter fitter = t.GetComponent<ContentSizeFitter>();
                bool contentDriven = (fitter != null && fitter.enabled &&
                    fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained) ||
                    t.GetComponent<LayoutGroup>() != null;
                if (rect != null && MakerPageScrollMath.IsStableViewport(
                    activeClip, contentDriven, rect.rect.height))
                {
                    rect.GetWorldCorners(corners);
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int i = 0; i < corners.Length; i++)
                    {
                        float y = host.InverseTransformPoint(corners[i]).y;
                        min = Mathf.Min(min, y);
                        max = Mathf.Max(max, y);
                    }
                    if (max >= top - 2f && min < top) bottom = Mathf.Max(bottom, min);
                }
                Canvas canvas = t.GetComponent<Canvas>();
                if (canvas != null && canvas.isRootCanvas) break;
            }
            if (float.IsNegativeInfinity(bottom) && !missingStableViewportReported)
            {
                missingStableViewportReported = true;
                Debug.LogWarning("Femboy Mode: Maker Body > All has no stable masked viewport; " +
                    "keeping the native scroll template height instead of measuring itself.");
            }
            float height = MakerPageScrollMath.ResolveHeight(top, bottom, templateHeight, 8f);
            if (Mathf.Abs(height - appliedHeight) > 1f)
            {
                SetHeight(height);
                LayoutRebuilder.MarkLayoutForRebuild(host);
            }
            if (resetPosition)
            {
                resetPosition = false;
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.verticalNormalizedPosition = 1f;
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
                transform.SetAsLastSibling();
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
