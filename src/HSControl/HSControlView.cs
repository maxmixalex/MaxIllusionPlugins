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

        // Compact, stable page selection: no full-height nested scroll container.
        // The original 650px stacked page made actions inaccessible at 720p.
        private int selectedPage;

        private VStack BuildSpeed(HSControlViewModel m) {
            var content = new VStack(7f, "hscontrol-speed-page") {
                new HStack(7f) {
                    new ComboBox<int>("自动速度", m.Mode, new[] {0, 1, 2, 3},
                        i => i == 0 ? "关闭" : i == 1 ? "慢 / 全 / 快" :
                            i == 2 ? "随机区间" : "快感联动",
                        m.ChangeMode, "hscontrol-speed-mode").ExpandWidth(),
                    new Checkbox("手动覆盖", m.ManualOverride, m.ChangeManualOverride,
                        "hscontrol-manual").Enabled(m.Enabled).Width(128f)
                }.Height(36f)
            };
            if (m.ManualOverride) {
                content.Add(new SliderFloat("手动速度", m.ManualSpeed, 0f, 1f,
                    m.ChangeManualSpeed, "hscontrol-manual-speed")
                    .Enabled(m.Enabled && m.Available).Height(38f));
            }
            if (m.Mode == 1) {
                content.Add(new ComboBox<int>("速度档位", m.Band, new[] {0, 1, 2},
                    i => i == 0 ? "慢速" : i == 1 ? "全范围切换" : "快速",
                    m.ChangeBand, "hscontrol-band").Enabled(m.Enabled).ExpandWidth().Height(36f));
            } else if (m.Mode == 2) {
                content.Add(new HStack(7f) {
                    new SliderFloat("下限", m.RangeMin, 0f, 1f,
                        m.ChangeRangeMin, "hscontrol-min").Enabled(m.Enabled).ExpandWidth(),
                    new SliderFloat("上限", m.RangeMax, 0f, 1f,
                        m.ChangeRangeMax, "hscontrol-max").Enabled(m.Enabled).ExpandWidth()
                }.Height(38f));
            } else if (m.Mode == 3) {
                content.Add(new ComboBox<int>("联动", m.Linkage, new[] {0, 1},
                    i => i == 0 ? "男女平均" : "较高快感值",
                    m.ChangeLinkage, "hscontrol-linkage").Enabled(m.Enabled).ExpandWidth().Height(36f));
            }
            content.Add(new HStack(6f) {
                new Label("当前 " + m.CurrentSpeed.ToString("0.00")).ExpandWidth(),
                new Label("目标 " + m.TargetSpeed.ToString("0.00")).ExpandWidth(),
                new Button("重新采样", m.Reroll, "hscontrol-reroll")
                    .Enabled(m.Available && m.Enabled && m.Mode != 0).Width(92f)
            }.Height(30f));
            content.Add(new ProgressBar(m.CurrentSpeed, null,
                "hscontrol-current").Height(24f));
            content.Add(new Label(m.Mode != 0 || m.ManualOverride ?
                "自动爱抚只在可用的原生爱抚动作中工作；手动输入优先。" :
                "自动速度已关闭，保留游戏原生爱抚控制。").Height(28f));
            return content;
        }

        private static VStack BuildScene(HSControlViewModel m) {
            return new VStack(7f, "hscontrol-scene-page") {
                new HStack(6f) {
                    new SliderFloat("男性快感", m.GaugeMale, 0f, 100f,
                        m.ChangeGaugeMale, "hscontrol-gauge-male")
                        .Enabled(m.Available && m.Enabled).ExpandWidth(),
                    new SliderFloat("女性快感", m.GaugeFemale, 0f, 100f,
                        m.ChangeGaugeFemale, "hscontrol-gauge-female")
                        .Enabled(m.Available && m.Enabled).ExpandWidth()
                }.Height(40f),
                new HStack(7f) {
                    new Checkbox("自动男性高潮", m.AutoMaleFinish,
                        m.ChangeAutoMaleFinish, "hscontrol-male-finish").ExpandWidth(),
                    new Checkbox("完成后继续", m.AutoReplay,
                        m.ChangeAutoReplay, "hscontrol-replay").ExpandWidth()
                }.Height(30f),
                new HStack(5f) {
                    new Button("弱循环", m.PlayWeak, "hscontrol-play-weak")
                        .Enabled(m.CanForceLoop).ExpandWidth(),
                    new Button("强循环", m.PlayStrong, "hscontrol-play-strong")
                        .Enabled(m.CanForceLoop).ExpandWidth()
                }.Height(34f),
                new HStack(5f) {
                    new Button("男高潮前", m.StageMale, "hscontrol-stage-male")
                        .Enabled(m.CanStage).ExpandWidth(),
                    new Button("女高潮前", m.StageFemale, "hscontrol-stage-female")
                        .Enabled(m.CanStage).ExpandWidth(),
                    new Button("双方高潮前", m.StageBoth, "hscontrol-stage-both")
                        .Enabled(m.CanStage).ExpandWidth()
                }.Height(34f),
                new HStack(5f) {
                    new Button("男性高潮", m.FinishMale, "hscontrol-finish-male")
                        .Enabled(m.CanStage).ExpandWidth(),
                    new Button("女性高潮", m.FinishFemale, "hscontrol-finish-female")
                        .Enabled(m.CanFemaleFinish).ExpandWidth(),
                    new Button("双方高潮", m.FinishBoth, "hscontrol-finish-both")
                        .Enabled(m.CanFemaleFinish).ExpandWidth()
                }.Height(34f)
            };
        }

        private static VStack BuildAutomation(HSControlViewModel m) {
            return new VStack(7f, "hscontrol-auto-page") {
                new Checkbox("自动切换动作强度", m.AutoStrength,
                    m.ChangeAutoStrength, "hscontrol-strength").Height(32f),
                new HStack(7f) {
                    new SliderFloat("最短间隔", m.StrengthMinSeconds, 1f, 120f,
                        m.ChangeStrengthMin, "hscontrol-strength-min")
                        .Enabled(m.Enabled && m.AutoStrength).ExpandWidth(),
                    new SliderFloat("最长间隔", m.StrengthMaxSeconds, 1f, 120f,
                        m.ChangeStrengthMax, "hscontrol-strength-max")
                        .Enabled(m.Enabled && m.AutoStrength).ExpandWidth()
                }.Height(40f),
                new Label("自动强度与自动高潮分别受控；关闭不会限制游戏原生快感值。")
                    .Height(42f)
            };
        }

        public override Widget Build(BuildContext context) {
            HSControlViewModel m = Model;
            Window window = new Window("HSControl " + m.Version, 488f, 378f,
                key: "hscontrol-window", minWidth: 448f, minHeight: 348f,
                closable: true, onVisibleChange: delegate(bool value) {
                    if (!value && m.Close != null) m.Close();
                });
            var pages = new Tabs(selectedPage, index => {
                selectedPage = index;
                MaximumUIRuntime.MarkDirty(this);
            }, "hscontrol-pages") {
                new Tab("速度", "hscontrol-tab-speed") { BuildSpeed(m) },
                new Tab("状态", "hscontrol-tab-scene") { BuildScene(m) },
                new Tab("自动化", "hscontrol-tab-auto") { BuildAutomation(m) }
            };
            window.Add(new VStack(5f) {
                new Checkbox("启用 HSControl", m.Enabled, m.ChangeEnabled,
                    "hscontrol-enabled").Height(29f),
                pages.ExpandWidth().ExpandHeight(),
                new Label(m.Available ? m.Status + " / " + m.AnimationState :
                    "当前场景无可用的 H 控制器", "hscontrol-status")
                    { WordWrap = true }.MinHeight(24f)
            }.ExpandHeight());
            return window;
        }

    }
}
