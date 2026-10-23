using UnityEngine;

namespace MaximumUI {
    internal static class GuiEventPolicy {
        public static bool ShouldPaint(EventType type) {
            return type == EventType.Repaint;
        }

        public static bool ControlEnabled(bool parentEnabled, bool widgetEnabled, bool inputBlocked, EventType type) {
            // A popup owns input without changing the underlying model's enabled
            // state. Repaint must preserve that state; input events stay blocked.
            return parentEnabled && widgetEnabled && (!inputBlocked || ShouldPaint(type));
        }

        public static bool ShouldReleasePointer(EventType type, int button) {
            return type == EventType.MouseUp && button == 0;
        }
    }
}
