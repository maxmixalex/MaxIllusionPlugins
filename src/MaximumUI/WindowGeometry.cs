using System;

namespace MaximumUI {
    internal static class WindowGeometry {
        public static MuiRect PlacePopup(MuiRect anchor, float width, float height, float screenWidth, float screenHeight) {
            float availableWidth = Math.Max(1f, screenWidth);
            float availableHeight = Math.Max(1f, screenHeight);
            width = Math.Min(availableWidth, Math.Max(1f, width));
            height = Math.Min(availableHeight, Math.Max(1f, height));
            float y = anchor.Bottom;
            if (y + height > availableHeight) y = anchor.Y - height;
            return new MuiRect(Math.Max(0f, Math.Min(anchor.X, availableWidth - width)),
                Math.Max(0f, Math.Min(y, availableHeight - height)), width, height);
        }

        public static MuiRect Clamp(MuiRect rect, float screenWidth, float screenHeight,
            float minimumWidth, float minimumHeight) {
            float widthLimit = Math.Max(1f, screenWidth);
            float heightLimit = Math.Max(1f, screenHeight);
            rect.Width = Math.Min(widthLimit, Math.Max(Math.Min(widthLimit, Math.Max(1f, minimumWidth)), rect.Width));
            rect.Height = Math.Min(heightLimit, Math.Max(Math.Min(heightLimit, Math.Max(1f, minimumHeight)), rect.Height));
            rect.X = Math.Max(0f, Math.Min(rect.X, widthLimit - rect.Width));
            rect.Y = Math.Max(0f, Math.Min(rect.Y, heightLimit - rect.Height));
            return rect;
        }

        public static MuiRect Resize(MuiRect start, MuiVec2 delta, float screenWidth, float screenHeight,
            float minimumWidth, float minimumHeight) {
            // The upper-left corner stays fixed; shrinking a screen never strands
            // the resize grip or title outside its available client area.
            start = Clamp(start, screenWidth, screenHeight, minimumWidth, minimumHeight);
            float availableWidth = Math.Max(1f, screenWidth - start.X);
            float availableHeight = Math.Max(1f, screenHeight - start.Y);
            start.Width = Math.Min(availableWidth, Math.Max(Math.Min(minimumWidth, availableWidth), start.Width + delta.X));
            start.Height = Math.Min(availableHeight, Math.Max(Math.Min(minimumHeight, availableHeight), start.Height + delta.Y));
            return start;
        }
    }
}
