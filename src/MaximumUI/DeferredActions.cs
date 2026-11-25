using System;
using System.Collections.Generic;

namespace MaximumUI {
    internal sealed class DeferredActions {
        private List<Action> pending = new List<Action>();
        private List<Action> executing = new List<Action>();
        private bool flushing;
        public int Count { get { return pending.Count; } }

        public void Add(Action action) { if (action != null) pending.Add(action); }

        public void Flush(Action<Exception> report) {
            if (flushing || pending.Count == 0) return;
            List<Action> swap = executing;
            executing = pending;
            pending = swap;
            flushing = true;
            try {
                for (int i = 0; i < executing.Count; i++) {
                    try { executing[i](); }
                    catch (Exception ex) { if (report != null) report(ex); }
                }
            } finally {
                executing.Clear();
                flushing = false;
            }
        }
    }
}
