using System.Collections.Generic;

namespace MaximumUI {
    public sealed class SelectionRequest {
        public object Value;
        public bool Selected;
        public bool Additive;
        public bool Range;
        public bool DoubleClick;
    }

    public sealed class SelectionState<T> {
        private readonly List<T> selectedItems = new List<T>();
        private readonly IEqualityComparer<T> comparer;

        public T Anchor;
        public bool HasAnchor;

        public SelectionState()
            : this(null, null) {
        }

        public SelectionState(IEnumerable<T> selected)
            : this(selected, null) {
        }

        public SelectionState(IEnumerable<T> selected, IEqualityComparer<T> comparer) {
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            if (selected == null) return;

            foreach (T item in selected) {
                if (!IsSelected(item)) selectedItems.Add(item);
            }
        }

        public IList<T> SelectedItems {
            get { return selectedItems; }
        }

        public int Count {
            get { return selectedItems.Count; }
        }

        public bool IsSelected(T item) {
            return IndexOfSelected(item) >= 0;
        }

        public void Clear() {
            selectedItems.Clear();
            HasAnchor = false;
            Anchor = default(T);
        }

        public void SetSingle(T item) {
            selectedItems.Clear();
            selectedItems.Add(item);
            Anchor = item;
            HasAnchor = true;
        }

        public void SetSelected(T item, bool selected) {
            int index = IndexOfSelected(item);
            if (selected) {
                if (index < 0) selectedItems.Add(item);
            } else if (index >= 0) {
                selectedItems.RemoveAt(index);
            }

            Anchor = item;
            HasAnchor = true;
        }

        public void Toggle(T item) {
            SetSelected(item, !IsSelected(item));
        }

        public void Apply(IList<T> orderedItems, T item, bool additive, bool range) {
            if (range && orderedItems != null && orderedItems.Count > 0) {
                ApplyRange(orderedItems, item, additive);
                return;
            }

            if (additive) {
                Toggle(item);
                return;
            }

            SetSingle(item);
        }

        public IList<T> Snapshot() {
            return new List<T>(selectedItems);
        }

        private void ApplyRange(IList<T> orderedItems, T item, bool additive) {
            int itemIndex = IndexOf(orderedItems, item);
            if (itemIndex < 0) {
                if (additive) Toggle(item);
                else SetSingle(item);
                return;
            }

            if (!HasAnchor) {
                Anchor = item;
                HasAnchor = true;
            }

            int anchorIndex = IndexOf(orderedItems, Anchor);
            if (anchorIndex < 0) anchorIndex = itemIndex;

            if (!additive) selectedItems.Clear();

            int first = anchorIndex < itemIndex ? anchorIndex : itemIndex;
            int last = anchorIndex < itemIndex ? itemIndex : anchorIndex;
            for (int i = first; i <= last; i++) {
                T rangeItem = orderedItems[i];
                if (!IsSelected(rangeItem)) selectedItems.Add(rangeItem);
            }
        }

        private int IndexOfSelected(T item) {
            for (int i = 0; i < selectedItems.Count; i++) {
                if (comparer.Equals(selectedItems[i], item)) return i;
            }

            return -1;
        }

        private int IndexOf(IList<T> items, T item) {
            for (int i = 0; i < items.Count; i++) {
                if (comparer.Equals(items[i], item)) return i;
            }

            return -1;
        }
    }
}
