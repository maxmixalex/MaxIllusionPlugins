using System;
using System.Collections.Generic;

namespace MaximumUI {
    public sealed class NavigationItem {
        public string Id;
        public string Key;
        public string Path;
        public string TypeName;
        public MuiRect Rect;
        public bool Enabled;

        public NavigationItem Clone() {
            return (NavigationItem)MemberwiseClone();
        }
    }

    public sealed class UiNavigationStore {
        private readonly List<NavigationItem> items = new List<NavigationItem>();
        private readonly List<NavigationItem> itemPool = new List<NavigationItem>();
        private readonly Dictionary<string, NavigationItem> itemById = new Dictionary<string, NavigationItem>();
        private string focusedId;
        private string requestedFocusId;
        private string defaultFocusId;
        private HashSet<string> previousFocusRequests = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> currentFocusRequests = new HashSet<string>(StringComparer.Ordinal);
        public bool KeyboardActive { get; private set; }
        internal int ExplicitFocusRevision { get; private set; }

        public string FocusedId {
            get { return focusedId; }
        }

        public int Count {
            get { return items.Count; }
        }

        public bool HasFocus {
            get { return !string.IsNullOrEmpty(focusedId); }
        }

        public void BeginFrame() {
            items.Clear();
            itemById.Clear();
            currentFocusRequests.Clear();
            requestedFocusId = null;
            defaultFocusId = null;
        }

        public void Register(Widget widget, string path, MuiRect rect, bool enabled) {
            if (widget == null || widget is EmptyWidget || !widget.Focusable) return;

            string id = ItemId(widget, path);
            bool repeatedRequest = widget.RequestFocus && previousFocusRequests.Contains(id);
            // Input suppression/modal blocking can temporarily disable a drawn
            // item for one event. Preserve an already observed request through
            // that event, while leaving a first disabled request pending.
            if (repeatedRequest) currentFocusRequests.Add(id);
            if (!enabled || !widget.Enabled) return;
            int slot = items.Count;
            NavigationItem item;
            if (slot < itemPool.Count) item = itemPool[slot];
            else {
                item = new NavigationItem();
                if (slot < 512) itemPool.Add(item);
            }
            item.Id = id;
            item.Key = widget.IdentityKeyText;
            item.Path = path ?? string.Empty;
            item.TypeName = widget.IdentityTypeName;
            item.Rect = rect;
            item.Enabled = enabled;

            items.Add(item);
            itemById[id] = item;
            itemById[widget.PathIdentity(path)] = item;

            if (widget.HasDefaultFocus && string.IsNullOrEmpty(defaultFocusId)) defaultFocusId = id;
            if (widget.RequestFocus) {
                currentFocusRequests.Add(id);
                // Declarative requests are edge-triggered by logical identity.
                // Repainting or rebuilding an unchanged request must not take
                // keyboard ownership away from a window the user clicked.
                if (!repeatedRequest) requestedFocusId = id;
            }
        }

        public void EndFrame() {
            HashSet<string> oldRequests = previousFocusRequests;
            previousFocusRequests = currentFocusRequests;
            currentFocusRequests = oldRequests;
            if (!string.IsNullOrEmpty(requestedFocusId) && itemById.ContainsKey(requestedFocusId)) {
                FocusId(requestedFocusId);
                return;
            }

            if (!string.IsNullOrEmpty(focusedId) && itemById.ContainsKey(focusedId)) return;

            if (!string.IsNullOrEmpty(defaultFocusId) && itemById.ContainsKey(defaultFocusId)) {
                focusedId = defaultFocusId;
                KeyboardActive = true;
                return;
            }

            focusedId = items.Count > 0 ? items[0].Id : null;
            KeyboardActive = false;
        }

        public bool Focus(object key) {
            if (key == null) return false;
            return FocusId("key:" + key);
        }

        public bool FocusPath(string path) {
            if (path == null) return false;
            return FocusId("path:" + path);
        }

        public bool FocusNext() {
            if (items.Count == 0) {
                focusedId = null;
                return false;
            }

            int index = FocusedIndex();
            int next = index < 0 ? 0 : (index + 1) % items.Count;
            focusedId = items[next].Id;
            KeyboardActive = true;
            return true;
        }

        public bool FocusPrevious() {
            if (items.Count == 0) {
                focusedId = null;
                return false;
            }

            int index = FocusedIndex();
            int previous = index < 0 ? items.Count - 1 : (index - 1 + items.Count) % items.Count;
            focusedId = items[previous].Id;
            KeyboardActive = true;
            return true;
        }

        public bool IsFocused(Widget widget, string path) {
            return string.Equals(focusedId, ItemId(widget, path), StringComparison.Ordinal);
        }

        internal void FocusPointer(Widget widget, string path) {
            string id = ItemId(widget, path);
            if (itemById.ContainsKey(id)) focusedId = id;
            KeyboardActive = false;
        }

        internal bool IsKeyboardFocused(Widget widget, string path) {
            return KeyboardActive && IsFocused(widget, path);
        }

        public NavigationItem FocusedItem() {
            if (string.IsNullOrEmpty(focusedId)) return null;

            NavigationItem item;
            return itemById.TryGetValue(focusedId, out item) ? item.Clone() : null;
        }

        public IList<NavigationItem> Snapshot() {
            List<NavigationItem> result = new List<NavigationItem>();
            for (int i = 0; i < items.Count; i++) {
                result.Add(items[i].Clone());
            }

            return result;
        }

        private bool FocusId(string id) {
            NavigationItem item;
            if (string.IsNullOrEmpty(id) || !itemById.TryGetValue(id, out item)) return false;
            // A path lookup can resolve a keyed item. Store its canonical ID
            // so IsFocused and sequential navigation recognize the selection.
            focusedId = item.Id;
            KeyboardActive = true;
            unchecked { ExplicitFocusRevision++; }
            return true;
        }

        private int FocusedIndex() {
            for (int i = 0; i < items.Count; i++) {
                if (string.Equals(items[i].Id, focusedId, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        private static string ItemId(Widget widget, string path) {
            return widget == null ? "path:" + (path ?? string.Empty) : widget.Identity(path);
        }
    }
}
