using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using MultiTeamH;

internal static class Program {
    const BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly Type Expressions = typeof(ExpressionControlCompat);
    static readonly Type Runtime = typeof(RuntimeModCompat);
    static int passed;

    static int Main() {
        try {
            Run("Expression exception restores character list and H flag", ExpressionException);
            Run("Missing character list leaves global H flag intact", MissingExpressionList);
            Run("Expression cleanup is idempotent", ExpressionCleanupOnce);
            Run("Background expression replay excludes unmanaged characters", UnmanagedCharactersOnce);
            Run("Expression state removed when session ends", ExpressionSessionCleanup);
            Run("HCtrl partial prefix failure restores previous team", PartialHCtrlPrefixFailure);
            Run("HCtrl exception restores UI, cursor, context and saved state", HCtrlException);
            Run("HCtrl UI cleanup failure still restores scene context", UiCleanupFailure);
            Run("HCtrl successful postfix then finalizer restores only once", HCtrlCleanupOnce);
            Run("AutoSpeed exception restores previous team and owner state", AutoSpeedException);
            Run("Runtime session cleanup releases cached plugin objects", RuntimeSessionCleanup);
            Run("Cyu coroutine exception restores camera and previous team", CyuCoroutineException);
            Run("Cyu kiss cleanup stops only the kiss iterator", CyuStopsOnlyKiss);
            Run("Cyu natural completion releases its tracked iterator", CyuCompletionReleasesIterator);
            Run("Nested external contexts restore their own caller", NestedContextRecovery);
            Run("Coroutine guard handles failures on resumed frames", CoroutineFailureAfterYield);
            Run("Coroutine guard cleans up cancellation", CoroutineCancellation);
            Run("Coroutine guard cleans up failing error handlers", CoroutineErrorHandlerFailure);
            Run("Breastmilk coroutine failure releases native processing", BreastmilkFailureFallback);
            Run("Breastmilk teardown clears references even if state restore fails", BreastmilkCleanupFailure);
            Run("Breastmilk partial binding failure restores the previous team", BreastmilkPartialBindingFailure);
            Run("Breastmilk failed parallel cleanup releases native processing", BreastmilkParallelCleanupFailure);
            Run("HSceneOptions reset clears prior loop and timer values", HSceneOptionsReset);
            Run("Native special menu exception restores original mode", NativeMenuFailure);
            Run("Native special menu restoration is idempotent", NativeMenuCleanupOnce);
            Run("Native portrait exception restores detached portrait", NativePortraitFailure);
            Run("Previous scene cleanup preserves a newer Free H selection", NewSelectionSurvivesPreviousScene);
            Run("Completed Free H launch clears only its own selection", OwnSelectionCompletes);
            Run("Incomplete Free H selection cannot become launch-ready", IncompleteSelection);
            Console.WriteLine("MultiTeamH regression: " + passed + " passed.");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static object Get(Type type, string field) { return type.GetField(field, Private).GetValue(null); }
    static void Set(Type type, string field, object value) { type.GetField(field, Private).SetValue(null, value); }
    static object Invoke(Type type, string method, params object[] args) {
        try { return type.GetMethod(method, Private).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static TeamSession Fresh() {
        TeamSession.Snapshot.Clear();
        var session = new TeamSession();
        TeamSession.Snapshot.Add(session);
        Set(Expressions, "forcedTeam", -1);
        Set(Expressions, "lastActiveFemale", null);
        ((IDictionary)Get(Expressions, "States")).Clear();
        ExpressionHost.hFrag = new HFlag();
        Set(Expressions, "femaleListField", typeof(ExpressionHost).GetField("femaleList"));
        Set(Expressions, "hFlagField", typeof(ExpressionHost).GetField("hFrag"));
        Set(Expressions, "orgasmField", typeof(ExpressionHost).GetField("orgazumCount"));
        Set(Expressions, "motionField", typeof(ExpressionHost).GetField("nowMotion"));
        Set(Expressions, "actionField", typeof(ExpressionHost).GetField("nowHAction"));
        Set(Expressions, "feraField", typeof(ExpressionHost).GetField("isFeraAction"));
        Set(Expressions, "updateStateField", typeof(ExpressionHost).GetField("updateState"));
        RuntimeModCompat.SessionStarted();
        Set(Runtime, "hCtrlStateFields", new[] { typeof(HCtrlHost).GetField("loopSpeed") });
        Set(Runtime, "hCtrlAutoSpeedField", typeof(HCtrlHost).GetField("autoSpeed"));
        Set(Runtime, "hCtrlAutoSpeedStateFields", new[] { typeof(AutoHost).GetField("dest") });
        GameCursor.isLock = false;
        return session;
    }
    static ExpressionHost Host(TeamSession session) {
        return new ExpressionHost { femaleList = new List<ExpressionData> {
            new ExpressionData { female = session.Females[0] },
            new ExpressionData { female = session.Females[1] },
            new ExpressionData { female = new ChaControl() } } };
    }
    static object BeginExpression(ExpressionHost host) {
        object[] args = { host, null };
        Invoke(Expressions, "BeforeProc", args);
        return args[1];
    }
    static Exception FinishExpression(Exception error, ExpressionHost host, object state) {
        return (Exception)Invoke(Expressions, "FinalizeProc", error, host, state);
    }
    static void ExpressionException() {
        var session = Fresh(); var host = Host(session);
        var originalList = host.femaleList; var originalFlag = ExpressionHost.hFrag;
        var scope = BeginExpression(host);
        Assert(host.femaleList.Count == 2 && ExpressionHost.hFrag == session.Flags[0], "expected focused filtering");
        var error = new InvalidOperationException("original Proc failure");
        Assert(FinishExpression(error, host, scope) == error, "exception must not be swallowed");
        Assert(ReferenceEquals(originalList, host.femaleList) && ExpressionHost.hFrag == originalFlag, "context not restored");
    }
    static void MissingExpressionList() {
        Fresh(); var originalFlag = ExpressionHost.hFrag;
        var host = new ExpressionHost { femaleList = null };
        Assert(BeginExpression(host) == null, "missing list should not enter a scope");
        Assert(ExpressionHost.hFrag == originalFlag, "early return leaked target flag");
    }
    static void ExpressionCleanupOnce() {
        var session = Fresh(); var host = Host(session); var scope = BeginExpression(host);
        Invoke(Expressions, "AfterProc", host, null, scope);
        var laterFlag = new HFlag(); ExpressionHost.hFrag = laterFlag;
        var laterList = new List<ExpressionData>(); host.femaleList = laterList;
        FinishExpression(null, host, scope);
        Assert(ExpressionHost.hFrag == laterFlag && ReferenceEquals(host.femaleList, laterList), "finalizer reapplied completed cleanup");
    }
    static void UnmanagedCharactersOnce() {
        var session = Fresh(); var host = Host(session);
        Set(Expressions, "forcedTeam", 1);
        var scope = BeginExpression(host);
        Assert(host.femaleList.Count == 1 && host.femaleList[0].female == session.Females[1], "replay processes unmanaged character twice");
        FinishExpression(null, host, scope);
    }
    static void ExpressionSessionCleanup() {
        var session = Fresh(); var host = Host(session);
        var scope = BeginExpression(host); FinishExpression(null, host, scope);
        Set(Expressions, "forcedTeam", 1);
        scope = BeginExpression(host); FinishExpression(null, host, scope);
        Assert(((IDictionary)Get(Expressions, "States")).Count > 0, "test must create state");
        ExpressionControlCompat.SessionEnded(session);
        Assert(((IDictionary)Get(Expressions, "States")).Count == 0 && Get(Expressions, "lastActiveFemale") == null, "ended session retains characters");
    }
    static object BeginHCtrl(HCtrlHost host) {
        object[] args = { host, typeof(HCtrlHost).GetMethod("Update"), null };
        Invoke(Runtime, "BeforeHCtrl", args);
        return args[2];
    }
    static Exception FinishRuntime(Exception error, object state) { return (Exception)Invoke(Runtime, "FinalizeExternalControl", error, state); }
    static void PartialHCtrlPrefixFailure() {
        var session = Fresh(); session.ThrowOnPush = true; Set(Runtime, "forcedHCtrlTarget", 1);
        var host = new HCtrlHost();
        // MethodInfo.Invoke does not copy ref arguments back when a method
        // throws. Call directly through a compiled delegate, as Harmony does.
        var prefix = Runtime.GetMethod("BeforeHCtrl", Private);
        var scope = Expression.Variable(prefix.GetParameters()[2].ParameterType.GetElementType(), "scope");
        var error = Expression.Variable(typeof(Exception), "error");
        var caught = Expression.Parameter(typeof(Exception), "caught");
        var call = Expression.Call(prefix, Expression.Constant(host, typeof(object)),
            Expression.Constant(typeof(HCtrlHost).GetMethod("Update"), typeof(MethodBase)), scope);
        var body = Expression.Block(new[] { scope, error },
            Expression.TryCatch(Expression.Block(call, Expression.Empty()),
                Expression.Catch(caught, Expression.Block(Expression.Assign(error, caught), Expression.Empty()))),
            Expression.NewArrayInit(typeof(object), Expression.Convert(scope, typeof(object)), Expression.Convert(error, typeof(object))));
        object[] result = Expression.Lambda<Func<object[]>>(body).Compile()();
        Exception failure = (Exception)result[1];
        Assert(failure != null && result[0] != null && session.BoundTeam == 1, "prefix failure not simulated");
        Assert(FinishRuntime(failure, result[0]) == failure && session.BoundTeam == 0, "prefix exception stranded background context");
    }
    static void HCtrlException() {
        var session = Fresh(); Set(Runtime, "forcedHCtrlTarget", 1);
        var host = new HCtrlHost { loopSpeed = 10 }; var scope = BeginHCtrl(host);
        host.loopSpeed = 99; GameCursor.isLock = true;
        var error = new InvalidOperationException("HCtrl failure");
        Assert(FinishRuntime(error, scope) == error, "original HCtrl failure suppressed");
        Assert(session.BoundTeam == 0 && session.UiExits == 1 && !GameCursor.isLock && host.loopSpeed == 10, "HCtrl state not restored");
    }
    static void UiCleanupFailure() {
        var session = Fresh(); Set(Runtime, "forcedHCtrlTarget", 1); var host = new HCtrlHost { loopSpeed = 10 };
        var scope = BeginHCtrl(host); session.ThrowOnUiExit = true; host.loopSpeed = 99; GameCursor.isLock = true;
        var error = new InvalidOperationException("original failure");
        Assert(FinishRuntime(error, scope) == error, "cleanup replaced original exception");
        Assert(session.BoundTeam == 0 && session.Pops == 1 && !GameCursor.isLock && host.loopSpeed == 10, "UI cleanup blocked remaining recovery");
    }
    static void HCtrlCleanupOnce() {
        var session = Fresh(); session.Targets = new[] { 0 }; var host = new HCtrlHost(); var scope = BeginHCtrl(host);
        Invoke(Runtime, "AfterHCtrl", host, typeof(HCtrlHost).GetMethod("Update"), scope);
        FinishRuntime(null, scope);
        Assert(session.Pops == 1, "world context restored twice");
    }
    static void AutoSpeedException() {
        var session = Fresh(); var host = new HCtrlHost { loopSpeed = 10 };
        Invoke(Runtime, "RegisterAutoSpeedOwner", host); Set(Runtime, "forcedAutoSpeedTarget", 1);
        object[] args = { host.autoSpeed, typeof(AutoHost).GetMethod("Update"), null };
        Invoke(Runtime, "BeforeAutoSpeed", args); host.loopSpeed = 99; host.autoSpeed.dest = 99;
        FinishRuntime(new Exception("AutoSpeed failed"), args[2]);
        Assert(session.BoundTeam == 0 && host.loopSpeed == 10 && host.autoSpeed.dest == 0, "AutoSpeed leaked target state");
    }
    static void RuntimeSessionCleanup() {
        Fresh(); var host = new HCtrlHost(); Invoke(Runtime, "GetHCtrlStates", host); Invoke(Runtime, "RegisterAutoSpeedOwner", host);
        RuntimeModCompat.SessionEnded();
        Assert(((IDictionary)Get(Runtime, "hCtrlStates")).Count == 0 && ((IDictionary)Get(Runtime, "autoSpeedOwners")).Count == 0, "runtime caches retained plugin instances");
    }
    static void CyuCoroutineException() {
        var session = Fresh();
        var camera = new CameraControl_Ver2 { CameraFov = 30f };
        session.Flags[1].ctrlCamera = camera;
        Set(Runtime, "cyuType", typeof(CyuHost));
        Set(Runtime, "cyuFemaleField", typeof(CyuHost).GetField("female"));
        var host = new CyuHost { female = session.Females[1] };
        object[] args = { host.Loop(), false, null };
        Invoke(Runtime, "BeforeCyuKissCoroutine", args);
        Assert(session.BoundTeam == 1 && args[2] != null, "Cyu scope not entered");
        camera.CameraFov = 90f;
        var error = new Exception("Cyu MoveNext failure");
        Assert(FinishRuntime(error, args[2]) == error, "Cyu exception suppressed");
        Assert(session.BoundTeam == 0 && camera.CameraFov == 30f, "Cyu leaked camera/context");
        Assert(((IDictionary)Get(Runtime, "cyuKissIterators")).Count == 0, "failed Cyu coroutine retained its iterator");
    }
    static void CyuStopsOnlyKiss() {
        for (int actionChange = 0; actionChange < 2; actionChange++) {
            var session = Fresh();
            Set(Runtime, "cyuType", typeof(CyuHost));
            Set(Runtime, "cyuFemaleField", typeof(CyuHost).GetField("female"));
            Set(Runtime, "cyuKissFlagField", typeof(CyuHost).GetField("kissFlag"));
            Set(Runtime, "cyuKissingField", typeof(CyuHost).GetField("kissing"));
            Set(Runtime, "cyuKissMethod", typeof(CyuHost).GetMethod("Kiss"));
            var host = new CyuHost { female = session.Females[0], kissFlag = true, kissing = true };
            session.Females[0].BoundComponent = host;
            IEnumerator iterator = host.Loop(); object[] args = { iterator, false, null };
            Invoke(Runtime, "BeforeCyuKissCoroutine", args);
            FinishRuntime(null, args[2]);
            var team = new TeamState { Female = session.Females[0] };
            if (actionChange == 0) RuntimeModCompat.CompleteKiss(team);
            else RuntimeModCompat.ActionChanging(session, team);
            Assert(host.StopAllCalls == 0 && ReferenceEquals(host.StoppedCoroutine, iterator),
                "kiss cleanup stopped Waitfor/GetMouse or failed to stop the actual kiss iterator");
            Assert(!host.kissFlag && !host.kissing && ((IDictionary)Get(Runtime, "cyuKissIterators")).Count == 0,
                "kiss cleanup retained flags or its tracked iterator");
        }
    }
    static void CyuCompletionReleasesIterator() {
        var session = Fresh();
        Set(Runtime, "cyuType", typeof(CyuHost));
        Set(Runtime, "cyuFemaleField", typeof(CyuHost).GetField("female"));
        var host = new CyuHost { female = session.Females[0] };
        object[] args = { host.Loop(), false, null };
        Invoke(Runtime, "BeforeCyuKissCoroutine", args);
        Invoke(Runtime, "AfterCyuKissCoroutine", false, args[2]);
        Assert(((IDictionary)Get(Runtime, "cyuKissIterators")).Count == 0 && session.BoundTeam == 0,
            "completed Cyu iterator remained tracked");
    }
    static void NestedContextRecovery() {
        var session = Fresh(); Set(Runtime, "forcedHCtrlTarget", 1);
        var outer = BeginHCtrl(new HCtrlHost());
        Set(Runtime, "cyuType", typeof(CyuHost));
        Set(Runtime, "cyuFemaleField", typeof(CyuHost).GetField("female"));
        var host = new CyuHost { female = session.Females[0] };
        object[] args = { host.Loop(), null };
        Invoke(Runtime, "BeforeCyuWaitfor", args);
        Assert(session.BoundTeam == 0, "inner scope target missing");
        FinishRuntime(new Exception("inner failure"), args[1]);
        Assert(session.BoundTeam == 1, "inner scope skipped outer binding");
        FinishRuntime(null, outer);
        Assert(session.BoundTeam == 0, "outer scope failed to restore focus");
    }
    static void CoroutineFailureAfterYield() {
        bool disposed = false, finished = false; Exception observed = null;
        var routine = CoroutineGuard.Run(FailingIterator(delegate { disposed = true; }),
            delegate(Exception ex) { observed = ex; }, delegate { finished = true; });
        Assert(routine.MoveNext() && (string)routine.Current == "first", "initial yielded value missing");
        Assert(!routine.MoveNext() && observed is InvalidOperationException && disposed && finished,
            "resumed failure did not run rollback/final cleanup");
    }
    static void CoroutineCancellation() {
        bool disposed = false, finished = false;
        var routine = CoroutineGuard.Run(FailingIterator(delegate { disposed = true; }),
            null, delegate { finished = true; });
        Assert(routine.MoveNext(), "iterator not started");
        ((IDisposable)routine).Dispose();
        Assert(disposed && finished, "cancelled iterator left switch flag/resources alive");
    }
    static void CoroutineErrorHandlerFailure() {
        bool disposed = false, finished = false; Exception observed = null;
        var routine = CoroutineGuard.Run(FailingIterator(delegate { disposed = true; }),
            delegate { throw new ApplicationException("logger/handler failure"); }, delegate { finished = true; });
        routine.MoveNext();
        try { routine.MoveNext(); } catch (Exception ex) { observed = ex; }
        Assert(observed is ApplicationException && disposed && finished, "handler failure bypassed cleanup");
    }
    static IEnumerator FailingIterator(Action onDispose) {
        try { yield return "first"; throw new InvalidOperationException("resumed failure"); }
        finally { onDispose(); }
    }
    static void NativeMenuFailure() {
        var sprite = new HSprite { Mode = HFlag.EMode.lesbian };
        var type = typeof(SpecialActionMenuBuildHooks); object[] args = { sprite, HFlag.EMode.aibu };
        Invoke(type, "Prefix", args);
        Assert(sprite.Mode == HFlag.EMode.aibu, "temporary menu context missing");
        var error = new InvalidOperationException("CreateMotionList failure");
        object[] finalizer = { sprite, args[1], error };
        Assert(Invoke(type, "Finalizer", finalizer) == error && sprite.Mode == HFlag.EMode.lesbian,
            "native menu exception leaked temporary action mode");
    }
    static void NativeMenuCleanupOnce() {
        var sprite = new HSprite { Mode = HFlag.EMode.lesbian };
        var type = typeof(SpecialActionMenuBuildHooks); object[] args = { sprite, HFlag.EMode.aibu };
        Invoke(type, "Prefix", args); Invoke(type, "Postfix", args);
        Invoke(type, "Finalizer", sprite, args[1], null);
        Assert(sprite.ModeRestores == 1, "native menu cleanup ran twice");
    }
    static void NativePortraitFailure() {
        var sprite = new HSprite(); var type = typeof(NativeThreePPortraitHook);
        Invoke(type, "PreserveTeamPortrait", sprite);
        var error = new Exception("InitHeroine failure");
        Assert(Invoke(type, "RecoverTeamPortrait", sprite, error) == error && !sprite.PortraitDetached && sprite.PortraitRestores == 1,
            "native portrait stayed detached after failure");
    }
    static void NewSelectionSurvivesPreviousScene() {
        MultiTeamSelection.Set(new SaveData.Heroine(), new SaveData.Player(), 1, 2);
        int oldRevision = MultiTeamSelection.Revision;
        var newFemale = new SaveData.Heroine(); var newMale = new SaveData.Player();
        MultiTeamSelection.Set(newFemale, newMale, 3, 4);
        MultiTeamSelection.MarkLaunchDataReady();
        MultiTeamSelection.Complete(oldRevision);
        Assert(MultiTeamSelection.HasPending && MultiTeamSelection.LaunchDataReady &&
            MultiTeamSelection.SecondaryFemale == newFemale && MultiTeamSelection.SecondaryMale == newMale &&
            MultiTeamSelection.SecondaryStage == 3 && MultiTeamSelection.SecondaryStatus == 4,
            "previous scene teardown consumed the next launch selection");
    }
    static void OwnSelectionCompletes() {
        MultiTeamSelection.Set(new SaveData.Heroine(), new SaveData.Player(), 1, 2);
        MultiTeamSelection.MarkLaunchDataReady();
        MultiTeamSelection.Complete(MultiTeamSelection.Revision);
        Assert(!MultiTeamSelection.HasPending && !MultiTeamSelection.LaunchDataReady &&
            MultiTeamSelection.SecondaryFemale == null && MultiTeamSelection.SecondaryMale == null &&
            MultiTeamSelection.SecondaryStage == 0 && MultiTeamSelection.SecondaryStatus == 0,
            "completed launch retained its characters or launch flags");
    }
    static void IncompleteSelection() {
        MultiTeamSelection.Set(new SaveData.Heroine(), null, 1, 2);
        MultiTeamSelection.MarkLaunchDataReady();
        Assert(!MultiTeamSelection.HasPending && !MultiTeamSelection.LaunchDataReady, "incomplete selection allowed a launch");
        MultiTeamSelection.Clear();
    }
    static void AttachMilk(TeamSession session) {
        Type type = typeof(BreastmilkCompat);
        Set(type, "disabled", false);
        string[] names = (string[])Get(type, "StateFieldNames");
        var fields = Array.ConvertAll(names, delegate(string name) { return typeof(MilkHost).GetField(name); });
        Set(type, "stateFields", fields);
        Set(type, "updateMethod", typeof(MilkHost).GetMethod("Update"));
        Set(type, "procMethod", typeof(MilkHost).GetMethod("ProcEModeSonyu"));
        Assert((bool)Invoke(type, "EnsureAttached", session, new MilkHost()), "milk boundary did not attach");
    }
    static void BreastmilkFailureFallback() {
        var session = Fresh(); AttachMilk(session); Type type = typeof(BreastmilkCompat);
        object[] suspended = { false };
        Assert(!(bool)Invoke(type, "SuspendOriginalIterator", suspended), "native iterator should be suspended during managed processing");
        Invoke(type, "Advance", session, 1);
        Invoke(type, "Advance", session, 1);
        Assert(Get(type, "session") == null && (bool)Get(type, "disabled"), "failure left broken parallel session attached");
        Assert((bool)Invoke(type, "SuspendOriginalIterator", suspended), "native coroutine remained permanently suspended");
        Assert(session.BoundTeam == 0, "milk failure leaked bound team");
    }
    static void BreastmilkCleanupFailure() {
        var session = Fresh(); AttachMilk(session); Type type = typeof(BreastmilkCompat);
        Array states = (Array)Get(type, "states");
        object state = states.GetValue(0);
        var values = (object[])state.GetType().GetField("values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
        values[0] = new object();
        Exception failure = null;
        try { BreastmilkCompat.SessionEnded(session); } catch (Exception ex) { failure = ex; }
        Assert(failure != null, "restore failure was not injected");
        Assert(Get(type, "session") == null && Get(type, "plugin") == null && states.GetValue(0) == null && states.GetValue(1) == null,
            "failed restore retained ended scene/plugin references");
        object[] suspended = { false };
        Assert((bool)Invoke(type, "SuspendOriginalIterator", suspended), "native coroutine remains suspended after cleanup failure");
    }
    static void BreastmilkPartialBindingFailure() {
        var session = Fresh(); AttachMilk(session); session.ThrowOnPush = true;
        Invoke(typeof(BreastmilkCompat), "Advance", session, 1);
        Assert(session.BoundTeam == 0 && session.Pops == 1, "partial Milk context binding leaked background team");
        Assert(Get(typeof(BreastmilkCompat), "session") == null, "failed binding left the managed Milk session attached");
    }
    static void BreastmilkParallelCleanupFailure() {
        var session = Fresh(); AttachMilk(session); Type type = typeof(BreastmilkCompat);
        BreastmilkCompat.EnterParallelTeam(session, 1);
        Array states = (Array)Get(type, "states");
        object focused = states.GetValue(0);
        var values = (object[])focused.GetType().GetField("values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(focused);
        values[0] = new object();
        BreastmilkCompat.LeaveParallelTeam(session, 1);
        Assert(Get(type, "session") == null && !(bool)Get(type, "parallelStateBound") && (bool)Get(type, "disabled"),
            "parallel cleanup failure left a live scope or replaced the caller's error");
        object[] suspended = { false };
        Assert((bool)Invoke(type, "SuspendOriginalIterator", suspended), "failed parallel cleanup still suppresses the native iterator");
    }
    static void HSceneOptionsReset() {
        var states = (Array)Get(typeof(HSceneOptionsCompat), "States");
        foreach (object state in states) {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            state.GetType().GetField("forceOLoop", flags).SetValue(state, true);
            state.GetType().GetField("forceStopVoice", flags).SetValue(state, true);
            state.GetType().GetField("orgasmTimer", flags).SetValue(state, 99f);
            state.GetType().GetField("Initialized", flags).SetValue(state, true);
        }
        HSceneOptionsCompat.Reset();
        foreach (object state in states) {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert(!(bool)state.GetType().GetField("forceOLoop", flags).GetValue(state) &&
                !(bool)state.GetType().GetField("forceStopVoice", flags).GetValue(state) &&
                (float)state.GetType().GetField("orgasmTimer", flags).GetValue(state) == 0f,
                "new scene inherited old HSceneOptions counters");
        }
    }
    internal sealed class MilkHost : UnityEngine.MonoBehaviour {
        public static float MilkCharge_Speed = 0.1f;
        public static int setNum = 1;
        public static float spMilkKeep, spmassage, spPosition, spnonKeep, z = 0.05f, spMilk = 180f;
        public static bool boReset;
        public static float[] timerSiru = new float[2];
        public static int preNum;
        public static BepInEx.Configuration.ConfigEntry<bool> Enabled {
            get { return new BepInEx.Configuration.ConfigEntry<bool> { Value = true }; }
        }
        public void Update() { }
        public static IEnumerator ProcEModeSonyu() { yield return null; throw new InvalidOperationException("native iterator error"); }
    }
    internal sealed class CyuHost : UnityEngine.MonoBehaviour {
        public ChaControl female;
        public bool kissFlag, kissing;
        public void Kiss(bool active) { kissFlag = active; }
        public IEnumerator Loop() { yield return female; }
    }
    internal sealed class ExpressionData { public ChaControl female; }
    internal sealed class ExpressionHost {
        public static HFlag hFrag;
        public List<ExpressionData> femaleList;
        public int orgazumCount;
        public string nowMotion = "", nowHAction = "";
        public bool isFeraAction, updateState;
    }
    internal sealed class HCtrlHost {
        public int loopSpeed;
        public AutoHost autoSpeed = new AutoHost();
        public void Update() { }
    }
    internal sealed class AutoHost : UnityEngine.Behaviour {
        public int dest;
        public void Update() { }
    }
}
