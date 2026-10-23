using System.Collections;
using System.Collections.Generic;

namespace MaximumUI {
    public abstract class Widget : IEnumerable {
        private readonly List<Widget> children = new List<Widget>();

        public object Key;
        public bool Enabled = true;
        public bool Focusable = true;
        public bool HasDefaultFocus;
        public bool RequestFocus;
        public readonly LayoutHints Layout = new LayoutHints();
        public string TooltipText;
        public Widget ContextMenuContent;

        protected Widget()
            : this(null) {
        }

        protected Widget(object key) {
            Key = key;
        }

        public IList<Widget> Children {
            get { return children; }
        }

        public void Add(Widget child) {
            if (child == null || child is EmptyWidget) return;

            Fragment fragment = child as Fragment;
            if (fragment != null) {
                for (int i = 0; i < fragment.Children.Count; i++) {
                    Add(fragment.Children[i]);
                }
                return;
            }

            children.Add(child);
        }

        public IEnumerator GetEnumerator() {
            return children.GetEnumerator();
        }
    }
}
