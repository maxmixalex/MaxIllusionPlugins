namespace MaximumUI {
    // Separate from Unity's event loop so build invalidation can be regression tested.
    internal sealed class ViewBuildSchedule {
        private int revision;
        private int builtRevision = -1;
        private int lastFrame = -1;

        public int Revision { get { return revision; } }

        public void MarkDirty() {
            unchecked { revision++; }
        }

        public bool ShouldBuild(MountOptions options, int frame, bool hasRoot) {
            return !hasRoot || revision != builtRevision || options.BuildEveryOnGUI ||
                (options.BuildEveryFrame && frame != lastFrame);
        }

        public void CompleteBuild(int frame, int startedRevision) {
            lastFrame = frame;
            // A view may call SetState while building. Do not discard that invalidation.
            builtRevision = startedRevision;
        }
    }
}
