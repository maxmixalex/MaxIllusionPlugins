using System;
using System.Collections.Generic;
using MaximumUI;
using UnityEngine;

namespace MaximumUI.Tests {
    internal static class ScrollChromeTests {
        internal const int TestCount = 22;
        private static readonly ThemeTokens Theme = ThemePalettes.MetroLight();
        private static readonly LayoutMetrics Metrics = ThemeTypography.CreateLayoutMetrics(Theme);

        public static void RunAll() {
            FramesPreserveEveryBorderEdge();
            FittingListsDoNotLoseContentWidth();
            ExactVisibleRowsFitWithoutAThumb();
            FinalRowsRemainFullyReachable();
            ScrollThumbMatchesNativeMinimumPlusRatio();
            TinyTracksKeepThumbsBounded();
            ForcedEmptyTracksRemainVisibleAndInactive();
            ScrollStatesRemainDistinctInBothThemes();
            ListChromeIsOutsideTheContentClip();
            ScrolledListPaintsItsFinalRows();
            SearchFiltersStayOutsideTheScrollingBody();
            DebugPanelsReachTheLastLogLine();
            ThemePanelsReachTheirFinalSetting();
            CheckboxSelectablesRetainWholeRowHitTargets();
            CheckboxSelectablesReserveTheirMarkWidth();
            HelpMessagesMeasureTheirWrappedHeight();
            HelpMessagesPaintWrappingAndSeverity();
            FittingScrollViewsUseTheirEntireWidth();
            ScrollViewsReflowWhenTheAllocatedSizeChanges();
            VirtualListsSupportKeyboardScrollBoundaries();
            UnassignedPaintPathsAreNotHoveredOrFocused();
            ScrollFrameQueriesMatchThePaintedClip();
            Console.WriteLine("MaximumUI scroll/chrome maturity regressions: " + TestCount + " passed.");
        }

        private static void FramesPreserveEveryBorderEdge() {
            MuiRect bounds = new MuiRect(17f, 29f, 320f, 162f);
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(bounds, 10000f);
            Require(frame.Viewport.X == 18f && frame.Viewport.Y == 30f, "A list host begins inside its left and top borders.");
            Require(frame.VerticalGutter.Right == bounds.Right - 1f && frame.VerticalGutter.Bottom == bounds.Bottom - 1f,
                "The native track must never cover a list's right or bottom border.");
            Require(frame.ContentViewport.Right == frame.VerticalGutter.X && Contains(frame.Viewport, frame.ContentViewport),
                "The content clip and track partition only the inner viewport.");
            ScrollbarLayout chrome = ScrollbarGeometry.Vertical(frame.VerticalGutter, frame.Content.Height, frame.ContentViewport.Height, float.MaxValue);
            Require(Contains(frame.Viewport, chrome.Track) && Contains(chrome.Track, chrome.Thumb), "Track and thumb stay inside the frame at the last page.");
        }

        private static void FittingListsDoNotLoseContentWidth() {
            ScrollFrameLayout shortList = ScrollGeometry.VerticalFrame(new MuiRect(0f, 0f, 250f, 130f), 64f);
            Require(!shortList.Vertical && shortList.Content.Width == 248f && shortList.VerticalGutter.Width == 0f,
                "A list that fits must not reserve a blank 16px strip.");
            ScrollFrameLayout longList = ScrollGeometry.VerticalFrame(shortList.Bounds, 1000f);
            Require(longList.Content.Width == shortList.Content.Width - ScrollbarGeometry.GutterSize, "Only an overflowing list reserves the scrollbar.");
        }

        private static void ExactVisibleRowsFitWithoutAThumb() {
            ListBox<int> list = new ListBox<int>(0, new[] { 0, 1, 2, 3, 4 }) { VisibleItems = 5 };
            LayoutNode measured = new LayoutEngine(Metrics).Measure(list, new LayoutConstraints(0f, 300f, 0f, 1000f), "list");
            ScrollFrameLayout frame = ScrollGeometry.ListFrame(new MuiRect(0f, 0f, 300f, measured.DesiredSize.Y), Metrics.RowHeight, 5, false);
            Require(!frame.Vertical && frame.ContentViewport.Height == Metrics.RowHeight * 5f,
                "VisibleItems is a number of complete rows; the frame must be measured in addition to those rows.");
        }

        private static void FinalRowsRemainFullyReachable() {
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(new MuiRect(0f, 0f, 200f, 120f), 25f * 100f);
            float final = frame.ClampY(float.MaxValue);
            Require(Near(2500f - final, frame.ContentViewport.Height), "Maximum scroll reveals the last row down to its bottom edge.");
            float keyboard = KeyboardNavigation.RevealRow(0f, 99, 25f, frame.ContentViewport.Height, 100);
            Require(Near(keyboard, final), "Keyboard reveal and the actual inner scroll viewport agree.");
            Require(frame.ClampY(float.NaN) == 0f && frame.ClampY(-10f) == 0f, "Invalid offsets cannot poison the frame.");
        }

        private static void ScrollThumbMatchesNativeMinimumPlusRatio() {
            ScrollbarLayout value = ScrollbarGeometry.Vertical(new MuiRect(0f, 0f, 16f, 200f), 1000f, 200f, 0f);
            Require(Near(value.Thumb.Height, 58.4f), "The 24px native minimum is added to the proportional remaining track.");
            Require(value.Thumb.Width == 8f && value.Thumb.X == 4f, "The 8px thumb is centered in the entire 16px target.");
            ScrollbarLayout huge = ScrollbarGeometry.Vertical(new MuiRect(0f, 0f, 16f, 200f), 1000000f, 200f, 0f);
            Require(huge.Thumb.Height >= 24f, "Large lists still have a usable drag handle.");
        }

        private static void TinyTracksKeepThumbsBounded() {
            foreach (float length in new[] { 0f, .25f, 1f, 2f, 4f, 8f, 20f, 28f, 32f }) {
                MuiRect gutter = new MuiRect(10f, 20f, 16f, length);
                ScrollbarLayout value = ScrollbarGeometry.Vertical(gutter, 10000f, 1f, 9999f);
                Require(value.Thumb.Width >= 0f && value.Thumb.Height >= 0f && Contains(value.Track, value.Thumb),
                    "Minimum thumb size must shrink to the available track in a tiny viewport.");
                if (length > 0f) {
                    float track = length - 2f * ScrollbarGeometry.NativeEndInset(length);
                    float minimum = ScrollbarGeometry.NativeMinimumThumb(track);
                    float valuesPerPixel = (track - minimum) / 10000f;
                    Require(valuesPerPixel > 0f && !float.IsInfinity(1f / valuesPerPixel) && 0f / valuesPerPixel == 0f,
                        "The native SliderHandler drag divisor must stay positive even in a sub-pixel track.");
                }
            }
            Require(!ScrollGeometry.HasNativeScrollArea(new MuiRect(0f, 0f, 16f, 0f)) &&
                !ScrollGeometry.HasNativeScrollArea(new MuiRect(0f, 0f, 0f, 100f)),
                "Collapsed hosts must bypass native scrollbar handlers rather than divide by a zero-length track.");
            MuiVec2 clean = ScrollGeometry.ClampPosition(new MuiVec2(float.NaN, float.PositiveInfinity), new MuiRect(0f, 0f, 100f, 1000f), new MuiRect(0f, 0f, 100f, 100f));
            Require(clean.X == 0f && clean.Y == 0f, "Non-finite offsets must not reach the native scroll clip or retained UI state.");
        }

        private static void ForcedEmptyTracksRemainVisibleAndInactive() {
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(new MuiRect(0f, 0f, 200f, 120f), 0f, true);
            ScrollbarLayout chrome = ScrollbarGeometry.Vertical(frame.VerticalGutter, frame.Content.Height, frame.ContentViewport.Height, 0f, frame.Vertical);
            Require(chrome.Visible && !chrome.Scrollable, "AlwaysShowVertical retains a track but does not invent a scroll range.");
            DrawList drawing = new DrawList();
            PopupPresentation.DrawScrollbar(drawing, Theme, chrome, true, true);
            Require(Same(drawing.Commands[1].Color, Theme.TextDisabled), "An inactive forced track cannot look pressed or active.");
        }

        private static void ScrollStatesRemainDistinctInBothThemes() {
            foreach (ThemeTokens theme in new[] { ThemePalettes.MetroLight(), ThemePalettes.MetroDark() }) {
                MuiColor normal = ScrollbarGeometry.ThumbColor(theme, false, false);
                MuiColor hover = ScrollbarGeometry.ThumbColor(theme, true, false);
                MuiColor pressed = ScrollbarGeometry.ThumbColor(theme, false, true);
                Require(!Same(normal, hover) && !Same(hover, pressed), "Hover and drag feedback must remain visible in both themes.");
                Require(Contrast(normal, ScrollbarGeometry.TrackColor(theme)) >= 3d, "Normal scrollbar boundaries require at least 3:1 contrast.");
            }
        }

        private static void ListChromeIsOutsideTheContentClip() {
            LayoutNode node = Node(new ListBox<int>(2, Range(30)), 250f, 162f);
            DrawList drawing = Painter().Paint(node, new WidgetPaintState());
            int depth = 0, frameIndex = -1, lastClipIndex = -1;
            for (int i = 0; i < drawing.Commands.Count; i++) {
                DrawCommand command = drawing.Commands[i];
                if (command.Type == DrawCommandType.PushClip) depth++;
                if (command.Type == DrawCommandType.PopClip) { depth--; lastClipIndex = i; }
                if (command.Type == DrawCommandType.StrokeRect && RectEqual(command.Rect, node.Rect)) {
                    Require(depth == 0, "The complete outline must be painted outside the content clip.");
                    frameIndex = i;
                }
            }
            Require(depth == 0 && frameIndex > lastClipIndex, "The outer border is restored after clipped rows and scrollbar chrome.");
        }

        private static void ScrolledListPaintsItsFinalRows() {
            LayoutNode node = Node(new ListBox<int>(99, Range(100), i => "Entry " + i), 250f, 130f);
            WidgetPaintState state = new WidgetPaintState(); state.ScrollPositions[node.Path] = new MuiVec2(0f, float.MaxValue);
            DrawList drawing = Painter().Paint(node, state);
            Require(HasText(drawing, "Entry 99") && !HasText(drawing, "Entry 0"), "A bottom-page snapshot must execute production scrolling instead of always painting the first options.");
            int texts = 0; foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Text) texts++;
            Require(texts <= 8, "List drawing must stay bounded to visible rows plus overscan.");
        }

        private static void SearchFiltersStayOutsideTheScrollingBody() {
            SearchableListBox<int> list = new SearchableListBox<int>(99, Range(100), i => "Entry " + i) { FilterText = "Entry", FilterPlaceholder = "Filter" };
            LayoutNode node = Node(list, 300f, 166f);
            WidgetPaintState state = new WidgetPaintState(); state.ScrollPositions[node.Path] = new MuiVec2(0f, float.MaxValue);
            DrawList drawing = Painter().Paint(node, state);
            DrawCommand filter = FindText(drawing, "Entry");
            Require(filter != null && filter.Rect.Y == node.Rect.Y, "The filter remains fixed while option rows scroll.");
            Require(HasText(drawing, "Entry 99"), "Filtering does not make the last match unreachable.");
            int depth = 0;
            foreach (DrawCommand command in drawing.Commands) {
                if (command.Type == DrawCommandType.PushClip) depth++;
                if (command.Type == DrawCommandType.PopClip) depth--;
                if (ReferenceEquals(command, filter)) Require(depth == 0, "The fixed filter cannot inherit the list's scroll clip.");
            }
        }

        private static void DebugPanelsReachTheLastLogLine() {
            List<DebugLogEntry> entries = new List<DebugLogEntry>();
            for (int i = 0; i < 100; i++) entries.Add(new DebugLogEntry { Message = "Log " + i });
            LayoutNode node = Node(new DebugLogWindow(entries) { Rows = 3, ShowTimestamp = false }, 320f, 132f);
            WidgetPaintState state = new WidgetPaintState(); state.ScrollPositions[node.Path] = new MuiVec2(0f, float.MaxValue);
            DrawList drawing = Painter().Paint(node, state);
            Require(HasText(drawing, "[Info] Log 99") && !HasText(drawing, "[Info] Log 0"), "Debug panel pagination must not truncate the data to VisibleRows.");
        }

        private static void ThemePanelsReachTheirFinalSetting() {
            ThemeEditor editor = new ThemeEditor(Theme) { MaxRows = 3 };
            LayoutNode node = Node(editor, 480f, 134f);
            WidgetPaintState state = new WidgetPaintState(); state.ScrollPositions[node.Path] = new MuiVec2(0f, float.MaxValue);
            DrawList drawing = Painter().Paint(node, state);
            Require(HasText(drawing, ThemeEditorTokens.ScalarName(ThemeScalarToken.SliderThumbHeight)) && !HasText(drawing, "WindowBg"),
                "Every theme setting, including the last scalar, must be available in a scrolled panel.");
        }

        private static void CheckboxSelectablesRetainWholeRowHitTargets() {
            Selectable selectable = new Selectable("Original action text", true) { ShowCheckbox = true };
            LayoutNode node = Node(selectable, 300f, 32f);
            DrawList drawing = Painter().Paint(node, new WidgetPaintState());
            int checkSegments = 0; foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.Line) checkSegments++;
            Require(checkSegments == 2 && HasText(drawing, "Original action text"), "Checkbox rows use real check geometry without altering their label.");
            InputDispatcher dispatcher = new InputDispatcher();
            dispatcher.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseDown, MouseButton = 0, MousePosition = new MuiVec2(290f, 15f) }, node);
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent { Type = MuiEventType.MouseUp, MouseButton = 0, MousePosition = new MuiVec2(290f, 15f) }, node);
            Require(ReferenceEquals(result.ClickedWidget, selectable), "Adding a checkbox must preserve the entire selectable row's existing input owner.");
        }

        private static void CheckboxSelectablesReserveTheirMarkWidth() {
            LayoutEngine engine = new LayoutEngine(Metrics);
            LayoutConstraints limits = new LayoutConstraints(0f, 2000f, 0f, 200f);
            LayoutNode plain = engine.Measure(new Selectable("One", false), limits, "a");
            LayoutNode check = engine.Measure(new Selectable("One", false) { ShowCheckbox = true }, limits, "b");
            Require(check.DesiredSize.X >= plain.DesiredSize.X + 16f, "Checkbox decoration contributes to automatic content measurement.");
        }

        private static void HelpMessagesMeasureTheirWrappedHeight() {
            LayoutMetrics metrics = ThemeTypography.CreateLayoutMetrics(Theme);
            float measuredWidth = 0f;
            metrics.MeasureWrappedTextHeight = delegate(string text, float width) { measuredWidth = width; return 80f; };
            HelpBox help = new HelpBox("A long failure message which needs several lines", HelpBoxType.Error);
            LayoutNode node = new LayoutEngine(metrics).Measure(help, new LayoutConstraints(0f, 220f, 0f, 1000f), "help");
            Require(measuredWidth == 196f && node.DesiredSize.Y >= 98f, "Help layout measures wrapped text within the actual 12px horizontal and 9px vertical insets.");
        }

        private static void HelpMessagesPaintWrappingAndSeverity() {
            foreach (HelpBoxType type in new[] { HelpBoxType.Warning, HelpBoxType.Error }) {
                LayoutNode node = Node(new HelpBox("Long warning with multiple words", type), 200f, 100f);
                DrawList drawing = Painter().Paint(node, new WidgetPaintState());
                DrawCommand text = FindText(drawing, "Long warning with multiple words");
                Require(text.TextFormat.WordWrap && text.TextFormat.VerticalAlignment == TextVerticalAlignment.Top && text.Rect.X == 12f && text.Rect.Y == 9f,
                    "Managed help text must wrap using the same insets and alignment as the native renderer.");
                Require(Same(drawing.Commands[0].Color, type == HelpBoxType.Error ? Theme.ErrorBg : Theme.WarningBg), "Severity is visible in the production help surface.");
            }
        }

        private static void FittingScrollViewsUseTheirEntireWidth() {
            ScrollView scroll = new ScrollView(); scroll.Add(new Label("Short"));
            LayoutNode node = new LayoutEngine(Metrics).Layout(scroll, new MuiRect(0f, 0f, 300f, 100f));
            Require(node.Children[0].Rect.Width == 300f, "Ordinary scroll hosts must not reserve an empty scrollbar gutter when content fits.");
            MuiRect content = ScrollGeometry.ContentExtent(node, node.Rect);
            MuiRect viewport = ScrollGeometry.ContentViewport(node.Rect, content, false, false, ScrollbarGeometry.GutterSize);
            Require(viewport.Width == 300f && viewport.Height == 100f, "A fitting host has no induced scrollbar in either direction.");
        }

        private static void ScrollViewsReflowWhenTheAllocatedSizeChanges() {
            ScrollView scroll = new ScrollView(); for (int i = 0; i < 8; i++) scroll.Add(new Label("Row " + i));
            LayoutEngine engine = new LayoutEngine(Metrics);
            LayoutNode node = engine.Measure(scroll, new LayoutConstraints(0f, 300f, 0f, 1000f), "scroll");
            engine.Arrange(node, new MuiRect(0f, 0f, 300f, 100f));
            Require(node.Children[0].Rect.Width == 284f, "A parent that gives less height must trigger scrollbar-width reflow.");
            MuiRect extent = ScrollGeometry.ContentExtent(node, node.Rect);
            Require(extent.Height == 256f && extent.Width == 284f, "Reflow preserves all content without creating accidental horizontal overflow.");

            engine = new LayoutEngine(new LayoutMetrics { RowHeight = 32f, TextWidth = 8f });
            scroll = new ScrollView { new Label(new string('A', 40)) { WordWrap = true } };
            node = engine.Measure(scroll, new LayoutConstraints(0f, 100f, 0f, 100f), "grows-wider");
            engine.Arrange(node, new MuiRect(0f, 0f, 300f, 100f));
            Require(node.Children[0].Rect.Width == 300f && node.Children[0].Rect.Height == 64f,
                "A wider final allocation must release a gutter induced only by the previous narrow wrapping.");

            scroll = new ScrollView { Height = 60f };
            scroll.Add(new Label(new string('A', 75)) { WordWrap = true });
            node = engine.Measure(scroll, new LayoutConstraints(0f, 300f, 0f, 100f), "grows-taller");
            engine.Arrange(node, new MuiRect(0f, 0f, 300f, 80f));
            Require(node.Children[0].Rect.Width == 300f && node.Children[0].Rect.Height == 64f,
                "A scrollbar must not perpetuate itself through its own narrower wrapping after the final height grows.");
        }

        private static void VirtualListsSupportKeyboardScrollBoundaries() {
            float next;
            Require(KeyboardNavigation.ScrollVertical(0f, 32f, 160f, 32000f, KeyCode.PageDown, out next) && next == 128f, "Page Down keeps one row of context.");
            Require(KeyboardNavigation.ScrollVertical(next, 32f, 160f, 32000f, KeyCode.End, out next) && next == 31840f, "End reaches the full content bottom.");
            Require(KeyboardNavigation.ScrollVertical(next, 32f, 160f, 32000f, KeyCode.Home, out next) && next == 0f, "Home returns to the first row.");
            Require(!KeyboardNavigation.ScrollVertical(0f, 32f, 160f, 32000f, KeyCode.Space, out next), "Scrolling must not consume activation keys.");
        }

        private static void UnassignedPaintPathsAreNotHoveredOrFocused() {
            WidgetPaintState state = new WidgetPaintState(); LayoutNode node = new LayoutNode { Widget = new Button("Button") };
            Require(!state.IsHovered(node) && !state.IsActive(node) && !state.IsFocused(node) && !state.IsScrollbarHovered(node),
                "Missing path identities are not proof of hover, focus, or pointer capture.");
        }

        private static void ScrollFrameQueriesMatchThePaintedClip() {
            Widget[] panels = { new ListBox<int>(0, Range(30)), new SearchableListBox<int>(0, Range(30)), new DebugLogWindow(), new ThemeEditor(Theme) };
            WidgetPainter painter = Painter();
            foreach (Widget panel in panels) {
                LayoutNode node = Node(panel, 480f, 160f); ScrollFrameLayout frame;
                Require(painter.TryGetScrollFrame(node, out frame), "Every built-in panel publishes its actual frame geometry.");
                DrawList drawing = painter.Paint(node, new WidgetPaintState()); bool found = false;
                foreach (DrawCommand command in drawing.Commands) if (command.Type == DrawCommandType.PushClip && RectEqual(command.Rect, frame.ContentViewport)) found = true;
                Require(found, "Diagnostic scroll metadata must be the geometry used by the actual painter.");
            }
        }

        private static WidgetPainter Painter() { return new WidgetPainter(Theme, Metrics); }
        private static LayoutNode Node(Widget widget, float width, float height) { return new LayoutEngine(Metrics).Layout(widget, new MuiRect(0f, 0f, width, height), "control"); }
        private static int[] Range(int count) { int[] values = new int[count]; for (int i = 0; i < count; i++) values[i] = i; return values; }
        private static DrawCommand FindText(DrawList list, string text) { foreach (DrawCommand command in list.Commands) if (command.Type == DrawCommandType.Text && command.Text == text) return command; return null; }
        private static bool HasText(DrawList list, string text) { return FindText(list, text) != null; }
        private static bool Contains(MuiRect outer, MuiRect inner) { return inner.X >= outer.X - .01f && inner.Y >= outer.Y - .01f && inner.Right <= outer.Right + .01f && inner.Bottom <= outer.Bottom + .01f; }
        private static bool RectEqual(MuiRect a, MuiRect b) { return Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Width, b.Width) && Near(a.Height, b.Height); }
        private static bool Same(MuiColor a, MuiColor b) { return Near(a.R, b.R) && Near(a.G, b.G) && Near(a.B, b.B) && Near(a.A, b.A); }
        private static bool Near(float a, float b) { return Math.Abs(a - b) < .01f; }
        private static double Contrast(MuiColor a, MuiColor b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05d) / (Math.Min(x, y) + .05d); }
        private static double Luminance(MuiColor c) { return Channel(c.R) * .2126d + Channel(c.G) * .7152d + Channel(c.B) * .0722d; }
        private static double Channel(double c) { return c <= .04045d ? c / 12.92d : Math.Pow((c + .055d) / 1.055d, 2.4d); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
