using MaximumUI;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private const string MaximumUiMountId = "MaxMixAlex.ExpressionControl.editor";
        private const string MaximumUiProgramMountId = "MaxMixAlex.ExpressionControl.program";
        private ExpressionEditorView maximumView;
        private ExpressionEditorView maximumProgramView;
        private bool maximumProgramOpen;

        private bool IsMaximumUiInputBusy() {
            return MaximumUIRuntime.IsAnyTextFieldFocused || MaximumUIRuntime.IsModalOpen;
        }

        private void EnsureMaximumUi() {
            if (maximumView == null) maximumView = new ExpressionEditorView(this);
            if (MaximumUIRuntime.IsMounted(MaximumUiMountId)) return;
            MaximumUIRuntime.EnsureHost();
            MaximumUIRuntime.Mount(MaximumUiMountId, maximumView, new MountOptions {
                Visible = false, BuildEveryFrame = false, Theme = MaximumTheme.MetroLight,
                SortOrder = 340, SoftMouseCapture = true, SoftKeyboardCapture = true
            });
        }

        private void EnsureMaximumUiProgram() {
            if (maximumProgramView == null) maximumProgramView = new ExpressionEditorView(this, true);
            if (MaximumUIRuntime.IsMounted(MaximumUiProgramMountId)) return;
            MaximumUIRuntime.EnsureHost();
            MaximumUIRuntime.Mount(MaximumUiProgramMountId, maximumProgramView, new MountOptions {
                Visible = false, BuildEveryFrame = false, Theme = MaximumTheme.MetroLight,
                SortOrder = 341, SoftMouseCapture = true, SoftKeyboardCapture = true
            });
        }

        private void ToggleMaximumUi() {
            EnsureMaximumUi();
            SetMaximumUiVisible(!MaximumUIRuntime.IsVisible(MaximumUiMountId));
        }

        private void SetMaximumUiVisible(bool visible) {
            MaximumUIRuntime.SetVisible(MaximumUiMountId, visible);
            // The legacy GUI keeps the program window's open state while the
            // global shortcut hides both windows, and restores both together.
            if (visible && maximumProgramOpen) EnsureMaximumUiProgram();
            MaximumUIRuntime.SetVisible(MaximumUiProgramMountId, visible && maximumProgramOpen);
            InvalidateMaximumUiViews();
        }

        private void ToggleMaximumUiProgram() {
            SetMaximumUiProgramVisible(!maximumProgramOpen);
        }

        private void SetMaximumUiProgramVisible(bool visible) {
            maximumProgramOpen = visible;
            if (visible) EnsureMaximumUiProgram();
            MaximumUIRuntime.SetVisible(MaximumUiProgramMountId,
                visible && MaximumUIRuntime.IsVisible(MaximumUiMountId));
            InvalidateMaximumUiViews();
        }

        private void InvalidateMaximumUiViews() {
            if (maximumView != null) MaximumUIRuntime.MarkDirty(maximumView);
            if (maximumProgramView != null) MaximumUIRuntime.MarkDirty(maximumProgramView);
        }

        private void HideMaximumUi() {
            maximumProgramOpen = false;
            SetMaximumUiVisible(false);
        }

        private void UnmountMaximumUi() {
            MaximumUIRuntime.Unmount(MaximumUiProgramMountId);
            MaximumUIRuntime.Unmount(MaximumUiMountId);
            maximumProgramOpen = false;
            maximumProgramView = null;
            maximumView = null;
        }
    }
}
