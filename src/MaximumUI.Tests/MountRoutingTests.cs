using System;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class MountRoutingTests {
        internal const int TestCount = 15;

        internal static void RunAll() {
            OverlappingMountsChooseTheTopVisibleWindow();
            KeyboardFollowsTheClickedMount();
            DragAndReleaseStayWithThePressOwner();
            UnrelatedMouseReleaseDoesNotEndTheDrag();
            ModalPreemptsCaptureAndRestoresKeyboardOwner();
            SameMountModalReleaseEndsThePointerLease();
            ConsumedReleaseEndsTheLeaseWithoutReactivatingInput();
            HiddenAndRemovedOwnersCannotCaptureInput();
            ExplicitFocusRequestsAreObservedOnce();
            DeclarativeFocusRequestsAreAppliedOncePerVisibleRequest();
            FocusPathUsesTheCanonicalItemIdentity();
            OutsideClickReleasesKeyboardOwnership();
            RootWindowHitGeometryMatchesInitialFoldedAndClampedBounds();
            QueryHitTestingReusesRecordedRegions();
            RetainedRoutingDoesNotAllocatePerEvent();
            Console.WriteLine("MaximumUI mount routing regressions: 15 groups passed.");
        }

        private static MountInputTarget Target(bool hit = false) {
            return new MountInputTarget { Available = true, PointerHit = hit };
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

        private static void OverlappingMountsChooseTheTopVisibleWindow() {
            var lower = Target(true); var upper = Target(true);
            var targets = new[] { lower, upper }; var router = new MountInputRouting();
            router.Route(targets, MuiEventType.MouseDown, 0);
            Require(ReferenceEquals(router.EventOwner, upper), "bottom-to-top drawing must not let the lower window consume the click");
            Require(router.Suppress(lower, MuiEventType.MouseDown) && !router.Suppress(upper, MuiEventType.MouseDown), "only one mount accepts the input event");
            Require(!router.Suppress(lower, MuiEventType.Repaint) && !router.Suppress(upper, MuiEventType.None), "routing must preserve normal painting and layout");
            router.Route(targets, MuiEventType.MouseUp, 0);
            upper.Available = false;
            router.Route(targets, MuiEventType.ScrollWheel, 0);
            Require(ReferenceEquals(router.EventOwner, lower), "hidden top window must not block visible content");
        }

        private static void KeyboardFollowsTheClickedMount() {
            var lower = Target(true); var upper = Target(); var targets = new[] { lower, upper };
            var router = new MountInputRouting();
            router.Route(targets, MuiEventType.MouseDown, 0);
            router.Route(targets, MuiEventType.MouseUp, 0);
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, lower) && ReferenceEquals(router.EventOwner, lower), "Tab/keyboard must follow the clicked window rather than the last mount");
            Require(router.Suppress(upper, MuiEventType.KeyDown), "non-focused upper mount still accepts keys");
        }

        private static void DragAndReleaseStayWithThePressOwner() {
            var lower = Target(true); var upper = Target(); var targets = new[] { lower, upper };
            var router = new MountInputRouting(); router.Route(targets, MuiEventType.MouseDown, 0);
            lower.PointerHit = false; upper.PointerHit = true;
            router.Route(targets, MuiEventType.MouseDrag, 0);
            Require(ReferenceEquals(router.EventOwner, lower), "drag migrated into an overlapping window");
            router.Route(targets, MuiEventType.MouseUp, 0);
            Require(ReferenceEquals(router.EventOwner, lower) && router.PointerOwner == null, "release was lost before the original renderer received it");
            router.Route(targets, MuiEventType.MouseMove, 0);
            Require(ReferenceEquals(router.EventOwner, upper), "finished drag retained a stale pointer lease");
            lower.HasPointerCapture = true;
            router.Route(targets, MuiEventType.MouseDrag, 0);
            Require(ReferenceEquals(router.EventOwner, lower), "a renderer's existing hotControl capture was ignored");
        }

        private static void UnrelatedMouseReleaseDoesNotEndTheDrag() {
            var target = Target(true); var targets = new[] { target }; var router = new MountInputRouting();
            router.Route(targets, MuiEventType.MouseDown, 0); target.PointerHit = false;
            router.Route(targets, MuiEventType.MouseUp, 1);
            Require(ReferenceEquals(router.PointerOwner, target), "right-button release ended a left-button drag");
            router.Route(targets, MuiEventType.MouseUp, 0);
            Require(router.PointerOwner == null, "matching release did not end drag ownership");
        }

        private static void ModalPreemptsCaptureAndRestoresKeyboardOwner() {
            var modal = Target(); var upper = Target(true); var targets = new[] { modal, upper };
            var router = new MountInputRouting(); router.Route(targets, MuiEventType.MouseDown, 0);
            upper.HasPointerCapture = true; modal.HasModal = true;
            router.Route(targets, MuiEventType.MouseDrag, 0);
            Require(ReferenceEquals(router.EventOwner, modal) && ReferenceEquals(router.KeyboardOwner, modal) && router.PointerOwner == null,
                "a lower-sort modal must preempt a drag in another mount");
            upper.HasModal = true; router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.ModalOwner, upper), "top visible modal must own input");
            upper.HasModal = false; modal.HasModal = false; upper.HasPointerCapture = false;
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, upper), "closing a modal lost the previous keyboard owner");
        }

        private static void HiddenAndRemovedOwnersCannotCaptureInput() {
            var old = Target(true); var upper = Target(); var router = new MountInputRouting();
            router.Route(new[] { old, upper }, MuiEventType.MouseDown, 0);
            old.Available = false; upper.PointerHit = true;
            router.Route(new[] { old, upper }, MuiEventType.MouseDrag, 0);
            Require(ReferenceEquals(router.EventOwner, upper), "hidden owner retained the pointer");
            old.Available = true; router.Focus(old);
            router.Route(new[] { upper }, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, upper), "removed/replaced mount retained keyboard ownership");
            router.Forget(upper);
            Require(router.KeyboardOwner == null && router.EventOwner == null, "unmount did not release routing references");
        }

        private static void SameMountModalReleaseEndsThePointerLease() {
            var modal = Target(true); var upper = Target(); var targets = new[] { modal, upper };
            var router = new MountInputRouting(); router.Route(targets, MuiEventType.MouseDown, 0);
            modal.HasModal = true;
            router.Route(targets, MuiEventType.MouseUp, 0);
            Require(ReferenceEquals(router.EventOwner, modal) && router.PointerOwner == null,
                "opening a modal in the drag owner swallowed its release");
            modal.HasModal = false; modal.PointerHit = false; upper.PointerHit = true;
            router.Route(targets, MuiEventType.MouseDown, 0);
            Require(ReferenceEquals(router.EventOwner, upper), "closed modal left a stale pointer lease over another window");
        }

        private static void ConsumedReleaseEndsTheLeaseWithoutReactivatingInput() {
            var lower = Target(true); var upper = Target(); var targets = new[] { lower, upper };
            var router = new MountInputRouting(); router.Route(targets, MuiEventType.MouseDown, 0);
            lower.PointerHit = false; upper.PointerHit = true;
            router.Route(targets, MuiEventType.None, 1, true);
            Require(ReferenceEquals(router.PointerOwner, lower), "consumed right release ended a left drag");
            router.Route(targets, MuiEventType.None, 0, true);
            Require(router.PointerOwner == null && !router.Suppress(upper, MuiEventType.None),
                "raw release must clear ownership without turning Used into a new input event");
            router.Route(targets, MuiEventType.MouseDown, 0);
            Require(ReferenceEquals(router.EventOwner, upper), "consumed release left the next window's click with the old owner");
        }

        private static void ExplicitFocusRequestsAreObservedOnce() {
            var lower = Target(); var upper = Target(); var targets = new[] { lower, upper };
            var navigation = new UiNavigationStore();
            var button = new Button("Explicit", key: "explicit") { HasDefaultFocus = true };
            RegisterNavigation(navigation, button, null);
            Require(navigation.ExplicitFocusRevision == 0, "default focus must not request ownership of another mount");
            navigation.FocusNext(); navigation.FocusPrevious(); navigation.FocusPointer(button, "0");
            Require(navigation.ExplicitFocusRevision == 0, "sequential or pointer navigation created a new explicit request");
            var router = new MountInputRouting(); router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, upper), "initial keyboard fallback should use the top window");
            Require(navigation.Focus("explicit"), "registered item could not be focused");
            lower.ExplicitFocusRevision = navigation.ExplicitFocusRevision;
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, lower), "explicit focus did not select its mount");
            router.Focus(upper); router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, upper), "already-observed request repeatedly stole focus");
            navigation.Focus("explicit");
            lower.ExplicitFocusRevision = navigation.ExplicitFocusRevision;
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, lower), "explicitly refocusing the same item must reacquire its mount");
            int revision = navigation.ExplicitFocusRevision;
            Require(!navigation.Focus("missing") && !navigation.FocusPath("missing") && navigation.ExplicitFocusRevision == revision,
                "failed focus lookup must not steal ownership");
            router.Focus(upper); lower.Available = false; navigation.Focus("explicit");
            lower.ExplicitFocusRevision = navigation.ExplicitFocusRevision;
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, upper), "hidden mount stole focus by requesting it");
        }

        private static void DeclarativeFocusRequestsAreAppliedOncePerVisibleRequest() {
            var navigation = new UiNavigationStore();
            var request = new Button("Requested", key: "request") { RequestFocus = true };
            var other = new Button("Other", key: "other");
            RegisterNavigation(navigation, request, other);
            Require(navigation.IsKeyboardFocused(request, "0") && navigation.ExplicitFocusRevision == 1,
                "a first declarative focus request was not published");
            navigation.FocusPointer(other, "1");
            RegisterNavigation(navigation, request, other);
            Require(navigation.IsFocused(other, "1") && !navigation.KeyboardActive && navigation.ExplicitFocusRevision == 1,
                "retained request stole focus back on the next GUI event");
            request = new Button("Rebuilt", key: "request") { RequestFocus = true };
            RegisterNavigation(navigation, request, other);
            Require(navigation.IsFocused(other, "1") && navigation.ExplicitFocusRevision == 1,
                "rebuilding the same logical request must not continually steal focus");
            navigation.BeginFrame();
            navigation.Register(request, "0", new MuiRect(), false);
            navigation.Register(other, "1", new MuiRect(), true);
            navigation.EndFrame();
            RegisterNavigation(navigation, request, other);
            Require(navigation.IsFocused(other, "1") && navigation.ExplicitFocusRevision == 1,
                "one suppressed input event rearmed a held declarative focus request");
            request.RequestFocus = false; RegisterNavigation(navigation, request, other);
            request.RequestFocus = true; RegisterNavigation(navigation, request, other);
            Require(navigation.IsKeyboardFocused(request, "0") && navigation.ExplicitFocusRevision == 2,
                "a withdrawn request could not be raised again");
            other.RequestFocus = true; RegisterNavigation(navigation, request, other);
            Require(navigation.IsKeyboardFocused(other, "1") && navigation.ExplicitFocusRevision == 3,
                "a newly requested sibling must take focus");
            other.RequestFocus = false; RegisterNavigation(navigation, request, other);
            Require(navigation.IsFocused(other, "1") && navigation.ExplicitFocusRevision == 3,
                "removing a sibling request reactivated an older held request");
            RegisterNavigation(navigation, null, other);
            RegisterNavigation(navigation, request, other);
            Require(navigation.IsKeyboardFocused(request, "0") && navigation.ExplicitFocusRevision == 4,
                "a request restored after leaving the visible navigation tree was ignored");
            var pending = new Button("Initially disabled", key: "pending") { RequestFocus = true, Enabled = false };
            RegisterNavigation(navigation, pending, other);
            Require(navigation.ExplicitFocusRevision == 4, "a disabled item published its first request before it could receive focus");
            pending.Enabled = true; RegisterNavigation(navigation, pending, other);
            Require(navigation.IsKeyboardFocused(pending, "0") && navigation.ExplicitFocusRevision == 5,
                "an initially disabled request was lost before the control became enabled");
        }

        private static void FocusPathUsesTheCanonicalItemIdentity() {
            var navigation = new UiNavigationStore();
            var first = new Button("First", key: "first");
            var second = new Button("Second", key: "second");
            RegisterNavigation(navigation, first, second);
            Require(navigation.FocusPath("1") && navigation.IsKeyboardFocused(second, "1") &&
                navigation.FocusedId == "key:second" && navigation.ExplicitFocusRevision == 1,
                "FocusPath stored an alias instead of the keyed item's canonical identity");
            navigation.FocusPrevious();
            Require(navigation.IsFocused(first, "0") && navigation.ExplicitFocusRevision == 1,
                "sequential navigation failed after FocusPath on a keyed item");
        }

        private static void RegisterNavigation(UiNavigationStore navigation, Widget first, Widget second) {
            navigation.BeginFrame();
            navigation.Register(first, "0", new MuiRect(0f, 0f, 100f, 24f), true);
            navigation.Register(second, "1", new MuiRect(0f, 30f, 100f, 24f), true);
            navigation.EndFrame();
        }

        private static void OutsideClickReleasesKeyboardOwnership() {
            var target = Target(); var targets = new[] { target }; var router = new MountInputRouting();
            router.Route(targets, MuiEventType.KeyDown, 0);
            router.Route(targets, MuiEventType.MouseDown, 0);
            router.Route(targets, MuiEventType.MouseUp, 0);
            router.Route(targets, MuiEventType.KeyDown, 0);
            Require(router.KeyboardOwner == null && router.EventOwner == null, "clicking back into the game left a hidden keyboard owner");
            target.ExplicitFocusRevision++; router.Route(targets, MuiEventType.KeyDown, 0);
            Require(ReferenceEquals(router.KeyboardOwner, target), "explicit focus could not restore UI keyboard ownership");
        }

        private static void RootWindowHitGeometryMatchesInitialFoldedAndClampedBounds() {
            var window = new Window("root", 420f, 300f, minWidth: 320f, minHeight: 240f);
            MuiRect first = MountInputRouting.RootWindowRect(window, new MuiRect(), false, false, 32f, 1280f, 720f);
            Require(first.X == 80f && first.Y == 60f && first.Width == 420f && first.Height == 300f, "first event must hit the renderer's initial geometry before queries exist");
            MuiRect folded = MountInputRouting.RootWindowRect(window, new MuiRect(100f, 90f, 420f, 300f), true, true, 32f, 1280f, 720f);
            Require(folded.Height == 32f && !folded.Contains(new MuiVec2(110f, 150f)), "folded window blocked input in its hidden content");
            MuiRect small = MountInputRouting.RootWindowRect(window, new MuiRect(900f, 700f, 800f, 600f), true, false, 32f, 320f, 200f);
            Require(small.X == 0f && small.Y == 0f && small.Width == 320f && small.Height == 200f, "routing and drawing disagree after a resolution shrink");
        }

        private static void QueryHitTestingReusesRecordedRegions() {
            var query = new UiQueryStore(); var window = new Window("visible");
            query.RecordWindow(window, "0", new MuiRect(10f, 10f, 100f, 100f), new MuiRect(10f, 10f, 100f, 20f), false, false, false, false);
            query.RecordInputRegion(new MuiRect(120f, 30f, 80f, 60f), true);
            query.RecordInputRegion(new MuiRect(220f, 30f, 80f, 60f));
            Require(query.ContainsVisibleWindow(new MuiVec2(20f, 20f)), "recorded visible window not hit");
            Require(query.ContainsInputRegion(new MuiVec2(150f, 40f)), "popup outside the root window not hit");
            Require(query.ContainsOverlayRegion(new MuiVec2(150f, 40f)) &&
                !query.ContainsOverlayRegion(new MuiVec2(250f, 40f)), "normal content must not become a stale popup hit after the root window moves");
            Require(!query.ContainsInputRegion(new MuiVec2(500f, 500f)), "input regions captured the entire viewport");
            query.BeginFrame(); window.Visible = false;
            query.RecordWindow(window, "0", new MuiRect(10f, 10f, 100f, 100f), new MuiRect(), false, false, false, false);
            Require(!query.ContainsVisibleWindow(new MuiVec2(20f, 20f)) && !query.ContainsInputRegion(new MuiVec2(150f, 40f)) &&
                !query.ContainsOverlayRegion(new MuiVec2(150f, 40f)), "new event retained hidden/stale regions");
        }

        private static void RetainedRoutingDoesNotAllocatePerEvent() {
            System.Reflection.MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) return;
            Func<long> allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            var lower = Target(true); var upper = Target(true); var targets = new[] { lower, upper };
            var router = new MountInputRouting();
            var query = new UiQueryStore();
            query.RecordInputRegion(new MuiRect(10f, 10f, 100f, 100f), true);
            var point = new MuiVec2(20f, 20f);
            for (int i = 0; i < 1024; i++) {
                router.Route(targets, MuiEventType.MouseMove, 0);
                query.ContainsInputRegion(point); query.ContainsOverlayRegion(point);
            }
            long before = allocated();
            for (int i = 0; i < 10000; i++) {
                router.Route(targets, MuiEventType.MouseMove, 0);
                query.ContainsInputRegion(point); query.ContainsOverlayRegion(point);
            }
            long bytes = allocated() - before;
            Require(bytes == 0, "retained cross-mount routing/hit testing allocated " + bytes + " bytes for 10000 unchanged events");
        }
    }
}
