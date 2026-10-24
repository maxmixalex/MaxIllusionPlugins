// These are deliberately small external-boundary substitutes. The actual
// compatibility implementations are linked into this test program.
// No Unity scene, native animation, Harmony patch engine or game code is emulated.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace BepInEx.Logging {
    public class ManualLogSource {
        public void LogInfo(object value) { }
        public void LogWarning(object value) { }
        public void LogError(object value) { }
    }
}
namespace BepInEx { }
namespace BepInEx.Configuration {
    public class ConfigEntry<T> { public T Value; }
    public struct KeyboardShortcut { public bool IsDown() { return false; } }
}
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public class HarmonyFinalizer : Attribute { }
    public class Harmony {
        public void Patch(MethodBase method, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null, HarmonyMethod finalizer = null) { }
    }
    public class HarmonyMethod { public HarmonyMethod(Type owner, string method) { } }
    public class CodeInstruction { public OpCode opcode; public object operand; }
    public static class AccessTools {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static FieldInfo Field(Type type, string name) {
            for (; type != null; type = type.BaseType) {
                FieldInfo field = type.GetField(name, All);
                if (field != null) return field;
            }
            return null;
        }
        public static MethodInfo Method(Type type, string name, Type[] args = null) {
            return args == null ? type.GetMethod(name, All) : type.GetMethod(name, All, null, args, null);
        }
        public static PropertyInfo Property(Type type, string name) { return type.GetProperty(name, All); }
    }
    public class Traverse {
        object target;
        FieldInfo field;
        public static Traverse Create(object target) { return new Traverse { target = target }; }
        public Traverse Field(string name) { field = AccessTools.Field(target.GetType(), name); return this; }
        public T GetValue<T>() { return (T)field.GetValue(target); }
        public void SetValue(object value) { field.SetValue(target, value); }
    }
}
namespace UnityEngine {
    public class Object {
        static int nextId;
        int id = ++nextId;
        public string name = "test";
        public int GetInstanceID() { return id; }
        public static T FindObjectOfType<T>() where T : class { return null; }
        public static Object FindObjectOfType(Type type) { return null; }
    }
    public class GameObject : Object {
        public Component AddComponent(Type type) { return (Component)Activator.CreateInstance(type); }
    }
    public class Component : Object {
        public GameObject gameObject = new GameObject();
        public Component BoundComponent;
        public Component GetComponent(Type type) { return BoundComponent != null && type.IsInstanceOfType(BoundComponent) ? BoundComponent : null; }
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour {
        public int StopAllCalls;
        public IEnumerator StoppedCoroutine;
        public void StopAllCoroutines() { StopAllCalls++; }
        public void StopCoroutine(IEnumerator routine) { StoppedCoroutine = routine; }
    }
    public struct Vector3 { }
    public struct Quaternion { }
    public class Transform : Object {
        public Vector3 position;
        public Quaternion rotation;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }
    public class Camera : Component { public Transform transform = new Transform(); }
    public struct AnimatorStateInfo { public string StateName; public bool IsName(string name) { return StateName == name; } }
    public abstract class CustomYieldInstruction { public abstract bool keepWaiting { get; } }
    public static class Time { public static int frameCount; public static float realtimeSinceStartup; }
}
public class ChaControl : UnityEngine.Component {
    public UnityEngine.AnimatorStateInfo getAnimatorStateInfo(int layer) { return new UnityEngine.AnimatorStateInfo(); }
}
public class HFlag : UnityEngine.Object {
    public enum EMode { none = -1, aibu, houshi, sonyu, masturbation, peeping, lesbian, houshi3P, sonyu3P }
    public enum ClickKind { none, inside, outside, actionChange }
    public enum FinishKind { none, inside, outside }
    public EMode mode;
    public ClickKind click;
    public FinishKind finish;
    public float gaugeMale;
    public bool isHSceneEnd, lockGugeMale, voiceWait;
    public string nowAnimStateName;
    public AnimationInfo nowAnimationInfo = new AnimationInfo();
    public AnimationInfo selectAnimationListInfo;
    public CameraControl_Ver2 ctrlCamera;
}
public class HSprite {
    public HFlag.EMode Mode;
    public int ModeRestores, PortraitRestores;
    public bool PortraitDetached;
}
public class AnimationInfo { public string nameAnimation; public int numCtrl; }
public static class SaveData { public class Heroine { } public class Player { } }
public class HandCtrl { public bool isKiss; }
public class FaceListCtrl { }
public class HVoiceCtrl { public class Voice { } }
public class HActionBase { public ChaControl female; }
public class HSceneProc {
    public List<ChaControl> lstFemale = new List<ChaControl>();
    public HVoiceCtrl voice = new HVoiceCtrl();
    public HandCtrl hand1 = new HandCtrl();
}
public class CameraControl_Ver2 {
    public UnityEngine.Vector3 TargetPos, CameraAngle, CameraDir;
    public float CameraFov;
    public UnityEngine.Transform targetObj, transBase;
    public UnityEngine.Camera thisCmaera;
}
public static class GameCursor { public static bool isLock; }
namespace MultiTeamH {
    internal static class DiagnosticProbe {
        internal static void OnceInfo(string key, string text) { }
        internal static void Info(string text) { }
        internal static void Warning(string text) { }
        internal static void Error(string text) { }
        internal static long Start() { return 0; }
        internal static double ElapsedMs(long started) { return 0; }
    }
    internal class TeamControllers { internal HandCtrl Hand = new HandCtrl(); }
    internal class TeamState {
        internal ChaControl Female, Male;
        internal HFlag Flags;
        internal bool IsClone;
        internal List<ChaControl> ContextMales = new List<ChaControl>();
        internal List<HActionBase> Actions = new List<HActionBase>();
        internal TeamControllers Controllers = new TeamControllers();
    }
    internal class TeamSession {
        internal static bool IsManagedSprite(HSprite sprite) { return sprite != null; }
        internal static bool BeginSpecialMenuBuild(HSprite sprite, out HFlag.EMode previous) {
            previous = sprite.Mode;
            if (sprite.Mode != HFlag.EMode.lesbian) return false;
            sprite.Mode = HFlag.EMode.aibu;
            return true;
        }
        internal static void EndSpecialMenuBuild(HSprite sprite, HFlag.EMode previous) { sprite.Mode = previous; sprite.ModeRestores++; }
        internal static void DetachManagedPortraitBeforeNative3P(HSprite sprite) { sprite.PortraitDetached = true; }
        internal static void RestoreManagedPortraitAfterNative3P(HSprite sprite) { sprite.PortraitDetached = false; sprite.PortraitRestores++; }
        internal static IList<TeamSession> Snapshot = new List<TeamSession>();
        internal readonly ChaControl[] Females = { new ChaControl(), new ChaControl() };
        internal readonly ChaControl[] Males = { new ChaControl(), new ChaControl() };
        internal readonly HFlag[] Flags = { new HFlag(), new HFlag() };
        internal int ActiveTeam, BoundTeam, Pops, UiExits;
        internal bool ThrowOnPush, ThrowOnUiExit, IsSwitching, IsLesbianActive, RejectFaceWrite;
        internal int[] Targets = { 0, 1 };
        internal HSceneProc Proc = new HSceneProc();
        internal ChaControl ActiveFemaleObject { get { return Females[ActiveTeam]; } }
        internal ChaControl ActiveMaleObject { get { return null; } }
        internal HFlag ActiveFlagsObject { get { return Flags[ActiveTeam]; } }
        internal HandCtrl ActiveHandObject { get { return null; } }
        internal ChaControl GetFemaleObject(int index) { return Females[index]; }
        internal ChaControl GetMaleObject(int index) { return Males[index]; }
        internal HFlag GetFlagsObject(int index) { return Flags[index]; }
        internal HFlag GetExpressionFlagsObject(int index) { return Flags[index]; }
        internal bool AllowFaceWrite(FaceListCtrl face, ChaControl female) { return !RejectFaceWrite; }
        internal HandCtrl GetHandObject(int index) { return null; }
        internal bool ContainsFemale(ChaControl female) { return GetTeamIndex(female) >= 0; }
        internal bool IsActiveFemale(ChaControl female) { return female == Females[ActiveTeam]; }
        internal int GetTeamIndex(ChaControl female) { return Array.IndexOf(Females, female); }
        internal object GetTeamActions(int index) { return null; }
        internal int GetBoundTeamIndex() { return BoundTeam; }
        internal bool PushControlContext(int target) {
            BoundTeam = target;
            if (ThrowOnPush) throw new InvalidOperationException("simulated partial bind");
            return target != ActiveTeam;
        }
        internal void PopExternalControlContext(int previous) { BoundTeam = previous < 0 ? ActiveTeam : previous; Pops++; }
        internal bool BeginExternalUiPass(int target) { return target != ActiveTeam; }
        internal void EndExternalUiPass(int target, bool started) {
            if (!started) return;
            UiExits++;
            if (ThrowOnUiExit) throw new InvalidOperationException("simulated UI cleanup failure");
        }
        internal static void RestoreSharedCursorLock(bool value, string source) { GameCursor.isLock = value; }
        internal int[] GetHCtrlTargets() { return Targets; }
        internal string DescribeControlState() { return "boundary"; }
        internal void CancelNativeKiss(TeamState team) { }
        internal void CapturePreKissCamera(ChaControl female) { }
        internal void EndKissCamera(ChaControl female, bool restore) { }
        internal void SetKissHeadOverride(ChaControl female, bool state) { }
        internal void RestoreMotionLookAfterKiss(ChaControl female) { }
        internal void PrepareKissCamera(ChaControl female) { }
        internal bool TryGetTeamBindings(ChaControl female, out ChaControl male, out HFlag flags, out HandCtrl hand) {
            male = null; flags = null; hand = null; return false;
        }
        internal bool TryGetExpressionBindings(ChaControl female, out FaceListCtrl face, out HandCtrl hand,
            out HVoiceCtrl.Voice voice, out int index) {
            face = null; hand = null; voice = null; index = 0; return false;
        }
    }
}
