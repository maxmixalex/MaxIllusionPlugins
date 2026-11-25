using System;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class ScrollMeasurementTests {
        internal const int TestCount = 5;

        internal static void RunAll() {
            NestedOverflowHasBoundedMeasurementWork();
            NaturalHeightOverflowKeepsItsCompleteExtent();
            ForcedHorizontalBarsPreserveNaturalMeasurement();
            InvalidatedLayoutsUseCurrentTextMetricsAndStructure();
            MovingCachedScrollHostsDoesNotMeasureAgain();
            Console.WriteLine("MaximumUI nested scroll measurement regressions: " + TestCount + " passed.");
        }

        private static void NestedOverflowHasBoundedMeasurementWork() {
            foreach (int depth in new[] { 1, 2, 4, 6, 8 }) {
                int measurements = 0;
                LayoutMetrics metrics = new LayoutMetrics {
                    RowHeight = 32f,
                    MeasureText = delegate { measurements++; return 50f; }
                };
                LayoutNode root = new LayoutEngine(metrics).Layout(Nested(depth), new MuiRect(0f, 0f, 500f, 100f));
                int labels = depth + 4;
                Require(measurements <= labels * (depth + 1),
                    "Nested overflow must not double subtree measurement at every level: depth " + depth +
                    ", " + labels + " declared labels, " + measurements + " measurements.");
                CheckNestedLayout(root, depth, 500f);
            }
        }

        private static void NaturalHeightOverflowKeepsItsCompleteExtent() {
            ScrollView scroll = new ScrollView();
            scroll.Add(new Label(new string('A', 75)) { WordWrap = true });
            LayoutEngine engine = new LayoutEngine(new LayoutMetrics { RowHeight = 32f, TextWidth = 8f });
            LayoutNode node = engine.Layout(scroll, new MuiRect(0f, 0f, 300f, 60f));
            Require(node.DesiredSize.Y == 60f && node.Children[0].Rect.Width == 284f,
                "Natural-height overflow still measures a bounded viewport and reserves its actual scrollbar.");
            Require(node.Children[0].Rect.Height == 96f && ScrollGeometry.ContentExtent(node, node.Rect).Height == 96f,
                "Deferring narrow reflow must retain all wrapped lines beyond the viewport.");
        }

        private static void ForcedHorizontalBarsPreserveNaturalMeasurement() {
            ScrollView scroll = new ScrollView { AlwaysShowHorizontal = true };
            scroll.Add(new Label(new string('A', 36)) { WordWrap = true });
            LayoutEngine engine = new LayoutEngine(new LayoutMetrics { RowHeight = 32f, TextWidth = 8f });
            LayoutNode node = engine.Measure(scroll, new LayoutConstraints(0f, 300f, 0f, 40f), "horizontal");
            Require(node.DesiredSize.X == 284f && node.DesiredSize.Y == 40f && node.Children[0].DesiredSize.Y == 64f,
                "A forced horizontal track may still change an otherwise natural height through wrapping.");
            engine.Arrange(node, new MuiRect(0f, 0f, 300f, 40f));
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect viewport = ScrollGeometry.ContentViewport(node.Rect, extent, true, false, ScrollbarGeometry.GutterSize);
            Require(viewport.Width == 284f && viewport.Height == 24f && extent.Height == 64f,
                "Both scrollbar gutters and the final text extent must remain consistent with native clipping.");
        }

        private static void InvalidatedLayoutsUseCurrentTextMetricsAndStructure() {
            LayoutMetrics metrics = new LayoutMetrics { RowHeight = 32f, TextWidth = 8f };
            LayoutCache cache = new LayoutCache(new LayoutEngine(metrics));
            ScrollView scroll = new ScrollView { Height = 50f };
            Label label = new Label("short") { WordWrap = true };
            scroll.Add(label);
            MuiRect bounds = new MuiRect(0f, 0f, 300f, 50f);
            LayoutNode before = cache.Layout(scroll, bounds, "changing");
            Require(before.Children[0].Rect.Height == 32f && before.Children[0].Rect.Width == 300f,
                "The initial fitting label uses the complete viewport.");

            metrics.RowHeight = 40f;
            metrics.TextWidth = 12f;
            label.Text = new string('A', 100);
            Label added = new Label("extra");
            scroll.Add(added);
            cache.Invalidate();
            LayoutNode changed = cache.Layout(scroll, bounds, "changing");
            Require(changed.Children.Count == 2 && changed.Children[0].Rect.Width == 284f &&
                changed.Children[0].Rect.Height == 200f && ScrollGeometry.ContentExtent(changed, changed.Rect).Height == 240f,
                "Invalidation must remeasure current font metrics, changed text and newly added children.");

            scroll.Children.Remove(added);
            label.Text = "short";
            metrics.RowHeight = 32f;
            metrics.TextWidth = 8f;
            cache.Invalidate();
            LayoutNode shorter = cache.Layout(scroll, bounds, "changing");
            Require(shorter.Children.Count == 1 && shorter.Children[0].Rect.Width == 300f && shorter.Children[0].Rect.Height == 32f,
                "Removing content must also release any previously required gutter.");
        }

        private static void MovingCachedScrollHostsDoesNotMeasureAgain() {
            int measurements = 0;
            LayoutMetrics metrics = new LayoutMetrics {
                RowHeight = 32f,
                MeasureText = delegate { measurements++; return 50f; }
            };
            LayoutCache cache = new LayoutCache(new LayoutEngine(metrics));
            ScrollView scroll = Nested(8);
            cache.Layout(scroll, new MuiRect(0f, 0f, 500f, 100f), "moves");
            int initial = measurements;
            LayoutNode moved = cache.Layout(scroll, new MuiRect(25f, 31f, 500f, 100f), "moves");
            Require(measurements == initial && cache.ArrangePassCount == 1,
                "A cached window move must arrange existing scroll nodes without remeasuring their content.");
            Require(moved.Children[0].Rect.X == 25f && moved.Children[0].Rect.Y == 31f,
                "Reusing measurement still updates the content's actual position.");
            CheckNestedLayout(moved, 8, 500f);
        }

        private static ScrollView Nested(int depth) {
            ScrollView scroll = new ScrollView { Height = 100f };
            if (depth > 1) scroll.Add(Nested(depth - 1));
            else for (int i = 0; i < 4; i++) scroll.Add(new Label("row"));
            scroll.Add(new Label("footer"));
            return scroll;
        }

        private static void CheckNestedLayout(LayoutNode node, int depth, float width) {
            Require(node.Rect.Width == width && node.Rect.Height == 100f, "Every nested viewport keeps its allocated size.");
            float contentWidth = width - ScrollbarGeometry.GutterSize;
            float contentHeight = depth > 1 ? 132f : 160f;
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect viewport = ScrollGeometry.ContentViewport(node.Rect, extent, false, false, ScrollbarGeometry.GutterSize);
            Require(extent.Width == contentWidth && extent.Height == contentHeight && viewport.Width == contentWidth && viewport.Height == 100f,
                "Each nested host must preserve its complete content and exactly one vertical gutter.");
            LayoutNode footer = node.Children[node.Children.Count - 1];
            Require(footer.Rect.Width == contentWidth && footer.Rect.Height == 32f && footer.Rect.Bottom - node.Rect.Y == contentHeight,
                "The final row stays reachable at the expected bottom of every scroll extent.");
            if (depth > 1) CheckNestedLayout(node.Children[0], depth - 1, contentWidth);
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
