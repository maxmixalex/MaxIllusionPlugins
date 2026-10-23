using System;
using UnityEngine;

namespace MaximumUI {
    internal sealed class DropdownOverlay {
        private readonly GUIStyle selectionStyle;
        private readonly GUIStyleState selectedState;
        private readonly GUIStyleState normalState;
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

        public DropdownOverlay(int windowId, int fontSize, MaximumTheme theme) {
            WindowId = windowId;
            Theme = theme ?? MaximumTheme.ExpressionLight;

            selectionStyle = new GUIStyle {
                fontSize = fontSize,
                padding = new RectOffset(4, 4, 4, 4)
            };
            selectedState = new GUIStyleState {
                textColor = Color.white,
                background = Theme.Gray
            };
            normalState = new GUIStyleState {
                textColor = Color.black,
                background = Texture2D.whiteTexture
            };
            selectionStyle.normal = normalState;
            selectionStyle.hover = selectedState;
            selectionStyle.active = selectedState;
            selectionStyle.focused = selectedState;
            selectionStyle.onNormal = normalState;
            selectionStyle.onHover = selectedState;
            selectionStyle.onActive = selectedState;
            selectionStyle.onFocused = selectedState;
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
            bool resetScroll = !Show || this.items.Length != nextItems.Length || !Mathf.Approximately(this.itemHeight, effectiveItemHeight);
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
            if (Rect.y + Rect.height > Screen.height) Rect.y -= Rect.height + itemHeight;

            float contentWidth = Mathf.Max(1f, rect.width - padding.left - padding.right);
            scrollViewRect = new Rect(padding.left, padding.top + filterHeight, contentWidth, visibleContentHeight);
            this.itemRect = scroll
                ? new Rect(0f, 0f, Mathf.Max(1f, contentWidth - 16f), totalContentHeight)
                : new Rect(padding.left, padding.top + filterHeight, contentWidth, totalContentHeight);
            if (resetScroll) scrollPosition = Vector2.zero;
            Show = true;
        }

        public void Focus(string controlName) {
            focusControlName = controlName ?? string.Empty;
            focusEstablished = false;
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
                scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, itemRect, false, false);
            }

            int selected = DrawItems();
            if (scroll) GUI.EndScrollView();

            if (selected >= 0) {
                Action<int> action = callback;
                Show = false;
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
            for (int i = 0; i < count; i++) {
                Rect row = new Rect(itemRect.x, itemRect.y + itemHeight * i, itemRect.width, itemHeight);
                bool hover = current != null && row.Contains(current.mousePosition);
                if (current == null || current.type == EventType.Repaint) {
                    selectionStyle.Draw(row, items[i] ?? string.Empty, hover, false, false, false);
                }

                if (hover && current != null && current.type == EventType.MouseDown && current.button == 0) {
                    current.Use();
                    return i;
                }
            }

            return -1;
        }

        private void CloseIfFocusLost() {
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) {
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
            GUI.FocusControl(null);
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
