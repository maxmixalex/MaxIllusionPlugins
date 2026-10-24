using System;
using UnityEngine;
using EC = ExpressionControl.ExpressionControl;

internal static class ExpressionRuntimeRegression {
    internal static void Run(Action<string, Action> run) {
        run("Expression hotkey defers MaximumUI mount to OnGUI", DeferredWindowCreation);
        run("Expression repeated GUI events do not retoggle the window", RepeatedGuiEvents);
        run("Expression queued toggles cancel without allocating a window", ToggleParity);
        run("Expression startup without a shortcut is safe", MissingShortcut);
        run("Expression mount failure restores shared GUI state", DrawFailureRestoresState);
        run("Expression window request survives a processing exception", ProcessingFailureKeepsWindowRequest);
        run("Expression typing shortcut does not toggle the editor", TextInputOwnsShortcut);
        run("Expression modal owns shortcut until it closes", ModalOwnsShortcut);
        run("Expression destruction cancels pending toggle and unmounts once", DestroyUnmounts);
        ExpressionProgramUiRegression.Run(run);
        ExpressionEditorViewRegression.Run(run);
        run("Expression state uses the current team's H flag", TeamState);
        run("Expression missing animation data is safe and can recover", MissingAnimationData);
        run("Expression action refresh does not require a motion change", ActionChangesInSameMotion);
        run("Expression orgasm counter changes once per transition and is capped", OrgasmTransitions);
        ExpressionProcessingRegression.Run(run);
    }

    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static EC Fresh() {
        Input.Modifiers.Clear(); Input.Pressed.Clear();
        new ExpressionControl.ShortCutKeyMgr().Update();

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
        Assert(!host.Mounted && host.Mounts == 0, "LateUpdate constructed named GUIStyles outside IMGUI");
        Assert(host.Updates == 1 && host.ProcCalls == 1, "hotkey changed normal expression processing");
        Draw(host);
        Assert(host.Mounted && host.Visible && host.Mounts == 1, "queued window did not open in OnGUI");
    }
    static void RepeatedGuiEvents() {
        var host = Fresh(); Press(host); Draw(host); Draw(host);
        Assert(host.Visible && host.Toggles == 1 && host.Mounts == 1, "Repaint/Layout recreated or toggled the UI twice");
        Press(host); Draw(host);
        Assert(!host.Visible && host.Toggles == 2, "second hotkey did not hide the window");
        Press(host); Draw(host);
        Assert(host.Visible && host.Mounts == 1, "reopening recreated GUI resources");
    }
    static void ToggleParity() {
        var host = Fresh(); Press(host); Press(host); Draw(host);
        Assert(!host.Mounted && host.Mounts == 0, "cancelled request allocated a hidden UI");
    }
    static void MissingShortcut() {
        var host = Fresh(); host.hotkey = null; host.LateUpdate();
        host.hotkey = new ExpressionControl.ShortCutKeyMgr(); host.LateUpdate(); Draw(host);
        Assert(host.ProcCalls == 2 && !host.Mounted, "absent shortcut broke runtime processing");
    }
    static void DrawFailureRestoresState() {
        var host = Fresh();
        host.ThrowOnMount = true;
        Press(host);
        try { Draw(host); throw new Exception("expected mount failure"); }
        catch (InvalidOperationException) { }
        Assert(GUI.color.Equals(new Color(1f)) && GUI.contentColor.Equals(new Color(2f)) &&
            GUI.backgroundColor.Equals(new Color(3f)) && GUI.enabled, "Expression mount leaked GUI state after failure");
        host.ThrowOnMount = false; Press(host); Draw(host);
        Assert(host.Mounted && host.Visible, "mount failure prevented a later retry");
    }
    static void TextInputOwnsShortcut() {
        var host = Fresh(); host.TextEditing = true; Press(host); Draw(host);
        Assert(!host.Mounted && host.Updates == 0 && host.ProcCalls == 1, "typing L opened the editor or skipped expression processing");
        host.TextEditing = false; Press(host); Draw(host);
        Assert(host.Visible, "hotkey did not recover after leaving text input");
    }
    static void ModalOwnsShortcut() {
        var host = Fresh(); Press(host); Draw(host); host.ModalOpen = true; Press(host); Draw(host);
        Assert(host.Visible && host.Toggles == 1, "modal shortcut hid the owning editor");
        host.ModalOpen = false; Press(host); Draw(host);
        Assert(!host.Visible, "modal closure did not restore hotkey");
    }
    static void DestroyUnmounts() {
        var host = Fresh(); Press(host); Draw(host); Press(host); host.OnDestroy(); Draw(host); host.OnDestroy();
        Assert(!host.Mounted && !host.Visible && host.Unmounts == 1 && host.Toggles == 1,
            "destruction left a mount or replayed a pending toggle");
    }
    static void ProcessingFailureKeepsWindowRequest() {
        var host = Fresh(); host.ThrowOnProc = true;
        try { Press(host); throw new Exception("expected processing failure"); }
        catch (InvalidOperationException) { }
        Draw(host);
        Assert(host.Mounted && host.Visible, "expression processing error discarded the UI open request");
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

// Native mount and scene boundaries only. Lifecycle/state control flow above
// is linked directly from production partials. No Unity drawing is simulated.
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
    public partial class ExpressionControl {
        private static readonly string hotkeyShowGUI = "showGUI";
        internal ShortCutKeyMgr hotkey;
        internal bool Mounted, Visible, TextEditing, ModalOpen, ThrowOnMount;
        internal int Mounts, Toggles, Unmounts;
        private bool IsMaximumUiInputBusy() { return TextEditing || ModalOpen; }
        private void ToggleMaximumUi() {
            if (!GUI.InOnGui) throw new InvalidOperationException("Mount requires current IMGUI skin");
            GUI.color = GUI.contentColor = GUI.backgroundColor = new Color(99f); GUI.enabled = false;
            if (ThrowOnMount) throw new InvalidOperationException("simulated mount error");
            if (!Mounted) { Mounted = true; Mounts++; }
            Visible = !Visible; Toggles++;
        }
        private void UnmountMaximumUi() {
            if (Mounted) Unmounts++;
            Mounted = Visible = false;
        }
        internal int Updates, ProcCalls, orgazumCount;
        internal bool ThrowOnProc, updateState, isFeraAction;
        internal string nowMotion, nowHAction;
        internal static HSceneProc hsceneProc;
        internal static HFlag hFrag;
        public void UpdateFemale() { Updates++; }
        public void Proc() { ProcCalls++; if (ThrowOnProc) throw new InvalidOperationException("simulated processing error"); ProcessExpressions(); }
        internal void TestUpdateHState() { UpdateHState(); }
    }
}
