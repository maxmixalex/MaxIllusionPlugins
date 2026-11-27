using System;
using HSControl;
using MaximumUI;

namespace MaximumUI.Tests
{
    internal static class HSControlViewTests
    {
        internal const int TestCount = 5;
        internal static void RunAll() {
            OriginalModesAndOrder();
            NoInputAndManualModes();
            GaugeLocksAndAutoCaressSwitch();
            RetainedUpdates();
            NativeActionButtons();
        }

        private static HSControlViewModel Make() {
            return new HSControlViewModel {
                Version = "test", Available = true, Enabled = true,
                SceneMode = 0, Mode = 2, ManualSpeed = .5f, CurrentSpeed = .42f,
                TargetSpeed = .71f, RangeMin = .1f, RangeMax = .9f,
                CanForceLoop = true, CanStageMale = true,
                CanStageFemale = true, CanStageBoth = true,
                CanFinishMale = true, CanFemaleFinish = true,
                CanFinishBoth = true,
                ChangeEnabled = delegate { }, ChangeMode = delegate { },
                ChangeManualOverride = delegate { }, ChangeManualSpeed = delegate { },
                ChangeAutoCaress = delegate { }, ChangeRangeMin = delegate { },
                ChangeRangeMax = delegate { }, ChangeGaugeMale = delegate { },
                ChangeGaugeFemale = delegate { }, ChangeBand = delegate { },
                ChangeLinkage = delegate { }, ChangeAutoStrength = delegate { },
                ChangeStrengthMin = delegate { }, ChangeStrengthMax = delegate { },
                ChangeAutoMaleFinish = delegate { },
                ChangeAutoFemaleFinish = delegate { }, ChangeAutoReplay = delegate { },
                ToggleFemaleLock = delegate { }, ToggleMaleLock = delegate { },
                PlayLoop = delegate { }, PlayWeak = delegate { }, PlayStrong = delegate { },
                StageMale = delegate { }, StageFemale = delegate { },
                StageBoth = delegate { }, FinishMale = delegate { },
                FinishFemale = delegate { }, FinishBoth = delegate { },
                Reroll = delegate { }, Close = delegate { }
            };
        }

        private static void OriginalModesAndOrder() {
            HSControlView view = new HSControlView { Model = Make() };
            int selection = -1;
            view.Model.ChangeMode = x => selection = x;
            Widget window = view.Build(null);
            Find<RadioButton>(window, "hscontrol-mode-linkage").OnSelect();
            Check(selection == 3, "Original gauge-linkage radio is operable");
            Check(Find<RadioButton>(window, "hscontrol-mode-none") != null &&
                Find<RadioButton>(window, "hscontrol-mode-half") != null &&
                Find<RadioButton>(window, "hscontrol-mode-range") != null,
                "Four original speed modes occupy one row");
            Check(Find<SliderFloat>(window, "hscontrol-min").Enabled &&
                Find<SliderFloat>(window, "hscontrol-max").Enabled,
                "Range controls stay adjacent and reachable");
            Check(FindOrNull<ScrollView>(window, "hscontrol-scroll") == null &&
                FindOrNull<Tabs>(window, "hscontrol-pages") == null,
                "HCtrl parity requires one window, not tab pages or scroll");
            Check(Find<Button>(window, "hscontrol-female-lock") != null &&
                Find<Button>(window, "hscontrol-male-lock") != null &&
                Find<Button>(window, "hscontrol-play-loop") != null &&
                Find<Button>(window, "hscontrol-stage-both") != null &&
                Find<Button>(window, "hscontrol-finish-both") != null,
                "Original top gauge locks and all bottom actions are on the same page");
        }

        private static void NoInputAndManualModes() {
            HSControlView view = new HSControlView { Model = Make() };
            view.Model.Enabled = false;
            view.Model.Available = false;
            Widget window = view.Build(null);
            Check(!Find<SliderFloat>(window, "hscontrol-gauge-female").Enabled &&
                !Find<SliderFloat>(window, "hscontrol-gauge-male").Enabled,
                "Unavailable scene has no gauge write access");
            view.Model.Enabled = true;
            view.Model.Available = true;
            view.Model.ManualOverride = true;
            view.Model.Mode = 0;
            window = view.Build(null);
            Check(Find<SliderFloat>(window, "hscontrol-manual-speed").Enabled &&
                FindOrNull<SliderFloat>(window, "hscontrol-min") == null,
                "Off mode displays original manual speed without irrelevant range");
        }

        private static void GaugeLocksAndAutoCaressSwitch() {
            HSControlView view = new HSControlView { Model = Make() };
            int locks = 0;
            view.Model.ToggleFemaleLock = () => locks += 1;
            view.Model.ToggleMaleLock = () => locks += 10;
            int autoCaressChanges = 0;
            view.Model.ChangeAutoCaress = x => autoCaressChanges += x ? 1 : -1;
            Widget window = view.Build(null);
            Find<Button>(window, "hscontrol-female-lock").OnClick();
            Find<Button>(window, "hscontrol-male-lock").OnClick();
            Find<Checkbox>(window, "hscontrol-aibu").OnChange(true);
            Check(locks == 11 && autoCaressChanges == 1,
                "Original gauge locking and explicit auto-caress are independently operable");
            Check(!view.Model.AutoCaress,
                "Automatic caress must be disabled by default even when speed mode is active");
        }

        private static void RetainedUpdates() {
            HSControlViewModel model = Make();
            HSControlViewModel same = Make();
            Check(model.SameDisplay(same), "Unchanged state keeps the retained view");
            same.FemaleLocked = true;
            Check(!model.SameDisplay(same), "Native gauge lock state repaints");
            same = Make(); same.AutoCaress = true;
            Check(!model.SameDisplay(same), "Auto-caress toggle repaints");
            same = Make(); same.CurrentSpeed = .62f;
            Check(!model.SameDisplay(same), "Native speed readback repaints");
        }

        private static void NativeActionButtons() {
            HSControlViewModel model = Make();
            model.CanFemaleFinish = false;
            int weak = 0, strong = 0, loop = 0;
            model.PlayWeak = () => weak++;
            model.PlayStrong = () => strong++;
            model.PlayLoop = () => loop++;
            HSControlView view = new HSControlView { Model = model };
            Widget window = view.Build(null);
            Check(!Find<Button>(window, "hscontrol-finish-female").Enabled,
                "Original mode/action readiness prevents unsafe climax commands");
            Find<Button>(window, "hscontrol-play-loop").OnClick();
            Find<Button>(window, "hscontrol-play-weak").OnClick();
            Find<Button>(window, "hscontrol-play-strong").OnClick();
            Check(loop == 1 && weak == 1 && strong == 1,
                "Loop, weak and strong are three distinct original actions");
        }

        private static T Find<T>(Widget root, string key) where T : Widget {
            T value = FindOrNull<T>(root, key);
            if (value == null) throw new InvalidOperationException("Missing " + key);
            return value;
        }
        private static T FindOrNull<T>(Widget root, string key) where T : Widget {
            if (root == null) return null;
            if (root is T && Equals(root.Key, key)) return (T)root;
            foreach (Widget child in root.Children) {
                T candidate = FindOrNull<T>(child, key);
                if (candidate != null) return candidate;
            }
            return null;
        }
        private static void Check(bool okay, string message) {
            if (!okay) throw new InvalidOperationException("HSControl View: " + message);
        }
    }
}
