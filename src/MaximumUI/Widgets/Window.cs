using System;

namespace MaximumUI {
    public sealed class Window : Widget {
        public string Title;
        public float Width;
        public float Height;
        public float MinWidth;
        public float MinHeight;
        public float FoldedHeight;
        public bool Foldable;
        public bool Closable;
        public bool Visible;
        public Action<bool> OnVisibleChange;

        public Window(
            string title,
            float width = 420f,
            float height = 300f,
            object key = null,
            float minWidth = 320f,
            float minHeight = 240f,
            bool foldable = true,
            bool closable = false,
            Action<bool> onVisibleChange = null)
            : base(key) {
            Title = title ?? string.Empty;
            Width = width;
            Height = height;
            MinWidth = minWidth;
            MinHeight = minHeight;
            FoldedHeight = 20f;
            Foldable = foldable;
            Closable = closable;
            Visible = true;
            OnVisibleChange = onVisibleChange;
        }
    }
}
