using System;

namespace LoveJuice {
    // ParticleSystem.particleCount is not guaranteed to reflect a just-issued
    // Emit() until the native particle update. Reserve each emitted particle
    // within the current frame to enforce one global budget across channels.
    internal sealed class NativeParticleBudget {
        private int frame = -1, startingCount, reservations;

        internal bool TryReserve(int currentFrame, int observedCount, int limit) {
            if (currentFrame != frame) {
                frame = currentFrame;
                startingCount = Math.Max(0, observedCount);
                reservations = 0;
            }
            int accounted = Math.Max(Math.Max(0, observedCount), startingCount + reservations);
            if (limit <= 0 || accounted >= limit) return false;
            reservations++;
            return true;
        }

        internal void Reset() {
            frame = -1;
            startingCount = 0;
            reservations = 0;
        }
    }
}
