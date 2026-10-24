namespace MaximumUI {
    public enum TwoPaneOrientation {
        Horizontal,
        Vertical
    }

    public sealed class VStack : Widget {
        public float Gap;

        public VStack(float gap = 0f, object key = null)
            : base(key) {
            Gap = gap;
        }
    }

    public sealed class HStack : Widget {
        public float Gap;

        public HStack(float gap = 0f, object key = null)
            : base(key) {
            Gap = gap;
        }
    }

    public sealed class Panel : Widget {
        public bool Selected;

        public Panel(object key = null)
            : base(key) {
        }
    }

    public sealed class TwoPaneLayout : Widget {
        public TwoPaneOrientation Orientation;
        public float Ratio;
        public float SplitterSize;
        public float MinFirst;
        public float MinSecond;
        public System.Action<float> OnChange;

        public TwoPaneLayout(float ratio, System.Action<float> onChange = null, object key = null)
            : base(key) {
            Orientation = TwoPaneOrientation.Horizontal;
            Ratio = ratio;
            SplitterSize = 6f;
            MinFirst = 40f;
            MinSecond = 40f;
            OnChange = onChange;
        }
    }

    public sealed class Grid : Widget {
        public int Columns;
        public float RowGap;
        public float ColumnGap;

        public Grid(int columns, float rowGap = 0f, float columnGap = 0f, object key = null)
            : base(key) {
            Columns = columns < 1 ? 1 : columns;
            RowGap = rowGap;
            ColumnGap = columnGap;
        }
    }

    public sealed class Table : Widget {
        public int Columns;
        public bool ShowHeader;
        public bool ShowOuterBorder;
        public bool ShowInnerBorders;
        public bool AlternatingRows;
        public float HeaderHeight;
        public float RowGap;
        public float ColumnGap;

        public Table(int columns, object key = null)
            : base(key) {
            Columns = columns < 1 ? 1 : columns;
            ShowHeader = true;
            ShowOuterBorder = true;
            ShowInnerBorders = true;
            AlternatingRows = false;
            HeaderHeight = 0f;
            RowGap = 1f;
            ColumnGap = 1f;
        }
    }

    public enum TableSortDirection {
        None,
        Ascending,
        Descending
    }

    public sealed class TableColumn : Widget {
        public string Title;
        public float Width;
        public float MinWidth;
        public float MaxWidth;
        public bool Stretch;
        public bool Visible;
        public bool Sortable;
        public TableSortDirection SortDirection;
        public System.Action<TableSortDirection> OnSort;

        public TableColumn(string title, object key = null)
            : base(key) {
            Title = title ?? string.Empty;
            Width = 0f;
            MinWidth = 0f;
            MaxWidth = 0f;
            Stretch = true;
            Visible = true;
            Sortable = false;
            SortDirection = TableSortDirection.None;
            OnSort = null;
        }
    }

    public class TableRow : Widget {
        public bool HasBackground;
        public MuiColor Background;

        public TableRow(object key = null)
            : base(key) {
            HasBackground = false;
            Background = new MuiColor(0f, 0f, 0f, 0f);
        }
    }

    public sealed class TableHeaderRow : TableRow {
        public TableHeaderRow(object key = null)
            : base(key) {
        }
    }

    public sealed class TableCell : Widget {
        public bool HasBackground;
        public MuiColor Background;
        public bool ShowBorder;

        public TableCell(object key = null)
            : base(key) {
            HasBackground = false;
            Background = new MuiColor(0f, 0f, 0f, 0f);
            ShowBorder = true;
        }
    }
}
