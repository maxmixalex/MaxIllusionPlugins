using System;

namespace HSControl
{
    internal enum SpeedMode { Off = 0, Bands = 1, Range = 2, GaugeLink = 3 }

    // Standalone state machine: independent of Unity or the original KK_HCtrl.
    // Driven by scene state; it never modifies a character card or save file.
    internal sealed class HSControlSettings
    {
        internal int Mode, Band, Linkage;
        internal float RangeMin = 0f, RangeMax = 1f;
        internal bool AutoStrength, ManualOverride, AutoMaleFinish, AutoFemaleFinish = true, AutoReplay;
        internal float ManualSpeed = .5f;
        internal float StrengthMinSeconds = 10f, StrengthMaxSeconds = 30f;
        internal float TargetMinSeconds = 1.5f, TargetMaxSeconds = 4.5f;

        internal HSControlSettings Copy() {
            return (HSControlSettings)MemberwiseClone();
        }

        internal static float Clamp(float value, float low, float high) {
            if (float.IsNaN(value) || float.IsInfinity(value)) return low;
            return Math.Max(low, Math.Min(high, value));
        }

        internal void Sanitize() {
            Mode = Math.Max(0, Math.Min(3, Mode));
            Band = Math.Max(0, Math.Min(2, Band));
            Linkage = Math.Max(0, Math.Min(1, Linkage));
            RangeMin = Clamp(RangeMin, 0f, 1f);
            RangeMax = Clamp(RangeMax, 0f, 1f);
            if (RangeMin > RangeMax) { float swap = RangeMin; RangeMin = RangeMax; RangeMax = swap; }
            ManualSpeed = Clamp(ManualSpeed, 0f, 1f);
            StrengthMinSeconds = Clamp(StrengthMinSeconds, 1f, 120f);
            StrengthMaxSeconds = Clamp(StrengthMaxSeconds, StrengthMinSeconds, 120f);
            TargetMinSeconds = Clamp(TargetMinSeconds, .2f, 15f);
            TargetMaxSeconds = Clamp(TargetMaxSeconds, TargetMinSeconds, 20f);
        }
    }

    internal sealed class HSControlMotionEngine
    {
        private readonly Random random;
        private float current, target, timer, strengthTimer, frequency, amplitude, easing;
        private float elapsed;
        private bool highBand;
        private int lastMode = -1, lastBand = -1;
        private float lastRangeMin = -1f, lastRangeMax = -1f;
        private bool initialized;

        internal bool Driving { get; private set; }
        internal bool StrengthToggleRequested { get; private set; }
        internal float Current { get { return current; } }
        internal float Target { get { return target; } }

        internal HSControlMotionEngine(int seed) {
            random = new Random(seed);
        }

        private float Next(float min, float max) {
            return min + (float)random.NextDouble() * (max - min);
        }

        internal void Reset(float startingSpeed) {
            current = target = HSControlSettings.Clamp(startingSpeed, 0f, 1f);
            timer = strengthTimer = elapsed = 0f;
            frequency = .7f; amplitude = .05f; easing = 1.5f;
            highBand = false;
            lastMode = lastBand = -1;
            lastRangeMin = lastRangeMax = -1f;
            initialized = true;
            Driving = StrengthToggleRequested = false;
        }

        internal static bool IsSupportedMode(int mode) {
            return mode == 0 || mode == 1 || mode == 2 || mode == 6 ||
                   mode == 7 || mode == 8 || mode == 9;
        }

        internal static bool CanDrive(int mode, string animState, bool rely) {
            if (string.IsNullOrEmpty(animState) || !IsSupportedMode(mode)) return false;
            if (mode == 0) return animState.IndexOf("_Idle", StringComparison.Ordinal) >= 0;
            if ((mode == 1 || mode == 6 || mode == 8) && rely) return false;
            return animState.IndexOf("WLoop", StringComparison.Ordinal) >= 0 ||
                   animState.IndexOf("SLoop", StringComparison.Ordinal) >= 0;
        }

        internal static float LinkedTarget(int mode, int linkage, float male, float female) {
            male = HSControlSettings.Clamp(male, 0f, 100f);
            female = HSControlSettings.Clamp(female, 0f, 100f);
            if (mode == 0) return female / 100f;
            if (mode == 1 || mode == 6 || mode == 8) return HSControlSettings.Clamp(male / 70f, 0f, 1f);
            if (mode == 2 || mode == 7 || mode == 9) {
                float m = male / 100f, f = female / 100f;
                return linkage == 1 ? Math.Max(m, f) : (m + f) * .5f;
            }
            return 0f;
        }

        private float ChooseTarget(HSControlSettings settings) {
            if (settings.Mode == (int)SpeedMode.Range)
                return Next(settings.RangeMin, settings.RangeMax);
            // Original "half" values 0/1/2 correspond to slow,
            // alternating full and fast, and fast ranges.
            if (settings.Band == 0) return Next(0f, .45f);
            if (settings.Band == 2) return Next(.65f, 1f);
            // Full switches between the two bands on a randomized timer.
            if (random.Next(0, 5) == 0) highBand = !highBand;
            return highBand ? Next(.65f, 1f) : Next(0f, .45f);
        }

        internal bool Step(float deltaSeconds, int mode, string animationState, bool rely,
            float maleGauge, float femaleGauge, HSControlSettings requested)
        {
            StrengthToggleRequested = false;
            Driving = false;
            if (requested == null || !CanDrive(mode, animationState, rely)) return false;
            HSControlSettings settings = requested.Copy();
            settings.Sanitize();
            if (!initialized) Reset(0f);
            if (settings.ManualOverride) {
                target = current = settings.ManualSpeed;
                Driving = true;
                return true;
            }
            if (settings.Mode == (int)SpeedMode.Off) return false;

            deltaSeconds = HSControlSettings.Clamp(deltaSeconds, 0f, .2f);
            elapsed += deltaSeconds;
            bool modeChanged = lastMode != settings.Mode || lastBand != settings.Band ||
                lastRangeMin != settings.RangeMin || lastRangeMax != settings.RangeMax;
            if (modeChanged) timer = 0f;
            lastMode = settings.Mode;
            lastBand = settings.Band;
            lastRangeMin = settings.RangeMin;
            lastRangeMax = settings.RangeMax;
            if (settings.Mode == (int)SpeedMode.GaugeLink) {
                target = LinkedTarget(mode, settings.Linkage, maleGauge, femaleGauge);
            } else {
                timer -= deltaSeconds;
                if (timer <= 0f) {
                    target = ChooseTarget(settings);
                    timer = Next(settings.TargetMinSeconds, settings.TargetMaxSeconds);
                    amplitude = Next(.05f, .2f);
                    frequency = Next(.5f, 1.5f);
                    easing = Next(1.5f, 5f);
                }
            }
            double oscillation = Math.Sin(elapsed * frequency) * amplitude;
            float desired = HSControlSettings.Clamp(target + (float)oscillation, 0f, 1f);
            float alpha = 1f - (float)Math.Exp(-deltaSeconds * easing);
            current = HSControlSettings.Clamp(current + (desired - current) * alpha, 0f, 1f);
            Driving = true;

            if (settings.AutoStrength && mode != 0) {
                strengthTimer -= deltaSeconds;
                if (strengthTimer <= 0f) {
                    StrengthToggleRequested = true;
                    strengthTimer = Next(settings.StrengthMinSeconds, settings.StrengthMaxSeconds);
                }
            } else strengthTimer = 0f;
            return true;
        }
    }
}
