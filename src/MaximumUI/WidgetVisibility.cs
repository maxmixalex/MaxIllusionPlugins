using System;
using System.Collections.Generic;

namespace MaximumUI {
    internal static class WidgetVisibility {
        // Structural prefilter: a retained root with no Dialog descendants
        // cannot become modal through a tab/fold state change alone. Runtime
        // computes this only when a view rebuilds; dynamic modal visibility
        // is still evaluated normally when any Dialog exists.
        public static bool MayContainDialog(Widget root) {
            if (root == null || root is EmptyWidget) return false;
            if (root is Dialog) return true;
            for (int i = 0; i < root.Children.Count; i++)
                if (MayContainDialog(root.Children[i])) return true;
            return false;
        }

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
            return ContainsDialog(widget, null);
        }

        // The renderer inspects nested trees multiple times while a modal
        // blocks background controls. Cache every subtree for ONE GUI event
        // so a deep chain cannot be rescanned quadratically. Keep the public
        // uncached form for isolated callers and runtime modal checks.
        internal static bool ContainsDialog(Widget widget,
            Dictionary<Widget, bool> memo) {
            if (widget == null || widget is EmptyWidget) return false;
            bool cached;
            if (memo != null && memo.TryGetValue(widget, out cached))
                return cached;
            bool result = ContainsDialogUncached(widget, memo);
            if (memo != null) memo[widget] = result;
            return result;
        }

        private static bool ContainsDialogUncached(Widget widget,
            Dictionary<Widget, bool> memo) {
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
                    if (ordinal++ == selected) return ContainsDialog(candidate, memo);
                }
                return false;
            }

            for (int i = 0; i < widget.Children.Count; i++) {
                if (ContainsDialog(widget.Children[i], memo)) return true;
            }
            return false;
        }
    }
}
