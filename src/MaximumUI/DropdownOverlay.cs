using System;
using UnityEngine;

namespace MaximumUI {
    internal sealed class DropdownOverlay {
        private readonly GUIStyle selectionStyle;
        private string[] items = new string[0];
        private Action<int> callback;
        private Rect itemRect;
        private Rect scrollViewRect;
        private Vector2 scrollPosition;
        private bool scroll;
        private float itemHeight;
        private int maxVisibleItems;
        private string focusControlName;
        private bool focusEstablished;
        private string filterText = string.Empty;
        private string filterPlaceholder = string.Empty;
        private Action<string> filterChanged;
        private int activeIndex;

        public DropdownOverlay(int windowId, int fontSize, MaximumTheme theme) {
            WindowId = windowId;
            Theme = theme ?? MaximumTheme.MetroDark;
            selectionStyle = new GUIStyle(Theme.ComboBoxItem) { fontSize = fontSize };
        }

        public readonly int WindowId;
        public MaximumTheme Theme { get; private set; }
        public Rect Rect;
        public bool Show { get; private set; }

        public void Set(Rect mainWindow, Rect itemRect, string[] items, Action<int> callback, int maxVisibleItems) {
            Rect rect = new Rect(
                mainWindow.x + itemRect.x,
                mainWindow.y + itemRect.y + itemRect.height,
                itemRect.width,
                itemRect.height * GetItemCount(items));
            Set(rect, itemRect.height, items, callback, maxVisibleItems);
        }

        public void Set(Rect mainWindow, Rect itemRect, string[] items, Action<int> callback, int maxVisibleItems, string filterText, Action<string> filterChanged, string filterPlaceholder) {
            Rect rect = new Rect(
                mainWindow.x + itemRect.x,
                mainWindow.y + itemRect.y + itemRect.height,
                itemRect.width,
                itemRect.height * GetItemCount(items));
            Set(rect, itemRect.height, items, callback, maxVisibleItems, filterText, filterChanged, filterPlaceholder);
        }

        public void Set(Rect rect, float itemHeight, string[] items, Action<int> callback, int maxVisibleItems) {
            Set(rect, itemHeight, items, callback, maxVisibleItems, string.Empty, null, string.Empty);
        }

        public void Set(Rect rect, float itemHeight, string[] items, Action<int> callback, int maxVisibleItems, string filterText, Action<string> filterChanged, string filterPlaceholder) {
            string[] nextItems = items ?? new string[0];
            float effectiveItemHeight = Mathf.Max(itemHeight, GetMinimumItemHeight());
            bool resetScroll = !Show || !ReferenceEquals(this.items, nextItems) || !Mathf.Approximately(this.itemHeight, effectiveItemHeight);
            this.items = nextItems;
            this.callback = callback;
            this.itemHeight = effectiveItemHeight;
            this.maxVisibleItems = maxVisibleItems;
            this.filterText = filterText ?? string.Empty;
            this.filterChanged = filterChanged;
            this.filterPlaceholder = filterPlaceholder ?? string.Empty;

            RectOffset padding = Theme.DropdownContentPadding;
            float filterHeight = this.filterChanged == null ? 0f : this.itemHeight + 3f;
            float totalContentHeight = this.itemHeight * GetItemCount(this.items);
            float maxHeightByItems = this.itemHeight * GetVisibleItemCount(this.items, this.maxVisibleItems);
            float visibleContentHeight = Mathf.Min(totalContentHeight, maxHeightByItems);
            scroll = totalContentHeight > visibleContentHeight || visibleContentHeight > Screen.height * 0.4f;
            if (scroll) visibleContentHeight = Mathf.Min(visibleContentHeight, Screen.height * 0.4f);
            Rect = new Rect(rect.x, rect.y, rect.width, visibleContentHeight + filterHeight + padding.top + padding.bottom);
            MuiRect placed = WindowGeometry.PlacePopup(new MuiRect(rect.x, rect.y - itemHeight, rect.width, itemHeight),
                Rect.width, Rect.height, Screen.width, Screen.height);
            Rect = new Rect(placed.X, placed.Y, placed.Width, placed.Height);

            float contentWidth = Mathf.Max(1f, Rect.width - padding.left - padding.right);
            scrollViewRect = new Rect(padding.left, padding.top + filterHeight, contentWidth, visibleContentHeight);
            this.itemRect = scroll
                ? new Rect(0f, 0f, Mathf.Max(1f, contentWidth - 16f), totalContentHeight)
                : new Rect(padding.left, padding.top + filterHeight, contentWidth, totalContentHeight);
            if (resetScroll) {
                scrollPosition = Vector2.zero;
                activeIndex = this.items.Length == 0 ? -1 : 0;
            }
            Show = true;
        }

        public void Focus(string controlName, int selectedIndex = 0) {
            focusControlName = controlName ?? string.Empty;
            focusEstablished = false;
            activeIndex = items.Length == 0 ? -1 : Math.Max(0, Math.Min(items.Length - 1, selectedIndex));
            scrollPosition.y = KeyboardNavigation.RevealRow(scrollPosition.y, activeIndex, itemHeight, scrollViewRect.height, items.Length);
            if (focusControlName.Length > 0) GUI.FocusControl(focusControlName);
        }

        public void Hide() {
            Show = false;
            focusControlName = string.Empty;
            focusEstablished = false;
        }

        public void Draw() {
            if (!Show) return;
            CloseIfFocusLost();
            if (!Show) return;
            HandleKeyboard();
            if (!Show) return;
            Color oldColor = GUI.color;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, Theme.WindowOpacity);
            try {
                if (Event.current == null || Event.current.type == EventType.Repaint) {
                    Theme.Dropdown.Draw(Rect, GUIContent.none, false, false, false, false);
                }
            } finally {
                GUI.color = oldColor;
            }

            GUI.BeginGroup(Rect);
            try {
                DrawContents();
            } finally {
                GUI.EndGroup();
            }
        }

        private void DrawContents() {
            DrawFilter();

            if (scroll) {
                scrollPosition = Theme.BeginScrollView(scrollViewRect, scrollPosition, itemRect, false, false);
            }

            int selected;
            try { selected = DrawItems(); }
            finally { if (scroll) GUI.EndScrollView(); }

            if (selected >= 0) {
                Action<int> action = callback;
                Hide();
                if (action != null) action(selected);
                Input.ResetInputAxes();
                return;
            }

            CloseIfFocusLost();
        }

        private void DrawFilter() {
            if (filterChanged == null) return;

            RectOffset padding = Theme.DropdownContentPadding;
            Rect rect = new Rect(padding.left, padding.top, Mathf.Max(1f, Rect.width - padding.left - padding.right), itemHeight);
            GUI.SetNextControlName(focusControlName);
            string next = GUI.TextField(rect, filterText, Theme.TextField);
            if (!string.Equals(next, filterText, StringComparison.Ordinal)) {
                filterText = next ?? string.Empty;
                Action<string> action = filterChanged;
                if (action != null) action(filterText);
            }

            if (string.IsNullOrEmpty(filterText) && !string.IsNullOrEmpty(filterPlaceholder) && (Event.current == null || Event.current.type == EventType.Repaint)) {
                Theme.Label.Draw(rect, filterPlaceholder, false, false, false, false);
            }
        }

        private int DrawItems() {
            Event current = Event.current;
            int count = items == null ? 0 : items.Length;
            int first = 0;
            int end = count;
            if (scroll) ScrollGeometry.VisibleRows(scrollPosition.y, scrollViewRect.height, itemHeight, count, out first, out end);
            for (int i = first; i < end; i++) {
                Rect row = new Rect(itemRect.x, itemRect.y + itemHeight * i, itemRect.width, itemHeight);
                bool hover = current != null && row.Contains(current.mousePosition);
                if (current == null || current.type == EventType.Repaint) {
                    selectionStyle.Draw(row, items[i] ?? string.Empty, hover, false, i == activeIndex, i == activeIndex);
                }

                if (GUI.enabled && hover && current != null && current.type == EventType.MouseDown && current.button == 0) {
                    current.Use();
                    return i;
                }
            }

            return -1;
        }

        private void HandleKeyboard() {
            Event current = Event.current;
            if (!GUI.enabled || current == null || current.type != EventType.KeyDown || current.alt || current.control || current.command) return;
            KeyCode key = current.keyCode;
            // Home/End and horizontal arrows retain text-editing semantics in a filter.
            bool moveKey = key == KeyCode.DownArrow || key == KeyCode.UpArrow || key == KeyCode.PageDown || key == KeyCode.PageUp ||
                (filterChanged == null && (key == KeyCode.Home || key == KeyCode.End));
            int next;
            if (moveKey && KeyboardNavigation.MoveSelection(activeIndex, items.Length, key, GetVisibleItemCount(items, maxVisibleItems), out next)) {
                activeIndex = next;
                scrollPosition.y = KeyboardNavigation.RevealRow(scrollPosition.y, activeIndex, itemHeight, scrollViewRect.height, items.Length);
                current.Use();
                Input.ResetInputAxes();
            } else if ((key == KeyCode.Return || key == KeyCode.KeypadEnter) && activeIndex >= 0 && activeIndex < items.Length) {
                Action<int> action = callback;
                int selected = activeIndex;
                Hide();
                current.Use();
                if (action != null) action(selected);
                Input.ResetInputAxes();
            }
        }

        private void CloseIfFocusLost() {
            Event current = Event.current;
            if (GUI.enabled && current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
                Hide();
                current.Use();
                return;
            }

            string focusedControl = GUI.GetNameOfFocusedControl();
            if (!string.IsNullOrEmpty(focusControlName)) {
                if (string.Equals(focusedControl, focusControlName, StringComparison.Ordinal)) {
                    focusEstablished = true;
                } else if (focusEstablished) {
                    Hide();
                    return;
                }
            }

            if (!AnyMouseButtonDown()) return;

            Vector2 mousePosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Rect.Contains(mousePosition)) return;

            Hide();
            if (GUI.enabled) GUI.FocusControl(null);
        }

        private static int GetVisibleItemCount(string[] items, int maxVisibleItems) {
            int count = items == null ? 0 : items.Length;
            if (maxVisibleItems <= 0) return Mathf.Max(1, count);

            return Mathf.Max(1, Mathf.Min(count, maxVisibleItems));
        }

        private static int GetItemCount(string[] items) {
            int count = items == null ? 0 : items.Length;
            return Mathf.Max(1, count);
        }

        private float GetMinimumItemHeight() {
            return Mathf.Max(20f, Theme.FontSize + selectionStyle.padding.top + selectionStyle.padding.bottom + 4f);
        }

        private static bool AnyMouseButtonDown() {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }
    }
}
