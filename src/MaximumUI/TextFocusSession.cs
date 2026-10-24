using System;

namespace MaximumUI {
    /// <summary>Retains editing ownership while a virtual row is outside the render range.</summary>
    internal sealed class TextFocusSession {
        private TextFieldState state;
        private Action<string> onBlur;
        private Widget source;
        private PopupOwnerState popupOwner;
        private int popupVersion;

        public bool Owns(TextFieldState candidate) { return ReferenceEquals(state, candidate); }

        public Action Track(TextFieldState candidate, Action<string> callback, Widget actionSource, PopupOwnerState owner) {
            Action previous = ReferenceEquals(state, candidate) ? null : TakeBlur();
            state = candidate;
            onBlur = callback;
            source = actionSource;
            popupOwner = owner;
            popupVersion = owner == null ? 0 : owner.Version;
            return previous;
        }

        public Action TakeBlur() {
            if (state == null) return null;
            Action commit = TextEditingLifecycle.CommitCallback(state, onBlur);
            Widget capturedSource = source;
            PopupOwnerState capturedOwner = popupOwner;
            int capturedVersion = popupVersion;
            Reset();
            if (commit == null) return null;
            return delegate {
                if ((capturedSource == null || capturedSource.Enabled) &&
                    (capturedOwner == null || capturedOwner.CanInvoke(capturedVersion))) commit();
            };
        }

        public void Reset() {
            if (state != null) {
                state.Focused = false;
                state.WasFocused = false;
                state.HasFocusStartValue = false;
            }
            state = null;
            onBlur = null;
            source = null;
            popupOwner = null;
            popupVersion = 0;
        }
    }
}
