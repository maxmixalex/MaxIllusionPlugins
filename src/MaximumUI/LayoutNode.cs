using System.Collections.Generic;

namespace MaximumUI {
    public sealed class LayoutConstraints {
        public float MinWidth;
        public float MaxWidth;
        public float MinHeight;
        public float MaxHeight;

        public LayoutConstraints(float minWidth, float maxWidth, float minHeight, float maxHeight) {
            MinWidth = minWidth;
            MaxWidth = maxWidth;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
        }
    }

    public sealed class LayoutMetrics {
        public float RowHeight = 22f;
        public float SmallRowHeight = 16f;
        public float TitleHeight = 24f;
        public float WindowPaddingLeft = 10f;
        public float WindowPaddingTop = 26f;
        public float WindowPaddingRight = 10f;
        public float WindowPaddingBottom = 10f;
        // Negative values preserve the previous window-padding fallback for callers
        // that configure only the original LayoutMetrics fields.
        public float DialogPaddingLeft = -1f;
        public float DialogPaddingTop = -1f;
        public float DialogPaddingRight = -1f;
        public float DialogPaddingBottom = -1f;
        internal float EffectiveDialogPaddingLeft { get { return DialogPaddingLeft >= 0f ? DialogPaddingLeft : WindowPaddingLeft; } }
        internal float EffectiveDialogPaddingTop { get { return DialogPaddingTop >= 0f ? DialogPaddingTop : WindowPaddingTop; } }
        internal float EffectiveDialogPaddingRight { get { return DialogPaddingRight >= 0f ? DialogPaddingRight : WindowPaddingRight; } }
        internal float EffectiveDialogPaddingBottom { get { return DialogPaddingBottom >= 0f ? DialogPaddingBottom : WindowPaddingBottom; } }
        public float PanelPadding = 4f;
        public float FoldoutIndent = 12f;
        public float TextWidth = 7f;
        public System.Func<string, float> MeasureText;
        public System.Func<string, float, float> MeasureWrappedTextHeight;
        public System.Func<Label, float> MeasureLabelWidth;
        public System.Func<Label, float, float> MeasureLabelHeight;
    }

    public sealed class LayoutNode {
        private readonly List<LayoutNode> children = new List<LayoutNode>();

        public Widget Widget;
        public string Path;
        public MuiRect Rect;
        public MuiVec2 DesiredSize;
        internal float ScrollMeasureWidth;
        internal float ScrollWideMeasureWidth;
        internal float ScrollWideContentHeight;

        public IList<LayoutNode> Children {
            get { return children; }
        }

        public LayoutNode Find(string path) {
            if (string.Equals(Path, path, System.StringComparison.Ordinal)) return this;
            for (int i = 0; i < children.Count; i++) {
                LayoutNode found = children[i].Find(path);
                if (found != null) return found;
            }

            return null;
        }
    }
}
