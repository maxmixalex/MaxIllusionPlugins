using System;
using System.Collections.Generic;

namespace MaximumUI {
    /// <summary>
    /// Caches measured layouts for a retained widget tree. Invalidate after changing
    /// widget data or layout metrics. Runtime mounts do this whenever View.Build runs.
    /// </summary>
    public sealed class LayoutCache {
        private sealed class Entry {
            public Widget Widget;
            public LayoutNode Node;
            public MuiRect Viewport;
        }

        private readonly LayoutEngine engine;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private const int MaximumEntries = 256;

        public long LayoutPassCount { get; private set; }
        public long CacheHitCount { get; private set; }
        public long ArrangePassCount { get; private set; }
        public int Count { get { return entries.Count; } }

        public LayoutCache() : this(new LayoutEngine()) { }

        public LayoutCache(LayoutEngine engine) {
            if (engine == null) throw new ArgumentNullException("engine");
            this.engine = engine;
        }

        public void Invalidate() {
            entries.Clear();
        }

        public LayoutNode Layout(Widget widget, MuiRect viewport, string rootPath) {
            string path = rootPath ?? string.Empty;
            Entry entry;
            if (entries.TryGetValue(path, out entry) && ReferenceEquals(widget, entry.Widget) &&
                entry.Viewport.Width == viewport.Width && entry.Viewport.Height == viewport.Height) {
                if (entry.Viewport.X != viewport.X || entry.Viewport.Y != viewport.Y) {
                    engine.Arrange(entry.Node, viewport);
                    entry.Viewport = viewport;
                    ArrangePassCount++;
                }
                CacheHitCount++;
                return entry.Node;
            }

            LayoutNode node = engine.Layout(widget, viewport, path);
            LayoutPassCount++;
            // Dynamic menus/virtual rows must not retain every path ever encountered.
            if (entry == null) {
                if (entries.Count >= MaximumEntries) entries.Clear();
                entry = new Entry();
                entries[path] = entry;
            }
            entry.Widget = widget;
            entry.Node = node;
            entry.Viewport = viewport;
            return node;
        }
    }
}
