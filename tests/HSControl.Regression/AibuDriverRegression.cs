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

        // Auto speed drives Aibu without a second switch, but an empty
        // native contact list must never change gauge or steal the hand.
        scene.hand.useAreaItems = new TestArea[0];
        for (int i = 0; i < 500; i++)
            Check(!driver.Tick(scene, flags, .97f, .016f),
                "no real contact means no forced gauge accumulation");
        Check(flags.femaleGaugeCalls == 0 && flags.gaugeFemale == 0f &&
              flags.dragStarts == 0 && scene.hand.action == 0,
            "idle H scene does not grow gauge or synthesize native drags");

        scene.hand.useAreaItems = new[] { new TestArea { idUse = 2, kindTouch = 3 } };
        Check(driver.Tick(scene, flags, .4f, .016f),
            "HCtrl speed mode starts one native caress pulse with a real contact");
        Check(driver.Running && scene.hand.action == 1 &&
              scene.hand.actionUseItem == 2 && flags.dragStarts == 1,
            "correct native useAreaItems idUse is bound before DragStart");
        Check(flags.femaleGaugeCalls == 1 &&
              Math.Abs(flags.gaugeFemale - 2f) < .001f,
            "gauge is raised once, only on a real owned caress");
        Check(!driver.Tick(scene, flags, .4f, .016f),
            "next update releases the previous native pulse");
        Check(!driver.Running && scene.hand.action == 0 &&
              flags.dragFinishes == 1 && scene.hand.actionUseItem == -1,
            "native action and selected item are restored immediately");
        int old = flags.femaleGaugeCalls;
        for (int i = 0; i < 25; i++)
            driver.Tick(scene, flags, .4f, .016f);
        Check(flags.femaleGaugeCalls == old,
            "no sustained multi-second input capture");

        UnityEngine.Input.PrimaryDown = true;
        for (int i = 0; i < 90; i++)
            driver.Tick(scene, flags, .5f, .016f);
        Check(scene.hand.action == 0 && flags.femaleGaugeCalls == old,
            "manual mouse input always takes priority");
        UnityEngine.Input.PrimaryDown = false;
        scene.hand1.action = 1;
        for (int i = 0; i < 90; i++)
            driver.Tick(scene, flags, .5f, .016f);
        Check(scene.hand1.action == 1 && flags.femaleGaugeCalls == old,
            "secondary hand remains usable while auto-speed is enabled");
        scene.hand1.action = 0;
        flags.nowAnimStateName = "Idle";
        Check(!driver.Tick(scene, flags, .5f, .016f),
            "unsupported plain Idle never triggers automatic caress");
        driver.Clear(scene);
        Console.WriteLine("HSControl one-frame Aibu assertions passed: " + assertions);
    }
}
