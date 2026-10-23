using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class MaximumTheme : IDisposable {
        private static MaximumTheme expressionLight;
        private static MaximumTheme cleanLight;
        private static MaximumTheme metroDark;
        private static MaximumTheme metroLight;
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private readonly bool shared;
        private bool disposed;
        private GUISkin scrollSkin;
        private readonly GUIContent measurementContent = new GUIContent();
        private readonly GUIStyle wrappedLabel;
        private readonly Dictionary<GUIStyle, GUIStyle> wrappedStyles = new Dictionary<GUIStyle, GUIStyle>();

        public readonly string Name;
        public readonly ThemeTokens Tokens;
        public readonly GUIStyle Heading;
        public readonly GUIStyle MutedLabel;
        public readonly GUIStyle PrimaryButton;
        public readonly GUIStyle QuietButton;
        public readonly GUIStyle DangerButton;
        public readonly GUIStyle NavigationItem;
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
            shared = true;
            Name = clean ? "Clean Light" : "Expression Light";
            Tokens = clean ? ThemePalettes.MetroLight() : new ThemeTokens();
            Tokens.FontSize = fontSize;
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
            Heading = new GUIStyle(Label) { fontSize = fontSize + 7 };
            MutedLabel = new GUIStyle(Label);
            SetTextColor(MutedLabel, ColorOf(Tokens.TextMuted));
            PrimaryButton = new GUIStyle(Button);
            QuietButton = new GUIStyle(Button);
            DangerButton = new GUIStyle(Button);
            NavigationItem = new GUIStyle(TabButton) { alignment = TextAnchor.MiddleLeft };
        }

        private MaximumTheme(ThemeTokens palette, int fontSize, string name, bool isShared) {
            shared = isShared;
            Name = string.IsNullOrEmpty(name) ? "Custom" : name;
            Tokens = ThemePalettes.Normalize(palette);
            FontSize = Math.Max(8, Math.Min(32, fontSize > 0 ? fontSize : (int)Tokens.FontSize));
            Tokens.FontSize = FontSize;
            WindowOpacity = 1f;
            int titleHeight = Math.Max(32, FontSize + 18);
            WindowContentPadding = new RectOffset(16, 16, titleHeight + 12, 14);
            DialogContentPadding = new RectOffset(20, 20, titleHeight + 16, 18);
            DropdownContentPadding = new RectOffset(4, 4, 4, 4);

            WindowOn = Own(Textures.WindowSurface(ColorOf(Tokens.WindowBg), ColorOf(Tokens.TitleBg), ColorOf(Tokens.Border), titleHeight));
            WindowOff = WindowOn;
            WindowFoldOn = Own(Textures.Frame(ColorOf(Tokens.TitleBg), ColorOf(Tokens.Border)));
            WindowFoldOff = WindowFoldOn;
            BackgroundOn = Own(Textures.Frame(ColorOf(Tokens.PanelBg), ColorOf(Tokens.Separator)));
            ButtonNormal = Own(Textures.Frame(ColorOf(Tokens.ButtonBg), ColorOf(Tokens.Border)));
            ButtonHover = Own(Textures.Frame(ColorOf(Tokens.ButtonHoverBg), ColorOf(Tokens.Accent)));
            ButtonActive = Own(Textures.Frame(ColorOf(Tokens.ButtonActiveBg), ColorOf(Tokens.Accent)));
            InputNormal = Own(Textures.Surface(ColorOf(Tokens.FieldBg), ColorOf(Tokens.Border), ColorOf(Tokens.TextMuted), 1, 1));
            InputActive = Own(Textures.Surface(ColorOf(Tokens.FieldActiveBg), ColorOf(Tokens.Border), ColorOf(Tokens.Accent), 1, 2));
            Black = Own(Textures.Solid(Color.black));
            Gray = Own(Textures.Solid(ColorOf(Tokens.Separator)));
            Texture2D accent = Own(Textures.Solid(ColorOf(Tokens.Accent)));
            Texture2D transparent = Own(Textures.Solid(Color.clear));
            Texture2D hoverSurface = Own(Textures.Solid(ColorOf(Tokens.ButtonHoverBg)));
            Texture2D selectedTab = Own(Textures.Surface(ColorOf(Tokens.SelectedBg), ColorOf(Tokens.SelectedBg), ColorOf(Tokens.Accent), 1, 3));
            Texture2D selectedRow = Own(Textures.Surface(ColorOf(Tokens.SelectedBg), ColorOf(Tokens.SelectedBg), ColorOf(Tokens.Accent), 2, 3));

            Label = TextStyle(FontSize, TextAnchor.MiddleLeft, Tokens.Text);
            wrappedLabel = new GUIStyle(Label) { wordWrap = true };
            CenterLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Heading = TextStyle(FontSize + 8, TextAnchor.MiddleLeft, Tokens.Text);
            MutedLabel = TextStyle(FontSize, TextAnchor.MiddleLeft, Tokens.TextMuted);
            Window = TextStyle(FontSize, TextAnchor.UpperLeft, Tokens.Text);
            Window.padding = new RectOffset(16, 64, 8, 0);
            Window.border = new RectOffset(1, 1, titleHeight, 1);
            SetBackground(Window, WindowOn);
            SetOnBackground(Window, WindowOn);
            FoldedWindow = new GUIStyle(Window) { border = new RectOffset(1, 1, 1, 1) };
            SetBackground(FoldedWindow, WindowFoldOn);
            SetOnBackground(FoldedWindow, WindowFoldOn);

            Box = TextStyle(FontSize, TextAnchor.UpperLeft, Tokens.Text);
            Box.border = new RectOffset(1, 1, 1, 1);
            SetBackground(Box, BackgroundOn);
            SetOnBackground(Box, BackgroundOn);
            Button = TextStyle(FontSize, TextAnchor.MiddleCenter, Tokens.Text);
            Button.border = new RectOffset(1, 1, 1, 1);
            Button.padding = new RectOffset(12, 12, 4, 4);
            Button.normal.background = ButtonNormal;
            Button.hover.background = ButtonHover;
            Button.active.background = ButtonActive;
            Button.focused.background = ButtonHover;
            SetOnBackground(Button, ButtonActive);

            PrimaryButton = new GUIStyle(Button);
            SetBackground(PrimaryButton, accent);
            PrimaryButton.hover.background = Own(Textures.Solid(ColorOf(Tokens.AccentHover)));
            PrimaryButton.focused.background = Own(Textures.Frame(ColorOf(Tokens.Accent), ColorOf(Tokens.OnAccent)));
            PrimaryButton.active.background = PrimaryButton.hover.background;
            SetTextColor(PrimaryButton, ColorOf(Tokens.OnAccent));
            QuietButton = new GUIStyle(Button);
            QuietButton.normal.background = transparent;
            QuietButton.hover.background = QuietButton.active.background = hoverSurface;
            DangerButton = new GUIStyle(Button);
            DangerButton.normal.background = Own(Textures.Frame(ColorOf(Tokens.ErrorBg), ColorOf(Tokens.Border)));
            DangerButton.hover.background = DangerButton.active.background = Own(Textures.Frame(ColorOf(Tokens.ErrorBg), ColorOf(Tokens.Text)));

            Toggle = new GUIStyle(Label);
            ToggleBox = new GUIStyle {
                border = new RectOffset(1, 1, 1, 1),
                fixedWidth = Math.Max(14f, Tokens.ToggleBoxSize), fixedHeight = Math.Max(14f, Tokens.ToggleBoxSize)
            };
            SetBackground(ToggleBox, InputNormal);
            SetOnBackground(ToggleBox, InputActive);
            ToggleBox.hover.background = ToggleBox.focused.background = InputActive;
            ToggleCheck = new GUIStyle();
            SetBackground(ToggleCheck, accent);
            TextField = new GUIStyle(Label) { border = new RectOffset(1, 1, 1, 2), padding = new RectOffset(9, 9, 4, 4) };
            SetBackground(TextField, InputNormal);
            TextField.hover.background = Own(Textures.Surface(ColorOf(Tokens.FieldHoverBg), ColorOf(Tokens.Border), ColorOf(Tokens.Accent), 1, 2));
            TextField.focused.background = TextField.active.background = InputActive;
            ComboBoxButton = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 22, 3, 3) };
            ComboBoxItem = new GUIStyle(Label) { padding = new RectOffset(10, 10, 3, 3) };
            SetBackground(ComboBoxItem, transparent);
            ComboBoxItem.hover.background = ComboBoxItem.focused.background = hoverSurface;
            ComboBoxItem.active.background = selectedRow;
            SetOnBackground(ComboBoxItem, selectedRow);

            TabButton = new GUIStyle(Button) { border = new RectOffset(1, 1, 1, 3), padding = new RectOffset(12, 12, 4, 5) };
            TabButton.normal.background = transparent;
            TabButton.hover.background = hoverSurface;
            TabButton.active.background = selectedTab;
            SetOnBackground(TabButton, selectedTab);
            NavigationItem = new GUIStyle(TabButton) {
                alignment = TextAnchor.MiddleLeft, border = new RectOffset(3, 1, 1, 1), padding = new RectOffset(14, 10, 4, 4)
            };
            NavigationItem.active.background = selectedRow;
            SetOnBackground(NavigationItem, selectedRow);
            FoldoutHeader = new GUIStyle(NavigationItem);
            HorizontalSlider = new GUIStyle { fixedHeight = Math.Max(2f, Tokens.SliderTrackHeight) };
            SetBackground(HorizontalSlider, Own(Textures.Solid(ColorOf(Tokens.Border))));
            HorizontalSliderThumb = new GUIStyle {
                fixedWidth = Math.Max(8f, Tokens.SliderThumbWidth), fixedHeight = Math.Max(16f, Tokens.SliderThumbHeight)
            };
            SetBackground(HorizontalSliderThumb, accent);
            HorizontalSliderThumb.hover.background = PrimaryButton.hover.background;
            HorizontalSliderThumb.active.background = PrimaryButton.hover.background;
            Separator = new GUIStyle();
            SetBackground(Separator, Gray);
            HelpBox = new GUIStyle(Box) { wordWrap = true, padding = new RectOffset(12, 12, 9, 9) };
            Dialog = new GUIStyle(Window);
            Texture2D dialogSurface = Own(Textures.WindowSurface(ColorOf(Tokens.DialogBg), ColorOf(Tokens.TitleBg), ColorOf(Tokens.Border), titleHeight));
            SetBackground(Dialog, dialogSurface);
            SetOnBackground(Dialog, dialogSurface);
            Dropdown = new GUIStyle(Box) { padding = DropdownContentPadding };
            SetBackground(Dropdown, Own(Textures.Frame(ColorOf(Tokens.DropdownBg), ColorOf(Tokens.Border))));
        }

        private static GUIStyle TextStyle(int size, TextAnchor alignment, MuiColor color) {
            GUIStyle result = new GUIStyle { fontSize = size, alignment = alignment };
            SetTextColor(result, ColorOf(color));
            return result;
        }

        private Texture2D Own(Texture2D texture) {
            ownedTextures.Add(texture);
            return texture;
        }

        private static Color ColorOf(MuiColor color) {
            return new Color(color.R, color.G, color.B, color.A);
        }

        /// <summary>Create on Unity's main thread; the caller owns this theme and must dispose it after unmount/replacement.</summary>
        public static MaximumTheme Create(ThemeTokens tokens, int fontSize = 0, string name = "Custom") {
            return new MaximumTheme(tokens, fontSize, name, false);
        }

        /// <summary>Releases only custom-theme textures. Built-in shared themes live for the application lifetime.</summary>
        public void Dispose() {
            if (shared || disposed) return;
            disposed = true;
            if (scrollSkin != null) UnityEngine.Object.Destroy(scrollSkin);
            scrollSkin = null;
            for (int i = 0; i < ownedTextures.Count; i++) {
                if (ownedTextures[i] != null) UnityEngine.Object.Destroy(ownedTextures[i]);
            }
            ownedTextures.Clear();
            wrappedStyles.Clear();
        }

        /// <summary>
        /// Native scrollbars require track, thumb and arrow styles from the same skin.
        /// Only BeginScrollView reads those styles; restore the caller's skin before
        /// drawing child controls and pair this with the usual GUI.EndScrollView().
        /// </summary>
        public Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect,
            bool alwaysShowHorizontal = false, bool alwaysShowVertical = false) {
            if (disposed) throw new ObjectDisposedException("MaximumTheme");
            GUISkin previous = GUI.skin;
            GUISkin themed = GetScrollSkin(previous);
            try {
                GUI.skin = themed;
                return GUI.BeginScrollView(position, scrollPosition, viewRect,
                    alwaysShowHorizontal, alwaysShowVertical, themed.horizontalScrollbar, themed.verticalScrollbar);
            } finally {
                GUI.skin = previous;
            }
        }

        private GUISkin GetScrollSkin(GUISkin source) {
            if (scrollSkin == null) {
                // A private scroll-only skin avoids mutating the global game skin or
                // inheriting user customStyles that could shadow canonical thumb names.
                scrollSkin = ScriptableObject.CreateInstance<GUISkin>();
                scrollSkin.hideFlags = HideFlags.HideAndDontSave;
                Texture2D track = Own(Textures.Solid(ColorOf(Tokens.FieldBg)));
                Texture2D thumb = Own(Textures.Solid(ColorOf(Tokens.TextMuted)));
                Texture2D hovered = Own(Textures.Solid(ColorOf(Tokens.Accent)));
                Texture2D active = Own(Textures.Solid(ColorOf(Tokens.AccentHover)));

                GUIStyle vertical = new GUIStyle {
                    fixedWidth = 12f, margin = new RectOffset(4, 0, 0, 0), padding = new RectOffset(2, 2, 2, 2)
                };
                GUIStyle horizontal = new GUIStyle {
                    fixedHeight = 12f, margin = new RectOffset(0, 0, 4, 0), padding = new RectOffset(2, 2, 2, 2)
                };
                SetBackground(vertical, track);
                SetBackground(horizontal, track);
                GUIStyle verticalThumb = new GUIStyle {
                    fixedWidth = 8f, padding = new RectOffset(0, 0, 6, 6)
                };
                GUIStyle horizontalThumb = new GUIStyle {
                    fixedHeight = 8f, padding = new RectOffset(6, 6, 0, 0)
                };
                SetBackground(verticalThumb, thumb);
                SetBackground(horizontalThumb, thumb);
                verticalThumb.hover.background = verticalThumb.focused.background = hovered;
                horizontalThumb.hover.background = horizontalThumb.focused.background = hovered;
                verticalThumb.active.background = horizontalThumb.active.background = active;
                scrollSkin.verticalScrollbar = vertical;
                scrollSkin.verticalScrollbarThumb = verticalThumb;
                scrollSkin.verticalScrollbarUpButton = new GUIStyle { fixedWidth = 12f, fixedHeight = 0f };
                scrollSkin.verticalScrollbarDownButton = new GUIStyle { fixedWidth = 12f, fixedHeight = 0f };
                scrollSkin.horizontalScrollbar = horizontal;
                scrollSkin.horizontalScrollbarThumb = horizontalThumb;
                scrollSkin.horizontalScrollbarLeftButton = new GUIStyle { fixedWidth = 0f, fixedHeight = 12f };
                scrollSkin.horizontalScrollbarRightButton = new GUIStyle { fixedWidth = 0f, fixedHeight = 12f };
                scrollSkin.scrollView = new GUIStyle();
            }
            Font font = source == null ? null : source.font;
            if (scrollSkin.font != font) scrollSkin.font = font;
            return scrollSkin;
        }

        public static MaximumTheme MetroDark {
            get {
                if (metroDark == null) metroDark = new MaximumTheme(ThemePalettes.MetroDark(), ScaledPixels(13), "Metro Dark", true);
                return metroDark;
            }
        }

        public static MaximumTheme MetroLight {
            get {
                if (metroLight == null) metroLight = new MaximumTheme(ThemePalettes.MetroLight(), ScaledPixels(13), "Metro Light", true);
                return metroLight;
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
                RowHeight = System.Math.Max(26f, FontSize + 12f),
                SmallRowHeight = System.Math.Max(16f, FontSize + 4f),
                PanelPadding = 12f,
                TitleHeight = WindowContentPadding.top,
                WindowPaddingLeft = WindowContentPadding.left,
                WindowPaddingTop = WindowContentPadding.top,
                WindowPaddingRight = WindowContentPadding.right,
                WindowPaddingBottom = WindowContentPadding.bottom,
                DialogPaddingLeft = DialogContentPadding.left,
                DialogPaddingTop = DialogContentPadding.top,
                DialogPaddingRight = DialogContentPadding.right,
                DialogPaddingBottom = DialogContentPadding.bottom,
                TextWidth = FontSize * 0.6f,
                MeasureText = MeasureTextWidth,
                MeasureWrappedTextHeight = MeasureWrappedTextHeight,
                MeasureLabelWidth = MeasureLabelWidth,
                MeasureLabelHeight = MeasureLabelHeight
            };
        }

        private GUIStyle LabelStyle(Label label) {
            GUIStyle custom = label == null ? null : label.Style as GUIStyle;
            if (custom != null) return custom;
            return label != null && label.Role == LabelRole.Heading ? Heading :
                label != null && label.Role == LabelRole.Muted ? MutedLabel : Label;
        }

        private float MeasureLabelWidth(Label label) {
            measurementContent.text = label == null ? string.Empty : label.Text ?? string.Empty;
            return LabelStyle(label).CalcSize(measurementContent).x;
        }

        private float MeasureLabelHeight(Label label, float width) {
            GUIStyle style = LabelStyle(label);
            measurementContent.text = label == null ? string.Empty : label.Text ?? string.Empty;
            float rowHeight = Math.Max(style.fontSize > 0 ? style.fontSize + 10f : FontSize + 10f,
                style.CalcSize(measurementContent).y);
            if (label == null || !label.WordWrap || width <= 0f || float.IsInfinity(width)) return rowHeight;
            GUIStyle wrapped;
            if (style == Label) wrapped = wrappedLabel;
            else if (!wrappedStyles.TryGetValue(style, out wrapped)) {
                if (wrappedStyles.Count >= 64) wrappedStyles.Clear();
                wrapped = new GUIStyle(style) { wordWrap = true };
                wrappedStyles.Add(style, wrapped);
            }
            return Math.Max(rowHeight, wrapped.CalcHeight(measurementContent, width));
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
