namespace MaximumUI {
    /// <summary>Fresh, independent palettes. Safe to inspect or edit without Unity native objects.</summary>
    public static class ThemePalettes {
        /// <summary>Copy and bound editable values before creating native styles or textures.</summary>
        public static ThemeTokens Normalize(ThemeTokens source) {
            ThemeTokens fallback = MetroLight();
            ThemeTokens result = (source ?? fallback).Clone();
            foreach (ThemeColorToken token in System.Enum.GetValues(typeof(ThemeColorToken))) {
                MuiColor value = result.GetColor(token);
                MuiColor defaultValue = fallback.GetColor(token);
                result.SetColor(token, new MuiColor(
                    Bound(value.R, 0f, 1f, defaultValue.R), Bound(value.G, 0f, 1f, defaultValue.G),
                    Bound(value.B, 0f, 1f, defaultValue.B), Bound(value.A, 0f, 1f, defaultValue.A)));
            }
            result.FontSize = Bound(result.FontSize, 8f, 32f, 14f);
            result.BorderWidth = Bound(result.BorderWidth, 0f, 4f, 1f);
            result.Radius = Bound(result.Radius, 0f, 16f, 0f);
            result.ToggleBoxSize = Bound(result.ToggleBoxSize, 12f, 32f, 20f);
            result.SliderTrackHeight = Bound(result.SliderTrackHeight, 1f, 16f, 2f);
            result.SliderThumbWidth = Bound(result.SliderThumbWidth, 6f, 32f, 8f);
            result.SliderThumbHeight = Bound(result.SliderThumbHeight, 8f, 40f, 24f);
            return result;
        }

        private static float Bound(float value, float min, float max, float fallback) {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return System.Math.Max(min, System.Math.Min(max, value));
        }

        public static ThemeTokens MetroDark() {
            return new ThemeTokens {
                WindowBg = Rgb(32, 32, 32), PanelBg = Rgb(43, 43, 43),
                TitleBg = Rgb(32, 32, 32), DialogBg = Rgb(43, 43, 43),
                DropdownBg = Rgb(43, 43, 43), Backdrop = new MuiColor(0f, 0f, 0f, 0.58f),
                Text = Rgb(255, 255, 255), TextMuted = Rgb(198, 198, 198),
                TextDisabled = Rgb(119, 119, 119), Border = Rgb(96, 96, 96),
                Separator = Rgb(67, 67, 67), Accent = Rgb(76, 166, 234),
                AccentHover = Rgb(117, 189, 241), AccentPressed = Rgb(55, 143, 210),
                OnAccent = Rgb(0, 0, 0), FocusRing = Rgb(255, 255, 255),
                ControlBorder = Rgb(158, 158, 158), ControlHoverBorder = Rgb(255, 255, 255),
                ButtonBg = Rgb(64, 64, 64), ButtonHoverBg = Rgb(77, 77, 77),
                ButtonActiveBg = Rgb(96, 96, 96), ButtonDisabledBg = Rgb(51, 51, 51),
                FieldBg = Rgb(32, 32, 32), FieldHoverBg = Rgb(32, 32, 32),
                FieldActiveBg = Rgb(24, 24, 24), SelectedBg = Rgb(41, 72, 96),
                InfoBg = Rgb(38, 57, 73), SuccessBg = Rgb(38, 65, 48),
                WarningBg = Rgb(73, 61, 34), ErrorBg = Rgb(76, 39, 42),
                BorderWidth = 1f, Radius = 0f, FontSize = 14f, ToggleBoxSize = 20f,
                SliderTrackHeight = 2f, SliderThumbWidth = 8f, SliderThumbHeight = 24f
            };
        }

        public static ThemeTokens MetroLight() {
            return new ThemeTokens {
                WindowBg = Rgb(255, 255, 255), PanelBg = Rgb(242, 242, 242),
                TitleBg = Rgb(255, 255, 255), DialogBg = Rgb(255, 255, 255),
                DropdownBg = Rgb(255, 255, 255), Backdrop = new MuiColor(0f, 0f, 0f, 0.42f),
                Text = Rgb(23, 23, 23), TextMuted = Rgb(96, 96, 96),
                TextDisabled = Rgb(153, 153, 153), Border = Rgb(204, 204, 204),
                Separator = Rgb(229, 229, 229),
                // Slightly darker than #0078D7 to retain >= 4.5:1 contrast with white text.
                Accent = Rgb(0, 118, 215), AccentHover = Rgb(0, 100, 182),
                AccentPressed = Rgb(0, 83, 151), OnAccent = Rgb(255, 255, 255),
                FocusRing = Rgb(23, 23, 23),
                ControlBorder = Rgb(118, 118, 118), ControlHoverBorder = Rgb(23, 23, 23),
                ButtonBg = Rgb(230, 230, 230), ButtonHoverBg = Rgb(218, 218, 218),
                ButtonActiveBg = Rgb(195, 195, 195), ButtonDisabledBg = Rgb(239, 239, 239),
                FieldBg = Rgb(255, 255, 255), FieldHoverBg = Rgb(255, 255, 255),
                FieldActiveBg = Rgb(255, 255, 255), SelectedBg = Rgb(204, 228, 247),
                InfoBg = Rgb(229, 241, 251), SuccessBg = Rgb(228, 242, 232),
                WarningBg = Rgb(255, 244, 206), ErrorBg = Rgb(253, 231, 233),
                BorderWidth = 1f, Radius = 0f, FontSize = 14f, ToggleBoxSize = 20f,
                SliderTrackHeight = 2f, SliderThumbWidth = 8f, SliderThumbHeight = 24f
            };
        }

        private static MuiColor Rgb(int r, int g, int b) {
            return new MuiColor(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
