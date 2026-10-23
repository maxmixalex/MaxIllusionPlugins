namespace MaximumUI {
    public static class LayoutExtensions {
        public static T Width<T>(this T widget, float width) where T : Widget {
            widget.Layout.HasWidth = true;
            widget.Layout.Width = width;
            return widget;
        }

        public static T Height<T>(this T widget, float height) where T : Widget {
            widget.Layout.HasHeight = true;
            widget.Layout.Height = height;
            return widget;
        }

        public static T MinWidth<T>(this T widget, float width) where T : Widget {
            widget.Layout.HasMinWidth = true;
            widget.Layout.MinWidth = width;
            return widget;
        }

        public static T MaxWidth<T>(this T widget, float width) where T : Widget {
            widget.Layout.HasMaxWidth = true;
            widget.Layout.MaxWidth = width;
            return widget;
        }

        public static T MinHeight<T>(this T widget, float height) where T : Widget {
            widget.Layout.HasMinHeight = true;
            widget.Layout.MinHeight = height;
            return widget;
        }

        public static T MaxHeight<T>(this T widget, float height) where T : Widget {
            widget.Layout.HasMaxHeight = true;
            widget.Layout.MaxHeight = height;
            return widget;
        }

        public static T ExpandWidth<T>(this T widget) where T : Widget {
            widget.Layout.ExpandWidth = true;
            return widget;
        }

        public static T ExpandHeight<T>(this T widget) where T : Widget {
            widget.Layout.ExpandHeight = true;
            return widget;
        }

        public static T Enabled<T>(this T widget, bool enabled) where T : Widget {
            widget.Enabled = enabled;
            return widget;
        }

        public static T Content<T>(this T widget, Widget child) where T : Widget {
            widget.Add(child);
            return widget;
        }

        public static T Disabled<T>(this T widget, bool disabled) where T : Widget {
            widget.Enabled = !disabled;
            return widget;
        }

        public static T EnabledWhen<T>(this T widget, bool enabled) where T : Widget {
            widget.Enabled = enabled;
            return widget;
        }

        public static T Tooltip<T>(this T widget, string text) where T : Widget {
            widget.TooltipText = text ?? string.Empty;
            return widget;
        }

        public static T ContextMenu<T>(this T widget, Widget content) where T : Widget {
            widget.ContextMenuContent = content;
            return widget;
        }

        public static T Focusable<T>(this T widget, bool focusable) where T : Widget {
            widget.Focusable = focusable;
            return widget;
        }

        public static T DefaultFocus<T>(this T widget) where T : Widget {
            widget.HasDefaultFocus = true;
            return widget;
        }

        public static T FocusHere<T>(this T widget) where T : Widget {
            widget.RequestFocus = true;
            return widget;
        }
    }
}
