namespace MaximumUI {
    public sealed class BuildContext {
        public string MountId;
        public MaximumTheme Theme;
        public UiStateStore StateStore;
        public UiQueryStore Query;
        public UiNavigationStore Navigation;
        public UiShortcutRouter Shortcuts;
        public DialogService Dialogs;
        public bool IsModalOpen;
        public bool IsMouseOverMaximumUI;

        public bool IsHovered(object key) {
            return Query != null && Query.IsHovered(key);
        }

        public bool IsActive(object key) {
            return Query != null && Query.IsActive(key);
        }

        public bool IsFocused(object key) {
            return Query != null && Query.IsFocused(key);
        }

        public bool IsClicked(object key) {
            return Query != null && Query.IsClicked(key);
        }

        public bool IsVisible(object key) {
            return Query != null && Query.IsVisible(key);
        }

        public bool IsWindowHovered(object key) {
            return Query != null && Query.IsWindowHovered(key);
        }

        public ShortcutBinding RegisterShortcut(string id, ShortcutChord chord, System.Action action, bool enabled = true, bool allowWhenTextInput = false, object owner = null) {
            if (Shortcuts == null) return null;
            return Shortcuts.Register(id, chord, action, enabled, allowWhenTextInput, owner);
        }

        public bool Focus(object key) {
            return Navigation != null && Navigation.Focus(key);
        }

        public bool FocusPath(string path) {
            return Navigation != null && Navigation.FocusPath(path);
        }
    }
}
