using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MaximumUI;

// Native host state (which popup is open, filter, scroll) is supplied by the
// fixture. All options, menu models, filtering, placement and draw commands are
// production code shared with DropdownOverlay / ImguiRenderer.
internal static class PreviewPopupRenderer {
    private static readonly Type Combo = typeof(Widget).Assembly.GetType("MaximumUI.IComboBoxWidget", true);
    private static readonly Type Searchable = typeof(Widget).Assembly.GetType("MaximumUI.ISearchableComboBoxWidget", true);

    internal static void Append(ManagedPreviewLayout preview, PreviewScene scene, MuiRect viewport,
        LayoutMetrics metrics, ThemeTokens palette, int layer) {
        LayoutNode target = preview.Nodes.Find(node => Equals(node.Widget.Key, scene.PopupKey));
        if (target == null || !preview.VisiblePaths.Contains(target.Path))
            throw new InvalidOperationException("Production popup anchor is not visible: " + scene.PopupKey);
        if (!target.Widget.Enabled) throw new InvalidOperationException("Cannot open a disabled production popup.");
        if (scene.PopupKind == "context") { Context(preview, target, viewport, metrics, palette, layer); return; }
        if (scene.PopupKind == "tooltip") {
            string text = target.Widget.TooltipText;
            if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Production tooltip content is missing.");
            MuiRect bounds = PopupGeometry.Tooltip(text,
                new MuiVec2(target.Rect.X + Math.Min(24f, target.Rect.Width * 0.5f), target.Rect.Y + target.Rect.Height * 0.5f),
                viewport, metrics.MeasureText, metrics.MeasureWrappedTextHeight, metrics.RowHeight);
            LayoutNode tooltip = new LayoutNode { Widget = target.Widget, Path = target.Path + ".tooltip", Rect = bounds };
            DrawList tipDrawing = new DrawList();
            PopupPresentation.DrawTooltip(tipDrawing, palette, bounds, text);
            AddDrawing(preview, tipDrawing, tooltip, layer);
            preview.Nodes.Add(tooltip); preview.VisiblePaths.Add(tooltip.Path);
            preview.Popups.Add(new PreviewPopup("tooltip", Convert.ToString(scene.PopupKey), bounds, 0, string.Empty, false, false, false));
            return;
        }
        DrawList drawing = new DrawList();
        PopupLayout layout;
        LayoutNode owner = new LayoutNode { Widget = target.Widget, Path = target.Path + ".popup" };
        int count;
        bool hasBack = false;
        if (scene.PopupKind == "combo") {
            if (!Combo.IsInstanceOfType(target.Widget)) throw new InvalidOperationException("Popup target is not a production combo.");
            bool searchable = Searchable.IsInstanceOfType(target.Widget);
            string filter = searchable ? scene.PopupFilter ?? string.Empty : string.Empty;
            ChoiceCache choices = new ChoiceCache();
            MethodInfo formatter = Combo.GetMethod("GetComboText");
            choices.Refresh(target.Widget, (IEnumerable)Get(Combo, "ComboOptions", target.Widget),
                value => (string)formatter.Invoke(target.Widget, new[] { value }), filter, 0, true);
            object selected = Get(Combo, "ComboValue", target.Widget);
            int active = 0;
            for (int i = 0; i < choices.Values.Count; i++) if (Equals(selected, choices.Values[i])) { active = i; break; }
            MuiRect anchor = FindCommand(preview, target, command => command.Type == DrawCommandType.FillRect).Rect;
            layout = PopupGeometry.Dropdown(anchor, viewport, choices.Labels.Length,
                (int)Get(Combo, "ComboMaxVisibleItems", target.Widget), metrics.RowHeight, searchable);
            float scroll = scene.PopupBottom ? layout.ClampScroll(float.MaxValue) : 0f;
            PopupPresentation.DrawDropdown(drawing, palette, layout, choices.Labels, active,
                Math.Min(1, choices.Labels.Length - 1), scroll, filter,
                searchable ? (string)Get(Searchable, "ComboFilterPlaceholder", target.Widget) : string.Empty);
            count = choices.Labels.Length;
        } else if (scene.PopupKind == "menu") {
            MenuBar bar = target.Widget as MenuBar;
            if (bar == null || scene.PopupMenuIndex >= bar.Children.Count)
                throw new InvalidOperationException("Missing production menu.");
            Menu menu = bar.Children[scene.PopupMenuIndex] as Menu;
            if (menu == null) throw new InvalidOperationException("Production menu bar child is not a Menu.");
            MuiRect anchor = FindCommand(preview, target, command => command.Type == DrawCommandType.Text && command.Text == menu.Title).Rect;
            if (scene.PopupSubmenuIndex >= 0) {
                menu = menu.Children[scene.PopupSubmenuIndex] as Menu;
                if (menu == null) throw new InvalidOperationException("Missing production submenu.");
                hasBack = true;
            }
            owner.Widget = menu;
            layout = PopupGeometry.Menu(anchor, viewport, menu, hasBack, metrics.RowHeight, metrics.MeasureText);
            PopupPresentation.DrawMenu(drawing, palette, layout, menu, hasBack,
                hoverIndex: 0, measureText: metrics.MeasureText);
            for (int i = 0; i < menu.Children.Count; i++) {
                LayoutNode row = new LayoutNode { Widget = menu.Children[i], Path = owner.Path + "." + i,
                    Rect = PopupGeometry.MenuRow(layout, menu, i, hasBack) };
                owner.Children.Add(row);
                preview.Nodes.Add(row);
                MuiRect visible = ManagedPreviewLayout.Intersect(row.Rect, layout.ContentViewport);
                if (visible.Width > 0f && visible.Height > 0f) preview.VisiblePaths.Add(row.Path);
            }
            count = menu.Children.Count;
        } else throw new InvalidOperationException("Unknown popup fixture " + scene.PopupKind);
        owner.Rect = layout.Bounds;
        preview.Nodes.Add(owner);
        preview.VisiblePaths.Add(owner.Path);
        AddDrawing(preview, drawing, owner, layer);
        preview.Popups.Add(new PreviewPopup(scene.PopupKind, Convert.ToString(scene.PopupKey), layout.Bounds,
            count, scene.PopupFilter, hasBack, layout.Scrollable, scene.PopupBottom));
    }

    private static object Get(Type type, string property, object owner) {
        return type.GetProperty(property).GetValue(owner);
    }

    private static DrawCommand FindCommand(ManagedPreviewLayout preview, LayoutNode owner, Predicate<DrawCommand> match) {
        foreach (DrawCommand command in preview.Drawing.Commands)
            if (preview.CommandOwners.TryGetValue(command, out LayoutNode found) && ReferenceEquals(found, owner) && match(command)) return command;
        throw new InvalidOperationException("Missing production popup anchor drawing at " + owner.Path);
    }

    private static void AddDrawing(ManagedPreviewLayout preview, DrawList drawing, LayoutNode owner, int layer) {
        foreach (DrawCommand command in drawing.Commands) {
            preview.Drawing.Commands.Add(command);
            LayoutNode specific = owner;
            if (command.Type == DrawCommandType.Text) foreach (LayoutNode row in owner.Children) {
                if (command.Rect.Y >= row.Rect.Y - 0.1f && command.Rect.Bottom <= row.Rect.Bottom + 0.1f) { specific = row; break; }
            }
            preview.CommandOwners.Add(command, specific);
            preview.CommandLayers.Add(command, layer);
        }
    }

    private static void Context(ManagedPreviewLayout preview, LayoutNode target, MuiRect viewport,
        LayoutMetrics metrics, ThemeTokens palette, int layer) {
        Widget content = target.Widget.ContextMenuContent;
        if (content == null) throw new InvalidOperationException("Production context menu content is missing.");
        string path = target.Path + ".context";
        LayoutNode measured = new LayoutEngine(metrics).Measure(content, new LayoutConstraints(0f, 260f, 0f, 420f), path);
        // Exact native context-menu host limits (ImguiRenderer.DrawContextMenu /
        // ClampOverlayRect); the content still uses the production layout/painter.
        float width = Math.Min(Math.Max(120f, measured.DesiredSize.X + 8f), Math.Max(20f, viewport.Width - 12f));
        float height = Math.Min(Math.Max(20f, measured.DesiredSize.Y + 8f), Math.Max(20f, viewport.Height - 12f));
        MuiRect bounds = new MuiRect(
            Math.Max(4f, Math.Min(target.Rect.X + target.Rect.Width * 0.5f, viewport.Width - width - 4f)),
            Math.Max(4f, Math.Min(target.Rect.Y + target.Rect.Height * 0.5f, viewport.Height - height - 4f)), width, height);
        PopupPresentation.DrawSurface(preview.Drawing, palette, bounds);
        ManagedPreviewLayout rendered = ManagedPreviewLayout.Create(content,
            new MuiRect(bounds.X + 4f, bounds.Y + 4f, width - 8f, height - 8f), metrics, palette, rootPath: path);
        foreach (DrawCommand command in rendered.Drawing.Commands) {
            preview.Drawing.Commands.Add(command);
            if (rendered.CommandOwners.TryGetValue(command, out LayoutNode owner)) preview.CommandOwners.Add(command, owner);
            preview.CommandLayers.Add(command, layer);
        }
        preview.Nodes.AddRange(rendered.Nodes);
        preview.VisiblePaths.UnionWith(rendered.VisiblePaths);
        preview.Popups.Add(new PreviewPopup("context", Convert.ToString(target.Widget.Key), bounds,
            content.Children.Count, string.Empty, false, false, false));
    }
}

internal sealed class PreviewPopup {
    public string Kind { get; private set; }
    public string Target { get; private set; }
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public int ItemCount { get; private set; }
    public string Filter { get; private set; }
    public bool HasBack { get; private set; }
    public bool Scrollable { get; private set; }
    public bool Bottom { get; private set; }
    internal PreviewPopup(string kind, string target, MuiRect bounds, int count, string filter, bool hasBack, bool scrollable, bool bottom) {
        Kind = kind; Target = target; X = bounds.X; Y = bounds.Y; Width = bounds.Width; Height = bounds.Height;
        ItemCount = count; Filter = filter ?? string.Empty; HasBack = hasBack; Scrollable = scrollable; Bottom = bottom;
    }
}
