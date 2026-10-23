// Only engine/game/loader boundaries are replaced. Production persistence,
// selection, normalization, shortcut and discovery code is linked by the project.
using System;
using System.Collections.Generic;

namespace UnityEngine {
    public class Object {
        public static T FindObjectOfType<T>() where T : class { return null; }
    }
    public class MonoBehaviour : Object { }
    public class Coroutine { }
    public enum KeyCode { LeftControl, RightControl, LeftShift, RightShift, LeftAlt, RightAlt }
    public static class Input {
        public static readonly HashSet<KeyCode> Modifiers = new HashSet<KeyCode>();
        public static readonly HashSet<string> Pressed = new HashSet<string>();
        public static int StringQueries;
        public static bool GetKey(KeyCode key) { return Modifiers.Contains(key); }
        public static bool GetKey(string key) {
            StringQueries++;
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Empty Unity key name");
            return Pressed.Contains(key);
        }
        public static bool GetKeyDown(string key) { return GetKey(key); }
    }
    public static class Time { public static float deltaTime = 0.02f; }
    public static class Screen { public static int width = 1280; }
    public static class Mathf {
        public static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Lerp(float from, float to, float t) { return from + (to - from) * Math.Max(0f, Math.Min(1f, t)); }
        public static float Log10(float value) { return (float)Math.Log10(value); }
    }
}

public class ChaControl : UnityEngine.MonoBehaviour {
    public bool hiPoly = true;
    public byte tearsLv;
    public readonly CharacterStatus fileStatus = new CharacterStatus();
    public readonly FaceBlendShapes fbsCtrl = new FaceBlendShapes();
    public int EyebrowPattern, EyesPattern, MouthPattern, MouthOpenWrites;
    public bool Blink = true;
    public float EyeOpen = 1f, MouthOpen;
    public int GetEyebrowPtn() { return EyebrowPattern; }
    public int GetEyesPtn() { return EyesPattern; }
    public int GetMouthPtn() { return MouthPattern; }
    public bool GetEyesBlinkFlag() { return Blink; }
    public float GetEyesOpenMax() { return EyeOpen; }
    public float GetMouthOpenMax() { return MouthOpen; }
    public void ChangeEyebrowPtn(int pattern, bool blend) { EyebrowPattern = pattern; }
    public void ChangeEyesPtn(int pattern, bool blend) { EyesPattern = pattern; }
    public void ChangeMouthPtn(int pattern, bool blend) { MouthPattern = pattern; }
    public void ChangeEyesOpenMax(float value) { EyeOpen = value; }
    public void ChangeMouthOpenMax(float value) { MouthOpen = value; MouthOpenWrites++; }
    public void ChangeHohoAkaRate(float value) { fileStatus.hohoAkaRate = value; }
    public void HideEyeHighlight(bool value) { fileStatus.hideEyesHighlight = value; }
    public void ChangeEyesBlinkFlag(bool value) { Blink = value; }
    public object GetComponent(Type type) { return null; }
}
public class FaceBlendShapes {
    public readonly BlendShape EyebrowCtrl = new BlendShape(), EyesCtrl = new BlendShape(), MouthCtrl = new BlendShape();
    public class BlendShape { public void CalculateBlendShape() { } }
}
public static class GlobalMethod {
    public class FloatBlend {
        public float LastFrom, LastTo, LastTime;
        public int Starts;
        public void Start(float from, float to, float time) { LastFrom = from; LastTo = to; LastTime = time; Starts++; }
    }
}
public class CharacterStatus { public bool hideEyesHighlight; public float hohoAkaRate; }
public static class ChaFileDefine {
    public enum SiruParts { SiruKao, SiruFrontUp, SiruFrontDown, SiruBackUp, SiruBackDown }
}
public class FaceListCtrl {
    public GlobalMethod.FloatBlend blendEye = new GlobalMethod.FloatBlend(), blendMouth = new GlobalMethod.FloatBlend();
    public class FaceInfo {
        public int eyebrow, eye, mouth, tears;
        public bool highlight, eyesblink;
        public float openMaxEye, openMinMouth, cheek;
    }
}
public class StudioScene : UnityEngine.MonoBehaviour { }
public class Singleton<T> { public static T Instance; }
namespace Manager {
    public class Character : UnityEngine.MonoBehaviour {
        public List<ChaControl> Female = new List<ChaControl>();
        public List<ChaControl> Male = new List<ChaControl>();
        public List<ChaControl> GetCharaList(byte sex) { return sex == 1 ? Female : Male; }
    }
}
namespace Studio {
    public class ObjectCtrlInfo { }
    public class OCIChar : ObjectCtrlInfo {
        public float MouthOpen;
        public void ChangeMouthOpen(float value) { MouthOpen = value; }
    }
    public class OCICharFemale : OCIChar { }
    public class OCICharMale : OCIChar { }
    public class Studio : UnityEngine.MonoBehaviour {
        public Dictionary<int, ObjectCtrlInfo> dicObjectCtrl = new Dictionary<int, ObjectCtrlInfo>();
    }
}
namespace BepInEx {
    public static class Paths { public static string ConfigPath; }
}
namespace BepInEx.Bootstrap {
    public class Metadata { public string GUID, Name; }
    public class PluginInfo { public Metadata Metadata; public object Instance; }
    public static class Chainloader {
        private static readonly Dictionary<string, PluginInfo> Plugins = new Dictionary<string, PluginInfo>();
        public static int DiscoveryReads;
        public static Dictionary<string, PluginInfo> PluginInfos { get { DiscoveryReads++; return Plugins; } }
    }
}
namespace ExpressionControl {
    public static class ExpressionControlPlugin { public const string Name = "ExpressionControl", Version = "test"; }
}
namespace JuiceControl {
    internal static class JuiceControl { internal static void LogDebug(object value) { } }
}
