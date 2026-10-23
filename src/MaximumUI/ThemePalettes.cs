namespace MaximumUI {
    /// <summary>Fresh, independent palettes. Safe to inspect or edit without Unity native objects.</summary>
    public static class ThemePalettes {
        /// <summary>Copy and bound editable values before creating native styles or textures.</summary>
        public static ThemeTokens Normalize(ThemeTokens source) {
            ThemeTokens fallback = MetroDark();
            ThemeTokens result = (source ?? fallback).Clone();
            foreach (ThemeColorToken token in System.Enum.GetValues(typeof(ThemeColorToken))) {
                MuiColor value = result.GetColor(token);
                MuiColor defaultValue = fallback.GetColor(token);
                result.SetColor(token, new MuiColor(
                    Bound(value.R, 0f, 1f, defaultValue.R), Bound(value.G, 0f, 1f, defaultValue.G),
                    Bound(value.B, 0f, 1f, defaultValue.B), Bound(value.A, 0f, 1f, defaultValue.A)));
            }
            result.FontSize = Bound(result.FontSize, 8f, 32f, 13f);
            result.BorderWidth = Bound(result.BorderWidth, 0f, 4f, 1f);
            result.Radius = Bound(result.Radius, 0f, 16f, 0f);
            result.ToggleBoxSize = Bound(result.ToggleBoxSize, 12f, 32f, 16f);
            result.SliderTrackHeight = Bound(result.SliderTrackHeight, 1f, 16f, 3f);
            result.SliderThumbWidth = Bound(result.SliderThumbWidth, 6f, 32f, 10f);
            result.SliderThumbHeight = Bound(result.SliderThumbHeight, 8f, 40f, 18f);
            return result;
        }

        private static float Bound(float value, float min, float max, float fallback) {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return System.Math.Max(min, System.Math.Min(max, value));
        }

        public static ThemeTokens MetroDark() {
            return new ThemeTokens {
                WindowBg = Rgb(25, 29, 34), PanelBg = Rgb(32, 37, 43),
                TitleBg = Rgb(21, 24, 28), DialogBg = Rgb(32, 37, 43),
                DropdownBg = Rgb(36, 42, 49), Backdrop = new MuiColor(0f, 0f, 0f, 0.58f),
                Text = Rgb(235, 240, 244), TextMuted = Rgb(164, 177, 190),
                TextDisabled = Rgb(117, 130, 144), Border = Rgb(66, 78, 90),
                Separator = Rgb(47, 56, 65), Accent = Rgb(73, 170, 230),
                AccentHover = Rgb(109, 191, 241), OnAccent = Rgb(19, 30, 40),
                FocusRing = Rgb(109, 191, 241),
                ButtonBg = Rgb(43, 50, 58), ButtonHoverBg = Rgb(55, 65, 75),
                ButtonActiveBg = Rgb(66, 79, 94), ButtonDisabledBg = Rgb(36, 42, 49),
                FieldBg = Rgb(23, 27, 31), FieldHoverBg = Rgb(29, 35, 41),
                FieldActiveBg = Rgb(27, 34, 42), SelectedBg = Rgb(28, 61, 82),
                InfoBg = Rgb(30, 60, 82), SuccessBg = Rgb(26, 70, 58),
                WarningBg = Rgb(77, 61, 30), ErrorBg = Rgb(84, 42, 48),
                BorderWidth = 1f, Radius = 0f, FontSize = 13f, ToggleBoxSize = 16f,
                SliderTrackHeight = 3f, SliderThumbWidth = 10f, SliderThumbHeight = 18f
            };
        }

        public static ThemeTokens MetroLight() {
            return new ThemeTokens {
                WindowBg = Rgb(245, 247, 250), PanelBg = Rgb(255, 255, 255),
                TitleBg = Rgb(235, 239, 244), DialogBg = Rgb(255, 255, 255),
                DropdownBg = Rgb(255, 255, 255), Backdrop = new MuiColor(0f, 0f, 0f, 0.42f),
                Text = Rgb(31, 41, 54), TextMuted = Rgb(83, 100, 119),
                TextDisabled = Rgb(125, 137, 150), Border = Rgb(165, 179, 195),
                Separator = Rgb(219, 226, 234), Accent = Rgb(0, 91, 163),
                AccentHover = Rgb(0, 74, 133), OnAccent = Rgb(255, 255, 255),
                FocusRing = Rgb(0, 91, 163),
                ButtonBg = Rgb(239, 242, 246), ButtonHoverBg = Rgb(228, 237, 247),
                ButtonActiveBg = Rgb(211, 226, 243), ButtonDisabledBg = Rgb(235, 239, 244),
                FieldBg = Rgb(250, 252, 254), FieldHoverBg = Rgb(244, 249, 255),
                FieldActiveBg = Rgb(255, 255, 255), SelectedBg = Rgb(219, 237, 251),
                InfoBg = Rgb(224, 240, 254), SuccessBg = Rgb(222, 241, 229),
                WarningBg = Rgb(255, 244, 214), ErrorBg = Rgb(251, 228, 231),
                BorderWidth = 1f, Radius = 0f, FontSize = 13f, ToggleBoxSize = 16f,
                SliderTrackHeight = 3f, SliderThumbWidth = 10f, SliderThumbHeight = 18f
            };
        }

        private static MuiColor Rgb(int r, int g, int b) {
            return new MuiColor(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
