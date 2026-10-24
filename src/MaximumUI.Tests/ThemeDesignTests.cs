using System;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class ThemeDesignTests {
        internal const int TestCount = 6;

        public static void RunAll() {
            PalettesKeepReadableTextAndAccentContrast();
            EditableTokensCloneAndRoundTripWithoutSharedState();
            InvalidThemeNumbersAreNormalizedBeforeNativeAllocation();
            PrimaryButtonsAndMutedLabelsUseTheirSemanticColors();
            SelectionHasAnAccentMarkerAndKeyboardFocus();
            ScopedEditorsOnlyMeasureAndPaintTheirSupportedTokens();
        }

        private static void PalettesKeepReadableTextAndAccentContrast() {
            ThemeTokens[] palettes = { ThemePalettes.MetroDark(), ThemePalettes.MetroLight() };
            foreach (ThemeTokens palette in palettes) {
                MuiColor[] surfaces = { palette.WindowBg, palette.PanelBg, palette.TitleBg, palette.DialogBg,
                    palette.DropdownBg, palette.FieldBg, palette.FieldHoverBg, palette.FieldActiveBg,
                    palette.ButtonBg, palette.ButtonHoverBg, palette.ButtonActiveBg, palette.SelectedBg,
                    palette.InfoBg, palette.SuccessBg, palette.WarningBg, palette.ErrorBg };
                foreach (MuiColor surface in surfaces) {
                    Assert(Contrast(palette.Text, surface) >= 4.5, "Body text must remain readable on every built-in surface.");
                    Assert(surface.A == 1f, "Embedded surfaces must not depend on the game scene behind them for readability.");
                }
                Assert(Contrast(palette.TextMuted, palette.PanelBg) >= 4.5, "Secondary labels must remain readable.");
                Assert(Contrast(palette.TextMuted, palette.WindowBg) >= 4.5, "Navigation captions must remain readable.");
                Assert(Contrast(palette.OnAccent, palette.Accent) >= 4.5, "Primary button text needs contrast against the accent.");
                Assert(Contrast(palette.OnAccent, palette.AccentHover) >= 4.5, "Primary hover text needs contrast.");
                Assert(Contrast(palette.OnAccent, palette.AccentPressed) >= 4.5, "Primary pressed text needs contrast.");
                Assert(Contrast(palette.ControlBorder, palette.FieldBg) >= 3.0, "Input boundaries must remain visible.");
                Assert(Contrast(palette.ControlHoverBorder, palette.FieldHoverBg) >= 3.0, "Hovered input boundaries must remain visible.");
                Assert(Contrast(palette.Accent, palette.SelectedBg) >= 3.0, "The selected-row marker must remain distinct.");
                Assert(Contrast(palette.FocusRing, palette.ButtonBg) >= 3.0, "Keyboard focus must be distinct from the surrounding control.");
            }
        }

        private static void EditableTokensCloneAndRoundTripWithoutSharedState() {
            ThemeTokens source = ThemePalettes.MetroDark();
            ThemeTokens copy = source.Clone();
            MuiColor sample = new MuiColor(0.11f, 0.22f, 0.33f, 0.44f);
            foreach (ThemeColorToken token in Enum.GetValues(typeof(ThemeColorToken))) {
                copy.SetColor(token, sample);
                Assert(Same(copy.GetColor(token), sample), "A theme token must survive editing and cloning: " + token);
            }
            ThemeTokens clonedCopy = copy.Clone();
            foreach (ThemeColorToken token in Enum.GetValues(typeof(ThemeColorToken))) {
                Assert(Same(clonedCopy.GetColor(token), sample), "CopyFrom must include every theme token: " + token);
            }
            Assert(!Same(source.Accent, copy.Accent), "Editing a copy must not mutate the original palette.");
            source.Accent = sample;
            Assert(!Same(ThemePalettes.MetroDark().Accent, sample), "Each palette request must return an independent value.");
        }

        private static void InvalidThemeNumbersAreNormalizedBeforeNativeAllocation() {
            ThemeTokens input = ThemePalettes.MetroLight();
            input.FontSize = float.NaN;
            input.ToggleBoxSize = float.PositiveInfinity;
            input.SliderTrackHeight = -100f;
            input.SliderThumbWidth = 1000000f;
            input.Accent = new MuiColor(-1f, 2f, float.NaN, float.PositiveInfinity);
            ThemeTokens output = ThemePalettes.Normalize(input);
            Assert(output.FontSize == 14f && output.ToggleBoxSize == 20f, "Invalid sizes must use the MDL2 desktop defaults.");
            Assert(output.SliderTrackHeight == 1f && output.SliderThumbWidth == 32f, "Finite sizes must respect limits.");
            Assert(output.Accent.R == 0f && output.Accent.G == 1f && !float.IsNaN(output.Accent.B) && output.Accent.A == 1f,
                "Invalid colors must not reach native textures.");
            Assert(float.IsNaN(input.FontSize), "Normalizing must not mutate pending editor data.");
            Assert(Same(ThemePalettes.Normalize(null).WindowBg, ThemePalettes.MetroLight().WindowBg), "The default palette must be light.");
        }

        private static void PrimaryButtonsAndMutedLabelsUseTheirSemanticColors() {
            ThemeTokens palette = ThemePalettes.MetroDark();
            Button button = new Button("Apply") { Variant = ButtonVariant.Primary };
            LayoutNode node = new LayoutEngine().Layout(button, new MuiRect(0f, 0f, 120f, 28f), "button");
            DrawList draw = new WidgetPainter(palette).Paint(node, new WidgetPaintState());
            Assert(HasCommand(draw, DrawCommandType.FillRect, palette.Accent), "Primary action must use the accent surface.");
            Assert(HasCommand(draw, DrawCommandType.Text, palette.OnAccent), "Primary action must use its matching foreground.");
            button.Enabled = false;
            draw = new WidgetPainter(palette).Paint(node, new WidgetPaintState());
            Assert(HasCommand(draw, DrawCommandType.FillRect, palette.ButtonDisabledBg), "Disabled primary action must have a disabled surface.");
            Assert(HasCommand(draw, DrawCommandType.Text, palette.TextDisabled), "Disabled action must have disabled text.");
            node = new LayoutEngine().Layout(new Label("Caption") { Role = LabelRole.Muted }, new MuiRect(0f, 0f, 120f, 28f), "caption");
            draw = new WidgetPainter(palette).Paint(node, new WidgetPaintState());
            Assert(HasCommand(draw, DrawCommandType.Text, palette.TextMuted), "Muted typography must use the semantic secondary color.");
        }

        private static void SelectionHasAnAccentMarkerAndKeyboardFocus() {
            ThemeTokens palette = ThemePalettes.MetroDark();
            LayoutNode node = new LayoutEngine().Layout(new Selectable("Overview", true), new MuiRect(12f, 14f, 164f, 34f), "navigation");
            DrawList draw = new WidgetPainter(palette).Paint(node, new WidgetPaintState { FocusedPath = "navigation" });
            bool marker = false;
            foreach (DrawCommand command in draw.Commands) {
                if (command.Type == DrawCommandType.FillRect && Same(command.Color, palette.Accent)
                    && command.Rect.Width == 3f && command.Rect.Height == 34f) marker = true;
            }
            Assert(marker, "A selected row must have a 3px leading marker.");
            Assert(HasCommand(draw, DrawCommandType.StrokeRect, palette.FocusRing), "Selected and keyboard-focused states must both remain visible.");
        }

        private static bool HasCommand(DrawList list, DrawCommandType type, MuiColor color) {
            foreach (DrawCommand command in list.Commands) if (command.Type == type && Same(command.Color, color)) return true;
            return false;
        }

        private static void ScopedEditorsOnlyMeasureAndPaintTheirSupportedTokens() {
            ThemeEditor editor = new ThemeEditor(ThemePalettes.MetroDark()) {
                ColorTokens = new[] { ThemeColorToken.Accent, ThemeColorToken.Text },
                ScalarTokens = new[] { ThemeScalarToken.FontSize }, MaxRows = 10
            };
            Assert(ThemeEditorTokens.TotalRowCount(editor) == 3 && ThemeEditorTokens.VisibleRowCount(editor) == 3,
                "an editor's size must match the subset that its backend applies");
            LayoutNode node = new LayoutEngine().Layout(editor, new MuiRect(0f, 0f, 400f, 200f));
            DrawList draw = new WidgetPainter(ThemePalettes.MetroDark()).Paint(node, new WidgetPaintState());
            bool accent = false;
            bool font = false;
            foreach (DrawCommand command in draw.Commands) {
                if (command.Type != DrawCommandType.Text) continue;
                if (command.Text == "Accent") accent = true;
                if (command.Text == "FontSize") font = true;
                Assert(command.Text != "Radius" && command.Text != "ButtonDisabledBg",
                    "unsupported native options are not presented as working edits");
            }
            Assert(accent && font, "the requested colors and sizes are still shown");
            editor.ColorTokens = new ThemeColorToken[0];
            editor.ScalarTokens = new ThemeScalarToken[0];
            Assert(ThemeEditorTokens.TotalRowCount(editor) == 0, "an empty explicit subset stays empty");
            editor.ColorTokens = null;
            editor.ScalarTokens = null;
            Assert(ThemeEditorTokens.TotalRowCount(editor) == ThemeEditorTokens.ColorTokens.Length + ThemeEditorTokens.ScalarTokens.Length,
                "unscoped editors preserve the existing complete token API");
        }

        private static bool Same(MuiColor a, MuiColor b) {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }

        private static double Contrast(MuiColor a, MuiColor b) {
            double first = Luminance(a), second = Luminance(b);
            return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        }

        private static double Luminance(MuiColor color) {
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        private static double Linear(double value) {
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
