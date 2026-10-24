using System;

namespace MaximumUI {
    /// <summary>Keeps model synchronization separate from a field's in-progress edit and commit.</summary>
    internal static class TextEditingLifecycle {
        public static bool Synchronize(TextFieldState state, string externalValue, bool focusedBefore,
            TextInputFlags flags, Func<char, bool> charFilter, int maxLength) {
            bool wasFocused = state.Focused || state.WasFocused || focusedBefore;
            bool committed = state.SynchronizeCommittedValue && state.CommitEditRevision == state.EditRevision;
            // OnBlur runs after rendering. Its last draft must survive the event
            // that transfers focus, including a transfer to another mount.
            if (!state.HasEditingValue || committed || (!focusedBefore && !wasFocused)) {
                string value = externalValue ?? string.Empty;
                if (!state.HasEditingValue || !string.Equals(state.EditingValue, value, StringComparison.Ordinal))
                    state.EditingValue = TextInputFilter.Apply(value, flags, charFilter, maxLength);
                state.HasEditingValue = true;
                if (committed && focusedBefore) {
                    state.FocusStartValue = state.EditingValue;
                    state.HasFocusStartValue = true;
                }
            }
            state.SynchronizeCommittedValue = false;
            return wasFocused;
        }

        public static void Edit(TextFieldState state, string value) {
            string next = value ?? string.Empty;
            if (string.Equals(state.EditingValue, next, StringComparison.Ordinal)) return;
            state.EditingValue = next;
            state.HasEditingValue = true;
            unchecked { state.EditRevision++; }
            state.SynchronizeCommittedValue = false;
        }

        public static void CompleteCommit(TextFieldState state, int editRevision) {
            // Callbacks are deferred until Update. A previously submitted value
            // must not replace any newer keystrokes entered before it completes.
            if (state.EditRevision != editRevision) return;
            state.CommitEditRevision = editRevision;
            state.SynchronizeCommittedValue = true;
        }

        public static Action CommitCallback(TextFieldState state, Action<string> callback) {
            if (callback == null) return null;
            string value = state.EditingValue ?? string.Empty;
            int revision = state.EditRevision;
            return delegate {
                callback(value);
                CompleteCommit(state, revision);
            };
        }
    }
}
