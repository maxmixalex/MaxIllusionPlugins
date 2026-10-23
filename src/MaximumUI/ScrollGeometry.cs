using System;

namespace MaximumUI {
    internal static class ScrollGeometry {
        public static MuiRect ContentExtent(LayoutNode node, MuiRect viewport) {
            float width = Math.Max(1f, viewport.Width - 16f);
            float height = viewport.Height;
            for (int i = 0; i < node.Children.Count; i++) {
                LayoutNode child = node.Children[i];
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
