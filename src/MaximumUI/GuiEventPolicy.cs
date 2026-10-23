using UnityEngine;

namespace MaximumUI {
    internal static class GuiEventPolicy {
        public static bool ShouldPaint(EventType type) {
            return type == EventType.Repaint;
        }

        public static bool ShouldReleasePointer(EventType type, int button) {
            return type == EventType.MouseUp && button == 0;
        }
    }
}
