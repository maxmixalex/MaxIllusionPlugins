
using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute {
        public HarmonyPatch(Type type, string methodName) { }
    }
}

namespace UnityEngine {
    internal sealed class GameObject {
        public bool activeSelf;
        public void SetActive(bool active) { activeSelf = active; }
    }
}

namespace UnityEngine.UI {
    internal sealed class Button {
        public bool interactable = true;
        public readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    }
}

internal sealed class HFlag {
    public bool isFreeH;
    public HSceneProc.AnimationListInfo selectAnimationListInfo;
    public HSceneProc.AnimationListInfo nowAnimationInfo;
}

internal sealed class HSceneProc {
    internal sealed class AnimationListInfo {
        public bool isFemaleInitiative;
        public AnimationListInfo(bool female) { isFemaleInitiative = female; }
    }
}

internal sealed class HSprite {
    public readonly HFlag flags = new HFlag();
    public readonly SonyuUi sonyu = new SonyuUi();
    public readonly FakeHeroine leadingHeroine = new FakeHeroine();
    public int FrontVoiceCalls, FrontSilentCalls, AnalVoiceCalls, AnalSilentCalls;
    public bool ThrowOnNormal;

    public void SetSonyuStart() { }
    public void SetSonyuActionButtonActive() { }
    public void OnInsertClick() {
        if (!FemboyMode.HSceneHooksRegression.NativeFrontAllowed(this)) return;
        if (ThrowOnNormal) throw new InvalidOperationException("mock game error");
        FrontVoiceCalls++;
    }
    public void OnInsertNoVoiceClick() {
        if (!FemboyMode.HSceneHooksRegression.NativeFrontAllowed(this)) return;
        FrontSilentCalls++;
    }
    public void OnInsertAnalClick() { AnalVoiceCalls++; }
    public void OnInsertAnalNoVoiceClick() { AnalSilentCalls++; }
}

internal sealed class FakeHeroine {
    public readonly object chaCtrl = new object();
}

internal sealed class SonyuUi {
    public readonly CategoryActionUi categoryActionButton = new CategoryActionUi();
}

internal sealed class CategoryActionUi {
    public readonly List<UnityEngine.UI.Button> lstButton =
        new List<UnityEngine.UI.Button>();
    public CategoryActionUi() {
        for (int i = 0; i < 14; i++)
            lstButton.Add(new UnityEngine.UI.Button());
    }
}

namespace KKAPI.Utilities {
    internal static class HSpriteExtensions {
        public static FakeHeroine GetLeadingHeroine(this HSprite sprite) {
            return sprite == null ? null : sprite.leadingHeroine;
        }
    }
}

namespace FemboyMode {
    internal sealed class FakeEntry<T> { public T Value; }

    internal static class FemboyModePlugin {
        public static FakeEntry<bool> EnableFreeHRestrictions =
            new FakeEntry<bool> { Value = true };
        public static bool Marked = true;
        public static bool IsFemboy(object character) { return Marked; }
    }
}
