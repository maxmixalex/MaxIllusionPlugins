using System;
using UnityEngine;

namespace AccessoryBatchManager {
    // Port of StarPlugins/ExpressionControl.Core/ComboBox.cs.
    internal sealed class ClassicComboBox {
        private readonly GUIStyle selectionGrid;
        private Rect itemRect;
        private string[] items = new string[0];
        private Action<int> callback;
        private bool scroll;
        private Vector2 scrollPosition;
        private Vector2 mainWindowOffset;

        internal ClassicComboBox(int windowId, int fontSize) {
            WindowId = windowId;
            Texture2D gray = Solid(new Color(0.8f, 0.8f, 0.8f, 1f));
            selectionGrid = new GUIStyle {
                fontSize = fontSize,
                padding = new RectOffset(4, 4, 4, 4)
            };
            selectionGrid.hover = new GUIStyleState { textColor = Color.white, background = gray };
            selectionGrid.active = selectionGrid.hover;
            selectionGrid.focused = selectionGrid.hover;
        }

        internal readonly int WindowId;
        internal Rect Rect;
        internal bool Show { get; private set; }

        internal void Set(Rect mainWindow, Rect buttonRect, string[] values, Action<int> onSelected) {
            Rect rect = new Rect(
                mainWindow.x + buttonRect.x,
                mainWindow.y + buttonRect.y + buttonRect.height,
                buttonRect.width,
                buttonRect.height * values.Length);
            Set(rect, buttonRect.height, values, onSelected);
            mainWindowOffset = new Vector2(Rect.x - mainWindow.x, Rect.y - mainWindow.y);
        }

        internal void Follow(Rect mainWindow) {
            if (!Show) return;
            Rect = new Rect(mainWindow.x + mainWindowOffset.x, mainWindow.y + mainWindowOffset.y, Rect.width, Rect.height);
        }

        private void Set(Rect rect, float itemHeight, string[] values, Action<int> onSelected) {
            scroll = rect.height > Screen.height * 0.4f;
            Rect = scroll ? new Rect(rect.x, rect.y, rect.width, Screen.height * 0.4f) : rect;
            if (Rect.y + Rect.height > Screen.height) Rect.y -= Rect.height + itemHeight;
            items = values ?? new string[0];
            itemRect = scroll ? new Rect(0f, 0f, rect.width - 16f, rect.height) : new Rect(0f, 0f, rect.width, rect.height);
            callback = onSelected;
            Show = true;
        }

        internal void Hide() { Show = false; }

        internal void DrawWindow(int id) {
            if (scroll) scrollPosition = GUI.BeginScrollView(new Rect(0f, 0f, Rect.width, Rect.height), scrollPosition, itemRect, false, false);
            int selected = GUI.SelectionGrid(itemRect, -1, items, 1, selectionGrid);
            if (scroll) GUI.EndScrollView();
            if (selected >= 0) {
                Action<int> action = callback;
                Show = false;
                if (action != null) action(selected);
                Input.ResetInputAxes();
            }
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) {
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (!Rect.Contains(mouse)) Show = false;
            }
        }

        private static Texture2D Solid(Color color) {
            var texture = new Texture2D(4, 4) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
