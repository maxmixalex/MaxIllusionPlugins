using System;
using MaximumUI;

namespace HSControl
{
    internal sealed class HSControlViewModel
    {
        internal string Version = "0.1.5", Status = "就绪", AnimationState = string.Empty;
        internal bool Available, Enabled, ManualOverride, AutoStrength;
        internal bool AutoMaleFinish, AutoFemaleFinish, AutoReplay, Driving;
        internal bool MaleLocked, FemaleLocked;
        internal bool CanForceLoop, CanStageMale, CanStageFemale, CanStageBoth;
        internal bool CanFinishMale, CanFemaleFinish, CanFinishBoth;
        internal float ManualSpeed, CurrentSpeed, TargetSpeed, GaugeMale, GaugeFemale;
        internal float RangeMin, RangeMax, StrengthMinSeconds, StrengthMaxSeconds;
        internal int Mode, Band, Linkage, SceneMode;
        internal Action<bool> ChangeEnabled, ChangeManualOverride, ChangeAutoStrength;
        internal Action<bool> ChangeAutoMaleFinish, ChangeAutoFemaleFinish, ChangeAutoReplay;
        internal Action<float> ChangeManualSpeed, ChangeGaugeMale, ChangeGaugeFemale;
        internal Action<float> ChangeRangeMin, ChangeRangeMax, ChangeStrengthMin, ChangeStrengthMax;
        internal Action<int> ChangeMode, ChangeBand, ChangeLinkage;
        internal Action ToggleMaleLock, ToggleFemaleLock, Close, Reroll;
        internal Action PlayLoop, PlayWeak, PlayStrong, StageMale, StageFemale, StageBoth;
        internal Action FinishMale, FinishFemale, FinishBoth;

        internal bool SameDisplay(HSControlViewModel other) {
            return other != null && Version == other.Version && Status == other.Status &&
                AnimationState == other.AnimationState && Available == other.Available &&
                Enabled == other.Enabled && ManualOverride == other.ManualOverride &&
                AutoStrength == other.AutoStrength &&
                AutoMaleFinish == other.AutoMaleFinish && AutoFemaleFinish == other.AutoFemaleFinish &&
                AutoReplay == other.AutoReplay && Driving == other.Driving &&
                MaleLocked == other.MaleLocked && FemaleLocked == other.FemaleLocked &&
                CanForceLoop == other.CanForceLoop && CanStageMale == other.CanStageMale &&
                CanStageFemale == other.CanStageFemale && CanStageBoth == other.CanStageBoth &&
                CanFinishMale == other.CanFinishMale && CanFemaleFinish == other.CanFemaleFinish &&
                CanFinishBoth == other.CanFinishBoth &&
                Mode == other.Mode && Band == other.Band && Linkage == other.Linkage &&
                SceneMode == other.SceneMode &&
                Near(ManualSpeed, other.ManualSpeed) && Near(CurrentSpeed, other.CurrentSpeed) &&
                Near(TargetSpeed, other.TargetSpeed) && Near(GaugeMale, other.GaugeMale) &&
                Near(GaugeFemale, other.GaugeFemale) && Near(RangeMin, other.RangeMin) &&
                Near(RangeMax, other.RangeMax) && Near(StrengthMinSeconds, other.StrengthMinSeconds) &&
                Near(StrengthMaxSeconds, other.StrengthMaxSeconds);
        }
        private static bool Near(float a, float b) { return Math.Abs(a - b) < .001f; }
    }

    /// <summary>
    /// HCtrl.GUIFunc parity order, verified against the installed original DLL:
    /// female/male gauge LOCK+slider -> automatic speed radios / strength ->
    /// mode-specific speed -> male/female auto climax + replay ->
    /// loop/weak/strong -> three pre-climax -> three climax actions.
    /// No separate tabs or hidden action pages.
    /// </summary>
    internal sealed class HSControlView : View
    {
        internal HSControlViewModel Model = new HSControlViewModel();

        public override Widget Build(BuildContext context)
        {
            HSControlViewModel m = Model;
            Window window = new Window("HSControl " + m.Version, 470f, 545f,
                key: "hscontrol-window", minWidth: 440f, minHeight: 525f,
                closable: true, onVisibleChange: visible => {
                    if (!visible && m.Close != null) m.Close();
                });

            var controls = new VStack(4f, "hscontrol-original-layout") {
                new HStack(6f) {
                    new Checkbox("启用 HSControl", m.Enabled,
                        m.ChangeEnabled, "hscontrol-enabled").Width(152f),
                    new Label(m.Available ? "模式 " + m.SceneMode + " · " + m.AnimationState :
                        "未进入 H 场景", "hscontrol-native-state").ExpandWidth()
                }.Height(28f),

                // Original GUIFunc begins with the two clickable gauge-lock
                // labels and numeric sliders (female first, male second).
                new HStack(6f) {
                    new Button(m.FemaleLocked ? "女感度 [锁]" : "女感度",
                        m.ToggleFemaleLock, "hscontrol-female-lock") {
                        Variant = m.FemaleLocked ? ButtonVariant.Danger : ButtonVariant.Quiet
                    }.Enabled(m.Enabled && m.Available).Width(112f),
                    new SliderFloat("", m.GaugeFemale, 0f, 100f,
                        m.ChangeGaugeFemale, "hscontrol-gauge-female")
                        .Enabled(m.Enabled && m.Available).ExpandWidth()
                }.Height(33f),
                new HStack(6f) {
                    new Button(m.MaleLocked ? "男感度 [锁]" : "男感度",
                        m.ToggleMaleLock, "hscontrol-male-lock") {
                        Variant = m.MaleLocked ? ButtonVariant.Danger : ButtonVariant.Quiet
                    }.Enabled(m.Enabled && m.Available).Width(112f),
                    new SliderFloat("", m.GaugeMale, 0f, 100f,
                        m.ChangeGaugeMale, "hscontrol-gauge-male")
                        .Enabled(m.Enabled && m.Available).ExpandWidth()
                }.Height(33f),

                // Match HCtrl.GUIFunc's actual TWO speed-control rows,
                // rather than squeezing four modes into one tiny line:
                // first Off / auto strong, then half / range / linkage.
                new HStack(7f) {
                    new Label("自动速度", "hscontrol-speed-heading").Width(75f),
                    new RadioButton("关闭", m.Mode == 0, () => m.ChangeMode(0),
                        "hscontrol-mode-none").ExpandWidth(),
                    new Checkbox("自动强弱", m.AutoStrength, m.ChangeAutoStrength,
                        "hscontrol-strength")
                        .Enabled(m.Enabled && m.SceneMode != 0).ExpandWidth()
                }.Height(31f),
                new HStack(7f) {
                    new RadioButton("慢 / 全 / 快", m.Mode == 1, () => m.ChangeMode(1),
                        "hscontrol-mode-half").ExpandWidth(),
                    new RadioButton("范围", m.Mode == 2, () => m.ChangeMode(2),
                        "hscontrol-mode-range").ExpandWidth(),
                    new RadioButton("感度联动", m.Mode == 3, () => m.ChangeMode(3),
                        "hscontrol-mode-linkage").ExpandWidth()
                }.Enabled(m.Enabled && m.Available).Height(31f)
            };

            if (m.Mode == 1) {
                controls.Add(new HStack(5f) {
                    new Label("速度档位", "hscontrol-band-label").Width(88f),
                    new ComboBox<int>("", m.Band, new[] {0,1,2},
                        i => i == 0 ? "慢" : i == 1 ? "全" : "快",
                        m.ChangeBand, "hscontrol-band").Enabled(m.Enabled).ExpandWidth()
                }.Height(34f));
            } else if (m.Mode == 2) {
                controls.Add(new HStack(5f) {
                    new SliderFloat("速度1 / 下限", m.RangeMin, 0f, 1f,
                        m.ChangeRangeMin, "hscontrol-min")
                        .Enabled(m.Enabled).ExpandWidth(),
                    new SliderFloat("速度2 / 上限", m.RangeMax, 0f, 1f,
                        m.ChangeRangeMax, "hscontrol-max")
                        .Enabled(m.Enabled).ExpandWidth()
                }.Height(36f));
            } else if (m.Mode == 3) {
                controls.Add(new HStack(5f) {
                    new Label("联动方式", "hscontrol-link-label").Width(88f),
                    new ComboBox<int>("", m.Linkage, new[] {0,1},
                        i => i == 0 ? "男女范围" : "男女最大",
                        m.ChangeLinkage, "hscontrol-linkage")
                        .Enabled(m.Enabled && m.SceneMode != 0).ExpandWidth()
                }.Height(34f));
            } else {
                controls.Add(new HStack(5f) {
                    new Label("手动速度", "hscontrol-manual-label").Width(88f),
                    new SliderFloat("", m.ManualSpeed, 0f, 1f,
                        m.ChangeManualSpeed, "hscontrol-manual-speed")
                        .Enabled(m.Enabled && m.Available && m.ManualOverride).ExpandWidth(),
                    new Checkbox("启用", m.ManualOverride, m.ChangeManualOverride,
                        "hscontrol-manual").Enabled(m.Enabled).Width(92f)
                }.Height(36f));
            }

            controls.Add(new HStack(6f) {
                new Label("自动速度同步爱抚",
                    "hscontrol-aibu-explain").ExpandWidth(),
                new Label("速度 " + m.CurrentSpeed.ToString("0.00") +
                    " → " + m.TargetSpeed.ToString("0.00"),
                    "hscontrol-current-readout").Width(155f)
            }.Height(30f));
            if (m.AutoStrength && m.SceneMode != 0) {
                controls.Add(new HStack(6f) {
                    new SliderFloat("最短间隔", m.StrengthMinSeconds, 1f, 120f,
                        m.ChangeStrengthMin, "hscontrol-strength-min")
                        .Enabled(m.Enabled).ExpandWidth(),
                    new SliderFloat("最长间隔", m.StrengthMaxSeconds, 1f, 120f,
                        m.ChangeStrengthMax, "hscontrol-strength-max")
                        .Enabled(m.Enabled).ExpandWidth()
                }.Height(32f));
            }

            controls.Add(new HStack(6f) {
                new Checkbox("男自动高潮", m.AutoMaleFinish, m.ChangeAutoMaleFinish,
                    "hscontrol-male-finish").Enabled(m.Enabled).ExpandWidth(),
                new Checkbox("女自动高潮", m.AutoFemaleFinish, m.ChangeAutoFemaleFinish,
                    "hscontrol-female-finish").Enabled(m.Enabled).ExpandWidth(),
                new Checkbox("自动再开", m.AutoReplay, m.ChangeAutoReplay,
                    "hscontrol-replay").Enabled(m.Enabled).ExpandWidth()
            }.Height(30f));

            controls.Add(new Separator().Height(8f));
            controls.Add(new HStack(6f) {
                new Button("循环", m.PlayLoop, "hscontrol-play-loop")
                    .Enabled(m.CanForceLoop).ExpandWidth(),
                new Button("弱", m.PlayWeak, "hscontrol-play-weak")
                    .Enabled(m.CanForceLoop).ExpandWidth(),
                new Button("强", m.PlayStrong, "hscontrol-play-strong")
                    .Enabled(m.CanForceLoop).ExpandWidth()
            }.Height(36f));
            controls.Add(new HStack(6f) {
                new Button("男高潮前", m.StageMale, "hscontrol-stage-male")
                    .Enabled(m.CanStageMale).ExpandWidth(),
                new Button("女高潮前", m.StageFemale, "hscontrol-stage-female")
                    .Enabled(m.CanStageFemale).ExpandWidth(),
                new Button("双方高潮前", m.StageBoth, "hscontrol-stage-both")
                    .Enabled(m.CanStageBoth).ExpandWidth()
            }.Height(36f));
            controls.Add(new HStack(6f) {
                new Button("男高潮", m.FinishMale, "hscontrol-finish-male")
                    .Enabled(m.CanFinishMale).ExpandWidth(),
                new Button("女高潮", m.FinishFemale, "hscontrol-finish-female")
                    .Enabled(m.CanFemaleFinish).ExpandWidth(),
                new Button("双方高潮", m.FinishBoth, "hscontrol-finish-both")
                    .Enabled(m.CanFinishBoth).ExpandWidth()
            }.Height(36f));
            controls.Add(new Label(m.Status, "hscontrol-status") {
                WordWrap = true
            }.MinHeight(23f));
            // No ScrollView, no Tabs: the original HCtrl order is always visible.
            window.Add(controls.ExpandWidth().ExpandHeight());
            return window;
        }
    }
}
