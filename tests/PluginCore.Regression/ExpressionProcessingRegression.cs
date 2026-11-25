using System;
using UnityEngine;
using EC = ExpressionControl.ExpressionControl;

// Executes the production per-character processing loop; live face/program
// application is recorded by the explicit external-boundary host.
internal static class ExpressionProcessingRegression {
    internal static void Run(Action<string, Action> run) {
        run("Expression transition applies even after native face callback refreshed state", NativeCallbackPrecedesProc);
        run("Expression same-state action change reevaluates its program once", SameStateActionChange);
        run("Expression stable timed program still runs at its interval", StableTimer);
        run("Expression transition and expired timer do not apply twice", TransitionAndTimer);
        run("Expression processing keeps non-program locks and eye updates active", LockAndEyeProcessing);
        run("Expression selected-actor counter change schedules one program evaluation", SelectedCounterChange);
        run("Expression removed-actor actions cannot modify the current scene", RemovedActorActions);
        run("Expression program mouth policies refresh without an animation transition", MouthPolicyRefresh);
        run("Expression disabling a program immediately releases ignored mouth controls", DisabledProgramMouthPolicy);
    }

    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static EC Host(out EC.FemaleData female) {
        var host = new EC { nowMotion = "SLoop", nowHAction = "current" };
        var actor = new ChaControl();
        female = new EC.FemaleData { female = actor, expression = new EC.Expression(actor),
            expressionRealtime = new EC.Expression(actor), programValid = true, programTimer = 20f,
            state = EC.FemaleState.loopS };
        host.femaleList.Add(female);
        EC.hFrag = new HFlag { mode = HFlag.EMode.sonyu, nowAnimStateName = "SLoop",
            nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "current" } };
        EC.hsceneProc = new HSceneProc { flags = EC.hFrag };
        Time.deltaTime = 0.02f;
        return host;
    }

    static void NativeCallbackPrecedesProc() {
        EC.FemaleData female; var host = Host(out female);
        // Native SetFace has already consumed the coarse state change, but
        // UpdateHState's pending transition has not been processed by Proc.
        host.updateState = true;
        host.Proc();
        Assert(host.ProgramApplications == 1 && ReferenceEquals(host.LastProgramActor, female) && !host.updateState,
            "native callback swallowed a state-only program's transition");
        host.Proc();
        Assert(host.ProgramApplications == 1, "stable frame replayed the completed transition");
    }

    static void SameStateActionChange() {
        EC.FemaleData female; var host = Host(out female);
        EC.hFrag.nowAnimationInfo.nameAnimation = "replacement";
        host.Proc();
        Assert(host.ProgramApplications == 1 && host.Updates == 1 && host.nowHAction == "replacement",
            "new action in the same coarse state did not reevaluate its expression program");
        host.Proc();
        Assert(host.ProgramApplications == 1, "action replacement continued to retrigger after completion");
    }

    static void StableTimer() {
        EC.FemaleData female; var host = Host(out female);
        female.programTimer = 5f; female.programTimerNow = 0.01f;
        host.Proc();
        Assert(host.ProgramApplications == 1 && female.programTimerNow == 3f, "stable timed program failed to reschedule");
        host.Proc();
        Assert(host.ProgramApplications == 1 && female.programTimerNow < 3f, "stable timer was reset every frame");
    }

    static void TransitionAndTimer() {
        EC.FemaleData female; var host = Host(out female);
        host.updateState = true; female.programTimer = 5f; female.programTimerNow = -1f;
        host.Proc();
        Assert(host.ProgramApplications == 1 && female.programTimerNow > 0f, "transition and timer selected two expressions in one pass");
    }

    static void LockAndEyeProcessing() {
        EC.FemaleData female; var host = Host(out female);
        female.programValid = false; female.lockExpression = true;
        female.female.ChangeEyesPtn(3, false);
        host.Proc();
        Assert(host.ProgramApplications == 0 && female.female.GetEyesPtn() == female.expression.eyesPtn,
            "non-program processing lost manual expression locks");
        Assert(female.EyeUpdates == 1 && female.EyeSmallUpdates == 1,
            "program schedule changes skipped existing eye updates");
    }

    static void SelectedCounterChange() {
        EC.FemaleData female; var host = Host(out female);
        host.AdjustCharacterOrgasmCount(female, 1);
        Assert(host.GetCharacterOrgasmCount(female) == 1 && host.updateState,
            "manual counter change did not schedule state-only program conditions");
        host.Proc();
        Assert(host.ProgramApplications == 1 && !host.updateState, "manual counter change was not applied once");
        host.AdjustCharacterOrgasmCount(female, int.MaxValue);
        Assert(host.GetCharacterOrgasmCount(female) == 10, "counter upper limit overflowed");
        host.updateState = false; host.AdjustCharacterOrgasmCount(female, 1);
        Assert(!host.updateState, "counter already at its limit created another transition");
        host.AdjustCharacterOrgasmCount(female, int.MinValue);
        Assert(host.GetCharacterOrgasmCount(female) == 0, "counter lower limit overflowed");
    }

    static void RemovedActorActions() {
        EC.FemaleData female; var host = Host(out female); int actions = 0;
        host.RunCharacterAction(female, delegate { actions++; });
        Assert(actions == 1, "current actor's command was not executed");
        host.femaleList.Remove(female);
        host.RunCharacterAction(female, delegate { actions++; });
        host.AdjustCharacterOrgasmCount(female, 1);
        Assert(actions == 1 && host.orgazumCount == 0 && !host.updateState && host.GetCharacterOrgasmCount(female) == 0,
            "stale callback changed another actor or a departed scene");
    }

    static void MouthPolicyRefresh() {
        EC.FemaleData female; Host(out female);
        female.program.applyMouthOpen = false; female.program.ignoreMouthInFera = true;
        female.UpdateState(false, "SLoop", true);
        Assert(female.ignoreMouthOpen && female.ignoreMouthLoad && female.isFeraMode,
            "enabling program mouth options waited for the next animation transition");
        female.program.applyMouthOpen = true; female.program.ignoreMouthInFera = false;
        female.UpdateState(false, "SLoop", false);
        Assert(!female.ignoreMouthOpen && !female.ignoreMouthLoad && !female.isFeraMode,
            "changing program left the previous program's mouth suppression active");
    }

    static void DisabledProgramMouthPolicy() {
        EC.FemaleData female; Host(out female);
        female.program.applyMouthOpen = false; female.program.ignoreMouthInFera = true;
        female.UpdateState(true, "SLoop", true);
        female.programValid = false;
        female.UpdateState(false, "SLoop", true);
        Assert(!female.ignoreMouthOpen && !female.ignoreMouthLoad, "disabled program continued suppressing manual mouth editing");
        female.programValid = true; female.state = EC.FemaleState.afterOrgazumM;
        female.UpdateState(false, null, true);
        Assert(!female.ignoreMouthLoad, "mouth policy ignored the supported action-state boundary");
    }
}
