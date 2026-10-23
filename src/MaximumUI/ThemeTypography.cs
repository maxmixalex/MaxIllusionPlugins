using System;

namespace MaximumUI {
    public enum TextAlignment { Start, Center, End }
    public enum TextVerticalAlignment { Middle, Top, Bottom }
    public enum TextWeight { Normal, Light, Semilight, Semibold, Bold }

    /// <summary>Portable text properties shared by native painting and exported previews.</summary>
    public struct TextFormat {
        /// <summary>Zero preserves an explicitly supplied native style or the painter's body style.</summary>
        public float FontSize;
        public TextWeight Weight;
        public TextAlignment Alignment;
        public TextVerticalAlignment VerticalAlignment;
        public bool WordWrap;

        public TextFormat(float fontSize, TextAlignment alignment = TextAlignment.Start,
            TextWeight weight = TextWeight.Normal, bool wordWrap = false,
            TextVerticalAlignment verticalAlignment = TextVerticalAlignment.Middle) {
            FontSize = fontSize;
            Alignment = alignment;
            Weight = weight;
            WordWrap = wordWrap;
            VerticalAlignment = verticalAlignment;
        }
    }

    /// <summary>MDL2 desktop density. No native Unity objects are needed to inspect these metrics.</summary>
    public static class ThemeTypography {
        public const float DefaultFontSize = 14f;

        public static float FontSize(ThemeTokens tokens, LabelRole role) {
            float size = tokens == null ? DefaultFontSize : tokens.FontSize;
            float reference = role == LabelRole.Heading ? 24f : role == LabelRole.Subheading ? 20f :
                role == LabelRole.Caption ? 12f : DefaultFontSize;
            return (float)Math.Round(reference * size / DefaultFontSize);
        }

        public static float LineHeight(ThemeTokens tokens, LabelRole role) {
            float reference = role == LabelRole.Heading ? 28f : role == LabelRole.Subheading ? 24f :
                role == LabelRole.Caption ? 16f : 20f;
            float size = tokens == null ? DefaultFontSize : tokens.FontSize;
            float height = (float)Math.Ceiling(reference * size / DefaultFontSize);
            // Enlarged light-face headings need room for their full descenders.
            // Keep the standard 24/28 step, then reserve one grid unit when
            // scaling up instead of clipping the larger glyphs at 31/36.
            if (role == LabelRole.Heading && size > DefaultFontSize)
                height = (float)Math.Ceiling(height / 4f) * 4f + 4f;
            return height;
        }

        public static MuiRect DialogTitleBounds(MuiRect dialog, ThemeTokens tokens) {
            return new MuiRect(dialog.X + 24f, dialog.Y + 16f, Math.Max(0f, dialog.Width - 48f),
                Math.Max(32f, LineHeight(tokens, LabelRole.Heading)));
        }

        public static TextFormat Format(ThemeTokens tokens, LabelRole role = LabelRole.Body,
            bool wordWrap = false, TextAlignment alignment = TextAlignment.Start) {
            return new TextFormat(FontSize(tokens, role), alignment,
                role == LabelRole.Heading ? TextWeight.Semilight : TextWeight.Normal, wordWrap);
        }

        public static LayoutMetrics CreateLayoutMetrics(ThemeTokens tokens) {
            float fontSize = tokens == null ? DefaultFontSize : tokens.FontSize;
            float row = Math.Max(32f, (float)Math.Ceiling((LineHeight(tokens, LabelRole.Body) + 12f) / 4f) * 4f);
            float title = Math.Max(32f, (float)Math.Ceiling((fontSize + 16f) / 4f) * 4f);
            return new LayoutMetrics {
                RowHeight = row,
                SmallRowHeight = Math.Max(24f, (float)Math.Ceiling((fontSize + 8f) / 4f) * 4f),
                PanelPadding = 12f,
                TitleHeight = title + 16f,
                WindowPaddingLeft = 16f, WindowPaddingRight = 16f,
                WindowPaddingTop = title + 16f, WindowPaddingBottom = 16f,
                DialogPaddingLeft = 24f, DialogPaddingRight = 24f,
                DialogPaddingTop = Math.Max(title + 32f, Math.Max(32f, LineHeight(tokens, LabelRole.Heading)) + 28f), DialogPaddingBottom = 24f,
                FoldoutIndent = 20f,
                TextWidth = fontSize * 0.5f,
                MeasureText = delegate(string text) { return EstimateTextWidth(text, fontSize); },
                MeasureLabelWidth = delegate(Label label) {
                    return EstimateTextWidth(label == null ? string.Empty : label.Text, FontSize(tokens, label == null ? LabelRole.Body : label.Role));
                },
                MeasureLabelHeight = delegate(Label label, float width) {
                    LabelRole role = label == null ? LabelRole.Body : label.Role;
                    float lines = label != null && label.WordWrap && width > 0f && !float.IsInfinity(width)
                        ? Math.Max(1f, (float)Math.Ceiling(EstimateTextWidth(label.Text, FontSize(tokens, role)) / width)) : 1f;
                    return LineHeight(tokens, role) * lines;
                }
            };
        }

        /// <summary>Portable fallback only; native themes and shaped previews replace this with actual font measurement.</summary>
        public static float EstimateTextWidth(string text, float fontSize) {
            if (string.IsNullOrEmpty(text)) return 0f;
            float width = 0f;
            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (char.IsLowSurrogate(ch) || char.IsControl(ch)) continue;
                width += ch >= 0x2e80 ? fontSize : ch == 'i' || ch == 'l' || ch == ' ' || ch == '.' || ch == ',' ? fontSize * 0.28f :
                    ch == 'W' || ch == 'M' || ch == '@' ? fontSize * 0.85f : fontSize * 0.54f;
            }
            return width;
        }
    }
}
