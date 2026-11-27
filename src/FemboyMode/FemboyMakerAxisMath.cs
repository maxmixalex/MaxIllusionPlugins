using System;

namespace FemboyMode
{
    // Pure drag math shared by the live native UGUI gizmo and headless tests.
    internal static class FemboyMakerAxisMath
    {
        internal static float DragValue(float start, float projectedPixels, int mode,
            bool fine, bool coarse)
        {
            if (float.IsNaN(start) || float.IsInfinity(start) ||
                float.IsNaN(projectedPixels) || float.IsInfinity(projectedPixels))
                return mode == 0 ? 1f : 0f;
            float step = mode == 0 ? .016f : mode == 1 ? .0015f : .85f;
            float precision = fine ? .2f : coarse ? 3f : 1f;
            double next = start + (double)projectedPixels * step * precision;
            double minimum = mode == 0 ? .25 : mode == 1 ? -.30 : -360;
            double maximum = mode == 0 ? 4 : mode == 1 ? .30 : 360;
            return (float)Math.Max(minimum, Math.Min(maximum, next));
        }
    }
}
