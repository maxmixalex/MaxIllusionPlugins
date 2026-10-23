using System;
using UnityEngine;

namespace MaximumUI {
    /// <summary>
    /// Optional externally owned scroll position. Multiple ScrollView or
    /// VirtualList widgets can share it; a shorter pane clamps only its own
    /// displayed position and never pulls a longer sibling back to the top.
    /// </summary>
    public sealed class ScrollPosition {
        public Vector2 Position;

        public void Reset() {
            Position = new Vector2(0f, 0f);
        }

        internal Vector2 Resolve(MuiRect viewport, MuiRect content, bool horizontal, bool vertical) {
            MuiRect visible = ScrollGeometry.ContentViewport(viewport, content, horizontal, vertical, 16f);
            return new Vector2 { x = Clamp(Position.x, content.Width - visible.Width),
                y = Clamp(Position.y, content.Height - visible.Height) };
        }

        internal void Publish(Vector2 localBefore, Vector2 localAfter, bool acceptsInput) {
            if (!acceptsInput || (localBefore.x == localAfter.x && localBefore.y == localAfter.y)) return;
            // Unity may deliver a wheel movement on the GUI event after the
            // wheel was consumed. Compare positions rather than event types.
            Position = new Vector2 { x = FiniteNonNegative(localAfter.x), y = FiniteNonNegative(localAfter.y) };
        }

        private static float Clamp(float value, float maximum) {
            return Math.Min(FiniteNonNegative(value), FiniteNonNegative(maximum));
        }

        private static float FiniteNonNegative(float value) {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        }
    }
}
