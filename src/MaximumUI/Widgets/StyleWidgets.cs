using System;
using System.Collections.Generic;

namespace MaximumUI {
    public class ThemeEditor : Widget {
        public ThemeTokens Tokens;
        public Action<ThemeTokens> OnChange;
        public bool ShowColors;
        public bool ShowScalars;
        public int MaxRows;
        public float ScalarStep;

        public ThemeEditor(ThemeTokens tokens, Action<ThemeTokens> onChange = null, object key = null)
            : base(key) {
            Tokens = tokens ?? new ThemeTokens();
            OnChange = onChange;
            ShowColors = true;
            ShowScalars = true;
            MaxRows = 14;
            ScalarStep = 1f;
        }
    }

    public sealed class StyleEditorWindow : ThemeEditor {
        public StyleEditorWindow(ThemeTokens tokens, Action<ThemeTokens> onChange = null, object key = null)
            : base(tokens, onChange, key) {
        }
    }

    public sealed class FontSelector : Widget, IComboBoxWidget {
        public string Label;
        public string Value;
        public IList<string> Options;
        public Action<string> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;

        public FontSelector(string label, string value, IEnumerable<string> fonts = null, Action<string> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Options = CopyStrings(fonts);
            if (Options.Count == 0) {
                Options.Add("Default");
                Options.Add("Monospace");
                Options.Add("Small");
                Options.Add("Large");
            }
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Options; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            return value == null ? string.Empty : value.ToString();
        }

        private static IList<string> CopyStrings(IEnumerable<string> source) {
            List<string> result = new List<string>();
            if (source == null) return result;

            foreach (string item in source) {
                if (item != null) result.Add(item);
            }

            return result;
        }
    }

    public sealed class FontSizeSelector : Widget, IComboBoxWidget {
        public string Label;
        public float Value;
        public IList<float> Options;
        public Action<float> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;

        public FontSizeSelector(string label, float value, IEnumerable<float> sizes = null, Action<float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Options = CopyFloats(sizes);
            if (Options.Count == 0) {
                Options.Add(10f);
                Options.Add(11f);
                Options.Add(12f);
                Options.Add(14f);
                Options.Add(16f);
                Options.Add(18f);
                Options.Add(20f);
            }
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Options; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            if (value is float) return ((float)value).ToString("0");
            return value == null ? string.Empty : value.ToString();
        }

        private static IList<float> CopyFloats(IEnumerable<float> source) {
            List<float> result = new List<float>();
            if (source == null) return result;

            foreach (float item in source) result.Add(item);
            return result;
        }
    }

    public sealed class StyleSelector : Widget, IComboBoxWidget {
        public string Label;
        public string Value;
        public IList<string> Options;
        public Action<string> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;

        public StyleSelector(string label, string value, IEnumerable<string> styles = null, Action<string> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Options = CopyStrings(styles);
            if (Options.Count == 0) {
                Options.Add("Default");
                Options.Add("Compact");
                Options.Add("High Contrast");
            }
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Options; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            return value == null ? string.Empty : value.ToString();
        }

        private static IList<string> CopyStrings(IEnumerable<string> source) {
            List<string> result = new List<string>();
            if (source == null) return result;

            foreach (string item in source) {
                if (item != null) result.Add(item);
            }

            return result;
        }
    }

    public sealed class ThemeSelector : Widget, IComboBoxWidget {
        public string Label;
        public string Value;
        public IList<string> Options;
        public Action<string> OnChange;
        public float LabelWidth;
        public int MaxVisibleItems;

        public ThemeSelector(string label, string value, IEnumerable<string> themes = null, Action<string> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Options = CopyStrings(themes);
            if (Options.Count == 0) {
                Options.Add("Expression Light");
                Options.Add("Classic");
                Options.Add("Debug Contrast");
            }
            OnChange = onChange;
            LabelWidth = 0f;
            MaxVisibleItems = 8;
        }

        string IComboBoxWidget.ComboLabel {
            get { return Label; }
        }

        object IComboBoxWidget.ComboValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IComboBoxWidget.ComboOptions {
            get { return Options; }
        }

        Delegate IComboBoxWidget.ComboOnChange {
            get { return OnChange; }
        }

        float IComboBoxWidget.ComboLabelWidth {
            get { return LabelWidth; }
        }

        int IComboBoxWidget.ComboMaxVisibleItems {
            get { return MaxVisibleItems; }
        }

        string IComboBoxWidget.GetComboText(object value) {
            return value == null ? string.Empty : value.ToString();
        }

        private static IList<string> CopyStrings(IEnumerable<string> source) {
            List<string> result = new List<string>();
            if (source == null) return result;

            foreach (string item in source) {
                if (item != null) result.Add(item);
            }

            return result;
        }
    }

    public sealed class FontAtlasViewer : Widget, IDebugLinesWidget {
        public string FontName;
        public int TextureWidth;
        public int TextureHeight;
        public int GlyphCount;
        public string SampleText;
        public IList<string> Diagnostics;
        public int Rows;

        public FontAtlasViewer(object key = null)
            : base(key) {
            FontName = "Default";
            TextureWidth = 0;
            TextureHeight = 0;
            GlyphCount = 0;
            SampleText = "The quick brown fox 0123456789";
            Diagnostics = new List<string>();
            Rows = 7;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Font Atlas"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            lines.Add("Font: " + (FontName ?? string.Empty));
            lines.Add("Texture: " + TextureWidth.ToString() + "x" + TextureHeight.ToString());
            lines.Add("Glyphs: " + GlyphCount.ToString());
            if (!string.IsNullOrEmpty(SampleText)) lines.Add("Sample: " + SampleText);
            if (Diagnostics != null) {
                for (int i = 0; i < Diagnostics.Count; i++) {
                    if (!string.IsNullOrEmpty(Diagnostics[i])) lines.Add(Diagnostics[i]);
                }
            }
            return lines;
        }
    }

    public sealed class FallbackGlyphDiagnostics : Widget, IDebugLinesWidget {
        public string FontName;
        public string SampleText;
        public IList<char> MissingGlyphs;
        public int Rows;

        public FallbackGlyphDiagnostics(object key = null)
            : base(key) {
            FontName = "Default";
            SampleText = string.Empty;
            MissingGlyphs = new List<char>();
            Rows = 6;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Fallback Glyphs"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            lines.Add("Font: " + (FontName ?? string.Empty));
            if (!string.IsNullOrEmpty(SampleText)) lines.Add("Sample: " + SampleText);
            if (MissingGlyphs == null || MissingGlyphs.Count == 0) {
                lines.Add("No missing glyphs reported.");
                return lines;
            }

            lines.Add("Missing glyphs: " + MissingGlyphs.Count.ToString());
            for (int i = 0; i < MissingGlyphs.Count; i++) {
                lines.Add("'" + MissingGlyphs[i].ToString() + "' U+" + ((int)MissingGlyphs[i]).ToString("X4"));
            }
            return lines;
        }
    }

    internal static class ThemeEditorTokens {
        public static readonly ThemeColorToken[] ColorTokens = new ThemeColorToken[] {
            ThemeColorToken.WindowBg,
            ThemeColorToken.PanelBg,
            ThemeColorToken.DialogBg,
            ThemeColorToken.DropdownBg,
            ThemeColorToken.Backdrop,
            ThemeColorToken.Text,
            ThemeColorToken.TextDisabled,
            ThemeColorToken.Border,
            ThemeColorToken.Separator,
            ThemeColorToken.Accent,
            ThemeColorToken.ButtonBg,
            ThemeColorToken.ButtonHoverBg,
            ThemeColorToken.ButtonActiveBg,
            ThemeColorToken.ButtonDisabledBg,
            ThemeColorToken.FieldBg,
            ThemeColorToken.FieldHoverBg,
            ThemeColorToken.FieldActiveBg,
            ThemeColorToken.SelectedBg,
            ThemeColorToken.InfoBg,
            ThemeColorToken.SuccessBg,
            ThemeColorToken.WarningBg,
            ThemeColorToken.ErrorBg
        };

        public static readonly ThemeScalarToken[] ScalarTokens = new ThemeScalarToken[] {
            ThemeScalarToken.BorderWidth,
            ThemeScalarToken.Radius,
            ThemeScalarToken.FontSize,
            ThemeScalarToken.ToggleBoxSize,
            ThemeScalarToken.SliderTrackHeight,
            ThemeScalarToken.SliderThumbWidth,
            ThemeScalarToken.SliderThumbHeight
        };

        public static string ColorName(ThemeColorToken token) {
            if (token == ThemeColorToken.WindowBg) return "WindowBg";
            if (token == ThemeColorToken.PanelBg) return "PanelBg";
            if (token == ThemeColorToken.DialogBg) return "DialogBg";
            if (token == ThemeColorToken.DropdownBg) return "DropdownBg";
            if (token == ThemeColorToken.Backdrop) return "Backdrop";
            if (token == ThemeColorToken.Text) return "Text";
            if (token == ThemeColorToken.TextDisabled) return "TextDisabled";
            if (token == ThemeColorToken.Border) return "Border";
            if (token == ThemeColorToken.Separator) return "Separator";
            if (token == ThemeColorToken.Accent) return "Accent";
            if (token == ThemeColorToken.ButtonBg) return "ButtonBg";
            if (token == ThemeColorToken.ButtonHoverBg) return "ButtonHoverBg";
            if (token == ThemeColorToken.ButtonActiveBg) return "ButtonActiveBg";
            if (token == ThemeColorToken.ButtonDisabledBg) return "ButtonDisabledBg";
            if (token == ThemeColorToken.FieldBg) return "FieldBg";
            if (token == ThemeColorToken.FieldHoverBg) return "FieldHoverBg";
            if (token == ThemeColorToken.FieldActiveBg) return "FieldActiveBg";
            if (token == ThemeColorToken.SelectedBg) return "SelectedBg";
            if (token == ThemeColorToken.InfoBg) return "InfoBg";
            if (token == ThemeColorToken.SuccessBg) return "SuccessBg";
            if (token == ThemeColorToken.WarningBg) return "WarningBg";
            return "ErrorBg";
        }

        public static string ScalarName(ThemeScalarToken token) {
            if (token == ThemeScalarToken.BorderWidth) return "BorderWidth";
            if (token == ThemeScalarToken.Radius) return "Radius";
            if (token == ThemeScalarToken.FontSize) return "FontSize";
            if (token == ThemeScalarToken.ToggleBoxSize) return "ToggleBoxSize";
            if (token == ThemeScalarToken.SliderTrackHeight) return "SliderTrackHeight";
            if (token == ThemeScalarToken.SliderThumbWidth) return "SliderThumbWidth";
            return "SliderThumbHeight";
        }

        public static int VisibleRowCount(ThemeEditor editor) {
            int count = TotalRowCount(editor);
            if (editor != null && editor.MaxRows > 0 && count > editor.MaxRows) count = editor.MaxRows;
            return count;
        }

        public static int TotalRowCount(ThemeEditor editor) {
            if (editor == null) return 1;

            int count = 0;
            if (editor.ShowColors) count += ColorTokens.Length;
            if (editor.ShowScalars) count += ScalarTokens.Length;
            return count;
        }
    }
}
