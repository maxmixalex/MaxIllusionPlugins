using System;
using HSControl;

internal static class AibuDriverRegression
{
    private static int assertions;
    private static void Check(bool condition, string message) {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("HSControl Aibu: " + message);
    }

    internal static void RunAll() {
        var scene = new HSceneProc();
        var flags = new HFlag { mode = 0, nowAnimStateName = "M_Idle" };
        var driver = new HSControlAibuDriver();

        // Idle with no native contact never injects a gauge value.
        scene.hand.useAreaItems = new TestArea[0];
        for (int i = 0; i < 500; i++)
            Check(!driver.Tick(scene, flags, .97f, .016f),
                "no real contact cannot create stimulation");
        Check(flags.femaleGaugeCalls == 0 && flags.gaugeFemale == 0f &&
              flags.dragStarts == 0 && scene.hand.action == 0,
            "idle H scene leaves gauge and native hands untouched");

        scene.hand.useAreaItems =
            new[] { new TestArea { idUse = 2, kindTouch = 3 } };
        Check(driver.Tick(scene, flags, .4f, .016f),
            "a real native contact starts an uninterrupted Aibu segment");
        Check(driver.Running && scene.hand.action == 1 &&
              scene.hand.actionUseItem == 2 && flags.dragStarts == 1,
            "bind correct native item before first drag");
        int initialCalls = flags.femaleGaugeCalls;
        flags.nowAnimStateName = "M_Loop";
        Check(HSControlActionRules.ShouldTickAutoCaress(true, false, true, 0,
                flags.nowAnimStateName) &&
              !HSControlActionRules.ShouldTickAutoCaress(true, false, false, 0,
                flags.nowAnimStateName),
            "owned native Aibu continues but no fresh drag starts during motion");
        for (int i = 0; i < 115; i++)
            Check(driver.Tick(scene, flags, .4f, .016f),
                "Aibu remains active rather than restarting its animation");
        Check(driver.Running && flags.dragStarts == 1 &&
              flags.dragFinishes == 0 && scene.hand.action == 1,
            "long caress does not repeatedly trigger DragStart/FinishDrag");
        Check(flags.femaleGaugeCalls > initialCalls &&
              flags.femaleGaugeCalls < 14 &&
              flags.gaugeFemale > 0f && flags.gaugeFemale < 25f,
            "bounded gauge fallback never fills bar in a few seconds");

        flags.nowAnimStateName = "M_Idle";
        UnityEngine.Input.PrimaryDown = true;
        int beforeManual = flags.femaleGaugeCalls;
        Check(!driver.Tick(scene, flags, .4f, .016f) &&
              !driver.Running && scene.hand.action == 0 &&
              scene.hand.actionUseItem == -1 &&
              flags.dragStarts == 1 && flags.dragFinishes == 1,
            "manual mouse press releases only the owned segment once");
        for (int i = 0; i < 80; i++)
            Check(!driver.Tick(scene, flags, .4f, .016f),
                "held manual input cannot be captured by automation");
        Check(flags.femaleGaugeCalls == beforeManual &&
              flags.dragStarts == 1,
            "manual input prevents synthetic gauge increase and restart");
        UnityEngine.Input.PrimaryDown = false;

        scene.hand1.action = 1;
        for (int i = 0; i < 80; i++)
            Check(!driver.Tick(scene, flags, .4f, .016f),
                "secondary hand has priority over auto caress");
        Check(scene.hand1.action == 1 && flags.dragStarts == 1,
            "secondary manual hand is never reset");
        scene.hand1.action = 0;

        // After manual release, acquire once and remain stable; a
        // real native scene transition ends without a restart loop.
        bool restarted = false;
        for (int i = 0; i < 120; i++)
            if (driver.Tick(scene, flags, .3f, .016f)) {
                restarted = true;
                break;
            }
        Check(restarted && flags.dragStarts == 2,
            "new stable segment may begin only after manual cooldown");
        flags.nowAnimStateName = "Orgasm_Loop";
        Check(!HSControlActionRules.ShouldTickAutoCaress(true, true, true, 0,
                flags.nowAnimStateName),
            "terminal animations cannot keep the synthetic drag");
        Check(!driver.Tick(scene, flags, .3f, .016f) &&
              !driver.Running && flags.dragFinishes == 2,
            "native action transition cleanly releases previous segment");
        driver.Clear(scene);
        Check(flags.dragFinishes == 2 &&
              !driver.TakeCompleted() &&
              scene.hand.actionUseItem == -1,
            "teardown is idempotent and resets only synthetic state");
        Console.WriteLine("HSControl stable Aibu assertions passed: " + assertions);
    }
}
