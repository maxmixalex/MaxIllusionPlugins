using System;

namespace FemboyMode
{
    internal static class MakerPageScrollMath
    {
        internal static float ResolveHeight(float top, float bottom, float fallback, float margin)
        {
            if (float.IsNaN(fallback) || float.IsInfinity(fallback) || fallback <= 0f) fallback = 160f;
            if (float.IsNaN(top) || float.IsInfinity(top) || float.IsNaN(bottom) ||
                float.IsInfinity(bottom) || bottom >= top) return fallback;
            if (float.IsNaN(margin) || float.IsInfinity(margin) || margin < 0f) margin = 0f;
            return Math.Max(1f, top - bottom - margin);
        }
    }
}
