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
        private readonly List<ItemQuery> itemPool = new List<ItemQuery>();
        private readonly List<WindowQuery> windowPool = new List<WindowQuery>();
        private readonly List<MuiRect> inputRegions = new List<MuiRect>();
        private readonly List<MuiRect> overlayRegions = new List<MuiRect>();
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
            inputRegions.Clear();
            overlayRegions.Clear();
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

        internal void RecordInputRegion(MuiRect rect) {
            RecordInputRegion(rect, false);
        }

        internal void RecordInputRegion(MuiRect rect, bool overlay) {
            if (rect.Width <= 0f || rect.Height <= 0f) return;
            inputRegions.Add(rect);
            if (overlay) overlayRegions.Add(rect);
        }

        internal bool ContainsInputRegion(MuiVec2 point) {
            for (int i = inputRegions.Count - 1; i >= 0; i--)
                if (inputRegions[i].Contains(point)) return true;
            return false;
        }

        internal bool ContainsOverlayRegion(MuiVec2 point) {
            for (int i = overlayRegions.Count - 1; i >= 0; i--)
                if (overlayRegions[i].Contains(point)) return true;
            return false;
        }

        internal bool ContainsVisibleWindow(MuiVec2 point) {
            for (int i = windows.Count - 1; i >= 0; i--)
                if (windows[i].Visible && windows[i].Rect.Contains(point)) return true;
            return false;
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

            int slot = items.Count;
            ItemQuery query;
            if (slot < itemPool.Count) query = itemPool[slot];
            else {
                query = new ItemQuery();
                if (slot < 512) itemPool.Add(query);
            }
            query.Id = widget.Identity(path);
            query.Key = widget.IdentityKeyText;
            query.Path = path ?? string.Empty;
            query.TypeName = widget.IdentityTypeName;
            query.Rect = rect;
            query.Visible = true;
            query.Hovered = hovered;
            query.Active = active;
            query.Focused = focused;
            query.Clicked = clicked;
            query.Edited = query.Activated = query.Deactivated = query.DeactivatedAfterEdit = query.ToggledOpen = false;
            AddItem(query, widget.PathIdentity(path));
        }

        public void RecordWindow(Window window, string path, MuiRect rect, MuiRect titleRect, bool hovered, bool focused, bool titleHovered, bool titleActive) {
            if (window == null) return;

            int slot = windows.Count;
            WindowQuery query;
            if (slot < windowPool.Count) query = windowPool[slot];
            else {
                query = new WindowQuery();
                if (slot < 32) windowPool.Add(query);
            }
            query.Id = window.Identity(path);
            query.Key = window.IdentityKeyText;
            query.Title = window.Title ?? string.Empty;
            query.Rect = rect;
            query.TitleRect = titleRect;
            query.Visible = window.Visible;
            query.Hovered = hovered;
            query.Focused = focused;
            query.TitleHovered = titleHovered;
            query.TitleActive = titleActive;

            windows.Add(query);
            windowById[query.Id] = query;
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
            ItemQuery query = FindItem(key);
            return query != null && query.Hovered;
        }

        public bool IsActive(object key) {
            ItemQuery query = FindItem(key);
            return query != null && query.Active;
        }

        public bool IsFocused(object key) {
            ItemQuery query = FindItem(key);
            return query != null && query.Focused;
        }

        public bool IsClicked(object key) {
            ItemQuery query = FindItem(key);
            return query != null && query.Clicked;
        }

        public bool IsVisible(object key) {
            ItemQuery query = FindItem(key);
            return query != null && query.Visible;
        }

        public bool IsWindowHovered(object key) {
            WindowQuery query;
            return key != null && windowById.TryGetValue("key:" + key, out query) && query.Hovered;
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

        private ItemQuery FindItem(object key) {
            ItemQuery query;
            return key != null && itemById.TryGetValue("key:" + key, out query) ? query : null;
        }

        private void AddItem(ItemQuery query, string pathId) {
            items.Add(query);
            itemById[query.Id] = query;
            itemById[pathId] = query;
        }
    }
}
