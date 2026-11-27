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
            HSControlActionRules.CanStartLoop(2, false, true) &&
            HSControlActionRules.CanStartLoop(6, false, true) &&
            HSControlActionRules.CanStartLoop(7, false, true) &&
            HSControlActionRules.CanStartLoop(8, false, true) &&
            HSControlActionRules.CanStartLoop(9, false, true),
            "native loop actions supported in 2P, 3P and MMF Houshi/Sonyu");
        Check(!HSControlActionRules.CanStartLoop(1, true, true) &&
            !HSControlActionRules.CanStartLoop(6, true, true) &&
            !HSControlActionRules.CanStartLoop(7, true, true) &&
            !HSControlActionRules.CanStartLoop(8, true, true) &&
            !HSControlActionRules.CanStartLoop(9, true, true) &&
            !HSControlActionRules.CanStartLoop(5, false, true) &&
            !HSControlActionRules.CanStartLoop(6, false, false),
            "native loop guard rejects rely, unsupported modes or missing action");
        Check(HSControlActionRules.LoopName(2, true, true) == "A_SLoop" &&
            HSControlActionRules.LoopName(7, true, false) == "A_WLoop" &&
            HSControlActionRules.LoopName(6, false, true) == "SLoop" &&
            HSControlActionRules.LoopName(8, false, true) == "SLoop" &&
            HSControlActionRules.LoopName(9, true, false) == "A_WLoop" &&
            HSControlActionRules.LoopName(1, false, false) == "WLoop" &&
            HSControlActionRules.LoopName(5, false, true) == null,
            "native loop state names match installed original two-person and 3P action families");
        Check(HSControlActionRules.CanStageGauge(1, true, false) &&
            HSControlActionRules.CanStageGauge(2, true, false) &&
            HSControlActionRules.CanStageGauge(7, true, false) &&
            HSControlActionRules.CanStageGauge(9, true, false),
            "manual gauge staging is independent of auto climax and loop state");
        Check(HSControlActionRules.CanStageGauge(0, false, true) &&
            HSControlActionRules.CanStageGauge(2, false, false) &&
            HSControlActionRules.CanStageGauge(2, true, true),
            "gauge staging never consumes a native click or requires a loop");
        Check(!HSControlActionRules.CanStageGauge(-1, true, false) &&
            !HSControlActionRules.CanStageGauge(4, true, false) &&
            !HSControlActionRules.CanStageGauge(11, true, false),
            "unsupported game modes remain excluded");
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

    private static void ReplayTests() {
        var gate = new HSControlReplayGate();
        ReplayCommand result = ReplayCommand.None;
        for (int i = 0; i < 51; i++) {
            result = gate.Step(.25f, 1, "OUT_A", false, true, true, true);
            Check(result == ReplayCommand.None, "Native Houshi replay respects 10+3 second wait");
        }
        Check(gate.Step(.25f, 1, "OUT_A", false, true, true, true) == ReplayCommand.Again,
            "Native Houshi uses the only mode which processes Again");
        for (int i = 0; i < 100; i++)
            Check(gate.Step(.25f, 1, "OUT_A", false, true, true, true) == ReplayCommand.None,
                "Houshi replay issues one command per result");
        gate.Reset();
        for (int i = 0; i < 52; i++)
            result = gate.Step(.25f, 2, "A_OUT_A", false, true, true, true);
        Check(result == ReplayCommand.Insert, "Sonyu must insert rather than send an unhandled Again");
        for (int i = 0; i < 23; i++)
            Check(gate.Step(.25f, 2, "A_OUT_A", false, true, true, true) == ReplayCommand.None,
                "Do not send Sonyu continuation while the previous animation remains active");
        Check(gate.Step(.25f, 2, "A_IN_A", false, false, true, true) == ReplayCommand.None,
            "Voice guard defers Sonyu continuation");
        Check(gate.Step(.25f, 2, "A_IN_A", false, true, true, true) == ReplayCommand.Resume,
            "Sonyu continuation uses native SpeedUp action after insertion");
        Check(gate.Step(.25f, 2, "A_IN_A", false, true, true, true) == ReplayCommand.None,
            "Sonyu continuation is not duplicated");
        Check(gate.Step(.1f, 2, "WLoop", false, true, true, true) == ReplayCommand.None,
            "New loop re-arms replay.");
        gate.Reset();
        for (int i = 0; i < 52; i++) result = gate.Step(.25f, 2, "IN_A", false, false, true, true);
        Check(result == ReplayCommand.None, "Voice busy prevents commands after timeout");
        Check(gate.Step(.25f, 2, "IN_A", false, true, true, true) == ReplayCommand.Resume,
            "Sonyu inside result directly requests native speedup");
        Check(gate.Step(.25f, 2, "IN_A", true, true, true, true) == ReplayCommand.None &&
            gate.Step(.25f, 5, "IN_A", false, true, true, true) == ReplayCommand.None,
            "Rely and unsupported game modes cannot restart");
        gate.Reset();
        for (int i = 0; i < 52; i++) result = gate.Step(.25f, 7, "OUT_A", false, true, false, true);
        Check(result == ReplayCommand.None, "Existing native click blocks auto replay");
        Check(gate.Step(.25f, 7, "OUT_A", false, true, true, true) == ReplayCommand.Insert,
            "3P Sonyu uses its native insert action");
        gate.Reset();
        for (int i = 0; i < 52; i++)
            result = gate.Step(.25f, 8, "OUT_A", false, true, true, true);
        Check(result == ReplayCommand.Again, "MMF Houshi uses native Again.");
        gate.Reset();
        for (int i = 0; i < 52; i++)
            result = gate.Step(.25f, 9, "OUT_A", false, true, true, true);
        Check(result == ReplayCommand.Insert, "MMF Sonyu uses native Insert.");
        Check(gate.Step(.25f, 9, "IN_A", true, true, true, true) == ReplayCommand.None,
            "MMF rely guard disallows forced restart.");
        gate.Reset();
        Check(gate.Step(.25f, 1, "OUT_A", false, true, true, false) == ReplayCommand.None,
            "Disabling auto replay cancels pending actions");
    }

    private static void OriginalSpeedCalibration() {
        var settings = new HSControlSettings { Mode = (int)SpeedMode.Bands, Band = 0 };
        Check(settings.TargetMinSeconds == 2f && settings.TargetMaxSeconds == 10f,
            "Random destination timing matches installed KK_HCtrl constructor.");
        var engine = new HSControlMotionEngine(2026);
        engine.Reset(.2f);
        Check(engine.Step(1f / 60f, 2, "WLoop", false, 0f, 0f, settings),
            "Native slow mode starts in an active loop.");
        Check(engine.Target >= .05f && engine.Target <= .4999f,
            "Slow band uses original 0.05–0.4999 range.");
        settings.Band = 2;
        engine.Step(1f / 60f, 2, "WLoop", false, 0f, 0f, settings);
        Check(engine.Target >= .5001f && engine.Target <= 1f,
            "Fast band uses original 0.5001–1.0 range.");
        settings.Mode = (int)SpeedMode.Range;
        settings.RangeMin = settings.RangeMax = 0f;
        engine.Step(1f / 60f, 2, "WLoop", false, 0f, 0f, settings);
        Check(Math.Abs(engine.Target - .05f) < .00001f,
            "Original engine speed floor applies to automatic ranges.");
        settings.RangeMin = .7f; settings.RangeMax = .7f;
        engine.Step(1f / 60f, 2, "WLoop", false, 0f, 0f, settings);
        Check(Math.Abs(engine.Target - .7f) < .00001f,
            "Exact fixed range remains deterministic.");
        var fast = new HSControlMotionEngine(42);
        var slow = new HSControlMotionEngine(42);
        settings.Mode = (int)SpeedMode.GaugeLink;
        fast.Reset(.1f); slow.Reset(.1f);
        for (int i = 0; i < 60; i++)
            fast.Step(1f / 60f, 2, "WLoop", false, 80f, 40f, settings);
        for (int i = 0; i < 30; i++)
            slow.Step(1f / 30f, 2, "WLoop", false, 80f, 40f, settings);
        Check(Math.Abs(fast.Current - slow.Current) < .03f,
            "Native Lerp calibration stays stable at 30 and 60 FPS.");
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
            OriginalSpeedCalibration();
            IniTests();
            ReplayTests();
            Console.WriteLine("HSControl state-machine and legacy-config assertions passed: " + assertions);
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
