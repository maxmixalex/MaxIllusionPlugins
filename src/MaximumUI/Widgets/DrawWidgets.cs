using System;

namespace MaximumUI {
    public sealed class DrawCanvas : Widget {
        public float CanvasWidth;
        public float CanvasHeight;
        public bool Clip;
        public bool DrawBackground;
        public Action<DrawList, MuiRect> OnDraw;

        public DrawCanvas(Action<DrawList, MuiRect> onDraw = null, object key = null)
            : base(key) {
            CanvasWidth = 180f;
            CanvasHeight = 100f;
            Clip = true;
            DrawBackground = true;
            OnDraw = onDraw;
        }
    }

    public sealed class DrawListWidget : Widget {
        public DrawList Source;
        public float CanvasWidth;
        public float CanvasHeight;
        public bool Clip;
        public bool DrawBackground;
        public bool LocalCoordinates;

        public DrawListWidget(DrawList source = null, object key = null)
            : base(key) {
            Source = source;
            CanvasWidth = 180f;
            CanvasHeight = 100f;
            Clip = true;
            DrawBackground = true;
            LocalCoordinates = true;
        }
    }
}
