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
    private readonly Dictionary<LayoutNode, PreviewScrollArea> scrollAreasByNode = new Dictionary<LayoutNode, PreviewScrollArea>();
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
        if (scene.PaintState == "hovered") paintState.HoveredPath = target.Path;
        else if (scene.PaintState == "pressed") { paintState.ActivePath = target.Path; paintState.HoveredPath = target.Path; }
        else if (scene.PaintState == "focused") paintState.FocusedPath = target.Path;
    }

    private void Expand(LayoutNode node) {
        Nodes.Add(node);
        if (node.Widget is VirtualList list) {
            VirtualItems += list.ItemCount;
            float rowHeight = Math.Max(1f, list.RowHeight);
            float totalHeight = Math.Max(node.Rect.Height, list.ItemCount * rowHeight);
            MuiRect extent = new MuiRect(0f, 0f, Math.Max(1f, node.Rect.Width - 16f), totalHeight);
            MuiRect clip = ScrollGeometry.ContentViewport(node.Rect, extent, false,
                list.AlwaysShowVertical || totalHeight > node.Rect.Height, 16f);
            clips.Add(node, clip);
            float y = list.ScrollPosition == null ? 0f : list.ScrollPosition.Position.y;
            if (scene != null && scene.ScrollOffsets.TryGetValue(node.Path, out float requested)) y = requested;
            y = Math.Max(0f, Math.Min(y, Math.Max(0f, totalHeight - clip.Height)));
            PreviewScrollArea area = new PreviewScrollArea(node.Path, Convert.ToString(node.Widget.Key), node.Rect, clip, extent, 0f, y, true);
            AddScrollArea(node, area);
            int first, end;
            ScrollGeometry.VisibleRows(y, node.Rect.Height, rowHeight, list.ItemCount, out first, out end);
            for (int index = first; index < end; index++) {
                Widget row = list.BuildItem == null ? null : list.BuildItem(index);
                if (row == null || row is EmptyWidget) continue;
                node.Children.Add(layoutEngine.Layout(row,
                    new MuiRect(node.Rect.X, node.Rect.Y + index * rowHeight - y, extent.Width, rowHeight),
                    node.Path + ".row" + index));
                VirtualRowsBuilt++;
            }
            area.SetVirtualStats(rowHeight, list.ItemCount, node.Children.Count);
        } else if (node.Widget is ScrollView scroll) {
            ScrollViews++;
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect clip = ScrollGeometry.ContentViewport(node.Rect, extent, scroll.AlwaysShowHorizontal, scroll.AlwaysShowVertical, 16f);
            clips.Add(node, clip);
            float x = scroll.ScrollPosition == null ? 0f : scroll.ScrollPosition.Position.x;
            float y = scroll.ScrollPosition == null ? 0f : scroll.ScrollPosition.Position.y;
            if (scene != null && scene.ScrollOffsets.TryGetValue(node.Path, out float requested)) y = requested;
            x = Math.Max(0f, Math.Min(x, Math.Max(0f, extent.Width - clip.Width)));
            y = Math.Max(0f, Math.Min(y, Math.Max(0f, extent.Height - clip.Height)));
            AddScrollArea(node, new PreviewScrollArea(node.Path, Convert.ToString(node.Widget.Key), node.Rect, clip, extent, x, y, false));
            for (int i = 0; i < node.Children.Count; i++) Offset(node.Children[i], -x, -y);
        }
        for (int i = 0; i < node.Children.Count; i++) Expand(node.Children[i]);
    }

    private void AddScrollArea(LayoutNode node, PreviewScrollArea area) {
        ScrollAreas.Add(area);
        scrollAreasByNode.Add(node, area);
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
        // WidgetPainter paints containers and descendants in preorder. Keeping
        // complete nodes here preserves table/splitter backgrounds that inspect
        // their children's geometry. We remove only descendant command suffixes
        // when stitching clip scopes; no control drawing is recreated in SVG.
        paintState.ParentEnabled = parentEnabled;
        painted.Add(node, painter.Paint(node, paintState));
        for (int i = 0; i < node.Children.Count; i++) CacheDrawing(node.Children[i], parentEnabled && node.Widget.Enabled);
    }

    private void AppendDrawing(LayoutNode node, int layer) {
        DrawList subtree = painted[node];
        int ownCommands = subtree.Commands.Count;
        for (int i = 0; i < node.Children.Count; i++) ownCommands -= painted[node.Children[i]].Commands.Count;
        if (ownCommands < 0) throw new InvalidOperationException("WidgetPainter no longer produces preorder commands.");
        for (int i = 0; i < ownCommands; i++) {
            DrawCommand command = subtree.Commands[i];
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
        if (scrollAreasByNode.TryGetValue(node, out PreviewScrollArea area)) {
            // Production popup and native scroll skins share these geometry and
            // color tokens. The SVG backend does not invent a scrollbar style.
            MuiRect vertical = new MuiRect(area.Viewport.Right, area.Viewport.Y,
                Math.Max(0f, area.Bounds.Right - area.Viewport.Right), area.Viewport.Height);
            MuiRect horizontal = new MuiRect(area.Viewport.X, area.Viewport.Bottom,
                area.Viewport.Width, Math.Max(0f, area.Bounds.Bottom - area.Viewport.Bottom));
            PopupPresentation.DrawScrollbar(Drawing, palette,
                ScrollbarGeometry.Vertical(vertical, area.ContentHeight, area.ViewHeight, area.Offset));
            PopupPresentation.DrawScrollbar(Drawing, palette,
                ScrollbarGeometry.Horizontal(horizontal, area.ContentWidth, area.Viewport.Width, area.OffsetX));
        }
    }

    private void FindVisible(LayoutNode node, MuiRect clip) {
        MuiRect own = Intersect(node.Rect, clip);
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
    public float ViewHeight { get; private set; }
    public float ContentHeight { get; private set; }
    public float Offset { get; private set; }
    public float VisibleHeight { get; private set; }
    public float VisibleContentStart { get; private set; }
    public bool Virtual { get; private set; }
    public float RowHeight { get; private set; }
    public int ItemCount { get; private set; }
    public int BuiltRows { get; private set; }
    public float Maximum { get { return Math.Max(0f, ContentHeight - ViewHeight); } }
    internal MuiRect Bounds, Viewport;
    internal float ContentWidth, OffsetX;
    internal PreviewScrollArea(string path, string key, MuiRect bounds, MuiRect viewport, MuiRect content, float offsetX, float offset, bool isVirtual) {
        Path = path; Key = key; Bounds = bounds; Viewport = viewport;
        ViewHeight = viewport.Height; ContentHeight = content.Height; ContentWidth = content.Width;
        OffsetX = offsetX; Offset = offset; Virtual = isVirtual;
    }
    internal void SetVisible(MuiRect visible) {
        VisibleHeight = visible.Width > 0f ? visible.Height : 0f;
        VisibleContentStart = Offset + Math.Max(0f, visible.Y - Viewport.Y);
    }
    internal void SetVirtualStats(float rowHeight, int itemCount, int builtRows) {
        RowHeight = rowHeight; ItemCount = itemCount; BuiltRows = builtRows;
    }
}
