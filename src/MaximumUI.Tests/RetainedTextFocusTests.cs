using System;
using System.Reflection;
using System.Runtime.Serialization;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class RetainedTextFocusTests {
        internal const int TestCount = 4;

        internal static void RunAll() {
            AnEvictedVirtualRowCommitsBeforeSave();
            ChangingFieldsCommitsThePreviousDraftOnlyOnce();
            AStalePopupOrDisabledSourceCannotCommit();
            HidingAndUnmountingDropRetainedEditingOwnership();
            Console.WriteLine("MaximumUI retained text focus regressions: " + TestCount + " passed.");
        }

        private static void AnEvictedVirtualRowCommitsBeforeSave() {
            string model = "1", saved = null;
            int commits = 0;
            TextField input = new TextField(model, key: "range") {
                OnBlur = delegate(string text) { model = text; commits++; }
            };
            VirtualList list = new VirtualList(100, 32f, delegate(int row) {
                return row == 0 ? (Widget)input : new Label("Row " + row);
            });
            LayoutEngine engine = new LayoutEngine();
            VirtualListCache rows = new VirtualListCache();
            rows.BeginRange(list, 0, 1, 0, engine);
            LayoutNode first = rows.GetRow(0, new MuiRect(0f, 0f, 200f, 32f), "list/0");
            Require(ReferenceEquals(first.Widget, input), "The actual virtual cache must initially render the editable field.");
            TextFieldState state = Focused(model);
            TextFocusSession focus = new TextFocusSession();
            focus.Track(state, input.OnBlur, input, null);
            TextEditingLifecycle.Edit(state, "42");

            rows.BeginRange(list, 99, 100, 0, engine);
            rows.GetRow(99, new MuiRect(0f, 3168f, 200f, 32f), "list/99");
            Require(rows.Count == 1 && focus.Owns(state), "Scrolling must evict the row while retaining its uncommitted editing owner.");
            DeferredActions queue = new DeferredActions();
            queue.Add(focus.TakeBlur()); // Actual command press uses this path.
            queue.Add(delegate { saved = model; }); // The command runs on release.
            queue.Flush(delegate(Exception error) { throw error; });
            Require(saved == "42" && commits == 1 && !state.Focused && !state.WasFocused,
                "Save must see the draft even though its TextField no longer participates in rendering.");
            Require(focus.TakeBlur() == null, "A subsequent render/release cannot submit that retained draft twice.");
        }

        private static void ChangingFieldsCommitsThePreviousDraftOnlyOnce() {
            TextFocusSession focus = new TextFocusSession();
            TextField first = new TextField("1"), second = new TextField("2");
            TextFieldState oldState = Focused(first.Value), newState = Focused(second.Value);
            int firstCommits = 0, secondCommits = 0;
            focus.Track(oldState, delegate(string text) { first.Value = text; firstCommits++; }, first, null);
            TextEditingLifecycle.Edit(oldState, "11");
            Action displaced = focus.Track(newState, delegate(string text) { second.Value = text; secondCommits++; }, second, null);
            Require(displaced != null && focus.Owns(newState) && !oldState.Focused,
                "Focusing another field must hand off, not silently replace, an offscreen draft.");
            displaced();
            TextEditingLifecycle.Edit(newState, "22");
            focus.TakeBlur()();
            Require(first.Value == "11" && second.Value == "22" && firstCommits == 1 && secondCommits == 1,
                "Each field keeps its original callback and submits exactly once.");
        }

        private static void AStalePopupOrDisabledSourceCannotCommit() {
            TextField input = new TextField("1");
            TextFieldState state = Focused(input.Value);
            TextFocusSession focus = new TextFocusSession();
            PopupOwnerState popup = new PopupOwnerState();
            popup.Capture(new Widget[] { new Panel() }, true);
            popup.BeginOpen();
            int commits = 0;
            focus.Track(state, delegate { commits++; }, input, popup);
            TextEditingLifecycle.Edit(state, "2");
            popup.BeginOpen();
            focus.TakeBlur()();
            Require(commits == 0 && !state.Focused, "Taking blur later must not rebind an old field to a newly opened popup generation.");

            state = Focused("3");
            focus.Track(state, delegate { commits++; }, input, null);
            input.Enabled = false;
            focus.TakeBlur()();
            Require(commits == 0, "Disabling the original source must prevent a retained blur from mutating it.");
        }

        private sealed class TestView : View {
            public override Widget Build(BuildContext context) { return new Window("Retained editing"); }
        }

        private static void HidingAndUnmountingDropRetainedEditingOwnership() {
            const string id = "retained-focus-lifecycle-test";
            MaximumUIRuntime.Mount(id, new TestView(), new MountOptions {
                Theme = (MaximumTheme)FormatterServices.GetUninitializedObject(typeof(MaximumTheme))
            });
            try {
                MethodInfo find = typeof(MaximumUIRuntime).GetMethod("FindMount", BindingFlags.NonPublic | BindingFlags.Static);
                object mount = find.Invoke(null, new object[] { id });
                ImguiRenderer renderer = (ImguiRenderer)mount.GetType().GetField("Renderer").GetValue(mount);
                TextFocusSession focus = (TextFocusSession)typeof(ImguiRenderer).GetField("textFocus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
                int commits = 0;
                TextFieldState state = Focused("1");
                focus.Track(state, delegate { commits++; }, new TextField("1"), null);
                TextEditingLifecycle.Edit(state, "unsubmitted");
                MaximumUIRuntime.SetVisible(id, false);
                Require(focus.TakeBlur() == null && !state.Focused && !state.WasFocused && commits == 0,
                    "Hiding a real mount must abandon its retained draft without executing old callbacks.");

                MaximumUIRuntime.SetVisible(id, true);
                state = Focused("2");
                focus.Track(state, delegate { commits++; }, new TextField("2"), null);
                MaximumUIRuntime.Unmount(id);
                Require(focus.TakeBlur() == null && !state.Focused && commits == 0,
                    "Unmounting must release retained editing ownership before disposing the renderer.");
            } finally { MaximumUIRuntime.Unmount(id); }
        }

        private static TextFieldState Focused(string value) {
            TextFieldState state = new TextFieldState();
            TextEditingLifecycle.Synchronize(state, value, false, TextInputFlags.None, null, 0);
            state.Focused = state.WasFocused = true;
            return state;
        }

        private static void Require(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
