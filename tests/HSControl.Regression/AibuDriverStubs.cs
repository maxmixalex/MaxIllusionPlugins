using System;
using System.Collections;

namespace UnityEngine
{
    internal static class Input
    {
        internal static bool PrimaryDown;
        internal static bool SecondaryDown;
        internal static bool GetMouseButton(int index) {
            return index == 0 ? PrimaryDown :
                index == 1 && SecondaryDown;
        }
    }
    internal static class Random
    {
        internal static float Range(float min, float max) { return min; }
    }
    internal static class Mathf
    {
        internal static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
        internal static float Clamp01(float value) { return Clamp(value, 0f, 1f); }
    }
}

internal sealed class TestArea
{
    public int kindTouch = 3;
    public int idUse = 1;
}
internal sealed class TestMotion
{
    public int stateMotion;
}
internal sealed class HandCtrl
{
    public int action;
    public TestArea[] useAreaItems = { new TestArea() };
    public IDictionary actionUseItem = new Hashtable { { 1, new object() } };
    public TestMotion nowMES = new TestMotion();
}
internal sealed class HSceneProc
{
    public HandCtrl hand = new HandCtrl();
    public HandCtrl hand1 = new HandCtrl();
}
internal sealed class HFlag
{
    public int mode;
    public string nowAnimStateName = "M_Idle";
    public bool rely;
    public float speed, rateDragGauge = 1f, gaugeFemale;
    public int dragStarts, dragFinishes, femaleGaugeCalls;
    public bool DragStart() { dragStarts++; return true; }
    public bool FinishDrag() { dragFinishes++; return true; }
    public void FemaleGaugeUp(float amount, bool unused, bool nativeMotion)
    {
        femaleGaugeCalls++;
        gaugeFemale += amount;
    }
}
