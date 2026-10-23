using System;
using System.Collections.Generic;
using AccessoryBatchManager;
using JuiceControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class InlineDialogLayoutTests {
        internal const int TestCount = 8;

        internal static void RunAll() {
            AccessoryConfirmationKeepsBackgroundLayout();
            JuiceNameDialogKeepsBackgroundLayout();
            NestedContainersExcludeDialogsFromTheirFlow();
            IndexedContainersKeepTheirPaneCellAndColumnAssignments();
            SelectedTabsKeepModalDiscoveryAndOriginalPaths();
            DialogsMeasureTheirOwnContentAndKeepInputNodes();
            DialogsDoNotEnlargeScrollContent();
            OverlayNodesCannotCreateSplittersOrExtendTreeLines();
        }

        private static void AccessoryConfirmationKeepsBackgroundLayout() {
            AccessoryEditorView view = new AccessoryEditorView();
            view.Model.Available = true;
            view.Model.Batch.Count = view.Model.Source.Count = view.Model.Destination.Count = 20;
            view.Model.Sources = view.Model.Destinations = new[] { "Anonymous coordinate" };
            view.Model.Confirmation = "Apply the selected operation?";
            for (int page = 0; page < 2; page++) {
                view.Model.Page = page;
                for (int minimum = 0; minimum < 2; minimum++) {
                    view.Model.Confirming = false;
                    Window closed = (Window)view.Build(null);
                    MuiRect viewport = WindowViewport(closed, minimum != 0);
                    LayoutNode before = Engine().Layout(closed, viewport);
                    view.Model.Confirming = true;
                    Window opened = (Window)view.Build(null);
                    LayoutNode after = Engine().Layout(opened, viewport);
                    SameFlow(before, after, "Accessory page " + page + " confirmation");
                    string list = page == 0 ? "accessory-list-Batch" : "accessory-list-Source";
                    Require(Find(after, list).Rect.Height > 0f, "opening confirmation preserves the visible accessory list");
                    Require(Find(after, "accessory-confirm").Path == "0.1", "accessory modal retains its declared child path");
                    Require(WidgetVisibility.ContainsRootDialog(opened, null, "0"), "accessory confirmation remains discoverable");
                }
            }
        }

        private static void JuiceNameDialogKeepsBackgroundLayout() {
            JuiceEditorView view = new JuiceEditorView();
            view.Model.HasCharacter = view.Model.CanSave = true;
            view.Model.Characters = new[] { "Anonymous character" };
            view.Model.CharacterPresets = view.Model.GroupPresets = new[] { "Anonymous preset" };
            view.Model.Name = "Example";
            view.Model.NamePrompt = "Save preset";
            view.Model.Effects.Add(new JuiceEffectRow { Id = "example", Name = "Effect", Maximum = 3 });
            for (int page = 0; page < 2; page++) {
                ((Selectable)FindWidget(view.Build(null), "juice-page-" + page)).OnChange(true);
                for (int minimum = 0; minimum < 2; minimum++) {
                    view.Model.NameDialogVisible = false;
                    Window closed = (Window)view.Build(null);
                    MuiRect viewport = WindowViewport(closed, minimum != 0);
                    LayoutNode before = Engine().Layout(closed, viewport);
                    view.Model.NameDialogVisible = true;
                    Window opened = (Window)view.Build(null);
                    LayoutNode after = Engine().Layout(opened, viewport);
                    SameFlow(before, after, "Juice page " + page + " name dialog");
                    string list = page == 0 ? "juice-effects" : "juice-presets-scroll";
                    Require(Find(after, list).Rect.Height > 0f, "opening the name dialog keeps Juice content usable");
                    Require(Find(after, "juice-name-dialog").Path == "0.1", "Juice name modal retains its declared child path");
                    Require(WidgetVisibility.ContainsRootDialog(opened, null, "0"), "Juice name modal remains discoverable");
                }
            }
        }

        private static void NestedContainersExcludeDialogsFromTheirFlow() {
            Func<Widget>[] containers = {
                delegate { return new VStack(7f); }, delegate { return new HStack(7f); },
                delegate { return new Panel(); }, delegate { return new ScrollView(); },
                delegate { return new Foldout("Open", true); },
                delegate { return new CollapsingHeader("Open", true); },
                delegate { return new ClosableCollapsingHeader("Open", true); },
                delegate { return new TreeNode("Open", true); }, delegate { return new Tab("Page"); },
                delegate { return new TableCell(); }, delegate { return new DragSource(null); },
                delegate { return new DropTarget("example"); }
            };
            for (int type = 0; type < containers.Length; type++) {
                for (int position = 0; position < 3; position++) {
                    Widget before = containers[type]();
                    Widget after = containers[type]();
                    before.Add(new Button("First", key: "first").Width(90f).Height(30f));
                    before.Add(new Label("Second", "second").Width(100f).Height(42f));
                    after.Add(new Button("First", key: "first").Width(90f).Height(30f));
                    after.Add(new Label("Second", "second").Width(100f).Height(42f));
                    after.Children.Insert(position, Modal("nested"));
                    MuiRect viewport = new MuiRect(20f, 30f, 600f, 400f);
                    LayoutNode expected = Engine().Layout(before, viewport);
                    LayoutNode actual = Engine().Layout(after, viewport);
                    SameFlow(expected, actual, before.GetType().Name + " dialog at " + position);
                    Require(Find(actual, "nested").Path == "0." + position, "overlay path uses original declaration index");
                    string secondPath = position < 2 ? "0.2" : "0.1";
                    Require(Find(actual, "second").Path == secondPath, "reordered flow nodes keep their original declared paths");
                }
            }
        }

        private static void IndexedContainersKeepTheirPaneCellAndColumnAssignments() {
            for (int orientation = 0; orientation < 2; orientation++) {
                TwoPaneLayout before = new TwoPaneLayout(0.4f) { Orientation = (TwoPaneOrientation)orientation };
                TwoPaneLayout after = new TwoPaneLayout(0.4f) { Orientation = (TwoPaneOrientation)orientation };
                before.Add(new Label("First", "first")); before.Add(new Label("Second", "second"));
                after.Add(new Label("First", "first")); after.Add(Modal("pane-modal")); after.Add(new Label("Second", "second"));
                SameFlow(Engine().Layout(before, Viewport()), Engine().Layout(after, Viewport()), "two-pane " + orientation);
            }

            Grid gridBefore = new Grid(2, 7f, 9f), gridAfter = new Grid(2, 7f, 9f);
            for (int i = 0; i < 3; i++) {
                gridBefore.Add(new Label("Cell " + i, "cell-" + i).Height(30f + i * 5f));
                gridAfter.Add(new Label("Cell " + i, "cell-" + i).Height(30f + i * 5f));
            }
            gridAfter.Children.Insert(1, Modal("grid-modal"));
            SameFlow(Engine().Layout(gridBefore, Viewport()), Engine().Layout(gridAfter, Viewport()), "grid cells");

            for (int defined = 0; defined < 2; defined++) {
                Table before = TableFixture(false, defined != 0), after = TableFixture(true, defined != 0);
                LayoutNode expected = Engine().Layout(before, Viewport());
                LayoutNode actual = Engine().Layout(after, Viewport());
                SameFlow(expected, actual, "table rows and columns " + defined);
                Require(Find(actual, "row-modal").Rect.Width == 320f, "table-row overlays retain their own arranged width");
                Require(Find(actual, "table-modal").Rect.Height == 180f, "table overlays retain their own arranged height");
            }
        }

        private static void SelectedTabsKeepModalDiscoveryAndOriginalPaths() {
            Tabs tabs = new Tabs(0);
            tabs.Add(new Tab("Hidden") { Visible = false });
            tabs.Add(new Tab("First") { new Label("First", "first") });
            tabs.Add(new Tab("Second") { new Label("Second", "second"), Modal("tab-modal") });
            LayoutNode first = Engine().Layout(tabs, Viewport());
            Require(Find(first, "tab-modal") == null && !WidgetVisibility.ContainsRootDialog(tabs, null, "0"),
                "inactive tab modals do not enter layout or block input");
            tabs.SelectedIndex = 1;
            LayoutNode second = Engine().Layout(tabs, Viewport());
            Require(Find(second, "tab-modal").Path == "0.2.1", "selected tab modal preserves both original tab and child indices");
            Require(WidgetVisibility.ContainsRootDialog(tabs, null, "0"), "selected tab modal is still discovered");
            tabs.Children[2].Children.RemoveAt(1);
            SameFlow(Engine().Layout(tabs, Viewport()), second, "selected tab modal");
        }

        private static void DialogsMeasureTheirOwnContentAndKeepInputNodes() {
            float wrappedWidth = 0f;
            LayoutMetrics metrics = Metrics();
            metrics.MeasureLabelHeight = delegate(Label label, float width) { wrappedWidth = width; return 30f; };
            Dialog dialog = Modal("own-size");
            VStack parent = new VStack { new Label("Small pane").Height(20f), dialog };
            LayoutNode layout = new LayoutEngine(metrics).Layout(parent, new MuiRect(0f, 0f, 80f, 40f));
            LayoutNode overlay = Find(layout, "own-size"), action = Find(overlay, "dialog-action");
            Equal(280f, wrappedWidth, "dialog wrapping uses its own width minus its own padding");
            Equal(320f, overlay.DesiredSize.X, "a small containing pane cannot clamp modal width");
            Equal(180f, overlay.Rect.Height, "a small containing pane cannot clamp modal height");
            Equal(28f, action.Rect.Height, "dialog actions keep their complete measured height");
            Require(overlay.Find(action.Path) == action, "reordered modal nodes remain findable by their original path");
            MuiVec2 point = new MuiVec2(action.Rect.X + 2f, action.Rect.Y + 2f);
            InputDispatcher dispatcher = new InputDispatcher();
            dispatcher.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseDown, MouseButton = 0, MousePosition = point }, overlay);
            InputDispatchResult click = dispatcher.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseUp, MouseButton = 0, MousePosition = point }, overlay);
            Require(click.ClickedWidget == action.Widget && click.ClickedPath == "0.1.1",
                "standalone modal input scanning keeps action identity after parent flow reordering");
        }

        private static void DialogsDoNotEnlargeScrollContent() {
            ScrollView before = new ScrollView { Height = 100f }, after = new ScrollView { Height = 100f };
            before.Add(new Label("Content", "content").Height(160f));
            after.Add(new Label("Content", "content").Height(160f));
            Dialog dialog = Modal("scroll-modal"); dialog.Width = 700f; dialog.Height = 500f;
            after.Add(dialog);
            MuiRect viewport = new MuiRect(10f, 15f, 200f, 100f);
            LayoutNode expected = Engine().Layout(before, viewport), actual = Engine().Layout(after, viewport);
            SameFlow(expected, actual, "scroll content");
            RectEqual(ScrollGeometry.ContentExtent(expected, viewport), ScrollGeometry.ContentExtent(actual, viewport),
                "an overlay cannot create horizontal or vertical scrolling range");
            Equal(160f, ScrollGeometry.ContentExtent(actual, viewport).Height, "ordinary overflowing content still contributes its full scroll range");
            Equal(700f, Find(actual, "scroll-modal").Rect.Width, "unbounded scroll measurement preserves the modal's independent dimensions");
        }

        private static void OverlayNodesCannotCreateSplittersOrExtendTreeLines() {
            ThemeTokens theme = ThemePalettes.MetroDark();
            WidgetPainter painter = new WidgetPainter(theme);
            for (int ordinary = 0; ordinary < 3; ordinary++) {
                TwoPaneLayout panes = new TwoPaneLayout(0.4f);
                for (int i = 0; i < ordinary; i++) panes.Add(new Label("Pane " + i));
                panes.Children.Insert(0, Modal("first-modal"));
                panes.Add(Modal("second-modal"));
                DrawList painted = painter.Paint(Engine().Layout(panes, Viewport()), null);
                int splitters = 0;
                foreach (DrawCommand command in painted.Commands)
                    if (command.Type == DrawCommandType.FillRect && object.Equals(command.Color, theme.Separator)) splitters++;
                Require(splitters == (ordinary == 2 ? 1 : 0), "only two ordinary panes produce a splitter, regardless of overlay count");
            }

            TreeNode before = new TreeNode("Tree", true) { DrawLines = true, Children = { new Label("Child") } };
            TreeNode after = new TreeNode("Tree", true) { DrawLines = true, Children = { new Label("Child"), Modal("tree-modal") } };
            DrawCommand expectedLine = OnlyLine(painter.Paint(Engine().Layout(before, Viewport()), null));
            DrawCommand actualLine = OnlyLine(painter.Paint(Engine().Layout(after, Viewport()), null));
            Require(expectedLine != null && actualLine != null, "the tree still connects its ordinary child");
            Equal(expectedLine.P1.X, actualLine.P1.X, "tree line start x");
            Equal(expectedLine.P1.Y, actualLine.P1.Y, "tree line start y");
            Equal(expectedLine.P2.X, actualLine.P2.X, "tree line end x");
            Equal(expectedLine.P2.Y, actualLine.P2.Y, "modal geometry must not extend the tree connector");
            after.Children.RemoveAt(0);
            Require(OnlyLine(painter.Paint(Engine().Layout(after, Viewport()), null)) == null,
                "an overlay alone is not a tree branch");
        }

        private static DrawCommand OnlyLine(DrawList list) {
            DrawCommand found = null;
            foreach (DrawCommand command in list.Commands) {
                if (command.Type != DrawCommandType.Line) continue;
                Require(found == null, "the fixture emits at most one tree line");
                found = command;
            }
            return found;
        }

        private static Table TableFixture(bool overlays, bool definedColumns) {
            Table table = new Table(1);
            if (definedColumns) {
                table.Add(new TableColumn("First")); table.Add(new TableColumn("Second"));
            }
            TableRow first = new TableRow("first-row") {
                new Label("First cell", "cell-a"), new Label("Second cell", "cell-b")
            };
            if (overlays) first.Children.Insert(1, Modal("row-modal"));
            table.Add(first);
            if (overlays) table.Add(Modal("table-modal"));
            table.Add(new TableRow("second-row") { new Label("Other", "cell-c"), new Label("Other", "cell-d") });
            return table;
        }

        private static Dialog Modal(string key) {
            return new Dialog("Example modal", key) {
                Width = 320f, Height = 180f,
                Children = { new Label("Modal content") { WordWrap = true }, new Button("Continue", key: "dialog-action").Height(28f) }
            };
        }

        private static LayoutMetrics Metrics() {
            return new LayoutMetrics {
                RowHeight = 26f, SmallRowHeight = 17f, TextWidth = 7.8f,
                WindowPaddingLeft = 16f, WindowPaddingTop = 44f, WindowPaddingRight = 16f, WindowPaddingBottom = 14f,
                DialogPaddingLeft = 20f, DialogPaddingTop = 48f, DialogPaddingRight = 20f, DialogPaddingBottom = 18f,
                PanelPadding = 12f
            };
        }

        private static LayoutEngine Engine() { return new LayoutEngine(Metrics()); }
        private static MuiRect Viewport() { return new MuiRect(20f, 30f, 600f, 400f); }
        private static MuiRect WindowViewport(Window window, bool minimum) {
            return new MuiRect(30f, 40f, minimum ? window.MinWidth : window.Width, minimum ? window.MinHeight : window.Height);
        }

        private static void SameFlow(LayoutNode expected, LayoutNode actual, string message) {
            Require(expected.Widget.GetType() == actual.Widget.GetType() && object.Equals(expected.Widget.Key, actual.Widget.Key),
                message + ": normal-flow widget identity/order changed");
            RectEqual(expected.Rect, actual.Rect, message + ": " + expected.Widget.GetType().Name + " rectangle");
            Equal(expected.DesiredSize.X, actual.DesiredSize.X, message + ": normal-flow desired width");
            Equal(expected.DesiredSize.Y, actual.DesiredSize.Y, message + ": normal-flow desired height");
            List<LayoutNode> before = Flow(expected), after = Flow(actual);
            Require(before.Count == after.Count, message + ": normal-flow child count changed");
            for (int i = 0; i < before.Count; i++) SameFlow(before[i], after[i], message);
        }

        private static List<LayoutNode> Flow(LayoutNode node) {
            List<LayoutNode> children = new List<LayoutNode>();
            foreach (LayoutNode child in node.Children) if (!(child.Widget is Dialog)) children.Add(child);
            return children;
        }

        private static LayoutNode Find(LayoutNode node, string key) {
            if (object.Equals(node.Widget.Key, key)) return node;
            foreach (LayoutNode child in node.Children) { LayoutNode found = Find(child, key); if (found != null) return found; }
            return null;
        }

        private static Widget FindWidget(Widget widget, string key) {
            if (object.Equals(widget.Key, key)) return widget;
            foreach (Widget child in widget.Children) { Widget found = FindWidget(child, key); if (found != null) return found; }
            return null;
        }

        private static void RectEqual(MuiRect expected, MuiRect actual, string message) {
            Equal(expected.X, actual.X, message + " x"); Equal(expected.Y, actual.Y, message + " y");
            Equal(expected.Width, actual.Width, message + " width"); Equal(expected.Height, actual.Height, message + " height");
        }

        private static void Equal(float expected, float actual, string message) {
            Require(Math.Abs(expected - actual) < 0.01f, message + ": expected " + expected + ", actual " + actual);
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
