using System.Collections.Generic;

namespace MaximumUI {
    internal interface IDebugLinesWidget {
        string DebugTitle { get; }
        int VisibleRows { get; }
        IList<string> GetDebugLines();
    }

    public sealed class MetricsWindow : Widget, IDebugLinesWidget {
        public RuntimeMetrics Metrics;
        public int Rows;

        public MetricsWindow(RuntimeMetrics metrics = null, object key = null)
            : base(key) {
            Metrics = metrics ?? new RuntimeMetrics();
            Rows = 7;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Metrics"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            RuntimeMetrics metrics = Metrics ?? new RuntimeMetrics();
            lines.Add("Frame: " + metrics.FrameCount.ToString());
            lines.Add("Mounts: " + metrics.VisibleMountCount.ToString() + " visible / " + metrics.MountCount.ToString() + " total");
            lines.Add("Callbacks: " + metrics.PendingCallbackCount.ToString());
            lines.Add("Mouse over UI: " + metrics.IsMouseOverAnyWindow.ToString());
            lines.Add("Text focused: " + metrics.IsAnyTextFieldFocused.ToString());
            lines.Add("Modal open: " + metrics.IsModalOpen.ToString());
            return lines;
        }
    }

    public sealed class DebugLogWindow : Widget, IDebugLinesWidget {
        public IList<DebugLogEntry> Entries;
        public int Rows;
        public bool ShowTimestamp;

        public DebugLogWindow(IEnumerable<DebugLogEntry> entries = null, object key = null)
            : base(key) {
            Entries = new List<DebugLogEntry>();
            if (entries != null) {
                foreach (DebugLogEntry entry in entries) Entries.Add(entry);
            }
            Rows = 8;
            ShowTimestamp = true;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Debug Log"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Entries == null || Entries.Count == 0) {
                lines.Add("No log entries.");
                return lines;
            }

            for (int i = 0; i < Entries.Count; i++) {
                DebugLogEntry entry = Entries[i];
                if (entry == null) continue;

                string text = "[" + entry.Level.ToString() + "] " + entry.Message;
                if (ShowTimestamp) text = entry.Time.ToString("HH:mm:ss") + " " + text;
                lines.Add(text);
            }

            return lines;
        }
    }

    public sealed class StateStoreInspector : Widget, IDebugLinesWidget {
        public UiStateStore StateStore;
        public int Rows;

        public StateStoreInspector(UiStateStore stateStore, object key = null)
            : base(key) {
            StateStore = stateStore;
            Rows = 8;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "State Store"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (StateStore == null) {
                lines.Add("No StateStore.");
                return lines;
            }

            IList<UiStateEntry> entries = StateStore.Snapshot();
            lines.Add("Entries: " + entries.Count.ToString());
            for (int i = 0; i < entries.Count; i++) {
                UiStateEntry entry = entries[i];
                lines.Add(entry.Id + " : " + entry.TypeName);
            }

            return lines;
        }
    }

    public sealed class LayoutDebugger : Widget, IDebugLinesWidget {
        public LayoutNode Root;
        public int Rows;

        public LayoutDebugger(LayoutNode root, object key = null)
            : base(key) {
            Root = root;
            Rows = 10;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Layout"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            AppendNode(lines, Root, 0);
            if (lines.Count == 0) lines.Add("No layout node.");
            return lines;
        }

        private static void AppendNode(IList<string> lines, LayoutNode node, int depth) {
            if (node == null) return;

            string indent = new string(' ', depth * 2);
            string typeName = node.Widget == null ? "null" : node.Widget.GetType().Name;
            lines.Add(indent + node.Path + " " + typeName + " " +
                node.Rect.X.ToString("0") + "," +
                node.Rect.Y.ToString("0") + " " +
                node.Rect.Width.ToString("0") + "x" +
                node.Rect.Height.ToString("0"));

            for (int i = 0; i < node.Children.Count; i++) {
                AppendNode(lines, node.Children[i], depth + 1);
            }
        }
    }

    public sealed class DrawListDebugger : Widget, IDebugLinesWidget {
        public DrawList DrawList;
        public int Rows;

        public DrawListDebugger(DrawList drawList, object key = null)
            : base(key) {
            DrawList = drawList;
            Rows = 10;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "DrawList"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (DrawList == null) {
                lines.Add("No DrawList.");
                return lines;
            }

            lines.Add("Commands: " + DrawList.Commands.Count.ToString());
            for (int i = 0; i < DrawList.Commands.Count; i++) {
                DrawCommand command = DrawList.Commands[i];
                lines.Add(i.ToString() + ": " + command.Type.ToString() + " " +
                    command.Rect.X.ToString("0") + "," +
                    command.Rect.Y.ToString("0") + " " +
                    command.Rect.Width.ToString("0") + "x" +
                    command.Rect.Height.ToString("0"));
            }

            return lines;
        }
    }

    public sealed class InputDebugger : Widget, IDebugLinesWidget {
        public UiQueryStore Query;
        public UiNavigationStore Navigation;
        public UiShortcutRouter Shortcuts;
        public int Rows;

        public InputDebugger(UiQueryStore query, object key = null)
            : base(key) {
            Query = query;
            Rows = 10;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Input"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Query == null) {
                lines.Add("No query store.");
                return lines;
            }

            InputCaptureState capture = Query.Capture;
            lines.Add("Mouse: " + capture.MousePosition.X.ToString("0") + ", " + capture.MousePosition.Y.ToString("0"));
            lines.Add("Capture mouse: " + capture.WantCaptureMouse.ToString());
            lines.Add("Capture keyboard: " + capture.WantCaptureKeyboard.ToString());
            lines.Add("Text input: " + capture.WantTextInput.ToString());
            if (Navigation != null) {
                NavigationItem focused = Navigation.FocusedItem();
                lines.Add("Navigation: " + Navigation.Count.ToString());
                lines.Add("Focused: " + (focused == null ? "none" : focused.Id));
            }
            lines.Add("Key: " + capture.KeyCode.ToString());
            lines.Add("Modifiers: " +
                (capture.Control ? "Ctrl " : string.Empty) +
                (capture.Shift ? "Shift " : string.Empty) +
                (capture.Alt ? "Alt " : string.Empty) +
                (capture.Command ? "Cmd" : string.Empty));
            if (Shortcuts != null) {
                lines.Add("Shortcuts: " + Shortcuts.Count.ToString());
                if (!string.IsNullOrEmpty(Shortcuts.LastTriggeredId)) {
                    lines.Add("Last shortcut: " + Shortcuts.LastTriggeredId);
                }
            }

            IList<ItemQuery> items = Query.SnapshotItems();
            for (int i = 0; i < items.Count && lines.Count < Rows; i++) {
                ItemQuery item = items[i];
                if (!item.Hovered && !item.Active && !item.Focused && !item.Clicked) continue;

                string flags = string.Empty;
                if (item.Hovered) flags += "H";
                if (item.Active) flags += "A";
                if (item.Focused) flags += "F";
                if (item.Clicked) flags += "C";
                lines.Add(flags + " " + item.Id + " " + item.TypeName);
            }

            return lines;
        }
    }

    public sealed class IDStackTool : Widget, IDebugLinesWidget {
        public UiQueryStore Query;
        public int Rows;
        public bool IncludeWindows;

        public IDStackTool(UiQueryStore query, object key = null)
            : base(key) {
            Query = query;
            Rows = 10;
            IncludeWindows = true;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "ID Stack"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Query == null) {
                lines.Add("No query store.");
                return lines;
            }

            IList<ItemQuery> items = Query.SnapshotItems();
            lines.Add("Items: " + items.Count.ToString());
            for (int i = 0; i < items.Count; i++) {
                ItemQuery item = items[i];
                lines.Add(FormatItem(item));
            }

            if (IncludeWindows) {
                IList<WindowQuery> windows = Query.SnapshotWindows();
                lines.Add("Windows: " + windows.Count.ToString());
                for (int i = 0; i < windows.Count; i++) {
                    WindowQuery window = windows[i];
                    lines.Add("window " + window.Id + " title=" + window.Title);
                }
            }

            return lines;
        }

        private static string FormatItem(ItemQuery item) {
            if (item == null) return "null";

            string key = string.IsNullOrEmpty(item.Key) ? string.Empty : " key=" + item.Key;
            return item.Id + key + " path=" + item.Path + " type=" + item.TypeName;
        }
    }

    public sealed class ItemPicker : Widget, IDebugLinesWidget {
        public UiQueryStore Query;
        public int Rows;
        public bool ShowAllItems;

        public ItemPicker(UiQueryStore query, object key = null)
            : base(key) {
            Query = query;
            Rows = 10;
            ShowAllItems = false;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Item Picker"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Query == null) {
                lines.Add("No query store.");
                return lines;
            }

            IList<ItemQuery> items = Query.SnapshotItems();
            for (int i = 0; i < items.Count; i++) {
                ItemQuery item = items[i];
                if (item == null) continue;
                if (!ShowAllItems && !item.Hovered && !item.Active && !item.Focused && !item.Clicked) continue;

                lines.Add(FormatFlags(item) + " " + item.Id + " " + item.TypeName + " " + FormatRect(item.Rect));
            }

            if (lines.Count == 0) lines.Add("No hovered, active, focused or clicked item.");
            return lines;
        }

        private static string FormatFlags(ItemQuery item) {
            string flags = string.Empty;
            if (item.Hovered) flags += "H";
            if (item.Active) flags += "A";
            if (item.Focused) flags += "F";
            if (item.Clicked) flags += "C";
            return flags.Length == 0 ? "-" : flags;
        }

        private static string FormatRect(MuiRect rect) {
            return rect.X.ToString("0") + "," + rect.Y.ToString("0") + " " +
                rect.Width.ToString("0") + "x" + rect.Height.ToString("0");
        }
    }

    public sealed class AboutWindow : Widget, IDebugLinesWidget {
        public string ProductName;
        public string Version;
        public string Description;
        public IList<string> Lines;
        public int Rows;

        public AboutWindow(object key = null)
            : this("MaximumUI", "0.1.0", "Declarative UI toolkit for Koikatsu plugins.", key) {
        }

        public AboutWindow(string productName, string version, string description, object key = null)
            : base(key) {
            ProductName = productName ?? "MaximumUI";
            Version = version ?? string.Empty;
            Description = description ?? string.Empty;
            Lines = new List<string>();
            Rows = 6;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "About"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            lines.Add(ProductName);
            if (!string.IsNullOrEmpty(Version)) lines.Add("Version: " + Version);
            if (!string.IsNullOrEmpty(Description)) lines.Add(Description);
            if (Lines != null) {
                for (int i = 0; i < Lines.Count; i++) {
                    if (!string.IsNullOrEmpty(Lines[i])) lines.Add(Lines[i]);
                }
            }
            return lines;
        }
    }

    public sealed class UserGuideWindow : Widget, IDebugLinesWidget {
        public IList<string> Topics;
        public int Rows;

        public UserGuideWindow(object key = null)
            : base(key) {
            Topics = new List<string>();
            Rows = 8;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "User Guide"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Topics != null && Topics.Count > 0) {
                for (int i = 0; i < Topics.Count; i++) {
                    if (!string.IsNullOrEmpty(Topics[i])) lines.Add(Topics[i]);
                }
                return lines;
            }

            lines.Add("The configured mount shortcut toggles a plugin window when enabled.");
            lines.Add("Use stable keys for TextField, ComboBox, Window and dynamic list rows.");
            lines.Add("Build reads previous-frame query state; callbacks update controlled state.");
            lines.Add("Tab and Shift+Tab move keyboard focus through focusable controls.");
            lines.Add("Right-click items with ContextMenuContent to open a context menu.");
            return lines;
        }
    }

    public sealed class DemoGalleryEntry {
        public string Category;
        public string Title;
        public string Status;

        public DemoGalleryEntry() {
            Category = string.Empty;
            Title = string.Empty;
            Status = string.Empty;
        }

        public DemoGalleryEntry(string category, string title, string status) {
            Category = category ?? string.Empty;
            Title = title ?? string.Empty;
            Status = status ?? string.Empty;
        }
    }

    public sealed class DemoGallery : Widget, IDebugLinesWidget {
        public IList<DemoGalleryEntry> Entries;
        public int Rows;

        public DemoGallery(object key = null)
            : base(key) {
            Entries = new List<DemoGalleryEntry>();
            Rows = 10;
        }

        string IDebugLinesWidget.DebugTitle {
            get { return "Demo Gallery"; }
        }

        int IDebugLinesWidget.VisibleRows {
            get { return Rows; }
        }

        IList<string> IDebugLinesWidget.GetDebugLines() {
            List<string> lines = new List<string>();
            if (Entries == null || Entries.Count == 0) {
                lines.Add("No demo entries.");
                return lines;
            }

            string currentCategory = null;
            for (int i = 0; i < Entries.Count; i++) {
                DemoGalleryEntry entry = Entries[i];
                if (entry == null) continue;

                if (!string.Equals(currentCategory, entry.Category)) {
                    currentCategory = entry.Category;
                    if (!string.IsNullOrEmpty(currentCategory)) lines.Add("[" + currentCategory + "]");
                }

                string status = string.IsNullOrEmpty(entry.Status) ? string.Empty : " - " + entry.Status;
                lines.Add(entry.Title + status);
            }

            return lines;
        }
    }
}
