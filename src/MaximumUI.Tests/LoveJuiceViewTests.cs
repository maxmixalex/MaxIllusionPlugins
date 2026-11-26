using System;
using LoveJuice;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class LoveJuiceViewTests {
        internal const int TestCount = 3;
        internal static void RunAll() {
            PreviewRequiresLiveActor();
            SettingsActionsUseRealWidgets();
            RetainedViewTracksNativeFeedback();
        }

        private static LoveJuiceViewModel Create() {
            return new LoveJuiceViewModel {
                Version = "test", Enabled = true, HasScene = true,
                Face = true, Body = true, ActorCount = 2, MaximumParticles = 48,
                ActiveParticles = 4, ChangeEnabled = delegate { },
                ChangeFace = delegate { }, ChangeBody = delegate { },
                ChangeMilk = delegate { }, ChangeShower = delegate { },
                ChangeSpecial = delegate { }, ChangeCollision = delegate { },
                ChangeMaximum = delegate { }, ChangePreviewChannel = delegate { },
                ChangeInterval = delegate { }, ChangeGravity = delegate { },
                ChangeDamping = delegate { }, ChangeLifetime = delegate { },
                ChangeScale = delegate { }, ChangeLaunchSpeed = delegate { },
                ChangeMilkThreshold = delegate { }, Preview = delegate { }, Close = delegate { }
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
