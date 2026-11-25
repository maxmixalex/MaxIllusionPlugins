using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public static class MaximumUIRuntime {
        private sealed class MountRecord {
            public string Id;
            public View View;
            public MountOptions Options;
            public UiStateStore StateStore = new UiStateStore();
            public UiQueryStore QueryStore = new UiQueryStore();
            public UiNavigationStore Navigation = new UiNavigationStore();
            public UiShortcutRouter Shortcuts = new UiShortcutRouter();
            public ImguiRenderer Renderer = new ImguiRenderer();
            public DialogService Dialogs = new DialogService();
            public readonly ViewBuildSchedule BuildSchedule = new ViewBuildSchedule();
            public readonly MountInputTarget InputTarget = new MountInputTarget();
            public Widget CachedRoot;
            public string RootWindowStateId;
            public BuildContext BuildContext;
            public RenderContext RenderContext;
            public bool Active = true;
            public int CallbackEpoch;
            public long Order;
            public long WindowActivation;
            public bool PendingWindowPromotion;
        }

        private sealed class BaseMountComparer : IComparer<MountRecord> {
            public int Compare(MountRecord a, MountRecord b) {
                int result = a.Options.SortOrder.CompareTo(b.Options.SortOrder);
                return result == 0 ? a.Order.CompareTo(b.Order) : result;
            }
        }

        private sealed class ActivatedWindowComparer : IComparer<MountRecord> {
            public int Compare(MountRecord a, MountRecord b) {
                int result = a.WindowActivation.CompareTo(b.WindowActivation);
                return result == 0 ? baseMountComparer.Compare(a, b) : result;
            }
        }

        private static readonly List<MountRecord> mounts = new List<MountRecord>();
        private static readonly DeferredActions pendingCallbacks = new DeferredActions();
        private static MountRecord[] mountSnapshot = new MountRecord[0];
        private static MountInputTarget[] inputSnapshot = new MountInputTarget[0];
        private static readonly MountInputRouting inputRouting = new MountInputRouting();
        private static MountInputTarget lastKeyboardOwner;
        private static MountRecord renderingMount;
        private static bool snapshotDirty;
        private static long nextOrder;
        private static long nextWindowActivation;
        private static readonly BaseMountComparer baseMountComparer = new BaseMountComparer();
        private static readonly ActivatedWindowComparer activatedWindowComparer = new ActivatedWindowComparer();
        private static MaximumUIHost host;
        private static int frameCount;

        public static bool IsMouseOverAnyWindow;
        public static bool IsAnyTextFieldFocused;
        public static bool IsModalOpen;

        public static void EnsureHost() {
            if (host != null) return;

            GameObject go = new GameObject("MaximumUIHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<MaximumUIHost>();
        }

        public static void Mount(string id, View view, MountOptions options) {
            if (string.IsNullOrEmpty(id) || view == null) return;

            Unmount(id);
            MountRecord mount = new MountRecord {
                Id = id,
                View = view,
                Options = options ?? new MountOptions(),
                Order = nextOrder++
            };
            mount.BuildContext = new BuildContext {
                MountId = id,
                Theme = mount.Options.Theme ?? MaximumTheme.MetroLight,
                StateStore = mount.StateStore,
                Query = mount.QueryStore,
                Navigation = mount.Navigation,
                Shortcuts = mount.Shortcuts,
                Dialogs = mount.Dialogs
            };
            mount.RenderContext = new RenderContext {
                MountId = id,
                Theme = mount.BuildContext.Theme,
                StateStore = mount.StateStore,
                Query = mount.QueryStore,
                Navigation = mount.Navigation,
                Dialogs = mount.Dialogs,
                CacheLayout = true,
                SoftMouseCapture = mount.Options.SoftMouseCapture,
                SoftKeyboardCapture = mount.Options.SoftKeyboardCapture
            };
            mount.Dialogs.Theme = mount.BuildContext.Theme;
            mount.InputTarget.Available = mount.Options.Visible;
            mounts.Add(mount);
            SortMounts();
            try {
                view.OnMount(mount.BuildContext);
            } catch {
                mount.Active = false;
                mount.InputTarget.Available = false;
                mount.Renderer.ResetPendingTextFocus();
                inputRouting.Forget(mount.InputTarget);
                mounts.Remove(mount);
                snapshotDirty = true;
                mount.Renderer.Dispose();
                mount.RenderContext.ResetOverlays();
                mount.StateStore.Clear();
                throw;
            }
        }

        public static void Unmount(string id) {
            MountRecord[] snapshot = GetMountSnapshot();
            for (int i = snapshot.Length - 1; i >= 0; i--) {
                if (!snapshot[i].Active || !string.Equals(snapshot[i].Id, id, StringComparison.Ordinal)) continue;

                MountRecord mount = snapshot[i];
                mount.Active = false;
                mount.InputTarget.Available = false;
                mount.Renderer.ResetPendingTextFocus();
                inputRouting.Forget(mount.InputTarget);
                mounts.Remove(mount);
                snapshotDirty = true;
                try {
                    mount.View.OnUnmount(mount.BuildContext);
                } finally {
                    mount.Renderer.Dispose();
                    mount.RenderContext.ResetOverlays();
                    mount.StateStore.Clear();
                }
            }
        }

        public static void MarkDirty(View view) {
            for (int i = 0; i < mounts.Count; i++) {
                if (mounts[i].View == view) mounts[i].BuildSchedule.MarkDirty();
            }
        }

        public static bool IsMounted(string id) {
            return FindMount(id) != null;
        }

        public static bool IsVisible(string id) {
            MountRecord mount = FindMount(id);
            Window window = mount == null ? null : mount.CachedRoot as Window;
            return mount != null && mount.Options.Visible && (window == null || window.Visible);
        }

        // Visibility is a mount operation: hiding must relinquish its input
        // lease and popups immediately, even before the next OnGUI event.
        public static bool SetVisible(string id, bool visible) {
            MountRecord mount = FindMount(id);
            if (mount == null) return false;
            Window window = mount.CachedRoot as Window;
            bool changed = mount.Options.Visible != visible || (window != null && window.Visible != visible);
            if (!changed) return true;
            mount.Options.Visible = visible;
            mount.InputTarget.Available = visible;
            if (window != null) window.Visible = visible;
            if (visible) {
                mount.BuildSchedule.MarkDirty();
                FocusMount(mount);
            } else {
                mount.PendingWindowPromotion = false;
                unchecked { mount.CallbackEpoch++; }
                inputRouting.Forget(mount.InputTarget);
                mount.Renderer.ResetPendingTextFocus();
                mount.Renderer.CancelInput();
                mount.RenderContext.ResetOverlays();
                mount.QueryStore.BeginFrame();
                mount.Dialogs.DismissAll();
                if (ReferenceEquals(lastKeyboardOwner, mount.InputTarget)) IsAnyTextFieldFocused = false;
                IsModalOpen = FindModalMount(GetMountSnapshot()) >= 0;
            }
            return true;
        }

        private static MountRecord FindMount(string id) {
            for (int i = 0; i < mounts.Count; i++) {
                MountRecord mount = mounts[i];
                if (mount.Active && string.Equals(mount.Id, id, StringComparison.Ordinal)) return mount;
            }
            return null;
        }

        public static bool Focus(string id) {
            return FocusMount(FindMount(id));
        }

        private static bool CanFocusMount(MountRecord mount) {
            if (mount == null || !mount.Active || !mount.Options.Visible) return false;
            Window window = mount.CachedRoot as Window;
            if (window != null && (!window.Visible || !window.Enabled) && !mount.Dialogs.HasOpenDialogs) return false;
            MountRecord[] snapshot = GetMountSnapshot();
            int modal = FindModalMount(snapshot);
            return modal < 0 || ReferenceEquals(snapshot[modal], mount);
        }

        private static bool FocusMount(MountRecord mount) {
            if (!CanFocusMount(mount)) return false;
            mount.InputTarget.Available = true;
            if (!inputRouting.Focus(mount.InputTarget)) return false;
            // Mount/visibility callbacks can request focus before the first GUI
            // build tells us whether this is a window or an explicitly layered HUD.
            if (mount.CachedRoot == null) mount.PendingWindowPromotion = true;
            else PromoteWindow(mount);
            return true;
        }

        private static bool PromoteWindow(MountRecord mount) {
            Window window = mount == null ? null : mount.CachedRoot as Window;
            if (window == null || !mount.Active || !mount.Options.Visible || !window.Visible || !window.Enabled) return false;
            int index = mounts.IndexOf(mount);
            if (index < 0) return false;
            // Non-window mounts retain their explicit layer boundaries. Within
            // a desktop window layer, activating an exposed window brings it
            // above its peers even if their initial SortOrder values differ.
            bool lastWindow = index + 1 == mounts.Count || !(mounts[index + 1].CachedRoot is Window);
            if (lastWindow && mount.WindowActivation != 0) return false;
            mount.WindowActivation = ++nextWindowActivation;
            SortMounts();
            return true;
        }

        private static void ResolveWindowPromotions(MountRecord[] snapshot, bool rootKindChanged) {
            bool reorder = rootKindChanged;
            for (int i = 0; i < snapshot.Length; i++) {
                MountRecord mount = snapshot[i];
                if (!mount.PendingWindowPromotion || mount.CachedRoot == null) continue;
                mount.PendingWindowPromotion = false;
                Window window = mount.CachedRoot as Window;
                if (window == null || !window.Visible || !window.Enabled || !CanFocusMount(mount)) continue;
                mount.WindowActivation = ++nextWindowActivation;
                reorder = true;
            }
            if (reorder) SortMounts();
        }

        private static bool PromotePointerWindow(MuiEventType type) {
            if (type != MuiEventType.MouseDown || inputRouting.ModalOwner != null || inputRouting.EventOwner == null) return false;
            for (int i = 0; i < mounts.Count; i++) {
                if (ReferenceEquals(mounts[i].InputTarget, inputRouting.EventOwner)) return PromoteWindow(mounts[i]);
            }
            return false;
        }

        public static void EnqueueCallback(Action callback) {
            EnqueueOwnedCallback(renderingMount, callback);
        }

        private static void EnqueueOwnedCallback(MountRecord owner, Action callback) {
            if (callback == null) return;
            if (owner == null) pendingCallbacks.Add(callback);
            else {
                int epoch = owner.CallbackEpoch;
                pendingCallbacks.Add(delegate {
                    if (owner.Active && owner.Options.Visible && owner.CallbackEpoch == epoch) {
                        try { callback(); }
                        finally { if (owner.Active) owner.BuildSchedule.MarkDirty(); }
                    }
                });
            }
        }

        public static bool SetTheme(string id, MaximumTheme theme) {
            for (int i = 0; i < mounts.Count; i++) {
                MountRecord mount = mounts[i];
                if (!string.Equals(mount.Id, id, StringComparison.Ordinal)) continue;
                MaximumTheme next = theme ?? MaximumTheme.MetroLight;
                if (ReferenceEquals(mount.BuildContext.Theme, next)) return true;
                mount.Options.Theme = next;
                mount.BuildContext.Theme = next;
                mount.RenderContext.Theme = next;
                mount.Dialogs.Theme = next;
                mount.RenderContext.ResetOverlays();
                mount.Renderer.InvalidateLayout();
                mount.BuildSchedule.MarkDirty();
                return true;
            }
            return false;
        }

        public static void Update() {
            FlushCallbacks();
            MountRecord[] snapshot = GetMountSnapshot();
            for (int i = 0; i < snapshot.Length; i++) {
                MountRecord mount = snapshot[i];
                if (!mount.Active) continue;
                if (mount.Options.HasToggleKey && !IsAnyTextFieldFocused && !IsModalOpen && Input.GetKeyDown(mount.Options.ToggleKey))
                    SetVisible(mount.Id, !IsVisible(mount.Id));

                mount.View.Update(mount.BuildContext);
            }
        }

        public static void OnGUI() {
            frameCount = Time.frameCount;
            IsMouseOverAnyWindow = false;
            IsAnyTextFieldFocused = false;
            IsModalOpen = false;
            MountRecord[] snapshot = GetMountSnapshot();
            MountInputTarget[] routingSnapshot = inputSnapshot;
            bool rootKindChanged = false;
            for (int i = 0; i < snapshot.Length; i++) {
                MountRecord mount = snapshot[i];
                if (!mount.Active || !mount.Options.Visible) continue;

                mount.BuildContext.IsModalOpen = mount.Dialogs.HasOpenDialogs;
                if (mount.BuildSchedule.ShouldBuild(mount.Options, frameCount, mount.CachedRoot != null)) {
                    int revision = mount.BuildSchedule.Revision;
                    mount.Shortcuts.BeginBuild();
                    bool wasWindow = mount.CachedRoot is Window;
                    mount.CachedRoot = mount.View.Build(mount.BuildContext) ?? EmptyWidget.Instance;
                    Window rootWindow = mount.CachedRoot as Window;
                    rootKindChanged |= wasWindow != (rootWindow != null);
                    mount.RootWindowStateId = rootWindow == null ? null : mount.RenderContext.StateId(rootWindow, "window", "0");
                    mount.Renderer.InvalidateLayout();
                    mount.BuildSchedule.CompleteBuild(frameCount, revision);
                }
            }

            ResolveWindowPromotions(snapshot, rootKindChanged);
            if (snapshotDirty) { snapshot = GetMountSnapshot(); routingSnapshot = inputSnapshot; }

            // Builds may unmount another view or close its dialogs. Select the
            // modal only after every build has completed against this snapshot.
            int modalMount = FindModalMount(snapshot);
            // A modal owns input across all mounts and is painted above other windows.
            IsModalOpen = modalMount >= 0;
            for (int i = 0; i < snapshot.Length; i++) {
                snapshot[i].RenderContext.SuppressDialogs = modalMount >= 0 && i != modalMount;
            }
            PrepareInputRouting(snapshot, routingSnapshot, modalMount);
            // A click can activate a lower, partially exposed window. Drawing
            // and the next hit test must use the same newly activated order.
            if (snapshotDirty) {
                snapshot = GetMountSnapshot(); routingSnapshot = inputSnapshot;
                modalMount = FindModalMount(snapshot);
            }
            // Route Tab before Unity's TextField consumes it. The previous GUI event
            // already collected the focus order; rendering below refreshes that order.
            RouteNavigation();
            for (int i = 0; i < snapshot.Length; i++) {
                if (i != modalMount) RenderMount(snapshot[i], modalMount >= 0, false);
            }
            if (modalMount >= 0) RenderMount(snapshot[modalMount], true, true);
            for (int i = 0; i < snapshot.Length; i++) {
                snapshot[i].BuildContext.IsMouseOverMaximumUI = IsMouseOverAnyWindow;
                snapshot[i].InputTarget.ExplicitFocusRevision = snapshot[i].Navigation.ExplicitFocusRevision;
            }
            inputRouting.ObserveFocusRequests(routingSnapshot);
            SynchronizeKeyboardOwner();

            RouteShortcuts();
        }

        private static void RenderMount(MountRecord mount, bool modalOpen, bool ownsModal) {
            if (!mount.Active || !mount.Options.Visible) {
                mount.Renderer.ResetPendingTextFocus();
                mount.Renderer.CancelInput();
                mount.RenderContext.ResetOverlays();
                return;
            }
            mount.RenderContext.Path = string.Empty;
            mount.RenderContext.IsModalOpen = modalOpen;
            mount.RenderContext.SuppressDialogs = modalOpen && !ownsModal;
            mount.BuildContext.IsModalOpen = modalOpen;
            MountRecord previous = renderingMount;
            renderingMount = mount;
            try { mount.Renderer.Render(mount.CachedRoot ?? EmptyWidget.Instance, mount.RenderContext); }
            finally { renderingMount = previous; }
        }

        public static RuntimeMetrics GetMetrics() {
            RuntimeMetrics metrics = new RuntimeMetrics {
                FrameCount = frameCount,
                MountCount = mounts.Count,
                PendingCallbackCount = pendingCallbacks.Count,
                IsMouseOverAnyWindow = IsMouseOverAnyWindow,
                IsAnyTextFieldFocused = IsAnyTextFieldFocused,
                IsModalOpen = IsModalOpen
            };

            for (int i = 0; i < mounts.Count; i++) {
                if (mounts[i].Options != null && mounts[i].Options.Visible) metrics.VisibleMountCount++;
            }

            return metrics;
        }

        public static void FlushCallbacks() {
            pendingCallbacks.Flush(Debug.LogException);
        }

        private static void SortMounts() {
            mounts.Sort(baseMountComparer);
            int start = 0;
            while (start < mounts.Count) {
                if (!(mounts[start].CachedRoot is Window)) { start++; continue; }
                int end = start + 1;
                while (end < mounts.Count && mounts[end].CachedRoot is Window) end++;
                if (end - start > 1) mounts.Sort(start, end - start, activatedWindowComparer);
                start = end;
            }
            snapshotDirty = true;
        }

        private static MountRecord[] GetMountSnapshot() {
            if (snapshotDirty) {
                mountSnapshot = mounts.ToArray();
                inputSnapshot = new MountInputTarget[mountSnapshot.Length];
                for (int i = 0; i < mountSnapshot.Length; i++) inputSnapshot[i] = mountSnapshot[i].InputTarget;
                snapshotDirty = false;
            }
            return mountSnapshot;
        }

        private static int FindModalMount(MountRecord[] snapshot) {
            for (int i = snapshot.Length - 1; i >= 0; i--) {
                MountRecord mount = snapshot[i];
                if (!mount.Active || !mount.Options.Visible) continue;
                if (mount.Dialogs.HasOpenDialogs || WidgetVisibility.ContainsRootDialog(mount.CachedRoot, mount.RenderContext, "0")) return i;
            }
            return -1;
        }

        private static void PrepareInputRouting(MountRecord[] snapshot, MountInputTarget[] targets, int modalMount) {
            Event current = Event.current;
            MuiEventType type = RoutingEventType(current);
            MuiVec2 pointer = current == null ? new MuiVec2() : new MuiVec2(current.mousePosition.x, current.mousePosition.y);
            for (int i = 0; i < snapshot.Length; i++) {
                MountRecord mount = snapshot[i];
                MountInputTarget target = mount.InputTarget;
                Window rootWindow = mount.CachedRoot as Window;
                target.Available = mount.Active && mount.Options.Visible &&
                    (mount.Dialogs.HasOpenDialogs || (mount.CachedRoot != null && !(mount.CachedRoot is EmptyWidget) &&
                    (rootWindow == null || rootWindow.Visible)));
                target.HasModal = i == modalMount;
                target.PointerHit = target.Available && HitTestMount(mount, pointer);
                target.HasPointerCapture = mount.Renderer.HasPointerCapture;
                target.ExplicitFocusRevision = mount.Navigation.ExplicitFocusRevision;
            }
            inputRouting.Route(targets, type, current == null ? 0 : current.button,
                current != null && current.rawType == EventType.MouseUp);
            SynchronizeKeyboardOwner();
            for (int i = 0; i < snapshot.Length; i++) {
                MountRecord mount = snapshot[i];
                mount.RenderContext.SuppressInput = inputRouting.Suppress(mount.InputTarget, type);
                mount.RenderContext.OwnsKeyboardInput = ReferenceEquals(inputRouting.KeyboardOwner, mount.InputTarget);
                if (inputRouting.ModalOwner != null && !ReferenceEquals(inputRouting.ModalOwner, mount.InputTarget) &&
                    mount.InputTarget.HasPointerCapture) mount.Renderer.CancelInput();
            }
            PromotePointerWindow(type);
        }

        private static void SynchronizeKeyboardOwner() {
            if (ReferenceEquals(lastKeyboardOwner, inputRouting.KeyboardOwner)) return;
            GUI.FocusControl(null);
            lastKeyboardOwner = inputRouting.KeyboardOwner;
        }

        private static bool HitTestMount(MountRecord mount, MuiVec2 pointer) {
            Window window = mount.CachedRoot as Window;
            if (window != null) {
                if (!window.Visible) return false;
                WindowState state = null;
                bool initialized = mount.RootWindowStateId != null && mount.StateStore.TryGet(mount.RootWindowStateId, out state);
                // A newly mounted root has no query yet. Match RenderWindow's
                // initial position, minimum size, fold height and screen clamp.
                if (!initialized) state = null;
                MuiRect saved = state == null ? new MuiRect() :
                    new MuiRect(state.Rect.x, state.Rect.y, state.Rect.width, state.Rect.height);
                bool folded = state != null && state.Folded;
                MuiRect rect = MountInputRouting.RootWindowRect(window, saved, state != null && state.Initialized, folded,
                    ImguiRenderer.WindowTitleHeight(window, mount.RenderContext), Screen.width, Screen.height);
                return rect.Contains(pointer) || (!folded && mount.QueryStore.ContainsOverlayRegion(pointer));
            }
            return mount.QueryStore.ContainsVisibleWindow(pointer) || mount.QueryStore.ContainsInputRegion(pointer);
        }

        private static MuiEventType RoutingEventType(Event current) {
            if (current == null) return MuiEventType.None;
            switch (current.type) {
                case EventType.MouseDown: return MuiEventType.MouseDown;
                case EventType.MouseUp: return MuiEventType.MouseUp;
                case EventType.MouseDrag: return MuiEventType.MouseDrag;
                case EventType.MouseMove: return MuiEventType.MouseMove;
                case EventType.ScrollWheel: return MuiEventType.ScrollWheel;
                case EventType.KeyDown: return MuiEventType.KeyDown;
                case EventType.KeyUp: return MuiEventType.KeyUp;
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand: return MuiEventType.TextInput;
                case EventType.Repaint: return MuiEventType.Repaint;
                default: return MuiEventType.None;
            }
        }

        private static bool RouteNavigation() {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != KeyCode.Tab) return false;

            MountRecord[] snapshot = GetMountSnapshot();
            for (int i = snapshot.Length - 1; i >= 0; i--) {
                MountRecord mount = snapshot[i];
                if (!ReferenceEquals(inputRouting.KeyboardOwner, mount.InputTarget)) continue;
                if (!mount.Active || !mount.Options.Visible || mount.Navigation == null || mount.Navigation.Count == 0) continue;
                if (mount.RenderContext.SuppressDialogs) continue;
                if (mount.RenderContext.HasOpenPopup) return false;

                bool moved = current.shift ? mount.Navigation.FocusPrevious() : mount.Navigation.FocusNext();
                if (!moved) continue;

                if (mount.QueryStore != null) mount.QueryStore.SetCapture(false, true, false);
                GUI.FocusControl(null);
                current.Use();
                Input.ResetInputAxes();
                return true;
            }

            return false;
        }

        private static void RouteShortcuts() {
            if (IsModalOpen) return;
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;

            MuiInputEvent input = new MuiInputEvent {
                Type = MuiEventType.KeyDown,
                KeyCode = (int)current.keyCode,
                Character = current.character,
                Control = current.control,
                Shift = current.shift,
                Alt = current.alt,
                Command = current.command
            };

            bool textInputActive = IsAnyTextFieldFocused;
            MountRecord[] snapshot = GetMountSnapshot();
            for (int i = snapshot.Length - 1; i >= 0; i--) {
                MountRecord mount = snapshot[i];
                if (!mount.Active || !mount.Options.Visible || mount.Shortcuts == null) continue;

                ShortcutBinding binding = mount.Shortcuts.Resolve(input, textInputActive);
                if (binding == null) continue;

                mount.Shortcuts.MarkTriggered(binding);
                Action action = binding.Action;
                EnqueueOwnedCallback(mount, action);
                current.Use();
                Input.ResetInputAxes();
                return;
            }
        }
    }
}
