using System.Collections;
using System.Collections.Generic;

namespace MaximumUI {
    public abstract class Widget : IEnumerable {
        private readonly List<Widget> children = new List<Widget>();
        private object identityKey;
        private string identityKeyText;
        private string identityKeyId;
        private string identityPath;
        private string identityPathId;
        private string identityTypeName;

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

        internal string IdentityKeyText {
            get {
                RefreshIdentityKey();
                return identityKeyText;
            }
        }

        private void RefreshIdentityKey() {
            if (object.ReferenceEquals(identityKey, Key)) return;
            identityKey = Key;
            identityKeyText = Key == null ? null : Key.ToString();
            identityKeyId = Key == null ? null : "key:" + identityKeyText;
        }

        internal string IdentityTypeName {
            get { return identityTypeName ?? (identityTypeName = GetType().Name); }
        }

        internal string Identity(string path) {
            // Keys are stable identifiers, not mutable display values.
            RefreshIdentityKey();
            return Key != null ? identityKeyId : PathIdentity(path);
        }

        internal string PathIdentity(string path) {
            path = path ?? string.Empty;
            if (identityPathId == null || !string.Equals(identityPath, path, System.StringComparison.Ordinal)) {
                identityPath = path;
                identityPathId = "path:" + path;
            }
            return identityPathId;
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
