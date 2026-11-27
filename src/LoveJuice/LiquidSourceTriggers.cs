using System;

namespace LoveJuice {
    // Pure decision logic shared by the actual scene runtime and managed tests.
    internal static class LiquidSourceTriggers {
        internal static bool KissAnimationSignal(string name, bool nativeMouthClick) {
            if (nativeMouthClick) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("kiss", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("kisu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("キス", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("ベロ", StringComparison.Ordinal) >= 0;
        }

        // Oral contact, visible drool or explicit preview should produce
        // saliva without requiring a kissing animation. The installed Cyu
        // original is used only when it is actually managing a kiss string.
        internal static bool SalivaActive(bool kissing, bool oralScene,
            bool wetFace, bool explicitPreview, bool allowWithoutKiss) {
            return kissing || explicitPreview || (allowWithoutKiss &&
                (oralScene || wetFace));
        }

        internal static bool DrawSalivaFilament(bool enabled,
            bool activity, float distance, bool visibleNativeKissString) {
            if (!enabled || !activity || visibleNativeKissString ||
                float.IsNaN(distance) || float.IsInfinity(distance))
                return false;
            return distance > .001f && distance < .15f;
        }

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
