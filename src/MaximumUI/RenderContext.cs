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
        internal bool SuppressInput;
        internal bool OwnsKeyboardInput = true;
        internal Dialog InputDialog;
        internal ContextMenuState ActiveContextMenu;
        internal MenuBarState ActiveMenuBar;
        public DragDropState DragDrop = new DragDropState();

        internal IList<DropdownOverlay> Dropdowns {
            get { return dropdowns; }
        }

        public IList<ComboBoxState> ComboBoxStates {
            get { return comboBoxStates; }
        }

        internal bool HasOpenDropdown {
            get {
                for (int i = 0; i < comboBoxStates.Count; i++)
                    if (comboBoxStates[i].Dropdown != null && comboBoxStates[i].Dropdown.Show) return true;
                return false;
            }
        }

        internal bool HasOpenPopup {
            get { return HasOpenDropdown || (ActiveContextMenu != null && ActiveContextMenu.Open) ||
                (ActiveMenuBar != null && ActiveMenuBar.Open); }
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

        internal void ResetOverlays() {
            CloseMenus();
            for (int i = 0; i < comboBoxStates.Count; i++) {
                ComboBoxState state = comboBoxStates[i];
                if (state.Dropdown != null) state.Dropdown.Hide();
                state.Dropdown = null;
                state.Open = false;
            }
            comboBoxStates.Clear();
            dropdowns.Clear();
            Overlay.Clear();
            InputDialog = null;
            DragDrop.Clear();
        }

        internal void CloseMenus() {
            if (ActiveContextMenu != null) ActiveContextMenu.Open = false;
            if (ActiveMenuBar != null) {
                ActiveMenuBar.Open = false;
                ActiveMenuBar.OpenIndex = -1;
                ActiveMenuBar.SubmenuPath.Clear();
            }
            ActiveContextMenu = null;
            ActiveMenuBar = null;
        }

        internal void PruneClosedComboBoxes() {
            for (int i = comboBoxStates.Count - 1; i >= 0; i--) {
                ComboBoxState state = comboBoxStates[i];
                if (state.Dropdown != null && state.Dropdown.Show) continue;
                if (state.Dropdown != null) dropdowns.Remove(state.Dropdown);
                comboBoxStates.RemoveAt(i);
            }
        }
    }

    public sealed class WindowState {
        public Rect Rect;
        public bool Initialized;
        public bool Folded;
        public DragState Drag = new DragState();
        internal bool Resizing;
        internal Rect ResizeStart;
        internal Vector2 ResizeMouseStart;
        internal float ExpandedHeight;
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
        internal readonly ChoiceCache Choices = new ChoiceCache();
        internal IComboBoxWidget Owner;
        internal System.Func<object, string> Formatter;
        internal System.Action<string> FilterChanged;
    }

    internal sealed class ListBoxState {
        public Vector2 Position;
        public readonly ChoiceCache Choices = new ChoiceCache();
        public IListBoxWidget Owner;
        public System.Func<object, string> Formatter;
    }

    internal sealed class VirtualListState {
        public Vector2 Position;
        public readonly VirtualListCache Rows = new VirtualListCache();
    }

    internal sealed class ContextMenuState {
        public bool Open;
        public Rect Rect;
        public Vector2 Position;
        public bool Seen;
        public Dialog ModalDialog;
    }

    internal sealed class MenuBarState {
        public bool Open;
        public int OpenIndex = -1;
        public Rect Anchor;
        public Rect PopupRect;
        public bool Seen;
        public Dialog ModalDialog;
        public readonly List<int> SubmenuPath = new List<int>();
    }

    internal sealed class TabsState {
        internal Tabs Owner;
        internal int Revision;
        internal readonly List<Tab> VisibleTabs = new List<Tab>();
        internal readonly List<TabItemButton> LeadingButtons = new List<TabItemButton>();
        internal readonly List<TabItemButton> TrailingButtons = new List<TabItemButton>();
        public bool Dragging;
        public int DragFrom = -1;
        public MuiVec2 DragStart;

        public void BeginDrag(int index, MuiVec2 position) {
            DragFrom = index;
            DragStart = position;
            Dragging = false;
        }

        public void MoveDrag(MuiVec2 position) {
            float x = position.X - DragStart.X;
            float y = position.Y - DragStart.Y;
            if (DragFrom >= 0 && x * x + y * y > 36f) Dragging = true;
        }

        public bool CompleteDrag(int target, bool allowed, out int from, out int to) {
            from = DragFrom;
            to = target;
            bool result = allowed && Dragging && from >= 0 && to >= 0 && from != to;
            ClearDrag();
            return result;
        }

        public void ClearDrag() {
            Dragging = false;
            DragFrom = -1;
            DragStart = new MuiVec2();
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
            StartPosition = default(Vector2);
            CurrentPosition = default(Vector2);
        }
    }
}
