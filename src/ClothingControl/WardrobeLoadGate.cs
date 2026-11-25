namespace ClothingControl {
    internal enum WardrobeLoadResult { Waiting, Ready, Cancelled, TimedOut }

    // Game-independent load barrier. Native reload notification accelerates the
    // barrier but never substitutes for verified outfit contents. Builds that
    // do not emit a notification require a sustained matching readback.
    internal sealed class WardrobeLoadGate {
        private readonly int source, destination, requestFrame;
        private readonly float deadline;
        private int signalFrame = -1, lastFrame = -1, matches;
        private float firstMatchTime;
        private bool sawDestination;

        internal WardrobeLoadGate(int source, int destination, int frame, float now) {
            this.source = source; this.destination = destination;
            requestFrame = frame; deadline = now + 8f;
        }

        internal void NotifyReload(int frame, int coordinate) {
            if (frame >= requestFrame && coordinate == destination) signalFrame = frame;
        }

        internal WardrobeLoadResult Poll(int frame, float now, bool sameTarget, int coordinate, bool contentsReady) {
            if (!sameTarget) return WardrobeLoadResult.Cancelled;
            if (now >= deadline) return WardrobeLoadResult.TimedOut;
            if (coordinate != destination) {
                if (sawDestination || coordinate != source) return WardrobeLoadResult.Cancelled;
                matches = 0; lastFrame = -1;
                return WardrobeLoadResult.Waiting;
            }
            sawDestination = true;
            if (!contentsReady || frame <= requestFrame) {
                matches = 0; lastFrame = -1;
                return WardrobeLoadResult.Waiting;
            }
            if (frame != lastFrame) {
                if (matches == 0) firstMatchTime = now;
                matches++;
                lastFrame = frame;
            }
            if (matches < 2) return WardrobeLoadResult.Waiting;
            if (signalFrame >= requestFrame && frame > signalFrame) return WardrobeLoadResult.Ready;
            return now - firstMatchTime >= .75f ? WardrobeLoadResult.Ready : WardrobeLoadResult.Waiting;
        }
    }
}
