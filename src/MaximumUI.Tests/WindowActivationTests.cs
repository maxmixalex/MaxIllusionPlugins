using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using MaximumUI;

namespace MaximumUI.Tests {
    internal static class WindowActivationTests {
        internal const int TestCount = 7;

        internal static void RunAll() {
            PublicFocusPromotesAWindowForDrawingAndHitTesting();
            PointerActivationPromotesOnlyTheActualPressOwner();
            ExplicitNonWindowLayersKeepTheirBoundaries();
            ModalOwnershipBlocksForeignActivation();
            HiddenAndDisabledWindowsCannotStealActivation();
            FocusBeforeFirstBuildIsAppliedAfterWindowDiscovery();
            ActivationSurvivesOtherMountsWithoutRebuildingSteadySnapshots();
            Console.WriteLine("MaximumUI window activation regressions: 7 groups passed.");
        }

        private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly MethodInfo FindMount = typeof(MaximumUIRuntime).GetMethod("FindMount", PrivateStatic);
        private static readonly MethodInfo GetSnapshot = typeof(MaximumUIRuntime).GetMethod("GetMountSnapshot", PrivateStatic);
        private static readonly MethodInfo ResolvePromotions = typeof(MaximumUIRuntime).GetMethod("ResolveWindowPromotions", PrivateStatic);
        private static readonly MethodInfo PromotePointer = typeof(MaximumUIRuntime).GetMethod("PromotePointerWindow", PrivateStatic);
        private static readonly FieldInfo InputSnapshot = typeof(MaximumUIRuntime).GetField("inputSnapshot", PrivateStatic);
        private static readonly FieldInfo InputRouter = typeof(MaximumUIRuntime).GetField("inputRouting", PrivateStatic);
        private static int fixtureCount;

        private sealed class TestView : View {
            internal Widget Root;
            internal BuildContext Context;
            public override void OnMount(BuildContext context) { Context = context; }
            public override Widget Build(BuildContext context) { return Root; }
        }

        private sealed class Mounted {
            internal string Id;
            internal string Name;
            internal object Record;
            internal TestView View;
            internal MountOptions Options;
            internal MountInputTarget Target {
                get { return (MountInputTarget)Record.GetType().GetField("InputTarget").GetValue(Record); }
            }
            internal void Build() {
                Record.GetType().GetField("CachedRoot").SetValue(Record, View.Build(View.Context));
            }
        }

        private sealed class Fixture : IDisposable {
            private readonly string prefix = "window-activation-" + (++fixtureCount) + "-";
            internal readonly List<Mounted> Mounts = new List<Mounted>();

            internal Mounted Add(string name, int order, Widget root, bool visible = true, bool build = true) {
                var mount = new Mounted {
                    Id = prefix + name, Name = name, View = new TestView { Root = root },
                    Options = new MountOptions {
                        SortOrder = order, Visible = visible, BuildEveryFrame = false,
                        Theme = (MaximumTheme)FormatterServices.GetUninitializedObject(typeof(MaximumTheme))
                    }
                };
                MaximumUIRuntime.Mount(mount.Id, mount.View, mount.Options);
                mount.Record = FindMount.Invoke(null, new object[] { mount.Id });
                Mounts.Add(mount);
                if (build) { mount.Build(); CompleteBuilds(); }
                return mount;
            }

            internal void CompleteBuilds() {
                // The real runtime invokes this after its View.Build loop. The
                // host supplies only that external GUI-event boundary here.
                ResolvePromotions.Invoke(null, new object[] { Snapshot(), true });
            }

            internal void ExpectOrder(params string[] names) {
                Array snapshot = Snapshot();
                var actual = new List<string>();
                foreach (object record in snapshot) {
                    string id = (string)record.GetType().GetField("Id").GetValue(record);
                    if (id.StartsWith(prefix, StringComparison.Ordinal)) actual.Add(id.Substring(prefix.Length));
                }
                Require(actual.Count == names.Length, "unexpected number of mounted layers");
                for (int i = 0; i < names.Length; i++)
                    Require(actual[i] == names[i], "draw order differs at " + i + ": " + string.Join(",", actual.ToArray()));
            }

            internal MountInputTarget[] Hit(params Mounted[] hits) {
                Snapshot();
                var targets = (MountInputTarget[])InputSnapshot.GetValue(null);
                for (int i = 0; i < targets.Length; i++) {
                    targets[i].PointerHit = false;
                    targets[i].HasModal = false;
                    targets[i].HasPointerCapture = false;
                }
                for (int i = 0; i < hits.Length; i++) hits[i].Target.PointerHit = true;
                return targets;
            }

            public void Dispose() {
                for (int i = Mounts.Count - 1; i >= 0; i--) MaximumUIRuntime.Unmount(Mounts[i].Id);
            }
        }

        private static Array Snapshot() { return (Array)GetSnapshot.Invoke(null, null); }
        private static MountInputRouting Router { get { return (MountInputRouting)InputRouter.GetValue(null); } }
        private static bool Promote(MuiEventType type) { return (bool)PromotePointer.Invoke(null, new object[] { type }); }
        private static Window Window(string title) { return new Window(title, key: title); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void PublicFocusPromotesAWindowForDrawingAndHitTesting() {
            using (var f = new Fixture()) {
                Mounted main = f.Add("main", 340, Window("main"));
                Mounted program = f.Add("program", 341, Window("program"));
                f.ExpectOrder("main", "program");
                Require(MaximumUIRuntime.Focus(main.Id), "visible main window rejected explicit focus");
                f.ExpectOrder("program", "main");
                Require(main.Options.SortOrder == 340 && program.Options.SortOrder == 341, "activation rewrote caller-owned initial layer values");
                Router.Route(f.Hit(main, program), MuiEventType.MouseDown, 0);
                Require(ReferenceEquals(Router.EventOwner, main.Target), "render order and overlapping-window hit order diverged");
                Router.Route(f.Hit(main, program), MuiEventType.MouseUp, 0);
            }
        }

        private static void PointerActivationPromotesOnlyTheActualPressOwner() {
            using (var f = new Fixture()) {
                Mounted main = f.Add("main", 340, Window("main"));
                Mounted program = f.Add("program", 341, Window("program"));
                Router.Route(f.Hit(main), MuiEventType.MouseMove, 0);
                Require(!Promote(MuiEventType.MouseMove), "hover unexpectedly reordered windows");
                f.ExpectOrder("main", "program");
                Router.Route(f.Hit(main), MuiEventType.MouseDown, 0);
                Require(Promote(MuiEventType.MouseDown), "clicking the exposed lower window did not activate it");
                f.ExpectOrder("program", "main");
                Router.Route(f.Hit(program), MuiEventType.MouseDrag, 0);
                Require(!Promote(MuiEventType.MouseDrag) && ReferenceEquals(Router.EventOwner, main.Target), "dragging crossed its original press owner");
                Router.Route(f.Hit(program), MuiEventType.MouseUp, 0);
                Router.Route(f.Hit(main, program), MuiEventType.MouseDown, 0);
                Require(ReferenceEquals(Router.EventOwner, main.Target), "subsequent overlap click still targeted the former top window");
                Router.Route(f.Hit(main, program), MuiEventType.MouseUp, 0);
            }
        }

        private static void ExplicitNonWindowLayersKeepTheirBoundaries() {
            using (var f = new Fixture()) {
                f.Add("background", 0, new VStack());
                Mounted lowA = f.Add("low-a", 100, Window("low-a"));
                f.Add("low-b", 120, Window("low-b"));
                Mounted overlay = f.Add("overlay", 200, new VStack());
                f.Add("high-a", 340, Window("high-a"));
                f.Add("high-b", 341, Window("high-b"));
                f.Add("top-overlay", 900, new VStack());
                Require(MaximumUIRuntime.Focus(lowA.Id), "lower desktop layer could not receive focus");
                f.ExpectOrder("background", "low-b", "low-a", "overlay", "high-a", "high-b", "top-overlay");
                Require(MaximumUIRuntime.Focus(overlay.Id), "non-window focus contract changed");
                f.ExpectOrder("background", "low-b", "low-a", "overlay", "high-a", "high-b", "top-overlay");
            }
        }

        private static void ModalOwnershipBlocksForeignActivation() {
            using (var f = new Fixture()) {
                Mounted main = f.Add("main", 340, Window("main"));
                Mounted program = f.Add("program", 341, Window("program"));
                main.View.Context.Dialogs.Show(new Dialog("Confirm"));
                Require(!MaximumUIRuntime.Focus(program.Id), "foreign focus stole activation from a modal");
                f.ExpectOrder("main", "program");
                MountInputTarget[] targets = f.Hit(program);
                main.Target.HasModal = true;
                Router.Route(targets, MuiEventType.MouseDown, 0);
                Require(ReferenceEquals(Router.EventOwner, main.Target) && !Promote(MuiEventType.MouseDown), "modal pointer routing was bypassed by front promotion");
                Router.Route(targets, MuiEventType.MouseUp, 0);
                main.View.Context.Dialogs.DismissAll();
                Router.Route(f.Hit(), MuiEventType.None, 0);
                Require(MaximumUIRuntime.Focus(program.Id), "closing the modal did not restore normal activation");
                main.View.Root.Add(new Dialog("Inline confirmation"));
                Require(!MaximumUIRuntime.Focus(program.Id), "an inline modal did not retain global priority");
            }
        }

        private static void HiddenAndDisabledWindowsCannotStealActivation() {
            using (var f = new Fixture()) {
                f.Add("main", 340, Window("main"));
                Mounted hidden = f.Add("hidden", 341, Window("hidden"), false);
                Mounted disabled = f.Add("disabled", 342, Window("disabled").Enabled(false));
                Require(!MaximumUIRuntime.Focus(hidden.Id) && !MaximumUIRuntime.Focus(disabled.Id), "noninteractive window stole focus");
                f.ExpectOrder("main", "hidden", "disabled");
                MaximumUIRuntime.SetVisible(hidden.Id, true);
                f.ExpectOrder("main", "disabled", "hidden");
                MaximumUIRuntime.SetVisible(hidden.Id, false);
                Require(!MaximumUIRuntime.Focus(hidden.Id), "hidden window retained explicit focus eligibility");
            }
        }

        private static void FocusBeforeFirstBuildIsAppliedAfterWindowDiscovery() {
            using (var f = new Fixture()) {
                Mounted main = f.Add("main", 340, Window("main"), true, false);
                f.Add("program", 341, Window("program"));
                Require(MaximumUIRuntime.Focus(main.Id), "newly mounted view cannot request focus before its first build");
                f.ExpectOrder("main", "program");
                main.Build(); f.CompleteBuilds();
                f.ExpectOrder("program", "main");
                Mounted nonWindow = f.Add("overlay", 900, new VStack(), true, false);
                Require(MaximumUIRuntime.Focus(nonWindow.Id), "unbuilt explicit layer cannot focus");
                nonWindow.Build(); f.CompleteBuilds();
                f.ExpectOrder("program", "main", "overlay");
            }
        }

        private static void ActivationSurvivesOtherMountsWithoutRebuildingSteadySnapshots() {
            using (var f = new Fixture()) {
                Mounted main = f.Add("main", 340, Window("main"));
                f.Add("program", 341, Window("program"));
                MaximumUIRuntime.Focus(main.Id);
                f.Add("new-window", 120, Window("new-window"));
                f.ExpectOrder("new-window", "program", "main");
                Array snapshot = Snapshot();
                for (int i = 0; i < 1000; i++) Require(MaximumUIRuntime.Focus(main.Id), "retained focus request failed");
                Require(ReferenceEquals(snapshot, Snapshot()), "focusing the already active window repeatedly rebuilt mount/input snapshots");
            }
        }
    }
}
