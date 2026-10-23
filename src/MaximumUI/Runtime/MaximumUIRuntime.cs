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
            public bool Dirty = true;
            public Widget CachedRoot;
            public BuildContext BuildContext;
            public RenderContext RenderContext;
        }

        private static readonly List<MountRecord> mounts = new List<MountRecord>();
        private static readonly List<Action> pendingCallbacks = new List<Action>();
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
                Options = options ?? new MountOptions()
            };
            mount.BuildContext = new BuildContext {
                MountId = id,
                Theme = mount.Options.Theme ?? MaximumTheme.ExpressionLight,
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
                Dialogs = mount.Dialogs
            };
            mounts.Add(mount);
            SortMounts();
            view.OnMount(mount.BuildContext);
        }

        public static void Unmount(string id) {
            for (int i = mounts.Count - 1; i >= 0; i--) {
                if (!string.Equals(mounts[i].Id, id, StringComparison.Ordinal)) continue;

                mounts[i].View.OnUnmount(mounts[i].BuildContext);
                mounts.RemoveAt(i);
            }
        }

        public static void MarkDirty(View view) {
            for (int i = 0; i < mounts.Count; i++) {
                if (mounts[i].View == view) mounts[i].Dirty = true;
            }
        }

        public static void EnqueueCallback(Action callback) {
            if (callback != null) pendingCallbacks.Add(callback);
        }

        public static void Update() {
            FlushCallbacks();
            for (int i = 0; i < mounts.Count; i++) {
                MountRecord mount = mounts[i];
                if (mount.Options.HasToggleKey && Input.GetKeyDown(mount.Options.ToggleKey)) {
                    mount.Options.Visible = !mount.Options.Visible;
                }

                mount.View.Update(mount.BuildContext);
            }
        }

        public static void OnGUI() {
            frameCount++;
            IsAnyTextFieldFocused = false;
            for (int i = 0; i < mounts.Count; i++) {
                MountRecord mount = mounts[i];
                if (!mount.Options.Visible) continue;

                mount.BuildContext.IsModalOpen = mount.Dialogs.HasOpenDialogs;
                if (mount.Options.BuildEveryOnGUI || mount.Dirty || mount.CachedRoot == null) {
                    mount.Shortcuts.BeginBuild();
                    mount.CachedRoot = mount.View.Build(mount.BuildContext);
                    mount.Dirty = false;
                }

                mount.RenderContext.Path = string.Empty;
                mount.RenderContext.IsModalOpen = mount.Dialogs.HasOpenDialogs;
                mount.BuildContext.IsModalOpen = mount.RenderContext.IsModalOpen;
                mount.Renderer.Render(mount.CachedRoot, mount.RenderContext);
                mount.BuildContext.IsMouseOverMaximumUI = IsMouseOverAnyWindow;
            }

            if (RouteNavigation()) return;
            RouteShortcuts();
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
            if (pendingCallbacks.Count == 0) return;

            Action[] callbacks = pendingCallbacks.ToArray();
            pendingCallbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++) {
                try {
                    callbacks[i]();
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }
        }

        private static void SortMounts() {
            mounts.Sort(delegate(MountRecord a, MountRecord b) {
                return a.Options.SortOrder.CompareTo(b.Options.SortOrder);
            });
        }

        private static bool RouteNavigation() {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != KeyCode.Tab) return false;

            for (int i = mounts.Count - 1; i >= 0; i--) {
                MountRecord mount = mounts[i];
                if (mount.Options == null || !mount.Options.Visible || mount.Navigation == null || mount.Navigation.Count == 0) continue;

                bool moved = current.shift ? mount.Navigation.FocusPrevious() : mount.Navigation.FocusNext();
                if (!moved) continue;

                if (mount.QueryStore != null) mount.QueryStore.SetCapture(false, true, false);
                current.Use();
                Input.ResetInputAxes();
                return true;
            }

            return false;
        }

        private static void RouteShortcuts() {
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
            for (int i = mounts.Count - 1; i >= 0; i--) {
                MountRecord mount = mounts[i];
                if (mount.Options == null || !mount.Options.Visible || mount.Shortcuts == null) continue;

                ShortcutBinding binding = mount.Shortcuts.Resolve(input, textInputActive);
                if (binding == null) continue;

                mount.Shortcuts.MarkTriggered(binding);
                Action action = binding.Action;
                EnqueueCallback(action);
                current.Use();
                Input.ResetInputAxes();
                return;
            }
        }
    }
}
