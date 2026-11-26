using System;
using LoveJuice;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class LoveJuiceViewTests {
        internal const int TestCount = 4;
        internal static void RunAll() {
            PreviewRequiresLiveActor();
            SettingsActionsUseRealWidgets();
            RetainedViewTracksNativeFeedback();
            ActorSelectionUsesDedicatedNativeControls();
        }

        private static LoveJuiceViewModel Create() {
            return new LoveJuiceViewModel {
                Version = "test", Enabled = true, HasScene = true,
                Face = true, Body = true, ActorCount = 2, MaximumParticles = 48,
                ActorNames = new[] { "1. Alice", "2. Bob" }, SelectedActorIndex = 0,
                ActiveParticles = 4, ChangeEnabled = delegate { },
                ChangeFace = delegate { }, ChangeBody = delegate { },
                ChangeMilk = delegate { }, ChangeShower = delegate { },
                ChangeSpecial = delegate { }, ChangeCollision = delegate { },
                ChangeMaximum = delegate { }, ChangePreviewChannel = delegate { },
                ChangeInterval = delegate { }, ChangeGravity = delegate { },
                ChangeDamping = delegate { }, ChangeLifetime = delegate { },
                ChangeScale = delegate { }, ChangeLaunchSpeed = delegate { },
                ChangeMilkThreshold = delegate { }, Preview = delegate { },
                SelectActor = delegate { }, Close = delegate { }
            };
        }

        private static void PreviewRequiresLiveActor() {
            LoveJuiceViewModel m = Create();
            LoveJuiceView v = new LoveJuiceView { Model = m };
            Check(Find<Button>(v.Build(null), "lovejuice-emit").Enabled, "Live preview active.");
            m.HasScene = false;
            Check(!Find<Button>(v.Build(null), "lovejuice-emit").Enabled, "No scene cannot emit.");
            m.HasScene = true; m.ActorCount = 0;
            Check(!Find<Button>(v.Build(null), "lovejuice-emit").Enabled, "No actor cannot emit.");
        }

        private static void SettingsActionsUseRealWidgets() {
            LoveJuiceViewModel m = Create();
            int enabledChanges = 0, selected = -1;
            m.ChangeEnabled = value => { if (!value) enabledChanges++; };
            m.ChangePreviewChannel = value => selected = value;
            LoveJuiceView v = new LoveJuiceView { Model = m };
            Widget w = v.Build(null);
            Find<Checkbox>(w, "lovejuice-enabled").OnChange(false);
            Find<ComboBox<int>>(w, "lovejuice-preview-channel").OnChange(4);
            Check(enabledChanges == 1 && selected == 4, "Native controls dispatch settings.");
            Check(Find<SliderFloat>(w, "lovejuice-gravity") != null &&
                Find<SliderFloat>(w, "lovejuice-scale") != null &&
                Find<SliderFloat>(w, "lovejuice-milk-threshold") != null,
                "Physics and source sliders are production widgets.");
            m.Enabled = false;
            w = v.Build(null);
            Check(Find<Checkbox>(w, "lovejuice-enabled").Enabled &&
                !Find<Checkbox>(w, "lovejuice-face").Enabled, "Master switch permits recovery.");
        }

        private static void RetainedViewTracksNativeFeedback() {
            LoveJuiceViewModel a = Create(), b = Create();
            Check(a.SameDisplayAs(b), "Equivalent readback avoids UI rebuilding.");
            b.ActiveParticles++;
            Check(!a.SameDisplayAs(b), "Particle counts are live readback.");
            b = Create(); b.Integration = "Legacy SkinEffects";
            Check(!a.SameDisplayAs(b), "Optional integration state refreshes UI.");
        }

        private static void ActorSelectionUsesDedicatedNativeControls() {
            LoveJuiceViewModel model = Create();
            int selected = -1;
            model.SelectActor = value => selected = value;
            LoveJuiceView view = new LoveJuiceView { Model = model };
            Widget ui = view.Build(null);
            ComboBox<int> actors = Find<ComboBox<int>>(ui, "lovejuice-target");
            Check(actors.Enabled && actors.Options.Count == 2, "Every active character is selectable.");
            actors.OnChange(1);
            Check(selected == 1, "Selected actor reaches the runtime callback.");
            model.ActorNames = new string[0]; model.SelectedActorIndex = -1; model.ActorCount = 0;
            ui = view.Build(null);
            Check(!Find<ComboBox<int>>(ui, "lovejuice-target").Enabled &&
                !Find<Button>(ui, "lovejuice-emit").Enabled,
                "No old actor survives an empty scene.");
        }

        private static T Find<T>(Widget root, string key) where T : Widget {
            T result = FindOrNull<T>(root, key);
            if (result != null) return result;
            throw new InvalidOperationException("LoveJuice view missing: " + key);
        }
        private static T FindOrNull<T>(Widget root, string key) where T : Widget {
            if (root == null) return null;
            if (root is T && Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) {
                T found = FindOrNull<T>(child, key);
                if (found != null) return found;
            }
            return null;
        }
        private static void Check(bool value, string message) {
            if (!value) throw new InvalidOperationException("LoveJuice UI: " + message);
        }
    }
}
