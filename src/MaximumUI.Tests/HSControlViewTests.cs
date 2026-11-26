using System;
using HSControl;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class HSControlViewTests {
        internal const int TestCount = 3;
        internal static void RunAll() {
            OriginalControlModesRemainReachable();
            DisabledAndManualStatesGuardActions();
            RetainedComparisonDetectsNativeStateUpdates();
        }

        private static HSControlViewModel Make() {
            return new HSControlViewModel {
                Version = "test", Available = true, Enabled = true,
                Mode = 2, ManualSpeed = .5f, CurrentSpeed = .42f, TargetSpeed = .71f,
                ChangeEnabled = delegate { }, ChangeMode = delegate { },
                ChangeManualOverride = delegate { }, ChangeManualSpeed = delegate { },
                ChangeRangeMin = delegate { }, ChangeRangeMax = delegate { },
                ChangeGaugeMale = delegate { }, ChangeGaugeFemale = delegate { },
                ChangeBand = delegate { }, ChangeLinkage = delegate { },
                ChangeAutoStrength = delegate { }, ChangeStrengthMin = delegate { },
                ChangeStrengthMax = delegate { }, ChangeAutoMaleFinish = delegate { },
                ChangeAutoFemaleFinish = delegate { }, ChangeAutoReplay = delegate { },
                Reroll = delegate { }, Close = delegate { }
            };
        }

        private static void OriginalControlModesRemainReachable() {
            HSControlView view = new HSControlView { Model = Make() };
            int selected = -1;
            view.Model.ChangeMode = value => selected = value;
            Window window = (Window)view.Build(null);
            ComboBox<int> chooser = Find<ComboBox<int>>(window, "hscontrol-speed-mode");
            Check(chooser.Options.Count == 4, "Original mode count is not reduced");
            chooser.OnChange(3);
            Check(selected == 3, "Gauge linkage can be selected");
            Check(Find<SliderFloat>(window, "hscontrol-min").Enabled, "Range slider is enabled in range mode");
            Check(Find<ProgressBar>(window, "hscontrol-current").Value == .42f,
                "Live speed indicator displays actual runtime data");
            Check(Find<ScrollView>(window, "hscontrol-scroll") != null,
                "The editor uses one bounded scroll container");
        }

        private static void DisabledAndManualStatesGuardActions() {
            HSControlView view = new HSControlView { Model = Make() };
            view.Model.Available = false; view.Model.Enabled = false;
            Widget widget = view.Build(null);
            Check(!Find<SliderFloat>(widget, "hscontrol-manual-speed").Enabled &&
                !Find<SliderFloat>(widget, "hscontrol-gauge-male").Enabled,
                "No scene means no writes to native gauges");
            view.Model.Available = true;
            view.Model.ManualOverride = true;
            view.Model.Mode = 0;
            widget = view.Build(null);
            Check(!Find<SliderFloat>(widget, "hscontrol-min").Enabled &&
                !Find<ComboBox<int>>(widget, "hscontrol-band").Enabled,
                "Unused controls stay disabled in manual mode");
            Check(Find<Checkbox>(widget, "hscontrol-enabled").Enabled,
                "Master toggle remains enabled for recovery");
        }

        private static void RetainedComparisonDetectsNativeStateUpdates() {
            HSControlViewModel model = Make();
            HSControlViewModel same = Make();
            Check(model.SameDisplay(same), "Unchanged controls avoid unnecessary rebuilds");
            same.CurrentSpeed = .60f;
            Check(!model.SameDisplay(same), "The progress widget must refresh after a speed change");
            same = Make(); same.AnimationState = "SLoop";
            Check(!model.SameDisplay(same), "Animation transitions refresh the status");
        }

        private static T Find<T>(Widget root, string key) where T : Widget {
            if (root == null) throw new InvalidOperationException("Missing " + key);
            if (root is T && Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) {
                T found = FindOrNull<T>(child, key);
                if (found != null) return found;
            }
            throw new InvalidOperationException("Missing " + key);
        }

        private static T FindOrNull<T>(Widget root, string key) where T : Widget {
            if (root is T && Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) {
                T found = FindOrNull<T>(child, key);
                if (found != null) return found;
            }
            return null;
        }

        private static void Check(bool okay, string description) {
            if (!okay) throw new InvalidOperationException("HSControl View: " + description);
        }
    }
}
