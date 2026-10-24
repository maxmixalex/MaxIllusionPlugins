namespace ExpressionControl {
    public partial class ExpressionControl {
        private void UpdateHState() {
            if (hsceneProc == null || this.updateState) return;
            // MultiTeamH supplies the owning team's context through hFrag.
            // hsceneProc is shared and normally still points at the focused team.
            HFlag flags = hFrag != null ? hFrag : hsceneProc.flags;
            if (flags == null) return;

            string motion = flags.nowAnimStateName ?? string.Empty;
            bool changed = this.nowMotion != motion;
            if (changed) {
                this.nowMotion = motion;
                if (motion.Contains("Orgasm_Start") || motion.Contains("S_IN_Start") ||
                    motion.Contains("F_IN_Start") ||
                    ((flags.mode == HFlag.EMode.masturbation || flags.mode == HFlag.EMode.lesbian) && motion == "Orgasm")) {
                    if (this.orgazumCount < 10) this.orgazumCount++;
                }
            }

            // During scene setup or animation replacement the descriptor can be
            // temporarily absent. Keep the last valid action until it arrives.
            if (flags.nowAnimationInfo != null) {
                string action = flags.nowAnimationInfo.nameAnimation ?? string.Empty;
                if (this.nowHAction != action) {
                    this.nowHAction = action;
                    this.isFeraAction = action.Contains("舐め") || action.Contains("フェラ") || action.Contains("咥え");
                    this.UpdateFemale();
                    changed = true;
                }
            }
            this.updateState = changed;
        }
    }
}
