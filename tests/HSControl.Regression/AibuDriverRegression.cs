using System;
using HSControl;

internal static class AibuDriverRegression
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("HSControl Aibu: " + message);
    }

    internal static void RunAll()
    {
        var scene = new HSceneProc();
        var flags = new HFlag { mode = 0, nowAnimStateName = "M_Idle" };
        var driver = new HSControlAibuDriver();

        // This was the user's serious regression: simply enabling a
        // random speed mode must not fill the gauge in an idle scene.
        for (int i = 0; i < 500; i++)
            Check(!driver.Tick(scene, flags, .97f, .016f, false),
                "speed-only idle cannot drive automatic stimulation");
        Check(flags.femaleGaugeCalls == 0 && flags.gaugeFemale == 0f &&
              flags.dragStarts == 0 && scene.hand.action == 0,
            "speed mode alone never fills the gauge or steals a hand");

        // Opt-in is necessary but not sufficient: an actual usable native
        // AibuItem must be bound via the scalar actionUseItem field.
        scene.hand.useAreaItems = new TestArea[0];
        for (int i = 0; i < 100; i++)
            driver.Tick(scene, flags, .8f, .016f, true);
        Check(flags.gaugeFemale == 0f && flags.femaleGaugeCalls == 0 &&
              scene.hand.action == 0 && scene.hand.actionUseItem == -1,
            "auto-caress with no usable native area cannot invent activity");

        scene.hand.useAreaItems = new[] { new TestArea { idUse = 2, kindTouch = 3 } };
        Check(driver.Tick(scene, flags, .4f, .016f, true),
            "explicit auto-caress can start a valid native action");
        Check(driver.Running && scene.hand.action == 1 &&
              scene.hand.actionUseItem == 2 && flags.dragStarts == 1,
            "native actionUseItem ID is selected before DragStart");
        Check(flags.femaleGaugeCalls == 1 &&
              Math.Abs(flags.gaugeFemale - 2f) < .001f,
            "only the real owned automatic action invokes FemaleGaugeUp");

        scene.hand1.action = 1;
        int before = flags.femaleGaugeCalls;
        Check(!driver.Tick(scene, flags, .9f, .016f, true) &&
              !driver.Running && flags.femaleGaugeCalls == before &&
              scene.hand1.action == 1,
            "secondary hand immediately interrupts only the automated action");
        scene.hand1.action = 0;

        UnityEngine.Input.PrimaryDown = true;
        Check(!driver.Tick(scene, flags, .9f, .016f, true) &&
              flags.femaleGaugeCalls == before,
            "mouse-driven primary hand always has priority");
        UnityEngine.Input.PrimaryDown = false;

        driver.Clear(scene);
        flags.gaugeFemale = 98f;
        scene.hand.action = 0;
        Check(driver.Tick(scene, flags, .5f, .016f, true),
            "a valid auto-caress may approach the threshold");
        Check(driver.TakeCompleted(), "owned activity reaching 100 requests a native finish");
        Check(!driver.TakeCompleted(), "completion request is consumed exactly once");
        Check(!driver.Running && scene.hand.action == 0,
            "owned synthetic drag is released after a finish request");

        flags.nowAnimStateName = "Orgasm_Loop";
        Check(!driver.Tick(scene, flags, .5f, .016f, true),
            "no automatic drag in native orgasm animation");
        Console.WriteLine("HSControl Aibu safety assertions passed: " + assertions);
    }
}
