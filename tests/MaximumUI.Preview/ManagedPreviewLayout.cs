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

    private readonly Dictionary<LayoutNode, MuiRect> clips = new Dictionary<LayoutNode, MuiRect>();
    private readonly Dictionary<LayoutNode, DrawList> painted = new Dictionary<LayoutNode, DrawList>();
    private LayoutEngine layoutEngine;
    private WidgetPainter painter;
    private readonly WidgetPaintState paintState = new WidgetPaintState();

    internal static ManagedPreviewLayout Create(Widget widget, MuiRect viewport, LayoutMetrics metrics, ThemeTokens palette) {
        ManagedPreviewLayout preview = new ManagedPreviewLayout {
            layoutEngine = new LayoutEngine(metrics), painter = new WidgetPainter(palette)
        };
        LayoutNode root = preview.layoutEngine.Layout(widget, viewport);
        preview.Expand(root);
        preview.Verify(root, viewport, false);
        preview.CacheDrawing(root);
        preview.AppendDrawing(root);
        return preview;
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
            y = Math.Max(0f, Math.Min(y, Math.Max(0f, totalHeight - clip.Height)));
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
        } else if (node.Widget is ScrollView scroll) {
            ScrollViews++;
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect clip = ScrollGeometry.ContentViewport(node.Rect, extent, scroll.AlwaysShowHorizontal, scroll.AlwaysShowVertical, 16f);
            clips.Add(node, clip);
            float x = scroll.ScrollPosition == null ? 0f : scroll.ScrollPosition.Position.x;
            float y = scroll.ScrollPosition == null ? 0f : scroll.ScrollPosition.Position.y;
            x = Math.Max(0f, Math.Min(x, Math.Max(0f, extent.Width - clip.Width)));
            y = Math.Max(0f, Math.Min(y, Math.Max(0f, extent.Height - clip.Height)));
            for (int i = 0; i < node.Children.Count; i++) Offset(node.Children[i], -x, -y);
        }
        for (int i = 0; i < node.Children.Count; i++) Expand(node.Children[i]);
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

    private void CacheDrawing(LayoutNode node) {
        // WidgetPainter paints containers and descendants in preorder. Keeping
        // complete nodes here preserves table/splitter backgrounds that inspect
        // their children's geometry. We remove only descendant command suffixes
        // when stitching clip scopes; no control drawing is recreated in SVG.
        painted.Add(node, painter.Paint(node, paintState));
        for (int i = 0; i < node.Children.Count; i++) CacheDrawing(node.Children[i]);
    }

    private void AppendDrawing(LayoutNode node) {
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
        }
        MuiRect clip;
        bool scoped = clips.TryGetValue(node, out clip);
        if (scoped) Drawing.PushClip(clip);
        for (int i = 0; i < node.Children.Count; i++) AppendDrawing(node.Children[i]);
        if (scoped) Drawing.PopClip();
    }
}
