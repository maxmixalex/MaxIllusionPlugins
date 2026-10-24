using System;
using System.Reflection;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class PainterCompositionTests {
        internal const int TestCount = 2;

        internal static void RunAll() {
            StagedDrawingMatchesTheProductionTraversal();
            OwnPaintingDoesNotVisitChildrenOrLoseFinalOutlines();
        }

        private static void StagedDrawingMatchesTheProductionTraversal() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Widget root = new VStack(4f) {
                new Panel { Selected = true }.Content(new VStack(2f) {
                    new Label("Selected panel"), new Button("Action")
                }),
                new Panel { Selected = true }.Disabled(true).Content(new Panel { Selected = true }.Content(new Checkbox("Inherited", true))),
                new Table(2) { new TableRow { new Label("A"), new Button("B") },
                    new TableRow { new Label("C"), new Button("D") } }
            };
            LayoutNode layout = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(theme))
                .Layout(root, new MuiRect(0f, 0f, 420f, 480f));
            WidgetPainter painter = new WidgetPainter(theme);
            DrawList flat = painter.Paint(layout, null), composed = new DrawList();
            Compose(painter, layout, true, composed);
            Require(flat.Commands.Count == composed.Commands.Count, "staged traversal keeps every production command exactly once");
            FieldInfo[] fields = typeof(DrawCommand).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < flat.Commands.Count; i++) foreach (FieldInfo field in fields)
                Require(EqualValue(field.GetValue(flat.Commands[i]), field.GetValue(composed.Commands[i])),
                    "staged command order and attributes match at " + i + "." + field.Name);
        }

        private static void OwnPaintingDoesNotVisitChildrenOrLoseFinalOutlines() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Panel panel = new Panel { Selected = true };
            panel.Add(new Button("A child whose commands belong between both stages"));
            LayoutNode layout = new LayoutEngine().Layout(panel, new MuiRect(0f, 0f, 400f, 100f));
            WidgetPainter painter = new WidgetPainter(theme);
            DrawList before = new DrawList(), after = new DrawList();
            painter.PaintNode(layout, new WidgetPaintState(), before, after);
            foreach (DrawCommand command in before.Commands)
                Require(command.Type != DrawCommandType.Text, "own painting does not also paint descendant controls");
            Require(after.Commands.Count == 1 && after.Commands[0].Type == DrawCommandType.StrokeRect &&
                Equals(after.Commands[0].Color, theme.Accent), "the selected outline is explicitly an after-children command");
            DrawList disabledBefore = new DrawList(), disabledAfter = new DrawList();
            painter.PaintNode(layout, new WidgetPaintState { ParentEnabled = false }, disabledBefore, disabledAfter);
            Require(Equals(disabledAfter.Commands[0].Color, theme.Border), "staged overlays retain inherited disabled state");
            Require(layout.Children.Count == 1, "a render host never removes children to obtain self drawing");
        }

        private static void Compose(WidgetPainter painter, LayoutNode node, bool parentEnabled, DrawList result) {
            DrawList before = new DrawList(), after = new DrawList();
            painter.PaintNode(node, new WidgetPaintState { ParentEnabled = parentEnabled }, before, after);
            foreach (DrawCommand command in before.Commands) result.Commands.Add(command);
            foreach (LayoutNode child in node.Children) Compose(painter, child, parentEnabled && node.Widget.Enabled, result);
            foreach (DrawCommand command in after.Commands) result.Commands.Add(command);
        }

        private static bool EqualValue(object left, object right) {
            Array a = left as Array, b = right as Array;
            if (a == null || b == null) return Equals(left, right);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!Equals(a.GetValue(i), b.GetValue(i))) return false;
            return true;
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
