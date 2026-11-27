using System;
using HSControl;

internal static class AibuDriverRegression
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Native Aibu driver: " + message);
    }

    internal static void RunAll()
    {
        var scene = new HSceneProc();
        var flags = new HFlag { mode = 0, nowAnimStateName = "M_Idle" };
        var driver = new HSControlAibuDriver();
        Check(driver.Tick(scene, flags, .45f, .016f),
            "native Aibu starts with a usable HandCtrl contact");
        Check(driver.Running && scene.hand.action == 1 &&
              flags.dragStarts == 1, "only primary free hand owns synthetic drag");
        Check(flags.femaleGaugeCalls == 1 &&
              Math.Abs(flags.gaugeFemale - 2.25f) < .001f,
              "original FemaleGaugeUp(speed * 5 * rateDragGauge) is executed");
        for (int i = 0; i < 60; i++)
            driver.Tick(scene, flags, .45f, .016f);
        Check(flags.dragFinishes >= 1,
            "native caress completes its own timed drag");
        driver.Stop(scene);
        Check(!driver.Running, "scene-owned drag can be released");

        scene = new HSceneProc();
        flags = new HFlag { mode = 0, nowAnimStateName = "Idle" };
        driver = new HSControlAibuDriver();
        scene.hand1.action = 1;
        Check(!driver.Tick(scene, flags, .5f, .016f) &&
              scene.hand1.action == 1 && flags.dragStarts == 0 &&
              flags.femaleGaugeCalls == 0,
              "native secondary manual hand is never stolen or double-counted");
        scene.hand1.action = 0;
        scene.hand.action = 2;
        Check(!driver.Tick(scene, flags, .5f, .016f) &&
              scene.hand.action == 2, "native primary manual hand takes precedence");
        scene.hand.action = 0;
        UnityEngine.Input.PrimaryDown = true;
        Check(!driver.Tick(scene, flags, .5f, .016f) &&
              scene.hand.action == 0,
              "manual mouse press always defeats automatic caress capture");
        UnityEngine.Input.PrimaryDown = false;
        scene.hand.useAreaItems = new TestArea[0];
        Check(driver.Tick(scene, flags, .5f, .016f) &&
              flags.dragStarts == 0 && scene.hand.action == 0 &&
              flags.femaleGaugeCalls == 1,
              "original HCtrl still raises gauge in Aibu auto-speed without inventing a hand drag");
        scene.hand.useAreaItems = new[] { new TestArea() };
        Check(driver.Tick(scene, flags, .5f, .016f) &&
              driver.Running,
            "imported plain Idle can start a valid native Aibu hand drag");
        scene.hand1.action = 1;
        int before = flags.femaleGaugeCalls;
        Check(!driver.Tick(scene, flags, .5f, .016f) &&
              flags.femaleGaugeCalls == before,
              "manual second-hand input pauses automatic gauge injection");
        driver.Stop(scene);
        Check(scene.hand.action == 0 &&
              scene.hand1.action == 1 &&
              flags.dragFinishes == 0,
              "stopping automation cannot cancel a concurrent native hand");
        flags.nowAnimStateName = "Orgasm_Loop";
        Check(!driver.Tick(scene, flags, .5f, .016f),
            "automatic Aibu is disabled outside a safe idle state");
        Console.WriteLine("HSControl native Aibu driver assertions passed: " + assertions);
    }
}
