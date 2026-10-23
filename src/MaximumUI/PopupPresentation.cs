using System;
using System.Collections.Generic;

namespace MaximumUI {
    /// <summary>Popup placement and row bounds in the same coordinate space as the anchor.</summary>
    public struct PopupLayout {
        public MuiRect Bounds;
        public MuiRect FilterRect;
        public MuiRect ScrollViewport;
        public MuiRect ContentViewport;
        public float RowHeight;
        public float ContentHeight;
        public bool Searchable;
        public bool Scrollable;

        public float ClampScroll(float offset) {
            return Math.Max(0f, Math.Min(Math.Max(0f, ContentHeight - ContentViewport.Height), offset));
        }

        public MuiRect Row(int index, float scrollY) {
            return new MuiRect(ContentViewport.X, ContentViewport.Y + Math.Max(0, index) * RowHeight - ClampScroll(scrollY),
                ContentViewport.Width, RowHeight);
        }
    }

    /// <summary>Pure managed geometry used by native popups and exported Gallery frames.</summary>
    public static class PopupGeometry {
        public const float TooltipHorizontalPadding = 12f;
        public const float TooltipVerticalPadding = 9f;

        public static MuiRect Tooltip(string text, MuiVec2 pointer, MuiRect viewport, Func<string, float> measureText,
            Func<string, float, float> measureWrappedHeight, float minHeight = 32f) {
            text = text ?? string.Empty;
            float margin = Math.Min(4f, Math.Max(0f, Math.Min(viewport.Width, viewport.Height) * 0.5f));
            MuiRect available = Inset(viewport, margin, margin, margin, margin);
            if (available.Width <= 0f || available.Height <= 0f) return new MuiRect(viewport.X, viewport.Y, 0f, 0f);
            float width = Math.Min(available.Width, Math.Min(320f, Math.Max(80f, Measure(text, measureText) + TooltipHorizontalPadding * 2f)));
            float contentWidth = Math.Max(1f, width - TooltipHorizontalPadding * 2f);
            float measuredHeight = measureWrappedHeight == null
                ? Math.Max(1f, (float)Math.Ceiling(Measure(text, measureText) / contentWidth)) * 20f
                : Math.Max(0f, measureWrappedHeight(text, contentWidth));
            float height = Math.Min(available.Height, Math.Max(minHeight, measuredHeight + TooltipVerticalPadding * 2f));
            MuiRect placed = WindowGeometry.PlacePopup(new MuiRect(pointer.X + 12f - available.X, pointer.Y - available.Y, 0f, 18f),
                width, height, available.Width, available.Height);
            placed.X += available.X;
            placed.Y += available.Y;
            return placed;
        }

        public static MuiRect TooltipTextBounds(MuiRect bounds) {
            return Inset(bounds, TooltipHorizontalPadding, TooltipVerticalPadding, TooltipHorizontalPadding, TooltipVerticalPadding);
        }

        public static PopupLayout Dropdown(MuiRect anchor, MuiRect viewport, int itemCount, int maxVisibleItems,
            float rowHeight, bool searchable, float padding = 4f, float scrollbarSize = 16f) {
            float row = Math.Max(1f, rowHeight);
            int count = Math.Max(1, itemCount);
            int visible = maxVisibleItems <= 0 ? count : Math.Max(1, Math.Min(count, maxVisibleItems));
            float filterHeight = searchable ? row + 4f : 0f;
            float available = Math.Max(0f, viewport.Height - padding * 2f - filterHeight);
            float rowsHeight = Math.Min(row * visible, Math.Min(available, Math.Max(row, viewport.Height * 0.4f)));
            return Create(anchor, viewport, anchor.Width, row * count, rowsHeight, row, searchable, padding, scrollbarSize);
        }

        public static PopupLayout Menu(MuiRect anchor, MuiRect viewport, Menu menu, bool hasBack,
            float rowHeight, Func<string, float> measureText, float scrollbarSize = 16f) {
            float row = Math.Max(1f, rowHeight);
            float width = 160f;
            float contentHeight = hasBack ? row : 0f;
            if (menu != null) {
                for (int i = 0; i < menu.Children.Count; i++) {
                    Widget child = menu.Children[i];
                    MenuItem item = child as MenuItem;
                    Menu submenu = child as Menu;
                    MenuSeparator separator = child as MenuSeparator;
                    contentHeight += MenuRowHeight(child, row);
                    if (item != null) {
                        float shortcut = string.IsNullOrEmpty(item.Shortcut) ? 0f : Measure(item.Shortcut, measureText) + 32f;
                        width = Math.Max(width, Measure(item.Text, measureText) + shortcut + 52f);
                    } else if (submenu != null) width = Math.Max(width, Measure(submenu.Title, measureText) + 68f);
                    else if (separator != null) width = Math.Max(width, Measure(separator.Text, measureText) + 24f);
                }
            }
            contentHeight = Math.Max(row, contentHeight);
            float available = Math.Max(0f, viewport.Height - 4f);
            bool scroll = contentHeight > available;
            return Create(anchor, viewport, width + (scroll ? scrollbarSize : 0f), contentHeight,
                Math.Min(contentHeight, available), row, false, 2f, scrollbarSize);
        }

        public static float MenuRowHeight(Widget child, float rowHeight) {
            MenuSeparator separator = child as MenuSeparator;
            if (separator != null) return string.IsNullOrEmpty(separator.Text) ? 8f : rowHeight;
            return child is MenuItem || child is Menu ? rowHeight : 0f;
        }

        public static MuiRect MenuRow(PopupLayout layout, Menu menu, int index, bool hasBack, float scrollY = 0f) {
            float y = layout.ContentViewport.Y + (hasBack ? layout.RowHeight : 0f) - layout.ClampScroll(scrollY);
            for (int i = 0; menu != null && i < index && i < menu.Children.Count; i++)
                y += MenuRowHeight(menu.Children[i], layout.RowHeight);
            float height = menu == null || index < 0 || index >= menu.Children.Count ? 0f : MenuRowHeight(menu.Children[index], layout.RowHeight);
            return new MuiRect(layout.ContentViewport.X, y, layout.ContentViewport.Width, height);
        }

        public static MuiRect DropdownTextBounds(MuiRect row) {
            return Inset(row, 10f, 0f, 10f, 0f);
        }

        public static MuiRect MenuIconBounds(MuiRect row, bool trailing = false) {
            return new MuiRect(trailing ? Math.Max(row.X, row.Right - 28f) : row.X + 4f, row.Y,
                Math.Min(20f, Math.Max(0f, row.Width - 8f)), row.Height);
        }

        public static MuiRect MenuTextBounds(MuiRect row, float shortcutWidth, bool submenu) {
            return Inset(row, 32f, 0f, submenu ? 32f : shortcutWidth + 8f, 0f);
        }

        public static MuiRect MenuShortcutBounds(MuiRect row, float shortcutWidth) {
            return new MuiRect(Math.Max(row.X, row.Right - shortcutWidth), row.Y,
                Math.Max(0f, Math.Min(row.Width, shortcutWidth) - 8f), row.Height);
        }

        public static MuiRect Inset(MuiRect rect, float left, float top, float right, float bottom) {
            left = Math.Min(Math.Max(0f, left), Math.Max(0f, rect.Width));
            top = Math.Min(Math.Max(0f, top), Math.Max(0f, rect.Height));
            return new MuiRect(rect.X + left, rect.Y + top, Math.Max(0f, rect.Width - left - right), Math.Max(0f, rect.Height - top - bottom));
        }

        private static PopupLayout Create(MuiRect anchor, MuiRect viewport, float width, float contentHeight,
            float visibleHeight, float rowHeight, bool searchable, float padding, float scrollbarSize) {
            padding = Math.Max(0f, padding);
            float filterHeight = searchable ? rowHeight + 4f : 0f;
            MuiRect placed = WindowGeometry.PlacePopup(new MuiRect(anchor.X - viewport.X, anchor.Y - viewport.Y, anchor.Width, anchor.Height),
                width, visibleHeight + filterHeight + padding * 2f, viewport.Width, viewport.Height);
            placed.X += viewport.X;
            placed.Y += viewport.Y;
            MuiRect inner = Inset(placed, padding, padding, padding, padding);
            float actualFilterHeight = Math.Min(inner.Height, filterHeight);
            MuiRect scrollViewport = new MuiRect(inner.X, inner.Y + actualFilterHeight, inner.Width, Math.Max(0f, inner.Height - actualFilterHeight));
            bool scroll = contentHeight > scrollViewport.Height;
            return new PopupLayout {
                Bounds = placed,
                FilterRect = searchable ? new MuiRect(inner.X, inner.Y, inner.Width, Math.Min(rowHeight, inner.Height)) : new MuiRect(),
                ScrollViewport = scrollViewport,
                ContentViewport = new MuiRect(scrollViewport.X, scrollViewport.Y, Math.Max(0f, scrollViewport.Width - (scroll ? scrollbarSize : 0f)), scrollViewport.Height),
                RowHeight = rowHeight, ContentHeight = contentHeight, Searchable = searchable, Scrollable = scroll
            };
        }

        private static float Measure(string text, Func<string, float> measure) {
            return measure == null ? ThemeTypography.EstimateTextWidth(text ?? string.Empty, ThemeTypography.DefaultFontSize) : Math.Max(0f, measure(text ?? string.Empty));
        }
    }

    public struct ScrollbarLayout {
        public MuiRect Track;
        public MuiRect Thumb;
        public bool Visible;
    }

    /// <summary>Matches the native 12px scroll track, 4px gutter and 2px track inset.</summary>
    public static class ScrollbarGeometry {
        public const float GutterSize = 16f;

        public static ScrollbarLayout Vertical(MuiRect gutter, float contentHeight, float viewportHeight, float offset) {
            return Calculate(gutter, contentHeight, viewportHeight, offset, false);
        }

        public static ScrollbarLayout Horizontal(MuiRect gutter, float contentWidth, float viewportWidth, float offset) {
            return Calculate(gutter, contentWidth, viewportWidth, offset, true);
        }

        private static ScrollbarLayout Calculate(MuiRect gutter, float content, float viewport, float offset, bool horizontal) {
            float cross = Math.Max(0f, horizontal ? gutter.Height : gutter.Width);
            float gap = Math.Min(4f, cross);
            MuiRect track = horizontal
                ? new MuiRect(gutter.X, gutter.Y + gap, gutter.Width, cross - gap)
                : new MuiRect(gutter.X + gap, gutter.Y, cross - gap, gutter.Height);
            MuiRect inner = PopupGeometry.Inset(track, 2f, 2f, 2f, 2f);
            float length = horizontal ? inner.Width : inner.Height;
            bool visible = content > viewport && content > 0f && viewport > 0f && length > 0f;
            float thumbLength = Math.Min(length, Math.Max(12f, length * Math.Max(0f, viewport) / Math.Max(1f, content)));
            float fraction = content <= viewport ? 0f : Math.Max(0f, Math.Min(1f, offset / (content - viewport)));
            float start = Math.Max(0f, length - thumbLength) * fraction;
            MuiRect thumb = horizontal ? new MuiRect(inner.X + start, inner.Y, thumbLength, inner.Height)
                : new MuiRect(inner.X, inner.Y + start, inner.Width, thumbLength);
            return new ScrollbarLayout { Track = track, Thumb = thumb, Visible = visible };
        }

        public static MuiColor TrackColor(ThemeTokens theme) { return theme.FieldBg; }
        public static MuiColor ThumbColor(ThemeTokens theme, bool hovered, bool pressed) {
            return pressed ? theme.Text : hovered ? theme.ControlHoverBorder : theme.TextMuted;
        }
    }

    public static class PopupPresentation {
        public static void DrawTooltip(DrawList drawing, ThemeTokens theme, MuiRect bounds, string text) {
            if (drawing == null || theme == null) return;
            drawing.FillRect(bounds, theme.PanelBg);
            drawing.StrokeRect(bounds, theme.Border, 1f);
            TextFormat format = ThemeTypography.Format(theme, LabelRole.Body, true);
            format.VerticalAlignment = TextVerticalAlignment.Top;
            drawing.Text(PopupGeometry.TooltipTextBounds(bounds), text, null, theme.Text, format);
        }

        public static MuiColor RowBackground(ThemeTokens theme, bool enabled, bool hover, bool selected, bool pressed = false) {
            return !enabled ? theme.DropdownBg : pressed ? theme.ButtonActiveBg : selected ? theme.SelectedBg : hover ? theme.ButtonHoverBg : theme.DropdownBg;
        }

        public static void DrawDropdown(DrawList drawing, ThemeTokens theme, PopupLayout layout, IList<string> items,
            int activeIndex, int hoverIndex = -1, float scrollY = 0f, string filterText = "", string placeholder = "",
            bool filterFocused = true, bool enabled = true) {
            if (drawing == null || theme == null) return;
            DrawSurface(drawing, theme, layout.Bounds);
            if (layout.Searchable) {
                drawing.FillRect(layout.FilterRect, theme.FieldBg);
                drawing.StrokeRect(layout.FilterRect, Mdl2ControlChrome.FieldBorderColor(theme, enabled, false, filterFocused), 2f);
                bool empty = string.IsNullOrEmpty(filterText);
                drawing.Text(PopupGeometry.Inset(layout.FilterRect, 8f, 0f, 8f, 0f), empty ? placeholder : filterText, null,
                    !enabled ? theme.TextDisabled : empty ? theme.TextMuted : theme.Text, ThemeTypography.Format(theme));
            }
            int count = items == null ? 0 : items.Count;
            int first, end;
            ScrollGeometry.VisibleRows(layout.ClampScroll(scrollY), layout.ContentViewport.Height, layout.RowHeight, count, out first, out end);
            drawing.PushClip(layout.ContentViewport);
            if (count == 0) drawing.Text(PopupGeometry.DropdownTextBounds(layout.Row(0, 0f)), "No results", null, theme.TextMuted, ThemeTypography.Format(theme));
            for (int i = first; i < end; i++) {
                MuiRect row = layout.Row(i, scrollY);
                bool selected = enabled && i == activeIndex;
                drawing.FillRect(row, RowBackground(theme, enabled, i == hoverIndex, selected));
                if (selected) drawing.FillRect(new MuiRect(row.X, row.Y, Math.Min(3f, row.Width), row.Height), theme.Accent);
                drawing.Text(PopupGeometry.DropdownTextBounds(row), items[i] ?? string.Empty, null, enabled ? theme.Text : theme.TextDisabled, ThemeTypography.Format(theme));
            }
            drawing.PopClip();
            DrawPopupScrollbar(drawing, theme, layout, scrollY);
        }

        public static void DrawMenu(DrawList drawing, ThemeTokens theme, PopupLayout layout, Menu menu, bool hasBack,
            int hoverIndex = -1, bool backHovered = false, float scrollY = 0f, Func<string, float> measureText = null, bool enabled = true) {
            if (drawing == null || theme == null || menu == null) return;
            enabled = enabled && menu.Enabled;
            DrawSurface(drawing, theme, layout.Bounds);
            drawing.PushClip(layout.ContentViewport);
            if (hasBack) {
                MuiRect back = layout.Row(0, scrollY);
                drawing.FillRect(back, RowBackground(theme, enabled, backHovered, false));
                Mdl2IconGeometry.Draw(drawing, PopupGeometry.MenuIconBounds(back), Mdl2Icon.ChevronLeft, enabled ? theme.Text : theme.TextDisabled, 1f);
                drawing.Text(PopupGeometry.MenuTextBounds(back, 0f, false), "Back", null, enabled ? theme.Text : theme.TextDisabled, ThemeTypography.Format(theme));
            }
            float rowY = layout.ContentViewport.Y + (hasBack ? layout.RowHeight : 0f) - layout.ClampScroll(scrollY);
            for (int i = 0; i < menu.Children.Count; i++) {
                Widget child = menu.Children[i];
                float rowHeight = PopupGeometry.MenuRowHeight(child, layout.RowHeight);
                MuiRect row = new MuiRect(layout.ContentViewport.X, rowY, layout.ContentViewport.Width, rowHeight);
                rowY += rowHeight;
                if (child == null) continue;
                if (row.Bottom < layout.ContentViewport.Y || row.Y > layout.ContentViewport.Bottom) continue;
                bool itemEnabled = enabled && child.Enabled;
                MuiColor text = itemEnabled ? theme.Text : theme.TextDisabled;
                MenuItem item = child as MenuItem;
                Menu submenu = child as Menu;
                MenuSeparator separator = child as MenuSeparator;
                if (item != null || submenu != null) drawing.FillRect(row, RowBackground(theme, itemEnabled, i == hoverIndex, false));
                if (item != null) {
                    if (item.Checked) Mdl2IconGeometry.Draw(drawing, PopupGeometry.MenuIconBounds(row), Mdl2Icon.Check, text, 1.5f);
                    float shortcutWidth = string.IsNullOrEmpty(item.Shortcut) ? 0f :
                        (measureText == null ? ThemeTypography.EstimateTextWidth(item.Shortcut, theme.FontSize) : measureText(item.Shortcut)) + 16f;
                    drawing.Text(PopupGeometry.MenuTextBounds(row, shortcutWidth, false), item.Text, null, text, ThemeTypography.Format(theme));
                    if (shortcutWidth > 0f) drawing.Text(PopupGeometry.MenuShortcutBounds(row, shortcutWidth), item.Shortcut, null,
                        itemEnabled ? theme.TextMuted : theme.TextDisabled, ThemeTypography.Format(theme, LabelRole.Body, false, TextAlignment.End));
                } else if (submenu != null) {
                    drawing.Text(PopupGeometry.MenuTextBounds(row, 0f, true), submenu.Title, null, text, ThemeTypography.Format(theme));
                    Mdl2IconGeometry.Draw(drawing, PopupGeometry.MenuIconBounds(row, true), Mdl2Icon.ChevronRight, text, 1f);
                } else if (separator != null) {
                    if (string.IsNullOrEmpty(separator.Text)) drawing.FillRect(new MuiRect(row.X + 4f, row.Y + 3f, Math.Max(0f, row.Width - 8f), 1f), theme.Separator);
                    else drawing.Text(PopupGeometry.Inset(row, 10f, 0f, 10f, 0f), separator.Text, null, theme.TextMuted, ThemeTypography.Format(theme));
                }
            }
            drawing.PopClip();
            DrawPopupScrollbar(drawing, theme, layout, scrollY);
        }

        public static void DrawScrollbar(DrawList drawing, ThemeTokens theme, ScrollbarLayout scrollbar, bool hovered = false, bool pressed = false) {
            if (drawing == null || theme == null || !scrollbar.Visible) return;
            drawing.FillRect(scrollbar.Track, ScrollbarGeometry.TrackColor(theme));
            drawing.FillRect(scrollbar.Thumb, ScrollbarGeometry.ThumbColor(theme, hovered, pressed));
        }

        public static void DrawSurface(DrawList drawing, ThemeTokens theme, MuiRect bounds) {
            drawing.FillRect(bounds, theme.DropdownBg);
            drawing.StrokeRect(bounds, theme.ControlBorder, 1f);
        }

        private static void DrawPopupScrollbar(DrawList drawing, ThemeTokens theme, PopupLayout layout, float scrollY) {
            if (!layout.Scrollable) return;
            MuiRect gutter = new MuiRect(layout.ContentViewport.Right, layout.ScrollViewport.Y,
                Math.Max(0f, layout.ScrollViewport.Right - layout.ContentViewport.Right), layout.ScrollViewport.Height);
            DrawScrollbar(drawing, theme, ScrollbarGeometry.Vertical(gutter, layout.ContentHeight, layout.ContentViewport.Height, layout.ClampScroll(scrollY)));
        }
    }
}
