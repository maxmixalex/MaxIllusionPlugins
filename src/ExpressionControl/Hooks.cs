using System;
using HarmonyLib;
namespace ExpressionControl
{
	public static class Hooks
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(FaceListCtrl), "SetFace", new Type[]
		{
			typeof(int),
			typeof(ChaControl),
			typeof(int),
			typeof(int)
		})]
		public static bool SetFaceHook(int _idFace, ChaControl _chara, int _voiceKind, int _action, FaceListCtrl __instance, ref bool __result)
		{
			if ((ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled)) return true;
			bool flag = _chara == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				Traverse traverse = Traverse.Create(__instance);
				bool flag2 = ExpressionControlPlugin.inst.IsIgnoreSetFace(_idFace, _chara, _voiceKind, _action, __instance, traverse.Field("blendEye").GetValue<GlobalMethod.FloatBlend>(), traverse.Field("blendMouth").GetValue<GlobalMethod.FloatBlend>());
				if (flag2)
				{
					__result = false;
					result = false;
				}
				else
				{
					result = true;
				}
			}
			return result;
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeEyebrowPtn", new Type[]
		{
			typeof(int),
			typeof(bool)
		})]
		public static bool ChangeEyebrowPtnHook(int ptn, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockExpressionPatternChange(__instance, ExpressionControl.PartType.eyebrow, ptn);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeEyesPtn", new Type[]
		{
			typeof(int),
			typeof(bool)
		})]
		public static bool ChangeEyesPtnHook(int ptn, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockExpressionPatternChange(__instance, ExpressionControl.PartType.eyes, ptn);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeMouthPtn", new Type[]
		{
			typeof(int),
			typeof(bool)
		})]
		public static bool ChangeMouthPtnHook(int ptn, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockExpressionPatternChange(__instance, ExpressionControl.PartType.mouth, ptn);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeEyesOpenMax", new Type[]
		{
			typeof(float)
		})]
		public static bool ChangeEyesOpenMaxHook(float __0, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockExpressionOpenChange(__instance, ExpressionControl.PartType.eyes, __0);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeMouthOpenMax", new Type[]
		{
			typeof(float)
		})]
		public static bool ChangeMouthOpenMaxHook(float __0, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockExpressionOpenChange(__instance, ExpressionControl.PartType.mouth, __0);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "HideEyeHighlight", new Type[]
		{
			typeof(bool)
		})]
		public static bool HideEyeHighlightHook(bool __0, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockHighlightChange(__instance, __0);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeEyesBlinkFlag", new Type[]
		{
			typeof(bool)
		})]
		public static bool ChangeEyesBlinkFlagHook(bool __0, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockBlinkChange(__instance, __0);
		}
		[HarmonyPrefix]
		[HarmonyPatch(typeof(ChaControl), "ChangeHohoAkaRate", new Type[]
		{
			typeof(float)
		})]
		public static bool ChangeHohoAkaRateHook(float __0, ChaControl __instance)
		{
			return (ExpressionControlPlugin.inst == null || !ExpressionControlPlugin.inst.enabled) || !ExpressionControlPlugin.inst.ShouldBlockHohoAkaChange(__instance, __0);
		}
	}
}
