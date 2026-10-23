namespace MaximumUI {
    public sealed class DragState {
        public bool Active;
        public MuiVec2 Offset;
    }

    public sealed class DragController {
        public bool Dispatch(MuiInputEvent input, MuiRect hitRect, DragState state, ref MuiRect targetRect) {
            if (input == null || state == null) return false;

            if (input.Type == MuiEventType.MouseDown && input.MouseButton == 0 && hitRect.Contains(input.MousePosition)) {
                state.Active = true;
                state.Offset = new MuiVec2(input.MousePosition.X - targetRect.X, input.MousePosition.Y - targetRect.Y);
                return true;
            }

            if (input.Type == MuiEventType.MouseDrag && state.Active) {
                targetRect.X = input.MousePosition.X - state.Offset.X;
                targetRect.Y = input.MousePosition.Y - state.Offset.Y;
                return true;
            }

            if (input.Type == MuiEventType.MouseUp && input.MouseButton == 0 && state.Active) {
                state.Active = false;
                return true;
            }

            return false;
        }
    }
}
