using System;
using System.Collections.Generic;
using ChaCustom;
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
        private const float ScrollStep = 36f;

        private RectTransform page;
        private VerticalLayoutGroup layout;
        private ContentSizeFitter originalFitter;
        private RectMask2D mask;
        private bool addedMask;
        private bool originalMaskEnabled, originalFitterEnabled, originalForceExpand;
        private TextAnchor originalAlignment;
        private int padLeft, padRight, padTop, padBottom;
        private Vector2 originalSize, originalPosition;
        private Scrollbar scrollbar;
        private GameObject ownedBar;
        private readonly List<ObservableScrollTrigger> wheelTriggers = new List<ObservableScrollTrigger>();
        private readonly List<Transform> nativeRows = new List<Transform>();
        private readonly Vector3[] corners = new Vector3[4];
        private bool installed, updatingBar, validated;
        private float viewportHeight, scrollOffset, maximumOffset, dragOffset, dragStartY, nextRefresh;
        private int lastScreenWidth, lastScreenHeight, lastChildCount, installedFrame;

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

            MakerPageScroll instance = null;
            try
            {
                instance = host.gameObject.AddComponent<MakerPageScroll>();
                instance.page = host;
                instance.layout = vertical;
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
                GameObject barObject = Object.Instantiate(source.verticalScrollbar.gameObject, host, false);
                instance.ownedBar = barObject;
                barObject.name = "FemboyMode.BodyAllScrollbar";
                var barRect = barObject.transform as RectTransform;
                if (barRect == null) throw new InvalidOperationException("Native scrollbar has no RectTransform.");
                LayoutElement barLayout = barObject.GetComponent<LayoutElement>();
                if (barLayout == null) barLayout = barObject.AddComponent<LayoutElement>();
                barLayout.ignoreLayout = true;
                barRect.anchorMin = new Vector2(1f, 0f);
                barRect.anchorMax = new Vector2(1f, 1f);
                barRect.pivot = new Vector2(1f, 1f);
                barRect.offsetMin = new Vector2(-14f, 0f);
                barRect.offsetMax = Vector2.zero;
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
                if (instance.originalFitter != null) instance.originalFitter.enabled = false;
                vertical.childAlignment = TextAnchor.UpperLeft;
                vertical.childForceExpandHeight = false;
                vertical.padding.right = Math.Max(instance.padRight, 17);
                instance.installed = true;
                instance.installedFrame = Time.frameCount;
                instance.SetViewportHeight(instance.MeasureViewportHeight());
                instance.DisableNativeWheelHandlers();
                instance.RecalculateContent();
                instance.ResetRaycasts();
                Debug.Log("Femboy Mode: unified Body > All scrolling installed; " +
                    instance.nativeRows.Count + " original rows and " + ownedWidgets.Count +
                    " owned controls retain the same native parent. Viewport=" +
                    instance.viewportHeight.ToString("0") + " contentRange=" +
                    instance.maximumOffset.ToString("0") + ".");
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
            // Measure without the negative scroll padding. No children move to
            // a different parent; Unity's native layout computes their height.
            layout.padding.top = padTop;
            layout.padding.bottom = padBottom;
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            float preferred = LayoutUtility.GetPreferredHeight(page);
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
        }

        private void LateUpdate()
        {
            if (!installed || page == null || !page.gameObject.activeInHierarchy) return;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                SetViewportHeight(MeasureViewportHeight());
            if (Time.unscaledTime >= nextRefresh || page.childCount != lastChildCount)
            {
                nextRefresh = Time.unscaledTime + .25f;
                RecalculateContent();
            }
            if (!validated && Time.frameCount > installedFrame + 2)
            {
                validated = true;
                if (page.rect.height < MinimumViewport || page.rect.width < 120f ||
                    scrollbar == null || scrollbar.handleRect == null ||
                    scrollbar.transform.parent != page)
                {
                    Debug.LogWarning("Femboy Mode: unified viewport validation failed; restoring original page.");
                    Uninstall();
                }
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!installed || eventData == null) return;
            SetOffset(scrollOffset - eventData.scrollDelta.y * ScrollStep);
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
