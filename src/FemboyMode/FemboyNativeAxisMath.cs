using System;

namespace FemboyMode
{
    // Pure conversion for native CustomGuideObject movement and rotation.
    // The visuals and pointer handling come from the original Maker scripts;
    // only the mapping into the card-local model transform is plugin-owned.
    internal static class FemboyNativeAxisMath
    {
        private static float Clamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        internal static float Moved(float start, float nativeMeters, bool scaling, float precision)
        {
            float neutral = scaling ? 1f : 0f;
            float minimum = scaling ? .25f : -.30f;
            float maximum = scaling ? 4f : .30f;
            if (float.IsNaN(nativeMeters) || float.IsInfinity(nativeMeters) ||
                float.IsNaN(precision) || float.IsInfinity(precision))
                return Clamp(start, minimum, maximum, neutral);
            float change = nativeMeters * precision * (scaling ? 3f : 1f);
            return Clamp(start + change, minimum, maximum, neutral);
        }

        internal static float Rotated(float start, float nativeDegrees, float precision)
        {
            if (float.IsNaN(nativeDegrees) || float.IsInfinity(nativeDegrees) ||
                float.IsNaN(precision) || float.IsInfinity(precision))
                return Clamp(start, -360f, 360f, 0f);
            return Clamp(start + nativeDegrees * precision, -360f, 360f, 0f);
        }
    }
}
