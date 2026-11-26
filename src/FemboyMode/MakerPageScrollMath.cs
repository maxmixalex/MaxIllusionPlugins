using System;

namespace FemboyMode
{
    internal static class MakerPageScrollMath
    {
        // A content-driven layout cannot be used as its own viewport.
        internal static bool IsStableViewport(bool hasActiveMask, bool contentDriven, float height)
        {
            return hasActiveMask && !contentDriven && !float.IsNaN(height) &&
                !float.IsInfinity(height) && height >= 96f;
        }

        internal static float ResolveHeight(float top, float bottom, float fallback, float margin)
        {
            if (float.IsNaN(fallback) || float.IsInfinity(fallback) || fallback <= 0f) fallback = 160f;
            if (float.IsNaN(top) || float.IsInfinity(top) || float.IsNaN(bottom) ||
                float.IsInfinity(bottom) || bottom >= top) return fallback;
            if (float.IsNaN(margin) || float.IsInfinity(margin) || margin < 0f) margin = 0f;
            float measured = top - bottom - margin;
            // Do not shrink the native Maker page into a 1px scroll area.
            if (float.IsNaN(measured) || float.IsInfinity(measured) || measured < 96f)
                return fallback;
            return measured;
        }
    }
}
