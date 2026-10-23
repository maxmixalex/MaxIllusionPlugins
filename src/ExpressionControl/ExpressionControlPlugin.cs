using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
[assembly: System.Reflection.AssemblyFileVersion(ExpressionControl.ExpressionControlPlugin.Version)]
namespace ExpressionControl
{
	[BepInDependency(KKAPI.KoikatuAPI.GUID)]
	[BepInProcess(KKAPI.KoikatuAPI.StudioProcessName)]
	[BepInProcess(KKAPI.KoikatuAPI.GameProcessName)]
#if KK
	[BepInProcess(KKAPI.KoikatuAPI.GameProcessNameSteam)]
#endif
	[BepInPlugin(GUID, Name, Version)]
	/// <info>
	/// Plugin structure thanks to Keelhauled
	/// </info>
	public class ExpressionControlPlugin : BaseUnityPlugin
	{
		public const string GUID = "ExpressionControl";
        public const string Version = "0.2.7.20";
		public const string Name = GUID;
		
		public void Start()
		{
			SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>(this.OnSceneLoad);
			ExpressionControlPlugin.goMain = new GameObject(ExpressionControlPlugin.Name);
			ExpressionControlPlugin.inst = ExpressionControlPlugin.goMain.AddComponent<ExpressionControl>();
			ExpressionControlPlugin.hotkey = ExpressionControlPlugin.goMain.AddComponent<ShortCutKeyMgr>();
            UnityEngine.Object.DontDestroyOnLoad(ExpressionControlPlugin.goMain);
			this.harmony = Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
		}
		public void OnSceneLoad(Scene scene, LoadSceneMode mode)
		{
			if (ExpressionControlPlugin.inst != null && ExpressionControlPlugin.inst.enabled) ExpressionControlPlugin.inst.ReStart();
		}
		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= this.OnSceneLoad;
			if (this.harmony != null) this.harmony.UnpatchSelf();
			this.harmony = null;
			if (ExpressionControlPlugin.goMain != null) UnityEngine.Object.Destroy(ExpressionControlPlugin.goMain);
			ExpressionControlPlugin.goMain = null;
			ExpressionControlPlugin.inst = null;
			ExpressionControlPlugin.hotkey = null;
		}
		private Harmony harmony;
		private static GameObject goMain;
		internal static ExpressionControl inst;
		internal static ShortCutKeyMgr hotkey;
	}
}
