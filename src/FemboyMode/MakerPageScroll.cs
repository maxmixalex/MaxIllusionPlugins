using System;
using System.Collections.Generic;
using ChaCustom;
using KKAPI.Maker;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx.Triggers;
using Object = UnityEngine.Object;

namespace FemboyMode
{
    /// <summary>
    /// One scroll region for the original Maker Body > All rows AND the
    /// Femboy extension rows. Never reparent, rename, replace or deactivate
    /// a native row: native scripts and KKAPI address these direct children.
    ///
    /// Instead, retain the existing VerticalLayoutGroup, scroll its padding,
    /// clip to its own fixed viewport and drive it with a copied native
    /// scrollbar. This avoids the 0.5.6-0.5.9 nested ScrollRect regressions.
    /// </summary>
    internal sealed class MakerPageScroll : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string TemplatePath = "03_ClothesTop/tglSocks/SocksTop/Scroll View";
        private const float MinimumViewport = 160f;
        private const float EdgeMarginPx = 42f;

        private RectTransform page;
        private VerticalLayoutGroup layout;
        private ContentSizeFitter originalFitter;
        private RectMask2D mask;
        private Mask stencilMask;
        private Image stencilImage;
        private bool addedMask, addedStencilMask, addedStencilImage;
        private bool originalStencilMaskEnabled, originalShowStencilGraphic;
        private readonly Dictionary<MaskableGraphic, bool> nativeGraphicMaskability =
            new Dictionary<MaskableGraphic, bool>();
        private bool originalMaskEnabled, originalFitterEnabled, originalForceExpand;
        private TextAnchor originalAlignment;
        private int padLeft, padRight, padTop, padBottom;
        private Vector2 originalSize, originalPosition;
        private Scrollbar scrollbar;
        private GameObject ownedBar;
        private RectTransform scrollbarParent;
        private float nativeBarWidth, nativeBarGap;
        private readonly List<ObservableScrollTrigger> wheelTriggers = new List<ObservableScrollTrigger>();
        private readonly List<Transform> nativeRows = new List<Transform>();
        private readonly Vector3[] corners = new Vector3[4];
        private bool installed, updatingBar, validated;
        private float viewportHeight, scrollOffset, maximumOffset, dragOffset, dragStartY, nextRefresh;
        private float nativeScrollSensitivity;
        private int lastScreenWidth, lastScreenHeight, lastChildCount, installedFrame;
        private int wheelHandledFrame = -1;

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

        private static bool AlreadyScrollable(RectTransform candidate)
        {
            for (Transform t = candidate; t != null; t = t.parent)
            {
                ScrollRect existing = t.GetComponent<ScrollRect>();
                if (existing != null && existing.vertical && existing.content != null &&
                    (existing.content == candidate || candidate.IsChildOf(existing.content)))
                    return true;
            }
            return false;
        }

        internal static MakerPageScroll Install(Transform anchor, IList<Transform> ownedWidgets)
        {
            RectTransform host = anchor == null ? null : anchor.parent as RectTransform;
            if (host == null) return null;
            Transform menu = FindMenu(host);
            if (menu == null) throw new InvalidOperationException("Body > All hierarchy is not verified.");
            if (AlreadyScrollable(host)) return null;
            if (host.GetComponent<MakerPageScroll>() != null) return null;

            VerticalLayoutGroup vertical = host.GetComponent<VerticalLayoutGroup>();
            if (vertical == null || !vertical.enabled)
                throw new InvalidOperationException("Body > All has no active VerticalLayoutGroup.");

            if (ownedWidgets == null || ownedWidgets.Count != 24)
                throw new InvalidOperationException("Femboy Maker controls are not fully registered.");
            var owned = new HashSet<Transform>();
            foreach (Transform widget in ownedWidgets)
                if (widget == null || widget.parent != host || !owned.Add(widget))
                    throw new InvalidOperationException("One of the registered Femboy controls moved.");
            if (owned.Contains(anchor))
                throw new InvalidOperationException("The native marker must not be treated as a scroll replacement.");

            Transform template = menu.Find(TemplatePath);
            ScrollRect source = template == null ? null : template.GetComponent<ScrollRect>();
            if (source == null || source.verticalScrollbar == null)
                throw new InvalidOperationException("Stock Maker scrollbar artwork is unavailable.");
            if (float.IsNaN(source.scrollSensitivity) ||
                float.IsInfinity(source.scrollSensitivity) || source.scrollSensitivity < 0f)
                throw new InvalidOperationException("Stock Maker ScrollRect has invalid scroll sensitivity.");

            MakerPageScroll instance = null;
            try
            {
                instance = host.gameObject.AddComponent<MakerPageScroll>();
                instance.page = host;
                instance.layout = vertical;
                instance.scrollbarParent = host.parent as RectTransform;
                if (instance.scrollbarParent == null)
                    throw new InvalidOperationException("Native Body All tab has no scrollbar sibling parent.");
                // Inherit Koikatsu's *serialized native ScrollRect setting*.
                // Do not substitute an invented 112-unit wheel step or create
                // another user preference for what should be stock behavior.
                instance.nativeScrollSensitivity = source.scrollSensitivity;
                instance.originalFitter = host.GetComponent<ContentSizeFitter>();
                instance.originalFitterEnabled = instance.originalFitter != null && instance.originalFitter.enabled;
                instance.originalForceExpand = vertical.childForceExpandHeight;
                instance.originalAlignment = vertical.childAlignment;
                instance.padLeft = vertical.padding.left;
                instance.padRight = vertical.padding.right;
                instance.padTop = vertical.padding.top;
                instance.padBottom = vertical.padding.bottom;
                instance.originalSize = host.sizeDelta;
                instance.originalPosition = host.anchoredPosition;
                instance.mask = host.GetComponent<RectMask2D>();
                instance.addedMask = instance.mask == null;
                instance.originalMaskEnabled = instance.mask != null && instance.mask.enabled;
                foreach (Transform child in host)
                    if (!owned.Contains(child)) instance.nativeRows.Add(child);
                if (!instance.nativeRows.Contains(anchor))
                    throw new InvalidOperationException("The original marker is not a native child.");

                // Clone ONLY the stock scrollbar, not its Scroll View, content,
                // masking panel, or scripts. All native controls stay in host.
                // The native SocksTop Scroll View places the scrollbar
                // OUTSIDE its clipped Viewport. Mirror that hierarchy: the
                // owned scrollbar is a sibling of AllTop, never its child.
                GameObject barObject = Object.Instantiate(source.verticalScrollbar.gameObject,
                    instance.scrollbarParent, false);
                instance.ownedBar = barObject;
                barObject.name = "FemboyMode.BodyAllScrollbar";
                var barRect = barObject.transform as RectTransform;
                if (barRect == null) throw new InvalidOperationException("Native scrollbar has no RectTransform.");
                LayoutElement barLayout = barObject.GetComponent<LayoutElement>();
                if (barLayout == null) barLayout = barObject.AddComponent<LayoutElement>();
                barLayout.ignoreLayout = true;
                RectTransform stockRect = source.verticalScrollbar.transform as RectTransform;
                float scaleRatio = stockRect == null ? 1f :
                    Mathf.Abs(stockRect.lossyScale.x) / Mathf.Max(.001f, Mathf.Abs(host.lossyScale.x));
                float stockWidth = MakerPageScrollMath.NativeScrollbarWidth(
                    stockRect == null ? float.NaN : stockRect.rect.width,
                    stockRect == null ? float.NaN : stockRect.sizeDelta.x,
                    scaleRatio);
                instance.nativeBarWidth = stockWidth;
                float originalGap = float.NaN;
                if (stockRect != null && source.viewport != null)
                {
                    // Use the native ScrollRect's own gap between its
                    // Viewport right edge and Scrollbar left edge.
                    source.viewport.GetWorldCorners(instance.corners);
                    float right = source.transform.InverseTransformPoint(
                        instance.corners[2]).x;
                    stockRect.GetWorldCorners(instance.corners);
                    float left = source.transform.InverseTransformPoint(
                        instance.corners[0]).x;
                    originalGap = (left - right) *
                        Mathf.Abs(source.transform.lossyScale.x) /
                        Mathf.Max(.001f, Mathf.Abs(host.lossyScale.x));
                }
                instance.nativeBarGap = MakerPageScrollMath.OutsideScrollbarGap(originalGap);
                barRect.anchorMin = new Vector2(0f, 1f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 1f);
                barRect.localScale = Vector3.one;
                barRect.localRotation = Quaternion.identity;
                // Do not reserve a scrollbar-width gutter *inside* the
                // original vertical layout: it must retain native width.
                vertical.padding.right = instance.padRight;
                Debug.Log("Femboy Mode: copied stock Maker scrollbar width " +
                    stockWidth.ToString("0.0") + " UI units; original rect=" +
                    (stockRect == null ? "missing" : stockRect.rect.width.ToString("0.0")) +
                    "; outside gap=" + instance.nativeBarGap.ToString("0.0") +
                    "; native ScrollRect.scrollSensitivity=" +
                    instance.nativeScrollSensitivity.ToString("0.###"));
                instance.scrollbar = barObject.GetComponent<Scrollbar>();
                if (instance.scrollbar == null || instance.scrollbar.handleRect == null)
                    throw new InvalidOperationException("Cloned native scrollbar is missing a handle.");
                instance.scrollbar.direction = Scrollbar.Direction.BottomToTop;
                instance.scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
                instance.scrollbar.onValueChanged.AddListener(instance.OnScrollbarChanged);
                barObject.SetActive(true);

                // Preserve native row objects AND parent hierarchy. Resize only
                // their existing layout root, and clip its overflowing children.
                if (instance.addedMask) instance.mask = host.gameObject.AddComponent<RectMask2D>();
                else instance.mask.enabled = true;
                // RectMask2D alone did not clip certain original Maker
                // labels / number fields in RX18 (reported 2026-11-26).
                // Add a native stencil Mask + mask graphic to the same
                // unchanged AllTop; all MaskableGraphics use the stencil
                // even when their shader ignores UnityUIClipRect.
                instance.stencilImage = host.GetComponent<Image>();
                instance.addedStencilImage = instance.stencilImage == null;
                if (instance.addedStencilImage)
                {
                    instance.stencilImage = host.gameObject.AddComponent<Image>();
                    instance.stencilImage.color = Color.white;
                    instance.stencilImage.raycastTarget = false;
                }
                instance.stencilMask = host.GetComponent<Mask>();
                instance.addedStencilMask = instance.stencilMask == null;
                if (!instance.addedStencilMask)
                {
                    instance.originalStencilMaskEnabled = instance.stencilMask.enabled;
                    instance.originalShowStencilGraphic = instance.stencilMask.showMaskGraphic;
                }
                else instance.stencilMask = host.gameObject.AddComponent<Mask>();
                if (instance.addedStencilMask)
                    instance.stencilMask.showMaskGraphic = !instance.addedStencilImage;
                else instance.stencilMask.showMaskGraphic =
                    instance.originalShowStencilGraphic;
                instance.stencilMask.enabled = true;
                instance.EnsureNativeTextMaskability();
                if (instance.originalFitter != null) instance.originalFitter.enabled = false;
                vertical.childAlignment = TextAnchor.UpperLeft;
                vertical.childForceExpandHeight = false;
                 instance.installed = true;
                instance.installedFrame = Time.frameCount;
                instance.SetViewportHeight(instance.MeasureViewportHeight());
                instance.PositionExternalScrollbar();
                instance.DisableNativeWheelHandlers();
                instance.RecalculateContent();
                instance.ResetRaycasts();
                Debug.Log("Femboy Mode: unified Body > All scrolling installed; " +
                    instance.nativeRows.Count + " original rows and " + ownedWidgets.Count +
                    " owned controls retain native parent; scrollbar outside viewport=" +
                    (instance.scrollbarParent == host.parent) +
                    "; stencil clipping=" + (instance.stencilMask != null &&
                    instance.stencilMask.enabled) +
                    "; native graphics forced maskable=" + instance.nativeGraphicMaskability.Count +
                    "; viewport=" + instance.viewportHeight.ToString("0") +
                    "; contentRange=" + instance.maximumOffset.ToString("0") + ".");
                return instance;
            }
            catch
            {
                if (instance != null) instance.Uninstall();
                throw;
            }
        }

        private float MeasureViewportHeight()
        {
            if (page == null) return 520f;
            Canvas canvas = page.GetComponentInParent<Canvas>();
            float scale = canvas == null || canvas.rootCanvas == null ?
                1f : Mathf.Max(.25f, canvas.rootCanvas.scaleFactor);
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ?
                null : canvas.worldCamera;
            Vector3 top = page.TransformPoint(new Vector3(page.rect.xMin, page.rect.yMax, 0f));
            float topPixels = RectTransformUtility.WorldToScreenPoint(camera, top).y;
            return MakerPageScrollMath.ViewportFromScreen(
                topPixels, Screen.height, scale, EdgeMarginPx);
        }

        private void SetViewportHeight(float height)
        {
            if (page == null) return;
            viewportHeight = Mathf.Clamp(height, MinimumViewport, 900f);
            Vector3 topBefore = page.TransformPoint(new Vector3(page.rect.xMin, page.rect.yMax, 0f));
            page.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
            Vector3 topAfter = page.TransformPoint(new Vector3(page.rect.xMin, page.rect.yMax, 0f));
            page.position += topBefore - topAfter;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            PositionExternalScrollbar();
        }

        private void EnsureNativeTextMaskability()
        {
            if (page == null || stencilMask == null) return;
            foreach (MaskableGraphic graphic in page.GetComponentsInChildren<MaskableGraphic>(true))
            {
                if (graphic == null || graphic == stencilImage || graphic.maskable) continue;
                if (!nativeGraphicMaskability.ContainsKey(graphic))
                    nativeGraphicMaskability.Add(graphic, false);
                graphic.maskable = true;
                graphic.SetMaterialDirty();
            }
        }

        private bool PageVisiblySelected()
        {
            if (page == null || !page.gameObject.activeInHierarchy) return false;
            // The scrollbar is a sibling of AllTop, outside its CanvasGroup.
            // The original tglAll toggle must explicitly control its visibility.
            Toggle tab = page.parent == null ? null : page.parent.GetComponent<Toggle>();
            if (tab != null && !tab.isOn) return false;
            for (Transform t = page; t != null; t = t.parent)
            {
                CanvasGroup group = t.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= .01f) return false;
                if (t.name == "CvsMenuTree") break;
            }
            return true;
        }

        private void PositionExternalScrollbar()
        {
            if (scrollbar == null || scrollbarParent == null || page == null) return;
            bool shouldShow = PageVisiblySelected();
            if (ownedBar != null && ownedBar.activeSelf != shouldShow)
                ownedBar.SetActive(shouldShow);
            if (!shouldShow) return;
            RectTransform bar = scrollbar.transform as RectTransform;
            if (bar == null) return;
            float ratioX = Mathf.Abs(page.lossyScale.x) /
                Mathf.Max(.001f, Mathf.Abs(scrollbarParent.lossyScale.x));
            float ratioY = Mathf.Abs(page.lossyScale.y) /
                Mathf.Max(.001f, Mathf.Abs(scrollbarParent.lossyScale.y));
            bar.sizeDelta = new Vector2(nativeBarWidth * ratioX,
                viewportHeight * ratioY);
            bar.rotation = page.rotation;
            // Top-left of the stock bar is outside the original content
            // right edge. Because this is a sibling of AllTop, it cannot be
            // cut off by the AllTop RectMask2D/stencil Mask.
            bar.position = page.TransformPoint(new Vector3(
                page.rect.xMax + nativeBarGap, page.rect.yMax, 0f));
        }

        private void DisableNativeWheelHandlers()
        {
            // Sliders keep their drag/keyboard interaction. Wheel events
            // bubble up to this one shared scroll region instead of changing
            // arbitrary numeric values while the user scrolls.
            foreach (Slider slider in page.GetComponentsInChildren<Slider>(true))
            {
                ObservableScrollTrigger trigger = slider.GetComponent<ObservableScrollTrigger>();
                if (trigger == null || !trigger.enabled) continue;
                wheelTriggers.Add(trigger);
                trigger.enabled = false;
            }
        }

        private void RecalculateContent()
        {
            if (!installed || page == null || layout == null) return;
            // Leave the scroll padding in place while measuring. Previously
            // resetting it to zero every 0.25s created intermittent jumps
            // during user wheel input. Preferred height includes the current
            // negative top padding; restore the saved offset numerically.
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            EnsureNativeTextMaskability();
            float preferred = LayoutUtility.GetPreferredHeight(page) +
                Mathf.RoundToInt(scrollOffset);
            if (float.IsNaN(preferred) || float.IsInfinity(preferred) ||
                preferred < 0f || preferred > 100000f)
            {
                Debug.LogWarning("Femboy Mode: invalid native content height; restoring layout.");
                Uninstall();
                return;
            }
            maximumOffset = MakerPageScrollMath.MaxOffset(preferred, viewportHeight);
            lastChildCount = page.childCount;
            SetOffset(scrollOffset);
        }

        private void SetOffset(float offset)
        {
            if (!installed || layout == null) return;
            scrollOffset = MakerPageScrollMath.ClampOffset(offset, maximumOffset);
            layout.padding.top = padTop - Mathf.RoundToInt(scrollOffset);
            LayoutRebuilder.MarkLayoutForRebuild(page);
            if (scrollbar == null) return;
            updatingBar = true;
            try
            {
                scrollbar.size = MakerPageScrollMath.ScrollbarSize(viewportHeight, maximumOffset);
                scrollbar.value = MakerPageScrollMath.ScrollbarValue(scrollOffset, maximumOffset);
            }
            finally { updatingBar = false; }
        }

        private void OnScrollbarChanged(float value)
        {
            if (!updatingBar) SetOffset(MakerPageScrollMath.FromScrollbar(value, maximumOffset));
        }

        private void OnEnable()
        {
            if (!installed) return;
            // Body > All is normally inactive when Maker first loads. Its
            // actual screen bounds become trustworthy after activation.
            SetViewportHeight(MeasureViewportHeight());
            nextRefresh = 0f;
            scrollOffset = 0f;
            PositionExternalScrollbar();
        }

        private void OnDisable()
        {
            if (ownedBar != null) ownedBar.SetActive(false);
        }

        private bool NativeRowsRemainVisible()
        {
            if (page == null) return false;
            int candidates = 0, visible = 0;
            Rect region = page.rect;
            foreach (Transform native in nativeRows)
            {
                if (native == null || native.parent != page) return false;
                if (!native.gameObject.activeInHierarchy) continue;
                RectTransform rt = native as RectTransform;
                if (rt == null || rt.rect.height < 1f || rt.rect.width < 1f) continue;
                LayoutElement element = rt.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                candidates++;
                rt.GetWorldCorners(corners);
                float xmin = float.PositiveInfinity, xmax = float.NegativeInfinity;
                float ymin = float.PositiveInfinity, ymax = float.NegativeInfinity;
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = page.InverseTransformPoint(corner);
                    xmin = Mathf.Min(xmin, local.x); xmax = Mathf.Max(xmax, local.x);
                    ymin = Mathf.Min(ymin, local.y); ymax = Mathf.Max(ymax, local.y);
                }
                if (MakerPageScrollMath.Intersects(xmin, xmax, region.xMin, region.xMax) &&
                    MakerPageScrollMath.Intersects(ymin, ymax, region.yMin, region.yMax))
                    visible++;
            }
            // A page legitimately may have no active rows during tab rebuild.
            return candidates == 0 || visible > 0;
        }

        private void LateUpdate()
        {
            if (!installed || page == null || !page.gameObject.activeInHierarchy) return;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                SetViewportHeight(MeasureViewportHeight());
            PositionExternalScrollbar();
            if (Time.unscaledTime >= nextRefresh || page.childCount != lastChildCount)
            {
                nextRefresh = Time.unscaledTime + .25f;
                RecalculateContent();
            }
            // Native Maker sliders can consume UI scroll events; raw wheel
            // polling in this exact viewport is a narrowly scoped fallback.
            PollWheelOnNativeMaker();
            if (!validated && Time.frameCount > installedFrame + 2)
            {
                validated = true;
                if (page.rect.height < MinimumViewport || page.rect.width < 120f ||
                    scrollbar == null || scrollbar.handleRect == null ||
                    scrollbar.transform.parent != scrollbarParent ||
                    stencilMask == null || !stencilMask.enabled ||
                    !NativeRowsRemainVisible())
                {
                    Debug.LogWarning("Femboy Mode: native Maker rows were clipped or page bounds invalid; " +
                        "restoring all original page layout and input without touching any native row.");
                    Uninstall();
                }
            }
        }

        private void ScrollByWheel(float delta)
        {
            if (!installed || page == null || !page.gameObject.activeInHierarchy ||
                wheelHandledFrame == Time.frameCount || maximumOffset <= 0f) return;
            float distance = MakerPageScrollMath.NativeWheelDistance(
                delta, nativeScrollSensitivity);
            if (Mathf.Abs(distance) <= .0001f) return;
            wheelHandledFrame = Time.frameCount;
            SetOffset(scrollOffset - distance);
            // Apply on this wheel frame, not one or two frames later.
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
        }

        private void PollWheelOnNativeMaker()
        {
            if (!installed || !MakerAPI.InsideAndLoaded || !MakerAPI.IsInterfaceVisible() ||
                page == null || !page.gameObject.activeInHierarchy) return;
            Vector2 wheel = Input.mouseScrollDelta;
            if (Mathf.Abs(wheel.y) <= .0001f) return;
            Canvas canvas = page.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                page, Input.mousePosition, camera))
                ScrollByWheel(wheel.y);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!installed || eventData == null) return;
            ScrollByWheel(eventData.scrollDelta.y);
            eventData.Use();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            dragStartY = eventData.position.y;
            dragOffset = scrollOffset;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!installed || eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            Canvas canvas = page.GetComponentInParent<Canvas>();
            float scale = canvas == null || canvas.rootCanvas == null ?
                1f : Mathf.Max(.25f, canvas.rootCanvas.scaleFactor);
            SetOffset(dragOffset + (eventData.position.y - dragStartY) / scale);
        }

        public void OnEndDrag(PointerEventData eventData) { }

        private void ResetRaycasts()
        {
            for (Transform t = page; t != null; t = t.parent)
            {
                UI_RaycastCtrl ctrl = t.GetComponent<UI_RaycastCtrl>();
                if (ctrl != null) ctrl.Reset();
                if (t.name == "CvsMenuTree") break;
            }
        }

        internal void Uninstall()
        {
            if (!installed && page == null) { Object.Destroy(this); return; }
            installed = false;
            foreach (ObservableScrollTrigger trigger in wheelTriggers)
                if (trigger != null) trigger.enabled = true;
            wheelTriggers.Clear();
            if (scrollbar != null)
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
            if (ownedBar != null) Object.Destroy(ownedBar);
            ownedBar = null;
            scrollbar = null;
            foreach (var original in nativeGraphicMaskability)
                if (original.Key != null)
                {
                    original.Key.maskable = original.Value;
                    original.Key.SetMaterialDirty();
                }
            nativeGraphicMaskability.Clear();
            if (page != null)
            {
                if (layout != null)
                {
                    layout.padding.left = padLeft;
                    layout.padding.right = padRight;
                    layout.padding.top = padTop;
                    layout.padding.bottom = padBottom;
                    layout.childAlignment = originalAlignment;
                    layout.childForceExpandHeight = originalForceExpand;
                }
                page.sizeDelta = originalSize;
                page.anchoredPosition = originalPosition;
                if (originalFitter != null) originalFitter.enabled = originalFitterEnabled;
                if (stencilMask != null)
                {
                    if (addedStencilMask) Object.Destroy(stencilMask);
                    else
                    {
                        stencilMask.enabled = originalStencilMaskEnabled;
                        stencilMask.showMaskGraphic = originalShowStencilGraphic;
                    }
                }
                if (addedStencilImage && stencilImage != null)
                    Object.Destroy(stencilImage);
                if (mask != null)
                {
                    if (addedMask) Object.Destroy(mask);
                    else mask.enabled = originalMaskEnabled;
                }
                LayoutRebuilder.MarkLayoutForRebuild(page);
                try { ResetRaycasts(); }
                catch (Exception ex) { Debug.LogWarning("Maker raycast restore failed: " + ex.Message); }
            }
            Object.Destroy(this);
        }
    }
}
