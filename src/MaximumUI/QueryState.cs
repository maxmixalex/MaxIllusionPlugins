using System;
using System.Collections.Generic;

namespace MaximumUI {
    public sealed class ItemQuery {
        public string Id;
        public string Key;
        public string Path;
        public string TypeName;
        public MuiRect Rect;
        public bool Visible;
        public bool Hovered;
        public bool Active;
        public bool Focused;
        public bool Clicked;
        public bool Edited;
        public bool Activated;
        public bool Deactivated;
        public bool DeactivatedAfterEdit;
        public bool ToggledOpen;

        public ItemQuery Clone() {
            return (ItemQuery)MemberwiseClone();
        }
    }

    public sealed class WindowQuery {
        public string Id;
        public string Key;
        public string Title;
        public MuiRect Rect;
        public MuiRect TitleRect;
        public bool Visible;
        public bool Hovered;
        public bool Focused;
        public bool TitleHovered;
        public bool TitleActive;

        public WindowQuery Clone() {
            return (WindowQuery)MemberwiseClone();
        }
    }

    public sealed class InputCaptureState {
        public bool WantCaptureMouse;
        public bool WantCaptureKeyboard;
        public bool WantTextInput;
        public bool WantSetMousePos;
        public bool NavActive;
        public bool NavVisible;
        public bool MouseDownOnUi;
        public MuiVec2 MousePosition;
        public int KeyCode;
        public char Character;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool Command;

        public InputCaptureState Clone() {
            return (InputCaptureState)MemberwiseClone();
        }
    }

    public sealed class UiQueryStore {
        private readonly List<ItemQuery> items = new List<ItemQuery>();
        private readonly List<WindowQuery> windows = new List<WindowQuery>();
        private readonly Dictionary<string, ItemQuery> itemById = new Dictionary<string, ItemQuery>();
        private readonly Dictionary<string, WindowQuery> windowById = new Dictionary<string, WindowQuery>();
        private readonly InputCaptureState capture = new InputCaptureState();

        public InputCaptureState Capture {
            get { return capture; }
        }

        public int ItemCount {
            get { return items.Count; }
        }

        public int WindowCount {
            get { return windows.Count; }
        }

        public void BeginFrame() {
            items.Clear();
            windows.Clear();
            itemById.Clear();
            windowById.Clear();
            capture.WantCaptureMouse = false;
            capture.WantCaptureKeyboard = false;
            capture.WantTextInput = false;
            capture.WantSetMousePos = false;
            capture.NavActive = false;
            capture.NavVisible = false;
            capture.MouseDownOnUi = false;
            capture.KeyCode = 0;
            capture.Character = '\0';
            capture.Control = false;
            capture.Shift = false;
            capture.Alt = false;
            capture.Command = false;
        }

        public void RecordInput(MuiInputEvent input) {
            if (input == null) return;

            capture.MousePosition = input.MousePosition;
            capture.KeyCode = input.KeyCode;
            capture.Character = input.Character;
            capture.Control = input.Control;
            capture.Shift = input.Shift;
            capture.Alt = input.Alt;
            capture.Command = input.Command;
        }

        public void SetCapture(bool mouse, bool keyboard, bool textInput) {
            capture.WantCaptureMouse = capture.WantCaptureMouse || mouse;
            capture.WantCaptureKeyboard = capture.WantCaptureKeyboard || keyboard;
            capture.WantTextInput = capture.WantTextInput || textInput;
            capture.NavActive = capture.NavActive || keyboard;
            capture.NavVisible = capture.NavVisible || keyboard;
        }

        public void SetNavigation(bool active, bool visible) {
            capture.NavActive = capture.NavActive || active;
            capture.NavVisible = capture.NavVisible || visible;
        }

        public void RecordLayout(LayoutNode root, InputDispatchResult result) {
            if (root == null) return;
            RecordLayoutNode(root, result);
        }

        public void RecordItem(Widget widget, string path, MuiRect rect, bool hovered, bool active, bool focused, bool clicked) {
            if (widget == null || widget is EmptyWidget) return;

            ItemQuery query = new ItemQuery {
                Id = ItemId(widget, path),
                Key = widget.Key == null ? null : widget.Key.ToString(),
                Path = path ?? string.Empty,
                TypeName = widget.GetType().Name,
                Rect = rect,
                Visible = true,
                Hovered = hovered,
                Active = active,
                Focused = focused,
                Clicked = clicked
            };

            AddItem(query);
        }

        public void RecordWindow(Window window, string path, MuiRect rect, MuiRect titleRect, bool hovered, bool focused, bool titleHovered, bool titleActive) {
            if (window == null) return;

            WindowQuery query = new WindowQuery {
                Id = ItemId(window, path),
                Key = window.Key == null ? null : window.Key.ToString(),
                Title = window.Title ?? string.Empty,
                Rect = rect,
                TitleRect = titleRect,
                Visible = window.Visible,
                Hovered = hovered,
                Focused = focused,
                TitleHovered = titleHovered,
                TitleActive = titleActive
            };

            windows.Add(query);
            windowById[query.Id] = query;
            if (!string.IsNullOrEmpty(query.Key)) windowById["key:" + query.Key] = query;
        }

        public ItemQuery GetItem(object key) {
            if (key == null) return null;
            ItemQuery query;
            return itemById.TryGetValue("key:" + key, out query) ? query.Clone() : null;
        }

        public ItemQuery GetItemByPath(string path) {
            if (path == null) return null;
            ItemQuery query;
            return itemById.TryGetValue("path:" + path, out query) ? query.Clone() : null;
        }

        public WindowQuery GetWindow(object key) {
            if (key == null) return null;
            WindowQuery query;
            return windowById.TryGetValue("key:" + key, out query) ? query.Clone() : null;
        }

        public bool IsHovered(object key) {
            ItemQuery query = GetItem(key);
            return query != null && query.Hovered;
        }

        public bool IsActive(object key) {
            ItemQuery query = GetItem(key);
            return query != null && query.Active;
        }

        public bool IsFocused(object key) {
            ItemQuery query = GetItem(key);
            return query != null && query.Focused;
        }

        public bool IsClicked(object key) {
            ItemQuery query = GetItem(key);
            return query != null && query.Clicked;
        }

        public bool IsVisible(object key) {
            ItemQuery query = GetItem(key);
            return query != null && query.Visible;
        }

        public bool IsWindowHovered(object key) {
            WindowQuery query = GetWindow(key);
            return query != null && query.Hovered;
        }

        public IList<ItemQuery> SnapshotItems() {
            List<ItemQuery> result = new List<ItemQuery>();
            for (int i = 0; i < items.Count; i++) {
                result.Add(items[i].Clone());
            }

            return result;
        }

        public IList<WindowQuery> SnapshotWindows() {
            List<WindowQuery> result = new List<WindowQuery>();
            for (int i = 0; i < windows.Count; i++) {
                result.Add(windows[i].Clone());
            }

            return result;
        }

        private void RecordLayoutNode(LayoutNode node, InputDispatchResult result) {
            bool hovered = result != null && string.Equals(result.HoveredPath, node.Path, StringComparison.Ordinal);
            bool active = result != null && string.Equals(result.ActivePath, node.Path, StringComparison.Ordinal);
            bool focused = result != null && string.Equals(result.FocusedPath, node.Path, StringComparison.Ordinal);
            bool clicked = result != null && string.Equals(result.ClickedPath, node.Path, StringComparison.Ordinal);
            RecordItem(node.Widget, node.Path, node.Rect, hovered, active, focused, clicked);

            for (int i = 0; i < node.Children.Count; i++) {
                RecordLayoutNode(node.Children[i], result);
            }
        }

        private void AddItem(ItemQuery query) {
            items.Add(query);
            itemById[query.Id] = query;
            itemById["path:" + query.Path] = query;
            if (!string.IsNullOrEmpty(query.Key)) itemById["key:" + query.Key] = query;
        }

        private static string ItemId(Widget widget, string path) {
            if (widget != null && widget.Key != null) return "key:" + widget.Key;
            return "path:" + (path ?? string.Empty);
        }
    }
}
