using System;

namespace MaximumUI {
    /// <summary>Small, consistent line icons. Geometry is shared by the native and managed renderers.</summary>
    public enum Mdl2Icon {
        ChevronLeft, ChevronRight, ChevronUp, ChevronDown,
        Close, Add, Remove, Check, Search, More
    }

    public static class Mdl2IconGeometry {
        private static readonly MuiVec2[] SearchRing = CreateSearchRing();

        public static int SegmentCount(Mdl2Icon icon) {
            if (icon == Mdl2Icon.Remove) return 1;
            if (icon == Mdl2Icon.Search) return 13;
            if (icon == Mdl2Icon.More) return 3;
            return 2;
        }

        /// <summary>Returns one line in a centered 16px box, without allocation or font dependencies.</summary>
        public static void Segment(Mdl2Icon icon, int index, MuiRect rect, out MuiVec2 start, out MuiVec2 end) {
            float x1 = 0f, y1 = 0f, x2 = 0f, y2 = 0f;
            if (index < 0 || index >= SegmentCount(icon)) {
                start = end = new MuiVec2(rect.X, rect.Y);
                return;
            }
            if (icon == Mdl2Icon.ChevronDown) {
                x1 = index == 0 ? 3f : 8f; y1 = index == 0 ? 6f : 11f;
                x2 = index == 0 ? 8f : 13f; y2 = index == 0 ? 11f : 6f;
            } else if (icon == Mdl2Icon.ChevronUp) {
                x1 = index == 0 ? 3f : 8f; y1 = index == 0 ? 10f : 5f;
                x2 = index == 0 ? 8f : 13f; y2 = index == 0 ? 5f : 10f;
            } else if (icon == Mdl2Icon.ChevronRight) {
                x1 = index == 0 ? 6f : 11f; y1 = index == 0 ? 3f : 8f;
                x2 = index == 0 ? 11f : 6f; y2 = index == 0 ? 8f : 13f;
            } else if (icon == Mdl2Icon.ChevronLeft) {
                x1 = index == 0 ? 10f : 5f; y1 = index == 0 ? 3f : 8f;
                x2 = index == 0 ? 5f : 10f; y2 = index == 0 ? 8f : 13f;
            } else if (icon == Mdl2Icon.Close) {
                x1 = 3f; y1 = index == 0 ? 3f : 13f;
                x2 = 13f; y2 = index == 0 ? 13f : 3f;
            } else if (icon == Mdl2Icon.Add) {
                x1 = index == 0 ? 3f : 8f; y1 = index == 0 ? 8f : 3f;
                x2 = index == 0 ? 13f : 8f; y2 = index == 0 ? 8f : 13f;
            } else if (icon == Mdl2Icon.Remove) {
                x1 = 3f; y1 = 8f; x2 = 13f; y2 = 8f;
            } else if (icon == Mdl2Icon.Check) {
                x1 = index == 0 ? 2f : 6f; y1 = index == 0 ? 8f : 12f;
                x2 = index == 0 ? 6f : 14f; y2 = index == 0 ? 12f : 4f;
            } else if (icon == Mdl2Icon.More) {
                x1 = 3f + index * 5f; y1 = 8f; x2 = x1 + 0.5f; y2 = 8f;
            } else if (icon == Mdl2Icon.Search) {
                if (index == 12) { x1 = 10f; y1 = 10f; x2 = 14f; y2 = 14f; }
                else {
                    MuiVec2 a = SearchRing[index], b = SearchRing[(index + 1) % SearchRing.Length];
                    x1 = a.X; y1 = a.Y; x2 = b.X; y2 = b.Y;
                }
            }
            float size = Math.Max(0f, Math.Min(16f, Math.Min(rect.Width, rect.Height)));
            float scale = size / 16f;
            float left = rect.X + (rect.Width - size) * 0.5f;
            float top = rect.Y + (rect.Height - size) * 0.5f;
            start = new MuiVec2(left + x1 * scale, top + y1 * scale);
            end = new MuiVec2(left + x2 * scale, top + y2 * scale);
        }

        public static void Draw(DrawList drawing, MuiRect rect, Mdl2Icon icon, MuiColor color, float thickness) {
            if (drawing == null || rect.Width <= 0f || rect.Height <= 0f) return;
            for (int i = 0; i < SegmentCount(icon); i++) {
                MuiVec2 a, b;
                Segment(icon, i, rect, out a, out b);
                drawing.Line(a, b, color, thickness);
            }
        }

        public static Mdl2Icon Chevron(ArrowDirection direction) {
            if (direction == ArrowDirection.Left) return Mdl2Icon.ChevronLeft;
            if (direction == ArrowDirection.Right) return Mdl2Icon.ChevronRight;
            if (direction == ArrowDirection.Up) return Mdl2Icon.ChevronUp;
            return Mdl2Icon.ChevronDown;
        }

        private static MuiVec2[] CreateSearchRing() {
            MuiVec2[] points = new MuiVec2[12];
            for (int i = 0; i < points.Length; i++) {
                double angle = i * Math.PI * 2d / points.Length;
                points[i] = new MuiVec2(6.5f + (float)Math.Cos(angle) * 4.5f, 6.5f + (float)Math.Sin(angle) * 4.5f);
            }
            return points;
        }
    }

    /// <summary>Shared state and geometry rules for MDL2 selection controls and input borders.</summary>
    public static class Mdl2ControlChrome {
        public static MuiRect SelectionBounds(MuiRect rect, float size) {
            size = Math.Max(0f, Math.Min(Math.Min(size, rect.Width), Math.Max(0f, rect.Height - 4f)));
            return new MuiRect(rect.X, rect.Y + Math.Max(0f, (rect.Height - size) * 0.5f), size, size);
        }

        public static MuiColor SelectionColor(ThemeTokens theme, bool enabled, bool hover, bool pressed) {
            if (!enabled) return theme.TextDisabled;
            return pressed ? theme.AccentPressed : hover ? theme.AccentHover : theme.Accent;
        }

        public static MuiColor ControlBorderColor(ThemeTokens theme, bool enabled, bool hover) {
            return !enabled ? theme.TextDisabled : hover ? theme.ControlHoverBorder : theme.ControlBorder;
        }

        public static MuiColor FieldBorderColor(ThemeTokens theme, bool enabled, bool hover, bool focused) {
            return enabled && focused ? theme.Accent : ControlBorderColor(theme, enabled, hover);
        }

        public static MuiRect SwitchBounds(MuiRect rect) {
            float height = Math.Min(20f, Math.Max(0f, rect.Height));
            return new MuiRect(rect.X, rect.Y + (rect.Height - height) * 0.5f, Math.Min(40f, rect.Width), height);
        }

        public static MuiRect SwitchThumb(MuiRect bounds, bool value) {
            float inset = Math.Min(4f, Math.Min(bounds.Width, bounds.Height) * 0.5f);
            float size = Math.Max(0f, Math.Min(bounds.Height - inset * 2f, bounds.Width - inset * 2f));
            return new MuiRect(value ? bounds.Right - size - inset : bounds.X + inset, bounds.Y + (bounds.Height - size) * 0.5f, size, size);
        }

        public static MuiRect FocusBounds(MuiRect rect) {
            float inset = Math.Max(0f, Math.Min(2f, Math.Min(rect.Width, rect.Height) * 0.5f));
            return new MuiRect(rect.X + inset, rect.Y + inset, Math.Max(0f, rect.Width - inset * 2f), Math.Max(0f, rect.Height - inset * 2f));
        }
    }
}
