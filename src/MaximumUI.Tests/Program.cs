using System;
using System.IO;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class Program {
        public static int Main() {
            try {
                LayoutEngineArrangesWindowStack();
                LayoutHintExtensionsSetConstraints();
                InputDispatcherReportsClicks();
                DragControllerMovesRects();
                WidgetPainterProducesDrawList();
                BasicFeatureControlsLayoutAndPaint();
                TextInputFlagsFilterAndPaint();
                NumericControlsLayoutAndPaint();
                ColorControlsLayoutAndPaint();
                SearchableSelectionControlsLayoutAndPaint();
                TreeHeaderControlsLayoutAndPaint();
                MenuBarLayoutAndPaint();
                TabsLayoutAndPaintAdvancedHeaders();
                MediaAndPlotLayoutAndPaint();
                CustomDrawWidgetsLayoutAndPaint();
                VirtualListLayoutAndPaint();
                TwoPaneLayoutArrangesPanes();
                DragDropLayoutAndPaint();
                TableColumnsLayoutAndPaint();
                SelectionStateAppliesSingleToggleAndRange();
                DebugToolsLayoutAndPaint();
                ThemeEditorLayoutAndPaint();
                QueryStoreRecordsItemsWindowsAndCapture();
                ShortcutRouterRegistersAndResolves();
                NavigationStoreFocusesDefaultRequestedAndSequentialItems();
                AdvancedControlsLayoutAndPaint();
                GridAndTableLayoutAndPaint();
                WidgetOverlayExtensionsStoreState();
                DrawListRecordsCommands();
                ViewBuildScheduleCoalescesEventsAndPreservesInvalidation();
                LayoutCacheReusesMovesResizesAndInvalidates();
                ScrollViewKeepsFullContentExtent();
                SmallSplitPanesStayInsideViewport();
                DialogVisibilityMatchesVisibleTabContent();
                FoldedWindowsDoNotActivateHiddenDialogs();
                NavigationSkipsDisabledAndUnfocusableWidgets();
                FontSizingAndWrappedLabelsRespectAvailableSpace();
                DrawingOnlyRunsDuringRepaint();
                DrawListCanAppendItselfWithoutGrowingForever();
                DialogCallbacksCannotCloseNewDialogsOrLoopForever();
                OverlayOrderSelectsOnlyTheTopDialog();
                ThemeDesignTests.RunAll();
                UiInteractionTests.RunAll();
                MountRoutingTests.RunAll();
                UiFollowupReviewTests.Run();
                CoordinateTests.Run();
                DialogLayoutTests.RunAll();
                NumericInputTests.Run();
                PluginEditorViewTests.RunAll();
                EmbeddedUiTests.Run();
                InlineDialogLayoutTests.RunAll();
                if (Environment.GetEnvironmentVariable("MAXIMUMUI_BENCHMARK") == "1") MeasureBuildAndLayoutPerformance();
                int count = 41 + ThemeDesignTests.TestCount + UiInteractionTests.TestCount + MountRoutingTests.TestCount
                    + UiFollowupReviewTests.TestCount + CoordinateTests.TestCount + DialogLayoutTests.TestCount + NumericInputTests.TestCount
                    + PluginEditorViewTests.TestCount + EmbeddedUiTests.TestCount + InlineDialogLayoutTests.TestCount;
                Console.WriteLine("MaximumUI regression groups: " + count + " passed.");
                Console.WriteLine("MaximumUI headless tests passed.");
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void OverlayOrderSelectsOnlyTheTopDialog() {
            OverlayLayer overlay = new OverlayLayer();
            RenderContext context = new RenderContext();
            Dialog first = new Dialog("first");
            Dialog second = new Dialog("second");
            Dialog service = new Dialog("service");
            System.Collections.Generic.List<string> order = new System.Collections.Generic.List<string>();
            overlay.Register(new OverlayRequest {
                SortOrder = 1000, ModalDialog = first,
                Render = delegate(RenderContext c) { order.Add("first"); Assert(object.ReferenceEquals(c.InputDialog, service), "lower dialog knows the top modal before drawing"); }
            });
            overlay.Register(new OverlayRequest {
                SortOrder = 1100, ModalDialog = service,
                Render = delegate(RenderContext c) { order.Add("service"); }
            });
            overlay.Register(new OverlayRequest {
                SortOrder = 1000, ModalDialog = second,
                Render = delegate(RenderContext c) { order.Add("second"); }
            });
            overlay.Render(context);
            Assert(string.Join(",", order.ToArray()) == "first,second,service", "equal-priority dialogs preserve registration order");
            Assert(object.ReferenceEquals(context.InputDialog, service), "service dialog owns input above declarative dialogs");
            overlay.Register(new OverlayRequest { SortOrder = 1000, ModalDialog = first });
            overlay.Register(new OverlayRequest { SortOrder = 1000, ModalDialog = second });
            overlay.Render(context);
            Assert(object.ReferenceEquals(context.InputDialog, second), "last equal-priority modal owns input");
            overlay.Render(context);
            Assert(context.InputDialog == null, "cleared overlay does not retain a closed modal owner");
        }

        private static void ViewBuildScheduleCoalescesEventsAndPreservesInvalidation() {
            MountOptions options = new MountOptions();
            ViewBuildSchedule schedule = new ViewBuildSchedule();
            Assert(schedule.ShouldBuild(options, 0, false), "first build requested");
            schedule.CompleteBuild(0, schedule.Revision);
            Assert(!schedule.ShouldBuild(options, 0, true), "Layout/Repaint share a build");
            Assert(schedule.ShouldBuild(options, 1, true), "plain views refresh on the next frame");
            options.BuildEveryFrame = false;
            Assert(!schedule.ShouldBuild(options, 1, true), "retained view is idle without invalidation");
            schedule.MarkDirty();
            int startedRevision = schedule.Revision;
            Assert(schedule.ShouldBuild(options, 0, true), "same-frame state change rebuilds immediately");
            schedule.MarkDirty();
            schedule.CompleteBuild(0, startedRevision);
            Assert(schedule.ShouldBuild(options, 0, true), "invalidation during Build is retained");
            schedule.CompleteBuild(0, schedule.Revision);
            Assert(!schedule.ShouldBuild(options, 5, true), "retained build remains cached");
            options.BuildEveryOnGUI = true;
            Assert(schedule.ShouldBuild(options, 0, true), "legacy per-event mode remains available");
        }

        private static void LayoutCacheReusesMovesResizesAndInvalidates() {
            VStack stack = new VStack();
            Label label = new Label("Before");
            stack.Add(label);
            LayoutCache cache = new LayoutCache();
            MuiRect viewport = new MuiRect(10f, 20f, 300f, 180f);
            LayoutNode first = cache.Layout(stack, viewport, "root");
            float originalY = first.Children[0].Rect.Y;
            LayoutNode second = cache.Layout(stack, viewport, "root");
            Assert(object.ReferenceEquals(first, second), "unchanged tree reuses all layout nodes");
            AssertEqual(1, (int)cache.LayoutPassCount, "one measurement across repeated events");
            viewport.Y += 35f;
            LayoutNode moved = cache.Layout(stack, viewport, "root");
            Assert(object.ReferenceEquals(first, moved), "moving a window only rearranges it");
            AssertEqual(originalY + 35f, moved.Children[0].Rect.Y, "cached child follows moved window");
            viewport.Width -= 20f;
            LayoutNode resized = cache.Layout(stack, viewport, "root");
            Assert(!object.ReferenceEquals(first, resized), "width change remeasures layout");
            label.Text = "A significantly longer label";
            float previousWidth = resized.Children[0].DesiredSize.X;
            cache.Invalidate();
            LayoutNode changed = cache.Layout(stack, viewport, "root");
            Assert(changed.Children[0].DesiredSize.X > previousWidth, "invalidated mutable data is remeasured");
            Label replacement = new Label("replacement");
            LayoutNode replaced = cache.Layout(replacement, viewport, "root");
            Assert(object.ReferenceEquals(replacement, replaced.Widget), "new widget at same path replaces cache entry");
            for (int i = 0; i < 1000; i++) cache.Layout(replacement, viewport, "dynamic." + i);
            Assert(cache.Count <= 256, "dynamic paths cannot grow the layout cache without bound");
        }

        private static void ScrollViewKeepsFullContentExtent() {
            VStack content = new VStack();
            for (int i = 0; i < 100; i++) content.Add(new Label("Row " + i).Height(22f));
            ScrollView scroll = new ScrollView { Height = 110f };
            scroll.Add(content);
            LayoutNode root = new LayoutEngine().Layout(scroll, new MuiRect(0f, 0f, 300f, 110f));
            AssertEqual(2200f, root.Children[0].DesiredSize.Y, "content measures beyond the viewport");
            AssertEqual(2200f, root.Children[0].Rect.Height, "content arrangement preserves scroll range");
            AssertEqual(2200f, root.Find("0.0.99").Rect.Bottom, "last row is included in scroll content");
            AssertEqual(284f, root.Children[0].Rect.Width, "vertical scrollbar does not create horizontal overflow");
            MuiRect extent = ScrollGeometry.ContentExtent(root, root.Rect);
            AssertEqual(284f, extent.Width, "actual renderer extent reserves the scrollbar gutter");
            AssertEqual(2200f, extent.Height, "actual renderer extent includes all rows");
            int first;
            int end;
            ScrollGeometry.VisibleRows(22000f, 220f, 22f, 100000, out first, out end);
            AssertEqual(999, first, "virtual/dropdown range follows scroll offset");
            AssertEqual(13, end - first, "large dropdown visits only visible rows plus overscan");
            ScrollGeometry.VisibleRows(22000f, 220f, 22f, 0, out first, out end);
            AssertEqual(0, end - first, "empty filtered result visits no rows");
        }

        private static void SmallSplitPanesStayInsideViewport() {
            TwoPaneLayout split = new TwoPaneLayout(0.7f) { MinFirst = 90f, MinSecond = 70f };
            split.Add(new Label("First"));
            split.Add(new Label("Second"));
            LayoutEngine engine = new LayoutEngine();
            LayoutNode root = engine.Layout(split, new MuiRect(0f, 0f, 60f, 100f));
            Assert(root.Children[0].Rect.Width >= 0f && root.Children[1].Rect.Width >= 0f, "small panes stay nonnegative");
            Assert(root.Children[1].Rect.Right <= root.Rect.Right, "horizontal split fits small viewport");
            split.Orientation = TwoPaneOrientation.Vertical;
            root = engine.Layout(split, new MuiRect(0f, 0f, 100f, 60f));
            Assert(root.Children[1].Rect.Bottom <= root.Rect.Bottom, "vertical split fits small viewport");
        }

        private static void DialogVisibilityMatchesVisibleTabContent() {
            Tabs tabs = new Tabs(0);
            tabs.Add(new LeadingTabButton("+", null));
            tabs.Add(new Tab("hidden") { Visible = false }.Content(new Dialog("Hidden")));
            tabs.Add(new Tab("normal").Content(new Label("No modal")));
            tabs.Add(new Tab("dialog").Content(new Dialog("Visible")));
            Assert(!WidgetVisibility.ContainsDialog(tabs), "leading buttons and hidden tabs do not shift selected tab");
            tabs.SelectedIndex = 1;
            Assert(WidgetVisibility.ContainsDialog(tabs), "selected visible tab activates its dialog");
            tabs.SelectedIndex = 99;
            Assert(WidgetVisibility.ContainsDialog(tabs), "out of range selection matches layout clamping");
            CollapsingHeader header = new CollapsingHeader("Closed", false);
            header.Add(new Dialog("Hidden"));
            Assert(!WidgetVisibility.ContainsDialog(header), "collapsed header cannot invisibly block input");
            ClosableCollapsingHeader closable = new ClosableCollapsingHeader("Hidden", true) { Visible = false };
            closable.Add(new Dialog("Hidden"));
            Assert(!WidgetVisibility.ContainsDialog(closable), "hidden closable header is ignored");
            TreeNode tree = new TreeNode("Closed", false);
            tree.Add(new Dialog("Hidden"));
            Assert(!WidgetVisibility.ContainsDialog(tree), "closed tree cannot invisibly block input");
            Window window = new Window("Hidden") { Visible = false };
            window.Add(new Dialog("Hidden"));
            Assert(!WidgetVisibility.ContainsDialog(window), "hidden window is ignored");
        }

        private static void FoldedWindowsDoNotActivateHiddenDialogs() {
            UiStateStore states = new UiStateStore();
            RenderContext context = new RenderContext { MountId = "first", StateStore = states, Path = "0" };
            Window keyed = new Window("Window", key: "main");
            keyed.Add(new Dialog("Hidden while folded"));
            Assert(WidgetVisibility.ContainsRootDialog(keyed, context, "0"), "a new unfolded window exposes its dialog");
            AssertEqual(0, states.Count, "modal detection does not create window state");
            WindowState keyedState = states.GetOrCreate<WindowState>(context.StateId(keyed, "window"));
            keyedState.Folded = true;
            Assert(WidgetVisibility.ContainsDialog(keyed), "the retained widget tree still contains the hidden dialog");
            Assert(!WidgetVisibility.ContainsRootDialog(keyed, context, "0"), "folded window content cannot lock global modal input");
            RenderContext otherMount = new RenderContext { MountId = "second", StateStore = states };
            Assert(WidgetVisibility.ContainsRootDialog(keyed, otherMount, "0"), "fold state is isolated by mount identity");
            keyedState.Folded = false;
            Assert(WidgetVisibility.ContainsRootDialog(keyed, context, "0"), "unfolding the same cached tree restores its dialog");

            Window unkeyed = new Window("No key");
            unkeyed.Add(new Dialog("Path-based dialog"));
            WindowState pathState = states.GetOrCreate<WindowState>(context.StateId(unkeyed, "window"));
            pathState.Folded = true;
            Assert(!WidgetVisibility.ContainsRootDialog(unkeyed, context, "0"), "unkeyed root uses the renderer's 0 path");
            Assert(WidgetVisibility.ContainsRootDialog(unkeyed, context, "outer.0"), "a different root path does not reuse folded state");
            context.Path = "outer.0";
            states.GetOrCreate<WindowState>(context.StateId(unkeyed, "window")).Folded = true;
            Assert(!WidgetVisibility.ContainsRootDialog(unkeyed, context, "outer.0"), "direct renderer custom root path finds the correct window state");
            DialogService dialogs = new DialogService();
            dialogs.Show(new Dialog("Service modal"));
            Assert(dialogs.HasOpenDialogs || WidgetVisibility.ContainsRootDialog(unkeyed, context, "outer.0"), "service dialogs remain modal independently of folded window contents");
        }

        private static void NavigationSkipsDisabledAndUnfocusableWidgets() {
            UiNavigationStore navigation = new UiNavigationStore();
            MuiRect rect = new MuiRect(0f, 0f, 40f, 22f);
            navigation.BeginFrame();
            navigation.Register(new Button("Disabled", key: "disabled") { HasDefaultFocus = true }, "0", rect, false);
            navigation.Register(new Button("Decorative", key: "decorative") { Focusable = false }, "1", rect, true);
            navigation.Register(new Button("First", key: "first"), "2", rect, true);
            navigation.Register(new Button("Second", key: "second"), "3", rect, true);
            navigation.EndFrame();
            AssertEqual(2, navigation.Count, "only enabled focusable widgets join navigation");
            AssertEqual("key:first", navigation.FocusedId, "disabled default focus is skipped");
            Assert(!navigation.Focus("disabled"), "disabled widget cannot be explicitly focused");
            navigation.FocusNext();
            AssertEqual("key:second", navigation.FocusedId, "Tab reaches next enabled widget");
            navigation.FocusNext();
            AssertEqual("key:first", navigation.FocusedId, "Tab wraps past disabled widgets");
        }

        private static void FontSizingAndWrappedLabelsRespectAvailableSpace() {
            AssertEqual(12, ThemeSizing.ScalePixels(12, 640f), "small screen keeps readable minimum");
            AssertEqual(12, ThemeSizing.ScalePixels(12, 1280f), "baseline screen font size");
            AssertEqual(18, ThemeSizing.ScalePixels(12, 3840f), "large screen scaling is bounded");
            AssertEqual(12, ThemeSizing.ScalePixels(12, float.NaN), "invalid screen dimension is safe");
            LayoutMetrics metrics = new LayoutMetrics {
                MeasureText = delegate(string text) { return text.Length * 13f; },
                MeasureWrappedTextHeight = delegate(string text, float width) { return (float)Math.Ceiling(text.Length * 13f / width) * 24f; }
            };
            Label label = new Label("ABCDEFGHIJKLMNO") { WordWrap = true };
            LayoutNode root = new LayoutEngine(metrics).Measure(label, new LayoutConstraints(0f, 65f, 0f, 200f), "0");
            AssertEqual(72f, root.DesiredSize.Y, "font metrics supply enough height for wrapped text");
        }

        private static void DrawingOnlyRunsDuringRepaint() {
            foreach (UnityEngine.EventType type in Enum.GetValues(typeof(UnityEngine.EventType))) {
                Assert(GuiEventPolicy.ShouldPaint(type) == (type == UnityEngine.EventType.Repaint), "drawing event policy: " + type);
            }
            bool release = GuiEventPolicy.ShouldReleasePointer(UnityEngine.EventType.MouseUp, 0);
            // Event.Use changes type to Used, so remember release before dispatching it.
            Assert(release && !GuiEventPolicy.ShouldReleasePointer(UnityEngine.EventType.Used, 0), "pointer release survives event consumption");
            Assert(!GuiEventPolicy.ShouldReleasePointer(UnityEngine.EventType.MouseUp, 1), "right button cannot release a left drag");
        }

        private static void DrawListCanAppendItselfWithoutGrowingForever() {
            DrawList drawList = new DrawList();
            drawList.FillRect(new MuiRect(2f, 3f, 4f, 5f), MuiColor.FromRgb(1f, 0f, 0f));
            drawList.Append(drawList, new MuiVec2(10f, 20f));
            AssertEqual(2, drawList.Commands.Count, "self append duplicates original commands exactly once");
            AssertEqual(2f, drawList.Commands[0].Rect.X, "self append leaves original unchanged");
            AssertEqual(12f, drawList.Commands[1].Rect.X, "self append offsets copied command");
        }

        private static void DialogCallbacksCannotCloseNewDialogsOrLoopForever() {
            DialogService service = new DialogService();
            Dialog original = new Dialog("Original");
            Dialog next = new Dialog("Next");
            int confirmCount = 0;
            service.Show(original);
            service.Complete(original, delegate {
                confirmCount++;
                service.Show(next);
            });
            service.Complete(original, delegate { confirmCount++; });
            AssertEqual(1, confirmCount, "confirmation runs once");
            AssertEqual(1, service.Dialogs.Count, "confirmation callback may open another dialog");
            Assert(object.ReferenceEquals(next, service.Dialogs[0]), "the newly opened dialog stays open");
            int closed = 0;
            next.OnClose = delegate { closed++; service.Show(new Dialog("Reopened")); };
            service.CloseAll();
            AssertEqual(1, closed, "close all invokes callbacks once");
            AssertEqual(1, service.Dialogs.Count, "close all does not recursively close callback-created dialogs");
            service.CloseAll();
            Dialog older = new Dialog("Older") { OnClose = delegate { closed++; } };
            Dialog failing = new Dialog("Failing") { OnClose = delegate { throw new InvalidOperationException("Expected callback failure"); } };
            service.Show(older);
            service.Show(failing);
            bool failed = false;
            try { service.CloseAll(); } catch (InvalidOperationException) { failed = true; }
            Assert(failed, "callback failure is reported");
            AssertEqual(2, closed, "callback failure does not skip remaining cleanup");
            AssertEqual(0, service.Dialogs.Count, "dialogs are removed even when callbacks fail");
        }

        private sealed class BenchmarkResult {
            public double Milliseconds;
            public long AllocatedBytes;
            public int Builds;
            public long Layouts;
        }

        private static void MeasureBuildAndLayoutPerformance() {
            System.Reflection.MethodInfo allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            Func<long> allocated = allocationMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
            RunLayoutBenchmark(3, 100, 0, allocated);
            RunLayoutBenchmark(3, 100, 1, allocated);
            RunLayoutBenchmark(3, 100, 2, allocated);
            BenchmarkResult legacy = RunLayoutBenchmark(150, 1000, 0, allocated);
            BenchmarkResult frame = RunLayoutBenchmark(150, 1000, 1, allocated);
            BenchmarkResult retained = RunLayoutBenchmark(150, 1000, 2, allocated);
            AssertEqual(300, legacy.Builds, "legacy benchmark builds twice per frame");
            AssertEqual(150, frame.Builds, "default benchmark builds once per frame");
            AssertEqual(1, retained.Builds, "retained benchmark builds only once");
            AssertEqual(150, (int)frame.Layouts, "default layout is shared across events");
            AssertEqual(1, (int)retained.Layouts, "retained layout is shared across idle frames");
            PrintBenchmark("per-event-strategy", legacy);
            PrintBenchmark("default-per-frame", frame);
            PrintBenchmark("retained-idle", retained);
            Console.WriteLine("Benchmark: same fixed engine, 1000 labels, 150 frames, 2 GUI events/frame; build/layout only, not a baseline DLL comparison or Unity frame rate.");
        }

        private static BenchmarkResult RunLayoutBenchmark(int frames, int rows, int mode, Func<long> allocated) {
            LayoutEngine engine = new LayoutEngine();
            LayoutCache cache = new LayoutCache(engine);
            ViewBuildSchedule schedule = new ViewBuildSchedule();
            MountOptions options = new MountOptions { BuildEveryFrame = mode != 2 };
            Widget tree = null;
            MuiRect viewport = new MuiRect(0f, 0f, 520f, 620f);
            BenchmarkResult result = new BenchmarkResult();
            GC.Collect();
            long before = allocated == null ? 0 : allocated();
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++) {
                for (int guiEvent = 0; guiEvent < 2; guiEvent++) {
                    if (mode == 0 || schedule.ShouldBuild(options, frame, tree != null)) {
                        VStack stack = new VStack(2f);
                        for (int i = 0; i < rows; i++) stack.Add(new Label("Benchmark row " + i));
                        tree = stack;
                        result.Builds++;
                        schedule.CompleteBuild(frame, schedule.Revision);
                        cache.Invalidate();
                    }
                    LayoutNode layout = mode == 0 ? engine.Layout(tree, viewport) : cache.Layout(tree, viewport, "0");
                    if (layout.Children.Count != rows) throw new InvalidOperationException("Benchmark dropped rows");
                }
            }
            watch.Stop();
            result.Milliseconds = watch.Elapsed.TotalMilliseconds;
            result.AllocatedBytes = allocated == null ? -1 : allocated() - before;
            result.Layouts = mode == 0 ? frames * 2 : cache.LayoutPassCount;
            return result;
        }

        private static void PrintBenchmark(string name, BenchmarkResult result) {
            Console.WriteLine("BENCH " + name + ": builds=" + result.Builds + ", layouts=" + result.Layouts +
                ", elapsed_ms=" + result.Milliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ", allocated_bytes=" + result.AllocatedBytes);
        }

        private static void LayoutEngineArrangesWindowStack() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new Toggle("A", false).Height(20f));
            stack.Add(new Toggle("B", false).Height(24f));
            stack.Add(new Spacer().ExpandHeight());
            stack.Add(new Toggle("C", false).Height(24f));

            Window window = new Window("Test", width: 300f, height: 200f, key: "window");
            window.Add(stack);

            LayoutEngine engine = new LayoutEngine();
            LayoutNode root = engine.Layout(window, new MuiRect(0f, 0f, 300f, 200f));

            AssertEqual(300f, root.Rect.Width, "window width");
            AssertEqual(200f, root.Rect.Height, "window height");

            LayoutNode stackNode = root.Find("0.0");
            AssertNotNull(stackNode, "stack node");
            AssertEqual(10f, stackNode.Rect.X, "stack x");
            AssertEqual(26f, stackNode.Rect.Y, "stack y");
            AssertEqual(280f, stackNode.Rect.Width, "stack width");
            AssertEqual(164f, stackNode.Rect.Height, "stack height");

            LayoutNode spacerNode = root.Find("0.0.2");
            AssertNotNull(spacerNode, "spacer node");
            AssertEqual(84f, spacerNode.Rect.Height, "spacer expanded height");
        }

        private static void LayoutHintExtensionsSetConstraints() {
            Button button = new Button("Constrained")
                .MinWidth(80f)
                .MaxWidth(100f)
                .MinHeight(30f)
                .MaxHeight(40f);

            Assert(button.Layout.HasMinWidth, "min width flag");
            AssertEqual(80f, button.Layout.MinWidth, "min width value");
            Assert(button.Layout.HasMaxWidth, "max width flag");
            AssertEqual(100f, button.Layout.MaxWidth, "max width value");
            Assert(button.Layout.HasMinHeight, "min height flag");
            AssertEqual(30f, button.Layout.MinHeight, "min height value");
            Assert(button.Layout.HasMaxHeight, "max height flag");
            AssertEqual(40f, button.Layout.MaxHeight, "max height value");
        }

        private static void InputDispatcherReportsClicks() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new Toggle("A", false).Height(20f));
            stack.Add(new Toggle("B", false).Height(24f));

            Window window = new Window("Input", width: 240f, height: 120f, key: "window");
            window.Add(stack);

            LayoutNode root = new LayoutEngine().Layout(window, new MuiRect(0f, 0f, 240f, 120f));
            LayoutNode target = root.Find("0.0.1");
            AssertNotNull(target, "target button");

            MuiVec2 point = new MuiVec2(target.Rect.X + 2f, target.Rect.Y + 2f);
            InputDispatcher dispatcher = new InputDispatcher();
            dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = point
            }, root);
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseUp,
                MouseButton = 0,
                MousePosition = point
            }, root);

            AssertEqual("0.0.1", result.ClickedPath, "clicked path");
            Assert(result.ClickedWidget is Toggle, "clicked widget");
        }

        private static void DrawListRecordsCommands() {
            DrawList drawList = new DrawList();
            drawList.PushClip(new MuiRect(0f, 0f, 100f, 100f));
            drawList.FillRect(new MuiRect(1f, 2f, 3f, 4f), MuiColor.FromRgb(1f, 0f, 0f));
            drawList.Text(new MuiRect(0f, 0f, 50f, 20f), "Hello", null, MuiColor.FromRgb(0f, 0f, 0f));
            drawList.Line(new MuiVec2(0f, 0f), new MuiVec2(10f, 10f), MuiColor.FromRgb(0f, 0f, 1f), 2f);
            drawList.FillCircle(new MuiVec2(12f, 12f), 4f, MuiColor.FromRgb(0f, 1f, 0f));
            drawList.PopClip();

            AssertEqual(6, drawList.Commands.Count, "draw command count");
            AssertEqual(DrawCommandType.PushClip, drawList.Commands[0].Type, "push clip");
            AssertEqual(DrawCommandType.FillRect, drawList.Commands[1].Type, "fill rect");
            AssertEqual(DrawCommandType.Text, drawList.Commands[2].Type, "text");
            AssertEqual(DrawCommandType.Line, drawList.Commands[3].Type, "line");
            AssertEqual(DrawCommandType.FillCircle, drawList.Commands[4].Type, "fill circle");
            AssertEqual(DrawCommandType.PopClip, drawList.Commands[5].Type, "pop clip");
        }

        private static void WidgetOverlayExtensionsStoreState() {
            Button menuButton = new Button("Open");
            VStack menu = new VStack();
            menu.Add(new Button("Item"));

            menuButton.Tooltip("Tip text").ContextMenu(menu);

            AssertEqual("Tip text", menuButton.TooltipText, "tooltip text");
            Assert(object.ReferenceEquals(menu, menuButton.ContextMenuContent), "context menu content");
        }

        private static void DragControllerMovesRects() {
            DragController controller = new DragController();
            DragState state = new DragState();
            MuiRect rect = new MuiRect(10f, 20f, 100f, 40f);
            MuiRect title = new MuiRect(10f, 20f, 100f, 20f);

            Assert(controller.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(30f, 35f)
            }, title, state, ref rect), "drag mouse down consumed");

            Assert(controller.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDrag,
                MouseButton = 0,
                MousePosition = new MuiVec2(50f, 60f)
            }, title, state, ref rect), "drag mouse move consumed");

            AssertEqual(30f, rect.X, "dragged x");
            AssertEqual(45f, rect.Y, "dragged y");

            Assert(controller.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseUp,
                MouseButton = 0,
                MousePosition = new MuiVec2(50f, 60f)
            }, title, state, ref rect), "drag mouse up consumed");
            Assert(!state.Active, "drag inactive after mouse up");
        }

        private static void WidgetPainterProducesDrawList() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new Label("Name").Height(20f));
            stack.Add(new Button("Apply").Height(24f));
            stack.Add(new Slider(0.5f, 0f, 1f).Height(16f));

            Window window = new Window("Paint", width: 240f, height: 120f, key: "window");
            window.Add(stack);

            LayoutNode root = new LayoutEngine().Layout(window, new MuiRect(0f, 0f, 240f, 120f));
            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.0.1"
            });

            Assert(drawList.Commands.Count >= 10, "paint command count");
            AssertEqual(DrawCommandType.FillRect, drawList.Commands[0].Type, "window fill");
            AssertEqual(DrawCommandType.StrokeRect, drawList.Commands[1].Type, "window stroke");
            AssertEqual(DrawCommandType.Text, drawList.Commands[2].Type, "window title");
        }

        private static void BasicFeatureControlsLayoutAndPaint() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new BulletText("Bullet").Height(20f));
            stack.Add(new ValueLabel("Mode", "Normal").Height(20f));
            stack.Add(new TextLink("Link").Height(20f));
            stack.Add(new HStack(4f) {
                new SmallButton("S").Height(18f),
                new ArrowButton(ArrowDirection.Right).Height(20f).Width(20f),
                new Checkbox("Check", true).Height(20f)
            }.Height(20f));
            stack.Add(new SegmentedControl<string>("B", new[] { "A", "B", "C" }).Height(22f));
            stack.Add(new StatusBadge("OK", StatusBadgeKind.Success).Height(18f));
            stack.Add(new LoadingIndicator("Loading").Height(18f));
            stack.Add(new SeparatorText("Section").Height(20f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 340f, 180f));
            AssertNotNull(root.Find("0.0"), "bullet node");
            AssertNotNull(root.Find("0.1"), "value label node");
            AssertNotNull(root.Find("0.2"), "text link node");
            AssertNotNull(root.Find("0.3.0"), "small button node");
            AssertNotNull(root.Find("0.3.1"), "arrow button node");
            AssertNotNull(root.Find("0.3.2"), "checkbox node");
            AssertNotNull(root.Find("0.4"), "segmented node");
            AssertNotNull(root.Find("0.5"), "status badge node");
            AssertNotNull(root.Find("0.6"), "loading node");
            AssertNotNull(root.Find("0.7"), "separator text node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode link = root.Find("0.2");
            InputDispatchResult down = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(link.Rect.X + 2f, link.Rect.Y + 2f)
            }, root);
            AssertEqual("0.2", down.ActivePath, "text link active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.2",
                ActivePath = "0.4"
            });
            Assert(drawList.Commands.Count >= 24, "basic feature paint command count");
        }

        private static void TextInputFlagsFilterAndPaint() {
            AssertEqual("ABC123", TextInputFilter.Apply("ab c123", TextInputFlags.Uppercase | TextInputFlags.NoBlank, null, 0), "uppercase no blank filter");
            AssertEqual("-12.5", TextInputFilter.Apply("x-12.5z", TextInputFlags.Decimal, null, 0), "decimal filter");
            AssertEqual("1e-05", TextInputFilter.Apply("1e-05 volts", TextInputFlags.Scientific | TextInputFlags.NoBlank, null, 0), "scientific filter");
            AssertEqual("DEAD", TextInputFilter.Apply("dead-xyz", TextInputFlags.Hexadecimal | TextInputFlags.Uppercase, null, 0), "hex filter");
            AssertEqual("ACE", TextInputFilter.Apply("ABCDE", TextInputFlags.None, delegate(char ch) { return ch != 'B' && ch != 'D'; }, 0), "custom char filter");
            AssertEqual("ABC", TextInputFilter.Apply("ABCDE", TextInputFlags.None, null, 3), "max length filter");

            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new TextField("abc", key: "text-flags") {
                Placeholder = "Text",
                Flags = TextInputFlags.Uppercase | TextInputFlags.NoBlank | TextInputFlags.EscapeReverts,
                MaxLength = 8
            }.Height(22f));
            stack.Add(new TextField("readonly", key: "text-readonly") {
                Flags = TextInputFlags.ReadOnly
            }.Height(22f));
            stack.Add(new PasswordField("secret", key: "password-flags") {
                Flags = TextInputFlags.NoBlank
            }.Height(22f));
            stack.Add(new SearchBox("needle", key: "search-flags") {
                Flags = TextInputFlags.EscapeClearsAll
            }.Height(22f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 260f, 120f));
            AssertNotNull(root.Find("0.0"), "text flags node");
            AssertNotNull(root.Find("0.1"), "readonly text node");
            AssertNotNull(root.Find("0.2"), "password flags node");
            AssertNotNull(root.Find("0.3"), "search flags node");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 12, "text input flags paint command count");
        }

        private static void NumericControlsLayoutAndPaint() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new InputInt("Int", 7) {
                HasMin = true,
                Min = 0,
                HasMax = true,
                Max = 10
            }.Height(22f));
            stack.Add(new InputFloat("Float", 0.5f) {
                HasMin = true,
                Min = 0f,
                HasMax = true,
                Max = 1f
            }.Height(22f));
            stack.Add(new InputDouble("Double", 3.14).Height(22f));
            stack.Add(new DragInt("DragInt", 3).Height(22f));
            stack.Add(new DragFloat("DragFloat", 0.25f).Height(22f));
            stack.Add(new SliderInt("SliderInt", 25, 0, 100).Height(22f));
            stack.Add(new SliderFloat("SliderFloat", 0.25f, 0f, 1f).Height(22f));
            stack.Add(new SliderAngle("Angle", 30f).Height(22f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 320f, 220f));
            AssertEqual(8, root.Children.Count, "numeric widget count");
            AssertNotNull(root.Find("0.0"), "input int node");
            AssertNotNull(root.Find("0.7"), "slider angle node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode dragNode = root.Find("0.3");
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(dragNode.Rect.X + 2f, dragNode.Rect.Y + 2f)
            }, root);
            AssertEqual("0.3", result.ActivePath, "numeric drag active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.6",
                ActivePath = "0.3"
            });
            Assert(drawList.Commands.Count >= 36, "numeric paint command count");
            Assert(ContainsText(drawList, "Angle"), "numeric angle label");
        }

        private static void ColorControlsLayoutAndPaint() {
            MuiColor color = new MuiColor(1f, 0.25f, 0.5f, 0.75f);
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new ColorButton("ColorButton", color).Height(22f));
            stack.Add(new ColorEdit3("Edit3", color) {
                DisplayMode = ColorDisplayMode.Byte
            }.Height(22f));
            stack.Add(new ColorEdit4("Edit4", color) {
                DisplayMode = ColorDisplayMode.Hex
            }.Height(22f));
            stack.Add(new ColorPicker3("Picker3", color) {
                DisplayMode = ColorDisplayMode.Float
            }.Height(92f));
            stack.Add(new ColorPicker4("Picker4", color) {
                DisplayMode = ColorDisplayMode.Hex
            }.Height(116f));
            stack.Add(new ColorEdit(color) {
                DisplayMode = ColorDisplayMode.Byte
            }.Height(22f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 340f, 310f));
            AssertEqual(6, root.Children.Count, "color widget count");
            AssertNotNull(root.Find("0.0"), "color button node");
            AssertNotNull(root.Find("0.1"), "color edit3 node");
            AssertNotNull(root.Find("0.2"), "color edit4 node");
            AssertNotNull(root.Find("0.3"), "color picker3 node");
            AssertNotNull(root.Find("0.4"), "color picker4 node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode picker = root.Find("0.4");
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(picker.Rect.X + 2f, picker.Rect.Y + 2f)
            }, root);
            AssertEqual("0.4", result.ActivePath, "color picker active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.1",
                ActivePath = "0.4"
            });
            Assert(drawList.Commands.Count >= 48, "color paint command count");
            Assert(ContainsText(drawList, "ColorButton"), "color button label");
            Assert(ContainsText(drawList, "Picker4"), "color picker label");
        }

        private static void SearchableSelectionControlsLayoutAndPaint() {
            string[] items = { "Alpha", "Beta", "Gamma", "Delta" };
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new SearchableComboBox<string>("Search", "Beta", items, delegate(string value) {
                return value;
            }).Height(22f));
            stack.Add(new SearchableListBox<string>("Gamma", items, delegate(string value) {
                return value;
            }) {
                FilterText = "a",
                VisibleItems = 3
            }.Height(92f));
            stack.Add(new Selectable("Selectable", false) {
                Value = "Gamma"
            }.Height(22f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 280f, 150f));
            AssertEqual(3, root.Children.Count, "searchable selection count");
            AssertNotNull(root.Find("0.0"), "searchable combo node");
            AssertNotNull(root.Find("0.1"), "searchable list node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode combo = root.Find("0.0");
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(combo.Rect.X + 2f, combo.Rect.Y + 2f)
            }, root);
            AssertEqual("0.0", result.ActivePath, "searchable combo active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.1"
            });
            Assert(drawList.Commands.Count >= 16, "searchable selection paint command count");
            Assert(ContainsText(drawList, "Search"), "searchable combo label");
            Assert(ContainsText(drawList, "a"), "searchable list filter text");
        }

        private static void TreeHeaderControlsLayoutAndPaint() {
            VStack stack = new VStack(4f).ExpandHeight();
            CollapsingHeader header = new CollapsingHeader("Header", true);
            header.Add(new Label("Header child").Height(18f));
            stack.Add(header.Height(42f));

            ClosableCollapsingHeader closable = new ClosableCollapsingHeader("Closable", true) {
                OnClose = delegate { }
            };
            closable.Add(new Label("Closable child").Height(18f));
            stack.Add(closable.Height(42f));

            TreeNode treeNode = new TreeNode("Root", true) {
                OpenOnArrow = true,
                OpenOnDoubleClick = true,
                DrawLines = true,
                Framed = true,
                Selected = true
            };
            treeNode.Add(new TreeLeaf("Leaf") {
                Bullet = true,
                Selected = true
            }.Height(20f));
            stack.Add(treeNode.Height(44f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 300f, 150f));
            AssertEqual(3, root.Children.Count, "tree header widget count");
            AssertNotNull(root.Find("0.0.0"), "collapsing child node");
            AssertNotNull(root.Find("0.1.0"), "closable child node");
            AssertNotNull(root.Find("0.2.0"), "tree leaf node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode tree = root.Find("0.2");
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(tree.Rect.X + 2f, tree.Rect.Y + 2f)
            }, root);
            AssertEqual("0.2", result.ActivePath, "tree node active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.0",
                ActivePath = "0.2"
            });
            Assert(drawList.Commands.Count >= 18, "tree header paint command count");
            Assert(ContainsText(drawList, "v Header"), "collapsing header text");
            Assert(ContainsText(drawList, "* Leaf"), "tree leaf bullet text");
            Assert(ContainsCommand(drawList, DrawCommandType.Line), "tree hierarchy line");
        }

        private static void MenuBarLayoutAndPaint() {
            MenuBar menuBar = new MenuBar {
                new Menu("File") {
                    new MenuItem("Open"),
                    new MenuSeparator(),
                    new MenuItem("Exit")
                },
                new Menu("View") {
                    new MenuItem("Controls") { Checked = true },
                    new MenuItem("Layout")
                }
            };

            LayoutNode root = new LayoutEngine().Layout(menuBar, new MuiRect(0f, 0f, 300f, 24f));
            AssertEqual(300f, root.Rect.Width, "menubar width");
            AssertEqual(24f, root.Rect.Height, "menubar height");

            InputDispatcher dispatcher = new InputDispatcher();
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(4f, 4f)
            }, root);
            AssertEqual("0", result.ActivePath, "menubar active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 4, "menubar paint command count");
        }

        private static void TabsLayoutAndPaintAdvancedHeaders() {
            Tabs tabs = new Tabs(1) {
                Reorderable = true,
                CloseWithMiddleMouse = true,
                OnClose = delegate(int index) { },
                OnReorder = delegate(int from, int to) { }
            };
            tabs.Add(new LeadingTabButton("+") {
                ButtonWidth = 24f
            });
            tabs.Add(new Tab("General") {
                Unsaved = true
            }.Content(new Label("General content").Height(20f)));
            tabs.Add(new Tab("Debug") {
                Closable = true
            }.Content(new Button("Dump").Height(22f)));
            tabs.Add(new Tab("Hidden") {
                Visible = false
            }.Content(new Label("Hidden content").Height(20f)));
            tabs.Add(new TrailingTabButton("Log") {
                ButtonWidth = 40f
            });

            LayoutNode root = new LayoutEngine().Layout(tabs, new MuiRect(0f, 0f, 320f, 90f));
            AssertEqual(1, root.Children.Count, "tabs selected content count");
            AssertNotNull(root.Find("0.2"), "selected tab source path");
            AssertNotNull(root.Find("0.2.0"), "selected tab child path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(ContainsText(drawList, "+"), "leading tab button text");
            Assert(ContainsText(drawList, "General*"), "unsaved tab marker text");
            Assert(ContainsText(drawList, "Debug  x"), "closable tab marker text");
            Assert(ContainsText(drawList, "Log"), "trailing tab button text");
        }

        private static void MediaAndPlotLayoutAndPaint() {
            object texture = new object();
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new Image(texture).Height(32f).Width(48f));
            stack.Add(new ImageButton(texture).Height(24f));
            stack.Add(new PlotLines(new[] { 0.1f, 0.5f, 0.2f, 0.9f }) {
                OverlayText = "Lines"
            }.Height(40f));
            stack.Add(new PlotHistogram(new[] { 0.2f, 0.6f, 0.4f, 1.0f }) {
                OverlayText = "Histogram"
            }.Height(40f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 240f, 150f));
            AssertNotNull(root.Find("0.0"), "image node");
            AssertNotNull(root.Find("0.1"), "image button node");
            AssertNotNull(root.Find("0.2"), "plot lines node");
            AssertNotNull(root.Find("0.3"), "plot histogram node");

            InputDispatcher dispatcher = new InputDispatcher();
            LayoutNode imageButton = root.Find("0.1");
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(imageButton.Rect.X + 2f, imageButton.Rect.Y + 2f)
            }, root);
            AssertEqual("0.1", result.ActivePath, "image button active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 18, "media plot paint command count");
            Assert(ContainsCommand(drawList, DrawCommandType.Image), "image command recorded");
        }

        private static void CustomDrawWidgetsLayoutAndPaint() {
            DrawList source = new DrawList();
            source.Line(new MuiVec2(1f, 2f), new MuiVec2(20f, 22f), MuiColor.FromRgb(1f, 0f, 0f), 2f);
            source.Polyline(new[] {
                new MuiVec2(0f, 0f),
                new MuiVec2(8f, 12f),
                new MuiVec2(16f, 4f)
            }, MuiColor.FromRgb(0f, 0f, 1f), 1f);
            source.FillTriangle(new MuiVec2(2f, 30f), new MuiVec2(24f, 30f), new MuiVec2(12f, 12f), MuiColor.FromRgb(0f, 1f, 0f));

            DrawList appended = new DrawList();
            appended.Append(source, new MuiVec2(10f, 20f));
            AssertEqual(3, appended.Commands.Count, "draw append count");
            AssertEqual(11f, appended.Commands[0].P1.X, "draw append p1 x");
            AssertEqual(22f, appended.Commands[0].P1.Y, "draw append p1 y");

            DrawCanvas canvas = new DrawCanvas(delegate(DrawList drawList, MuiRect rect) {
                drawList.StrokeRect(rect, MuiColor.FromRgb(0f, 0f, 0f), 1f);
                drawList.StrokeCircle(new MuiVec2(rect.X + 30f, rect.Y + 24f), 12f, MuiColor.FromRgb(0f, 0f, 1f), 2f, 16);
                drawList.Bezier(
                    new MuiVec2(rect.X + 4f, rect.Bottom - 4f),
                    new MuiVec2(rect.X + 24f, rect.Y + 4f),
                    new MuiVec2(rect.Right - 24f, rect.Bottom - 4f),
                    new MuiVec2(rect.Right - 4f, rect.Y + 4f),
                    MuiColor.FromRgb(1f, 0f, 0f),
                    2f,
                    12);
            }) {
                CanvasWidth = 120f,
                CanvasHeight = 60f
            };

            HStack stack = new HStack(4f).ExpandWidth();
            stack.Add(canvas.Width(120f).Height(60f));
            stack.Add(new DrawListWidget(source) {
                CanvasWidth = 80f,
                CanvasHeight = 60f
            }.Width(80f).Height(60f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 220f, 70f));
            AssertEqual(2, root.Children.Count, "custom draw widget count");
            AssertNotNull(root.Find("0.0"), "draw canvas node");
            AssertNotNull(root.Find("0.1"), "draw list widget node");

            DrawList drawCommands = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(ContainsCommand(drawCommands, DrawCommandType.StrokeCircle), "draw canvas circle command");
            Assert(ContainsCommand(drawCommands, DrawCommandType.Bezier), "draw canvas bezier command");
            Assert(ContainsCommand(drawCommands, DrawCommandType.Polyline), "draw list polyline command");
        }

        private static void VirtualListLayoutAndPaint() {
            VirtualList list = new VirtualList(1000, 22f, delegate(int index) {
                return new HStack(4f) {
                    new Label("#" + index.ToString()).Width(48f).Height(22f),
                    new Button("Select").Height(22f).ExpandWidth()
                };
            }) {
                Height = 88f
            };

            LayoutNode root = new LayoutEngine().Layout(list, new MuiRect(0f, 0f, 260f, 88f));
            AssertEqual(260f, root.Rect.Width, "virtual list width");
            AssertEqual(88f, root.Rect.Height, "virtual list height");
            AssertEqual(0, root.Children.Count, "virtual list has no eager children");

            InputDispatcher dispatcher = new InputDispatcher();
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(4f, 4f)
            }, root);
            AssertEqual("0", result.ActivePath, "virtual list active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 3, "virtual list paint command count");
        }

        private static void TwoPaneLayoutArrangesPanes() {
            TwoPaneLayout layout = new TwoPaneLayout(0.25f) {
                SplitterSize = 6f,
                MinFirst = 20f,
                MinSecond = 20f
            };
            layout.Add(new Panel { new Label("Left").Height(20f) });
            layout.Add(new Panel { new Label("Right").Height(20f) });

            LayoutNode root = new LayoutEngine().Layout(layout, new MuiRect(0f, 0f, 206f, 80f));
            LayoutNode left = root.Find("0.0");
            LayoutNode right = root.Find("0.1");
            AssertNotNull(left, "left pane");
            AssertNotNull(right, "right pane");
            AssertEqual(50f, left.Rect.Width, "left pane width");
            AssertEqual(56f, right.Rect.X, "right pane x");
            AssertEqual(150f, right.Rect.Width, "right pane width");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 5, "two pane paint command count");
        }

        private static void DragDropLayoutAndPaint() {
            DragPayload payload = new DragPayload("test", 42, "payload");
            HStack stack = new HStack(4f).ExpandWidth();
            stack.Add(new DragSource(payload) {
                new Label("Source").Height(20f)
            }.Width(100f).Height(34f));
            stack.Add(new DropTarget("test") {
                EmptyText = "Drop here"
            }.Height(34f).ExpandWidth());

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 260f, 40f));
            LayoutNode source = root.Find("0.0");
            LayoutNode target = root.Find("0.1");
            AssertNotNull(source, "drag source node");
            AssertNotNull(target, "drop target node");
            Assert(source.Widget is DragSource, "drag source widget");
            Assert(target.Widget is DropTarget, "drop target widget");

            InputDispatcher dispatcher = new InputDispatcher();
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(source.Rect.X + 2f, source.Rect.Y + 2f)
            }, root);
            AssertEqual("0.0", result.ActivePath, "drag source active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.1",
                ActivePath = "0.0"
            });
            Assert(drawList.Commands.Count >= 5, "drag drop paint command count");
            Assert(ContainsCommand(drawList, DrawCommandType.StrokeRect), "drag drop stroke command");
            AssertEqual("test", payload.Type, "payload type");
            AssertEqual("payload", payload.PreviewText, "payload preview");
        }

        private static void TableColumnsLayoutAndPaint() {
            Table table = new Table(4);
            table.AlternatingRows = true;
            table.Add(new TableColumn("Name") {
                Width = 80f,
                MinWidth = 70f,
                Stretch = false,
                Sortable = true,
                SortDirection = TableSortDirection.Ascending
            });
            table.Add(new TableColumn("Hidden") {
                Visible = false
            });
            table.Add(new TableColumn("Value") {
                MaxWidth = 120f
            });
            table.Add(new TableColumn("Action") {
                Width = 60f,
                Stretch = false
            });
            table.Add(new TableRow {
                new TableCell { new Label("Alpha").Height(20f) },
                new TableCell { new Label("Hidden").Height(20f) },
                new TableCell { new ProgressBar(0.5f).Height(20f) },
                new TableCell { new Button("Run").Height(20f) }
            });

            LayoutNode root = new LayoutEngine().Layout(table, new MuiRect(0f, 0f, 300f, 70f));
            AssertEqual(1, root.Children.Count, "table data row count");

            LayoutNode row = root.Find("0.4");
            AssertNotNull(row, "table row path preserves source index");
            AssertEqual(23f, row.Rect.Y, "table row y after auto header");

            LayoutNode firstCell = root.Find("0.4.0");
            LayoutNode hiddenCell = root.Find("0.4.1");
            LayoutNode secondCell = root.Find("0.4.2");
            LayoutNode thirdCell = root.Find("0.4.3");
            AssertNotNull(firstCell, "first table cell");
            AssertNotNull(hiddenCell, "hidden table cell");
            AssertNotNull(secondCell, "second table cell");
            AssertNotNull(thirdCell, "third table cell");
            AssertEqual(80f, firstCell.Rect.Width, "fixed first column width");
            AssertEqual(0f, hiddenCell.Rect.Width, "hidden column width");
            AssertEqual(81f, secondCell.Rect.X, "visible column shifts over hidden column");
            AssertEqual(120f, secondCell.Rect.Width, "stretch column max width");
            AssertEqual(202f, thirdCell.Rect.X, "third column x after hidden column");
            AssertEqual(60f, thirdCell.Rect.Width, "fixed third column width");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 12, "table columns paint command count");
        }

        private static void SelectionStateAppliesSingleToggleAndRange() {
            string[] items = new[] { "A", "B", "C", "D", "E", "F" };
            SelectionState<string> selection = new SelectionState<string>();

            selection.Apply(items, "B", false, false);
            AssertEqual(1, selection.Count, "single select count");
            Assert(selection.IsSelected("B"), "single selected item");
            Assert(!selection.IsSelected("A"), "single clears other items");

            selection.Apply(items, "D", true, false);
            AssertEqual(2, selection.Count, "ctrl toggle count");
            Assert(selection.IsSelected("B"), "ctrl keeps previous item");
            Assert(selection.IsSelected("D"), "ctrl adds new item");

            selection.Apply(items, "F", false, true);
            AssertEqual(3, selection.Count, "shift range count");
            Assert(!selection.IsSelected("B"), "range replaces previous selection");
            Assert(selection.IsSelected("D"), "range includes anchor");
            Assert(selection.IsSelected("E"), "range includes middle item");
            Assert(selection.IsSelected("F"), "range includes clicked item");

            selection.Apply(items, "E", true, false);
            AssertEqual(2, selection.Count, "ctrl toggle removes selected item");
            Assert(!selection.IsSelected("E"), "selected item removed");

            SelectionRequest request = new SelectionRequest {
                Value = "F",
                Selected = true,
                Additive = true,
                Range = false,
                DoubleClick = true
            };
            AssertEqual("F", request.Value.ToString(), "selection request value");
            Assert(request.Additive, "selection request additive");
            Assert(request.DoubleClick, "selection request double click");
        }

        private static void DebugToolsLayoutAndPaint() {
            DebugLog.Clear();
            DebugLog.Info("hello");
            DebugLog.Warning("careful");

            UiStateStore store = new UiStateStore();
            store.GetOrCreate<TestDebugState>("debug");

            LayoutNode sampleLayout = new LayoutEngine().Layout(
                new VStack(2f) {
                    new Label("Sample").Height(20f),
                    new Button("Action").Height(22f)
                },
                new MuiRect(0f, 0f, 160f, 60f));
            DrawList sampleDrawList = new WidgetPainter().Paint(sampleLayout, new WidgetPaintState());
            UiQueryStore query = new UiQueryStore();
            query.RecordLayout(sampleLayout, new InputDispatchResult {
                HoveredPath = "0.1",
                ActivePath = "0.1",
                ClickedPath = "0.1"
            });

            DemoGallery gallery = new DemoGallery();
            gallery.Entries.Add(new DemoGalleryEntry("Controls", "Buttons", "implemented"));
            gallery.Entries.Add(new DemoGalleryEntry("Debug", "Item picker", "implemented"));

            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new MetricsWindow(new RuntimeMetrics {
                FrameCount = 3,
                MountCount = 2,
                VisibleMountCount = 1,
                PendingCallbackCount = 4
            }).Height(110f).ExpandWidth());
            stack.Add(new DebugLogWindow(DebugLog.Snapshot()).Height(90f).ExpandWidth());
            stack.Add(new StateStoreInspector(store).Height(90f).ExpandWidth());
            stack.Add(new LayoutDebugger(sampleLayout).Height(90f).ExpandWidth());
            stack.Add(new DrawListDebugger(sampleDrawList).Height(90f).ExpandWidth());
            stack.Add(new IDStackTool(query).Height(90f).ExpandWidth());
            stack.Add(new ItemPicker(query).Height(90f).ExpandWidth());
            stack.Add(new AboutWindow().Height(90f).ExpandWidth());
            stack.Add(new UserGuideWindow().Height(90f).ExpandWidth());
            stack.Add(gallery.Height(90f).ExpandWidth());

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 320f, 840f));
            AssertEqual(10, root.Children.Count, "debug widget count");
            AssertNotNull(root.Find("0.0"), "metrics node");
            AssertNotNull(root.Find("0.1"), "debug log node");
            AssertNotNull(root.Find("0.2"), "state store node");
            AssertNotNull(root.Find("0.3"), "layout debugger node");
            AssertNotNull(root.Find("0.4"), "draw list debugger node");
            AssertNotNull(root.Find("0.5"), "id stack node");
            AssertNotNull(root.Find("0.6"), "item picker node");
            AssertNotNull(root.Find("0.7"), "about node");
            AssertNotNull(root.Find("0.8"), "user guide node");
            AssertNotNull(root.Find("0.9"), "demo gallery node");
            AssertEqual(1, store.Count, "state store count");
            AssertEqual(2, DebugLog.Snapshot().Count, "debug log count");
            Assert(DebugLog.LogToMemory().IndexOf("[Info] hello", StringComparison.Ordinal) >= 0, "debug log memory export");
            string tempPath = Path.Combine(Path.GetTempPath(), "MaximumUI.Tests.DebugLog.txt");
            DebugLog.LogToFile(tempPath);
            Assert(File.ReadAllText(tempPath).IndexOf("[Warning] careful", StringComparison.Ordinal) >= 0, "debug log file export");
            File.Delete(tempPath);

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 60, "debug tools paint command count");
        }

        private sealed class TestDebugState {
            public int Value = 1;
        }

        private static void ThemeEditorLayoutAndPaint() {
            ThemeTokens tokens = new ThemeTokens();
            tokens.SetColor(ThemeColorToken.Accent, new MuiColor(0.2f, 0.3f, 0.4f, 0.5f));
            tokens.SetScalar(ThemeScalarToken.FontSize, 14f);

            ThemeTokens clone = tokens.Clone();
            AssertEqual(0.2f, clone.GetColor(ThemeColorToken.Accent).R, "theme clone color");
            AssertEqual(14f, clone.GetScalar(ThemeScalarToken.FontSize), "theme clone scalar");
            clone.SetScalar(ThemeScalarToken.FontSize, 16f);
            AssertEqual(14f, tokens.GetScalar(ThemeScalarToken.FontSize), "theme clone independent scalar");

            ThemeEditor editor = new ThemeEditor(tokens) {
                MaxRows = 6
            };
            LayoutNode root = new LayoutEngine().Layout(editor, new MuiRect(0f, 0f, 300f, 180f));
            AssertEqual(300f, root.Rect.Width, "theme editor width");
            AssertEqual(160f, root.DesiredSize.Y, "theme editor desired visible height");

            InputDispatcher dispatcher = new InputDispatcher();
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(4f, 4f)
            }, root);
            AssertEqual("0", result.ActivePath, "theme editor active path");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 20, "theme editor paint command count");

            FontAtlasViewer atlas = new FontAtlasViewer {
                FontName = "Default",
                TextureWidth = 256,
                TextureHeight = 256,
                GlyphCount = 96
            };
            FallbackGlyphDiagnostics glyphs = new FallbackGlyphDiagnostics {
                FontName = "Default",
                SampleText = "ABC"
            };
            glyphs.MissingGlyphs.Add('?');

            VStack styleStack = new VStack(4f).ExpandHeight();
            styleStack.Add(new FontSelector("Font", "Default", new[] { "Default", "Mono" }).Height(22f));
            styleStack.Add(new FontSizeSelector("Size", 12f, new[] { 10f, 12f, 14f }).Height(22f));
            styleStack.Add(new StyleSelector("Style", "Default", new[] { "Default", "Compact" }).Height(22f));
            styleStack.Add(new ThemeSelector("Theme", "Expression Light", new[] { "Expression Light", "Classic" }).Height(22f));
            styleStack.Add(atlas.Height(120f));
            styleStack.Add(glyphs.Height(100f));

            LayoutNode styleRoot = new LayoutEngine().Layout(styleStack, new MuiRect(0f, 0f, 320f, 320f));
            AssertEqual(6, styleRoot.Children.Count, "font style widget count");
            AssertNotNull(styleRoot.Find("0.0"), "font selector node");
            AssertNotNull(styleRoot.Find("0.1"), "font size selector node");
            AssertNotNull(styleRoot.Find("0.2"), "style selector node");
            AssertNotNull(styleRoot.Find("0.3"), "theme selector node");
            AssertNotNull(styleRoot.Find("0.4"), "font atlas node");
            AssertNotNull(styleRoot.Find("0.5"), "fallback glyph node");

            DrawList styleDrawList = new WidgetPainter().Paint(styleRoot, new WidgetPaintState());
            Assert(styleDrawList.Commands.Count >= 24, "font style paint command count");
        }

        private static void QueryStoreRecordsItemsWindowsAndCapture() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new Button("One", key: "one").Height(22f));
            stack.Add(new Button("Two", key: "two").Height(22f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 200f, 80f));
            LayoutNode two = root.Find("0.1");
            AssertNotNull(two, "query target node");

            InputDispatcher dispatcher = new InputDispatcher();
            dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseDown,
                MouseButton = 0,
                MousePosition = new MuiVec2(two.Rect.X + 2f, two.Rect.Y + 2f)
            }, root);
            InputDispatchResult result = dispatcher.Dispatch(new MuiInputEvent {
                Type = MuiEventType.MouseUp,
                MouseButton = 0,
                MousePosition = new MuiVec2(two.Rect.X + 2f, two.Rect.Y + 2f)
            }, root);

            UiQueryStore query = new UiQueryStore();
            query.BeginFrame();
            query.RecordInput(new MuiInputEvent {
                Type = MuiEventType.KeyDown,
                KeyCode = 65,
                Control = true,
                Shift = true,
                MousePosition = new MuiVec2(12f, 34f)
            });
            query.RecordLayout(root, result);
            query.SetCapture(true, true, false);

            AssertEqual(3, query.ItemCount, "query item count includes root");
            Assert(query.IsHovered("two"), "query hovered by key");
            Assert(query.IsClicked("two"), "query clicked by key");
            Assert(!query.IsClicked("one"), "query other item not clicked");

            ItemQuery item = query.GetItem("two");
            AssertNotNull(item, "query item lookup");
            AssertEqual("key:two", item.Id, "query item id");
            AssertEqual("Button", item.TypeName, "query item type");

            Window window = new Window("Query", width: 200f, height: 120f, key: "window-query");
            query.RecordWindow(
                window,
                "0",
                new MuiRect(5f, 6f, 200f, 120f),
                new MuiRect(5f, 6f, 200f, 24f),
                true,
                false,
                true,
                false);
            Assert(query.IsWindowHovered("window-query"), "window query hovered");

            BuildContext context = new BuildContext {
                Query = query
            };
            Assert(context.IsHovered("two"), "build context hover wrapper");
            Assert(context.IsClicked("two"), "build context click wrapper");
            Assert(context.IsWindowHovered("window-query"), "build context window wrapper");
            Assert(query.Capture.WantCaptureMouse, "capture mouse");
            Assert(query.Capture.WantCaptureKeyboard, "capture keyboard");
            Assert(query.Capture.Control, "capture ctrl");
            Assert(query.Capture.Shift, "capture shift");

            LayoutNode debugRoot = new LayoutEngine().Layout(new InputDebugger(query).Height(160f), new MuiRect(0f, 0f, 240f, 160f));
            DrawList drawList = new WidgetPainter().Paint(debugRoot, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 8, "input debugger paint command count");
        }

        private static void ShortcutRouterRegistersAndResolves() {
            UiShortcutRouter router = new UiShortcutRouter();
            int primary = 0;
            int overrideValue = 0;
            int textAllowed = 0;

            router.BeginBuild();
            router.Register("primary", new ShortcutChord(65, control: true), delegate { primary++; });
            router.Register("override", new ShortcutChord(65, control: true), delegate { overrideValue++; });
            router.Register("text", new ShortcutChord(66, control: true), delegate { textAllowed++; }, allowWhenTextInput: true);

            AssertEqual(3, router.Count, "shortcut count");
            ShortcutBinding binding = router.Resolve(new MuiInputEvent {
                Type = MuiEventType.KeyDown,
                KeyCode = 65,
                Control = true
            }, false);
            AssertNotNull(binding, "shortcut binding");
            AssertEqual("override", binding.Id, "last registered shortcut wins");
            binding.Action();
            router.MarkTriggered(binding);
            AssertEqual(0, primary, "primary shortcut shadowed");
            AssertEqual(1, overrideValue, "override shortcut triggered");
            AssertEqual("override", router.LastTriggeredId, "last triggered id");
            AssertEqual(1, router.TriggerCount, "shortcut trigger count");

            ShortcutBinding blocked = router.Resolve(new MuiInputEvent {
                Type = MuiEventType.KeyDown,
                KeyCode = 65,
                Control = true
            }, true);
            Assert(blocked == null, "shortcut blocked by text input");

            ShortcutBinding allowed = router.Resolve(new MuiInputEvent {
                Type = MuiEventType.KeyDown,
                KeyCode = 66,
                Control = true
            }, true);
            AssertNotNull(allowed, "shortcut allowed in text input");
            allowed.Action();
            AssertEqual(1, textAllowed, "text input allowed shortcut triggered");

            BuildContext context = new BuildContext {
                Shortcuts = new UiShortcutRouter()
            };
            context.Shortcuts.BeginBuild();
            ShortcutBinding registered = context.RegisterShortcut("context", new ShortcutChord(67), delegate { primary++; });
            AssertNotNull(registered, "context shortcut registered");
            AssertEqual(1, context.Shortcuts.Count, "context shortcut count");

            DrawList drawList = new WidgetPainter().Paint(
                new LayoutEngine().Layout(new InputDebugger(new UiQueryStore()) {
                    Navigation = new UiNavigationStore(),
                    Shortcuts = router
                }.Height(160f), new MuiRect(0f, 0f, 240f, 160f)),
                new WidgetPaintState());
            Assert(drawList.Commands.Count >= 8, "shortcut debugger paint command count");
        }

        private static void NavigationStoreFocusesDefaultRequestedAndSequentialItems() {
            Button first = new Button("First", key: "first").Height(22f);
            Button second = new Button("Second", key: "second").Height(22f).DefaultFocus();
            Button third = new Button("Third", key: "third").Height(22f);
            Button ignored = new Button("Ignored", key: "ignored").Height(22f).Focusable(false);

            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(first);
            stack.Add(second);
            stack.Add(third);
            stack.Add(ignored);

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 240f, 120f));
            UiNavigationStore navigation = new UiNavigationStore();
            navigation.BeginFrame();
            RegisterNavigationForTest(navigation, root);
            navigation.EndFrame();

            AssertEqual(3, navigation.Count, "navigation focusable count");
            AssertEqual("key:second", navigation.FocusedId, "default focused id");
            Assert(navigation.FocusNext(), "focus next");
            AssertEqual("key:third", navigation.FocusedId, "focus next id");
            Assert(navigation.FocusNext(), "focus wraps next");
            AssertEqual("key:first", navigation.FocusedId, "focus wrap id");
            Assert(navigation.FocusPrevious(), "focus previous wraps");
            AssertEqual("key:third", navigation.FocusedId, "focus previous id");
            Assert(navigation.Focus("first"), "focus by key");
            AssertEqual("key:first", navigation.FocusedId, "focus key id");

            third.FocusHere();
            navigation.BeginFrame();
            RegisterNavigationForTest(navigation, root);
            navigation.EndFrame();
            AssertEqual("key:third", navigation.FocusedId, "requested focus id");
            NavigationItem focused = navigation.FocusedItem();
            AssertNotNull(focused, "focused item");
            AssertEqual("Button", focused.TypeName, "focused item type");

            BuildContext context = new BuildContext {
                Navigation = navigation
            };
            Assert(context.Focus("second"), "context focus by key");
            AssertEqual("key:second", navigation.FocusedId, "context focus result");

            UiQueryStore query = new UiQueryStore();
            query.BeginFrame();
            query.RecordLayout(root, new InputDispatchResult {
                FocusedPath = "0.1"
            });
            query.SetNavigation(navigation.HasFocus, navigation.Count > 0);
            Assert(query.Capture.NavActive, "query nav active");
            Assert(query.Capture.NavVisible, "query nav visible");

            DrawList drawList = new WidgetPainter().Paint(
                new LayoutEngine().Layout(new InputDebugger(query) {
                    Navigation = navigation
                }.Height(160f), new MuiRect(0f, 0f, 240f, 160f)),
                new WidgetPaintState());
            Assert(drawList.Commands.Count >= 8, "navigation debugger paint command count");
        }

        private static void RegisterNavigationForTest(UiNavigationStore navigation, LayoutNode node) {
            if (node == null || node.Widget == null) return;

            if (IsFocusableForTest(node.Widget)) {
                navigation.Register(node.Widget, node.Path, node.Rect, node.Widget.Enabled);
            }

            for (int i = 0; i < node.Children.Count; i++) {
                RegisterNavigationForTest(navigation, node.Children[i]);
            }
        }

        private static bool IsFocusableForTest(Widget widget) {
            return widget != null && widget.Focusable && widget.Enabled && widget is Button;
        }

        private static void AdvancedControlsLayoutAndPaint() {
            VStack stack = new VStack(4f).ExpandHeight();
            stack.Add(new RadioButton("Choice", true).Height(20f));
            stack.Add(new ProgressBar(0.5f).Height(16f));
            stack.Add(new TextArea("line 1\nline 2").Height(44f));
            stack.Add(new PasswordField("secret").Height(20f));
            stack.Add(new SearchBox("needle").Height(20f));
            stack.Add(new ListBox<string>("B", new[] { "A", "B", "C" }).Height(60f));
            stack.Add(new Selectable("Row", true).Height(20f));
            stack.Add(new TreeNode("Node", true) {
                new Label("Child").Height(18f)
            }.Height(42f));
            stack.Add(new ColorEdit(new MuiColor(1f, 0.25f, 0.25f, 1f)).Height(20f));
            stack.Add(new VectorField("Vec", 1f, 2f, 3f).Height(20f));

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 320f, 220f));
            AssertNotNull(root.Find("0.0"), "radio node");
            AssertNotNull(root.Find("0.1"), "progress node");
            AssertNotNull(root.Find("0.2"), "textarea node");
            AssertNotNull(root.Find("0.3"), "password node");
            AssertNotNull(root.Find("0.4"), "search node");
            AssertNotNull(root.Find("0.5"), "listbox node");
            AssertNotNull(root.Find("0.6"), "selectable node");
            AssertNotNull(root.Find("0.7"), "tree node");
            AssertNotNull(root.Find("0.8"), "color node");
            AssertNotNull(root.Find("0.9"), "vector node");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState {
                HoveredPath = "0.6"
            });
            Assert(drawList.Commands.Count >= 28, "advanced paint command count");
        }

        private static void GridAndTableLayoutAndPaint() {
            VStack stack = new VStack(6f).ExpandHeight();
            stack.Add(new Grid(3, 4f, 4f) {
                new Button("One").Height(20f),
                new Button("Two").Height(20f),
                new Button("Three").Height(20f),
                new Toggle("Four", true).Height(20f),
                new Selectable("Five", false).Height(20f),
                new ProgressBar(0.25f).Height(20f)
            }.Height(44f).ExpandWidth());
            stack.Add(new Table(3) {
                new TableRow {
                    new TableCell { new Label("Name").Height(18f) },
                    new TableCell { new Label("Value").Height(18f) },
                    new TableCell { new Label("Action").Height(18f) }
                },
                new TableRow {
                    new TableCell { new Label("Alpha").Height(20f) },
                    new TableCell { new ProgressBar(0.75f).Height(20f) },
                    new TableCell { new Button("Run").Height(20f) }
                }
            }.Height(42f).ExpandWidth());

            LayoutNode root = new LayoutEngine().Layout(stack, new MuiRect(0f, 0f, 360f, 120f));
            LayoutNode grid = root.Find("0.0");
            LayoutNode table = root.Find("0.1");
            AssertNotNull(grid, "grid node");
            AssertNotNull(table, "table node");
            AssertEqual(6, grid.Children.Count, "grid child count");
            AssertEqual(2, table.Children.Count, "table row count");

            LayoutNode gridFirst = root.Find("0.0.0");
            LayoutNode gridSecond = root.Find("0.0.1");
            LayoutNode secondRowFirst = root.Find("0.0.3");
            AssertNotNull(gridFirst, "grid first");
            AssertNotNull(gridSecond, "grid second");
            AssertNotNull(secondRowFirst, "grid second row first");
            Assert(gridSecond.Rect.X > gridFirst.Rect.X, "grid columns advance");
            Assert(secondRowFirst.Rect.Y > gridFirst.Rect.Y, "grid rows advance");

            LayoutNode firstCell = root.Find("0.1.0.0");
            LayoutNode secondCell = root.Find("0.1.0.1");
            AssertNotNull(firstCell, "table first cell");
            AssertNotNull(secondCell, "table second cell");
            Assert(secondCell.Rect.X > firstCell.Rect.X, "table columns advance");

            DrawList drawList = new WidgetPainter().Paint(root, new WidgetPaintState());
            Assert(drawList.Commands.Count >= 24, "grid table paint command count");
        }

        private static void Assert(bool condition, string label) {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertNotNull(object value, string label) {
            Assert(value != null, label);
        }

        private static void AssertEqual(float expected, float actual, string label) {
            if (Math.Abs(expected - actual) > 0.001f) {
                throw new InvalidOperationException(label + ": expected " + expected + ", actual " + actual);
            }
        }

        private static void AssertEqual(int expected, int actual, string label) {
            if (expected != actual) {
                throw new InvalidOperationException(label + ": expected " + expected + ", actual " + actual);
            }
        }

        private static void AssertEqual(string expected, string actual, string label) {
            if (!string.Equals(expected, actual, StringComparison.Ordinal)) {
                throw new InvalidOperationException(label + ": expected " + expected + ", actual " + actual);
            }
        }

        private static void AssertEqual(DrawCommandType expected, DrawCommandType actual, string label) {
            if (expected != actual) {
                throw new InvalidOperationException(label + ": expected " + expected + ", actual " + actual);
            }
        }

        private static bool ContainsCommand(DrawList drawList, DrawCommandType type) {
            for (int i = 0; i < drawList.Commands.Count; i++) {
                if (drawList.Commands[i].Type == type) return true;
            }

            return false;
        }

        private static bool ContainsText(DrawList drawList, string text) {
            for (int i = 0; i < drawList.Commands.Count; i++) {
                DrawCommand command = drawList.Commands[i];
                if (command.Type == DrawCommandType.Text && string.Equals(command.Text, text, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
