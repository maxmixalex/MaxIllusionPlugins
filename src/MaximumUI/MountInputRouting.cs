using System;
using System.Collections.Generic;

namespace MaximumUI {
    // One retained target per mount. Runtime refreshes these facts before each
    // GUI event; routing never allocates or clones a query/window snapshot.
    internal sealed class MountInputTarget {
        internal bool Available;
        internal bool HasModal;
        internal bool PointerHit;
        internal bool HasPointerCapture;
        internal int ExplicitFocusRevision;
        internal int ObservedFocusRevision;
    }

    internal sealed class MountInputRouting {
        private MountInputTarget keyboardOwner;
        private MountInputTarget pointerOwner;
        private MountInputTarget modalOwner;
        private bool keyboardDismissed;
        private int heldButton = -1;

        internal MountInputTarget EventOwner { get; private set; }
        internal MountInputTarget KeyboardOwner { get { return modalOwner ?? keyboardOwner; } }
        internal MountInputTarget PointerOwner { get { return pointerOwner; } }
        internal MountInputTarget ModalOwner { get { return modalOwner; } }

        internal bool Focus(MountInputTarget target) {
            if (target == null || !target.Available) return false;
            keyboardOwner = target;
            keyboardDismissed = false;
            return true;
        }

        internal void Forget(MountInputTarget target) {
            if (ReferenceEquals(keyboardOwner, target)) keyboardOwner = null;
            if (ReferenceEquals(modalOwner, target)) modalOwner = null;
            if (ReferenceEquals(pointerOwner, target)) {
                pointerOwner = null;
                heldButton = -1;
            }
            if (ReferenceEquals(EventOwner, target)) EventOwner = null;
        }

        internal void Route(IList<MountInputTarget> orderedTargets, MuiEventType type, int button, bool rawMouseUp = false) {
            ValidateOwners(orderedTargets);
            ObserveFocusRequests(orderedTargets);
            if (keyboardOwner == null && !keyboardDismissed) keyboardOwner = TopAvailable(orderedTargets, false);
            modalOwner = TopAvailable(orderedTargets, true);

            if (modalOwner != null) {
                if (!ReferenceEquals(pointerOwner, modalOwner)) {
                    pointerOwner = null;
                    heldButton = -1;
                }
                EventOwner = modalOwner;
                if (type == MuiEventType.MouseUp || rawMouseUp) ReleasePointer(button);
                return;
            }

            if (!IsPointerEvent(type)) {
                EventOwner = KeyboardOwner;
                // Another OnGUI host may already have consumed the release.
                // Return the lease without reactivating its consumed event.
                if (rawMouseUp) ReleasePointer(button);
                return;
            }

            if (pointerOwner == null) {
                for (int i = orderedTargets.Count - 1; i >= 0; i--) {
                    MountInputTarget target = orderedTargets[i];
                    if (target.Available && target.HasPointerCapture) { pointerOwner = target; break; }
                }
            }

            MountInputTarget hit = null;
            for (int i = orderedTargets.Count - 1; i >= 0; i--) {
                MountInputTarget target = orderedTargets[i];
                if (target.Available && target.PointerHit) { hit = target; break; }
            }
            EventOwner = pointerOwner ?? hit;

            if (type == MuiEventType.MouseDown) {
                if (pointerOwner == null) {
                    keyboardOwner = hit;
                    keyboardDismissed = hit == null;
                    pointerOwner = hit;
                    heldButton = hit == null ? -1 : button;
                }
            } else if (type == MuiEventType.MouseUp || rawMouseUp) {
                // Keep EventOwner for this release event, then relinquish the
                // pointer lease. The renderer releases its own hotControl.
                ReleasePointer(button);
            }
        }

        private void ReleasePointer(int button) {
            if (heldButton >= 0 && heldButton != button) return;
            pointerOwner = null;
            heldButton = -1;
        }

        internal void ObserveFocusRequests(IList<MountInputTarget> orderedTargets) {
            for (int i = 0; i < orderedTargets.Count; i++) {
                MountInputTarget target = orderedTargets[i];
                if (target.ObservedFocusRevision == target.ExplicitFocusRevision) continue;
                target.ObservedFocusRevision = target.ExplicitFocusRevision;
                if (target.Available) Focus(target);
            }
        }

        internal bool Suppress(MountInputTarget target, MuiEventType type) {
            return IsInputEvent(type) && !ReferenceEquals(EventOwner, target);
        }

        internal static bool IsInputEvent(MuiEventType type) {
            return IsPointerEvent(type) || type == MuiEventType.KeyDown ||
                type == MuiEventType.KeyUp || type == MuiEventType.TextInput;
        }

        private static bool IsPointerEvent(MuiEventType type) {
            return type == MuiEventType.MouseDown || type == MuiEventType.MouseUp ||
                type == MuiEventType.MouseDrag || type == MuiEventType.MouseMove || type == MuiEventType.ScrollWheel;
        }

        private void ValidateOwners(IList<MountInputTarget> targets) {
            if (!ContainsAvailable(targets, keyboardOwner)) keyboardOwner = null;
            if (!ContainsAvailable(targets, pointerOwner) ||
                (heldButton < 0 && pointerOwner != null && !pointerOwner.HasPointerCapture)) {
                pointerOwner = null;
                heldButton = -1;
            }
        }

        private static bool ContainsAvailable(IList<MountInputTarget> targets, MountInputTarget value) {
            if (value == null || !value.Available) return false;
            for (int i = 0; i < targets.Count; i++) if (ReferenceEquals(targets[i], value)) return true;
            return false;
        }

        private static MountInputTarget TopAvailable(IList<MountInputTarget> targets, bool modalOnly) {
            for (int i = targets.Count - 1; i >= 0; i--) {
                MountInputTarget target = targets[i];
                if (target.Available && (!modalOnly || target.HasModal)) return target;
            }
            return null;
        }

        internal static MuiRect RootWindowRect(Window window, MuiRect saved, bool initialized, bool folded,
            float titleHeight, float screenWidth, float screenHeight) {
            MuiRect rect = initialized ? saved : new MuiRect(80f, 60f, window.Width, window.Height);
            rect.Width = Math.Max(rect.Width, window.MinWidth);
            rect.Height = folded ? titleHeight : Math.Max(rect.Height, window.MinHeight);
            return WindowGeometry.Clamp(rect, screenWidth, screenHeight, window.MinWidth,
                folded ? titleHeight : window.MinHeight);
        }
    }
}
