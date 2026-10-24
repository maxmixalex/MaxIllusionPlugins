namespace MultiTeamH {
    /// <summary>Maps persistent team identities to the native voice slots.</summary>
    internal static class ExpressionVoiceBinding {
        internal static int FlagVoiceIndex(int team, int sharedOwner) {
            // Each independent 1F1M HFlag uses its own slot zero. Only a shared
            // 2F graph has a second woman in the same flag's voice request array.
            return sharedOwner >= 0 && team != sharedOwner ? 1 : 0;
        }

        internal static HVoiceCtrl.Voice Resolve(int team, HVoiceCtrl.Voice[] byTeam,
            HVoiceCtrl.Voice[] currentSlots, int boundPrimary) {
            if (team < 0 || team > 1) return null;
            if (byTeam != null && team < byTeam.Length && byTeam[team] != null) return byTeam[team];
            // Before the persistent catalogue has been initialized, a slot is
            // useful only when its owner is known. Never borrow the other voice.
            if (currentSlots == null || boundPrimary < 0 || boundPrimary > 1) return null;
            int slot = team == boundPrimary ? 0 : 1;
            return slot < currentSlots.Length ? currentSlots[slot] : null;
        }
    }
}
