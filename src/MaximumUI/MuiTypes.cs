namespace MaximumUI {
    public struct MuiVec2 {
        public float X;
        public float Y;

        public MuiVec2(float x, float y) {
            X = x;
            Y = y;
        }
    }

    public struct MuiRect {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public MuiRect(float x, float y, float width, float height) {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right {
            get { return X + Width; }
        }

        public float Bottom {
            get { return Y + Height; }
        }

        public bool Contains(MuiVec2 point) {
            return point.X >= X && point.X < Right &&
                   point.Y >= Y && point.Y < Bottom;
        }
    }

    public struct MuiColor {
        public float R;
        public float G;
        public float B;
        public float A;

        public MuiColor(float r, float g, float b, float a) {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static MuiColor FromRgb(float r, float g, float b) {
            return new MuiColor(r, g, b, 1f);
        }
    }

    public enum MuiEventType {
        None,
        MouseMove,
        MouseDown,
        MouseUp,
        MouseDrag,
        ScrollWheel,
        KeyDown,
        KeyUp,
        TextInput,
        Repaint
    }

    public sealed class MuiInputEvent {
        public MuiEventType Type;
        public MuiVec2 MousePosition;
        public int MouseButton;
        public int KeyCode;
        public char Character;
        public float ScrollDeltaY;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool Command;
    }
}
