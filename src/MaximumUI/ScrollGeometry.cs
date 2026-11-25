using System;

namespace MaximumUI {
    /// <summary>A list's frame, native scrolling host and content clip are separate rectangles.</summary>
    public struct ScrollFrameLayout {
        public MuiRect Bounds;
        public MuiRect Viewport;
        public MuiRect ContentViewport;
        public MuiRect Content;
        public MuiRect VerticalGutter;
        public bool Vertical;

        public float ClampY(float position) {
            return Math.Min(ScrollGeometry.FiniteNonNegative(position), Math.Max(0f, Content.Height - ContentViewport.Height));
        }
    }

    public static class ScrollGeometry {
        public const float FrameBorder = 1f;

        public static ScrollFrameLayout ListFrame(MuiRect bounds, float rowHeight, int itemCount, bool searchable) {
            return VerticalFrame(ListBodyBounds(bounds, rowHeight, searchable), Math.Max(1f, rowHeight) * Math.Max(0, itemCount));
        }

        public static ScrollFrameLayout DebugFrame(MuiRect bounds, float rowHeight, int lineCount) {
            return VerticalFrame(bounds, Math.Max(1f, rowHeight) * (Math.Max(0, lineCount) + 1) + 4f);
        }

        public static ScrollFrameLayout ThemeEditorFrame(MuiRect bounds, float rowHeight, ThemeEditor editor) {
            return VerticalFrame(bounds, Math.Max(1f, rowHeight) * (Math.Max(1, ThemeEditorTokens.TotalRowCount(editor)) + (editor.ShowTitle ? 1 : 0)) + 4f);
        }

        public static ScrollFrameLayout VerticalFrame(MuiRect bounds, float contentHeight, bool alwaysShowVertical = false) {
            MuiRect viewport = PopupGeometry.Inset(bounds, FrameBorder, FrameBorder, FrameBorder, FrameBorder);
            float height = Math.Max(viewport.Height, FiniteNonNegative(contentHeight));
            bool vertical = alwaysShowVertical || height > viewport.Height;
            float width = Math.Max(0f, viewport.Width - (vertical ? ScrollbarGeometry.GutterSize : 0f));
            MuiRect content = new MuiRect(0f, 0f, width, height);
            MuiRect clip = new MuiRect(viewport.X, viewport.Y, width, viewport.Height);
            return new ScrollFrameLayout {
                Bounds = bounds, Viewport = viewport, ContentViewport = clip, Content = content, Vertical = vertical,
                VerticalGutter = new MuiRect(clip.Right, viewport.Y, Math.Max(0f, viewport.Right - clip.Right), viewport.Height)
            };
        }

        public static MuiRect ListFilterBounds(MuiRect bounds, float rowHeight) {
            return new MuiRect(bounds.X, bounds.Y, Math.Max(0f, bounds.Width), Math.Min(Math.Max(0f, rowHeight), Math.Max(0f, bounds.Height)));
        }

        public static MuiRect ListBodyBounds(MuiRect bounds, float rowHeight, bool searchable) {
            if (!searchable) return bounds;
            float top = Math.Min(bounds.Bottom, bounds.Y + Math.Max(0f, rowHeight) + 4f);
            return new MuiRect(bounds.X, top, Math.Max(0f, bounds.Width), Math.Max(0f, bounds.Bottom - top));
        }

        internal static float FiniteNonNegative(float value) {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        }

        internal static bool HasNativeScrollArea(MuiRect viewport) {
            return FiniteNonNegative(viewport.Width) > 0f && FiniteNonNegative(viewport.Height) > 0f;
        }

        internal static MuiVec2 ClampPosition(MuiVec2 position, MuiRect content, MuiRect viewport) {
            return new MuiVec2(Math.Min(FiniteNonNegative(position.X), Math.Max(0f, FiniteNonNegative(content.Width) - FiniteNonNegative(viewport.Width))),
                Math.Min(FiniteNonNegative(position.Y), Math.Max(0f, FiniteNonNegative(content.Height) - FiniteNonNegative(viewport.Height))));
        }

        public static MuiRect ContentViewport(MuiRect viewport, MuiRect content, bool horizontal, bool vertical, float scrollbarSize) {
            float size = Math.Max(0f, scrollbarSize);
            // Match BeginScrollView's horizontal -> vertical -> horizontal pass.
            horizontal = horizontal || content.Width > viewport.Width;
            vertical = vertical || content.Height > viewport.Height - (horizontal ? size : 0f);
            horizontal = horizontal || content.Width > viewport.Width - (vertical ? size : 0f);
            return new MuiRect(viewport.X, viewport.Y, Math.Max(0f, viewport.Width - (vertical ? size : 0f)),
                Math.Max(0f, viewport.Height - (horizontal ? size : 0f)));
        }

        public static MuiRect ContentExtent(LayoutNode node, MuiRect viewport) {
            float width = 0f;
            float height = 0f;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
                if (child.Widget is Dialog) continue;
                width = Math.Max(width, child.Rect.Right - node.Rect.X);
                height = Math.Max(height, child.Rect.Bottom - node.Rect.Y);
            }
            return new MuiRect(0f, 0f, width, Math.Max(0f, height));
        }

        public static void VisibleRows(float scrollY, float viewportHeight, float rowHeight, int itemCount, out int first, out int end) {
            int count = Math.Max(0, itemCount);
            float row = Math.Max(1f, rowHeight);
            first = Math.Max(0, Math.Min(count, (int)Math.Floor(Math.Max(0f, scrollY) / row) - 1));
            int visible = Math.Max(0, (int)Math.Ceiling(Math.Max(0f, viewportHeight) / row)) + 3;
            end = first + Math.Min(count - first, visible);
        }
    }
}
