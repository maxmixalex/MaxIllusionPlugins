using System;
using System.Collections.Generic;

namespace MaximumUI {
    /// <summary>Shared, allocation-free plot geometry for native painting and exported frames.</summary>
    public static class PlotGeometry {
        public static MuiRect ContentRect(MuiRect bounds) {
            return PopupGeometry.Inset(bounds, 2f, 2f, 2f, 2f);
        }

        public static MuiRect OverlayBounds(MuiRect bounds, float textWidth, float textHeight) {
            MuiRect content = ContentRect(bounds);
            float width = Math.Min(content.Width, Math.Max(0f, textWidth) + 8f);
            float height = Math.Min(content.Height, Math.Max(0f, textHeight) + 4f);
            return new MuiRect(content.X + (content.Width - width) * 0.5f,
                content.Y + (content.Height - height) * 0.5f, width, height);
        }

        public static void Range(IList<float> values, bool hasRange, float explicitMin, float explicitMax,
            out float min, out float max) {
            if (hasRange && Finite(explicitMin) && Finite(explicitMax) && explicitMax > explicitMin) {
                min = explicitMin;
                max = explicitMax;
                return;
            }
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            if (values != null) for (int i = 0; i < values.Count; i++) {
                float value = values[i];
                if (!Finite(value)) continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            if (!Finite(min) || !Finite(max)) { min = 0f; max = 1f; }
            else if (max <= min) max = min + 1f;
        }

        /// <summary>Missing/non-finite values return false, allowing a line to break across gaps.</summary>
        public static bool TryPoint(IList<float> values, int index, MuiRect content, float min, float max,
            out MuiVec2 point) {
            point = new MuiVec2();
            if (values == null || index < 0 || index >= values.Count || content.Width <= 0f || content.Height <= 0f ||
                !Finite(values[index]) || !Finite(min) || !Finite(max)) return false;
            double range = (double)max - min;
            float fraction = range <= 0d ? 0f : (float)Math.Max(0d, Math.Min(1d, ((double)values[index] - min) / range));
            float x = values.Count == 1 ? content.X + content.Width * 0.5f : content.X + content.Width * index / (values.Count - 1);
            point = new MuiVec2(x, content.Bottom - content.Height * fraction);
            return true;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
