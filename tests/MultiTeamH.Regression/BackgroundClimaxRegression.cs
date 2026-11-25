using System;
using MultiTeamH;

internal static class BackgroundClimaxRegression {
    internal static void Run(Action<string, Action> run) {
        run("Background service climax uses native click and preserves gauge", ServiceRequest);
        run("Background service climax respects available finish actions", ServiceFinishAvailability);
        run("Background insertion climax supports vaginal and anal loops", InsertionRequests);
        run("Automatic climax is disabled for focus, locks and pending actions", RequestGuards);
        run("Automatic climax rejects unsupported modes and transitions", ModeAndStateGuards);
        run("Automatic climax waits for a finite full gauge", GaugeGuards);
        run("Automatic climax waits for the actual Animator to enter its loop", ActualAnimationGuards);
    }

    static HFlag Ready(HFlag.EMode mode = HFlag.EMode.houshi, string state = "OLoop") {
        return new HFlag { mode = mode, nowAnimStateName = state, gaugeMale = 100f,
            nowAnimationInfo = new AnimationInfo { numCtrl = 1 } };
    }
    static bool Request(HFlag flags, bool enabled = true, bool background = true,
        BackgroundClimaxFinish finish = BackgroundClimaxFinish.Outside, bool outsideOnly = false) {
        return BackgroundClimax.TryRequest(flags,
            new UnityEngine.AnimatorStateInfo { StateName = flags == null ? null : flags.nowAnimStateName },
            false, enabled, background, finish, outsideOnly);
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void ServiceRequest() {
        var flags = Ready();
        Assert(Request(flags) && flags.click == HFlag.ClickKind.outside && flags.voiceWait,
            "service finish did not submit the native outside click");
        Assert(flags.gaugeMale == 100f && flags.finish == HFlag.FinishKind.none && flags.nowAnimStateName == "OLoop",
            "auto request must not bypass the game's gauge/animation/finish flow");
        Assert(!Request(flags), "the pending native request must not be repeated");
    }
    static void ServiceFinishAvailability() {
        var flags = Ready();
        Assert(Request(flags, finish: BackgroundClimaxFinish.Inside) && flags.click == HFlag.ClickKind.inside,
            "available inside finish not selected");
        flags = Ready(); flags.nowAnimationInfo.numCtrl = 0;
        Assert(Request(flags, finish: BackgroundClimaxFinish.Inside) && flags.click == HFlag.ClickKind.outside,
            "unsupported inside finish must use the available native outside action");
        flags = Ready();
        Assert(Request(flags, finish: BackgroundClimaxFinish.Inside, outsideOnly: true) && flags.click == HFlag.ClickKind.outside,
            "native outside-only selection was ignored");
    }
    static void InsertionRequests() {
        foreach (string state in new[] { "WLoop", "SLoop", "A_WLoop", "A_SLoop" }) {
            var flags = Ready(HFlag.EMode.sonyu, state);
            Assert(Request(flags, finish: BackgroundClimaxFinish.Inside) && flags.click == HFlag.ClickKind.inside,
                "native insertion loop not supported: " + state);
            flags = Ready(HFlag.EMode.sonyu, state);
            Assert(Request(flags) && flags.click == HFlag.ClickKind.outside, "outside selection not respected: " + state);
        }
    }
    static void RequestGuards() {
        Assert(!Request(Ready(), enabled: false), "disabled feature wrote an action");
        Assert(!Request(Ready(), background: false), "focused team was modified");
        foreach (Action<HFlag> block in new Action<HFlag>[] {
            f => f.lockGugeMale = true,
            f => f.voiceWait = true,
            f => f.isHSceneEnd = true,
            f => f.click = HFlag.ClickKind.actionChange,
            f => f.finish = HFlag.FinishKind.inside,
            f => f.selectAnimationListInfo = new AnimationInfo(),
            f => f.nowAnimationInfo = null
        }) {
            var flags = Ready(); block(flags);
            var click = flags.click; var finish = flags.finish;
            Assert(!Request(flags) && flags.click == click && flags.finish == finish, "request overwrote protected state");
        }
        Assert(!Request(null), "missing team was accepted");
    }
    static void ModeAndStateGuards() {
        foreach (HFlag.EMode mode in new[] { HFlag.EMode.none, HFlag.EMode.aibu, HFlag.EMode.masturbation,
            HFlag.EMode.lesbian, HFlag.EMode.peeping, HFlag.EMode.houshi3P, HFlag.EMode.sonyu3P })
            Assert(!Request(Ready(mode)), "unsupported mode accepted: " + mode);
        foreach (string state in new[] { null, "", "Idle", "WLoop", "SLoop", "IN_Start", "Oral_Idle", "OUT_A" })
            Assert(!Request(Ready(HFlag.EMode.houshi, state)), "service transition accepted: " + state);
        foreach (string state in new[] { null, "", "Idle", "InsertIdle", "OLoop", "A_OLoop", "IN_Start", "OUT_A" })
            Assert(!Request(Ready(HFlag.EMode.sonyu, state)), "insertion transition accepted: " + state);
    }
    static void GaugeGuards() {
        foreach (float gauge in new[] { 0f, 70f, 99.999f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) {
            var flags = Ready(); flags.gaugeMale = gauge;
            Assert(!Request(flags) && flags.click == HFlag.ClickKind.none, "invalid/unfilled gauge triggered finish: " + gauge);
        }
    }
    static void ActualAnimationGuards() {
        var flags = Ready();
        Assert(!BackgroundClimax.TryRequest(flags, new UnityEngine.AnimatorStateInfo { StateName = "WLoop" },
            false, true, true, BackgroundClimaxFinish.Outside, false),
            "logical SetPlay state was treated as an already running native loop");
        Assert(!flags.voiceWait && flags.click == HFlag.ClickKind.none, "early request stranded a pending voice wait");
        Assert(!BackgroundClimax.TryRequest(flags, new UnityEngine.AnimatorStateInfo { StateName = "OLoop" },
            true, true, true, BackgroundClimaxFinish.Outside, false), "Animator transition was interrupted");
    }
}
