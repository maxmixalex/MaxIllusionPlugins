namespace MaximumUI {
    public sealed class ScrollView : Widget {
        public bool AlwaysShowHorizontal;
        public bool AlwaysShowVertical;
        public float Height;
        // Share one position between views to synchronize scrolling without
        // rebuilding either widget tree. Null keeps the mount-local position.
        public ScrollPosition ScrollPosition;

        public ScrollView(object key = null)
            : base(key) {
        }
    }

    internal sealed class ScrollViewState {
        internal UnityEngine.Vector2 Position;
    }
}
