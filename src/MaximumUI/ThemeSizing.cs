using System;

namespace MaximumUI {
    internal static class ThemeSizing {
        public static int ScalePixels(int value, float screenWidth) {
            float scale = 1f + (screenWidth / 1280f - 1f) * 0.6f;
            if (float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            scale = Math.Max(1f, Math.Min(1.5f, scale));
            return Math.Max(1, (int)Math.Round(value * scale));
        }
    }
}
