using System;
using MaximumUI;

namespace HSControl
{
    internal sealed class HSControlViewModel
    {
        internal string Version = "0.1.2", Status = "Ready", AnimationState = string.Empty;
        internal bool Available, Enabled, ManualOverride, AutoStrength;
        internal bool AutoMaleFinish, AutoFemaleFinish, AutoReplay, Driving;
        internal bool CanForceLoop, CanStage, CanFemaleFinish;
        internal float ManualSpeed, CurrentSpeed, TargetSpeed, GaugeMale, GaugeFemale;
        internal float RangeMin, RangeMax, StrengthMinSeconds, StrengthMaxSeconds;
        internal int Mode, Band, Linkage, SceneMode;
        internal Action<bool> ChangeEnabled, ChangeManualOverride, ChangeAutoStrength;
        internal Action<bool> ChangeAutoMaleFinish, ChangeAutoFemaleFinish, ChangeAutoReplay;
        internal Action<float> ChangeManualSpeed, ChangeGaugeMale, ChangeGaugeFemale;
        internal Action<float> ChangeRangeMin, ChangeRangeMax, ChangeStrengthMin, ChangeStrengthMax;
        internal Action<int> ChangeMode, ChangeBand, ChangeLinkage;
        internal Action Close, Reroll;
        internal Action PlayWeak, PlayStrong, StageMale, StageFemale, StageBoth;
        internal Action FinishMale, FinishFemale, FinishBoth;

        internal bool SameDisplay(HSControlViewModel other) {
            return other != null && Version == other.Version && Status == other.Status &&
                AnimationState == other.AnimationState && Available == other.Available &&
                Enabled == other.Enabled && ManualOverride == other.ManualOverride &&
                AutoStrength == other.AutoStrength && AutoMaleFinish == other.AutoMaleFinish &&
                AutoFemaleFinish == other.AutoFemaleFinish && AutoReplay == other.AutoReplay &&
                Driving == other.Driving && CanForceLoop == other.CanForceLoop &&
                CanStage == other.CanStage && CanFemaleFinish == other.CanFemaleFinish &&
                Mode == other.Mode && Band == other.Band &&
                Linkage == other.Linkage && SceneMode == other.SceneMode &&
                Math.Abs(ManualSpeed-other.ManualSpeed) < .001f &&
                Math.Abs(CurrentSpeed-other.CurrentSpeed) < .005f &&
                Math.Abs(TargetSpeed-other.TargetSpeed) < .005f &&
                Math.Abs(GaugeMale-other.GaugeMale) < .1f &&
                Math.Abs(GaugeFemale-other.GaugeFemale) < .1f &&
                Math.Abs(RangeMin-other.RangeMin) < .001f &&
                Math.Abs(RangeMax-other.RangeMax) < .001f &&
                Math.Abs(StrengthMinSeconds-other.StrengthMinSeconds) < .01f &&
                Math.Abs(StrengthMaxSeconds-other.StrengthMaxSeconds) < .01f;
        }
    }

    // The same independent MaximumUI window/retained controls used by
    // JuiceControl and ClothingControl; the runtime is the only game adapter.
    internal sealed class HSControlView : View
    {
        internal HSControlViewModel Model = new HSControlViewModel();

        public override Widget Build(BuildContext context) {
            HSControlViewModel m = Model;
            Window window = new Window("HSControl " + m.Version, 470f, 510f,
                key: "hscontrol-window", minWidth: 405f, minHeight: 350f,
                closable: true, onVisibleChange: delegate(bool value) {
                    if (!value && m.Close != null) m.Close();
                });
            VStack controls = new VStack(6f) {
                new Checkbox("启用 HSControl", m.Enabled, m.ChangeEnabled, "hscontrol-enabled").Height(30f),
                new Separator(),
                new Label("运动速度", "hscontrol-speed-heading"),
                new HStack(6f) {
                    new ComboBox<int>("自动速度", m.Mode, new[] { 0,1,2,3 },
                        i => i == 0 ? "关闭" : i == 1 ? "慢 / 全 / 快" :
                            i == 2 ? "随机区间" : "状态联动",
                        m.ChangeMode, "hscontrol-speed-mode").ExpandWidth(),
                    new Checkbox("手动覆盖", m.ManualOverride, m.ChangeManualOverride,
                        "hscontrol-manual").Enabled(m.Enabled).ExpandWidth()
                }.Height(36f),
                new SliderFloat("手动速度", m.ManualSpeed, 0f, 1f,
                    m.ChangeManualSpeed, "hscontrol-manual-speed").Enabled(m.Enabled && m.Available && m.ManualOverride).Height(40f),
                new ComboBox<int>("速度档位", m.Band, new[] { 0,1,2 },
                    i => i == 0 ? "慢速" : i == 1 ? "全范围切换" : "快速",
                    m.ChangeBand, "hscontrol-band").Enabled(m.Enabled && m.Mode == 1).ExpandWidth().Height(36f),
                new HStack(6f) {
                    new SliderFloat("下限", m.RangeMin, 0f, 1f,
                        m.ChangeRangeMin, "hscontrol-min").Enabled(m.Enabled && m.Mode == 2).ExpandWidth(),
                    new SliderFloat("上限", m.RangeMax, 0f, 1f,
                        m.ChangeRangeMax, "hscontrol-max").Enabled(m.Enabled && m.Mode == 2).ExpandWidth()
                }.Height(38f),
                new ComboBox<int>("联动方式", m.Linkage, new[] { 0,1 },
                    i => i == 0 ? "平均状态" : "较高状态",
                    m.ChangeLinkage, "hscontrol-linkage").Enabled(m.Enabled && m.Mode == 3).ExpandWidth().Height(36f),
                new Label("当前速度（实时）"),
                new ProgressBar(m.CurrentSpeed, null, "hscontrol-current").Height(28f),
                new HStack(4f) {
                    new Label("当前 " + m.CurrentSpeed.ToString("0.00")).ExpandWidth(),
                    new Label("目标 " + m.TargetSpeed.ToString("0.00")).ExpandWidth(),
                    new Button("重新采样", m.Reroll, "hscontrol-reroll").Enabled(m.Available && m.Enabled && m.Mode != 0).Width(96f)
                }.Height(30f),
                new Separator(),
                new Label("强度与计时", "hscontrol-strength-heading"),
                new Checkbox("自动切换强度", m.AutoStrength, m.ChangeAutoStrength, "hscontrol-strength").Height(30f),
                new HStack(6f) {
                    new SliderFloat("最短间隔", m.StrengthMinSeconds, 1f, 120f,
                        m.ChangeStrengthMin, "hscontrol-strength-min").Enabled(m.AutoStrength).ExpandWidth(),
                    new SliderFloat("最长间隔", m.StrengthMaxSeconds, 1f, 120f,
                        m.ChangeStrengthMax, "hscontrol-strength-max").Enabled(m.AutoStrength).ExpandWidth()
                }.Height(38f),
                new Separator(),
                new Label("场景控制", "hscontrol-scene-heading"),
                new HStack(6f) {
                    new SliderFloat("男性", m.GaugeMale, 0f, 100f,
                        m.ChangeGaugeMale, "hscontrol-gauge-male").Enabled(m.Available && m.Enabled).ExpandWidth(),
                    new SliderFloat("女性", m.GaugeFemale, 0f, 100f,
                        m.ChangeGaugeFemale, "hscontrol-gauge-female").Enabled(m.Available && m.Enabled).ExpandWidth()
                }.Height(42f),
                new HStack(6f) {
                    new Checkbox("自动男性结束", m.AutoMaleFinish, m.ChangeAutoMaleFinish,
                        "hscontrol-male-finish").ExpandWidth(),
                    new Checkbox("完成后自动继续", m.AutoReplay, m.ChangeAutoReplay,
                        "hscontrol-replay").ExpandWidth()
                }.Height(30f),
                new Separator(),
                new Label("原生动作", "hscontrol-actions-heading"),
                new HStack(4f) {
                    new Button("弱循环", m.PlayWeak, "hscontrol-play-weak").Enabled(m.CanForceLoop).ExpandWidth(),
                    new Button("强循环", m.PlayStrong, "hscontrol-play-strong").Enabled(m.CanForceLoop).ExpandWidth()
                }.Height(34f),
                new HStack(4f) {
                    new Button("男结束前", m.StageMale, "hscontrol-stage-male").Enabled(m.CanStage).ExpandWidth(),
                    new Button("女结束前", m.StageFemale, "hscontrol-stage-female").Enabled(m.CanStage).ExpandWidth(),
                    new Button("双方结束前", m.StageBoth, "hscontrol-stage-both").Enabled(m.CanStage).ExpandWidth()
                }.Height(34f),
                new HStack(4f) {
                    new Button("男性结束", m.FinishMale, "hscontrol-finish-male").Enabled(m.CanStage).ExpandWidth(),
                    new Button("女性结束", m.FinishFemale, "hscontrol-finish-female").Enabled(m.CanFemaleFinish).ExpandWidth(),
                    new Button("双方结束", m.FinishBoth, "hscontrol-finish-both").Enabled(m.CanFemaleFinish).ExpandWidth()
                }.Height(34f)
            };
            window.Add(new VStack(5f) {
                new ScrollView("hscontrol-scroll") { controls.ExpandWidth() }.ExpandHeight().ExpandWidth(),
                new Label(m.Available ? m.Status + " / " + m.AnimationState : "当前场景无可用的 H 控制器",
                    "hscontrol-status") { WordWrap = true }.MinHeight(24f)
            }.ExpandHeight());
            return window;
        }
    }
}
