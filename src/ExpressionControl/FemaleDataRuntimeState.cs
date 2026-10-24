using System;

namespace ExpressionControl {
    public partial class ExpressionControl {
		private static readonly ExpressionControl.FemaleState[] inActions = new ExpressionControl.FemaleState[]
		{
			ExpressionControl.FemaleState.kiss,
			ExpressionControl.FemaleState.loopW,
			ExpressionControl.FemaleState.loopS,
			ExpressionControl.FemaleState.spurtF,
			ExpressionControl.FemaleState.spurtM,
			ExpressionControl.FemaleState.orgazumF,
			ExpressionControl.FemaleState.orgazumM,
			ExpressionControl.FemaleState.drinkVomitWait
		};
        public partial class FemaleData {
            internal void RefreshRuntimeState(bool updateState, string nowMotion, bool isFeraAction) {
                nowMotion = nowMotion ?? string.Empty;
                bool kissAction = this.IsKissAction();
                if (this.isKiss != kissAction) {
                    this.isKiss = kissAction;
                    this.state = this.GetFemaleState(nowMotion);
                }
                if (updateState) this.state = this.GetFemaleState(nowMotion);

                // Program/interval edits call UpdateState(false, ...). These
                // mouth policies depend on the current program, not on whether
                // the animation happened to change in this frame.
                this.isFeraMode = isFeraAction;
                this.ignoreMouthOpen = this.programValid && !this.program.applyMouthOpen;
                this.ignoreMouthLoad = this.programValid && isFeraAction && this.program.ignoreMouthInFera &&
                    Array.IndexOf(ExpressionControl.inActions, this.state) >= 0;
            }
        }
    }
}
