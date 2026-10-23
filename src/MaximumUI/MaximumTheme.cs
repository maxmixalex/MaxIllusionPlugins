using UnityEngine;

namespace MaximumUI {
    public sealed class MaximumTheme {
        private static MaximumTheme expressionLight;
        private static MaximumTheme cleanLight;
        private readonly GUIContent measurementContent = new GUIContent();
        private readonly GUIStyle wrappedLabel;

        public readonly int FontSize;
        public readonly GUIStyle Window;
        public readonly GUIStyle FoldedWindow;
        public readonly GUIStyle Box;
        public readonly GUIStyle Label;
        public readonly GUIStyle CenterLabel;
        public readonly GUIStyle Button;
        public readonly GUIStyle Toggle;
        public readonly GUIStyle ToggleBox;
        public readonly GUIStyle ToggleCheck;
        public readonly GUIStyle TextField;
        public readonly GUIStyle ComboBoxButton;
        public readonly GUIStyle ComboBoxItem;
        public readonly GUIStyle HorizontalSlider;
        public readonly GUIStyle HorizontalSliderThumb;
        public readonly GUIStyle Separator;
        public readonly GUIStyle HelpBox;
        public readonly GUIStyle Dialog;
        public readonly GUIStyle TabButton;
        public readonly GUIStyle FoldoutHeader;
        public readonly GUIStyle Dropdown;
        public readonly float WindowOpacity;
        public readonly RectOffset WindowContentPadding;
        public readonly RectOffset DialogContentPadding;
        public readonly RectOffset DropdownContentPadding;

        public readonly Texture2D WindowOn;
        public readonly Texture2D WindowOff;
        public readonly Texture2D WindowFoldOn;
        public readonly Texture2D WindowFoldOff;
        public readonly Texture2D BackgroundOn;
        public readonly Texture2D ButtonNormal;
        public readonly Texture2D ButtonHover;
        public readonly Texture2D ButtonActive;
        public readonly Texture2D InputNormal;
        public readonly Texture2D InputActive;
        public readonly Texture2D Black;
        public readonly Texture2D Gray;

        private MaximumTheme(int fontSize, bool clean) {
            FontSize = fontSize;
            WindowOpacity = clean ? 1f : 0.8f;
            Color border = new Color(0.78f, 0.81f, 0.85f, 1f);
            Color text = new Color(0.13f, 0.17f, 0.23f, 1f);
            Color accent = new Color(0.15f, 0.36f, 0.75f, 1f);
            if (clean) {
                WindowOn = Textures.Frame(new Color(0.97f, 0.98f, 0.99f, 1f), border);
                WindowOff = WindowOn;
                WindowFoldOn = WindowOn;
                WindowFoldOff = WindowOn;
                BackgroundOn = Textures.Frame(new Color(0.94f, 0.96f, 0.98f, 1f), border);
                ButtonNormal = Textures.Frame(Color.white, border);
                ButtonHover = Textures.Frame(new Color(0.91f, 0.95f, 1f, 1f), accent);
                ButtonActive = Textures.Frame(new Color(0.80f, 0.88f, 1f, 1f), accent);
                InputNormal = Textures.Frame(Color.white, border);
                InputActive = Textures.Frame(Color.white, accent);
            } else {
                WindowOn = ThemeResources.LoadTexture("WindowOn.png", 32, 32);
                WindowOff = ThemeResources.LoadTexture("WindowOff.png", 32, 32);
                WindowFoldOn = ThemeResources.LoadTexture("WindowFoldOn.png", 32, 32);
                WindowFoldOff = ThemeResources.LoadTexture("WindowFoldOff.png", 32, 32);
                BackgroundOn = ThemeResources.LoadTexture("BackgroundOn.png", 32, 32);
                ButtonNormal = ThemeResources.LoadTexture("ButtonNormal.png", 32, 32);
                ButtonHover = ThemeResources.LoadTexture("ButtonHover.png", 32, 32);
                ButtonActive = ThemeResources.LoadTexture("ButtonActive.png", 32, 32);
                InputNormal = ThemeResources.LoadTexture("InputNormal.png", 32, 32);
                InputActive = ThemeResources.LoadTexture("InputActive.png", 32, 32);
            }
            Black = Textures.Solid(Color.black);
            Gray = Textures.Solid(clean ? border : new Color(0.8f, 0.8f, 0.8f, 1f));
            int titlePadding = clean ? System.Math.Max(28, fontSize + 14) : 26;
            WindowContentPadding = new RectOffset(10, 10, titlePadding, 10);
            DialogContentPadding = new RectOffset(10, 10, titlePadding, 10);
            DropdownContentPadding = new RectOffset(4, 4, 4, 4);

            Window = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.UpperCenter,
                border = new RectOffset(8, 8, 20, 8),
                contentOffset = new Vector2(0f, 2f)
            };
            SetBackground(Window, WindowOff);
            SetOnBackground(Window, WindowOn);
            SetTextColor(Window, Color.black);

            FoldedWindow = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.UpperCenter,
                border = new RectOffset(8, 8, 0, 0),
                contentOffset = new Vector2(0f, 2f),
                padding = { bottom = -20 }
            };
            SetBackground(FoldedWindow, WindowFoldOff);
            SetOnBackground(FoldedWindow, WindowFoldOn);
            SetTextColor(FoldedWindow, Color.black);

            Label = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(Label, Color.black);
            wrappedLabel = new GUIStyle(Label) { wordWrap = true };

            CenterLabel = new GUIStyle(Label) {
                alignment = TextAnchor.MiddleCenter
            };

            Button = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(6, 6, 6, 6)
            };
            Button.normal.background = ButtonNormal;
            Button.hover.background = ButtonHover;
            Button.focused.background = ButtonHover;
            Button.active.background = ButtonActive;
            SetTextColor(Button, Color.black);

            Toggle = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(Toggle, Color.black);

            ToggleBox = new GUIStyle() {
                border = new RectOffset(4, 4, 4, 4),
                fixedWidth = 14f,
                fixedHeight = 14f,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
            SetBackground(ToggleBox, InputNormal);
            ToggleBox.hover.background = InputActive;
            ToggleBox.active.background = InputActive;
            ToggleBox.focused.background = InputActive;

            ToggleCheck = new GUIStyle();
            SetBackground(ToggleCheck, Black);

            TextField = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(6, 6, 6, 6)
            };
            TextField.normal.background = InputNormal;
            TextField.hover.background = InputActive;
            TextField.focused.background = InputActive;
            TextField.active.background = InputActive;
            SetTextColor(TextField, Color.black);

            ComboBoxButton = new GUIStyle(Button);

            ComboBoxItem = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(6, 6, 2, 2)
            };
            ComboBoxItem.normal.background = InputNormal;
            ComboBoxItem.hover.background = InputActive;
            ComboBoxItem.focused.background = InputActive;
            ComboBoxItem.active.background = ButtonActive;
            SetTextColor(ComboBoxItem, Color.black);

            HorizontalSlider = new GUIStyle() {
                fixedHeight = 4f,
                border = new RectOffset(2, 2, 2, 2)
            };
            SetBackground(HorizontalSlider, Gray);
            HorizontalSliderThumb = new GUIStyle() {
                fixedWidth = 12f,
                fixedHeight = 12f,
                border = new RectOffset(4, 4, 4, 4)
            };
            SetBackground(HorizontalSliderThumb, ButtonActive);

            Box = new GUIStyle() {
                fontSize = fontSize,
                alignment = TextAnchor.UpperLeft,
                border = new RectOffset(12, 12, 12, 12)
            };
            SetBackground(Box, BackgroundOn);
            SetOnBackground(Box, BackgroundOn);
            SetTextColor(Box, Color.black);

            Separator = new GUIStyle();
            SetBackground(Separator, Black);

            HelpBox = new GUIStyle(Box) {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            Dialog = new GUIStyle(Window);
            TabButton = new GUIStyle(Button) {
                border = new RectOffset(6, 6, 6, 6)
            };
            TabButton.onNormal.background = ButtonActive;
            TabButton.onHover.background = ButtonActive;
            TabButton.onFocused.background = ButtonActive;
            TabButton.onActive.background = ButtonActive;
            TabButton.onNormal.textColor = Color.black;
            TabButton.onHover.textColor = Color.black;
            TabButton.onFocused.textColor = Color.black;
            TabButton.onActive.textColor = Color.black;

            FoldoutHeader = new GUIStyle(Button) {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 6, 2, 2)
            };

            Dropdown = new GUIStyle(Box) {
                padding = DropdownContentPadding
            };

            if (clean) {
                GUIStyle[] framed = { Window, FoldedWindow, Box, Button, ToggleBox, TextField,
                    ComboBoxButton, ComboBoxItem, HelpBox, Dialog, TabButton, FoldoutHeader, Dropdown };
                for (int i = 0; i < framed.Length; i++) {
                    framed[i].border = new RectOffset(1, 1, 1, 1);
                    SetTextColor(framed[i], text);
                }
                GUIStyle[] labels = { Label, CenterLabel, Toggle, wrappedLabel };
                for (int i = 0; i < labels.Length; i++) SetTextColor(labels[i], text);
                Window.alignment = FoldedWindow.alignment = Dialog.alignment = TextAnchor.UpperLeft;
                Window.padding = FoldedWindow.padding = Dialog.padding = new RectOffset(10, 54, 5, 5);
                Window.contentOffset = FoldedWindow.contentOffset = Dialog.contentOffset = Vector2.zero;
                Button.padding = ComboBoxButton.padding = TabButton.padding = new RectOffset(8, 8, 3, 3);
                TextField.padding = new RectOffset(7, 7, 3, 3);
                SetBackground(Separator, Gray);
                SetBackground(ToggleCheck, Textures.Solid(accent));
            }
        }

        public static MaximumTheme ExpressionLight {
            get {
                if (expressionLight == null) expressionLight = new MaximumTheme(ScaledPixels(12), false);
                return expressionLight;
            }
        }

        /// <summary>Opaque, compact light theme with consistent spacing and a blue selection accent.</summary>
        public static MaximumTheme CleanLight {
            get {
                if (cleanLight == null) cleanLight = new MaximumTheme(ScaledPixels(12), true);
                return cleanLight;
            }
        }

        public static int ScaledPixels(int value) {
            return ThemeSizing.ScalePixels(value, Screen.width);
        }

        public LayoutMetrics CreateLayoutMetrics() {
            return new LayoutMetrics {
                RowHeight = System.Math.Max(22f, FontSize + 10f),
                SmallRowHeight = System.Math.Max(16f, FontSize + 4f),
                TitleHeight = WindowContentPadding.top,
                WindowPaddingLeft = WindowContentPadding.left,
                WindowPaddingTop = WindowContentPadding.top,
                WindowPaddingRight = WindowContentPadding.right,
                WindowPaddingBottom = WindowContentPadding.bottom,
                TextWidth = FontSize * 0.6f,
                MeasureText = MeasureTextWidth,
                MeasureWrappedTextHeight = MeasureWrappedTextHeight
            };
        }

        private float MeasureTextWidth(string text) {
            measurementContent.text = text ?? string.Empty;
            return Label.CalcSize(measurementContent).x;
        }

        private float MeasureWrappedTextHeight(string text, float width) {
            measurementContent.text = text ?? string.Empty;
            return wrappedLabel.CalcHeight(measurementContent, width);
        }

        public static void SetBackground(GUIStyle style, Texture2D texture) {
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
        }

        public static void SetOnBackground(GUIStyle style, Texture2D texture) {
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onActive.background = texture;
            style.onFocused.background = texture;
        }

        public static void SetTextColor(GUIStyle style, Color color) {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

    }
}
