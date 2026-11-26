using System;
using System.IO;
using HSControl;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string label) {
        assertions++;
        if (!value) throw new InvalidOperationException("HSControl regression: " + label);
    }

    private static void EngineTests() {
        Check(HSControlActionRules.CanStartLoop(1, false, true) &&
            HSControlActionRules.CanStartLoop(2, false, true), "native loop actions supported in Houshi/Sonyu");
        Check(!HSControlActionRules.CanStartLoop(1, true, true) &&
            !HSControlActionRules.CanStartLoop(7, false, true) &&
            !HSControlActionRules.CanStartLoop(1, false, false), "native loop guard rejects rely, unsupported modes or missing action");
        Check(HSControlActionRules.LoopName(2, true, true) == "A_SLoop" &&
            HSControlActionRules.LoopName(1, false, false) == "WLoop", "native loop state name uses the original action family");
        Check(HSControlActionRules.IsLoop("A_WLoop") &&
            !HSControlActionRules.IsLoop("Stop_Idle"), "only loop animations accept gated finish actions");
        Check(HSControlMotionEngine.CanDrive(0, "M_Idle", false), "native Aibu idle is supported");
        Check(HSControlMotionEngine.CanDrive(1, "WLoop", false), "Houshi loop supported");
        Check(!HSControlMotionEngine.CanDrive(1, "Stop_Idle", false), "no edits during transitions");
        Check(!HSControlMotionEngine.CanDrive(1, "WLoop", true), "native rely guard");
        Check(!HSControlMotionEngine.CanDrive(3, "WLoop", false), "unsupported scene modes remain untouched");
        Check(Math.Abs(HSControlMotionEngine.LinkedTarget(0, 0, 40, 50) - .5f) < .001f, "Aibu female gauge");
        Check(Math.Abs(HSControlMotionEngine.LinkedTarget(1, 0, 70, 5) - 1f) < .001f, "Houshi male gauge");
        Check(Math.Abs(HSControlMotionEngine.LinkedTarget(2, 0, 80, 40) - .6f) < .001f, "Sonyu mean gauge");
        Check(Math.Abs(HSControlMotionEngine.LinkedTarget(2, 1, 80, 40) - .8f) < .001f, "Sonyu higher gauge");

        HSControlMotionEngine engine = new HSControlMotionEngine(42);
        HSControlSettings settings = new HSControlSettings { Mode = 2, RangeMin = .25f, RangeMax = .50f };
        engine.Reset(.2f);
        for (int i = 0; i < 1000; i++) {
            Check(engine.Step(.03f, 2, "WLoop", false, 40, 70, settings), "valid random range drives");
            Check(engine.Current >= 0f && engine.Current <= 1f, "speed always clamped");
        }
        Check(!engine.Step(.03f, 3, "WLoop", false, 60, 60, settings), "unsupported mode has no writeback");
        Check(!engine.Driving, "disabled engine does not retain a stale write flag");
        settings.ManualOverride = true;
        settings.ManualSpeed = .4f;
        Check(engine.Step(.016f, 2, "WLoop", false, 50, 40, settings), "manual override drives");
        Check(Math.Abs(engine.Current - .4f) < .001f, "manual override exact value");
        settings.ManualSpeed = 400f;
        Check(engine.Step(.016f, 2, "WLoop", false, 50, 40, settings) &&
            Math.Abs(engine.Current - 1f) < .001f, "manual override clamped");
        settings.ManualOverride = false;
        settings.Mode = 0;
        Check(!engine.Step(.016f, 2, "WLoop", false, 50, 40, settings), "off mode never modifies native scene");

        settings.Mode = 3;
        settings.AutoStrength = true;
        engine.Reset(0f);
        Check(engine.Step(.016f, 2, "WLoop", false, 50, 40, settings), "linkage drives");
        Check(engine.StrengthToggleRequested, "initial strength timer selects native change");
        Check(!engine.Step(.016f, 2, "Stop_Idle", false, 50, 40, settings) &&
            !engine.StrengthToggleRequested, "invalid transition clears strength request");
        settings.RangeMin = float.NaN; settings.RangeMax = -8f;
        settings.StrengthMinSeconds = -100f; settings.StrengthMaxSeconds = float.PositiveInfinity;
        settings.Sanitize();
        Check(settings.RangeMin == 0f && settings.RangeMax == 0f, "bad ranges sanitized");
        Check(settings.StrengthMinSeconds >= 1f &&
            settings.StrengthMaxSeconds <= 120f, "bad timer sanitized");
    }

    private static void IniTests() {
        string path = Path.Combine(Path.GetTempPath(), "hscontrol-test-" + Guid.NewGuid().ToString("N") + ".ini");
        try {
            File.WriteAllText(path, "[Other]\nmode=99\n[Config]\n" +
                "autoSW=True\nautoSWTimerMin=10\nautoSWTimerMax=30\n" +
                "autoMaleOrga=False\nautoFemaleOrga=True\nautoReplay=True\n" +
                "autoSpeedMode=3\nautoSpeedMode_Half=2\n" +
                "autoSpeedMode_RangeMin=0.2\nautoSpeedMode_RangeMax=0.8\n" +
                "autoSpeedMode_LinkageType=1\n");
            HSControlSettings settings = HSLegacyIni.Read(path);
            Check(settings.Mode == 3 && settings.Band == 2 && settings.Linkage == 1, "actual legacy options imported");
            Check(settings.AutoStrength && settings.AutoReplay && !settings.AutoMaleFinish &&
                settings.AutoFemaleFinish, "legacy booleans imported");
            Check(settings.StrengthMinSeconds == 10f && settings.StrengthMaxSeconds == 30f,
                "legacy timer bounds");
            Check(settings.RangeMin == .2f && settings.RangeMax == .8f, "legacy numeric bounds");
            File.WriteAllText(path, "[Config]\nautoSpeedMode=999\nautoSpeedMode_RangeMin=invalid\n");
            settings = HSLegacyIni.Read(path);
            Check(settings.Mode == 3 && settings.RangeMin == 0f, "invalid legacy settings sanitized");
        } finally { if (File.Exists(path)) File.Delete(path); }
    }

    public static int Main() {
        try {
            EngineTests();
            IniTests();
            Console.WriteLine("HSControl state-machine and legacy-config assertions passed: " + assertions);
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
