using System;
using System.Collections.Generic;

namespace MaximumUI {
    internal sealed class VirtualListCache {
        private sealed class Row {
            public Widget Widget;
            public LayoutNode Layout;
            public MuiRect Viewport;
            public string Path;
        }

        private readonly Dictionary<int, Row> rows = new Dictionary<int, Row>();
        private readonly List<int> expired = new List<int>();
        private VirtualList owner;
        private Func<int, Widget> builder;
        private LayoutEngine engine;
        private int revision;
        private int itemCount;

        public int Count { get { return rows.Count; } }
        public long BuildCount { get; private set; }
        public long LayoutCount { get; private set; }

        public void BeginRange(VirtualList widget, int first, int end, int nextRevision, LayoutEngine nextEngine) {
            if (!ReferenceEquals(owner, widget) || !ReferenceEquals(builder, widget.BuildItem) ||
                revision != nextRevision || itemCount != widget.ItemCount || !ReferenceEquals(engine, nextEngine)) {
                rows.Clear();
                owner = widget;
                builder = widget.BuildItem;
                revision = nextRevision;
                itemCount = widget.ItemCount;
                engine = nextEngine;
            }
            expired.Clear();
            foreach (KeyValuePair<int, Row> pair in rows) {
                if (pair.Key < first || pair.Key >= end) expired.Add(pair.Key);
            }
            for (int i = 0; i < expired.Count; i++) rows.Remove(expired[i]);
        }

        public LayoutNode GetRow(int index, MuiRect viewport, string path) {
            Row row;
            if (!rows.TryGetValue(index, out row)) {
                row = new Row { Widget = builder == null ? null : builder(index) };
                rows.Add(index, row);
                BuildCount++;
            }
            if (row.Widget == null || row.Widget is EmptyWidget) return null;
            if (row.Layout == null || row.Viewport.Width != viewport.Width || row.Viewport.Height != viewport.Height ||
                !string.Equals(row.Path, path, StringComparison.Ordinal)) {
                row.Layout = engine.Layout(row.Widget, viewport, path);
                row.Viewport = viewport;
                row.Path = path;
                LayoutCount++;
            } else if (row.Viewport.X != viewport.X || row.Viewport.Y != viewport.Y) {
                engine.Arrange(row.Layout, viewport);
                row.Viewport = viewport;
            }
            return row.Layout;
        }
    }
}
