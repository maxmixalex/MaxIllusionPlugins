using System;
using MaximumUI;

namespace LoveJuice {
    internal sealed class LoveJuiceViewModel {
        internal string Version = "0.3.0", Status = "就绪", Integration = "未连接";
        internal bool Enabled, HasScene, Face, Body, Milk, Shower, Special, Collision;
        internal int ActorCount, ActiveParticles, MaximumParticles, PreviewChannel;
        internal string[] ActorNames = new string[0];
        internal int SelectedActorIndex = -1;
        internal float Interval, Gravity, Damping, Lifetime, Scale, LaunchSpeed, MilkThreshold;
        internal Action<bool> ChangeEnabled, ChangeFace, ChangeBody, ChangeMilk,
            ChangeShower, ChangeSpecial, ChangeCollision;
        internal Action<int> ChangeMaximum, ChangePreviewChannel, SelectActor;
        internal Action<float> ChangeInterval, ChangeGravity, ChangeDamping,
            ChangeLifetime, ChangeScale, ChangeLaunchSpeed, ChangeMilkThreshold;
        internal Action Preview, Close;

        internal bool SameDisplayAs(LoveJuiceViewModel other) {
            return other != null && Version == other.Version && Status == other.Status &&
                Integration == other.Integration && Enabled == other.Enabled &&
                HasScene == other.HasScene && ActorCount == other.ActorCount &&
                ActiveParticles == other.ActiveParticles && MaximumParticles == other.MaximumParticles &&
                PreviewChannel == other.PreviewChannel &&
                SelectedActorIndex == other.SelectedActorIndex &&
                SameNames(ActorNames, other.ActorNames) &&
                Face == other.Face && Body == other.Body &&
                Milk == other.Milk && Shower == other.Shower && Special == other.Special &&
                Collision == other.Collision && Near(Interval, other.Interval) &&
                Near(Gravity, other.Gravity) && Near(Damping, other.Damping) &&
                Near(Lifetime, other.Lifetime) && Near(Scale, other.Scale) &&
                Near(LaunchSpeed, other.LaunchSpeed) && Near(MilkThreshold, other.MilkThreshold);
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

        public override Widget Build(BuildContext context) {
            LoveJuiceViewModel m = Model;
            Window window = new Window("LoveJuice " + m.Version, 430f, 700f,
                key: "lovejuice-window", minWidth: 390f, minHeight: 540f,
                closable: true, onVisibleChange: visible => {
                    if (!visible && m.Close != null) m.Close();
                });
            var controls = new VStack(6f) {
                new Label("Studio 原生粒子素材", "lovejuice-sources"),
                new Checkbox("面部滴落", m.Face, m.ChangeFace, "lovejuice-face").Enabled(m.Enabled).Height(32f),
                new Checkbox("身体滴落", m.Body, m.ChangeBody, "lovejuice-body").Enabled(m.Enabled).Height(32f),
                new Checkbox("乳液效果", m.Milk, m.ChangeMilk, "lovejuice-milk").Enabled(m.Enabled).Height(32f),
                new Checkbox("淋浴效果", m.Shower, m.ChangeShower, "lovejuice-shower").Enabled(m.Enabled).Height(32f),
                new Checkbox("FemboyMode 附加效果", m.Special, m.ChangeSpecial,
                    "lovejuice-special").Enabled(m.Enabled).Height(32f),
                new Separator(),
                new Label("物理与性能", "lovejuice-physics"),
                new SliderFloat("发射间隔", m.Interval, .06f, 4f, m.ChangeInterval, "lovejuice-interval")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("重力", m.Gravity, 0f, 30f, m.ChangeGravity, "lovejuice-gravity")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("空气阻力", m.Damping, 0f, 10f, m.ChangeDamping, "lovejuice-damping")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("持续时间", m.Lifetime, .2f, 8f, m.ChangeLifetime, "lovejuice-lifetime")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("粒子尺寸", m.Scale, .001f, .3f, m.ChangeScale, "lovejuice-scale")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("初始速度", m.LaunchSpeed, 0f, 5f, m.ChangeLaunchSpeed, "lovejuice-launch")
                    .Enabled(m.Enabled).Height(36f),
                new SliderFloat("乳液触发值", m.MilkThreshold, 0f, 100f, m.ChangeMilkThreshold, "lovejuice-milk-threshold")
                    .Enabled(m.Enabled && m.Milk).Height(36f),
                new Checkbox("世界碰撞", m.Collision, m.ChangeCollision, "lovejuice-collision")
                    .Enabled(m.Enabled).Height(32f),
                new SliderInt("最大粒子数", m.MaximumParticles, 1, 128, m.ChangeMaximum, "lovejuice-maximum")
                    .Enabled(m.Enabled).Height(40f)
            };
            window.Add(new VStack(5f) {
                new Checkbox("启用 LoveJuice", m.Enabled, m.ChangeEnabled,
                    "lovejuice-enabled").Height(34f),
                new HStack(5f) {
                    new Label("场景角色：" + m.ActorCount, "lovejuice-actors").ExpandWidth(),
                    new Label("存活粒子：" + m.ActiveParticles + " / " + m.MaximumParticles,
                        "lovejuice-active").ExpandWidth()
                }.Height(28f),
                new ComboBox<int>("预览角色", m.SelectedActorIndex,
                    ActorChoices(m.ActorNames),
                    i => i >= 0 && i < m.ActorNames.Length ? m.ActorNames[i] : "无角色",
                    m.SelectActor, "lovejuice-target")
                    .Enabled(m.HasScene && m.ActorNames.Length > 0).ExpandWidth().Height(36f),
                new ScrollView("lovejuice-scroll") { controls.ExpandWidth() }.ExpandWidth().ExpandHeight(),
                new HStack(5f) {
                    new ComboBox<int>("预览类型", m.PreviewChannel, new[] { 0, 1, 2, 3, 4, 5 },
                        i => i == 0 ? "水滴" : i == 1 ? "口水" : i == 2 ? "身体" :
                            i == 3 ? "淋浴" : i == 4 ? "乳液" : "附加",
                        m.ChangePreviewChannel, "lovejuice-preview-channel").Enabled(m.Enabled).ExpandWidth(),
                    new Button("实际发射", m.Preview, "lovejuice-emit")
                        .Enabled(m.Enabled && m.HasScene && m.ActorCount > 0).Width(106f)
                }.Height(38f),
                new Label(m.Integration, "lovejuice-integration") { WordWrap = true }.MinHeight(20f),
                new Label(m.Status, "lovejuice-status") { WordWrap = true }.MinHeight(20f)
            }.ExpandHeight());
            return window;
        }
    }
}
