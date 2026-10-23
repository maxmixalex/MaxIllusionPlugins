using UnityEngine;

namespace MaximumUI {
    public sealed class MaximumTheme {
        private static MaximumTheme expressionLight;

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

        private MaximumTheme(int fontSize) {
            FontSize = fontSize;
            WindowOpacity = 0.8f;
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
            Black = Textures.Solid(Color.black);
            Gray = Textures.Solid(new Color(0.8f, 0.8f, 0.8f, 1f));
            WindowContentPadding = new RectOffset(10, 10, 26, 10);
            DialogContentPadding = new RectOffset(10, 10, 26, 10);
            DropdownContentPadding = new RectOffset(4, 4, 4, 4);

            Window = new GUIStyle("window") {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                border = new RectOffset(8, 8, 20, 8),
                contentOffset = new Vector2(0f, 2f)
            };
            SetBackground(Window, WindowOff);
            SetOnBackground(Window, WindowOn);
            SetTextColor(Window, Color.black);

            FoldedWindow = new GUIStyle("window") {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                border = new RectOffset(8, 8, 0, 0),
                contentOffset = new Vector2(0f, 2f),
                padding = { bottom = -20 }
            };
            SetBackground(FoldedWindow, WindowFoldOff);
            SetOnBackground(FoldedWindow, WindowFoldOn);
            SetTextColor(FoldedWindow, Color.black);

            Label = new GUIStyle("label") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(Label, Color.black);

            CenterLabel = new GUIStyle(Label) {
                alignment = TextAnchor.MiddleCenter
            };

            Button = new GUIStyle("button") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(6, 6, 6, 6)
            };
            Button.normal.background = ButtonNormal;
            Button.hover.background = ButtonHover;
            Button.focused.background = ButtonHover;
            Button.active.background = ButtonActive;
            SetTextColor(Button, Color.black);

            Toggle = new GUIStyle("toggle") {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft
            };
            SetTextColor(Toggle, Color.black);

            ToggleBox = new GUIStyle("box") {
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

            TextField = new GUIStyle("textfield") {
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

            ComboBoxItem = new GUIStyle("button") {
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

            HorizontalSlider = new GUIStyle("horizontalslider") {
                fixedHeight = 4f,
                border = new RectOffset(2, 2, 2, 2)
            };
            SetBackground(HorizontalSlider, Gray);
            HorizontalSliderThumb = new GUIStyle("horizontalsliderthumb") {
                fixedWidth = 12f,
                fixedHeight = 12f,
                border = new RectOffset(4, 4, 4, 4)
            };
            SetBackground(HorizontalSliderThumb, ButtonActive);

            Box = new GUIStyle("box") {
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
        }

        public static MaximumTheme ExpressionLight {
            get {
                if (expressionLight == null) expressionLight = new MaximumTheme(ScaledPixels(12));
                return expressionLight;
            }
        }

        public static int ScaledPixels(int value) {
            return (int)((1f + (Screen.width / 1280f - 1f) * 0.6f) * value);
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
