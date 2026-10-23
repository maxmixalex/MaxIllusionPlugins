using System;

namespace JuiceControl {
    // A retained control tree may still have callbacks queued after a reload,
    // character replacement or component shutdown. Tokens follow target identity,
    // so a normal display-value refresh does not discard a pending user edit.
    internal sealed class JuiceEditorSession {
        private object generation = new object();
        private object[] characters = new object[0];
        private object[] targets = new object[0];
        private string path;
        private bool naming;

        internal int CharacterCount { get { return characters.Length; } }

        internal bool MatchesCharacterAt(int index, object character) {
            return index >= 0 && index < characters.Length && ReferenceEquals(characters[index], character);
        }

        internal void Invalidate() {
            generation = new object();
            characters = new object[0];
            targets = new object[0];
            path = null;
            naming = false;
        }

        internal void Close(Action dismissNameDialog) {
            Invalidate();
            if (dismissNameDialog != null) dismissNameDialog();
        }

        internal void Bind(JuiceEditorModel model, object[] characterIdentities, object[] targetIdentities, string sourcePath,
            Func<bool> isActive, Func<bool> targetsCurrent) {
            if (!SameIdentities(characters, characterIdentities) || !SameIdentities(targets, targetIdentities) ||
                !string.Equals(path, sourcePath, StringComparison.Ordinal) || naming != model.NameDialogVisible) {
                Invalidate();
                characters = characterIdentities == null ? new object[0] : (object[])characterIdentities.Clone();
                targets = targetIdentities == null ? new object[0] : (object[])targetIdentities.Clone();
                path = sourcePath;
                naming = model.NameDialogVisible;
            }
            object token = generation;
            Action<JuiceEditorCommand> execute = model.Execute;
            model.Execute = delegate(JuiceEditorCommand command) {
                if (!IsCurrent(token, isActive)) return;
                // Dismissing the UI remains possible if its business target has
                // just disappeared; an older generation still cannot dismiss it.
                if (command != JuiceEditorCommand.Close && command != JuiceEditorCommand.CancelName &&
                    targetsCurrent != null && !targetsCurrent()) return;
                if (execute != null) execute(command);
            };
            model.SelectCharacter = Guard(token, isActive, targetsCurrent, model.SelectCharacter);
            model.SelectCharacterPreset = Guard(token, isActive, targetsCurrent, model.SelectCharacterPreset);
            model.SelectGroupPreset = Guard(token, isActive, targetsCurrent, model.SelectGroupPreset);
            model.ChangeName = Guard(token, isActive, targetsCurrent, model.ChangeName);
            model.LockAll = Guard(token, isActive, targetsCurrent, model.LockAll);
            for (int i = 0; i < model.Effects.Count; i++) {
                JuiceEffectRow row = model.Effects[i];
                row.Change = Guard(token, isActive, targetsCurrent, row.Change);
                row.Lock = Guard(token, isActive, targetsCurrent, row.Lock);
            }
        }

        private bool IsCurrent(object token, Func<bool> isActive) {
            return ReferenceEquals(token, generation) && (isActive == null || isActive());
        }

        private static bool SameIdentities(object[] previous, object[] current) {
            if (current == null) return previous.Length == 0;
            if (previous.Length != current.Length) return false;
            for (int i = 0; i < previous.Length; i++)
                if (!ReferenceEquals(previous[i], current[i])) return false;
            return true;
        }

        private Action<T> Guard<T>(object token, Func<bool> isActive, Func<bool> targetsCurrent, Action<T> action) {
            if (action == null) return null;
            return delegate(T value) {
                if (IsCurrent(token, isActive) && (targetsCurrent == null || targetsCurrent())) action(value);
            };
        }
    }
}
