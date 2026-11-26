using System;

namespace LoveJuice {
    internal struct LiquidLevelBursts {
        internal int Face, Body, Special;
    }

    // A rising readback level is a real change, not a periodic timer guess.
    // The first observation establishes a baseline: loading a saved character
    // with an existing overlay must not explode into a burst.
    internal sealed class LiquidLevelEvents {
        private bool ready;
        private int face, body, special;

        private static int BoundedLevel(int value) { return Math.Max(0, Math.Min(20, value)); }
        private static int Rise(int previous, int current) {
            return Math.Min(3, Math.Max(0, current - previous));
        }

        internal LiquidLevelBursts Observe(int faceLevel, int bodyLevel, int specialLevel) {
            faceLevel = BoundedLevel(faceLevel);
            bodyLevel = BoundedLevel(bodyLevel);
            specialLevel = BoundedLevel(specialLevel);
            LiquidLevelBursts changes = new LiquidLevelBursts();
            if (ready) {
                changes.Face = Rise(face, faceLevel);
                changes.Body = Rise(body, bodyLevel);
                changes.Special = Rise(special, specialLevel);
            }
            face = faceLevel; body = bodyLevel; special = specialLevel;
            ready = true;
            return changes;
        }

        internal void Reset() { ready = false; face = body = special = 0; }
    }
}
