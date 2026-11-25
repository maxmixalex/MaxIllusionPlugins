using System;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private Exception presetReadError;

        private void ReloadMaximumUiPresets() {
            string selected = currentProgram == null ? string.Empty : currentProgram.guid;
            try { xml.Load(); presetReadError = null; }
            catch (Exception error) { presetReadError = error; throw; }
            int index = xml.programs.FindIndex(p => p.guid == selected);
            if (xml.programs.Count > 0) SelectProgram(Math.Max(0, index));
            else { currentProgram = new Program(); currentProgramNum = 0; selectedProgramUnit = 0; }
            SelectScenePreset(selectedScenePreset);
            if (hotkey != null) hotkey.Set(hotkeyShowGUI, xml.keyConfig.showGUI);
            isSortExpressionModify = false;
        }
    }
}
