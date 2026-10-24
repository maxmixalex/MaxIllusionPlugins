using System;
using System.Collections.Generic;

namespace MaximumUI {
    internal interface IListBoxWidget {
        object ListValue { get; }
        System.Collections.IEnumerable ListItems { get; }
        Delegate ListOnChange { get; }
        int ListVisibleItems { get; }
        string GetListText(object value);
    }

    internal interface ISearchableListBoxWidget {
        string ListFilterText { get; }
        string ListFilterPlaceholder { get; }
        Action<string> ListOnFilterChange { get; }
    }

    internal interface ISegmentedControlWidget {
        object SegmentValue { get; }
        System.Collections.IEnumerable SegmentItems { get; }
        Delegate SegmentOnChange { get; }
        string GetSegmentText(object value);
    }

    public sealed class Foldout : Widget {
        public string Label;
        public bool Open;
        public Action<bool> OnChange;
        public bool Framed;

        public Foldout(string label, bool open, Action<bool> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Open = open;
            OnChange = onChange;
            Framed = true;
        }
    }

    public sealed class CollapsingHeader : Widget {
        public string Label;
        public bool Open;
        public bool Framed;
        public Action<bool> OnChange;

        public CollapsingHeader(string label, bool open, Action<bool> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Open = open;
            Framed = true;
            OnChange = onChange;
        }
    }

    public sealed class ClosableCollapsingHeader : Widget {
        public string Label;
        public bool Open;
        public bool Visible;
        public bool Framed;
        public Action<bool> OnChange;
        public Action OnClose;

        public ClosableCollapsingHeader(string label, bool open, Action<bool> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Open = open;
            Visible = true;
            Framed = true;
            OnChange = onChange;
            OnClose = null;
        }
    }

    public sealed class Tabs : Widget {
        public int SelectedIndex;
        public Action<int> OnChange;
        public Action<int> OnClose;
        public Action<int, int> OnReorder;
        public bool Reorderable;
        public bool CloseWithMiddleMouse;
        public bool NoTooltip;

        public Tabs(int selectedIndex, Action<int> onChange = null, object key = null)
            : base(key) {
            SelectedIndex = selectedIndex;
            OnChange = onChange;
            OnClose = null;
            OnReorder = null;
            Reorderable = false;
            CloseWithMiddleMouse = true;
            NoTooltip = false;
        }
    }

    public sealed class Tab : Widget {
        public string Title;
        public bool Closable;
        public bool Unsaved;
        public bool Visible;
        public bool NoTooltip;
        public Action OnClose;

        public Tab(string title, object key = null)
            : base(key) {
            Title = title ?? string.Empty;
            Closable = false;
            Unsaved = false;
            Visible = true;
            NoTooltip = false;
            OnClose = null;
        }
    }

    public enum TabButtonPlacement {
        Leading,
        Trailing
    }

    public class TabItemButton : Widget {
        public string Text;
        public Action OnClick;
        public TabButtonPlacement Placement;
        public float ButtonWidth;

        public TabItemButton(string text, Action onClick = null, TabButtonPlacement placement = TabButtonPlacement.Trailing, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            OnClick = onClick;
            Placement = placement;
            ButtonWidth = 28f;
        }
    }

    public sealed class LeadingTabButton : TabItemButton {
        public LeadingTabButton(string text, Action onClick = null, object key = null)
            : base(text, onClick, TabButtonPlacement.Leading, key) {
        }
    }

    public sealed class TrailingTabButton : TabItemButton {
        public TrailingTabButton(string text, Action onClick = null, object key = null)
            : base(text, onClick, TabButtonPlacement.Trailing, key) {
        }
    }

    public sealed class MenuBar : Widget {
        public bool Main;

        public MenuBar(object key = null)
            : base(key) {
            Main = false;
        }
    }

    public sealed class Menu : Widget {
        public string Title;

        public Menu(string title, object key = null)
            : base(key) {
            Title = title ?? string.Empty;
        }
    }

    public sealed class MenuItem : Widget {
        public string Text;
        public string Shortcut;
        public bool Checked;
        public Action OnClick;

        public MenuItem(string text, Action onClick = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Shortcut = string.Empty;
            Checked = false;
            OnClick = onClick;
        }
    }

    public sealed class MenuSeparator : Widget {
        public string Text;

        public MenuSeparator(string text = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
        }
    }

    public sealed class DragPayload {
        public string Type;
        public object Data;
        public string PreviewText;

        public DragPayload(string type, object data, string previewText = null) {
            Type = type ?? string.Empty;
            Data = data;
            PreviewText = previewText ?? string.Empty;
        }
    }

    public sealed class DragSource : Widget {
        public DragPayload Payload;
        public bool EnabledPreview;

        public DragSource(DragPayload payload, object key = null)
            : base(key) {
            Payload = payload;
            EnabledPreview = true;
        }
    }

    public sealed class DropTarget : Widget {
        public string AcceptedType;
        public Action<DragPayload> OnDrop;
        public string EmptyText;

        public DropTarget(string acceptedType, Action<DragPayload> onDrop = null, object key = null)
            : base(key) {
            AcceptedType = acceptedType ?? string.Empty;
            OnDrop = onDrop;
            EmptyText = string.Empty;
        }
    }

    public sealed class Selectable : Widget {
        public string Text;
        public object Value;
        public bool Selected;
        /// <summary>Draw a checkbox while retaining selection, range and double-click semantics.</summary>
        public bool ShowCheckbox;
        public Action<bool> OnChange;
        public Action<SelectionRequest> OnSelect;
        public Action OnDoubleClick;
        public bool AllowDoubleClick;

        public Selectable(string text, bool selected, Action<bool> onChange = null, object key = null)
            : base(key) {
            Text = text ?? string.Empty;
            Value = Text;
            Selected = selected;
            OnChange = onChange;
            OnSelect = null;
            OnDoubleClick = null;
            AllowDoubleClick = true;
        }
    }

    public sealed class ListBox<T> : Widget, IListBoxWidget {
        public T Value;
        public IEnumerable<T> Items;
        public IList<T> Options;
        public Func<T, string> Format;
        public Func<T, string> GetText;
        public Action<T> OnChange;
        public int VisibleItems;

        public ListBox(T value, IEnumerable<T> items, Func<T, string> format = null, Action<T> onChange = null, object key = null)
            : base(key) {
            Value = value;
            Options = new List<T>();
            if (items != null) {
                foreach (T item in items) Options.Add(item);
            }
            Items = Options;
            GetText = format ?? DefaultGetText;
            Format = GetText;
            OnChange = onChange;
            VisibleItems = 5;
        }

        private static string DefaultGetText(T value) {
            return value == null ? string.Empty : value.ToString();
        }

        object IListBoxWidget.ListValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IListBoxWidget.ListItems {
            get { return Items; }
        }

        Delegate IListBoxWidget.ListOnChange {
            get { return OnChange; }
        }

        int IListBoxWidget.ListVisibleItems {
            get { return VisibleItems; }
        }

        string IListBoxWidget.GetListText(object value) {
            string text;
            if (value is T) {
                text = GetText((T)value);
                return text ?? string.Empty;
            }

            if (value == null) {
                text = GetText(default(T));
                return text ?? string.Empty;
            }

            return value.ToString();
        }
    }

    public sealed class SearchableListBox<T> : Widget, IListBoxWidget, ISearchableListBoxWidget {
        public T Value;
        public IEnumerable<T> Items;
        public IList<T> Options;
        public Func<T, string> Format;
        public Func<T, string> GetText;
        public Action<T> OnChange;
        public int VisibleItems;
        public string FilterText;
        public string FilterPlaceholder;
        public Action<string> OnFilterChange;

        public SearchableListBox(T value, IEnumerable<T> items, Func<T, string> format = null, Action<T> onChange = null, object key = null)
            : base(key) {
            Value = value;
            Options = new List<T>();
            if (items != null) {
                foreach (T item in items) Options.Add(item);
            }
            Items = Options;
            GetText = format ?? DefaultGetText;
            Format = GetText;
            OnChange = onChange;
            VisibleItems = 5;
            FilterText = string.Empty;
            FilterPlaceholder = "Filter";
            OnFilterChange = null;
        }

        private static string DefaultGetText(T value) {
            return value == null ? string.Empty : value.ToString();
        }

        object IListBoxWidget.ListValue {
            get { return Value; }
        }

        System.Collections.IEnumerable IListBoxWidget.ListItems {
            get { return Items; }
        }

        Delegate IListBoxWidget.ListOnChange {
            get { return OnChange; }
        }

        int IListBoxWidget.ListVisibleItems {
            get { return VisibleItems; }
        }

        string IListBoxWidget.GetListText(object value) {
            string text;
            if (value is T) {
                text = GetText((T)value);
                return text ?? string.Empty;
            }

            if (value == null) {
                text = GetText(default(T));
                return text ?? string.Empty;
            }

            return value.ToString();
        }

        string ISearchableListBoxWidget.ListFilterText {
            get { return FilterText ?? string.Empty; }
        }

        string ISearchableListBoxWidget.ListFilterPlaceholder {
            get { return FilterPlaceholder ?? string.Empty; }
        }

        Action<string> ISearchableListBoxWidget.ListOnFilterChange {
            get { return OnFilterChange; }
        }
    }

    public sealed class SegmentedControl<T> : Widget, ISegmentedControlWidget {
        public T Value;
        public IEnumerable<T> Items;
        public IList<T> Options;
        public Func<T, string> Format;
        public Func<T, string> GetText;
        public Action<T> OnChange;

        public SegmentedControl(T value, IEnumerable<T> items, Func<T, string> format = null, Action<T> onChange = null, object key = null)
            : base(key) {
            Value = value;
            Options = new List<T>();
            if (items != null) {
                foreach (T item in items) Options.Add(item);
            }
            Items = Options;
            GetText = format ?? DefaultGetText;
            Format = GetText;
            OnChange = onChange;
        }

        private static string DefaultGetText(T value) {
            return value == null ? string.Empty : value.ToString();
        }

        object ISegmentedControlWidget.SegmentValue {
            get { return Value; }
        }

        System.Collections.IEnumerable ISegmentedControlWidget.SegmentItems {
            get { return Items; }
        }

        Delegate ISegmentedControlWidget.SegmentOnChange {
            get { return OnChange; }
        }

        string ISegmentedControlWidget.GetSegmentText(object value) {
            string text;
            if (value is T) {
                text = GetText((T)value);
                return text ?? string.Empty;
            }

            if (value == null) {
                text = GetText(default(T));
                return text ?? string.Empty;
            }

            return value.ToString();
        }
    }

    public sealed class TreeNode : Widget {
        public string Label;
        public bool Open;
        public bool Leaf;
        public bool Selected;
        public bool Bullet;
        public bool Framed;
        public bool OpenOnArrow;
        public bool OpenOnDoubleClick;
        public bool DrawLines;
        public Action<bool> OnOpenChange;
        public Action OnSelect;

        public TreeNode(string label, bool open, Action<bool> onOpenChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Open = open;
            OnOpenChange = onOpenChange;
            Leaf = false;
            Selected = false;
            Bullet = false;
            Framed = false;
            OpenOnArrow = false;
            OpenOnDoubleClick = false;
            DrawLines = false;
        }
    }

    public sealed class TreeLeaf : Widget {
        public string Label;
        public bool Selected;
        public bool Bullet;
        public bool Framed;
        public Action OnSelect;

        public TreeLeaf(string label, Action onSelect = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Selected = false;
            Bullet = false;
            Framed = false;
            OnSelect = onSelect;
        }
    }

    public enum ColorDisplayMode {
        Float,
        Byte,
        Hex
    }

    public sealed class ColorButton : Widget {
        public string Label;
        public MuiColor Value;
        public bool ShowAlpha;
        public bool ShowCheckerboard;
        public Action OnClick;

        public ColorButton(string label, MuiColor value, Action onClick = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            ShowAlpha = true;
            ShowCheckerboard = true;
            OnClick = onClick;
        }
    }

    public sealed class ColorEdit3 : Widget {
        public string Label;
        public MuiColor Value;
        public ColorDisplayMode DisplayMode;
        public bool ShowPreview;
        public Action<MuiColor> OnChange;

        public ColorEdit3(string label, MuiColor value, Action<MuiColor> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Value.A = 1f;
            DisplayMode = ColorDisplayMode.Float;
            ShowPreview = true;
            OnChange = onChange;
        }
    }

    public sealed class ColorEdit4 : Widget {
        public string Label;
        public MuiColor Value;
        public ColorDisplayMode DisplayMode;
        public bool ShowPreview;
        public Action<MuiColor> OnChange;

        public ColorEdit4(string label, MuiColor value, Action<MuiColor> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            DisplayMode = ColorDisplayMode.Float;
            ShowPreview = true;
            OnChange = onChange;
        }
    }

    public sealed class ColorPicker3 : Widget {
        public string Label;
        public MuiColor Value;
        public ColorDisplayMode DisplayMode;
        public bool ShowPreview;
        public Action<MuiColor> OnChange;

        public ColorPicker3(string label, MuiColor value, Action<MuiColor> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            Value.A = 1f;
            DisplayMode = ColorDisplayMode.Float;
            ShowPreview = true;
            OnChange = onChange;
        }
    }

    public sealed class ColorPicker4 : Widget {
        public string Label;
        public MuiColor Value;
        public ColorDisplayMode DisplayMode;
        public bool ShowPreview;
        public Action<MuiColor> OnChange;

        public ColorPicker4(string label, MuiColor value, Action<MuiColor> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            Value = value;
            DisplayMode = ColorDisplayMode.Float;
            ShowPreview = true;
            OnChange = onChange;
        }
    }

    public sealed class ColorEdit : Widget {
        public MuiColor Value;
        public bool ShowAlpha;
        public ColorDisplayMode DisplayMode;
        public Action<MuiColor> OnChange;

        public ColorEdit(MuiColor value, Action<MuiColor> onChange = null, object key = null)
            : base(key) {
            Value = value;
            ShowAlpha = true;
            DisplayMode = ColorDisplayMode.Float;
            OnChange = onChange;
        }
    }

    public sealed class VectorField : Widget {
        public string Label;
        public float X;
        public float Y;
        public float Z;
        public int Components;
        public Action<float, float, float> OnChange;

        public VectorField(string label, float x, float y, float z, Action<float, float, float> onChange = null, object key = null)
            : base(key) {
            Label = label ?? string.Empty;
            X = x;
            Y = y;
            Z = z;
            Components = 3;
            OnChange = onChange;
        }
    }

    public sealed class PlotLines : Widget {
        public IList<float> Values;
        public string OverlayText;
        public float Min;
        public float Max;
        public bool HasRange;

        public PlotLines(IEnumerable<float> values, object key = null)
            : base(key) {
            Values = new List<float>();
            if (values != null) {
                foreach (float value in values) Values.Add(value);
            }
            OverlayText = string.Empty;
            Min = 0f;
            Max = 1f;
            HasRange = false;
        }
    }

    public sealed class PlotHistogram : Widget {
        public IList<float> Values;
        public string OverlayText;
        public float Min;
        public float Max;
        public bool HasRange;

        public PlotHistogram(IEnumerable<float> values, object key = null)
            : base(key) {
            Values = new List<float>();
            if (values != null) {
                foreach (float value in values) Values.Add(value);
            }
            OverlayText = string.Empty;
            Min = 0f;
            Max = 1f;
            HasRange = false;
        }
    }

    public sealed class VirtualList : Widget {
        // Runtime mounts retain visible rows until their view is invalidated.
        // Disable this for row factories that intentionally read live external data.
        public bool CacheItems = true;
        public int ItemCount;
        public float RowHeight;
        public float Height;
        public Func<int, Widget> BuildItem;
        public bool AlwaysShowVertical;
        public ScrollPosition ScrollPosition;

        public VirtualList(int itemCount, float rowHeight, Func<int, Widget> buildItem, object key = null)
            : base(key) {
            ItemCount = itemCount < 0 ? 0 : itemCount;
            RowHeight = rowHeight <= 0f ? 20f : rowHeight;
            Height = RowHeight * 6f;
            BuildItem = buildItem;
            AlwaysShowVertical = false;
        }
    }
}
