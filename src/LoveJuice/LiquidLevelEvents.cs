using System;

namespace LoveJuice {
    internal struct LiquidLevelBursts {
        internal int Face, Body, Special, Intimate;
    }

    // A rising readback level is a real change, not a periodic timer guess.
    // The first observation establishes a baseline: loading a saved character
    // with an existing overlay must not explode into a burst.
    internal sealed class LiquidLevelEvents {
        private bool ready;
        private int face, body, special, intimate;

        private static int BoundedLevel(int value) { return Math.Max(0, Math.Min(20, value)); }
        private static int Rise(int previous, int current) {
            return Math.Min(3, Math.Max(0, current - previous));
        }

        internal LiquidLevelBursts Observe(int faceLevel, int bodyLevel, int specialLevel) {
            return Observe(faceLevel, bodyLevel, specialLevel, 0);
        }

        internal LiquidLevelBursts Observe(int faceLevel, int bodyLevel, int specialLevel, int intimateLevel) {
            faceLevel = BoundedLevel(faceLevel);
            bodyLevel = BoundedLevel(bodyLevel);
            specialLevel = BoundedLevel(specialLevel);
            intimateLevel = BoundedLevel(intimateLevel);
            LiquidLevelBursts changes = new LiquidLevelBursts();
            if (ready) {
                changes.Face = Rise(face, faceLevel);
                changes.Body = Rise(body, bodyLevel);
                changes.Special = Rise(special, specialLevel);
                changes.Intimate = Rise(intimate, intimateLevel);
            }
            face = faceLevel; body = bodyLevel; special = specialLevel; intimate = intimateLevel;
            ready = true;
            return changes;
        }

        internal void Reset() { ready = false; face = body = special = intimate = 0; }
    }
}
