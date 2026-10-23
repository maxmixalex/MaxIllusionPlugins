using System;

namespace MaximumUI {
    public abstract class View {
        public virtual void OnMount(BuildContext context) {
        }

        public virtual void OnUnmount(BuildContext context) {
        }

        public virtual void Update(BuildContext context) {
        }

        public abstract Widget Build(BuildContext context);
    }

    public abstract class StatefulView<TState> : View {
        protected TState State;

        protected StatefulView(TState initialState) {
            State = initialState;
        }

        protected void SetState(Action<TState> update) {
            if (update != null) update(State);
            MaximumUIRuntime.MarkDirty(this);
        }

        protected void ReplaceState(TState state) {
            State = state;
            MaximumUIRuntime.MarkDirty(this);
        }
    }
}
