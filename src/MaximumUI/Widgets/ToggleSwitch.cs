using System;

namespace MaximumUI {
    /// <summary>A binary setting whose change takes effect immediately; separate from list selection checkboxes.</summary>
    public sealed class ToggleSwitch : Widget {
        public string Text;
        public bool Value;
        public Action<bool> OnChange;

        public ToggleSwitch(string text, bool value, Action<bool> onChange = null, object key = null) : base(key) {
            Text = text ?? string.Empty;
            Value = value;
            OnChange = onChange;
        }
    }
}
