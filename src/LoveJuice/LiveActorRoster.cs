using System;
using System.Collections.Generic;

namespace LoveJuice {
    // Identity-stable, game-independent selection of the actual active H
    // performer. Reordering never redirects a saved selection to another
    // character. Removal selects the first remaining actor.
    internal sealed class LiveActorRoster<T> where T : class {
        private readonly List<T> items = new List<T>();
        private T selected;

        internal int Count { get { return items.Count; } }
        internal T Selected { get { return selected; } }
        internal int SelectedIndex {
            get {
                for (int i = 0; i < items.Count; i++)
                    if (ReferenceEquals(items[i], selected)) return i;
                return -1;
            }
        }
        internal T At(int index) { return index >= 0 && index < items.Count ? items[index] : null; }

        internal bool Replace(IEnumerable<T> next) {
            var unique = new List<T>();
            if (next != null) foreach (T item in next) {
                if (item == null) continue;
                bool duplicate = false;
                foreach (T prior in unique)
                    if (ReferenceEquals(prior, item)) { duplicate = true; break; }
                if (!duplicate) unique.Add(item);
            }
            bool rosterChanged = unique.Count != items.Count;
            if (!rosterChanged)
                for (int i = 0; i < items.Count; i++)
                    if (!ReferenceEquals(unique[i], items[i])) { rosterChanged = true; break; }
            T old = selected;
            items.Clear();
            items.AddRange(unique);
            if (SelectedIndex < 0) selected = items.Count == 0 ? null : items[0];
            return rosterChanged || !ReferenceEquals(old, selected);
        }

        internal bool Select(int index) {
            T next = At(index);
            if (next == null || ReferenceEquals(next, selected)) return false;
            selected = next;
            return true;
        }
    }
}
