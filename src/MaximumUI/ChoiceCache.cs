using System;
using System.Collections;
using System.Collections.Generic;

namespace MaximumUI {
    // Enumerate and format retained choices once. Filtering never calls user formatters
    // again, and an unchanged filter reuses the same arrays across GUI events.
    internal sealed class ChoiceCache {
        private object owner;
        private IEnumerable source;
        private int revision;
        private int sourceCount = -1;
        private bool initialized;
        private string filter;
        private readonly List<object> allValues = new List<object>();
        private readonly List<string> allLabels = new List<string>();
        private readonly List<object> filteredValues = new List<object>();
        private readonly List<string> filteredLabels = new List<string>();
        private static readonly string[] EmptyLabels = new string[0];

        public IList<object> Values { get { return filteredValues; } }
        public string[] Labels { get; private set; }
        public long EnumerationCount { get; private set; }
        public long FilterCount { get; private set; }

        public ChoiceCache() { Labels = EmptyLabels; }

        public bool Refresh(object nextOwner, IEnumerable nextSource, Func<object, string> format,
            string nextFilter, int nextRevision, bool cache) {
            ICollection collection = nextSource as ICollection;
            int count = collection == null ? -1 : collection.Count;
            bool changed = !cache || !initialized || !ReferenceEquals(owner, nextOwner) ||
                !ReferenceEquals(source, nextSource) || revision != nextRevision || sourceCount != count;
            if (changed) {
                // Publish a complete snapshot only. A failing enumerable/formatter must
                // be retried, rather than marking a partial list as a valid cache hit.
                initialized = false;
                allValues.Clear();
                allLabels.Clear();
                if (nextSource != null) {
                    foreach (object value in nextSource) {
                        allValues.Add(value);
                        allLabels.Add((format == null ? Convert.ToString(value) : format(value)) ?? string.Empty);
                    }
                }
                owner = nextOwner;
                source = nextSource;
                revision = nextRevision;
                sourceCount = count;
                initialized = true;
                EnumerationCount++;
            }
            nextFilter = nextFilter ?? string.Empty;
            if (!changed && string.Equals(filter, nextFilter, StringComparison.Ordinal)) return false;
            filter = nextFilter;
            filteredValues.Clear();
            filteredLabels.Clear();
            for (int i = 0; i < allValues.Count; i++) {
                if (filter.Length > 0 && allLabels[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                filteredValues.Add(allValues[i]);
                filteredLabels.Add(allLabels[i]);
            }
            Labels = filteredLabels.Count == 0 ? EmptyLabels : filteredLabels.ToArray();
            FilterCount++;
            return true;
        }
    }
}
