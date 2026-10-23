using System;

namespace MaximumUI {
    public sealed class Dialog : Widget {
        public string Title;
        public float Width;
        public float Height;
        public bool CloseOnEscape;
        public bool CloseOnOutsideClick;
        public Action OnClose;

        public Dialog(string title, object key = null)
            : base(key) {
            Title = title ?? string.Empty;
            Width = 360f;
            Height = 180f;
            CloseOnEscape = true;
            CloseOnOutsideClick = false;
        }
    }

    internal sealed class DialogState {
        internal UnityEngine.Rect Rect;
        internal bool Initialized;
        internal DragState Drag = new DragState();
    }
}
