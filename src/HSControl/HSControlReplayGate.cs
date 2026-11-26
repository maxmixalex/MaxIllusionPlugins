using System;

namespace HSControl {
    internal enum ReplayCommand { None, Again, Insert, Resume }

    // Mirrors the original KK_HCtrl.dll's CoAutoRestart sequence:
    // 10s + 3s delay, voice-idle guard, then a mode-specific native action.
    // Sonyu OUT_A requires insertion followed by a separate restart request.
    internal sealed class HSControlReplayGate {
        private string finishedState;
        private int activeMode = -1;
        private float elapsed;
        private bool issued, awaitingResume;

        internal void Reset() {
            finishedState = null;
            activeMode = -1;
            elapsed = 0f;
            issued = false;
            awaitingResume = false;
        }

        internal static bool Supported(int mode, bool rely) {
            return !rely && (mode == 1 || mode == 2 || mode == 6 || mode == 7);
        }

        internal ReplayCommand Step(float delta, int mode, string state, bool rely,
            bool voiceReady, bool actionFree, bool enabled) {
            if (!enabled || !Supported(mode, rely) || string.IsNullOrEmpty(state)) {
                Reset();
                return ReplayCommand.None;
            }
            if (state.IndexOf("Loop", StringComparison.Ordinal) >= 0) {
                Reset();
                return ReplayCommand.None;
            }
            float dt = Math.Max(0f, Math.Min(.25f,
                float.IsNaN(delta) || float.IsInfinity(delta) ? 0f : delta));
            if (awaitingResume) {
                if (mode != activeMode) { Reset(); return ReplayCommand.None; }
                elapsed += dt;
                // Never send the "speedup" continuation while the old OUT_A
                // is still active. Wait for the game's insertion transition.
                if (elapsed < 6f || !voiceReady || !actionFree ||
                    state.EndsWith("OUT_A", StringComparison.Ordinal))
                    return ReplayCommand.None;
                awaitingResume = false;
                issued = true;
                return ReplayCommand.Resume;
            }
            if (!state.EndsWith("_A", StringComparison.Ordinal)) {
                if (!issued) { finishedState = null; elapsed = 0f; }
                return ReplayCommand.None;
            }
            if (issued) return ReplayCommand.None;
            if (!string.Equals(finishedState, state, StringComparison.Ordinal) ||
                activeMode != mode) {
                finishedState = state;
                activeMode = mode;
                elapsed = 0f;
            }
            elapsed += dt;
            if (elapsed < 13f || !voiceReady || !actionFree) return ReplayCommand.None;
            if (mode == 1 || mode == 6) {
                issued = true;
                return ReplayCommand.Again;
            }
            if (state.EndsWith("OUT_A", StringComparison.Ordinal)) {
                awaitingResume = true;
                elapsed = 0f;
                return ReplayCommand.Insert;
            }
            issued = true;
            return ReplayCommand.Resume;
        }
    }
}
