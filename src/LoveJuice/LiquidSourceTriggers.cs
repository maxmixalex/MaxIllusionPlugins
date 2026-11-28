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

        // Stable 0.55–0.95s physical tear cycle. Both endpoints may remain
        // still while the filament continues to sag and shed droplets.
        // Tube radius varies along the actual 3-D centreline: bulbous
        // near roots, a fluid neck before the tear separates and an
        // extremely thin end cap. Never a flat alpha-textured ribbon.
        internal static float SalivaRadius(float t, float phase,
            float strength) {
            t = Math.Max(0f, Math.Min(1f, t));
            phase = Math.Max(0f, Math.Min(1f, phase));
            strength = Math.Max(0f, Math.Min(1f, strength));
            float root = (float)Math.Sin(Math.PI * t);
            float taper = .00025f + .0035f * Math.Max(.0f, root);
            float neck = 1f - .83f * (float)Math.Pow(phase, 4f) *
                (float)Math.Pow(t, 3f);
            return taper * Math.Max(.08f, neck) * strength;
        }

        internal static float SalivaTearPeriod(float mouthDistance) {
            if (float.IsNaN(mouthDistance) ||
                float.IsInfinity(mouthDistance)) return .72f;
            return Math.Max(.55f, Math.Min(.95f, .55f + mouthDistance * 2.4f));
        }

        // A single-ended mouth filament is NOT tied to a stationary point
        // below the face. It grows under gravity, necks down, then separates.
        // The detached drop is integrated independently by SalivaFilament.
        internal static float SalivaHangLength(float phase) {
            if (float.IsNaN(phase) || float.IsInfinity(phase))
                return .012f;
            phase = Math.Max(0f, Math.Min(1f, phase));
            return .012f + .044f * (float)Math.Pow(phase, 1.4f);
        }

        internal static float SalivaSag(float distance, float seconds, float phase) {
            if (float.IsNaN(distance) || float.IsInfinity(distance) ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) ||
                float.IsNaN(phase) || float.IsInfinity(phase))
                return .009f;
            float baseSag = Math.Max(.008f, Math.Min(.022f, distance * .13f));
            float wave = (float)Math.Sin(seconds * 5.5f) * .0035f;
            float tearStretch = Math.Max(0f, Math.Min(1f, phase));
            return Math.Max(.003f, baseSag + wave + .010f * tearStretch * tearStretch);
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
