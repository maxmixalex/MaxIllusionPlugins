using System;
using System.Collections.Generic;

namespace JuiceControl {
    internal delegate bool ReadLevel(string id, out int level);

    // Shared, game-independent policy: an unavailable value cannot replace a
    // previously observed value. A successfully read zero is still valid.
    internal static class ReadbackState {
        internal static bool TryConvert(object raw, int maximum, out int level) {
            level = 0;
            if (raw == null || maximum < 0) return false;
            try {
                int value = Convert.ToInt32(raw);
                if (value < 0 || value > maximum) return false;
                level = value;
                return true;
            } catch (InvalidCastException) { return false; }
              catch (FormatException) { return false; }
              catch (OverflowException) { return false; }
        }

        internal static void RefreshKnown(Dictionary<string, int> levels, ReadLevel read) {
            foreach (string id in new List<string>(levels.Keys)) {
                int value;
                if (read(id, out value)) levels[id] = value;
            }
        }

        internal static void CaptureKnown(Dictionary<string, int> levels, IEnumerable<string> ids, ReadLevel read) {
            Dictionary<string, int> next = new Dictionary<string, int>(levels.Comparer);
            foreach (string id in ids) {
                int value;
                if (read(id, out value) || levels.TryGetValue(id, out value))
                    next[id] = value;
            }
            levels.Clear();
            foreach (KeyValuePair<string, int> pair in next) levels[pair.Key] = pair.Value;
        }
    }
}
