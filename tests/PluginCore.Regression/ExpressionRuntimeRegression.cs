using System;
using UnityEngine;
using EC = ExpressionControl.ExpressionControl;

internal static class ExpressionRuntimeRegression {
    internal static void Run(Action<string, Action> run) {
        run("Expression hotkey defers named style creation to OnGUI", DeferredWindowCreation);
        run("Expression repeated GUI events do not retoggle the window", RepeatedGuiEvents);
        run("Expression queued toggles cancel without allocating a window", ToggleParity);
        run("Expression startup without a shortcut is safe", MissingShortcut);
        run("Expression drawing failure restores shared GUI state", DrawFailureRestoresState);
        run("Expression window request survives a processing exception", ProcessingFailureKeepsWindowRequest);
        run("Expression state uses the current team's H flag", TeamState);
        run("Expression missing animation data is safe and can recover", MissingAnimationData);
        run("Expression action refresh does not require a motion change", ActionChangesInSameMotion);
        run("Expression orgasm counter changes once per transition and is capped", OrgasmTransitions);
    }

    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static EC Fresh() {
        Input.Modifiers.Clear(); Input.Pressed.Clear();
        new ExpressionControl.ShortCutKeyMgr().Update();
        ExpressionControl.GUIMgr.Created = 0;
        GUI.InOnGui = false;
        GUI.color = new Color(1f); GUI.contentColor = new Color(2f); GUI.backgroundColor = new Color(3f); GUI.enabled = true;
        EC.hsceneProc = null; EC.hFrag = null;
        var host = new EC { hotkey = new ExpressionControl.ShortCutKeyMgr() };
        host.hotkey.Set("showGUI", "L");
        return host;
    }
    static void Press(EC host) {
        Input.Pressed.Add("l");
        try { host.LateUpdate(); }
        finally { Input.Pressed.Clear(); }
    }
    static void Draw(EC host) {
        GUI.InOnGui = true;
        try { host.OnGUI(); }
        finally { GUI.InOnGui = false; }
    }
    static void DeferredWindowCreation() {
        var host = Fresh();
        Press(host);
        Assert(host.gui == null && ExpressionControl.GUIMgr.Created == 0, "LateUpdate constructed named GUIStyles outside IMGUI");
        Assert(host.Updates == 1 && host.ProcCalls == 1, "hotkey changed normal expression processing");
        Draw(host);
        Assert(host.gui != null && host.gui.show && host.gui.Draws == 1, "queued window did not open in OnGUI");
    }
    static void RepeatedGuiEvents() {
        var host = Fresh(); Press(host); Draw(host); Draw(host);
        Assert(host.gui.show && host.gui.Draws == 2 && ExpressionControl.GUIMgr.Created == 1, "Repaint/Layout recreated or toggled the UI twice");
        Press(host); Draw(host);
        Assert(!host.gui.show && host.gui.Draws == 2, "second hotkey did not hide the window");
        Press(host); Draw(host);
        Assert(host.gui.show && ExpressionControl.GUIMgr.Created == 1, "reopening recreated GUI resources");
    }
    static void ToggleParity() {
        var host = Fresh(); Press(host); Press(host); Draw(host);
        Assert(host.gui == null && ExpressionControl.GUIMgr.Created == 0, "cancelled request allocated a hidden UI");
    }
    static void MissingShortcut() {
        var host = Fresh(); host.hotkey = null; host.LateUpdate();
        host.hotkey = new ExpressionControl.ShortCutKeyMgr(); host.LateUpdate(); Draw(host);
        Assert(host.ProcCalls == 2 && host.gui == null, "absent shortcut broke runtime processing");
    }
    static void DrawFailureRestoresState() {
        var host = Fresh();
        GUI.InOnGui = true;
        host.gui = new ExpressionControl.GUIMgr(1120, new Rect(), "test") { show = true };
        GUI.InOnGui = false;
        GUI.color = new Color(1f); GUI.contentColor = new Color(2f); GUI.backgroundColor = new Color(3f); GUI.enabled = true;
        host.gui.ThrowOnDraw = true;
        try { Draw(host); throw new Exception("expected draw failure"); }
        catch (InvalidOperationException) { }
        Assert(GUI.color.Equals(new Color(1f)) && GUI.contentColor.Equals(new Color(2f)) &&
            GUI.backgroundColor.Equals(new Color(3f)) && GUI.enabled, "Expression window leaked GUI state after failure");
    }
    static void ProcessingFailureKeepsWindowRequest() {
        var host = Fresh(); host.ThrowOnProc = true;
        try { Press(host); throw new Exception("expected processing failure"); }
        catch (InvalidOperationException) { }
        Draw(host);
        Assert(host.gui != null && host.gui.show, "expression processing error discarded the UI open request");
    }
    static void TeamState() {
        var host = Fresh();
        EC.hsceneProc = new HSceneProc { flags = new HFlag { mode = HFlag.EMode.aibu, nowAnimStateName = "Idle",
            nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "focused" } } };
        EC.hFrag = new HFlag { mode = HFlag.EMode.sonyu, nowAnimStateName = "S_IN_Start",
            nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "フェラ" } };
        host.TestUpdateHState();
        Assert(host.nowMotion == "S_IN_Start" && host.nowHAction == "フェラ" && host.isFeraAction && host.orgazumCount == 1,
            "background expression read the shared focused HScene flags");
    }
    static void MissingAnimationData() {
        var host = Fresh(); EC.hsceneProc = new HSceneProc(); host.TestUpdateHState();
        EC.hFrag = new HFlag(); host.TestUpdateHState();
        Assert(host.nowMotion == string.Empty && host.Updates == 0, "missing animation descriptor was not tolerated");
        host.updateState = false;
        EC.hFrag.nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "ready" };
        host.TestUpdateHState();
        Assert(host.nowHAction == "ready" && host.Updates == 1, "late animation descriptor was not applied");
    }
    static void ActionChangesInSameMotion() {
        var host = Fresh(); host.nowMotion = "WLoop"; host.nowHAction = "previous";
        EC.hsceneProc = new HSceneProc { flags = new HFlag { nowAnimStateName = "WLoop",
            nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "next" } } };
        host.TestUpdateHState();
        Assert(host.nowHAction == "next" && host.Updates == 1 && host.updateState,
            "same-state animation replacement left old character bindings");
    }
    static void OrgasmTransitions() {
        var host = Fresh(); EC.hsceneProc = new HSceneProc();
        EC.hFrag = new HFlag { mode = HFlag.EMode.lesbian, nowAnimStateName = "Orgasm" };
        EC.hsceneProc.flags = EC.hFrag;
        EC.hFrag.nowAnimationInfo = new HSceneProc.AnimationListInfo { nameAnimation = "test" };
        host.TestUpdateHState(); host.updateState = false; host.TestUpdateHState();
        Assert(host.orgazumCount == 1, "same orgasm state counted multiple times");
        host.orgazumCount = 10; host.nowMotion = "Idle"; host.updateState = false; host.TestUpdateHState();
        Assert(host.orgazumCount == 10, "orgasm counter exceeded its existing cap");
    }
}

// GUI and scene boundaries only. Lifecycle/state control flow above is linked
// directly from the production partials. No Unity drawing is simulated here.
namespace UnityEngine {
    public struct Color { public float Value; public Color(float value) { Value = value; } }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public static class GUI {
        public static bool InOnGui, enabled = true;
        public static Color color, contentColor, backgroundColor;
    }
}
public class HFlag {
    public enum EMode { aibu, houshi, sonyu, masturbation, lesbian }
    public EMode mode;
    public string nowAnimStateName;
    public HSceneProc.AnimationListInfo nowAnimationInfo;
}
public class HSceneProc {
    public HFlag flags;
    public class AnimationListInfo { public string nameAnimation; }
}
namespace ExpressionControl {
    internal class GUIMgr {
        internal static int Created;
        internal bool show, ThrowOnDraw;
        internal int Draws;
        internal GUIMgr(int id, Rect rect, string title) {
            if (!GUI.InOnGui) throw new InvalidOperationException("Named GUIStyle requires current IMGUI skin");
            Created++;
        }
        internal void Draw() {
            Draws++;
            GUI.color = GUI.contentColor = GUI.backgroundColor = new Color(99f); GUI.enabled = false;
            if (ThrowOnDraw) throw new InvalidOperationException("simulated draw error");
        }
    }
    public partial class ExpressionControl {
        private static readonly int WINDOW_ID = 1120;
        private static readonly string hotkeyShowGUI = "showGUI";
        internal ShortCutKeyMgr hotkey;
        internal GUIMgr gui;
        internal int Updates, ProcCalls, orgazumCount;
        internal bool ThrowOnProc, updateState, isFeraAction;
        internal string nowMotion, nowHAction;
        internal static HSceneProc hsceneProc;
        internal static HFlag hFrag;
        public void UpdateFemale() { Updates++; }
        public void Proc() { ProcCalls++; if (ThrowOnProc) throw new InvalidOperationException("simulated processing error"); }
        internal void TestUpdateHState() { UpdateHState(); }
    }
}
