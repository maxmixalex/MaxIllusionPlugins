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
        // Installed KK_HCtrl AutoSpeed .ctor: randomized destination every 2-10 s.
        internal float TargetMinSeconds = 2f, TargetMaxSeconds = 10f;

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
            frequency = .7f; amplitude = .05f; easing = .02f;
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
            if (mode == 0) return animState.IndexOf("_Idle", StringComparison.Ordinal) >= 0 ||
                                  string.Equals(animState, "Idle", StringComparison.Ordinal);
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
            const float minimum = .05f, halfLow = .4999f, halfHigh = .5001f;
            if (settings.Mode == (int)SpeedMode.Range)
                return HSControlSettings.Clamp(Next(settings.RangeMin, settings.RangeMax), minimum, 1f);
            // Same normalized cutoffs as the installed KK_HCtrl AutoSpeed:
            // speedMin=.05, halfLow=.4999, halfHigh=.5001.
            if (settings.Band == 0) return Next(minimum, halfLow);
            if (settings.Band == 2) return Next(halfHigh, 1f);
            // Keep full range useful: choose between the two halves on
            // randomized destination changes, not every render frame.
            if (random.Next(0, 5) == 0) highBand = !highBand;
            return highBand ? Next(halfHigh, 1f) : Next(minimum, halfLow);
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
            timer -= deltaSeconds;
            if (timer <= 0f) {
                if (settings.Mode != (int)SpeedMode.GaugeLink)
                    target = ChooseTarget(settings);
                timer = Next(settings.TargetMinSeconds, settings.TargetMaxSeconds);
                amplitude = Next(.05f, .2f);
                frequency = Next(.5f, 1.5f);
                // Installed KK_HCtrl uses Mathf.Lerp(now,dest,.005-.05)
                // once per frame. Convert to a per-second exponential so
                // 30/60/120 FPS keep comparable animation pace.
                easing = Next(.005f, .05f);
            }
            if (settings.Mode == (int)SpeedMode.GaugeLink)
                target = LinkedTarget(mode, settings.Linkage, maleGauge, femaleGauge);
            double oscillation = Math.Sin(elapsed * frequency) * amplitude;
            float desired = HSControlSettings.Clamp(target + (float)oscillation, .05f, 1f);
            float alpha = 1f - (float)Math.Pow(1f - easing, deltaSeconds * 60f);
            current = HSControlSettings.Clamp(current + (desired - current) * alpha, .05f, 1f);
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
