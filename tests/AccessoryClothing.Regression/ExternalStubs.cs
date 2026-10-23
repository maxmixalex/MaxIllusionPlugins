// Test doubles for external Unity/game services. All transfer, persistence,
// selection highlighting and MaterialEditor bridge algorithms are linked production code.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine {
    public class Object {
        private static int next;
        private readonly int id = ++next;
        public bool Destroyed;
        public static int InstantiateCalls;
        public string name = "test";
        public int GetInstanceID() { return id; }
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
        public static T Instantiate<T>(T value) where T : Object { InstantiateCalls++; return (T)((ICloneable)value).Clone(); }
    }
    public class Component : Object {
        public GameObject gameObject;
        public Transform transform { get { return gameObject == null ? null : gameObject.transform; } }
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) { return gameObject == null ? new T[0] : gameObject.GetComponentsInChildren<T>(includeInactive); }
    }
    public class GameObject : Object {
        public bool activeInHierarchy = true;
        public readonly List<object> Components = new List<object>();
        public readonly Transform transform = new Transform();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) { return Components.OfType<T>().ToArray(); }
        public object GetComponent(Type type) { return Components.FirstOrDefault(type.IsInstanceOfType); }
        public T Add<T>(T component) where T : Component { component.gameObject = this; Components.Add(component); return component; }
    }
    public class Renderer : Component {
        public bool enabled = true;
        public Material[] sharedMaterials = new Material[0];
    }
    public sealed class MeshRenderer : Renderer { }
    public sealed class SkinnedMeshRenderer : Renderer {
        public Mesh sharedMesh { get; set; }
        public Transform[] bones = new Transform[0];
        public Transform rootBone;
        private readonly Dictionary<int, float> weights = new Dictionary<int, float>();
        public void SetBlendShapeWeight(int index, float value) { weights[index] = value; }
        public float GetBlendShapeWeight(int index) { float value; return weights.TryGetValue(index, out value) ? value : 0; }
    }
    public sealed class Transform : Component { public Transform parent; }
    public sealed class Mesh : Object, ICloneable {
        public Vector3[] vertices = new Vector3[0];
        public BoneWeight[] boneWeights = new BoneWeight[0];
        public Matrix4x4[] bindposes = new Matrix4x4[0];
        private List<string> shapeNames = new List<string>();
        public int vertexCount { get { return vertices.Length; } }
        public int blendShapeCount { get { return shapeNames.Count; } }
        public void AddBlendShapeFrame(string name, float weight, Vector3[] deltas, Vector3[] normals, Vector3[] tangents) { shapeNames.Add(name); }
        public int GetBlendShapeIndex(string name) { return shapeNames.IndexOf(name); }
        public string GetBlendShapeName(int index) { return shapeNames[index]; }
        public void RecalculateBounds() { }
        public object Clone() { var copy = (Mesh)MemberwiseClone(); copy.vertices = (Vector3[])vertices.Clone(); copy.shapeNames = new List<string>(shapeNames); return copy; }
    }
    public struct BoneWeight {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }
    public struct Matrix4x4 {
        private System.Numerics.Matrix4x4 value;
        public static Matrix4x4 identity { get { return new Matrix4x4 { value = System.Numerics.Matrix4x4.Identity }; } }
        public Matrix4x4 inverse { get { System.Numerics.Matrix4x4 result; System.Numerics.Matrix4x4.Invert(value, out result); return new Matrix4x4 { value = result }; } }
        public float this[int index] { get { return new[] { value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44 }[index]; } }
        public Vector3 MultiplyPoint3x4(Vector3 p) { var r = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(p.x, p.y, p.z), value); return new Vector3(r.X, r.Y, r.Z); }
        public Vector3 MultiplyVector(Vector3 p) { var r = System.Numerics.Vector3.TransformNormal(new System.Numerics.Vector3(p.x, p.y, p.z), value); return new Vector3(r.X, r.Y, r.Z); }
    }
    public static class Mathf {
        public static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        public static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        public static float Clamp01(float value) { return Clamp(value, 0, 1); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Sign(float value) { return Math.Sign(value); }
        public static float Sqrt(float value) { return (float)Math.Sqrt(value); }
        public static float Exp(float value) { return (float)Math.Exp(value); }
    }
    public static class Time { public static int frameCount; }
    public static class Application { public static string loadedLevelName = "test"; }
    public sealed class WaitForSeconds { public WaitForSeconds(float seconds) { } }
    public sealed class WaitForEndOfFrame { }
    public class Texture : Object { }
    public sealed class Material : Object {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public Material() { }
        public Material(Material source) { foreach (var item in source.values) values[item.Key] = item.Value; }
        public void SetColor(string key, Color value) { values[key] = value; }
        public Color GetColor(string key) { object value; return values.TryGetValue(key, out value) ? (Color)value : default(Color); }
        public void SetInt(string key, int value) { values[key] = value; }
        public int GetInt(string key) { object value; return values.TryGetValue(key, out value) ? (int)value : 0; }
        public void SetTexture(string key, Texture value) { values[key] = value; }
        public Texture GetTexture(string key) { object value; return values.TryGetValue(key, out value) ? (Texture)value : null; }
        public bool HasProperty(string key) { return values.ContainsKey(key); }
    }
    public struct Color {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
        public Vector3 normalized { get { return magnitude == 0 ? zero : this / magnitude; } }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z)); }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
        public static Vector3 operator /(Vector3 a, float b) { return new Vector3(a.x / b, a.y / b, a.z / b); }
    }
    public sealed class Coroutine { }
}

public sealed class ChaControl : UnityEngine.Component {
    public readonly ChaFile chaFile = new ChaFile();
    public ChaFileCoordinate nowCoordinate = new ChaFileCoordinate();
    public readonly ChaFileStatus fileStatus = new ChaFileStatus();
    public readonly TestListController lstCtrl = new TestListController();
    public UnityEngine.GameObject[] objAccessory = new UnityEngine.GameObject[20];
    public UnityEngine.GameObject[] objClothes = new UnityEngine.GameObject[9];
    public UnityEngine.GameObject objBody;
    public byte sex = 1;
    public object[] cusAcsCmp, infoAccessory, hideHairAcs;
    public Func<IEnumerator> ReloadFactory;
    public IEnumerator StartedRoutine;
    public ChaControl() { gameObject = new UnityEngine.GameObject(); }
    public string GetAccessoryDefaultParentStr(int type, int id) { return "a_n_head"; }
    public UnityEngine.Coroutine StartCoroutine(IEnumerator routine) { StartedRoutine = routine; return new UnityEngine.Coroutine(); }
    public void AssignCoordinate(ChaFileDefine.CoordinateType type) { }
    public IEnumerator ReloadAsync(bool a, bool b, bool c, bool d, bool e) { return ReloadFactory == null ? EmptyRoutine() : ReloadFactory(); }
    private static IEnumerator EmptyRoutine() { yield break; }
}
public sealed class ChaFile {
    public ChaFileCoordinate[] coordinate = Enumerable.Range(0, 7).Select(_ => new ChaFileCoordinate()).ToArray();
    public readonly ChaFileParameter parameter = new ChaFileParameter();
}
public sealed class ChaFileParameter { public string fullname = "Alice"; }
public sealed class ChaFileStatus {
    public int coordinateType;
    public bool[] showAccessory = Enumerable.Repeat(true, 20).ToArray();
    public byte[] clothesState = new byte[9];
}
public sealed class ChaFileCoordinate {
    public ChaFileAccessory accessory = new ChaFileAccessory();
    public ChaFileClothes clothes = new ChaFileClothes();
}
public sealed class ChaFileAccessory {
    public PartsInfo[] parts = Enumerable.Range(0, 20).Select(_ => new PartsInfo()).ToArray();
    public sealed class PartsInfo {
        public int type = 120, id, hideCategory;
        public string parentKey = "head";
        public bool partsOfHead;
        public bool noShake { get; set; }
        public UnityEngine.Vector3[,] addMove = new UnityEngine.Vector3[2, 3];
        public UnityEngine.Color[] color = new UnityEngine.Color[4];
        public PartsInfo() {
            addMove[0, 2] = new UnityEngine.Vector3(1f, 1f, 1f);
            addMove[1, 2] = new UnityEngine.Vector3(1f, 1f, 1f);
        }
    }
}
public sealed class ChaFileClothes {
    public PartsInfo[] parts = Enumerable.Range(0, 9).Select(_ => new PartsInfo()).ToArray();
    public sealed class PartsInfo {
        public int id, emblemeId, emblemeId2, sleevesType;
        public bool[] hideOpt = new bool[2];
        public ColorInfo[] colorInfo = Enumerable.Range(0, 4).Select(_ => new ColorInfo()).ToArray();
        public sealed class ColorInfo {
            public UnityEngine.Color baseColor, patternColor;
            public int pattern;
            public UnityEngine.Vector3 tiling;
        }
    }
}
public static class ChaFileDefine { public enum CoordinateType { School01, School02, Gym, Swim, Club, Casual, Pajamas } }
public static class ChaListDefine { public enum CategoryNo { co_top = 1, co_bot, co_bra, co_shorts, co_gloves, co_panst, co_socks, co_shoes } }
public static class ChaAccessoryDefine { public static string GetReverseParent(string key) { return key + "_reversed"; } }
public sealed class ListInfoBase { public string Name = "test item"; }
public sealed class TestListController {
    public int Lookups;
    public ListInfoBase GetListInfo(ChaListDefine.CategoryNo category, int id) { Lookups++; return new ListInfoBase { Name = "Item " + id }; }
}
namespace ChaCustom { public sealed class CustomBase { public static CustomBase Instance; public bool updateCustomUI; } }
namespace KKAPI.Maker {
    public static class MakerAPI { public static bool InsideMaker; public static ChaControl GetCharacterControl() { return null; } }
}
namespace KKAPI { public enum GameMode { Maker, MainGame, Studio } }
namespace KKAPI.Chara {
    public class CharaCustomFunctionController : UnityEngine.Component {
        public ChaControl ChaControl;
        public ChaFile ChaFileControl;
        public IEnumerator StartedRoutine;
        public int StartedCount, StoppedCount;
        public UnityEngine.Coroutine StartCoroutine(IEnumerator routine) { StartedRoutine = routine; StartedCount++; return new UnityEngine.Coroutine(); }
        public void StopCoroutine(UnityEngine.Coroutine routine) { StoppedCount++; }
        protected virtual void OnReload(KKAPI.GameMode mode, bool maintainState) { }
        protected virtual void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate, bool maintainState) { }
        protected virtual void OnCardBeingSaved(KKAPI.GameMode mode) { }
        protected virtual void Update() { }
        protected virtual void OnDestroy() { }
    }
}
namespace AccessoryBatchManager {
    internal static class AccessoryNameLocalizer { internal static string Localize(string value) { return value; } }
    internal static class MakerEventBridge {
        internal static readonly List<string> Events = new List<string>();
        internal static void AccessoryKindChanged(int slot) { Events.Add("changed:" + slot); }
        internal static void AccessoryTransferred(int source, int destination) { Events.Add("transfer:" + source + ":" + destination); }
        internal static void AccessoriesCopied(int[] slots, int source, int destination) { Events.Add("copy:" + source + ":" + destination + ":" + string.Join(",", slots)); }
    }
}
namespace BepInEx { public static class Paths { public static string ConfigPath = System.IO.Path.GetTempPath(); } }
namespace ClothingCompression {
    internal sealed class ClothingCompressionPlugin {
        internal static readonly TestLog Log = new TestLog();
        internal static readonly TestOption<bool> VerboseDiagnostics = new TestOption<bool>();
        internal static readonly ClothingCompressionPlugin Instance = new ClothingCompressionPlugin();
        internal static readonly MeshAccessDetour MeshAccess = new MeshAccessDetour();
        internal static CompressionLibraryStore Library;
        internal void RefreshMakerControls(CompressionController controller) { }
    }
    internal sealed class TestOption<T> { internal T Value = default(T); }
    internal sealed class MeshAccessDetour { internal void Apply() { } internal void Undo() { } }
    internal sealed class TestLog {
        internal void LogError(object message) { }
        internal void LogInfo(object message) { }
        internal void LogWarning(object message) { }
        internal void LogMessage(object message) { }
    }
}
namespace Sideloader.AutoResolver {
    public sealed class ResolveInfo { public string GUID, Property; public int Slot; }
    public static class UniversalAutoResolver {
        public static ResolveInfo TryGetResolutionInfo(ChaListDefine.CategoryNo category, int id) { return null; }
    }
}
