using MaximumUI;

namespace ExpressionControl {
    public partial class ExpressionControl {
        private const string MaximumUiMountId = "MaxMixAlex.ExpressionControl.editor";
        private ExpressionEditorView maximumView;

        private bool IsMaximumUiInputBusy() {
            return MaximumUIRuntime.IsAnyTextFieldFocused || MaximumUIRuntime.IsModalOpen;
        }

        private void ToggleMaximumUi() {
            if (maximumView == null) maximumView = new ExpressionEditorView(this);
            if (!MaximumUIRuntime.IsMounted(MaximumUiMountId)) {
                MaximumUIRuntime.EnsureHost();
                MaximumUIRuntime.Mount(MaximumUiMountId, maximumView, new MountOptions {
                    Visible = false, BuildEveryFrame = false, Theme = MaximumTheme.MetroLight,
                    SortOrder = 340, SoftMouseCapture = true, SoftKeyboardCapture = true
                });
            }
            MaximumUIRuntime.SetVisible(MaximumUiMountId, !MaximumUIRuntime.IsVisible(MaximumUiMountId));
            maximumView.Invalidate();
        }

        private void HideMaximumUi() {
            MaximumUIRuntime.SetVisible(MaximumUiMountId, false);
        }

        private void UnmountMaximumUi() {
            MaximumUIRuntime.Unmount(MaximumUiMountId);
            maximumView = null;
        }


    }
}
