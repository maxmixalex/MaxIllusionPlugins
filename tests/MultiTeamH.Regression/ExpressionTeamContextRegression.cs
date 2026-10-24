using System;
using System.Collections;
using System.Collections.Generic;
using MultiTeamH;

internal static partial class Program {
    static void RunExpressionTeamContextTests() {
        Run("Background Proc restores focused expression counters and motion", BackgroundStateRestored);
        Run("Focus change loads and retains the newly focused expression state", ExpressionFocusChange);
        Run("Native face callback uses the target actor's flags and condition state", NativeFaceContext);
        Run("Blend callback for background actor restores context after exception", NativeBlendFailure);
        Run("Nested actor callbacks restore each caller and persist their own updates", NestedExpressionCharacters);
        Run("Same-actor nested guard does not create another scope", SameActorGuard);
        Run("Foreign face controller cannot bypass ownership through ExpressionControl", RejectForeignFaceController);
        Run("Character refresh inside a cross-team callback preserves the Proc filter", CharacterRefreshInProc);
        Run("Session teardown inside a character callback cannot resurrect cached actors", CharacterSessionTeardown);
        Run("Unmanaged and unavailable character callbacks leave the host untouched", UnknownCharacterContext);
        Run("Both members of each team run once with their own expression context", ExpressionMaleOwnership);
        Run("Direct program evaluation uses its FemaleData actor's context", DirectProgramContext);
        Run("Direct actor state refresh replaces stale focused motion and action arguments", DirectActorStateRefresh);
        Run("Recursive Proc does not replay the background team twice", NestedExpressionProc);
        Run("An older live session cannot route expression processing for a newer scene", ExpressionSceneOwnership);
        Run("Expression processing outside H does not enter a retained session", ExpressionOutsideH);
        Run("Missing background flags and session teardown do not replay the whole actor list", SkipUnavailableExpressionReplay);
        Run("Stable voice identities survive native voice slot swaps", StableExpressionVoices);
        Run("Independent teams use voice request slot zero", IndependentExpressionVoiceSlots);
        Run("Two-female owner and partner use their shared voice request slots", SharedExpressionVoiceSlots);
        Run("Incomplete voice catalogues never borrow the other actor's voice", MissingExpressionVoice);
    }

    static void SetExpressionState(ExpressionHost host, string motion, int count) {
        host.nowMotion = motion;
        host.nowHAction = motion + "-action";
        host.orgazumCount = count;
        host.isFeraAction = count == 2;
        host.updateState = count == 2;
    }

    static ExpressionHost PreparedExpressionHost(TeamSession session) {
        var host = Host(session);
        object scope = BeginExpression(host);
        SetExpressionState(host, "focused", 1);
        FinishExpression(null, host, scope);
        Set(Expressions, "forcedTeam", 1);
        scope = BeginExpression(host);
        SetExpressionState(host, "background", 2);
        FinishExpression(null, host, scope);
        Set(Expressions, "forcedTeam", -1);
        return host;
    }

    static object BeginCharacter(ExpressionHost host, ChaControl actor) {
        object[] args = { host, actor, null };
        Invoke(Expressions, "BeforeCharacterCall", args);
        return args[2];
    }
    static Exception FinishCharacter(Exception error, ExpressionHost host, object state) {
        return (Exception)Invoke(Expressions, "FinalizeCharacterCall", error, host, state);
    }

    static void BackgroundStateRestored() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        Assert(host.nowMotion == "focused" && host.nowHAction == "focused-action" && host.orgazumCount == 1 &&
            !host.isFeraAction && !host.updateState, "background Proc leaked its conditions into focused/native callbacks");
        Set(Expressions, "forcedTeam", 1);
        var scope = BeginExpression(host);
        Assert(host.nowMotion == "background" && host.orgazumCount == 2 && host.isFeraAction && host.updateState,
            "background state was discarded while restoring focus");
        FinishExpression(null, host, scope);
    }

    static void ExpressionFocusChange() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        session.ActiveTeam = 1;
        var scope = BeginExpression(host);
        Assert(host.nowMotion == "background" && host.orgazumCount == 2 && ExpressionHost.hFrag == session.Flags[1],
            "focus switch loaded the previous team's state");
        host.orgazumCount = 3;
        FinishExpression(null, host, scope);
        Assert(host.orgazumCount == 3 && Get(Expressions, "lastActiveFemale") == session.Females[1],
            "top-level cleanup reverted the user's focus switch");
    }

    static void NativeFaceContext() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var originalFlag = ExpressionHost.hFrag;
        object[] args = { host, session.Females[1], new FaceListCtrl(), false, null };
        Assert((bool)Invoke(Expressions, "BeforeSetFace", args), "valid owning controller was blocked");
        Assert(ExpressionHost.hFrag == session.Flags[1] && host.orgazumCount == 2 && host.nowMotion == "background",
            "native background face callback evaluated foreground conditions");
        host.orgazumCount = 4;
        Invoke(Expressions, "AfterCharacterCall", host, args[4]);
        Assert(ExpressionHost.hFrag == originalFlag && host.orgazumCount == 1 && host.nowMotion == "focused",
            "native callback leaked background flags or counters");
        var scope = BeginCharacter(host, session.Females[1]);
        Assert(host.orgazumCount == 4, "native callback lost the target's state update");
        FinishCharacter(null, host, scope);
    }

    static void NativeBlendFailure() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var flags = ExpressionHost.hFrag;
        var scope = BeginCharacter(host, session.Females[1]);
        var failure = new InvalidOperationException("native face blend failed");
        Assert(FinishCharacter(failure, host, scope) == failure, "original character failure was swallowed");
        Assert(ExpressionHost.hFrag == flags && host.nowMotion == "focused", "character failure leaked its context");
        host.nowMotion = "later"; ExpressionHost.hFrag = new HFlag(); var laterFlag = ExpressionHost.hFrag;
        FinishCharacter(null, host, scope);
        Assert(host.nowMotion == "later" && ExpressionHost.hFrag == laterFlag, "character cleanup ran twice");
    }

    static void NestedExpressionCharacters() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var original = ExpressionHost.hFrag;
        var outer = BeginCharacter(host, session.Females[1]);
        host.orgazumCount = 5;
        var inner = BeginCharacter(host, session.Females[0]);
        Assert(host.orgazumCount == 1 && ExpressionHost.hFrag == session.Flags[0], "nested callback missed its own actor");
        host.orgazumCount = 6;
        FinishCharacter(null, host, inner);
        Assert(host.orgazumCount == 5 && ExpressionHost.hFrag == session.Flags[1], "nested callback did not restore its caller");
        FinishCharacter(null, host, outer);
        Assert(host.orgazumCount == 6 && ExpressionHost.hFrag == original && Get(Expressions, "activeCharacterScope") == null,
            "nested callback discarded updated focus state or retained a scope");
    }

    static void SameActorGuard() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        var outer = BeginCharacter(host, session.Females[1]);
        Assert(BeginCharacter(host, session.Females[1]) == null, "same-actor nested part hook allocated a redundant scope");
        FinishCharacter(null, host, outer);
    }

    static void RejectForeignFaceController() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var flags = ExpressionHost.hFrag;
        session.RejectFaceWrite = true;
        object[] args = { host, session.Females[1], new FaceListCtrl(), false, null };
        Assert(!(bool)Invoke(Expressions, "BeforeSetFace", args) && (bool)args[3] && args[4] == null,
            "foreign controller could write through ExpressionControl before native ownership guard");
        Assert(ExpressionHost.hFrag == flags && host.orgazumCount == 1, "rejected native face call changed expression context");
    }

    static void CharacterRefreshInProc() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var original = host.femaleList;
        var proc = BeginExpression(host);
        var character = BeginCharacter(host, session.Females[1]);
        Assert(ReferenceEquals(host.femaleList, original), "cross-team callback could not find the background actor's locks");
        Invoke(Expressions, "BeforeUpdateFemale", host);
        var refreshed = new List<ExpressionData>(host.femaleList) { new ExpressionData { female = new ChaControl() } };
        host.femaleList = refreshed;
        Invoke(Expressions, "AfterUpdateFemale", host);
        Assert(ReferenceEquals(host.femaleList, refreshed), "refresh removed the cross-team callback's target midway");
        FinishCharacter(null, host, character);
        Assert(host.femaleList.Count == 3 && !host.femaleList.Contains(original[1]), "character callback leaked the other team into current Proc");
        FinishExpression(null, host, proc);
        Assert(ReferenceEquals(host.femaleList, refreshed), "final Proc restoration discarded a refreshed character list");
    }

    static void CharacterSessionTeardown() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        var proc = BeginExpression(host); var character = BeginCharacter(host, session.Females[1]);
        ExpressionControlCompat.SessionEnded(session); TeamSession.Snapshot.Clear();
        FinishCharacter(null, host, character); FinishExpression(null, host, proc);
        Assert(((IDictionary)Get(Expressions, "States")).Count == 0 && Get(Expressions, "lastActiveFemale") == null,
            "unwinding an ended session recreated retained character state");
        Assert(Get(Expressions, "activeProcScope") == null && Get(Expressions, "activeCharacterScope") == null,
            "ended session left an active scope");
    }

    static void UnknownCharacterContext() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var flags = ExpressionHost.hFrag;
        Assert(BeginCharacter(host, null) == null && BeginCharacter(host, new ChaControl()) == null,
            "unmanaged actor entered a team expression scope");
        session.Flags[1] = null;
        Assert(BeginCharacter(host, session.Females[1]) == null && ExpressionHost.hFrag == flags && host.orgazumCount == 1,
            "temporarily missing background flags replaced valid foreground state");
    }

    static void ExpressionMaleOwnership() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        var firstMale = new ExpressionData { female = session.Males[0] };
        var secondMale = new ExpressionData { female = session.Males[1] };
        host.femaleList.Add(firstMale); host.femaleList.Add(secondMale); host.femaleList.Add(secondMale);
        var scope = BeginExpression(host);
        Assert(host.femaleList.Count == 3 && host.femaleList.Contains(firstMale) && !host.femaleList.Contains(secondMale),
            "background male was processed as an unmanaged foreground actor");
        FinishExpression(null, host, scope);
        Set(Expressions, "forcedTeam", 1); scope = BeginExpression(host);
        Assert(host.femaleList.Count == 2 && host.femaleList.Contains(secondMale) && !host.femaleList.Contains(firstMale),
            "owning team replay omitted or duplicated its male actor");
        FinishExpression(null, host, scope); Set(Expressions, "forcedTeam", -1);
        var character = BeginCharacter(host, session.Males[1]);
        Assert(ExpressionHost.hFrag == session.Flags[1] && host.orgazumCount == 2, "background male guard used foreground conditions");
        FinishCharacter(null, host, character);
    }

    static void DirectProgramContext() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        object[] args = { host, host.femaleList[1], null };
        Invoke(Expressions, "BeforeFemaleDataCall", args);
        Assert(ExpressionHost.hFrag == session.Flags[1] && host.orgazumCount == 2,
            "direct program matching bypassed actor context routing");
        FinishCharacter(null, host, args[2]);
    }

    static void NestedExpressionProc() {
        var session = Fresh(); var host = PreparedExpressionHost(session);
        var outer = BeginExpression(host); var inner = BeginExpression(host);
        var property = inner.GetType().GetField("IsTopLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert(!(bool)property.GetValue(inner), "recursive Proc was treated as a second top-level replay");
        FinishExpression(null, host, inner); FinishExpression(null, host, outer);
        Assert(Get(Expressions, "activeProcScope") == null, "recursive Proc retained scope stack");
    }

    static void DirectActorStateRefresh() {
        var session = Fresh(); var host = PreparedExpressionHost(session); var flags = ExpressionHost.hFrag;
        session.Flags[1].nowAnimStateName = "OLoop";
        session.Flags[1].nowAnimationInfo.nameAnimation = "フェラ";
        var data = host.femaleList[1]; data.instMono = host;
        object[] args = { data, "focused-motion", false, null };
        Invoke(Expressions, "BeforeFemaleDataUpdate", args);
        Assert((string)args[1] == "OLoop" && (bool)args[2] && ExpressionHost.hFrag == session.Flags[1],
            "batch/UI program refresh used the focused actor's motion or mouth mode");
        Invoke(Expressions, "AfterCharacterCall", data, args[3]);
        Assert(ExpressionHost.hFrag == flags && host.orgazumCount == 1,
            "FemaleData postfix restored state onto the data object rather than its host");
    }

    static void StableExpressionVoices() {
        var first = new HVoiceCtrl.Voice(); var second = new HVoiceCtrl.Voice();
        var stable = new[] { first, second };
        var swapped = new[] { second, first };
        Assert(ReferenceEquals(ExpressionVoiceBinding.Resolve(0, stable, swapped, 1), first) &&
            ReferenceEquals(ExpressionVoiceBinding.Resolve(1, stable, swapped, 1), second),
            "changing focus swapped the actors' voice identities");
        Assert(ReferenceEquals(ExpressionVoiceBinding.Resolve(0, null, swapped, 1), first) &&
            ReferenceEquals(ExpressionVoiceBinding.Resolve(1, null, swapped, 1), second),
            "early catalogue fallback ignored the currently bound primary");
    }

    static void ExpressionSceneOwnership() {
        Fresh(); var newer = new TeamSession(); TeamSession.Snapshot.Add(newer);
        ExpressionHost.hsceneProc = newer.Proc;
        var host = Host(newer); var scope = BeginExpression(host);
        Assert(scope != null && ExpressionHost.hFrag == newer.Flags[0] && host.femaleList.Count == 2,
            "older session hijacked a new scene's expression state");
        FinishExpression(null, host, scope);
    }

    static void ExpressionOutsideH() {
        var session = Fresh(); var host = Host(session); var flags = ExpressionHost.hFrag;
        ExpressionHost.hsceneProc = null;
        Assert(BeginExpression(host) == null && ExpressionHost.hFrag == flags && host.femaleList.Count == 3,
            "scene exit applied a retained session to non-H expression processing");
    }

    static void SkipUnavailableExpressionReplay() {
        var session = Fresh(); var host = Host(session); var scope = BeginExpression(host);
        session.Flags[1] = null;
        Invoke(Expressions, "AfterProc", host, typeof(ExpressionHost).GetMethod("Replay"), scope);
        Assert(host.ReplayCalls == 0, "missing background flags replayed every actor under foreground conditions");
        scope = BeginExpression(host);
        ExpressionControlCompat.SessionEnded(session);
        Invoke(Expressions, "AfterProc", host, typeof(ExpressionHost).GetMethod("Replay"), scope);
        Assert(host.ReplayCalls == 0, "ended session replayed expression work after teardown");
    }

    static void IndependentExpressionVoiceSlots() {
        Assert(ExpressionVoiceBinding.FlagVoiceIndex(0, -1) == 0 && ExpressionVoiceBinding.FlagVoiceIndex(1, -1) == 0,
            "independent team's spurt request read a secondary voice slot");
    }
    static void SharedExpressionVoiceSlots() {
        Assert(ExpressionVoiceBinding.FlagVoiceIndex(0, 0) == 0 && ExpressionVoiceBinding.FlagVoiceIndex(1, 0) == 1 &&
            ExpressionVoiceBinding.FlagVoiceIndex(0, 1) == 1 && ExpressionVoiceBinding.FlagVoiceIndex(1, 1) == 0,
            "shared two-female voice slots followed fixed team numbers rather than action ownership");
    }
    static void MissingExpressionVoice() {
        var first = new HVoiceCtrl.Voice();
        Assert(ExpressionVoiceBinding.Resolve(1, null, new[] { first }, 0) == null &&
            ExpressionVoiceBinding.Resolve(0, null, new[] { first }, -1) == null &&
            ExpressionVoiceBinding.Resolve(-1, new[] { first }, new[] { first }, 0) == null &&
            ExpressionVoiceBinding.Resolve(2, null, null, 0) == null,
            "missing catalogue silently borrowed another actor's voice");
    }
}
