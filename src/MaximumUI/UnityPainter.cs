using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class UnityPainter : IDisposable {
        private readonly Texture2D white;
        private Texture2D disc;
        private readonly Dictionary<TextStyleKey, GUIStyle> textStyles = new Dictionary<TextStyleKey, GUIStyle>();
        private GUIStyle defaultTextStyle;
        private MaximumTheme theme;
        private readonly Stack<MuiVec2> clipOrigins = new Stack<MuiVec2>();
        private MuiVec2 clipOrigin;

        public UnityPainter() {
            white = Textures.Solid(Color.white);
        }

        public MaximumTheme Theme {
            get { return theme; }
            set {
                if (object.ReferenceEquals(theme, value)) return;
                theme = value;
                defaultTextStyle = null;
                textStyles.Clear();
            }
        }

        public void Dispose() {
            if (white != null) UnityEngine.Object.Destroy(white);
            if (disc != null) UnityEngine.Object.Destroy(disc);
            textStyles.Clear();
            defaultTextStyle = null;
        }

        public void Paint(DrawList drawList) {
            if (drawList == null || Event.current == null || !GuiEventPolicy.ShouldPaint(Event.current.type)) return;

            clipOrigins.Clear();
            clipOrigin = new MuiVec2(0f, 0f);
            try {
              for (int i = 0; i < drawList.Commands.Count; i++) {
                DrawCommand command = drawList.Commands[i];
                if (command == null) continue;

                if (command.Type == DrawCommandType.PushClip) {
                    GUI.BeginClip(ToRectRelative(command.Rect));
                    clipOrigins.Push(clipOrigin);
                    clipOrigin = new MuiVec2(command.Rect.X, command.Rect.Y);
                } else if (command.Type == DrawCommandType.PopClip) {
                    if (clipOrigins.Count > 0) {
                        GUI.EndClip();
                        clipOrigin = clipOrigins.Pop();
                    }
                } else if (command.Type == DrawCommandType.FillRect) {
                    DrawTexture(command.Rect, white, command.Color);
                } else if (command.Type == DrawCommandType.StrokeRect) {
                    Stroke(command.Rect, command.Color, command.Thickness);
                } else if (command.Type == DrawCommandType.Line) {
                    DrawLine(command.P1, command.P2, command.Color, command.Thickness);
                } else if (command.Type == DrawCommandType.Polyline) {
                    DrawPolyline(command.Points, command.Color, command.Thickness);
                } else if (command.Type == DrawCommandType.FillTriangle) {
                    FillTriangle(command.P1, command.P2, command.P3, command.Color);
                } else if (command.Type == DrawCommandType.StrokeTriangle) {
                    DrawLine(command.P1, command.P2, command.Color, command.Thickness);
                    DrawLine(command.P2, command.P3, command.Color, command.Thickness);
                    DrawLine(command.P3, command.P1, command.Color, command.Thickness);
                } else if (command.Type == DrawCommandType.FillCircle) {
                    FillCircle(command.P1, command.Radius, command.Color);
                } else if (command.Type == DrawCommandType.StrokeCircle) {
                    StrokeCircle(command.P1, command.Radius, command.Color, command.Thickness, command.Segments);
                } else if (command.Type == DrawCommandType.Bezier) {
                    StrokeBezier(command.P1, command.P2, command.P3, command.P4, command.Color, command.Thickness, command.Segments);
                } else if (command.Type == DrawCommandType.Text) {
                    DrawText(command);
                } else if (command.Type == DrawCommandType.Image) {
                    DrawTexture(command.Rect, command.Resource as Texture, command.Color);
                }
              }
            } finally {
                // A malformed/custom draw command must not leave the whole plugin UI clipped.
                while (clipOrigins.Count > 0) {
                    GUI.EndClip();
                    clipOrigin = clipOrigins.Pop();
                }
            }
        }

        private void Stroke(MuiRect rect, MuiColor color, float thickness) {
            float t = thickness <= 0f ? 1f : thickness;
            DrawTexture(new MuiRect(rect.X, rect.Y, rect.Width, t), white, color);
            DrawTexture(new MuiRect(rect.X, rect.Bottom - t, rect.Width, t), white, color);
            DrawTexture(new MuiRect(rect.X, rect.Y, t, rect.Height), white, color);
            DrawTexture(new MuiRect(rect.Right - t, rect.Y, t, rect.Height), white, color);
        }

        private void DrawText(DrawCommand command) {
            GUIStyle source = command.Resource as GUIStyle;
            if (source == null) {
                if (defaultTextStyle == null) {
                    defaultTextStyle = new GUIStyle((theme ?? MaximumTheme.MetroLight).Label);
                    MaximumTheme.SetTextColor(defaultTextStyle, Color.white);
                }
                source = defaultTextStyle;
            }
            TextStyleKey key = new TextStyleKey(source, command.TextFormat);
            GUIStyle style;
            if (!textStyles.TryGetValue(key, out style)) {
                // GUI.contentColor multiplies style color. A black source style used to
                // make every custom color black; normalize a private cached copy only.
                style = new GUIStyle(source);
                TextFormat format = command.TextFormat;
                if (format.FontSize > 0f) {
                    style.fontSize = Math.Max(1, (int)Math.Round(format.FontSize));
                    style.font = ThemeFonts.Get(format.Weight);
                    style.fontStyle = format.Weight == TextWeight.Bold ? FontStyle.Bold : FontStyle.Normal;
                    style.alignment = format.VerticalAlignment == TextVerticalAlignment.Top
                        ? (format.Alignment == TextAlignment.Center ? TextAnchor.UpperCenter : format.Alignment == TextAlignment.End ? TextAnchor.UpperRight : TextAnchor.UpperLeft)
                        : format.VerticalAlignment == TextVerticalAlignment.Bottom
                        ? (format.Alignment == TextAlignment.Center ? TextAnchor.LowerCenter : format.Alignment == TextAlignment.End ? TextAnchor.LowerRight : TextAnchor.LowerLeft)
                        : (format.Alignment == TextAlignment.Center ? TextAnchor.MiddleCenter : format.Alignment == TextAlignment.End ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                    style.wordWrap = format.WordWrap;
                    style.padding = new RectOffset(0, 0, 0, 0);
                    style.clipping = TextClipping.Clip;
                }
                MaximumTheme.SetTextColor(style, Color.white);
                // Keep warm entries when custom drawing exceeds the cache budget.
                // Clearing the whole dictionary here makes a stable 129-style
                // frame recreate every GUIStyle on every repaint.
                if (textStyles.Count < 128) textStyles.Add(key, style);
            }

            Color oldColor = GUI.contentColor;
            try {
                GUI.contentColor = ToColor(command.Color);
                GUI.Label(ToRectRelative(command.Rect), command.Text ?? string.Empty, style);
            } finally {
                GUI.contentColor = oldColor;
            }
        }

        private struct TextStyleKey : IEquatable<TextStyleKey> {
            private readonly GUIStyle source;
            private readonly TextFormat format;
            public TextStyleKey(GUIStyle source, TextFormat format) { this.source = source; this.format = format; }
            public bool Equals(TextStyleKey other) {
                return object.ReferenceEquals(source, other.source) && format.FontSize == other.format.FontSize &&
                    format.Weight == other.format.Weight && format.Alignment == other.format.Alignment &&
                    format.VerticalAlignment == other.format.VerticalAlignment && format.WordWrap == other.format.WordWrap;
            }
            public override bool Equals(object obj) { return obj is TextStyleKey && Equals((TextStyleKey)obj); }
            public override int GetHashCode() {
                unchecked {
                    int hash = source == null ? 0 : source.GetHashCode();
                    hash = hash * 31 + format.FontSize.GetHashCode();
                    hash = hash * 31 + (int)format.Weight;
                    hash = hash * 31 + (int)format.Alignment;
                    hash = hash * 31 + (int)format.VerticalAlignment;
                    return hash * 31 + (format.WordWrap ? 1 : 0);
                }
            }
        }

        private void DrawTexture(MuiRect rect, Texture texture, MuiColor color) {
            if (texture == null) return;

            Color oldColor = GUI.color;
            try {
                GUI.color = ToColor(color);
                GUI.DrawTexture(ToRectRelative(rect), texture);
            } finally {
                GUI.color = oldColor;
            }
        }

        private void DrawLine(MuiVec2 start, MuiVec2 end, MuiColor color, float thickness) {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length <= 0.01f) {
                float size = thickness <= 0f ? 1f : thickness;
                DrawTexture(new MuiRect(start.X, start.Y, size, size), white, color);
                return;
            }

            start = ToPointRelative(start);
            end = ToPointRelative(end);

            float t = thickness <= 0f ? 1f : thickness;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            try {
                GUI.color = ToColor(color);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, new Vector2(start.X, start.Y));
                GUI.DrawTexture(new Rect(start.X, start.Y - t * 0.5f, length, t), white);
            } finally {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
            }
        }

        private void DrawPolyline(MuiVec2[] points, MuiColor color, float thickness) {
            if (points == null || points.Length < 2) return;
            for (int i = 1; i < points.Length; i++) {
                DrawLine(points[i - 1], points[i], color, thickness);
            }
        }

        private void StrokeCircle(MuiVec2 center, float radius, MuiColor color, float thickness, int segments) {
            if (radius <= 0f) return;
            int count = Mathf.Clamp(segments <= 0 ? 32 : segments, 8, 128);
            MuiVec2 previous = new MuiVec2(center.X + radius, center.Y);
            for (int i = 1; i <= count; i++) {
                float angle = (float)(Math.PI * 2.0 * i / count);
                MuiVec2 next = new MuiVec2(center.X + Mathf.Cos(angle) * radius, center.Y + Mathf.Sin(angle) * radius);
                DrawLine(previous, next, color, thickness);
                previous = next;
            }
        }

        private void FillCircle(MuiVec2 center, float radius, MuiColor color) {
            if (radius <= 0f) return;

            if (disc == null) disc = Textures.Disc();
            DrawTexture(new MuiRect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), disc, color);
        }

        private void FillTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color) {
            if (a.Y > b.Y) Swap(ref a, ref b);
            if (b.Y > c.Y) Swap(ref b, ref c);
            if (a.Y > b.Y) Swap(ref a, ref b);

            int minY = Mathf.FloorToInt(a.Y);
            int maxY = Mathf.CeilToInt(c.Y);
            for (int y = minY; y <= maxY; y++) {
                float x1;
                float x2;
                if (!TriangleScanline(a, b, c, y + 0.5f, out x1, out x2)) continue;
                if (x2 < x1) {
                    float tmp = x1;
                    x1 = x2;
                    x2 = tmp;
                }
                DrawTexture(new MuiRect(x1, y, Mathf.Max(1f, x2 - x1), 1f), white, color);
            }
        }

        private void StrokeBezier(MuiVec2 p1, MuiVec2 p2, MuiVec2 p3, MuiVec2 p4, MuiColor color, float thickness, int segments) {
            int count = Mathf.Clamp(segments <= 0 ? 24 : segments, 4, 128);
            MuiVec2 previous = p1;
            for (int i = 1; i <= count; i++) {
                float t = (float)i / count;
                MuiVec2 next = Cubic(p1, p2, p3, p4, t);
                DrawLine(previous, next, color, thickness);
                previous = next;
            }
        }

        private static void Swap(ref MuiVec2 a, ref MuiVec2 b) {
            MuiVec2 temp = a;
            a = b;
            b = temp;
        }

        private static bool TriangleScanline(MuiVec2 a, MuiVec2 b, MuiVec2 c, float y, out float x1, out float x2) {
            x1 = 0f;
            x2 = 0f;
            int count = 0;
            AddIntersection(a, b, y, ref x1, ref x2, ref count);
            AddIntersection(b, c, y, ref x1, ref x2, ref count);
            AddIntersection(c, a, y, ref x1, ref x2, ref count);
            return count >= 2;
        }

        private static void AddIntersection(MuiVec2 a, MuiVec2 b, float y, ref float x1, ref float x2, ref int count) {
            if (Mathf.Abs(a.Y - b.Y) <= 0.0001f) return;
            float min = Mathf.Min(a.Y, b.Y);
            float max = Mathf.Max(a.Y, b.Y);
            if (y < min || y >= max) return;
            float t = (y - a.Y) / (b.Y - a.Y);
            float x = a.X + (b.X - a.X) * t;
            if (count++ == 0) x1 = x2 = x;
            else { x1 = Mathf.Min(x1, x); x2 = Mathf.Max(x2, x); }
        }

        private static MuiVec2 Cubic(MuiVec2 p1, MuiVec2 p2, MuiVec2 p3, MuiVec2 p4, float t) {
            float inv = 1f - t;
            float a = inv * inv * inv;
            float b = 3f * inv * inv * t;
            float c = 3f * inv * t * t;
            float d = t * t * t;
            return new MuiVec2(
                p1.X * a + p2.X * b + p3.X * c + p4.X * d,
                p1.Y * a + p2.Y * b + p3.Y * c + p4.Y * d);
        }

        private Rect ToRectRelative(MuiRect rect) {
            return new Rect(rect.X - clipOrigin.X, rect.Y - clipOrigin.Y, rect.Width, rect.Height);
        }

        private MuiVec2 ToPointRelative(MuiVec2 point) {
            return new MuiVec2(point.X - clipOrigin.X, point.Y - clipOrigin.Y);
        }

        private static Color ToColor(MuiColor color) {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }
}
