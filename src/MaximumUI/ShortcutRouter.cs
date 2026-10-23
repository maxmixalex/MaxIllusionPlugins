using System;
using System.Collections.Generic;

namespace MaximumUI {
    public sealed class ShortcutChord {
        public int KeyCode;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool Command;

        public ShortcutChord() {
        }

        public ShortcutChord(int keyCode, bool control = false, bool shift = false, bool alt = false, bool command = false) {
            KeyCode = keyCode;
            Control = control;
            Shift = shift;
            Alt = alt;
            Command = command;
        }

        public bool Matches(MuiInputEvent input) {
            if (input == null || input.Type != MuiEventType.KeyDown) return false;
            if (input.KeyCode != KeyCode) return false;
            if (Control && !input.Control) return false;
            if (Shift && !input.Shift) return false;
            if (Alt && !input.Alt) return false;
            if (Command && !input.Command) return false;
            return true;
        }

        public string DisplayText {
            get {
                string text = string.Empty;
                if (Control) text += "Ctrl+";
                if (Shift) text += "Shift+";
                if (Alt) text += "Alt+";
                if (Command) text += "Cmd+";
                return text + KeyCode.ToString();
            }
        }
    }

    public sealed class ShortcutBinding {
        public string Id;
        public ShortcutChord Chord;
        public Action Action;
        public bool Enabled;
        public bool AllowWhenTextInput;
        public object Owner;
    }

    public sealed class UiShortcutRouter {
        private readonly List<ShortcutBinding> bindings = new List<ShortcutBinding>();

        public string LastTriggeredId;
        public ShortcutChord LastTriggeredChord;
        public int TriggerCount;

        public int Count {
            get { return bindings.Count; }
        }

        public void BeginBuild() {
            bindings.Clear();
        }

        public ShortcutBinding Register(string id, ShortcutChord chord, Action action, bool enabled = true, bool allowWhenTextInput = false, object owner = null) {
            if (chord == null || action == null) return null;

            ShortcutBinding binding = new ShortcutBinding {
                Id = id ?? string.Empty,
                Chord = chord,
                Action = action,
                Enabled = enabled,
                AllowWhenTextInput = allowWhenTextInput,
                Owner = owner
            };
            bindings.Add(binding);
            return binding;
        }

        public ShortcutBinding Resolve(MuiInputEvent input, bool textInputActive) {
            if (input == null || input.Type != MuiEventType.KeyDown) return null;

            for (int i = bindings.Count - 1; i >= 0; i--) {
                ShortcutBinding binding = bindings[i];
                if (binding == null || !binding.Enabled || binding.Chord == null || binding.Action == null) continue;
                if (textInputActive && !binding.AllowWhenTextInput) continue;
                if (binding.Chord.Matches(input)) return binding;
            }

            return null;
        }

        public void MarkTriggered(ShortcutBinding binding) {
            if (binding == null) return;

            LastTriggeredId = binding.Id;
            LastTriggeredChord = binding.Chord;
            TriggerCount++;
        }

        public IList<ShortcutBinding> Snapshot() {
            return new List<ShortcutBinding>(bindings);
        }
    }
}
