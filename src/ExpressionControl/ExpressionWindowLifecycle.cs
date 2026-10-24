using UnityEngine;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private bool pendingGuiToggle;

        public void LateUpdate() {
            ShortCutKeyMgr.ShortCutKey shortcut = this.hotkey == null ? null : this.hotkey[hotkeyShowGUI];
            if (shortcut != null && !IsMaximumUiInputBusy() && shortcut.GetKeyState()) {
                // First mount still happens in OnGUI: native theme resources
                // need a valid skin. A typed L or a modal must not toggle this.
                this.pendingGuiToggle = !this.pendingGuiToggle;
                this.UpdateFemale();
            }
            this.Proc();
        }

        public void OnGUI() {
            if (!this.pendingGuiToggle) return;
            Color oldColor = GUI.color;
            Color oldContent = GUI.contentColor;
            Color oldBackground = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try {
                this.pendingGuiToggle = false;
                ToggleMaximumUi();
            } finally {
                GUI.color = oldColor;
                GUI.contentColor = oldContent;
                GUI.backgroundColor = oldBackground;
                GUI.enabled = oldEnabled;
            }
        }

        public void OnDestroy() {
            this.pendingGuiToggle = false;
            UnmountMaximumUi();
        }
    }
}
