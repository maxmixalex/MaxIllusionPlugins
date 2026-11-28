using System;
using MaximumUI;

namespace LoveJuice {
    internal sealed class LoveJuiceViewModel {
        internal string Version = "0.4.0", Status = "就绪", Integration = "未连接";
        internal string NativeSourceState = "原生物理源：未检查";
        internal string CurrentAnimation = string.Empty;
        internal int SelectedFaceLevel = -1, SelectedBodyLevel = -1,
            SelectedIntimateLevel = -1;
        internal bool SelectedMalePreReady;
        internal bool Enabled, HasScene, Face, SalivaAnytime, ExperimentalSpheres, Body, Intimate, MalePre, Milk, Shower, Special, Collision;
        internal int ActorCount, ActiveParticles, MaximumParticles, PreviewChannel;
        internal string[] ActorNames = new string[0];
        internal int SelectedActorIndex = -1;
        internal float Interval, Gravity, Damping, Lifetime, Scale, LaunchSpeed, MilkThreshold, MalePreThreshold;
        internal Action<bool> ChangeEnabled, ChangeFace, ChangeSalivaAnytime, ChangeExperimentalSpheres, ChangeBody, ChangeIntimate, ChangeMalePre, ChangeMilk,
            ChangeShower, ChangeSpecial, ChangeCollision;
        internal Action<int> ChangeMaximum, ChangePreviewChannel, SelectActor;
        internal Action<float> ChangeInterval, ChangeGravity, ChangeDamping,
            ChangeLifetime, ChangeScale, ChangeLaunchSpeed, ChangeMilkThreshold, ChangeMalePreThreshold;
        internal Action Preview, InspectStudio, Close;

        internal bool SameDisplayAs(LoveJuiceViewModel other) {
            return other != null && Version == other.Version && Status == other.Status &&
                Integration == other.Integration &&
                NativeSourceState == other.NativeSourceState &&
                CurrentAnimation == other.CurrentAnimation &&
                SelectedFaceLevel == other.SelectedFaceLevel &&
                SelectedBodyLevel == other.SelectedBodyLevel &&
                SelectedIntimateLevel == other.SelectedIntimateLevel &&
                SelectedMalePreReady == other.SelectedMalePreReady &&
                Enabled == other.Enabled &&
                HasScene == other.HasScene && ActorCount == other.ActorCount &&
                ActiveParticles == other.ActiveParticles && MaximumParticles == other.MaximumParticles &&
                PreviewChannel == other.PreviewChannel &&
                SelectedActorIndex == other.SelectedActorIndex &&
                SameNames(ActorNames, other.ActorNames) &&
                Face == other.Face && SalivaAnytime == other.SalivaAnytime &&
                ExperimentalSpheres == other.ExperimentalSpheres && Body == other.Body && Intimate == other.Intimate && MalePre == other.MalePre &&
                Milk == other.Milk && Shower == other.Shower && Special == other.Special &&
                Collision == other.Collision && Near(Interval, other.Interval) &&
                Near(Gravity, other.Gravity) && Near(Damping, other.Damping) &&
                Near(Lifetime, other.Lifetime) && Near(Scale, other.Scale) &&
                Near(LaunchSpeed, other.LaunchSpeed) && Near(MilkThreshold, other.MilkThreshold) &&
                Near(MalePreThreshold, other.MalePreThreshold);
        }
        private static bool SameNames(string[] a, string[] b) {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static bool Near(float a, float b) { return Math.Abs(a - b) < .0001f; }
    }

    // Production and headless preview share the exact same widget composition.
    internal sealed class LoveJuiceView : View {
        internal LoveJuiceViewModel Model = new LoveJuiceViewModel();

        private static int[] ActorChoices(string[] names) {
            int length = names == null ? 0 : names.Length;
            int[] result = new int[length];
            for (int i = 0; i < length; i++) result[i] = i;
            return result;
        }

        internal bool AdvancedOpen;

        public override Widget Build(BuildContext context) {
            LoveJuiceViewModel m = Model;
            Window window = new Window("LoveJuice " + m.Version, 468f, 565f,
                key: "lovejuice-window", minWidth: 410f, minHeight: 490f,
                closable: true, onVisibleChange: visible => {
                    if (!visible && m.Close != null) m.Close();
                });

            var primary = new VStack(5f, "lovejuice-automatic-sources") {
                new Label("自由 H 中自动运行。面板用于开关和诊断，不需要持续打开。",
                    "lovejuice-how-to") { WordWrap = true }.MinHeight(38f),
                new Label("实际条件（仅有事件/状态时产生效果）",
                    "lovejuice-triggers-title").Height(26f),
                new Label("所选角色实际读数：面部 " +
                    (m.SelectedFaceLevel < 0 ? "—" : m.SelectedFaceLevel.ToString()) +
                    " / 身体 " +
                    (m.SelectedBodyLevel < 0 ? "—" : m.SelectedBodyLevel.ToString()) +
                    " / 爱液 " +
                    (m.SelectedIntimateLevel < 0 ? "—" :
                        m.SelectedIntimateLevel.ToString()),
                    "lovejuice-live-readback") { WordWrap = true }.MinHeight(30f),
                new Label(m.NativeSourceState, "lovejuice-native-host-state") {
                    WordWrap = true
                }.MinHeight(30f),
                new Checkbox("口水拉丝", m.Face, m.ChangeFace, "lovejuice-face")
                    .Enabled(m.Enabled).Height(28f),
                new Label("亲吻，或开启扩展后：口部动作 / 面部口水状态",
                    "lovejuice-saliva-condition").Height(26f),
                new Checkbox("其他口部场景也允许", m.SalivaAnytime,
                    m.ChangeSalivaAnytime, "lovejuice-saliva-anytime")
                    .Enabled(m.Enabled && m.Face).Height(28f),
                new Checkbox("身体液体", m.Body, m.ChangeBody,
                    "lovejuice-body").Enabled(m.Enabled).Height(28f),
                new Label("原生身体 Siru 状态或 SkinEffects WetBody 等级变化/非零",
                    "lovejuice-body-condition") { WordWrap = true }.MinHeight(33f),
                new Checkbox("爱液", m.Intimate, m.ChangeIntimate,
                    "lovejuice-intimate").Enabled(m.Enabled).Height(28f),
                new Label("需要 SkinEffects 的 PussyJuiceBody 读数；没有则不自动产生",
                    "lovejuice-intimate-condition") { WordWrap = true }.MinHeight(33f),
                new Checkbox("男性前液（代理条件）", m.MalePre,
                    m.ChangeMalePre, "lovejuice-male-pre")
                    .Enabled(m.Enabled).Height(28f),
                new Label("仅主男性，循环动作中男性快感达到设定阈值；并非真实前液事件",
                    "lovejuice-pre-condition") { WordWrap = true }.MinHeight(34f)
            };

            var advanced = new Foldout("高级：可选代理来源、参数与原生资源检查",
                AdvancedOpen, opened => {
                    AdvancedOpen = opened;
                    MaximumUIRuntime.MarkDirty(this);
                }, "lovejuice-advanced");
            advanced.Add(new VStack(6f, "lovejuice-advanced-body") {
                new Label("以下条件是可选的代理，不代表游戏真实液体事件。",
                    "lovejuice-proxy-warning") { WordWrap = true }.MinHeight(33f),
                new Checkbox("乳液：女性快感代理", m.Milk, m.ChangeMilk,
                    "lovejuice-milk").Enabled(m.Enabled).Height(30f),
                new Checkbox("淋浴：动作名含 shower", m.Shower,
                    m.ChangeShower, "lovejuice-shower")
                    .Enabled(m.Enabled).Height(30f),
                new Checkbox("FemboyMode 额外来源", m.Special,
                    m.ChangeSpecial, "lovejuice-special")
                    .Enabled(m.Enabled).Height(30f),
                new SliderFloat("男性前液快感阈值", m.MalePreThreshold, 0f, 100f,
                    m.ChangeMalePreThreshold, "lovejuice-male-pre-threshold")
                    .Enabled(m.Enabled && m.MalePre).Height(38f),
                new SliderFloat("乳液快感阈值", m.MilkThreshold, 0f, 100f,
                    m.ChangeMilkThreshold, "lovejuice-milk-threshold")
                    .Enabled(m.Enabled && m.Milk).Height(38f),
                new SliderFloat("源滴落间隔", m.Interval, .06f, 4f,
                    m.ChangeInterval, "lovejuice-interval")
                    .Enabled(m.Enabled).Height(38f),
                new SliderFloat("初始速度（原生物理）", m.LaunchSpeed, 0f, 5f,
                    m.ChangeLaunchSpeed, "lovejuice-launch")
                    .Enabled(m.Enabled).Height(38f),
                new SliderInt("最大液滴数", m.MaximumParticles, 1, 128,
                    m.ChangeMaximum, "lovejuice-maximum")
                    .Enabled(m.Enabled).Height(38f),
                new Button("只读检查本机 Studio 液体资源", m.InspectStudio,
                    "lovejuice-inspect-studio").Height(34f),
                new Checkbox("旧版球体/贴图拖尾（仅实验）", m.ExperimentalSpheres,
                    m.ChangeExperimentalSpheres, "lovejuice-experimental-spheres")
                    .Enabled(m.Enabled).Height(30f),
                new Label("以下物理参数仅作用于旧版实验渲染器",
                    "lovejuice-experimental-heading").Height(26f),
                new SliderFloat("实验重力", m.Gravity, 0f, 30f,
                    m.ChangeGravity, "lovejuice-gravity")
                    .Enabled(m.Enabled && m.ExperimentalSpheres).Height(36f),
                new SliderFloat("实验阻力", m.Damping, 0f, 10f,
                    m.ChangeDamping, "lovejuice-damping")
                    .Enabled(m.Enabled && m.ExperimentalSpheres).Height(36f),
                new SliderFloat("实验持续时间", m.Lifetime, .2f, 8f,
                    m.ChangeLifetime, "lovejuice-lifetime")
                    .Enabled(m.Enabled && m.ExperimentalSpheres).Height(36f),
                new SliderFloat("实验尺寸", m.Scale, .001f, .3f,
                    m.ChangeScale, "lovejuice-scale")
                    .Enabled(m.Enabled && m.ExperimentalSpheres).Height(36f),
                new Checkbox("实验世界碰撞", m.Collision,
                    m.ChangeCollision, "lovejuice-collision")
                    .Enabled(m.Enabled && m.ExperimentalSpheres).Height(30f)
            }.ExpandWidth());

            window.Add(new VStack(5f) {
                new Checkbox("启用 LoveJuice（默认关闭）", m.Enabled,
                    m.ChangeEnabled, "lovejuice-enabled").Height(34f),
                new HStack(5f) {
                    new Label(m.HasScene ? "已进入 H 场景" : "未进入 H 场景",
                        "lovejuice-scene-state").ExpandWidth(),
                    new Label("角色 " + m.ActorCount + " · 物理源 " +
                        m.ActiveParticles, "lovejuice-active").ExpandWidth()
                }.Height(29f),
                new ScrollView("lovejuice-scroll") {
                    primary.ExpandWidth(),
                    advanced.ExpandWidth()
                }.ExpandWidth().ExpandHeight(),
                new HStack(5f) {
                    new ComboBox<int>("测试角色", m.SelectedActorIndex,
                        ActorChoices(m.ActorNames),
                        i => i >= 0 && i < m.ActorNames.Length
                            ? m.ActorNames[i] : "无",
                        m.SelectActor, "lovejuice-target")
                        .Enabled(m.HasScene && m.ActorNames.Length > 0).ExpandWidth(),
                    new ComboBox<int>("来源", m.PreviewChannel,
                        new[] {0, 1, 2, 3, 4, 5, 6, 7},
                        i => i == 0 ? "水" : i == 1 ? "口水" :
                            i == 2 ? "身体" : i == 3 ? "淋浴" :
                            i == 4 ? "乳液" : i == 5 ? "附加" :
                            i == 6 ? "爱液" : "前液",
                        m.ChangePreviewChannel, "lovejuice-preview-channel")
                        .Enabled(m.Enabled).Width(115f),
                    new Button("测试", m.Preview, "lovejuice-emit")
                        .Enabled(m.Enabled && m.HasScene && m.ActorCount > 0)
                        .Width(58f)
                }.Height(38f),
                new Label(m.Integration, "lovejuice-integration") {
                    WordWrap = true
                }.MinHeight(20f),
                new Label(m.Status, "lovejuice-status") {
                    WordWrap = true
                }.MinHeight(20f)
            }.ExpandHeight());
            return window;
        }

    }
}
