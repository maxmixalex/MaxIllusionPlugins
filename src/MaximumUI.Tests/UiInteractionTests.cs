using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MaximumUI;
using UnityEngine;

namespace MaximumUI.Tests {
    internal static class UiInteractionTests {
        internal const int TestCount = 12;

        public static void RunAll() {
            ChoiceListsRetainAndFilterWithoutRepeatedFormatting();
            VirtualRowsReuseAndBoundWorkToTheViewport();
            WindowGeometryKeepsChromeReachable();
            PopupGeometryFitsEveryScreenEdge();
            KeyboardNavigationPreservesSelectionAndScroll();
            PointerFocusDoesNotStealKeyboardInput();
            LabelRolesUseTheirActualMetrics();
            RuntimeMountsCanRemoveAndReplaceDuringUpdate();
            RuntimeThemesUpdateEveryContextAndUnmountOnce();
            SliderKeysRespectRangeAndStep();
            TabsDistinguishClicksFromDragAndAlwaysRelease();
            PopupStateClosesEveryOwner();
            Console.WriteLine("MaximumUI interaction/performance regressions: 12 groups passed.");
        }

        private static void ChoiceListsRetainAndFilterWithoutRepeatedFormatting() {
            List<int> values = new List<int>();
            for (int i = 0; i < 10000; i++) values.Add(i);
            int formats = 0;
            Func<object, string> format = delegate(object value) { formats++; return "Entry " + value; };
            ChoiceCache cache = new ChoiceCache();
            object owner = new object();
            cache.Refresh(owner, values, format, null, 0, true);
            string[] first = cache.Labels;
            for (int i = 0; i < 300; i++) cache.Refresh(owner, values, format, string.Empty, 0, true);
            Require(formats == 10000 && cache.EnumerationCount == 1, "idle choices format 10,000 items once, not per GUI event");
            Require(ReferenceEquals(first, cache.Labels), "unchanged choices reuse the same label array");
            cache.Refresh(owner, values, format, "entry 9999", 0, true);
            Require(formats == 10000 && cache.Values.Count == 1 && (int)cache.Values[0] == 9999, "case-insensitive filtering reuses formatted labels and preserves value mapping");
            values.Add(10000);
            cache.Refresh(owner, values, format, string.Empty, 0, true);
            Require(cache.Values.Count == 10001 && cache.EnumerationCount == 2, "collection count change refreshes a retained widget");
            values[0] = -10;
            cache.Refresh(owner, values, format, string.Empty, 1, true);
            Require((int)cache.Values[0] == -10, "explicit invalidation refreshes same-size mutation");
            cache.Refresh(owner, null, format, string.Empty, 2, true);
            Require(cache.Values.Count == 0 && cache.Labels.Length == 0, "empty source removes stale options");
            cache.Refresh(owner, values, format, string.Empty, 2, false);
            long count = cache.EnumerationCount;
            cache.Refresh(owner, values, format, string.Empty, 2, false);
            Require(cache.EnumerationCount == count + 1, "uncached direct renderers preserve live enumeration");
        }

        private static void VirtualRowsReuseAndBoundWorkToTheViewport() {
            int builds = 0;
            VirtualList list = new VirtualList(100000, 24f, delegate(int i) {
                builds++;
                return new Label("Entry " + i);
            });
            VirtualListCache cache = new VirtualListCache();
            LayoutEngine engine = new LayoutEngine();
            for (int frame = 0; frame < 300; frame++) {
                cache.BeginRange(list, 0, 13, 0, engine);
                for (int i = 0; i < 13; i++) cache.GetRow(i, new MuiRect(0f, i * 24f, 300f, 24f), "list.row" + i);
            }
            Require(builds == 13 && cache.LayoutCount == 13, "idle virtual viewport builds and measures 13 rows once across 300 events");
            for (int first = 1; first < 1000; first++) {
                cache.BeginRange(list, first, first + 13, 0, engine);
                for (int i = first; i < first + 13; i++) cache.GetRow(i, new MuiRect(0f, i * 24f, 300f, 24f), "list.row" + i);
                Require(cache.Count == 13, "scrolling never retains previously visited pages");
            }
            Require(builds == 1012, "scrolling one row rebuilds only the entering row");
            cache.BeginRange(list, 999, 1012, 1, engine);
            cache.GetRow(999, new MuiRect(0f, 999f * 24f, 300f, 24f), "list.row999");
            Require(builds == 1013, "view invalidation refreshes retained row content");
        }

        private static void WindowGeometryKeepsChromeReachable() {
            MuiRect clamped = WindowGeometry.Clamp(new MuiRect(-100f, 900f, 900f, 700f), 320f, 200f, 640f, 480f);
            Require(clamped.X == 0f && clamped.Y == 0f && clamped.Width == 320f && clamped.Height == 200f, "screen bounds override a larger requested minimum");
            MuiRect resized = WindowGeometry.Resize(new MuiRect(40f, 30f, 250f, 150f), new MuiVec2(1000f, 1000f), 640f, 480f, 200f, 100f);
            Require(resized.X == 40f && resized.Y == 30f && resized.Right == 640f && resized.Bottom == 480f, "resize keeps origin and grip on-screen");
            resized = WindowGeometry.Resize(resized, new MuiVec2(-1000f, -1000f), 640f, 480f, 200f, 100f);
            Require(resized.Width == 200f && resized.Height == 100f, "resize respects reachable minimum dimensions");
        }

        private static void PopupGeometryFitsEveryScreenEdge() {
            MuiRect popup = WindowGeometry.PlacePopup(new MuiRect(290f, 180f, 200f, 20f), 250f, 120f, 320f, 200f);
            Require(popup.Right <= 320f && popup.Bottom <= 200f && popup.Y == 60f, "bottom-right popup flips upward and shifts left");
            popup = WindowGeometry.PlacePopup(new MuiRect(-20f, 5f, 100f, 20f), 1000f, 1000f, 320f, 200f);
            Require(popup.X == 0f && popup.Y == 0f && popup.Width == 320f && popup.Height == 200f, "oversized popup is fully reachable on a small screen");
        }

        private static void KeyboardNavigationPreservesSelectionAndScroll() {
            int next;
            Require(KeyboardNavigation.MoveSelection(-1, 10, KeyCode.DownArrow, 4, out next) && next == 0, "Down from no selection starts with first row");
            Require(KeyboardNavigation.MoveSelection(8, 10, KeyCode.PageDown, 4, out next) && next == 9, "page navigation stays inside items");
            Require(KeyboardNavigation.MoveSelection(8, 10, KeyCode.Home, 4, out next) && next == 0, "Home reaches first row");
            Require(!KeyboardNavigation.MoveSelection(0, 0, KeyCode.DownArrow, 4, out next) && next == -1, "empty list has no actionable selection");
            Require(KeyboardNavigation.RevealRow(0f, 20, 24f, 120f, 100) == 384f, "keyboard selection reveals the entire row");
            Require(KeyboardNavigation.RevealRow(384f, 2, 24f, 120f, 100) == 48f, "moving up scrolls back to selected row");
            Require(KeyboardNavigation.IsActivation(KeyCode.Space, false, false, false) && !KeyboardNavigation.IsActivation(KeyCode.Return, true, false, false), "plain activation preserves modified shortcuts");
        }

        private static void PointerFocusDoesNotStealKeyboardInput() {
            Button a = new Button("first", key: "a");
            Button b = new Button("second", key: "b");
            UiNavigationStore navigation = new UiNavigationStore();
            navigation.BeginFrame();
            navigation.Register(a, "0", new MuiRect(), true);
            navigation.Register(b, "1", new MuiRect(), true);
            navigation.EndFrame();
            Require(!navigation.KeyboardActive, "merely displaying a window does not activate keyboard control");
            navigation.FocusNext();
            Require(navigation.IsKeyboardFocused(b, "1"), "Tab connects to actionable keyboard focus");
            navigation.FocusPointer(a, "0");
            Require(navigation.IsFocused(a, "0") && !navigation.KeyboardActive, "mouse focus does not leave keyboard navigation active");
            navigation.Focus("a");
            Require(navigation.IsKeyboardFocused(a, "0"), "explicit programmatic focus supports activation");
        }

        private static void SliderKeysRespectRangeAndStep() {
            float next;
            Require(KeyboardNavigation.MoveSlider(50f, 0f, 100f, KeyCode.RightArrow, true, false, out next) && next == 51f,
                "integer slider moves exactly one unit");
            Require(KeyboardNavigation.MoveSlider(0.5f, 0f, 1f, KeyCode.LeftArrow, false, true, out next) && Math.Abs(next - 0.4f) < 0.00001f,
                "Shift moves a floating slider by ten percent of its range");
            Require(KeyboardNavigation.MoveSlider(0f, 10f, -10f, KeyCode.Home, false, false, out next) && next == -10f,
                "Home normalizes a reversed range");
            Require(KeyboardNavigation.MoveSlider(90f, 0f, 100f, KeyCode.End, false, false, out next) && next == 100f,
                "End reaches the exact upper bound");
            Require(KeyboardNavigation.MoveSlider(100f, 0f, 100f, KeyCode.UpArrow, true, true, out next) && next == 100f,
                "keyboard input is consumed even at a limit");
            Require(!KeyboardNavigation.MoveSlider(5f, 0f, 10f, KeyCode.Return, false, false, out next) && next == 5f,
                "unrelated keys leave the model unchanged");
            Require(!KeyboardNavigation.MoveSlider(5f, float.NaN, 10f, KeyCode.Home, false, false, out next),
                "invalid numeric ranges are not published");
        }

        private static void TabsDistinguishClicksFromDragAndAlwaysRelease() {
            TabsState state = new TabsState();
            int from;
            int to;
            state.BeginDrag(1, new MuiVec2(10f, 10f));
            state.MoveDrag(new MuiVec2(13f, 14f));
            Require(!state.CompleteDrag(2, true, out from, out to) && !state.Dragging && state.DragFrom == -1,
                "ordinary click jitter never reorders a tab");
            state.BeginDrag(1, new MuiVec2(10f, 10f));
            state.MoveDrag(new MuiVec2(17f, 10f));
            Require(state.CompleteDrag(3, true, out from, out to) && from == 1 && to == 3 && state.DragFrom == -1,
                "a real drag reports its source and target exactly once");
            Require(!state.CompleteDrag(3, true, out from, out to), "a repeated MouseUp cannot reorder twice");
            state.BeginDrag(0, new MuiVec2());
            state.MoveDrag(new MuiVec2(20f, 0f));
            Require(!state.CompleteDrag(-1, true, out from, out to) && !state.Dragging,
                "release outside all headers cancels cleanly");
            state.BeginDrag(0, new MuiVec2());
            state.MoveDrag(new MuiVec2(20f, 0f));
            Require(!state.CompleteDrag(2, false, out from, out to) && state.DragFrom == -1,
                "suppressed or disabled release cannot dispatch a reorder");
            state.BeginDrag(0, new MuiVec2());
            state.MoveDrag(new MuiVec2(20f, 0f));
            state.ClearDrag();
            Require(!state.Dragging && state.DragFrom == -1, "hide, unmount or modal capture can cancel without MouseUp");
        }

        private static void PopupStateClosesEveryOwner() {
            RenderContext context = new RenderContext();
            Require(!context.HasOpenPopup, "a fresh context does not suppress Tab");
            ContextMenuState menu = new ContextMenuState { Open = true };
            context.ActiveContextMenu = menu;
            Require(context.HasOpenPopup, "context menus reserve popup input");
            context.CloseMenus();
            Require(!menu.Open && !context.HasOpenPopup && context.ActiveContextMenu == null,
                "closing a context menu restores ordinary navigation");
            MenuBarState bar = new MenuBarState { Open = true, OpenIndex = 1 };
            bar.SubmenuPath.Add(2);
            context.ActiveMenuBar = bar;
            Require(context.HasOpenPopup, "menu bars also reserve Tab and Escape");
            context.CloseMenus();
            Require(!bar.Open && bar.OpenIndex == -1 && bar.SubmenuPath.Count == 0 && !context.HasOpenPopup,
                "closing a menu releases nested paths and its input owner");
            context.DragDrop.Active = true;
            context.DragDrop.Payload = new DragPayload("test", "value");
            context.ResetOverlays();
            Require(!context.DragDrop.Active && context.DragDrop.Payload == null,
                "resetting overlays releases the drag payload as well as menu state");
        }

        private static void LabelRolesUseTheirActualMetrics() {
            LayoutMetrics metrics = new LayoutMetrics {
                MeasureLabelWidth = delegate(Label label) { return label.Role == LabelRole.Heading ? 240f : 120f; },
                MeasureLabelHeight = delegate(Label label, float width) { return label.Role == LabelRole.Heading ? 42f : 24f; }
            };
            Label heading = new Label("Heading") { Role = LabelRole.Heading };
            LayoutNode node = new LayoutEngine(metrics).Measure(heading, new LayoutConstraints(0f, 300f, 0f, 300f), "heading");
            Require(node.DesiredSize.X == 240f && node.DesiredSize.Y == 42f, "heading size uses its own font metrics");
        }

        // This object only transports identity through mount metadata. No native
        // style, texture or Unity drawing operation is called in these tests.
        private static MaximumTheme ThemeIdentity() {
            return (MaximumTheme)FormatterServices.GetUninitializedObject(typeof(MaximumTheme));
        }

        private sealed class LifecycleView : View {
            public Action OnUpdate;
            public int Updates;
            public int Unmounts;
            public BuildContext Context;
            public override void OnMount(BuildContext context) { Context = context; }
            public override void OnUnmount(BuildContext context) { Unmounts++; }
            public override void Update(BuildContext context) { Updates++; if (OnUpdate != null) OnUpdate(); }
            public override Widget Build(BuildContext context) { return new Label("test"); }
        }

        private static void RuntimeMountsCanRemoveAndReplaceDuringUpdate() {
            const string firstId = "test-lifecycle-first";
            const string secondId = "test-lifecycle-second";
            const string thirdId = "test-lifecycle-third";
            MaximumTheme theme = ThemeIdentity();
            LifecycleView first = new LifecycleView();
            LifecycleView second = new LifecycleView();
            LifecycleView third = new LifecycleView();
            try {
                MaximumUIRuntime.Mount(firstId, first, new MountOptions { Theme = theme });
                MaximumUIRuntime.Mount(secondId, second, new MountOptions { Theme = theme });
                first.OnUpdate = delegate {
                    MaximumUIRuntime.Unmount(firstId);
                    MaximumUIRuntime.Mount(thirdId, third, new MountOptions { Theme = theme });
                };
                MaximumUIRuntime.Update();
                Require(first.Updates == 1 && second.Updates == 1 && third.Updates == 0, "self-unmount cannot skip the next view or update a newly mounted view twice");
                MaximumUIRuntime.Update();
                Require(second.Updates == 2 && third.Updates == 1 && first.Unmounts == 1, "next update observes the new mount snapshot");
            } finally {
                MaximumUIRuntime.Unmount(firstId);
                MaximumUIRuntime.Unmount(secondId);
                MaximumUIRuntime.Unmount(thirdId);
            }
        }

        private static void RuntimeThemesUpdateEveryContextAndUnmountOnce() {
            const string id = "test-theme-lifecycle";
            MaximumTheme original = ThemeIdentity();
            MaximumTheme replacement = ThemeIdentity();
            LifecycleView view = new LifecycleView();
            try {
                MaximumUIRuntime.Mount(id, view, new MountOptions { Theme = original });
                Require(MaximumUIRuntime.SetTheme(id, replacement), "mounted theme can be replaced");
                Require(ReferenceEquals(view.Context.Theme, replacement) && ReferenceEquals(view.Context.Dialogs.Theme, replacement), "view and dialog service receive new theme");
                Require(!MaximumUIRuntime.SetTheme("missing-test-view", replacement), "unknown mount is not implicitly created");
                MaximumUIRuntime.Unmount(id);
                MaximumUIRuntime.Unmount(id);
                Require(view.Unmounts == 1, "unmount is idempotent");
            } finally { MaximumUIRuntime.Unmount(id); }
        }

        private static void Require(bool value, string message) {
            if (!value) throw new InvalidOperationException("UI regression: " + message);
        }
    }
}
