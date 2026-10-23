using System;
using UnityEngine;

namespace JuiceControl {
    internal sealed class JuiceGui {
        public readonly GUIStyle WindowStyle;
        public readonly GUIStyle FoldedWindowStyle;
        public readonly GUIStyle LabelStyle;
        public readonly GUIStyle WrappedLabelStyle;
        public readonly GUIStyle CenterLabelStyle;
        public readonly GUIStyle ButtonStyle;
        public readonly GUIStyle ToggleStyle;
        public readonly GUIStyle TextFieldStyle;
        public readonly GUIStyle GroupStyle;
        public readonly GUIStyle LineStyle;
        public readonly ComboBox Combo;

        public JuiceGui(int comboWindowId) {
            int fontSize = JuiceControlGuiUtility.GetPix(12);

            WindowStyle = new GUIStyle("window") {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter
            };
            SetBackground(WindowStyle, JuiceControlGuiUtility.TexGuiWindowOff);
            SetOnBackground(WindowStyle, JuiceControlGuiUtility.TexGuiWindowOn);
            WindowStyle.border = new RectOffset(8, 8, 20, 8);
            SetTextColor(WindowStyle, Color.black);

            FoldedWindowStyle = new GUIStyle("window") {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter
            };
            SetBackground(FoldedWindowStyle, JuiceControlGuiUtility.TexGuiWindowFoldOff);
            SetOnBackground(FoldedWindowStyle, JuiceControlGuiUtility.TexGuiWindowFoldOn);
            FoldedWindowStyle.border = new RectOffset(8, 8, 0, 0);
            FoldedWindowStyle.padding.bottom = -20;
            SetTextColor(FoldedWindowStyle, Color.black);

            LabelStyle = new GUIStyle("label") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(LabelStyle, Color.black);
            WrappedLabelStyle = new GUIStyle(LabelStyle) {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            CenterLabelStyle = new GUIStyle(LabelStyle) {
                alignment = TextAnchor.MiddleCenter
            };

            ButtonStyle = new GUIStyle("button") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter
            };
            ButtonStyle.normal.background = JuiceControlGuiUtility.TexGuiButtonNormal;
            ButtonStyle.hover.background = JuiceControlGuiUtility.TexGuiButtonHover;
            ButtonStyle.focused.background = JuiceControlGuiUtility.TexGuiButtonHover;
            ButtonStyle.active.background = JuiceControlGuiUtility.TexGuiButtonActive;
            ButtonStyle.border = new RectOffset(6, 6, 6, 6);
            SetTextColor(ButtonStyle, Color.black);

            ToggleStyle = new GUIStyle("toggle") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(ToggleStyle, Color.black);

            TextFieldStyle = new GUIStyle("textfield") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            TextFieldStyle.normal.background = JuiceControlGuiUtility.TexGuiInputNormal;
            TextFieldStyle.hover.background = JuiceControlGuiUtility.TexGuiInputActive;
            TextFieldStyle.focused.background = JuiceControlGuiUtility.TexGuiInputActive;
            TextFieldStyle.active.background = JuiceControlGuiUtility.TexGuiInputActive;
            TextFieldStyle.border = new RectOffset(6, 6, 6, 6);
            SetTextColor(TextFieldStyle, Color.black);

            GroupStyle = new GUIStyle("box") {
                fontSize = fontSize,
                alignment = TextAnchor.UpperLeft
            };
            SetBackground(GroupStyle, JuiceControlGuiUtility.TexGuiBackgroundOn);
            SetOnBackground(GroupStyle, JuiceControlGuiUtility.TexGuiBackgroundOn);
            GroupStyle.border = new RectOffset(12, 12, 12, 12);
            SetTextColor(GroupStyle, Color.black);

            LineStyle = new GUIStyle();
            SetBackground(LineStyle, JuiceControlGuiUtility.TexBlack);

            Combo = new ComboBox(comboWindowId, fontSize);
        }

        public void DrawComboBox() {
            if (!Combo.Show) return;

            Combo.Rect = GUI.Window(Combo.WindowId, Combo.Rect, Combo.GuiFunc, string.Empty, GroupStyle);
        }

        public void HandleMouseInput(Rect mainWindow, bool showDialog, Rect dialogRect, ref bool mouseDownOnWindow) {
            if (JuiceControlGuiUtility.GetAnyMouseButtonDown()) {
                Vector2 mousePosition = MousePosition;
                mouseDownOnWindow = mainWindow.Contains(mousePosition) ||
                    (showDialog && dialogRect.Contains(mousePosition)) ||
                    (Combo.Show && Combo.Rect.Contains(mousePosition));

                if (Combo.Show && !Combo.Rect.Contains(mousePosition)) {
                    Combo.Hide();
                }
            }

            if (mouseDownOnWindow && JuiceControlGuiUtility.GetAnyMouseButton()) {
                Vector2 mousePosition = MousePosition;
                if (mainWindow.Contains(mousePosition) ||
                    (showDialog && dialogRect.Contains(mousePosition)) ||
                    (Combo.Show && Combo.Rect.Contains(mousePosition))) {
                    if (Combo.Show && !Combo.Rect.Contains(mousePosition)) {
                        Combo.Hide();
                    }
                    Input.ResetInputAxes();
                }
            } else {
                mouseDownOnWindow = false;
            }
        }

        public float CenteredHorizontalSlider(float value, float leftValue, float rightValue, float rowHeight, params GUILayoutOption[] options) {
            Rect rowRect = GUILayoutUtility.GetRect(80f, 10000f, rowHeight, rowHeight, options);
            float sliderHeight = GetSliderHeight(rowRect.width);
            Rect sliderRect = new Rect(rowRect.x, rowRect.y + Mathf.Max(0f, (rowRect.height - sliderHeight) * 0.5f), rowRect.width, sliderHeight);
            return GUI.HorizontalSlider(sliderRect, value, leftValue, rightValue);
        }

        public void DragNamedDialog(Rect dialogRect) {
            GUI.DragWindow(new Rect(0f, 0f, dialogRect.width, 24f));
        }

        private static float GetSliderHeight(float width) {
            float height = GUI.skin.horizontalSlider.fixedHeight;
            if (height <= 0f) {
                height = GUI.skin.horizontalSlider.CalcHeight(GUIContent.none, width);
            }
            return height > 0f ? height : 16f;
        }

        private static Vector2 MousePosition {
            get { return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y); }
        }

        private static void SetBackground(GUIStyle style, Texture2D texture) {
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
        }

        private static void SetOnBackground(GUIStyle style, Texture2D texture) {
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onActive.background = texture;
            style.onFocused.background = texture;
        }

        private static void SetTextColor(GUIStyle style, Color color) {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        internal sealed class ComboBox {
            private readonly GUIStyle selectionGrid;
            private readonly GUIStyleState blackState;
            private readonly GUIStyleState whiteState;
            private Rect rectItem;
            private string[] items = new string[0];
            private Action<int> callback;
            private bool scroll;
            private Vector2 scrollPosition;

            public ComboBox(int windowId, int fontSize) {
                WindowId = windowId;
                selectionGrid = new GUIStyle {
                    fontSize = fontSize,
                    padding = new RectOffset(4, 4, 4, 4)
                };
                blackState = new GUIStyleState {
                    textColor = Color.white,
                    background = JuiceControlGuiUtility.TexGray
                };
                whiteState = new GUIStyleState {
                    textColor = Color.black,
                    background = Texture2D.whiteTexture
                };
                selectionGrid.hover = blackState;
                selectionGrid.active = blackState;
                selectionGrid.focused = blackState;
            }

            public readonly int WindowId;
            public Rect Rect;
            public bool Show { get; private set; }

            public void Set(Rect mainWindow, Rect itemRect, string[] items, Action<int> callback) {
                Rect rect = new Rect(mainWindow.x + itemRect.x, mainWindow.y + itemRect.y + itemRect.height, itemRect.width, itemRect.height * items.Length);
                Set(rect, itemRect.height, items, callback);
            }

            public void Set(Rect mainWindow, Rect subWindow, Rect itemRect, string[] items, Action<int> callback) {
                Rect rect = new Rect(mainWindow.x + subWindow.x + itemRect.x, mainWindow.y + subWindow.y + itemRect.y + itemRect.height, itemRect.width, itemRect.height * items.Length);
                Set(rect, itemRect.height, items, callback);
            }

            public void Set(Rect rect, float itemHeight, string[] items, Action<int> callback) {
                this.items = items ?? new string[0];
                this.callback = callback;
                scroll = rect.height > Screen.height * 0.4f;
                Rect = scroll ? new Rect(rect.x, rect.y, rect.width, Screen.height * 0.4f) : rect;
                if (Rect.y + Rect.height > Screen.height) {
                    Rect.y = Rect.y - (Rect.height + itemHeight);
                }

                rectItem = scroll ? new Rect(0f, 0f, rect.width - 16f, rect.height) : new Rect(0f, 0f, rect.width, rect.height);
                Show = true;
            }

            public void Hide() {
                Show = false;
            }

            public void GuiFunc(int windowId) {
                if (scroll) {
                    scrollPosition = GUI.BeginScrollView(new Rect(0f, 0f, Rect.width, Rect.height), scrollPosition, rectItem, false, false);
                }

                int selected = GUI.SelectionGrid(rectItem, -1, items, 1, selectionGrid);
                if (scroll) {
                    GUI.EndScrollView();
                }

                if (selected >= 0) {
                    Action<int> action = callback;
                    Show = false;
                    if (action != null) action(selected);
                    Input.ResetInputAxes();
                }

                if (JuiceControlGuiUtility.GetAnyMouseButtonDown()) {
                    Vector2 mousePosition = MousePosition;
                    if (!Rect.Contains(mousePosition)) {
                        Show = false;
                    }
                }
            }
        }
    }
}
