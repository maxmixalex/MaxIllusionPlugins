using UnityEngine;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private bool pendingGuiToggle;

        public void LateUpdate() {
            ShortCutKeyMgr.ShortCutKey shortcut = this.hotkey == null ? null : this.hotkey[hotkeyShowGUI];
            if (shortcut != null && shortcut.GetKeyState()) {
                // Named GUIStyles resolve through the current IMGUI skin. There
                // is no valid skin context in LateUpdate, especially after a
                // scene switch. Only queue visibility here; create in OnGUI.
                this.pendingGuiToggle = !this.pendingGuiToggle;
                this.UpdateFemale();
            }
            this.Proc();
        }

        public void OnGUI() {
            if (!this.pendingGuiToggle && (this.gui == null || !this.gui.show)) return;
            Color oldColor = GUI.color;
            Color oldContent = GUI.contentColor;
            Color oldBackground = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try {
                if (this.pendingGuiToggle) {
                    this.pendingGuiToggle = false;
                    if (this.gui == null)
                        this.gui = new GUIMgr(WINDOW_ID, new Rect(0f, 0f, 20f, 50f),
                            ExpressionControlPlugin.Name + ExpressionControlPlugin.Version);
                    this.gui.show = !this.gui.show;
                }
                if (this.gui != null && this.gui.show) this.gui.Draw();
            } finally {
                GUI.color = oldColor;
                GUI.contentColor = oldContent;
                GUI.backgroundColor = oldBackground;
                GUI.enabled = oldEnabled;
            }
        }
    }
}
