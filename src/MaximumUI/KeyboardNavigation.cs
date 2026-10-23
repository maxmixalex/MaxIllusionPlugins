using System;
using UnityEngine;

namespace MaximumUI {
    internal static class KeyboardNavigation {
        public static bool IsActivation(KeyCode key, bool control, bool alt, bool command) {
            return !control && !alt && !command &&
                (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Space);
        }

        public static bool MoveSelection(int selected, int count, KeyCode key, int pageSize, out int next) {
            next = count <= 0 ? -1 : Math.Max(0, Math.Min(count - 1, selected));
            if (count <= 0) return false;
            if (key == KeyCode.DownArrow || key == KeyCode.RightArrow) next = selected < 0 ? 0 : next + 1;
            else if (key == KeyCode.UpArrow || key == KeyCode.LeftArrow) next = selected < 0 ? count - 1 : next - 1;
            else if (key == KeyCode.Home) next = 0;
            else if (key == KeyCode.End) next = count - 1;
            else if (key == KeyCode.PageDown) next += Math.Max(1, pageSize);
            else if (key == KeyCode.PageUp) next -= Math.Max(1, pageSize);
            else return false;
            next = Math.Max(0, Math.Min(count - 1, next));
            return true;
        }

        public static float RevealRow(float scroll, int row, float rowHeight, float viewportHeight, int count) {
            float height = Math.Max(1f, rowHeight);
            float viewport = Math.Max(0f, viewportHeight);
            float maxScroll = Math.Max(0f, Math.Max(0, count) * height - viewport);
            float top = Math.Max(0, row) * height;
            if (top < scroll) scroll = top;
            else if (top + height > scroll + viewport) scroll = top + height - viewport;
            return Math.Max(0f, Math.Min(maxScroll, scroll));
        }

        public static bool MoveSlider(float value, float min, float max, KeyCode key, bool integer, bool fast, out float next) {
            next = value;
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsInfinity(min) || float.IsInfinity(max)) return false;
            if (min > max) { float swap = min; min = max; max = swap; }
            double step = integer ? 1d : ((double)max - min) / 100d;
            if (fast) step *= 10d;
            double candidate = float.IsNaN(value) ? min : value;
            if (key == KeyCode.Home) candidate = min;
            else if (key == KeyCode.End) candidate = max;
            else if (key == KeyCode.RightArrow || key == KeyCode.UpArrow) candidate += step;
            else if (key == KeyCode.LeftArrow || key == KeyCode.DownArrow) candidate -= step;
            else return false;
            next = (float)Math.Max(min, Math.Min(max, candidate));
            return true;
        }
    }
}
