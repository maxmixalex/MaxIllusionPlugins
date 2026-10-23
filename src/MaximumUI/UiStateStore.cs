using System.Collections.Generic;

namespace MaximumUI {
    public sealed class UiStateStore {
        private readonly Dictionary<string, object> states = new Dictionary<string, object>();

        public T GetOrCreate<T>(string id) where T : new() {
            object value;
            if (states.TryGetValue(id, out value)) return (T)value;

            T created = new T();
            states[id] = created;
            return created;
        }

        public void Remove(string id) {
            states.Remove(id);
        }

        public void Clear() {
            states.Clear();
        }

        public int Count {
            get { return states.Count; }
        }

        public IList<UiStateEntry> Snapshot() {
            List<UiStateEntry> result = new List<UiStateEntry>();
            foreach (KeyValuePair<string, object> pair in states) {
                result.Add(new UiStateEntry {
                    Id = pair.Key,
                    TypeName = pair.Value == null ? "null" : pair.Value.GetType().Name
                });
            }

            return result;
        }
    }
}
