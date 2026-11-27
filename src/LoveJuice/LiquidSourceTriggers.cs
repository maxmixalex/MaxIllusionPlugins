using System;

namespace LoveJuice {
    // Pure decision logic shared by the actual scene runtime and managed tests.
    internal static class LiquidSourceTriggers {
        internal static int MalePreLevel(int sex, bool activeLoop, bool isPrimary,
            float gauge, float threshold) {
            if (sex != 0 || !activeLoop || !isPrimary ||
                float.IsNaN(gauge) || float.IsInfinity(gauge) ||
                float.IsNaN(threshold) || float.IsInfinity(threshold)) return 0;
            float minimum = Math.Max(0f, Math.Min(100f, threshold));
            if (gauge < minimum) return 0;
            // This is an opt-in proxy from the native *male* gauge, not a
            // fabricated readback level or a frame-rate-dependent event.
            return Math.Max(1, Math.Min(3, (int)Math.Round(gauge / 33f,
                MidpointRounding.AwayFromZero)));
        }
    }
}
