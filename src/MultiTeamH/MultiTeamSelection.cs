namespace MultiTeamH {
    // Pending launch state is separate from remembered card settings: a delayed
    // shutdown of one scene must not consume another scene's newer selection.
    internal static partial class MultiTeamSelection {
        private static SaveData.Heroine secondaryFemale;
        private static SaveData.Player secondaryMale;
        private static int secondaryStage;
        private static int secondaryStatus;

        internal static int Revision { get; private set; }
        internal static bool HasPending { get; private set; }
        internal static bool LaunchDataReady { get; private set; }
        internal static SaveData.Heroine SecondaryFemale { get { return secondaryFemale; } }
        internal static SaveData.Player SecondaryMale { get { return secondaryMale; } }
        internal static int SecondaryStage { get { return secondaryStage; } }
        internal static int SecondaryStatus { get { return secondaryStatus; } }

        internal static void Set(SaveData.Heroine female, SaveData.Player male, int stage, int status) {
            unchecked { Revision++; }
            secondaryFemale = female;
            secondaryMale = male;
            secondaryStage = stage;
            secondaryStatus = status;
            HasPending = female != null && male != null;
            LaunchDataReady = false;
        }

        internal static void MarkLaunchDataReady() { LaunchDataReady = HasPending; }

        internal static void Complete(int expectedRevision) {
            if (expectedRevision == Revision) Complete();
        }

        internal static void Complete() {
            secondaryFemale = null;
            secondaryMale = null;
            secondaryStage = 0;
            secondaryStatus = 0;
            HasPending = false;
            LaunchDataReady = false;
        }

        internal static void Clear() { Complete(); }
    }
}
