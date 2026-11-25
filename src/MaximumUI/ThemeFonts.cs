using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    /// <summary>Application-lifetime dynamic fonts. Fonts are resolved once, never in a repaint loop.</summary>
    internal static class ThemeFonts {
        private static readonly Dictionary<TextWeight, Font> fonts = new Dictionary<TextWeight, Font>();

        public static Font Get(TextWeight weight) {
            Font font;
            if (fonts.TryGetValue(weight, out font)) return font;
            string family = weight == TextWeight.Light ? "Segoe UI Light" :
                weight == TextWeight.Semilight ? "Segoe UI Semilight" :
                weight == TextWeight.Semibold ? "Segoe UI Semibold" : "Segoe UI";
            try {
                font = Font.CreateDynamicFontFromOSFont(new[] {
                    family, "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", "Yu Gothic UI",
                    "Meiryo", "Noto Sans CJK SC", "Noto Sans", "Arial"
                }, (int)ThemeTypography.DefaultFontSize);
                if (font != null) font.hideFlags = HideFlags.HideAndDontSave;
            } catch (ArgumentException) {
                // Unity's default font remains a valid fallback on systems without these families.
                font = null;
            }
            fonts.Add(weight, font);
            return font;
        }
    }
}
