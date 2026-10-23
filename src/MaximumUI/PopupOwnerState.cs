using System;
using System.Collections.Generic;

namespace MaximumUI {
    /// <summary>Keeps popup input tied to its live owner independently of IMGUI input suppression.</summary>
    internal sealed class PopupOwnerState {
        private readonly List<Widget> ancestors = new List<Widget>(4);
        private readonly List<Widget> popupPath = new List<Widget>(2);
        private bool rootEnabled;
        private int version;
        private bool lastEnabled;

        public bool Open { get; private set; }
        public int Version { get { return version; } }
        public bool RootEnabled { get { return rootEnabled; } }
        public bool IsEnabled {
            get {
                if (!rootEnabled || ancestors.Count == 0) return false;
                for (int i = 0; i < ancestors.Count; i++)
                    if (ancestors[i] == null || !ancestors[i].Enabled) return false;
                Widget parent = ancestors[ancestors.Count - 1];
                for (int i = 0; i < popupPath.Count; i++) {
                    Widget child = popupPath[i];
                    if (child == null || !child.Enabled || !parent.Children.Contains(child)) return false;
                    parent = child;
                }
                return true;
            }
        }

        public bool Capture(IList<Widget> chain, bool enabled) {
            int count = chain == null ? 0 : chain.Count;
            bool changed = rootEnabled != enabled || ancestors.Count != count;
            if (!changed) for (int i = 0; i < count; i++)
                if (!ReferenceEquals(ancestors[i], chain[i])) { changed = true; break; }
            if (changed) {
                ancestors.Clear();
                for (int i = 0; i < count; i++) ancestors.Add(chain[i]);
                popupPath.Clear();
                unchecked { version++; }
            }
            rootEnabled = enabled;
            bool valid = IsEnabled;
            if (!valid && (lastEnabled || Open)) Invalidate();
            lastEnabled = valid;
            return valid;
        }

        public bool CapturePopupPath(IList<Widget> chain, int start, int count) {
            bool changed = popupPath.Count != count;
            if (!changed) for (int i = 0; i < count; i++)
                if (!ReferenceEquals(popupPath[i], chain[start + i])) { changed = true; break; }
            if (changed) {
                popupPath.Clear();
                for (int i = 0; i < count; i++) popupPath.Add(chain[start + i]);
                unchecked { version++; }
            }
            if (!IsEnabled) { Invalidate(); return false; }
            return true;
        }

        public bool BeginOpen() {
            if (!IsEnabled) { Invalidate(); return false; }
            unchecked { version++; }
            Open = true;
            return true;
        }

        // Selection closes its surface before its queued action is delivered.
        // Keep that action valid unless its owner changes, disables, or reopens.
        public void Close() { Open = false; }

        public void Invalidate() {
            unchecked { version++; }
            Open = false;
        }

        public bool CanInvoke(int capturedVersion) { return capturedVersion == version && IsEnabled; }

        public Action Guard(Action callback, Widget actionSource = null) {
            if (callback == null) return null;
            int captured = version;
            return delegate {
                if (CanInvoke(captured) && (actionSource == null || actionSource.Enabled)) callback();
            };
        }

        public void AppendAncestorsTo(List<Widget> destination) {
            for (int i = 0; i < ancestors.Count; i++) destination.Add(ancestors[i]);
        }
    }
}
