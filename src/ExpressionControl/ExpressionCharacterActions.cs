using System;

namespace ExpressionControl {
    public partial class ExpressionControl {
        // Small actor-aware entry points for editor operations. Without an
        // optional integration they retain the original single-scene behavior;
        // MultiTeamH scopes these calls to the selected actor's owning team.
        internal void RunCharacterAction(FemaleData female, Action action) {
            if (female == null || female.female == null || !this.femaleList.Contains(female) || action == null) return;
            action();
        }

        internal int GetCharacterOrgasmCount(FemaleData female) {
            return female == null || female.female == null || !this.femaleList.Contains(female) ? 0 : this.orgazumCount;
        }

        internal void AdjustCharacterOrgasmCount(FemaleData female, int direction) {
            if (female == null || female.female == null || !this.femaleList.Contains(female)) return;
            int next = (int)Math.Max(0L, Math.Min(10L, (long)this.orgazumCount + direction));
            if (next == this.orgazumCount) return;
            this.orgazumCount = next;
            this.updateState = true;
        }
    }
}
