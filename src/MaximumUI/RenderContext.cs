using System.Collections.Generic;
using UnityEngine;

namespace MaximumUI {
    public sealed class RenderContext {
        private readonly List<DropdownOverlay> dropdowns = new List<DropdownOverlay>();
        private readonly List<ComboBoxState> comboBoxStates = new List<ComboBoxState>();

        public string MountId;
        public MaximumTheme Theme;
        public UiStateStore StateStore;
        public UiQueryStore Query;
        public UiNavigationStore Navigation;
        public OverlayLayer Overlay = new OverlayLayer();
        public DialogService Dialogs;
        public string Path;
        public bool IsModalOpen;
        // Opt in only when the caller invalidates ImguiRenderer after mutating widgets.
        public bool CacheLayout;
        public bool SoftMouseCapture = true;
        public bool SoftKeyboardCapture;
        internal bool SuppressDialogs;
        internal Dialog InputDialog;
        public DragDropState DragDrop = new DragDropState();

        internal IList<DropdownOverlay> Dropdowns {
            get { return dropdowns; }
        }

        public IList<ComboBoxState> ComboBoxStates {
            get { return comboBoxStates; }
        }

        public string StateId(Widget widget, string suffix) {
            return StateId(widget, suffix, Path);
        }

        internal string StateId(Widget widget, string suffix, string path) {
            string keyPart = widget != null && widget.Key != null ? "key:" + widget.Key : "path:" + path;
            return MountId + ":" + keyPart + ":" + suffix;
        }

        internal void RegisterDropdown(DropdownOverlay dropdown) {
            if (dropdown != null && !dropdowns.Contains(dropdown)) dropdowns.Add(dropdown);
        }

        public void RegisterComboBox(ComboBoxState state) {
            if (state == null || state.Dropdown == null) return;
            RegisterDropdown(state.Dropdown);
            if (!comboBoxStates.Contains(state)) comboBoxStates.Add(state);
        }

        public void HideOtherComboBoxes(ComboBoxState activeState) {
            for (int i = 0; i < comboBoxStates.Count; i++) {
                ComboBoxState state = comboBoxStates[i];
                if (state == activeState || state.Dropdown == null) continue;
                state.Dropdown.Hide();
                state.Open = false;
            }
        }
    }

    public sealed class WindowState {
        public Rect Rect;
        public bool Initialized;
        public bool Folded;
        public DragState Drag = new DragState();
    }

    public sealed class ComboBoxState {
        public bool Open;
        public Vector2 ScrollPosition;
        internal DropdownOverlay Dropdown;
        public Rect ButtonRect;
        public bool HasButtonRect;
        public string WindowStateId;
        public string ControlName;
        public string FilterControlName;
        public string FilterText = string.Empty;
        public bool Searchable;
        public string FilterPlaceholder = string.Empty;
        public string[] Items;
        public System.Action<int> Callback;
        public int MaxVisibleItems = 8;
        internal Dialog ModalDialog;
    }

    internal sealed class ContextMenuState {
        public bool Open;
        public Rect Rect;
        public Vector2 Position;
    }

    internal sealed class MenuBarState {
        public bool Open;
        public int OpenIndex = -1;
        public Rect Anchor;
        public Rect PopupRect;
    }

    internal sealed class TabsState {
        public bool Dragging;
        public int DragFrom = -1;
        public Vector2 DragStart;

        public void ClearDrag() {
            Dragging = false;
            DragFrom = -1;
            DragStart = Vector2.zero;
        }
    }

    public sealed class DragDropState {
        public bool Active;
        public string SourceId;
        public DragPayload Payload;
        public Vector2 StartPosition;
        public Vector2 CurrentPosition;

        public void Clear() {
            Active = false;
            SourceId = null;
            Payload = null;
            StartPosition = Vector2.zero;
            CurrentPosition = Vector2.zero;
        }
    }
}
