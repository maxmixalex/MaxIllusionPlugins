using System.Collections.Generic;

namespace MaximumUI {
    public enum DrawCommandType {
        PushClip,
        PopClip,
        FillRect,
        StrokeRect,
        Line,
        Polyline,
        FillTriangle,
        StrokeTriangle,
        FillCircle,
        StrokeCircle,
        Bezier,
        Text,
        Image
    }

    public sealed class DrawCommand {
        public DrawCommandType Type;
        public MuiRect Rect;
        public MuiColor Color;
        public MuiVec2 P1;
        public MuiVec2 P2;
        public MuiVec2 P3;
        public MuiVec2 P4;
        public MuiVec2[] Points;
        public string Text;
        public object Resource;
        public float Thickness;
        public float Radius;
        public int Segments;
        public TextFormat TextFormat;
    }

    public sealed class DrawList {
        private readonly List<DrawCommand> commands = new List<DrawCommand>();
        public TextFormat DefaultTextFormat;

        public IList<DrawCommand> Commands {
            get { return commands; }
        }

        public void Clear() {
            commands.Clear();
        }

        public void PushClip(MuiRect rect) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.PushClip,
                Rect = rect
            });
        }

        public void PopClip() {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.PopClip
            });
        }

        public void FillRect(MuiRect rect, MuiColor color) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.FillRect,
                Rect = rect,
                Color = color
            });
        }

        public void StrokeRect(MuiRect rect, MuiColor color, float thickness) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.StrokeRect,
                Rect = rect,
                Color = color,
                Thickness = thickness
            });
        }

        public void Line(MuiVec2 start, MuiVec2 end, MuiColor color, float thickness) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.Line,
                P1 = start,
                P2 = end,
                Color = color,
                Thickness = thickness
            });
        }

        public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness) {
            Polyline(points, color, thickness, false);
        }

        public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness, bool closed) {
            if (points == null || points.Count == 0) return;

            MuiVec2[] copy = new MuiVec2[points.Count + (closed && points.Count > 1 ? 1 : 0)];
            for (int i = 0; i < points.Count; i++) copy[i] = points[i];
            if (copy.Length > points.Count) copy[copy.Length - 1] = points[0];

            commands.Add(new DrawCommand {
                Type = DrawCommandType.Polyline,
                Points = copy,
                Color = color,
                Thickness = thickness
            });
        }

        public void FillTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.FillTriangle,
                P1 = a,
                P2 = b,
                P3 = c,
                Color = color
            });
        }

        public void StrokeTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color, float thickness) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.StrokeTriangle,
                P1 = a,
                P2 = b,
                P3 = c,
                Color = color,
                Thickness = thickness
            });
        }

        public void FillCircle(MuiVec2 center, float radius, MuiColor color) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.FillCircle,
                P1 = center,
                Radius = radius,
                Color = color
            });
        }

        public void StrokeCircle(MuiVec2 center, float radius, MuiColor color, float thickness, int segments) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.StrokeCircle,
                P1 = center,
                Radius = radius,
                Color = color,
                Thickness = thickness,
                Segments = segments
            });
        }

        public void Bezier(MuiVec2 p1, MuiVec2 p2, MuiVec2 p3, MuiVec2 p4, MuiColor color, float thickness, int segments) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.Bezier,
                P1 = p1,
                P2 = p2,
                P3 = p3,
                P4 = p4,
                Color = color,
                Thickness = thickness,
                Segments = segments
            });
        }

        public void Text(MuiRect rect, string text, object fontOrStyle, MuiColor color) {
            Text(rect, text, fontOrStyle, color, DefaultTextFormat);
        }

        public void Text(MuiRect rect, string text, object fontOrStyle, MuiColor color, TextFormat format) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.Text,
                Rect = rect,
                Text = text ?? string.Empty,
                Resource = fontOrStyle,
                Color = color,
                TextFormat = format
            });
        }

        public void Image(MuiRect rect, object texture, MuiColor color) {
            commands.Add(new DrawCommand {
                Type = DrawCommandType.Image,
                Rect = rect,
                Resource = texture,
                Color = color
            });
        }

        public void Append(DrawList source) {
            Append(source, new MuiVec2(0f, 0f));
        }

        public void Append(DrawList source, MuiVec2 offset) {
            if (source == null) return;

            int count = source.Commands.Count;
            for (int i = 0; i < count; i++) {
                DrawCommand command = source.Commands[i];
                if (command == null) continue;
                commands.Add(CloneWithOffset(command, offset));
            }
        }

        private static DrawCommand CloneWithOffset(DrawCommand source, MuiVec2 offset) {
            DrawCommand clone = new DrawCommand {
                Type = source.Type,
                Rect = Offset(source.Rect, offset),
                Color = source.Color,
                P1 = Offset(source.P1, offset),
                P2 = Offset(source.P2, offset),
                P3 = Offset(source.P3, offset),
                P4 = Offset(source.P4, offset),
                Text = source.Text,
                Resource = source.Resource,
                Thickness = source.Thickness,
                Radius = source.Radius,
                Segments = source.Segments,
                TextFormat = source.TextFormat
            };

            if (source.Points != null) {
                clone.Points = new MuiVec2[source.Points.Length];
                for (int i = 0; i < source.Points.Length; i++) clone.Points[i] = Offset(source.Points[i], offset);
            }

            return clone;
        }

        private static MuiRect Offset(MuiRect rect, MuiVec2 offset) {
            return new MuiRect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height);
        }

        private static MuiVec2 Offset(MuiVec2 point, MuiVec2 offset) {
            return new MuiVec2(point.X + offset.X, point.Y + offset.Y);
        }
    }
}
