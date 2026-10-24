using System;

namespace MaximumUI {
    internal static class ScrollWheelRouting {
        // EndScrollView otherwise consumes a wheel event even when the requested
        // axis is already at its boundary. Let an enclosing scroll host use it.
        public static bool CanConsume(MuiVec2 position, MuiVec2 delta, MuiVec2 maximum) {
            return CanMove(position.X, delta.X, maximum.X) || CanMove(position.Y, delta.Y, maximum.Y);
        }

        private static bool CanMove(float position, float delta, float maximum) {
            if (float.IsNaN(delta) || float.IsInfinity(delta)) return false;
            float limit = ScrollGeometry.FiniteNonNegative(maximum);
            float value = Math.Min(ScrollGeometry.FiniteNonNegative(position), limit);
            return (delta < 0f && value > 0f) || (delta > 0f && value < limit);
        }
    }
}
