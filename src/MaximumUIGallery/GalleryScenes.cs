using System;
using MaximumUI;

namespace MaximumUIGallery {
    /// <summary>
    /// Deterministic entry points into the production Gallery. The standalone renderer
    /// enumerates these pages; it does not maintain a second set of sample widgets.
    /// Creating a view does not mount it, initialize Unity or allocate native resources.
    /// </summary>
    public static class GalleryScenes {
        internal static readonly string[] Ids = {
            "overview", "buttons", "text-input", "numeric-input", "selection", "colors",
            "drawing", "layout", "dialogs", "appearance", "large-lists", "diagnostics"
        };
        internal static readonly string[] Titles = {
            "Form", "Buttons", "Text input", "Numeric input", "Selection", "Colors",
            "Drawing", "Layout", "Dialogs", "Appearance", "Large lists", "Diagnostics"
        };

        public static string[] PageIds { get { return (string[])Ids.Clone(); } }
        public static string[] PageTitles { get { return (string[])Titles.Clone(); } }

        public static string[] StatesForPage(string pageId) {
            FindPage(pageId);
            if (pageId == "overview") return new[] { "default", "edited" };
            if (pageId == "buttons" || pageId == "text-input" || pageId == "numeric-input")
                return new[] { "default", "disabled" };
            if (pageId == "selection") return new[] { "default", "selected", "collapsed" };
            if (pageId == "layout") return new[] { "default", "activity-tab", "settings-tab", "collapsed" };
            if (pageId == "dialogs") return new[] { "default", "dialog-open" };
            if (pageId == "appearance") return new[] { "default", "pending" };
            if (pageId == "large-lists") return new[] { "default", "filtered", "empty", "selected" };
            return new[] { "default" };
        }

        public static View Create(string pageId, ThemeTokens palette, string state = "default") {
            int page = FindPage(pageId);
            if (palette == null) throw new ArgumentNullException("palette");
            string[] states = StatesForPage(pageId);
            bool found = false;
            for (int i = 0; i < states.Length; i++) if (states[i] == state) { found = true; break; }
            if (!found) throw new ArgumentException("Unknown state for Gallery page: " + state, "state");
            GalleryView view = new GalleryView();
            view.ConfigureScene(page, palette, state);
            return view;
        }

        private static int FindPage(string pageId) {
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == pageId) return i;
            throw new ArgumentException("Unknown Gallery page: " + pageId, "pageId");
        }
    }
}
