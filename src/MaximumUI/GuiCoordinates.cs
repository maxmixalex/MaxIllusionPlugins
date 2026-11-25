using System;

namespace MaximumUI {
    /// <summary>
    /// Mirrors IMGUI group/scroll translations and their screen-space clipping.
    /// The reusable value-type stack allocates only when a new maximum depth grows it.
    /// </summary>
    internal sealed class GuiCoordinates {
        private struct Frame {
            public MuiVec2 Origin;
            public MuiRect Clip;
        }

        private Frame[] stack = new Frame[8];
        private int depth;
        private MuiVec2 origin;
        private MuiRect clip;

        public MuiVec2 Origin { get { return origin; } }
        public MuiRect Clip { get { return clip; } }
        public int Depth { get { return depth; } }

        public void Reset(float screenWidth, float screenHeight) {
            depth = 0;
            origin = new MuiVec2(0f, 0f);
            clip = new MuiRect(0f, 0f, Math.Max(0f, screenWidth), Math.Max(0f, screenHeight));
        }

        /// <summary>
        /// viewport is the actual content clip, after subtracting visible scrollbars.
        /// scroll must be the value returned by GUI.BeginScrollView for this event.
        /// </summary>
        public void PushScroll(MuiRect viewport, MuiVec2 scroll, MuiRect content) {
            Push();
            clip = Intersect(clip, ToScreen(viewport));
            // Legacy GUI.BeginScrollView rounds its combined scroll/content offset
            // before GUIClip.Push (UnityCsReference 2017.4, Modules/IMGUI/GUI.cs).
            // Keep the addition in float before Math.Round to match Mathf.Round.
            origin.X += viewport.X + (float)Math.Round(-scroll.X - content.X);
            origin.Y += viewport.Y + (float)Math.Round(-scroll.Y - content.Y);
        }

        public void PushGroup(MuiRect rect) {
            Push();
            clip = Intersect(clip, ToScreen(rect));
            origin.X += rect.X;
            origin.Y += rect.Y;
        }

        public void Pop() {
            if (depth == 0) throw new InvalidOperationException("GUI coordinate stack is already at its root.");
            Frame previous = stack[--depth];
            origin = previous.Origin;
            clip = previous.Clip;
        }

        public MuiRect ToScreen(MuiRect rect) {
            return new MuiRect(origin.X + rect.X, origin.Y + rect.Y, rect.Width, rect.Height);
        }

        public MuiVec2 ToScreen(MuiVec2 point) {
            return new MuiVec2(origin.X + point.X, origin.Y + point.Y);
        }

        /// <summary>
        /// Returns only the visible screen-space part of a local rectangle.
        /// Empty or non-finite geometry returns the canonical zero rectangle.
        /// </summary>
        public MuiRect ClipToScreen(MuiRect localRect) {
            if (!HasFiniteArea(localRect) || !HasFiniteArea(clip)) return new MuiRect();
            MuiRect screen = ToScreen(localRect);
            if (!HasFiniteArea(screen)) return new MuiRect();
            MuiRect visible = Intersect(clip, screen);
            return visible.Width > 0f && visible.Height > 0f ? visible : new MuiRect();
        }

        public bool IsVisible(MuiRect local) {
            MuiRect visible = ClipToScreen(local);
            return visible.Width > 0f && visible.Height > 0f;
        }

        /// <summary>
        /// point uses the same local coordinates as localRect (for example,
        /// Event.current.mousePosition inside the current IMGUI group).
        /// </summary>
        public bool ContainsLocalPoint(MuiRect localRect, MuiVec2 point) {
            return HasFiniteArea(localRect) && localRect.Contains(point) && ContainsScreenPoint(ToScreen(point));
        }

        public bool ContainsScreenPoint(MuiVec2 point) {
            return HasFiniteArea(clip) && clip.Contains(point);
        }

        private void Push() {
            if (depth == stack.Length) {
                Frame[] expanded = new Frame[stack.Length * 2];
                Array.Copy(stack, expanded, stack.Length);
                stack = expanded;
            }
            stack[depth++] = new Frame { Origin = origin, Clip = clip };
        }

        private static MuiRect Intersect(MuiRect first, MuiRect second) {
            float left = Math.Max(first.X, second.X);
            float top = Math.Max(first.Y, second.Y);
            float right = Math.Min(first.Right, second.X + Math.Max(0f, second.Width));
            float bottom = Math.Min(first.Bottom, second.Y + Math.Max(0f, second.Height));
            return new MuiRect(left, top, Math.Max(0f, right - left), Math.Max(0f, bottom - top));
        }

        private static bool HasFiniteArea(MuiRect rect) {
            return rect.Width > 0f && rect.Height > 0f &&
                IsFinite(rect.X) && IsFinite(rect.Y) && IsFinite(rect.Right) && IsFinite(rect.Bottom);
        }

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
