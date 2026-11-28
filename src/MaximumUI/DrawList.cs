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
        // Normal DrawLists preserve their existing per-command identity. Only
        // the renderer's private scratch list opts into command recycling.
        private readonly bool reusable;
        // Stable positional slots are faster than Push/Pop-ing thousands
        // of commands on every Repaint. Only used by renderer-owned scratch
        // lists; public DrawList instances are never pooled.
        private readonly List<DrawCommand> retained = new List<DrawCommand>();
        private int nextRetained;
        private const int MaximumRecycledCommands = 4096;
        public TextFormat DefaultTextFormat;

        public DrawList() : this(false) { }

        internal DrawList(bool reusable) { this.reusable = reusable; }

        internal int RecycledCount {
            get { return retained.Count - System.Math.Min(nextRetained, retained.Count); }
        }

        private DrawCommand RentCommand() {
            if (!reusable) return new DrawCommand();
            int slot = nextRetained++;
            if (slot < retained.Count) return retained[slot];
            DrawCommand result = new DrawCommand();
            if (slot < MaximumRecycledCommands) retained.Add(result);
            return result;
        }

        public IList<DrawCommand> Commands {
            get { return commands; }
        }

        public void Clear() {
            if (reusable) {
                for (int i = 0; i < commands.Count; i++) {
                    DrawCommand command = commands[i];
                    if (command == null) continue;
                    // Release object graphs after each event. Values for the
                    // new draw type are overwritten by the next API call.
                    command.Resource = null;
                    command.Text = null;
                    command.Points = null;
                }
                nextRetained = 0;
                // Each DrawCanvas previously received a fresh DrawList.
                // Do not carry one canvas's default typography into another.
                DefaultTextFormat = default(TextFormat);
            }
            commands.Clear();
        }

        public void PushClip(MuiRect rect) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.PushClip;
            command.Rect = rect;
            commands.Add(command);
        }

        public void PopClip() {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.PopClip;
            commands.Add(command);
        }

        public void FillRect(MuiRect rect, MuiColor color) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.FillRect;
            command.Rect = rect;
            command.Color = color;
            commands.Add(command);
        }

        public void StrokeRect(MuiRect rect, MuiColor color, float thickness) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.StrokeRect;
            command.Rect = rect;
            command.Color = color;
            command.Thickness = thickness;
            commands.Add(command);
        }

        public void Line(MuiVec2 start, MuiVec2 end, MuiColor color, float thickness) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.Line;
            command.P1 = start;
            command.P2 = end;
            command.Color = color;
            command.Thickness = thickness;
            commands.Add(command);
        }

        public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness) {
            Polyline(points, color, thickness, false);
        }

        public void Polyline(IList<MuiVec2> points, MuiColor color, float thickness, bool closed) {
            if (points == null || points.Count == 0) return;

            MuiVec2[] copy = new MuiVec2[points.Count + (closed && points.Count > 1 ? 1 : 0)];
            for (int i = 0; i < points.Count; i++) copy[i] = points[i];
            if (copy.Length > points.Count) copy[copy.Length - 1] = points[0];

            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.Polyline;
            command.Points = copy;
            command.Color = color;
            command.Thickness = thickness;
            commands.Add(command);
        }

        public void FillTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.FillTriangle;
            command.P1 = a;
            command.P2 = b;
            command.P3 = c;
            command.Color = color;
            commands.Add(command);
        }

        public void StrokeTriangle(MuiVec2 a, MuiVec2 b, MuiVec2 c, MuiColor color, float thickness) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.StrokeTriangle;
            command.P1 = a;
            command.P2 = b;
            command.P3 = c;
            command.Color = color;
            command.Thickness = thickness;
            commands.Add(command);
        }

        public void FillCircle(MuiVec2 center, float radius, MuiColor color) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.FillCircle;
            command.P1 = center;
            command.Radius = radius;
            command.Color = color;
            commands.Add(command);
        }

        public void StrokeCircle(MuiVec2 center, float radius, MuiColor color, float thickness, int segments) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.StrokeCircle;
            command.P1 = center;
            command.Radius = radius;
            command.Color = color;
            command.Thickness = thickness;
            command.Segments = segments;
            commands.Add(command);
        }

        public void Bezier(MuiVec2 p1, MuiVec2 p2, MuiVec2 p3, MuiVec2 p4, MuiColor color, float thickness, int segments) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.Bezier;
            command.P1 = p1;
            command.P2 = p2;
            command.P3 = p3;
            command.P4 = p4;
            command.Color = color;
            command.Thickness = thickness;
            command.Segments = segments;
            commands.Add(command);
        }

        public void Text(MuiRect rect, string text, object fontOrStyle, MuiColor color) {
            Text(rect, text, fontOrStyle, color, DefaultTextFormat);
        }

        public void Text(MuiRect rect, string text, object fontOrStyle, MuiColor color, TextFormat format) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.Text;
            command.Rect = rect;
            command.Text = text ?? string.Empty;
            command.Resource = fontOrStyle;
            command.Color = color;
            command.TextFormat = format;
            commands.Add(command);
        }

        public void Image(MuiRect rect, object texture, MuiColor color) {
            DrawCommand command = RentCommand();
            command.Type = DrawCommandType.Image;
            command.Rect = rect;
            command.Resource = texture;
            command.Color = color;
            commands.Add(command);
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

        private DrawCommand CloneWithOffset(DrawCommand source, MuiVec2 offset) {
            DrawCommand clone = RentCommand();
            clone.Type = source.Type;
            clone.Rect = Offset(source.Rect, offset);
            clone.Color = source.Color;
            clone.P1 = Offset(source.P1, offset);
            clone.P2 = Offset(source.P2, offset);
            clone.P3 = Offset(source.P3, offset);
            clone.P4 = Offset(source.P4, offset);
            clone.Text = source.Text;
            clone.Resource = source.Resource;
            clone.Thickness = source.Thickness;
            clone.Radius = source.Radius;
            clone.Segments = source.Segments;
            clone.TextFormat = source.TextFormat;

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
