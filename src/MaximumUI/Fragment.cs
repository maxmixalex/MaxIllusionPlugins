namespace MaximumUI {
    public sealed class EmptyWidget : Widget {
        public static readonly EmptyWidget Instance = new EmptyWidget();

        private EmptyWidget() {
        }
    }

    public sealed class Fragment : Widget {
        public Fragment(object key = null)
            : base(key) {
        }
    }
}
