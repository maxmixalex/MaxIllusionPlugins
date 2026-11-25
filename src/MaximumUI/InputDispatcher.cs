using System;

namespace MaximumUI {
    public sealed class InputDispatchResult {
        public string HoveredPath;
        public string ActivePath;
        public string FocusedPath;
        public string ClickedPath;
        public Widget HoveredWidget;
        public Widget ActiveWidget;
        public Widget FocusedWidget;
        public Widget ClickedWidget;
        public bool Consumed;
    }

    public sealed class InputDispatcher {
        private string activePath;
        private string focusedPath;

        public InputDispatchResult Dispatch(MuiInputEvent input, LayoutNode root) {
            InputDispatchResult result = new InputDispatchResult {
                ActivePath = activePath,
                FocusedPath = focusedPath
            };

            if (input == null || root == null) return result;

            // A retained view can disable a control (or its containing panel)
            // between press and release. Old focus must not consume keyboard
            // input or complete a click after that change.
            if (!string.IsNullOrEmpty(activePath) && GetInteractiveWidget(root, activePath) == null) activePath = null;
            if (!string.IsNullOrEmpty(focusedPath) && GetInteractiveWidget(root, focusedPath) == null) focusedPath = null;
            result.ActivePath = activePath;
            result.FocusedPath = focusedPath;

            LayoutNode hovered = HitTest(root, input.MousePosition);
            if (hovered != null) {
                result.HoveredPath = hovered.Path;
                result.HoveredWidget = hovered.Widget;
            }

            if (input.Type == MuiEventType.MouseDown && input.MouseButton == 0 && hovered != null && IsInteractive(hovered.Widget)) {
                activePath = hovered.Path;
                focusedPath = hovered.Path;
                result.ActivePath = activePath;
                result.FocusedPath = focusedPath;
                result.ActiveWidget = hovered.Widget;
                result.FocusedWidget = hovered.Widget;
                result.Consumed = true;
            } else if (input.Type == MuiEventType.MouseUp && input.MouseButton == 0) {
                LayoutNode active = string.IsNullOrEmpty(activePath) ? null : root.Find(activePath);
                if (active != null && hovered != null && string.Equals(active.Path, hovered.Path, StringComparison.Ordinal)) {
                    result.ClickedPath = active.Path;
                    result.ClickedWidget = active.Widget;
                    result.Consumed = true;
                }

                activePath = null;
                result.ActivePath = null;
            } else if (input.Type == MuiEventType.KeyDown || input.Type == MuiEventType.TextInput) {
                result.FocusedPath = focusedPath;
                result.FocusedWidget = string.IsNullOrEmpty(focusedPath) ? null : GetInteractiveWidget(root, focusedPath);
                result.Consumed = result.FocusedWidget != null;
            } else {
                result.ActivePath = activePath;
                result.FocusedPath = focusedPath;
                result.ActiveWidget = string.IsNullOrEmpty(activePath) ? null : GetInteractiveWidget(root, activePath);
                result.FocusedWidget = string.IsNullOrEmpty(focusedPath) ? null : GetInteractiveWidget(root, focusedPath);
            }

            return result;
        }

        public void ClearCapture() {
            activePath = null;
        }

        public void ClearFocus() {
            focusedPath = null;
        }

        private static Widget GetInteractiveWidget(LayoutNode root, string path) {
            if (root == null || root.Widget == null || !root.Widget.Enabled) return null;
            if (string.Equals(root.Path, path, StringComparison.Ordinal)) return IsInteractive(root.Widget) ? root.Widget : null;
            for (int i = 0; i < root.Children.Count; i++) {
                Widget found = GetInteractiveWidget(root.Children[i], path);
                if (found != null) return found;
            }
            return null;
        }

        private static LayoutNode HitTest(LayoutNode node, MuiVec2 point) {
            if (node == null || node.Widget == null || !node.Widget.Enabled || !node.Rect.Contains(point)) return null;

            for (int i = node.Children.Count - 1; i >= 0; i--) {
                LayoutNode child = HitTest(node.Children[i], point);
                if (child != null) return child;
            }

            return node;
        }

        private static bool IsInteractive(Widget widget) {
            return widget is Button ||
                   widget is SmallButton ||
                   widget is ArrowButton ||
                   widget is ImageButton ||
                   widget is TextLink ||
                   widget is Toggle ||
                   widget is ToggleSwitch ||
                   widget is Checkbox ||
                   widget is RadioButton ||
                   widget is ISegmentedControlWidget ||
                   widget is Slider ||
                   widget is InputInt ||
                   widget is InputFloat ||
                   widget is InputDouble ||
                   widget is DragInt ||
                   widget is DragFloat ||
                   widget is SliderInt ||
                   widget is SliderFloat ||
                   widget is SliderAngle ||
                   widget is TextField ||
                   widget is TextArea ||
                   widget is PasswordField ||
                   widget is SearchBox ||
                   widget is IComboBoxWidget ||
                   widget is IListBoxWidget ||
                   widget is ThemeEditor ||
                   widget is Foldout ||
                   widget is CollapsingHeader ||
                   widget is ClosableCollapsingHeader ||
                   widget is MenuBar ||
                   widget is Selectable ||
                   widget is TreeNode ||
                   widget is TreeLeaf ||
                   widget is ColorButton ||
                   widget is ColorEdit ||
                   widget is ColorEdit3 ||
                   widget is ColorEdit4 ||
                   widget is ColorPicker3 ||
                   widget is ColorPicker4 ||
                   widget is VectorField ||
                   widget is VirtualList ||
                   widget is TwoPaneLayout ||
                   widget is DragSource ||
                   widget is DropTarget ||
                   widget is Tabs;
        }
    }
}
