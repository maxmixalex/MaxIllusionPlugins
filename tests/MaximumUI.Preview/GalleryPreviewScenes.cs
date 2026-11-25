using System;
using System.Collections.Generic;
using System.Reflection;
using MaximumUI;

internal static class GalleryPreviewScenes {
    internal static IEnumerable<PreviewScene> Create(Assembly assembly) {
        Type catalog = assembly.GetType("MaximumUIGallery.GalleryScenes", true);
        string[] ids = (string[])catalog.GetProperty("PageIds").GetValue(null);
        string[] titles = (string[])catalog.GetProperty("PageTitles").GetValue(null);
        MethodInfo create = catalog.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        MethodInfo states = catalog.GetMethod("StatesForPage", BindingFlags.Public | BindingFlags.Static);
        if (ids.Length != titles.Length || ids.Length == 0 || create == null || states == null)
            throw new InvalidOperationException("The production Gallery scene catalog is incomplete.");
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Length; i++) {
            string id = ids[i], title = titles[i];
            if (!seen.Add(id)) throw new InvalidOperationException("Duplicate Gallery page " + id);
            yield return Scene(create, id, title, "default", false, false, false);
            yield return Scene(create, id, title, "default", true, false, false);
            yield return Scene(create, id, title, "default", false, true, false);
            yield return Scene(create, id, title, "default", false, false, true);
            foreach (string state in (string[])states.Invoke(null, new object[] { id })) {
                if (state == "default") continue;
                yield return Scene(create, id, title, state, false, false, false);
                if (state == "dialog-open") {
                    yield return Scene(create, id, title, state, false, false, true);
                    yield return Scene(create, id, title, state, false, true, false);
                }
            }
            if (id == "dialogs") {
                yield return Scene(create, id, title, "service-alert", false, false, false);
                yield return Scene(create, id, title, "service-confirm", false, false, false);
                yield return Scene(create, id, title, "service-alert", false, false, true);
                yield return Scene(create, id, title, "service-confirm", false, false, true);
            }
        }
    }

    private static PreviewScene Scene(MethodInfo create, string id, string title, string state, bool dark, bool compact, bool largeText) {
        ThemeTokens palette = dark ? ThemePalettes.MetroDark() : ThemePalettes.MetroLight();
        if (largeText) palette.FontSize = 18f;
        string theme = dark ? "dark" : "light";
        string size = compact ? "minimum" : largeText ? "large-text" : "standard";
        float viewportWidth = 1000f, viewportHeight = 760f;
        bool serviceDialog = state == "service-alert" || state == "service-confirm";
        Func<Widget> build = () => {
            View view = (View)create.Invoke(null, new object[] { id, palette.Clone(), serviceDialog ? "default" : state });
            DialogService service = (DialogService)Activator.CreateInstance(typeof(DialogService),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { new Func<LayoutMetrics>(() => Program.PreviewMetrics(palette)),
                    new Func<MuiVec2>(() => new MuiVec2(viewportWidth, viewportHeight)) }, null);
            BuildContext context = new BuildContext {
                StateStore = new UiStateStore(), Query = new UiQueryStore(), Navigation = new UiNavigationStore(),
                Shortcuts = new UiShortcutRouter(), Dialogs = service
            };
            Widget widget = view.Build(context);
            if (serviceDialog) {
                string key = state == "service-alert" ? "dialog-alert" : "dialog-confirm";
                Button button = Find(widget, key) as Button;
                if (button == null || button.OnClick == null) throw new InvalidOperationException("Production dialog command " + key + " is missing.");
                button.OnClick();
                if (service.Dialogs.Count != 1) throw new InvalidOperationException("Production " + key + " did not open exactly one dialog.");
                foreach (Dialog dialog in service.Dialogs) widget.Add(dialog);
            }
            return widget;
        };
        Window window = build() as Window;
        if (window == null) throw new InvalidOperationException("Production Gallery " + id + " must return a Window.");
        viewportWidth = compact ? window.MinWidth : window.Width;
        viewportHeight = compact ? window.MinHeight : window.Height;
        return new PreviewScene {
            Slug = "gallery-" + id + "-" + theme + "-" + size + "-" + state,
            Title = title, Source = "MaximumUIGallery.GalleryScenes.Create → GalleryView.Build",
            Detail = "Production Gallery page " + id + "; state " + state,
            Palette = palette, PaletteName = dark ? "Metro Dark" : "Metro Light",
            Gallery = true, PageId = id, StateId = state, SizeName = size,
            Width = compact ? window.MinWidth : window.Width,
            Height = compact ? window.MinHeight : window.Height,
            Build = build, Widget = window
        };
    }

    private static Widget Find(Widget root, string key) {
        if (Equals(root.Key, key)) return root;
        foreach (Widget child in root.Children) {
            Widget found = Find(child, key);
            if (found != null) return found;
        }
        return null;
    }
}
