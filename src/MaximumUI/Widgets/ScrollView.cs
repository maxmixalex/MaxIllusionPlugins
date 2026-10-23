namespace MaximumUI {
    public sealed class ScrollView : Widget {
        public bool AlwaysShowHorizontal;
        public bool AlwaysShowVertical;
        public float Height;

        public ScrollView(object key = null)
            : base(key) {
        }
    }

    internal sealed class ScrollViewState {
        internal UnityEngine.Vector2 Position;
    }
}
