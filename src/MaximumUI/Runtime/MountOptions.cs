using UnityEngine;

namespace MaximumUI {
    public sealed class MountOptions {
        public bool Visible;
        public KeyCode ToggleKey;
        public bool HasToggleKey;
        public bool BuildEveryOnGUI;
        public bool SoftMouseCapture;
        public bool SoftKeyboardCapture;
        public int SortOrder;
        public MaximumTheme Theme;

        public MountOptions() {
            Visible = true;
            HasToggleKey = false;
            BuildEveryOnGUI = true;
            SoftMouseCapture = true;
            SoftKeyboardCapture = false;
            SortOrder = 0;
            Theme = null;
        }
    }
}
