using System;

namespace MaximumUI {
    internal static class ScrollGeometry {
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
            float width = Math.Max(1f, viewport.Width - 16f);
            float height = viewport.Height;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
                if (child.Widget is Dialog) continue;
                width = Math.Max(width, child.Rect.Right - node.Rect.X);
                height = Math.Max(height, child.Rect.Bottom - node.Rect.Y);
            }
            return new MuiRect(0f, 0f, width, Math.Max(1f, height));
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
