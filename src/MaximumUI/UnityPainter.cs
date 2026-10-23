using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class UnityPainter {
        private readonly Texture2D white;
        private readonly Stack<MuiVec2> clipOrigins = new Stack<MuiVec2>();
        private MuiVec2 clipOrigin;

        public UnityPainter() {
            white = Textures.Solid(Color.white);
        }

        public void Paint(DrawList drawList) {
            if (drawList == null) return;

            clipOrigins.Clear();
            clipOrigin = new MuiVec2(0f, 0f);
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
        }

        private void Stroke(MuiRect rect, MuiColor color, float thickness) {
            float t = thickness <= 0f ? 1f : thickness;
            DrawTexture(new MuiRect(rect.X, rect.Y, rect.Width, t), white, color);
            DrawTexture(new MuiRect(rect.X, rect.Bottom - t, rect.Width, t), white, color);
            DrawTexture(new MuiRect(rect.X, rect.Y, t, rect.Height), white, color);
            DrawTexture(new MuiRect(rect.Right - t, rect.Y, t, rect.Height), white, color);
        }

        private void DrawText(DrawCommand command) {
            GUIStyle style = command.Resource as GUIStyle;
            if (style == null) style = GUI.skin.label;

            Color oldColor = GUI.contentColor;
            GUI.contentColor = ToColor(command.Color);
            GUI.Label(ToRectRelative(command.Rect), command.Text ?? string.Empty, style);
            GUI.contentColor = oldColor;
        }

        private void DrawTexture(MuiRect rect, Texture texture, MuiColor color) {
            if (texture == null) return;

            Color oldColor = GUI.color;
            GUI.color = ToColor(color);
            GUI.DrawTexture(ToRectRelative(rect), texture);
            GUI.color = oldColor;
        }

        private void DrawLine(MuiVec2 start, MuiVec2 end, MuiColor color, float thickness) {
            start = ToPointRelative(start);
            end = ToPointRelative(end);
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length <= 0.01f) {
                float size = thickness <= 0f ? 1f : thickness;
                DrawTexture(new MuiRect(start.X, start.Y, size, size), white, color);
                return;
            }

            float t = thickness <= 0f ? 1f : thickness;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = ToColor(color);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, new Vector2(start.X, start.Y));
            GUI.DrawTexture(new Rect(start.X, start.Y - t * 0.5f, length, t), white);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
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
            MuiVec2[] points = new MuiVec2[count + 1];
            for (int i = 0; i <= count; i++) {
                float angle = (float)(Math.PI * 2.0 * i / count);
                points[i] = new MuiVec2(center.X + Mathf.Cos(angle) * radius, center.Y + Mathf.Sin(angle) * radius);
            }
            DrawPolyline(points, color, thickness);
        }

        private void FillCircle(MuiVec2 center, float radius, MuiColor color) {
            if (radius <= 0f) return;

            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++) {
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y));
                DrawTexture(new MuiRect(center.X - halfWidth, center.Y + y, halfWidth * 2f, 1f), white, color);
            }
        }

        private void FillTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color) {
            MuiVec2[] points = new MuiVec2[] { a, b, c };
            Array.Sort(points, CompareY);
            a = points[0];
            b = points[1];
            c = points[2];

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

        private static int CompareY(MuiVec2 a, MuiVec2 b) {
            return a.Y.CompareTo(b.Y);
        }

        private static bool TriangleScanline(MuiVec2 a, MuiVec2 b, MuiVec2 c, float y, out float x1, out float x2) {
            x1 = 0f;
            x2 = 0f;
            float[] xs = new float[3];
            int count = 0;
            AddIntersection(a, b, y, xs, ref count);
            AddIntersection(b, c, y, xs, ref count);
            AddIntersection(c, a, y, xs, ref count);
            if (count < 2) return false;
            x1 = xs[0];
            x2 = xs[1];
            if (count > 2) {
                x1 = Mathf.Min(x1, xs[2]);
                x2 = Mathf.Max(x2, xs[2]);
            }
            return true;
        }

        private static void AddIntersection(MuiVec2 a, MuiVec2 b, float y, float[] xs, ref int count) {
            if (count >= xs.Length) return;
            if (Mathf.Abs(a.Y - b.Y) <= 0.0001f) return;
            float min = Mathf.Min(a.Y, b.Y);
            float max = Mathf.Max(a.Y, b.Y);
            if (y < min || y >= max) return;
            float t = (y - a.Y) / (b.Y - a.Y);
            xs[count++] = a.X + (b.X - a.X) * t;
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
