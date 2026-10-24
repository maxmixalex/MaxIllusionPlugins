using System;
using UnityEngine;

namespace MaximumUI {
    internal sealed class DropdownOverlay {
        private readonly GUIStyle selectionStyle;
        private readonly GUIStyle disabledStyle;
        private readonly GUIStyle placeholderStyle;
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
        private PopupLayout layout;

        public DropdownOverlay(int windowId, int fontSize, MaximumTheme theme) {
            WindowId = windowId;
            Theme = theme ?? MaximumTheme.MetroLight;
            selectionStyle = new GUIStyle(Theme.Label) { fontSize = fontSize, padding = new RectOffset() };
            disabledStyle = new GUIStyle(selectionStyle);
            MaximumTheme.SetTextColor(disabledStyle, ToColor(Theme.Tokens.TextDisabled));
            placeholderStyle = new GUIStyle(selectionStyle);
            MaximumTheme.SetTextColor(placeholderStyle, ToColor(Theme.Tokens.TextMuted));
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

            layout = PopupGeometry.Dropdown(new MuiRect(rect.x, rect.y - itemHeight, rect.width, itemHeight),
                new MuiRect(0f, 0f, Screen.width, Screen.height), this.items.Length, maxVisibleItems,
                effectiveItemHeight, this.filterChanged != null, Theme.DropdownContentPadding.left);
            Rect = ToRect(layout.Bounds);
            scroll = layout.Scrollable;
            scrollViewRect = LocalRect(layout.ScrollViewport);
            this.itemRect = scroll
                ? new Rect(0f, 0f, layout.ContentViewport.Width, layout.ContentHeight)
                : new Rect(layout.ContentViewport.X - Rect.x, layout.ContentViewport.Y - Rect.y, layout.ContentViewport.Width, layout.ContentHeight);
            if (resetScroll) {
                scrollPosition = Vector2.zero;
                activeIndex = this.items.Length == 0 ? -1 : 0;
            }
            scrollPosition.y = layout.ClampScroll(scrollPosition.y);
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
                    Fill(Rect, Theme.Tokens.DropdownBg);
                    Stroke(Rect, Theme.Tokens.ControlBorder, 1f);
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

            Rect rect = LocalRect(layout.FilterRect);
            GUI.SetNextControlName(focusControlName);
            string next = GUI.TextField(rect, filterText, Theme.TextField);
            if (!string.Equals(next, filterText, StringComparison.Ordinal)) {
                filterText = next ?? string.Empty;
                Action<string> action = filterChanged;
                if (action != null) action(filterText);
            }

            if (string.IsNullOrEmpty(filterText) && !string.IsNullOrEmpty(filterPlaceholder) && (Event.current == null || Event.current.type == EventType.Repaint)) {
                placeholderStyle.Draw(LocalRect(PopupGeometry.Inset(layout.FilterRect, 8f, 0f, 8f, 0f)), filterPlaceholder, false, false, false, false);
            }
            if (Event.current == null || Event.current.type == EventType.Repaint)
                Stroke(rect, Mdl2ControlChrome.FieldBorderColor(Theme.Tokens, GUI.enabled, false,
                    string.Equals(GUI.GetNameOfFocusedControl(), focusControlName, StringComparison.Ordinal)), 2f);
        }

        private int DrawItems() {
            Event current = Event.current;
            int count = items == null ? 0 : items.Length;
            int first = 0;
            int end = count;
            if (scroll) ScrollGeometry.VisibleRows(scrollPosition.y, scrollViewRect.height, itemHeight, count, out first, out end);
            if (count == 0 && (current == null || current.type == EventType.Repaint)) {
                MuiRect empty = PopupGeometry.DropdownTextBounds(new MuiRect(itemRect.x, itemRect.y, itemRect.width, itemHeight));
                placeholderStyle.Draw(ToRect(empty), "No results", false, false, false, false);
            }
            for (int i = first; i < end; i++) {
                MuiRect sharedRow = layout.Row(i, 0f);
                Rect row = new Rect(itemRect.x, itemRect.y + sharedRow.Y - layout.ContentViewport.Y, sharedRow.Width, sharedRow.Height);
                bool hover = GUI.enabled && current != null && row.Contains(current.mousePosition);
                if (current == null || current.type == EventType.Repaint) {
                    bool selected = GUI.enabled && i == activeIndex;
                    Fill(row, PopupPresentation.RowBackground(Theme.Tokens, GUI.enabled, hover, selected));
                    if (selected) Fill(new Rect(row.x, row.y, Mathf.Min(3f, row.width), row.height), Theme.Tokens.Accent);
                    MuiRect text = PopupGeometry.DropdownTextBounds(new MuiRect(row.x, row.y, row.width, row.height));
                    (GUI.enabled ? selectionStyle : disabledStyle).Draw(ToRect(text), items[i] ?? string.Empty, false, false, false, false);
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
            return Theme.RowHeight;
        }

        private Rect LocalRect(MuiRect rect) {
            return new Rect(rect.X - Rect.x, rect.Y - Rect.y, rect.Width, rect.Height);
        }

        private static Rect ToRect(MuiRect rect) { return new Rect(rect.X, rect.Y, rect.Width, rect.Height); }
        private static Color ToColor(MuiColor color) { return new Color(color.R, color.G, color.B, color.A); }

        private static void Fill(Rect rect, MuiColor color) {
            Color previous = GUI.color;
            GUI.color = new Color(previous.r * color.R, previous.g * color.G, previous.b * color.B, previous.a * color.A);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Stroke(Rect rect, MuiColor color, float width) {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private static bool AnyMouseButtonDown() {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }
    }
}
