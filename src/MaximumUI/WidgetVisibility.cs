using System;

namespace MaximumUI {
    internal static class WidgetVisibility {
        public static bool ContainsRootDialog(Widget root, RenderContext context, string rootPath) {
            // RenderWindow stores folding outside the declarative tree and skips
            // its entire content in DrawWindowContents while folded.
            Window window = root as Window;
            if (window != null) {
                if (!window.Visible) return false;
                WindowState state;
                if (context != null && context.StateStore != null &&
                    context.StateStore.TryGet(context.StateId(window, "window", rootPath), out state) && state.Folded) return false;
            }
            return ContainsDialog(root);
        }

        public static bool ContainsDialog(Widget widget) {
            if (widget == null || widget is EmptyWidget) return false;
            if (widget is Dialog) return true;

            Window window = widget as Window;
            if (window != null && !window.Visible) return false;
            Tab tab = widget as Tab;
            if (tab != null && !tab.Visible) return false;
            Foldout foldout = widget as Foldout;
            if (foldout != null && !foldout.Open) return false;
            CollapsingHeader header = widget as CollapsingHeader;
            if (header != null && !header.Open) return false;
            ClosableCollapsingHeader closable = widget as ClosableCollapsingHeader;
            if (closable != null && (!closable.Visible || !closable.Open)) return false;
            TreeNode tree = widget as TreeNode;
            if (tree != null && (!tree.Open || tree.Leaf)) return false;

            Tabs tabs = widget as Tabs;
            if (tabs != null) {
                int count = 0;
                for (int i = 0; i < tabs.Children.Count; i++) {
                    Tab candidate = tabs.Children[i] as Tab;
                    if (candidate != null && candidate.Visible) count++;
                }
                int selected = Math.Max(0, Math.Min(tabs.SelectedIndex, count - 1));
                int ordinal = 0;
                for (int i = 0; i < tabs.Children.Count; i++) {
                    Tab candidate = tabs.Children[i] as Tab;
                    if (candidate == null || !candidate.Visible) continue;
                    if (ordinal++ == selected) return ContainsDialog(candidate);
                }
                return false;
            }

            for (int i = 0; i < widget.Children.Count; i++) {
                if (ContainsDialog(widget.Children[i])) return true;
            }
            return false;
        }
    }
}
