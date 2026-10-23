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
        public float PanelPadding = 4f;
        public float FoldoutIndent = 12f;
        public float TextWidth = 7f;
    }

    public sealed class LayoutNode {
        private readonly List<LayoutNode> children = new List<LayoutNode>();

        public Widget Widget;
        public string Path;
        public MuiRect Rect;
        public MuiVec2 DesiredSize;

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
