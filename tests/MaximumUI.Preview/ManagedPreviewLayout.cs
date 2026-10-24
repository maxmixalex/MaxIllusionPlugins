using System;
using System.Collections.Generic;
using MaximumUI;

// The managed WidgetPainter exposes no native scroll host. This backend expands
// only the production visible row range and applies viewport clipping, while
// keeping all widget composition, layout and control painting in production code.
internal sealed class ManagedPreviewLayout {
    internal readonly List<LayoutNode> Nodes = new List<LayoutNode>();
    internal readonly DrawList Drawing = new DrawList();
    internal int VirtualRowsBuilt;
    internal int VirtualItems;
    internal int ScrollViews;
    internal readonly List<PreviewScrollArea> ScrollAreas = new List<PreviewScrollArea>();
    internal readonly Dictionary<DrawCommand, LayoutNode> CommandOwners = new Dictionary<DrawCommand, LayoutNode>();
    internal readonly Dictionary<DrawCommand, int> CommandLayers = new Dictionary<DrawCommand, int>();
    internal readonly HashSet<string> VisiblePaths = new HashSet<string>(StringComparer.Ordinal);
    internal readonly List<PreviewPopup> Popups = new List<PreviewPopup>();
    internal LayoutNode Root;
    internal int DialogCount { get { return dialogs.Count; } }

    private readonly Dictionary<LayoutNode, MuiRect> clips = new Dictionary<LayoutNode, MuiRect>();
    private readonly Dictionary<LayoutNode, DrawList> painted = new Dictionary<LayoutNode, DrawList>();
    private readonly Dictionary<LayoutNode, DrawList> paintedAfterChildren = new Dictionary<LayoutNode, DrawList>();
    private readonly Dictionary<LayoutNode, PreviewScrollArea> scrollAreasByNode = new Dictionary<LayoutNode, PreviewScrollArea>();
    private readonly Dictionary<ScrollPosition, MuiVec2> sharedScrollRequests = new Dictionary<ScrollPosition, MuiVec2>();
    private readonly Dictionary<ScrollPosition, string> sharedScrollIds = new Dictionary<ScrollPosition, string>();
    private ThemeTokens palette;
    private LayoutEngine layoutEngine;
    private WidgetPainter painter;
    private readonly WidgetPaintState paintState = new WidgetPaintState();
    private PreviewScene scene;
    private readonly List<LayoutNode> dialogs = new List<LayoutNode>();

    internal static ManagedPreviewLayout Create(Widget widget, MuiRect viewport, LayoutMetrics metrics, ThemeTokens palette, PreviewScene scene = null, string rootPath = "0") {
        ManagedPreviewLayout preview = new ManagedPreviewLayout {
            layoutEngine = new LayoutEngine(metrics), painter = new WidgetPainter(palette, metrics), scene = scene, palette = palette
        };
        LayoutNode root = preview.layoutEngine.Layout(widget, viewport, rootPath);
        preview.Root = root;
        preview.ExtractDialogs(root, viewport);
        preview.CollectSharedScrollRequests(root);
        foreach (LayoutNode dialog in preview.dialogs) preview.CollectSharedScrollRequests(dialog);
        preview.Expand(root);
        foreach (LayoutNode dialog in preview.dialogs) preview.Expand(dialog);
        preview.Verify(root, viewport, false);
        foreach (LayoutNode dialog in preview.dialogs) preview.Verify(dialog, viewport, false);
        preview.ResolvePaintState();
        preview.CacheDrawing(root, true);
        foreach (LayoutNode dialog in preview.dialogs) preview.CacheDrawing(dialog, true);
        preview.AppendDrawing(root, 0);
        int layer = 0;
        foreach (LayoutNode dialog in preview.dialogs) {
            // Mirrors native OverlayLayer composition: both kinds of modal
            // dialog render above scroll clipping and dim their host.
            preview.Drawing.FillRect(viewport, palette.Backdrop);
            preview.AppendDrawing(dialog, ++layer);
        }
        preview.FindVisible(root, viewport);
        foreach (LayoutNode dialog in preview.dialogs) preview.FindVisible(dialog, viewport);
        if (scene != null && scene.PopupKind != null) PreviewPopupRenderer.Append(preview, scene, viewport, metrics, palette, ++layer);
        return preview;
    }

    private void ExtractDialogs(LayoutNode parent, MuiRect viewport) {
        for (int i = 0; i < parent.Children.Count; i++) {
            LayoutNode child = parent.Children[i];
            if (child.Widget is Dialog dialog) {
                MuiRect bounds = WindowGeometry.Clamp(new MuiRect(
                    viewport.X + (viewport.Width - dialog.Width) * 0.5f,
                    viewport.Y + (viewport.Height - dialog.Height) * 0.5f,
                    dialog.Width, dialog.Height), viewport.Right, viewport.Bottom, 120f, 80f);
                LayoutNode overlay = layoutEngine.Layout(dialog, bounds, child.Path);
                dialogs.Add(overlay);
                parent.Children.RemoveAt(i--);
                ExtractDialogs(overlay, viewport);
            } else ExtractDialogs(child, viewport);
        }
    }

    private void ResolvePaintState() {
        if (scene != null && (scene.PopupKind == "combo" || scene.PopupKind == "menu")) {
            LayoutNode popup = Nodes.Find(node => Equals(node.Widget.Key, scene.PopupKey));
            if (popup == null) throw new InvalidOperationException("Missing production popup host " + scene.PopupKey);
            paintState.OpenPopupPath = popup.Path;
            paintState.OpenMenuIndex = scene.PopupKind == "menu" ? scene.PopupMenuIndex : -1;
        }
        if (scene == null || string.IsNullOrEmpty(scene.PaintState)) return;
        LayoutNode target = null;
        for (int i = 0; i < Nodes.Count; i++) {
            LayoutNode node = Nodes[i];
            if (!node.Widget.Enabled || node.Rect.Width <= 0f || node.Rect.Height <= 0f) continue;
            if (scene.PaintPath != null && node.Path == scene.PaintPath || scene.PaintKey != null && Equals(node.Widget.Key, scene.PaintKey)) { target = node; break; }
            if (scene.PaintKey == null && target == null &&
                ((scene.PaintState == "focused" && node.Widget is TextField) ||
                (scene.PaintState != "focused" && node.Widget is Button))) target = node;
        }
        if (target == null) throw new InvalidOperationException("No target for " + scene.PaintState + " in " + scene.Slug);
        scene.PaintPath = target.Path;
        if (scene.PaintState == "scrollbar-hovered") paintState.HoveredScrollbarPath = target.Path;
        else if (scene.PaintState == "scrollbar-pressed") { paintState.HoveredScrollbarPath = target.Path; paintState.ActiveScrollbarPath = target.Path; }
        else if (scene.PaintState == "hovered") paintState.HoveredPath = target.Path;
        else if (scene.PaintState == "pressed") { paintState.ActivePath = target.Path; paintState.HoveredPath = target.Path; }
        else if (scene.PaintState == "focused") paintState.FocusedPath = target.Path;
    }

    private void Expand(LayoutNode node) {
        Nodes.Add(node);
        if (node.Widget is VirtualList list) {
            VirtualItems += list.ItemCount;
            float rowHeight = Math.Max(1f, list.RowHeight);
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(node.Rect, list.ItemCount * rowHeight, list.AlwaysShowVertical);
            MuiRect extent = frame.Content;
            MuiRect clip = frame.ContentViewport;
            clips.Add(node, clip);
            float y = RequestedScroll(node, list.ScrollPosition).Y;
            y = frame.ClampY(y);
            PreviewScrollArea area = new PreviewScrollArea(node.Path, Convert.ToString(node.Widget.Key), frame.Bounds, clip, extent, 0f, y, true,
                frame.Viewport, true, false, frame.Vertical);
            AddScrollArea(node, area);
            int first, end;
            ScrollGeometry.VisibleRows(y, clip.Height, rowHeight, list.ItemCount, out first, out end);
            for (int index = first; index < end; index++) {
                Widget row = list.BuildItem == null ? null : list.BuildItem(index);
                if (row == null || row is EmptyWidget) continue;
                node.Children.Add(layoutEngine.Layout(row,
                    new MuiRect(clip.X, clip.Y + index * rowHeight - y, extent.Width, rowHeight),
                    node.Path + ".row" + index));
                VirtualRowsBuilt++;
            }
            area.SetVirtualStats(rowHeight, list.ItemCount, node.Children.Count);
        } else if (node.Widget is ScrollView scroll) {
            ScrollViews++;
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect clip = ScrollGeometry.ContentViewport(node.Rect, extent, scroll.AlwaysShowHorizontal, scroll.AlwaysShowVertical, 16f);
            clips.Add(node, clip);
            MuiVec2 requestedScroll = RequestedScroll(node, scroll.ScrollPosition);
            float x = requestedScroll.X, y = requestedScroll.Y;
            x = Math.Max(0f, Math.Min(x, Math.Max(0f, extent.Width - clip.Width)));
            y = Math.Max(0f, Math.Min(y, Math.Max(0f, extent.Height - clip.Height)));
            AddScrollArea(node, new PreviewScrollArea(node.Path, Convert.ToString(node.Widget.Key), node.Rect, clip, extent, x, y, false,
                node.Rect, false, false, scroll.AlwaysShowVertical, scroll.AlwaysShowHorizontal));
            for (int i = 0; i < node.Children.Count; i++) Offset(node.Children[i], -x, -y);
        } else if (painter.TryGetScrollFrame(node, out ScrollFrameLayout frame)) {
            // Query the production painter's actual filtered rows/tokens/metrics.
            // The painter owns row drawing and chrome for these built-in lists.
            float y = scene != null && scene.ScrollOffsets.TryGetValue(node.Path, out float requested) ? requested : 0f;
            y = frame.ClampY(y);
            paintState.ScrollPositions[node.Path] = new MuiVec2(0f, y);
            clips.Add(node, frame.ContentViewport);
            AddScrollArea(node, new PreviewScrollArea(node.Path, Convert.ToString(node.Widget.Key), frame.Bounds,
                frame.ContentViewport, frame.Content, 0f, y, false, frame.Viewport, true, true, frame.Vertical));
        }
        for (int i = 0; i < node.Children.Count; i++) Expand(node.Children[i]);
    }

    private void AddScrollArea(LayoutNode node, PreviewScrollArea area) {
        ScrollPosition binding = ScrollBinding(node);
        if (binding != null && sharedScrollIds.TryGetValue(binding, out string id)) area.SharedScrollBinding = id;
        ScrollAreas.Add(area);
        scrollAreasByNode.Add(node, area);
    }

    private void CollectSharedScrollRequests(LayoutNode node) {
        ScrollPosition binding = ScrollBinding(node);
        if (binding != null && !sharedScrollIds.ContainsKey(binding)) sharedScrollIds.Add(binding, node.Path);
        if (binding != null && scene != null) {
            bool hasX = scene.HorizontalOffsets.TryGetValue(node.Path, out float x);
            bool hasY = scene.ScrollOffsets.TryGetValue(node.Path, out float y);
            if (hasX || hasY) {
                if (!sharedScrollRequests.TryGetValue(binding, out MuiVec2 requested))
                    requested = new MuiVec2(binding.Position.x, binding.Position.y);
                sharedScrollRequests[binding] = new MuiVec2(hasX ? x : requested.X, hasY ? y : requested.Y);
            }
        }
        foreach (LayoutNode child in node.Children) CollectSharedScrollRequests(child);
    }

    private static ScrollPosition ScrollBinding(LayoutNode node) {
        return node.Widget is VirtualList list ? list.ScrollPosition :
            node.Widget is ScrollView scroll ? scroll.ScrollPosition : null;
    }

    private MuiVec2 RequestedScroll(LayoutNode node, ScrollPosition binding) {
        if (binding != null && sharedScrollRequests.TryGetValue(binding, out MuiVec2 shared)) return shared;
        float x = binding == null ? 0f : binding.Position.x;
        float y = binding == null ? 0f : binding.Position.y;
        if (scene != null) {
            if (scene.HorizontalOffsets.TryGetValue(node.Path, out float requestedX)) x = requestedX;
            if (scene.ScrollOffsets.TryGetValue(node.Path, out float requestedY)) y = requestedY;
        }
        return new MuiVec2(x, y);
    }

    private static void Offset(LayoutNode node, float x, float y) {
        if (x == 0f && y == 0f) return;
        node.Rect = new MuiRect(node.Rect.X + x, node.Rect.Y + y, node.Rect.Width, node.Rect.Height);
        for (int i = 0; i < node.Children.Count; i++) Offset(node.Children[i], x, y);
    }

    private void Verify(LayoutNode node, MuiRect viewport, bool clipped) {
        MuiRect rect = node.Rect;
        if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height) || rect.Width < 0f || rect.Height < 0f)
            throw new InvalidOperationException("Invalid production layout geometry at " + node.Path);
        if (!clipped && rect.Width > 0f && rect.Height > 0f &&
            (rect.X < viewport.X - 0.1f || rect.Y < viewport.Y - 0.1f || rect.Right > viewport.Right + 0.1f || rect.Bottom > viewport.Bottom + 0.1f))
            throw new InvalidOperationException("Production content extends outside its window: " + node.Path + " " + node.Widget.GetType().Name + " " + node.Widget.Key);
        if (node.Widget is VirtualList list && list.ItemCount > 0 && rect.Height < Math.Min(24f, list.RowHeight))
            throw new InvalidOperationException("A nonempty production list has no usable viewport: " + node.Widget.Key);
        for (int i = 0; i < node.Children.Count; i++) Verify(node.Children[i], viewport, clipped || clips.ContainsKey(node));
    }

    private void CacheDrawing(LayoutNode node, bool parentEnabled) {
        // Paint only each node's own commands, retaining its real child geometry
        // for tables/splitters. The production painter explicitly separates
        // overlays that belong after children, such as selected-panel outlines.
        paintState.ParentEnabled = parentEnabled;
        if (scrollAreasByNode.TryGetValue(node, out PreviewScrollArea area)) area.SetColors(palette, parentEnabled && node.Widget.Enabled);
        DrawList before = new DrawList(), after = new DrawList();
        painter.PaintNode(node, paintState, before, after);
        painted.Add(node, before);
        paintedAfterChildren.Add(node, after);
        for (int i = 0; i < node.Children.Count; i++) CacheDrawing(node.Children[i], parentEnabled && node.Widget.Enabled);
    }

    private void AppendDrawing(LayoutNode node, int layer) {
        DrawList own = painted[node];
        for (int i = 0; i < own.Commands.Count; i++) {
            DrawCommand command = own.Commands[i];
            // The managed painter labels unexpanded lists; real factory rows
            // replace that diagnostic text, while its field surface is retained.
            if (node.Widget is VirtualList && command.Type == DrawCommandType.Text) continue;
            Drawing.Commands.Add(command);
            CommandOwners.Add(command, node);
            CommandLayers.Add(command, layer);
        }
        MuiRect clip;
        bool scoped = clips.TryGetValue(node, out clip);
        if (scoped) Drawing.PushClip(clip);
        for (int i = 0; i < node.Children.Count; i++) AppendDrawing(node.Children[i], layer);
        if (scoped) Drawing.PopClip();
        if (scrollAreasByNode.TryGetValue(node, out PreviewScrollArea area) && !area.PainterOwnsChrome) {
            bool hovered = paintState.IsScrollbarHovered(node), pressed = paintState.IsScrollbarActive(node);
            PopupPresentation.DrawScrollbar(Drawing, palette, area.VerticalScrollbar(), hovered, pressed, area.Enabled);
            PopupPresentation.DrawScrollbar(Drawing, palette, area.HorizontalScrollbar(), hovered, pressed, area.Enabled);
            if (area.Framed) Drawing.StrokeRect(area.Bounds, area.BorderColor, ScrollGeometry.FrameBorder);
        }
        foreach (DrawCommand command in paintedAfterChildren[node].Commands) {
            Drawing.Commands.Add(command);
            CommandOwners.Add(command, node);
            CommandLayers.Add(command, layer);
        }
    }

    private void FindVisible(LayoutNode node, MuiRect clip) {
        MuiRect own = Intersect(node.Rect, clip);
        if (scrollAreasByNode.TryGetValue(node, out PreviewScrollArea area)) area.SetVisibleFrame(Intersect(area.Bounds, clip));
        if (own.Width > 0.1f && own.Height > 0.1f && own.Height >= Math.Min(node.Rect.Height, 16f)) VisiblePaths.Add(node.Path);
        if (clips.TryGetValue(node, out MuiRect local)) {
            clip = Intersect(clip, local);
            scrollAreasByNode[node].SetVisible(clip);
        }
        for (int i = 0; i < node.Children.Count; i++) FindVisible(node.Children[i], clip);
    }

    internal static MuiRect Intersect(MuiRect left, MuiRect right) {
        float x = Math.Max(left.X, right.X), y = Math.Max(left.Y, right.Y);
        return new MuiRect(x, y, Math.Max(0f, Math.Min(left.Right, right.Right) - x), Math.Max(0f, Math.Min(left.Bottom, right.Bottom) - y));
    }
}

internal sealed class PreviewScrollArea {
    public string Path { get; private set; }
    public string Key { get; private set; }
    public string SharedScrollBinding { get; internal set; }
    public float ViewHeight { get; private set; }
    public float ContentHeight { get; private set; }
    public float Offset { get; private set; }
    public float VisibleHeight { get; private set; }
    public float VisibleWidth { get; private set; }
    public float VisibleContentLeft { get; private set; }
    public float ViewWidth { get { return Viewport.Width; } }
    public float FullWidth { get { return ContentWidth; } }
    public float HorizontalOffset { get { return OffsetX; } }
    public float MaximumX { get { return Math.Max(0f, ContentWidth - Viewport.Width); } }
    public float VisibleContentStart { get; private set; }
    public bool Virtual { get; private set; }
    public float RowHeight { get; private set; }
    public int ItemCount { get; private set; }
    public int BuiltRows { get; private set; }
    public float Maximum { get { return Math.Max(0f, ContentHeight - ViewHeight); } }
    public bool Framed { get; private set; }
    public bool VerticalBarVisible { get { return VerticalScrollbar().Visible; } }
    public float[] FrameBounds { get { return RectValues(Bounds); } }
    public float[] VisibleFrameBounds { get { return RectValues(visibleFrame); } }
    public float[] ContentBounds { get { return RectValues(Viewport); } }
    public float[] VerticalTrack { get { return RectValues(VerticalScrollbar().Track); } }
    public float[] VerticalThumb { get { return RectValues(VerticalScrollbar().Thumb); } }
    public float[] FrameRgb { get { return ColorValues(BorderColor); } }
    public float[] TrackRgb { get { return ColorValues(trackColor); } }
    internal MuiRect Bounds, Viewport, ScrollHost;
    private MuiRect visibleFrame;
    internal MuiColor BorderColor;
    private MuiColor trackColor;
    internal bool PainterOwnsChrome, Enabled;
    private readonly bool forceVertical, forceHorizontal;
    internal float ContentWidth, OffsetX;
    internal PreviewScrollArea(string path, string key, MuiRect bounds, MuiRect viewport, MuiRect content, float offsetX, float offset, bool isVirtual,
        MuiRect scrollHost, bool framed, bool painterOwnsChrome, bool alwaysVertical, bool alwaysHorizontal = false) {
        Path = path; Key = key; Bounds = bounds; Viewport = viewport; ScrollHost = scrollHost;
        ViewHeight = viewport.Height; ContentHeight = content.Height; ContentWidth = content.Width;
        OffsetX = offsetX; Offset = offset; Virtual = isVirtual; Framed = framed; PainterOwnsChrome = painterOwnsChrome;
        forceVertical = alwaysVertical; forceHorizontal = alwaysHorizontal;
    }
    internal ScrollbarLayout VerticalScrollbar() {
        MuiRect gutter = new MuiRect(Viewport.Right, Viewport.Y, Math.Max(0f, ScrollHost.Right - Viewport.Right), Viewport.Height);
        return ScrollbarGeometry.Vertical(gutter, ContentHeight, ViewHeight, Offset, forceVertical);
    }
    internal ScrollbarLayout HorizontalScrollbar() {
        MuiRect gutter = new MuiRect(Viewport.X, Viewport.Bottom, Viewport.Width, Math.Max(0f, ScrollHost.Bottom - Viewport.Bottom));
        return ScrollbarGeometry.Horizontal(gutter, ContentWidth, Viewport.Width, OffsetX, forceHorizontal);
    }
    internal void SetColors(ThemeTokens palette, bool enabled) {
        Enabled = enabled; BorderColor = enabled ? palette.ControlBorder : palette.Border;
        trackColor = ScrollbarGeometry.TrackColor(palette);
    }
    internal void SetVisibleFrame(MuiRect visible) { visibleFrame = visible; }
    private static float[] RectValues(MuiRect rect) { return new[] { rect.X, rect.Y, rect.Width, rect.Height }; }
    private static float[] ColorValues(MuiColor color) { return new[] { color.R, color.G, color.B }; }
    internal void SetVisible(MuiRect visible) {
        VisibleHeight = visible.Width > 0f ? visible.Height : 0f;
        VisibleContentStart = Offset + Math.Max(0f, visible.Y - Viewport.Y);
        VisibleWidth = visible.Height > 0f ? visible.Width : 0f;
        VisibleContentLeft = OffsetX + Math.Max(0f, visible.X - Viewport.X);
    }
    internal void SetVirtualStats(float rowHeight, int itemCount, int builtRows) {
        RowHeight = rowHeight; ItemCount = itemCount; BuiltRows = builtRows;
    }
}
