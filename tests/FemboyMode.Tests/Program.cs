using System;
using FemboyMode;

internal static class Program
{
    private static int failures;

    private static void Check(bool condition, string description)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL: " + description);
    }

    private static int Main()
    {
        Check(FemboyRules.FrontInsertStartIndex(false) == 0,
            "normal initiative uses buttons 0/1");
        Check(FemboyRules.FrontInsertStartIndex(true) == 7,
            "female initiative uses buttons 7/8");
        Check(FemboyRules.HasBothFrontButtons(2, 0),
            "two buttons enough on the first page");
        Check(!FemboyRules.HasBothFrontButtons(1, 0),
            "one button cannot contain both actions");
        Check(FemboyRules.HasBothFrontButtons(9, 7),
            "nine buttons enough for female-initiative page");
        Check(!FemboyRules.HasBothFrontButtons(8, 7),
            "eight buttons cannot contain both actions");
        Check(!FemboyRules.HasBothFrontButtons(16, -1),
            "negative action index is rejected");

        Check(FemboyRules.SafeBoneScale(-2f) == 0.5f,
            "clamp small scales");
        Check(FemboyRules.SafeBoneScale(7f) == 2f,
            "clamp large scales");
        Check(FemboyRules.SafeBoneScale(1.25f) == 1.25f,
            "preserve valid scales");
        Check(FemboyRules.SafeBoneScale(float.NaN) == 1f,
            "bad card NaN uses neutral scaling");
        Check(FemboyRules.SafeBoneScale(float.PositiveInfinity) == 1f,
            "infinity uses neutral scaling");
        Check(FemboyRules.SafeBoneScale(float.NegativeInfinity) == 1f,
            "negative infinity uses neutral scaling");

        long sceneA = FemboyRules.EffectKey(123, 0);
        Check(sceneA != FemboyRules.EffectKey(123, 1),
            "separate heroines have separate effect keys");
        Check(sceneA != FemboyRules.EffectKey(124, 0),
            "separate scenes have separate effect keys");

        // Real-world RX18 rig uses indexed and case-varied male bones:
        // configured cm_j_dan100 must resolve cm_J_dan100_00.
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_j_dan100") == 0,
            "literal bone name is preferred");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan100") == 1,
            "case-insensitive bone match");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan100_00") == 2,
            "RX18 numbered bone resolves to configured base");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan100_01") == 3,
            "other indexed suffix remains eligible");
        Check(FemboyRules.BoneMatchRank("cm_J_dan100_00", "cm_J_dan100_00") == 0,
            "explicit fully qualified indexed bone wins");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan101_00") == -1,
            "neighboring bone family never matches");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan1000_00") == -1,
            "numeric prefix is not enough");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "cm_J_dan100_XX") == -1,
            "non-numeric suffix is rejected");
        Check(FemboyRules.BoneMatchRank("", "cm_J_dan100_00") == -1,
            "empty requested name is rejected");
        Check(FemboyRules.BoneMatchRank("cm_j_dan100", "") == -1,
            "empty actual name is rejected");

        Check(FemboyRules.DefaultMaleFinishBone == "cm_J_dan109_00",
            "native LiquidSiru uses the actual male finish bone");
        Check(FemboyRules.BoneMatchRank("cm_J_dan109_00",
            "cm_J_dan109_00") == 0,
            "indexed male finish bone is an exact match");

        Check(FemboyRules.IsFemaleClimaxVoice(0, 142),
            "Aibu native climax voice trigger");
        Check(FemboyRules.IsFemaleClimaxVoice(0, 143),
            "Aibu native after-climax voice trigger");
        Check(!FemboyRules.IsFemaleClimaxVoice(0, 141),
            "Aibu pre-climax voice is not suppressed");
        Check(!FemboyRules.IsFemaleClimaxVoice(0, 318),
            "Sonyu voice codes do not apply to Aibu");
        Check(!FemboyRules.IsFemaleClimaxVoice(0, 0),
            "ordinary Aibu voice remains");
        Check(FemboyRules.IsFemaleClimaxVoice(2, 318),
            "Sonyu heroine finish voice is routed");
        Check(FemboyRules.IsFemaleClimaxVoice(2, 319),
            "Sonyu simultaneous finish voice is routed");
        Check(FemboyRules.IsFemaleClimaxVoice(2, 325),
            "Sonyu W finish voice is routed");
        Check(FemboyRules.IsFemaleClimaxVoice(2, 330),
            "Sonyu S finish ending voice is routed");
        Check(!FemboyRules.IsFemaleClimaxVoice(2, 320),
            "male outside/inside cue 320 is untouched");
        Check(!FemboyRules.IsFemaleClimaxVoice(2, 324),
            "male outside/inside cue 324 is untouched");
        Check(!FemboyRules.IsFemaleClimaxVoice(2, 331),
            "post-finish dialogue is untouched");
        Check(!FemboyRules.IsFemaleClimaxVoice(2, 142),
            "Aibu voice codes do not apply to Sonyu");
        Check(!FemboyRules.IsFemaleClimaxVoice(1, 142),
            "Houshi voice never suppressed");

        Check(FemboyRules.IsFemaleFinishAnimation(2, 3, "WF_IN_Start"),
            "Sonyu heroine orgasm W is eligible");
        Check(FemboyRules.IsFemaleFinishAnimation(2, 6, "A_SS_IN_Start"),
            "Sonyu simultaneous S is eligible");
        Check(FemboyRules.IsFemaleFinishAnimation(2, 4, "A_OLoop"),
            "anal prefixed finish loop is eligible");
        Check(FemboyRules.IsFemaleFinishAnimation(2, 5, "A_SF_IN_Start"),
            "anal prefixed solo finish is eligible");
        Check(!FemboyRules.IsFemaleFinishAnimation(2, 1, "WF_IN_Start"),
            "actual male inside-only climax never retargets heroine");
        Check(!FemboyRules.IsFemaleFinishAnimation(0, 3, "WF_IN_Start"),
            "Aibu has no equivalent male controller");
        Check(!FemboyRules.IsFemaleFinishAnimation(2, 3, null),
            "null state never triggers a controller swap");
        Check(!FemboyRules.IsFemaleFinishAnimation(2, 3, "SLoop"),
            "ordinary loop remains in native female controller");
        Check(!FemboyRules.IsFemaleFinishAnimation(2, 3, "M_IN_Start"),
            "male-only climax does not retarget female actor");

        Check(FemboyRules.MaleSegmentBoneName(100) == "cm_J_dan100_00",
            "native segment bone 100 name");
        Check(FemboyRules.MaleSegmentBoneName(109) == "cm_J_dan109_00",
            "native segment bone 109 name");
        Check(FemboyRules.MaleSegmentBoneName(99) == null,
            "bone copy rejects unrelated 99");
        Check(FemboyRules.MaleSegmentBoneName(110) == null,
            "bone copy rejects unrelated 110");

        Check(FemboyRules.IsPhallusToyLayer(25, 7, 8),
            "native aibulayer_02 final row is p_item_vibe fake phallus to remove");
        Check(!FemboyRules.IsPhallusToyLayer(12, 5, 6),
            "native denma vibrator ID5/6 must remain untouched");
        Check(!FemboyRules.IsPhallusToyLayer(11, 4, -1),
            "native tongue item must remain untouched");
        Check(!FemboyRules.IsPhallusToyLayer(25, 7, 6),
            "partial ID match is never mistaken for the fake phallus");

        Check(FemboyRules.SafeShaftFraction(-1f) == 0f,
            "negative shaft interpolation clamps to root");
        Check(FemboyRules.SafeShaftFraction(0f) == 0f,
            "zero fraction aims at root");
        Check(FemboyRules.SafeShaftFraction(0.5f) == 0.5f,
            "half fraction aims at scaled shaft midpoint");
        Check(FemboyRules.SafeShaftFraction(1f) == 1f,
            "fraction one aims at tip");
        Check(FemboyRules.SafeShaftFraction(4f) == 1f,
            "oversized fraction clamps at tip");
        Check(FemboyRules.SafeShaftFraction(float.NaN) == 0.5f,
            "malformed shaft fraction uses safe midpoint");
        Check(FemboyRules.SafeShaftFraction(float.PositiveInfinity) == 0.5f,
            "infinite shaft fraction uses safe midpoint");

        Check(!FemboyRules.CanSwapFullController(false, false),
            "native controller remains when both toggles off");
        Check(!FemboyRules.CanSwapFullController(true, false),
            "old existing true setting cannot accidentally reenable truncation");
        Check(!FemboyRules.CanSwapFullController(false, true),
            "experimental opt-in alone cannot swap controller");
        Check(FemboyRules.CanSwapFullController(true, true),
            "both explicit toggles are necessary for experimental swap");

        Check(!FemboyRules.IsPhallusToyLayer(25, 5, 6),
            "vibrator object pair cannot be removed even with phallus icon");
        Check(!FemboyRules.IsPhallusToyLayer(12, 7, 8),
            "wrong icon cannot identify phallus toy");
        Check(!FemboyRules.IsPhallusToyLayer(25, 8, 7),
            "left/right IDs are not interchangeable");
        Check(!FemboyRules.IsPhallusToyLayer(25, -1, -1),
            "missing entries cannot be falsely identified as toy");

        // Physical fluid emitted drop count (first shot + intervals).
        Check(FemboyRules.FluidIntervalCount(9, 1f) == 9,
            "amount 1.0 preserves native ten-shot preset");
        Check(FemboyRules.FluidIntervalCount(9, 2f) == 19,
            "amount 2.0 doubles native projectile count");
        Check(FemboyRules.FluidIntervalCount(9, 0.5f) == 4,
            "amount 0.5 halves physical droplet count");
        Check(FemboyRules.FluidIntervalCount(0, 1f) == 1,
            "empty native preset still emits two droplets");
        Check(FemboyRules.FluidIntervalCount(9, 50f) == 49,
            "amount is capped at 5x for ten original droplets");
        Check(FemboyRules.FluidIntervalCount(30, 5f) == 79,
            "large presets are capped to at most 80 droplets");
        Check(FemboyRules.FluidIntervalCount(9, float.NaN) == 9,
            "malformed amount returns to 1.0");
        Check(FemboyRules.IsFemaleInsertionFinishStart("A_WS_IN_Start"),
            "anal simultaneous climax start triggers physical liquid");
        Check(FemboyRules.IsFemaleInsertionFinishStart("SF_IN_Start"),
            "female-only insertion climax triggers liquid");
        Check(FemboyRules.IsFemaleInsertionFinishStart("WF_IN_Start"),
            "normal insertion climax start triggers liquid");
        Check(FemboyRules.IsFemaleInsertionFinishStart("SS_IN_Start"),
            "combined insertion climax start triggers liquid");
        Check(!FemboyRules.IsFemaleInsertionFinishStart("A_WS_IN_A"),
            "post-climax recovery state does not retrigger liquid");
        Check(!FemboyRules.IsFemaleInsertionFinishStart("A_M_OUT_Start"),
            "male-only climax does not trigger heroine liquid");

        Check(FemboyRules.SafeLocalFluidPitch(0f) == 0f,
            "default fluid follows actual bone forward with no fixed world tilt");
        Check(FemboyRules.SafeLocalFluidPitch(12.5f) == 12.5f,
            "relative pitch correction is preserved");
        Check(FemboyRules.SafeLocalFluidPitch(-40f) == -30f,
            "extreme downward bone-local correction is limited");
        Check(FemboyRules.SafeLocalFluidPitch(45f) == 30f,
            "extreme upward bone-local correction is limited");
        Check(FemboyRules.SafeLocalFluidPitch(float.NaN) == 0f,
            "malformed pitch never produces an invalid quaternion");
        Check(FemboyRules.SafeLocalFluidPitch(float.PositiveInfinity) == 0f,
            "infinite pitch returns to model-forward direction");

        Check(!FemboyRules.NeedsNewFluidPreset(0, 9, 0, 9),
            "same Aibu scene and pose reuses a physical fluid source");
        Check(FemboyRules.NeedsNewFluidPreset(0, 9, 2, 9),
            "switching Aibu to Sonyu rebuilds the native Metaball source");
        Check(FemboyRules.NeedsNewFluidPreset(2, 9, 2, 14),
            "switching Sonyu poses rebuilds the native Metaball source");
        Check(!FemboyRules.NeedsNewFluidPreset(2, 14, 2, 14),
            "same Sonyu pose keeps active emission without restarting");

        Check(FemboyRules.DefaultFluidLaunchSpeed < 1.0f &&
              FemboyRules.DefaultFluidLaunchSpeed > 0.2f,
            "bone-relative default launch is much shorter than old 2.5");
        Check(FemboyRules.DefaultFluidLocalPitch == 0f,
            "default launch follows shaft direction without world-up tilt");
        Check(FemboyRules.IsSonyuClimaxStartVoice(325),
            "Sonyu W climax voice starts model-local emission");
        Check(FemboyRules.IsSonyuClimaxStartVoice(326),
            "observed RX18 Sonyu S climax code 326 starts emission");
        Check(FemboyRules.IsSonyuClimaxStartVoice(330),
            "last native Sonyu climax voice is covered");
        Check(FemboyRules.IsSonyuClimaxStartVoice(363),
            "female-initiative start code +38 is covered");
        Check(FemboyRules.IsSonyuClimaxStartVoice(368),
            "female-initiative last code +38 is covered");
        Check(!FemboyRules.IsSonyuClimaxStartVoice(318),
            "post-climax voice does not duplicate initial fluid");
        Check(!FemboyRules.IsSonyuClimaxStartVoice(320),
            "actual male-only fluid voice is never redirected");
        Check(!FemboyRules.IsSonyuClimaxStartVoice(362),
            "nonclimax female-initiative code is not redirected");

        Check(FemboyRules.ShouldEmitFromConsumedSonyuVoice(326, -1),
            "RX18 voice 326 launches only after the native voice event is consumed");
        Check(FemboyRules.ShouldEmitFromConsumedSonyuVoice(363, -1),
            "female-initiative voice launches after consumption");
        Check(!FemboyRules.ShouldEmitFromConsumedSonyuVoice(326, 326),
            "deferred voice 326 does not emit repeatedly every Update frame");
        Check(!FemboyRules.ShouldEmitFromConsumedSonyuVoice(318, -1),
            "post-climax dialogue is not treated as a new fluid event");
        Check(!FemboyRules.ShouldEmitFromConsumedSonyuVoice(-1, -1),
            "no queued voice means no phantom emission");

        Check(FemboyRules.IsSonyuFinishPhase("A_WF_IN_Start"),
            "start of insertion climax is a finish phase");
        Check(FemboyRules.IsSonyuFinishPhase("A_WS_IN_A"),
            "post-climax recovery is still part of the same finish cycle");
        Check(!FemboyRules.IsSonyuOrdinaryMotionLoop("A_WS_IN_A"),
            "recovery A cannot unlock a duplicate discharge");
        Check(FemboyRules.IsSonyuOrdinaryMotionLoop("S_M_IN_Loop"),
            "native insertion loop is a new-cycle rearm point");
        Check(FemboyRules.IsSonyuOrdinaryMotionLoop("S_A_M_IN_Loop"),
            "female-initiative insertion loop is a rearm point");
        Check(!FemboyRules.IsSonyuOrdinaryMotionLoop("S_WS_IN_Loop"),
            "finish loop does not accidentally rearm");
        var cycle = new FemboySonyuCycleGate();
        Check(cycle.CanFire(12, "S_M_IN_Loop"),
            "first climax in the selected pose is allowed");
        cycle.MarkFired(12, "S_M_IN_Loop");
        cycle.Observe(12, "A_WF_IN_Start");
        Check(!cycle.CanFire(12, "A_WF_IN_Start"),
            "HFlag/SetPlay/VoiceProc all refer to one first climax");
        Check(!cycle.CanFire(12, "A_WS_IN_A"),
            "second recovery voice code cannot fire another 20 droplets");
        cycle.Observe(12, "S_M_IN_Loop");
        Check(cycle.CanFire(12, "S_M_IN_Loop"),
            "returning to ordinary insertion rearms the next climax");
        cycle.MarkFired(12, "A_SF_IN_Start");
        Check(!cycle.CanFire(12, "SS_IN_A"),
            "second cycle recovery remains suppressed");
        Check(cycle.CanFire(10, "SF_IN_Start"),
            "a genuine pose change starts independent fluid state");
        var directRestart = new FemboySonyuCycleGate();
        directRestart.MarkFired(12, "A_SF_IN_Start");
        directRestart.Observe(12, "A_SS_IN_A");
        Check(FemboyRules.IsSonyuFinishRecovery("A_SS_IN_A"),
            "RX18 observed A_SS_IN_A is a native recovery state");
        Check(!directRestart.CanFire(12, "A_SS_IN_A"),
            "recovery voice 329 cannot trigger a duplicate emission");
        Check(directRestart.CanFire(12, "A_SF_IN_Start"),
            "RX18 next climax Start rearms directly after recovery A");
        directRestart.MarkFired(12, "A_SF_IN_Start");
        Check(!directRestart.CanFire(12, "A_SF_IN_Start"),
            "multiple native events for second climax Start stay deduplicated");
        Check(!FemboyRules.IsSonyuFinishRecovery("A_SF_IN_Start"),
            "climax Start is not mistaken for completed recovery");

        Check(FemboyRules.IsReversedHoushiName("[Reversed] Test"),
            "injected role-swapped service entry is detectable");
        Check(FemboyRules.IsReversedHoushiName("[反转] 测试动作"),
            "a localized native row still has a reversible action identity");
        Check(!FemboyRules.IsReversedHoushiName("Test"),
            "ordinary donor action remains untouched");
        Check(FemboyRules.IsHoushiBodyMotionClip("M_IN_Loop"),
            "standard native Houshi motion can be overridden with paired clip");
        Check(FemboyRules.IsHoushiBodyMotionClip("SM_OUT_A"),
            "kPlug-compatible SM action motion can be overridden");
        Check(FemboyRules.IsHoushiBodyMotionClip("ML_OUT_Start"),
            "kPlug-compatible ML action start is recognized");
        Check(FemboyRules.IsHoushiBodyMotionClip("S_IN_Start"),
            "short native Houshi insertion motion is recognized");
        Check(!FemboyRules.IsHoushiBodyMotionClip("A_SF_IN_Start"),
            "Sonyu climax state must not enter Houshi clip overrides");
        Check(!FemboyRules.IsHoushiBodyMotionClip("Orgasm_Loop"),
            "female facial climax animations are left native");
        Check(!FemboyRules.IsHoushiBodyMotionClip("cf_j_head"),
            "shared body bone names are not treated as animation clips");
        Check(!FemboyRules.IsHoushiBodyMotionClip("M_Kiss_A"),
            "unrelated face and kiss states are not overridden");

        Check(FemboyRules.IsSonyuFluidEmissionPhase("A_SS_IN_Start"),
            "combined climax Start can emit without another recovery phase");
        Check(FemboyRules.IsSonyuFluidEmissionPhase("A_SS_IN_Loop"),
            "a deferred combined climax voice may arrive in its Loop");
        Check(!FemboyRules.IsSonyuFluidEmissionPhase("A_SS_IN_A"),
            "recovery climax-coded voice never spawns a second discharge");
        Check(FemboyRules.IsFemaleInsertionFinishStart("M_A_SS_IN_Start"),
            "speed-variant female initiative state is recognized");
        var combined = new FemboySonyuCycleGate();
        combined.NoteNativeOrgasm(15, "A_WF_IN_Start");
        Check(combined.CanFire(15, "A_WF_IN_Start", 20f),
            "first registered native orgasm can emit");
        combined.MarkFired(15, "A_WF_IN_Start");
        Check(!combined.CanFire(15, "A_WS_IN_Loop", 0.5f),
            "one native orgasm cannot be emitted twice in its loops");
        combined.NoteNativeOrgasm(15, "A_SS_IN_Start");
        Check(!combined.CanFire(15, "A_SS_IN_Start", 0.2f),
            "counter changes during same finish cannot cause immediate duplicate");
        Check(combined.CanFire(15, "A_SS_IN_Start", 3f),
            "a subsequent combined climax registered by HFlag re-arms even with no recovery");
        combined.MarkFired(15, "A_SS_IN_Start");
        Check(!combined.CanFire(15, "A_SS_IN_Start", 8f),
            "same start code repeated without a new native counter stays deduplicated");

        Check(FemboyRules.IsSonyuCombinedClimaxVoice(318),
            "native combined finish voice 318 is recognized");
        Check(FemboyRules.IsSonyuCombinedClimaxVoice(319),
            "native combined finish voice 319 is recognized");
        Check(FemboyRules.IsSonyuCombinedClimaxVoice(356),
            "female-initiative combined finish is recognized");
        Check(!FemboyRules.IsSonyuCombinedClimaxVoice(325),
            "ordinary climax voice remains on its own path");
        Check(FemboyRules.ShouldEmitFromConsumedCombinedVoice(318, -1),
            "consumed combined cue is eligible for the sameW/sameS callback");
        Check(!FemboyRules.ShouldEmitFromConsumedCombinedVoice(318, 318),
            "pending combined voice cannot emit twice");
        Check(!FemboyRules.IsSonyuFluidEmissionPhase("A_WS_IN_A"),
            "recovery cue remains ineligible even with a combined voice code");

        Check(FemboyRules.IsSonyuPreCombinedFinish("A_OLoop"),
            "HCtrl pre-simultaneous-climax A_OLoop is an explicit phase boundary");
        Check(!FemboyRules.IsSonyuFluidEmissionPhase("A_OLoop"),
            "HCtrl pre-climax voice alone never causes an early discharge");
        var hctrl = new FemboySonyuCycleGate();
        hctrl.MarkFired(15, "A_SF_IN_Start");
        hctrl.Observe(15, "A_OLoop");
        Check(hctrl.CanFire(15, "A_SS_IN_Start", 0.1f),
            "HCtrl pre -> simultaneous climax rearms with no new native count");
        hctrl.MarkFired(15, "A_SS_IN_Start");
        Check(!hctrl.CanFire(15, "A_SS_IN_Start", 1.2f),
            "native voice callbacks in the same simultaneous climax do not rearm");
        Check(!FemboyRules.IsSonyuPreCombinedFinish("A_WS_IN_A"),
            "recovery A does not masquerade as HCtrl pre-climax");

        Check(FemboyRules.SharedHoushiJointKey("cf_j_spine01", false) == "spine01",
            "female body joint is mapped by canonical stem");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_spine01", true) == "spine01",
            "male body rig uses the same cf_j_spine01 joint as the heroine");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_head", false) == "head",
            "head orientation can move while facial expression bones stay native");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_eye01", false) == null,
            "face and expression joints are not rewritten");
        Check(FemboyRules.SharedHoushiJointKey("cm_J_dan109_00", true) == null,
            "genital add-on joints are not exchanged");
        Check(FemboyRules.SharedHoushiJointKey("cf_d_bust00", false) == null,
            "body shape bones and physics controls are untouched");
        Check(FemboyRules.SharedHoushiJointKey("cm_j_hand_L", false) == null,
            "cm_j is not the female body namespace");
        Check(FemboyRules.SharedHoushiJointKey("cm_j_hand_L", true) == null,
            "cm_j is not the male body namespace either");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_hips", true) == "hips",
            "male hips are available under the shared human skeleton");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_waist01", true) == "waist01",
            "male lower torso is mapped under cf_j_waist");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_waist02", false) == "waist02",
            "female lower torso is mapped under cf_j_waist");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_arm00_L", true) == "arm00_l",
            "male arm action moves the shared body arm joint");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_thigh00_R", false) == "thigh00_r",
            "female leg action moves the shared body thigh joint");
        Check(FemboyRules.SharedHoushiJointKey("cf_j_shoulder_L", true) == "shoulder_l",
            "male shoulder also belongs to the shared cf_j rig");
        Check(FemboyRules.SharedHoushiJointKey("cf_J_Eye01_s_L", true) == null,
            "facial expression bones stay excluded");
        Check(FemboyRules.SharedHoushiJointKey("cf_d_shoulder_L", false) == null,
            "clothing/shape helpers are not animated during role transfer");
        Check(FemboyRules.IsHoushiRootJoint("hips") &&
              !FemboyRules.IsHoushiRootJoint("waist01"),
            "only pelvis/hips drives actor visual-anchor alignment");

        Check(FemboyRules.NativeHoushiActionKey(12, 1, "A") ==
              FemboyRules.NativeHoushiActionKey(12, 1, "A"),
            "same native donor copied by HSprite cannot create duplicate menu rows");
        Check(FemboyRules.NativeHoushiActionKey(12, 1, "A") !=
              FemboyRules.NativeHoushiActionKey(13, 1, "A"),
            "different native donor ids remain distinct");
        Check(FemboyRules.ImportedHoushiActionKey("pkg", 12, "Service", "female/a",
              "khh_f_60", "male/a", "khh_m_60") ==
              FemboyRules.ImportedHoushiActionKey("pkg", 12, "Service", "female/a",
              "khh_f_60", "male/a", "khh_m_60"),
            "copied kPlug AnimationLoader entry is stable by asset identity");
        Check(FemboyRules.ImportedHoushiActionKey("pkg", 12, "Service", "female/a",
              "khh_f_60", "male/a", "khh_m_60") !=
              FemboyRules.ImportedHoushiActionKey("pkg", 13, "Service", "female/a",
              "khh_f_60", "male/a", "khh_m_60"),
            "imported StudioId differentiates actions reusing the same controller bundle");
        Check(FemboyRules.ImportedHoushiActionKey("pkg", 12, "Service", "female/a",
              "khh_f_60", "male/a", "khh_m_60") !=
              FemboyRules.ImportedHoushiActionKey("pkg", 12, "Service", "female/b",
              "khh_f_60", "male/a", "khh_m_60"),
            "kPlug asset path differences cannot be collapsed with vanilla donors");

        Check(FemboyRules.UseNativeUnderwearContact(false, 0),
            "worn underwear (state 0) retains native cloth contact");
        Check(!FemboyRules.UseNativeUnderwearContact(false, 1),
            "shifted underwear (state 1) rebinds the live male Kokan contact");
        Check(!FemboyRules.UseNativeUnderwearContact(false, 2),
            "hung underwear (state 2) rebinds the male Kokan contact");
        Check(!FemboyRules.UseNativeUnderwearContact(false, 3),
            "removed underwear (state 3) rebinds the male Kokan contact");
        Check(!FemboyRules.UseNativeUnderwearContact(true, 0),
            "no underwear object at all does not block male Kokan contact");
        Check(FemboyRules.IsReversedHoushiName("[反转] 单手口交"),
            "oral reversal uses the marked native Houshi action");

        if (failures > 0)
        {
            Console.Error.WriteLine(failures + " headless rules tests failed.");
            return 1;
        }

        Console.WriteLine("All 188 FemboyMode rules assertions passed.");
        return 0;
    }
}
