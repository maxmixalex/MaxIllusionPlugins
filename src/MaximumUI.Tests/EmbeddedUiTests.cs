using System;
using System.Reflection;
using System.Runtime.Serialization;
using MaximumUI;
using UnityEngine;

namespace MaximumUI.Tests {
    internal static class EmbeddedUiTests {
        internal const int TestCount = 8;

        internal static void Run() {
            SharedScrollDoesNotLetAShorterPaneMoveItsSibling();
            SharedScrollAcceptsOnlyActualUnblockedMovement();
            SharedScrollClampsInvalidAndScrollbarPositions();
            HidingAViewDismissesItsDialogsAndReopensTheSameMount();
            HiddenOwnerCallbacksStayCancelledAfterReopening();
            WidgetIdentityTracksChangesAndSnapshotsRemainStable();
            ReusedQueriesClearPreviousInteractionFlags();
            RetainedQueryAndNavigationRegistrationReusesMemory();
        }

        private static void SharedScrollDoesNotLetAShorterPaneMoveItsSibling() {
            ScrollPosition shared = new ScrollPosition { Position = new Vector2 { x = 0f, y = 800f } };
            MuiRect viewport = new MuiRect(0f, 0f, 300f, 200f);
            Vector2 longPosition = shared.Resolve(viewport, new MuiRect(0f, 0f, 284f, 2000f), false, true);
            Vector2 shortPosition = shared.Resolve(viewport, new MuiRect(0f, 0f, 284f, 260f), false, true);
            shared.Publish(shortPosition, shortPosition, true);
            Assert(longPosition.y == 800f && shortPosition.y == 60f && shared.Position.y == 800f,
                "A short or filtered pane must clamp locally without overwriting a longer pane's requested position.");
            shared.Reset();
            Assert(shared.Position.y == 0f, "Changing the source can explicitly reset both panes.");
        }

        private static void SharedScrollAcceptsOnlyActualUnblockedMovement() {
            ScrollPosition shared = new ScrollPosition { Position = new Vector2 { x = 0f, y = 500f } };
            shared.Publish(new Vector2 { x = 0f, y = 60f }, new Vector2 { x = 0f, y = 30f }, false);
            Assert(shared.Position.y == 500f, "A blocked lower window cannot scroll a sibling.");
            shared.Publish(new Vector2 { x = 0f, y = 60f }, new Vector2 { x = 0f, y = 30f }, true);
            Assert(shared.Position.y == 30f, "An actual wheel/thumb movement in either pane updates their common position.");
            ScrollView scroll = new ScrollView { ScrollPosition = shared };
            VirtualList list = new VirtualList(1000, 24f, null) { ScrollPosition = shared };
            Assert(ReferenceEquals(scroll.ScrollPosition, list.ScrollPosition), "Ordinary and virtual views share the same API.");
        }

        private static void SharedScrollClampsInvalidAndScrollbarPositions() {
            ScrollPosition position = new ScrollPosition { Position = new Vector2 { x = float.NaN, y = float.PositiveInfinity } };
            MuiRect viewport = new MuiRect(0f, 0f, 100f, 100f);
            MuiRect content = new MuiRect(0f, 0f, 100f, 200f);
            Vector2 resolved = position.Resolve(viewport, content, false, false);
            Assert(resolved.x == 0f && resolved.y == 0f, "Invalid external scroll values cannot poison geometry.");
            position.Position = new Vector2 { x = 999f, y = 999f };
            resolved = position.Resolve(viewport, content, false, false);
            Assert(resolved.x == 16f && resolved.y == 116f, "Clamping accounts for mutually induced scrollbars.");
        }

        private sealed class MountedView : View {
            internal BuildContext Context;
            internal int Unmounts;
            public override void OnMount(BuildContext context) { Context = context; }
            public override void OnUnmount(BuildContext context) { Unmounts++; }
            public override Widget Build(BuildContext context) { return new Window("Test", key: "window"); }
        }

        private static MaximumTheme ThemeIdentity() {
            return (MaximumTheme)FormatterServices.GetUninitializedObject(typeof(MaximumTheme));
        }

        private static void HidingAViewDismissesItsDialogsAndReopensTheSameMount() {
            const string id = "test-embedded-visibility";
            MountedView view = new MountedView();
            MountOptions options = new MountOptions { Theme = ThemeIdentity(), Visible = false, BuildEveryFrame = false };
            try {
                MaximumUIRuntime.Mount(id, view, options);
                Assert(MaximumUIRuntime.IsMounted(id) && !MaximumUIRuntime.IsVisible(id), "Mounted hidden editors remain available to their hotkey.");
                MaximumUIRuntime.SetVisible(id, true);
                int results = 0;
                view.Context.Dialogs.Show(new Dialog("Stale confirmation") { OnClose = delegate { results++; } });
                MaximumUIRuntime.SetVisible(id, false);
                Assert(!view.Context.Dialogs.HasOpenDialogs && results == 0, "Hiding cannot execute a stale confirmation or reopen a hidden dialog.");
                MaximumUIRuntime.SetVisible(id, true);
                Assert(MaximumUIRuntime.IsVisible(id) && view.Unmounts == 0, "Reopening retains the same view and its user state.");
            } finally { MaximumUIRuntime.Unmount(id); }
            Assert(view.Unmounts == 1 && !MaximumUIRuntime.IsMounted(id) && !MaximumUIRuntime.SetVisible(id, true), "Teardown removes the mount exactly once.");
        }

        private static void HiddenOwnerCallbacksStayCancelledAfterReopening() {
            const string id = "test-embedded-callback";
            MountedView view = new MountedView();
            try {
                MaximumUIRuntime.Mount(id, view, new MountOptions { Theme = ThemeIdentity(), BuildEveryFrame = false });
                MethodInfo find = typeof(MaximumUIRuntime).GetMethod("FindMount", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo enqueue = typeof(MaximumUIRuntime).GetMethod("EnqueueOwnedCallback", BindingFlags.NonPublic | BindingFlags.Static);
                object record = find.Invoke(null, new object[] { id });
                int calls = 0;
                enqueue.Invoke(null, new object[] { record, new Action(delegate { calls++; }) });
                MaximumUIRuntime.SetVisible(id, false);
                MaximumUIRuntime.SetVisible(id, true);
                MaximumUIRuntime.FlushCallbacks();
                Assert(calls == 0, "A queued destructive command must not survive hide/reopen or a scene change.");
                ViewBuildSchedule schedule = (ViewBuildSchedule)record.GetType().GetField("BuildSchedule").GetValue(record);
                schedule.CompleteBuild(0, schedule.Revision);
                enqueue.Invoke(null, new object[] { record, new Action(delegate { calls++; }) });
                MaximumUIRuntime.FlushCallbacks();
                Assert(calls == 1 && schedule.ShouldBuild(new MountOptions { BuildEveryFrame = false }, 0, true),
                    "A current retained-view callback runs once and invalidates its own view automatically.");
            } finally { MaximumUIRuntime.Unmount(id); }
        }

        private static void WidgetIdentityTracksChangesAndSnapshotsRemainStable() {
            UiQueryStore query = new UiQueryStore();
            UiNavigationStore navigation = new UiNavigationStore();
            Button button = new Button("Edit", null, key: "first");
            MuiRect rect = new MuiRect(0f, 0f, 120f, 28f);
            query.RecordItem(button, "0.1", rect, true, false, true, false);
            ItemQuery saved = query.GetItem("first");
            button.Key = "second";
            query.BeginFrame();
            query.RecordItem(button, "0.2", rect, false, true, false, true);
            navigation.BeginFrame();
            navigation.Register(button, "0.2", rect, true);
            navigation.EndFrame();
            Assert(query.GetItem("first") == null && query.GetItemByPath("0.2").Id == "key:second", "Changing a widget's key/path refreshes the cached identities.");
            Assert(saved.Key == "first" && saved.Path == "0.1" && saved.Hovered && !saved.Clicked, "Public query snapshots remain independent of pooled records.");
            Assert(navigation.FocusPath("0.2") && navigation.IsFocused(button, "0.2"), "Navigation uses the updated canonical identity.");
        }

        private static void ReusedQueriesClearPreviousInteractionFlags() {
            UiQueryStore query = new UiQueryStore();
            Window window = new Window("Old", key: "window");
            Button first = new Button("First", null, key: "first");
            Button second = new Button("Second", null, key: "second");
            MuiRect rect = new MuiRect(0f, 0f, 100f, 40f);
            query.RecordItem(first, "0", rect, true, true, true, true);
            query.RecordWindow(window, "window", rect, rect, true, true, true, true);
            WindowQuery snapshot = query.GetWindow("window");
            query.BeginFrame();
            window.Title = "New";
            query.RecordItem(second, "1", rect, false, false, false, false);
            query.RecordWindow(window, "window", rect, rect, false, false, false, false);
            Assert(query.GetItem("first") == null && !query.IsHovered("second") && !query.IsClicked("second"), "Reused slots cannot leak prior widget interactions.");
            Assert(snapshot.Title == "Old" && snapshot.Hovered && query.GetWindow("window").Title == "New" && !query.IsWindowHovered("window"), "Window snapshots stay stable while live window records are recycled.");
        }

        private static void RetainedQueryAndNavigationRegistrationReusesMemory() {
            MethodInfo allocated = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            if (allocated == null) return;
            Func<long> bytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocated);
            UiQueryStore query = new UiQueryStore();
            UiNavigationStore navigation = new UiNavigationStore();
            Button[] controls = new Button[80];
            string[] paths = new string[controls.Length];
            for (int i = 0; i < controls.Length; i++) { controls[i] = new Button("Field", null, key: i); paths[i] = "0." + i; }
            MuiRect rect = new MuiRect(0f, 0f, 200f, 28f);
            Action frame = delegate {
                query.BeginFrame(); navigation.BeginFrame();
                for (int i = 0; i < controls.Length; i++) {
                    query.RecordItem(controls[i], paths[i], rect, false, false, false, false);
                    navigation.Register(controls[i], paths[i], rect, true);
                }
                navigation.EndFrame();
            };
            for (int i = 0; i < 20; i++) frame();
            long before = bytes();
            for (int i = 0; i < 1000; i++) frame();
            long allocatedBytes = bytes() - before;
            Assert(allocatedBytes == 0, "Retained query/navigation registration allocated " + allocatedBytes + " bytes after warmup.");
        }

        private static void Assert(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
