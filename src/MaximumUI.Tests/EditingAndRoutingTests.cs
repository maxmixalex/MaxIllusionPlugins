using System;
using System.Reflection;
using MaximumUI;
using UnityEngine;

namespace MaximumUI.Tests {
    internal static class EditingAndRoutingTests {
        internal const int TestCount = 13;

        internal static void RunAll() {
            BlurOnlyFieldsCommitTheDraft();
            LiveChangeFieldsKeepTheirLatestDraftOnBlur();
            EnterShowsTheAcceptedValueWhileFocused();
            DeferredCommitsPreserveNewerTyping();
            EnterRebasesEscapeToTheAcceptedValue();
            IdleFieldsTrackExternalValuesWithoutAllocations();
            UnchangedTextFiltersReuseTheirBuffer();
            NestedWheelsFallThroughOnlyAtTheirDirectionalBoundary();
            ScrollbarTargetsNeverOverlapListRows();
            PointerFocusDoesNotEnableParentKeyboardNavigation();
            ActionPressCommitsBeforeReleaseCallbacks();
            EditingAffordancesDoNotBlurOnPress();
            SelectedPanelOutlinesArePaintedAfterChildren();
            Console.WriteLine("MaximumUI editing/scroll routing regressions: " + TestCount + " passed.");
        }

        private static void BlurOnlyFieldsCommitTheDraft() {
            string model = "4";
            TextFieldState state = Focused(model);
            TextEditingLifecycle.Edit(state, "42");
            bool wasFocused = Sync(state, model, false);
            Require(wasFocused && state.EditingValue == "42", "The first event after focus leaves must retain an uncommitted draft.");
            Action commit = TextEditingLifecycle.CommitCallback(state, delegate(string text) { model = text; });
            state.Focused = state.WasFocused = false;
            // Repaint can occur before Update flushes deferred callbacks. The
            // captured submission, including an empty draft, must be immutable.
            Sync(state, model, false);
            commit();
            Sync(state, model, false);
            Require(model == "42" && state.EditingValue == "42", "A field without OnChange must save the draft on blur.");

            state = Focused("previous");
            TextEditingLifecycle.Edit(state, string.Empty);
            Sync(state, "previous", false);
            string submitted = null;
            TextEditingLifecycle.CommitCallback(state, delegate(string text) { submitted = text; })();
            Require(submitted == string.Empty, "Clearing a blur-only field must not resurrect its old text.");
        }

        private static void LiveChangeFieldsKeepTheirLatestDraftOnBlur() {
            TextFieldState state = Focused("1");
            TextEditingLifecycle.Edit(state, "12");
            TextEditingLifecycle.Edit(state, "123");
            // The model still acknowledges an earlier OnChange when focus moves.
            Sync(state, "12", false);
            string submitted = null;
            TextEditingLifecycle.CommitCallback(state, delegate(string text) { submitted = text; })();
            Require(submitted == "123", "A lagging OnChange acknowledgement must not replace the newest blur value.");
        }

        private static void EnterShowsTheAcceptedValueWhileFocused() {
            string model = "5";
            TextFieldState state = Focused(model);
            TextEditingLifecycle.Edit(state, "1000");
            Action commit = TextEditingLifecycle.CommitCallback(state, delegate(string text) {
                model = Math.Min(100, int.Parse(text)).ToString();
            });
            Sync(state, model, true);
            Require(state.EditingValue == "1000", "A queued submission must retain its draft until the model accepts it.");
            commit();
            Sync(state, model, true);
            Require(state.EditingValue == "100", "Enter must show a clamped accepted value without requiring blur.");
        }

        private static void DeferredCommitsPreserveNewerTyping() {
            TextFieldState state = Focused("5");
            TextEditingLifecycle.Edit(state, "10");
            Action commit = TextEditingLifecycle.CommitCallback(state, delegate { });
            TextEditingLifecycle.Edit(state, "101");
            commit();
            Sync(state, "10", true);
            Require(state.EditingValue == "101", "An older deferred commit cannot replace text entered afterwards.");

            Action next = TextEditingLifecycle.CommitCallback(state, delegate { });
            next();
            TextEditingLifecycle.Edit(state, "1012");
            Sync(state, "101", true);
            Require(state.EditingValue == "1012", "Newer text also wins when entered after callback completion but before synchronization.");
        }

        private static void EnterRebasesEscapeToTheAcceptedValue() {
            TextFieldState state = Focused("5");
            state.HasFocusStartValue = true;
            state.FocusStartValue = "5";
            TextEditingLifecycle.Edit(state, "1000");
            TextEditingLifecycle.CommitCallback(state, delegate { })();
            Sync(state, "100", true);
            TextEditingLifecycle.Edit(state, "101");
            Require(state.FocusStartValue == "100", "Escape after a successful Enter must revert to that accepted value, not the pre-submit model.");
        }

        private static void IdleFieldsTrackExternalValuesWithoutAllocations() {
            TextFieldState state = new TextFieldState();
            Sync(state, "old", false);
            Sync(state, "new", false);
            Require(state.EditingValue == "new", "Idle fields continue following externally changed models.");
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (method == null) return;
            Func<long> allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            for (int i = 0; i < 100; i++) Sync(state, "new", false);
            long before = allocated();
            for (int i = 0; i < 10000; i++) Sync(state, "new", false);
            Require(allocated() - before == 0L, "Unchanged idle model synchronization must allocate nothing.");
        }

        private static void UnchangedTextFiltersReuseTheirBuffer() {
            string text = new string(new[] { '1', '2', '.', '5' });
            Require(ReferenceEquals(text, TextInputFilter.Apply(text, TextInputFlags.None)), "An unchanged ordinary field must reuse its existing text.");
            Require(ReferenceEquals(text, TextInputFilter.Apply(text, TextInputFlags.Decimal)), "An already valid numeric draft must not allocate another string.");
            Require(TextInputFilter.Apply("x1 y2z3", TextInputFlags.Decimal, null, 2) == "12", "Filtering and the accepted-character length limit still compose correctly.");
            Require(TextInputFilter.Apply(" abc ", TextInputFlags.NoBlank, null, 2) == "ab", "Removing prefix blanks must not count them toward the length limit.");
        }

        private static void NestedWheelsFallThroughOnlyAtTheirDirectionalBoundary() {
            MuiVec2 maximum = new MuiVec2(0f, 200f);
            MuiVec2 down = new MuiVec2(0f, 1f), up = new MuiVec2(0f, -1f);
            Require(!ScrollWheelRouting.CanConsume(new MuiVec2(0f, 0f), up, maximum), "An inner list at its top must let the enclosing list scroll up.");
            Require(ScrollWheelRouting.CanConsume(new MuiVec2(0f, 80f), up, maximum), "A parent with remaining space can accept that same wheel event.");
            Require(!ScrollWheelRouting.CanConsume(maximum, down, maximum), "An inner list at its bottom must release a downward wheel event.");
            Require(ScrollWheelRouting.CanConsume(maximum, up, maximum), "The reverse direction must immediately return to the inner list.");
            Require(!ScrollWheelRouting.CanConsume(new MuiVec2(), down, new MuiVec2(100f, 0f)), "A vertical wheel must not consume a horizontal-only host's event.");
            Require(ScrollWheelRouting.CanConsume(new MuiVec2(50f, 0f), new MuiVec2(-1f, 0f), new MuiVec2(100f, 0f)), "A horizontal wheel uses its own axis.");
            Require(!ScrollWheelRouting.CanConsume(new MuiVec2(), new MuiVec2(float.NaN, float.PositiveInfinity), maximum), "Non-finite input must not claim the event.");
        }

        private static void ScrollbarTargetsNeverOverlapListRows() {
            ScrollFrameLayout frame = ScrollGeometry.VerticalFrame(new MuiRect(50f, 60f, 240f, 162f), 1000f);
            GuiCoordinates coordinates = new GuiCoordinates();
            coordinates.Reset(1280f, 720f);
            coordinates.PushScroll(frame.ContentViewport, new MuiVec2(0f, 300f), frame.Content);
            MuiRect row = new MuiRect(0f, 320f, frame.Content.Width, 32f);
            // Event coordinates inside native BeginScrollView are content-local,
            // including when a drag starts over the track or leaves its bounds.
            MuiVec2 rail = new MuiVec2(frame.VerticalGutter.X - coordinates.Origin.X + 8f, 332f);
            Require(!coordinates.ContainsLocalPoint(row, rail), "A scrollbar press/drag cannot satisfy a row's production hit test.");
            Require(coordinates.ContainsLocalPoint(row, new MuiVec2(10f, 332f)), "The visible row remains selectable beside the scrollbar.");
            coordinates.Pop();
        }

        private static void PointerFocusDoesNotEnableParentKeyboardNavigation() {
            TextField input = new TextField("edit", key: "edit");
            VirtualList list = new VirtualList(10, 32f, delegate { return new Label("row"); }, "list");
            UiNavigationStore navigation = new UiNavigationStore();
            navigation.BeginFrame();
            navigation.Register(input, "edit", new MuiRect(0f, 0f, 100f, 32f), true);
            navigation.Register(list, "list", new MuiRect(0f, 40f, 100f, 160f), true);
            navigation.EndFrame();
            navigation.Focus("edit");
            navigation.FocusPointer(list, "list");
            Require(!navigation.IsKeyboardFocused(list, "list"), "Pointer focus on a scroll host must not enable its PageUp/PageDown handlers while a native text field still owns keyboard focus.");
            navigation.Focus("list");
            Require(navigation.IsKeyboardFocused(list, "list"), "Explicit keyboard navigation still enables the list's paging commands.");

            SearchableListBox<int> searchable = new SearchableListBox<int>(0, new[] { 0, 1 }, key: "searchable");
            ListBoxState state = new ListBoxState();
            state.FilterWidget.Key = "filter";
            navigation.BeginFrame();
            navigation.Register(searchable, "list", new MuiRect(0f, 0f, 100f, 160f), true);
            navigation.Register(state.FilterWidget, "filter", new MuiRect(0f, 0f, 100f, 32f), true);
            navigation.EndFrame();
            navigation.Focus("filter");
            Require(!navigation.IsKeyboardFocused(searchable, "list") && navigation.IsKeyboardFocused(state.FilterWidget, "filter"),
                "A search filter has its own tab target; text-navigation keys cannot activate the list behind it.");
        }

        private static void SelectedPanelOutlinesArePaintedAfterChildren() {
            ThemeTokens theme = ThemePalettes.MetroLight();
            Panel panel = new Panel { Selected = true };
            panel.Add(new Panel());
            LayoutNode node = new LayoutEngine(ThemeTypography.CreateLayoutMetrics(theme)).Layout(panel, new MuiRect(0f, 0f, 200f, 120f), "panel");
            DrawList drawing = new WidgetPainter(theme).Paint(node, new WidgetPaintState());
            DrawCommand last = drawing.Commands[drawing.Commands.Count - 1];
            Require(last.Type == DrawCommandType.StrokeRect && last.Color.R == theme.Accent.R && last.Color.G == theme.Accent.G && last.Color.B == theme.Accent.B,
                "A selected panel's accent outline must remain visible above all child surfaces.");
        }

        private static void ActionPressCommitsBeforeReleaseCallbacks() {
            foreach (bool buttonBeforeField in new[] { false, true }) {
                string model = "1", saved = null;
                TextFieldState state = Focused(model);
                TextEditingLifecycle.Edit(state, "42");
                DeferredActions queue = new DeferredActions();
                Require(GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDown, 0, true, true, false),
                    "An actual enabled command press must release native text focus before its MouseUp action.");
                // If the field precedes the button it observes this transition on
                // the following MouseUp pass; otherwise it observes MouseDown.
                EventType observedBlur = buttonBeforeField ? EventType.MouseDown : EventType.MouseUp;
                Require(Sync(state, model, false), "The field must retain its pending focus-loss transition.");
                queue.Add(TextEditingLifecycle.CommitCallback(state, delegate(string text) { model = text; }));
                state.Focused = state.WasFocused = false;
                if (observedBlur == EventType.MouseDown) queue.Flush(delegate(Exception error) { throw error; });
                queue.Add(delegate { saved = model; });
                queue.Flush(delegate(Exception error) { throw error; });
                Require(saved == "42", "Save must read the committed draft regardless of which control appears first in the layout.");
            }
        }

        private static void EditingAffordancesDoNotBlurOnPress() {
            Require(!GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDown, 0, true, true, true),
                "A search field's clear affordance must retain its text editor focus.");
            Require(!GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDrag, 0, true, true, false) &&
                !GuiEventPolicy.ShouldBlurTextOnPress(EventType.ScrollWheel, 0, true, true, false),
                "Dragging and scrolling cannot opportunistically commit an unrelated editor.");
            Require(!GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDown, 0, false, true, false) &&
                !GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDown, 0, true, false, false) &&
                !GuiEventPolicy.ShouldBlurTextOnPress(EventType.MouseDown, 1, true, true, false),
                "Disabled, clipped, outside, and alternate-button interactions must preserve editing focus.");
        }

        private static TextFieldState Focused(string value) {
            TextFieldState state = new TextFieldState();
            Sync(state, value, false);
            state.Focused = state.WasFocused = true;
            return state;
        }

        private static bool Sync(TextFieldState state, string value, bool focused) {
            return TextEditingLifecycle.Synchronize(state, value, focused, TextInputFlags.None, null, 0);
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
