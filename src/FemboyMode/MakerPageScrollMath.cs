using System;

namespace FemboyMode
{
    internal static class MakerPageScrollMath
    {
        internal const float DefaultViewportHeight = 520f;

        internal static float ViewportFromScreen(float topPixels, float screenHeight,
            float scaleFactor, float bottomMarginPixels)
        {
            if (!Finite(topPixels) || !Finite(screenHeight) || !Finite(scaleFactor) ||
                !Finite(bottomMarginPixels) || screenHeight <= 0f ||
                topPixels <= 0f || topPixels > screenHeight * 1.5f ||
                scaleFactor < .25f || bottomMarginPixels < 0f)
                return DefaultViewportHeight;
            float height = (topPixels - bottomMarginPixels) / scaleFactor;
            if (!Finite(height) || height < 160f) return DefaultViewportHeight;
            return Clamp(height, 160f, 900f);
        }

        // Use the actual game's scrollbar art width instead of a hard-coded
        // skinny 14px rail. Only fall back when the stock RectTransform has
        // not yet received a valid layout pass (inactive Maker tab).
        internal static float NativeScrollbarWidth(float rectWidth,
            float sizeDeltaWidth, float parentScaleRatio)
        {
            float candidate = Math.Abs(rectWidth);
            if (!Finite(candidate) || candidate < 12f || candidate > 64f)
                candidate = Math.Abs(sizeDeltaWidth);
            if (!Finite(candidate) || candidate < 12f || candidate > 64f)
                candidate = 26f;
            if (!Finite(parentScaleRatio) || parentScaleRatio <= 0f)
                parentScaleRatio = 1f;
            return Clamp(candidate * parentScaleRatio, 18f, 64f);
        }

        // Wheel distances are immediate UI units, never smoothed over time.
        internal static float WheelDistance(float wheelDelta, float step)
        {
            if (!Finite(wheelDelta) || !Finite(step) || step <= 0f) return 0f;
            return Clamp(wheelDelta, -12f, 12f) * Clamp(step, 40f, 320f);
        }

        internal static float MaxOffset(float preferredContentHeight, float viewportHeight)
        {
            if (!Finite(preferredContentHeight) || !Finite(viewportHeight) ||
                preferredContentHeight < 0f || preferredContentHeight > 100000f ||
                viewportHeight < 1f)
                return 0f;
            return Math.Max(0f, preferredContentHeight - viewportHeight);
        }

        internal static bool Intersects(float minA, float maxA, float minB, float maxB)
        {
            if (!Finite(minA) || !Finite(maxA) || !Finite(minB) || !Finite(maxB) ||
                minA > maxA || minB > maxB) return false;
            return maxA > minB && maxB > minA;
        }

        internal static float ClampOffset(float offset, float maximumOffset)
        {
            if (!Finite(offset) || !Finite(maximumOffset) || maximumOffset <= 0f) return 0f;
            return Clamp(offset, 0f, maximumOffset);
        }

        internal static float ScrollbarSize(float viewportHeight, float maxOffset)
        {
            if (!Finite(viewportHeight) || !Finite(maxOffset) || viewportHeight < 1f ||
                maxOffset <= .01f) return 1f;
            return Clamp(viewportHeight / (viewportHeight + maxOffset), 0f, 1f);
        }

        internal static float ScrollbarValue(float offset, float maximumOffset)
        {
            if (!Finite(maximumOffset) || maximumOffset <= .01f) return 1f;
            return 1f - ClampOffset(offset, maximumOffset) / maximumOffset;
        }

        internal static float FromScrollbar(float value, float maximumOffset)
        {
            if (!Finite(value) || !Finite(maximumOffset) || maximumOffset <= 0f) return 0f;
            return (1f - Clamp(value, 0f, 1f)) * maximumOffset;
        }

        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static float Clamp(float v, float min, float max) {
            return Math.Max(min, Math.Min(max, v));
        }
    }
}
