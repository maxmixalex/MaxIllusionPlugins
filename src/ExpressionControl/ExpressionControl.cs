using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Studio;
using UnityEngine;
namespace ExpressionControl
{
	[DefaultExecutionOrder(9999)]
	public partial class ExpressionControl : MonoBehaviour
	{
		public void Start()
		{
			ExpressionControl.hsceneProc = FindObjectOfType<HSceneProc>();
			ExpressionControl.hFrag = FindObjectOfType<HFlag>();
			ExpressionControl.studioScene = FindObjectOfType<StudioScene>();
			bool flag = ExpressionControl.studioScene != null;
			if (flag)
			{
				ExpressionControl.isStudio = true;
			}
			this.xml = new XmlMgr();
			try { this.xml.Load(); }
			catch (Exception ex)
			{
				// Keep a broken preset file available for repair and avoid running a
				// partially initialized component on every following frame.
				Debug.LogError("ExpressionControl: failed to load presets; fix the XML and reload the plugin. " + ex);
				this.enabled = false;
				return;
			}
			this.selectedScenePreset = this.xml.scenePresets.Count > 0 ? 0 : -1;
			this.hotkey = ExpressionControlPlugin.hotkey;
			this.hotkey.Set(ExpressionControl.hotkeyShowGUI, this.xml.keyConfig.showGUI);
			bool flag2 = this.xml.programs.Count == 0;
			if (flag2)
			{
				this.currentProgram = new ExpressionControl.Program();
				this.currentProgram.name = "- - -";
			}
			else
			{
				this.currentProgram = this.xml.programs[0].DeepClone<ExpressionControl.Program>();
			}
		}
		public void LateUpdate()
		{
			bool keyState = this.hotkey[ExpressionControl.hotkeyShowGUI].GetKeyState();
			if (keyState)
			{
				bool flag = this.gui == null;
				if (flag)
				{
					this.gui = new GUIMgr(ExpressionControl.WINDOW_ID, new Rect(0f, 0f, 20f, 50f), ExpressionControlPlugin.Name + ExpressionControlPlugin.Version);
				}
				this.gui.show = !this.gui.show;
				this.UpdateFemale();
			}
			this.Proc();
		}
		public void OnGUI()
		{
			bool flag = this.gui != null && this.gui.show;
			if (flag)
			{
				Color oldColor = GUI.color;
				Color oldContent = GUI.contentColor;
				Color oldBackground = GUI.backgroundColor;
				bool oldEnabled = GUI.enabled;
				try { this.gui.Draw(); }
				finally
				{
					GUI.color = oldColor;
					GUI.contentColor = oldContent;
					GUI.backgroundColor = oldBackground;
					GUI.enabled = oldEnabled;
				}
			}
		}
		public void ReStart()
		{
			ExpressionControl.studioScene = FindObjectOfType<StudioScene>();
			bool flag = ExpressionControl.studioScene == null;
			if (flag)
			{
				ExpressionControl.isStudio = false;
			}
			else
			{
				ExpressionControl.isStudio = true;
			}
			ExpressionControl.hsceneProc = FindObjectOfType<HSceneProc>();
			bool flag2 = ExpressionControl.hsceneProc == null;
			if (flag2)
			{
				bool flag3 = ExpressionControl.isHScene;
				if (flag3)
				{
					this.orgazumCount = 0;
					ExpressionControl.isHScene = false;
					bool flag4 = this.gui != null;
					if (flag4)
					{
						this.gui.show = false;
						this.gui.showProgram = false;
						this.guiStateMain = ExpressionControl.GUIState.normal;
						this.guiStateProgram = ExpressionControl.GUIState.normal;
					}
				}
			}
			else
			{
				bool flag5 = !ExpressionControl.isHScene;
				if (flag5)
				{
					ExpressionControl.isHScene = true;
					bool flag6 = this.gui != null;
					if (flag6)
					{
						this.gui.show = false;
						this.gui.showProgram = false;
						this.guiStateMain = ExpressionControl.GUIState.normal;
						this.guiStateProgram = ExpressionControl.GUIState.normal;
					}
				}
				this.UpdateFemale();
				ExpressionControl.hFrag = FindObjectOfType<HFlag>();
			}
		}
		public void Proc()
		{
			bool flag = ExpressionControl.hsceneProc == null;
			if (flag)
			{
				using (List<ExpressionControl.FemaleData>.Enumerator enumerator = this.femaleList.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ExpressionControl.FemaleData femaleData = enumerator.Current;
						bool flag2 = !(femaleData.female == null);
						if (flag2)
						{
							bool flag3 = !femaleData.programValid;
							if (flag3)
							{
								bool flag4 = femaleData.CheckExpression(true);
								if (flag4)
								{
									bool flag5 = !femaleData.lockEyeY;
									if (flag5)
									{
										femaleData.RestoreEyeY();
									}
									bool flag6 = !femaleData.lockEyeSmall;
									if (flag6)
									{
										femaleData.RestoreEyeSmall();
									}
								}
							}
							else
							{
								bool flag7 = femaleData.CheckExpression(false);
								bool flag8 = femaleData.programTimer < 20f && (femaleData.programTimerNow -= Time.deltaTime) <= 0f;
								if (flag8)
								{
									this.ProcAllProgram(femaleData, flag7, null);
								}
							}
							femaleData.ProcEyeSmall();
							femaleData.ProcEyeY();
						}
					}
					return;
				}
			}
			this.UpdateHState();
			foreach (ExpressionControl.FemaleData femaleData2 in this.femaleList)
			{
				bool flag9 = !(femaleData2.female == null);
				if (flag9)
				{
					bool programValid = femaleData2.programValid;
					if (programValid)
					{
						ExpressionControl.FemaleState state = femaleData2.state;
						femaleData2.UpdateState(this.updateState, this.nowMotion, this.isFeraAction);
						femaleData2.CheckExpression(false);
						bool flag10 = this.updateState && state != femaleData2.state;
						if (flag10)
						{
							this.ProcAllProgram(femaleData2, false, null);
						}
						bool flag16 = femaleData2.programTimer < 20f && (femaleData2.programTimerNow -= Time.deltaTime) <= 0f;
						if (flag16)
						{
							this.ProcAllProgram(femaleData2, false, null);
						}
						bool flag14 = !femaleData2.lockEyeY && !this.IsProgramControllingPart(femaleData2, ExpressionControl.ExpressionPart.eyeY);
						if (flag14)
						{
							femaleData2.RestoreEyeY();
						}
						bool flag15 = !femaleData2.lockEyeSmall && !this.IsProgramControllingPart(femaleData2, ExpressionControl.ExpressionPart.eyeSmall);
						if (flag15)
						{
							femaleData2.RestoreEyeSmall();
						}
					}
					else
					{
						bool flag11 = femaleData2.CheckExpression(true);
						if (flag11)
						{
							bool flag12 = !femaleData2.lockEyeY;
							if (flag12)
							{
								femaleData2.RestoreEyeY();
							}
							bool flag13 = !femaleData2.lockEyeSmall;
							if (flag13)
							{
								femaleData2.RestoreEyeSmall();
							}
						}
					}
					femaleData2.ProcEyeSmall();
					femaleData2.ProcEyeY();
				}
			}
			this.updateState = false;
		}

		public bool ShouldBlockExpressionPatternChange(ChaControl chara, ExpressionControl.PartType type, int ptn)
		{
			ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == chara);
			bool flag = femaleData == null || femaleData.expression == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				switch (type)
				{
				case ExpressionControl.PartType.eyebrow:
					result = (femaleData.lockExpression || femaleData.lockEyebrowPtn || this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph)) && femaleData.expression.eyebrowPtn != ptn;
					break;
				case ExpressionControl.PartType.eyes:
					result = (femaleData.lockExpression || femaleData.lockEyePtn || this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph)) && femaleData.expression.eyesPtn != ptn;
					break;
				case ExpressionControl.PartType.mouth:
					result = (femaleData.lockExpression || femaleData.lockMouthPtn || this.IsProgramControllingMouthPattern(femaleData)) && femaleData.expression.mouthPtn != ptn;
					break;
				default:
					result = false;
					break;
				}
			}
			return result;
		}
		public bool ShouldBlockExpressionOpenChange(ChaControl chara, ExpressionControl.PartType type, float open)
		{
			ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == chara);
			bool flag = femaleData == null || femaleData.expression == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				switch (type)
				{
				case ExpressionControl.PartType.eyes:
					result = (femaleData.lockExpression || femaleData.lockEyeOpen || this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph)) && !Utl.Equals(femaleData.expression.eyeOpen, open);
					break;
				case ExpressionControl.PartType.mouth:
					result = (femaleData.lockExpression || femaleData.lockMouthOpen || this.IsProgramControllingMouthOpen(femaleData)) && !Utl.Equals(femaleData.expression.mouthOpen, open);
					break;
				default:
					result = false;
					break;
				}
			}
			return result;
		}
		public bool ShouldBlockHighlightChange(ChaControl chara, bool hide)
		{
			ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == chara);
			return femaleData != null && femaleData.expression != null && (femaleData.lockExpression || femaleData.lockHighlight || this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.highlight)) && femaleData.expression.hideHighlight != hide;
		}
		public bool ShouldBlockBlinkChange(ChaControl chara, bool enable)
		{
			ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == chara);
			return femaleData != null && femaleData.expression != null && (femaleData.lockExpression || femaleData.lockBlink) && femaleData.expression.blink != enable;
		}
		public bool ShouldBlockHohoAkaChange(ChaControl chara, float rate)
		{
			ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == chara);
			return femaleData != null && femaleData.expression != null && (femaleData.lockExpression || femaleData.lockHohoAka || this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.hoho)) && !Utl.Equals(femaleData.expression.hohoAka, rate);
		}
		private bool IsProgramControllingMouthPattern(ExpressionControl.FemaleData fd)
		{
			return !fd.ignoreMouthLoad && this.IsProgramControllingPart(fd, ExpressionControl.ExpressionPart.morph);
		}
		private bool IsProgramControllingMouthOpen(ExpressionControl.FemaleData fd)
		{
			return !fd.ignoreMouthLoad && !fd.ignoreMouthOpen && this.IsProgramControllingPart(fd, ExpressionControl.ExpressionPart.morph);
		}
		private bool IsProgramControllingPart(ExpressionControl.FemaleData fd, ExpressionControl.ExpressionPart part)
		{
			bool flag = fd == null || fd.program == null || !fd.programValid || (fd.isKiss && fd.program.ignoreKiss);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				result = this.GetMatchedUnits(fd).Any((ExpressionControl.Program.Unit x) => this.UnitControlsPart(x, part) && this.UnitHasValidExpression(x));
			}
			return result;
		}
		private bool UnitControlsPart(ExpressionControl.Program.Unit unit, ExpressionControl.ExpressionPart part)
		{
			return ExpressionRules.ControlsPart(unit, part);
		}
		private bool UnitHasValidExpression(ExpressionControl.Program.Unit unit)
		{
			return unit != null && unit.expressionGuids != null && this.xml != null && this.xml.expressions != null && unit.expressionGuids.Any((string guid) => this.xml.expressions.Any((ExpressionControl.Expression x) => x.guid == guid));
		}
		private void UpdateHState()
		{
			bool flag = ExpressionControl.hsceneProc == null || this.updateState;
			if (!flag)
			{
				bool flag2 = this.nowMotion != ExpressionControl.hsceneProc.flags.nowAnimStateName;
				if (flag2)
				{
					this.nowMotion = ExpressionControl.hsceneProc.flags.nowAnimStateName;
					bool flag3 = this.nowMotion.Contains("Orgasm_Start") || this.nowMotion.Contains("S_IN_Start") || this.nowMotion.Contains("F_IN_Start") || (ExpressionControl.hFrag != null && (ExpressionControl.hFrag.mode == HFlag.EMode.masturbation || ExpressionControl.hFrag.mode == HFlag.EMode.lesbian) && this.nowMotion == "Orgasm");
					if (flag3)
					{
						int num = this.orgazumCount + 1;
						this.orgazumCount = num;
						bool flag4 = num > 10;
						if (flag4)
						{
							this.orgazumCount = 10;
						}
					}
					bool flag5 = this.nowHAction != ExpressionControl.hsceneProc.flags.nowAnimationInfo.nameAnimation;
					if (flag5)
					{
						this.nowHAction = ExpressionControl.hsceneProc.flags.nowAnimationInfo.nameAnimation;
						this.isFeraAction = (this.nowHAction.Contains("舐め") || this.nowHAction.Contains("フェラ") || this.nowHAction.Contains("咥え"));
						this.UpdateFemale();
					}
					this.updateState = true;
				}
			}
		}
		public bool IsIgnoreSetFace(int _idFace, ChaControl _chara, int _voiceKind, int _action, FaceListCtrl __instance, GlobalMethod.FloatBlend ___blendEye, GlobalMethod.FloatBlend ___blendMouth)
		{
			bool flag = __instance.facelib.Count <= _voiceKind || _voiceKind < 0;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				bool flag2 = __instance.facelib[_voiceKind].Count <= _action || _action < 0;
				if (flag2)
				{
					result = true;
				}
				else
				{
					bool flag3 = !__instance.facelib[_voiceKind][_action].ContainsKey(_idFace);
					if (flag3)
					{
						result = true;
					}
					else
					{
						ExpressionControl.FemaleData femaleData = this.femaleList.FirstOrDefault((ExpressionControl.FemaleData x) => x.female == _chara);
						bool flag4 = femaleData == null;
						if (flag4)
						{
							result = false;
						}
						else
						{
							bool lockExpression = femaleData.lockExpression;
							if (lockExpression)
							{
								result = true;
							}
							else
							{
								FaceListCtrl.FaceInfo info = __instance.facelib[_voiceKind][_action][_idFace];
								bool flag5 = ExpressionControl.hsceneProc == null;
								bool flag6;
								if (flag5)
								{
									flag6 = false;
									bool flag7 = !femaleData.lockEyeY;
									if (flag7)
									{
										femaleData.RestoreEyeY();
									}
									bool flag8 = !femaleData.lockEyeSmall;
									if (flag8)
									{
										femaleData.RestoreEyeSmall();
									}
								}
								else
								{
									this.UpdateHState();
									femaleData.UpdateState(this.updateState, this.nowMotion, this.isFeraAction);
									flag6 = true;
								}
								bool flag10 = !femaleData.lockEyebrowPtn && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph);
								if (flag10)
								{
									_chara.ChangeEyebrowPtn(info.eyebrow, true);
								}
								bool flag11 = !femaleData.lockEyePtn && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph);
								if (flag11)
								{
									_chara.ChangeEyesPtn(info.eye, true);
								}
								bool flag12 = !femaleData.lockEyeOpen && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.morph);
								if (flag12)
								{
									NullCheck.SafeProc<FBSCtrlEyes>(_chara.eyesCtrl, delegate(FBSCtrlEyes e)
									{
										___blendEye.Start(e.OpenMax, info.openMaxEye, 0.3f);
									});
								}
								bool flag13 = !femaleData.lockMouthPtn && (flag6 || !femaleData.ignoreMouthLoad) && !this.IsProgramControllingMouthPattern(femaleData);
								if (flag13)
								{
									_chara.ChangeMouthPtn(info.mouth, true);
								}
								bool flag14 = !femaleData.lockMouthOpen && (flag6 || (!femaleData.ignoreMouthOpen && !femaleData.ignoreMouthLoad)) && !this.IsProgramControllingMouthOpen(femaleData);
								if (flag14)
								{
									NullCheck.SafeProc<FBSCtrlMouth>(_chara.mouthCtrl, delegate(FBSCtrlMouth m)
									{
										___blendMouth.Start(m.OpenMin, info.openMinMouth, 0.3f);
									});
								}
								bool flag15 = !femaleData.lockTear && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.tear);
								if (flag15)
								{
									_chara.tearsLv = (byte)info.tears;
								}
								bool flag16 = !femaleData.lockHohoAka && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.hoho);
								if (flag16)
								{
									_chara.ChangeHohoAkaRate(info.cheek);
								}
								bool flag17 = !femaleData.lockHighlight && !this.IsProgramControllingPart(femaleData, ExpressionControl.ExpressionPart.highlight);
								if (flag17)
								{
									_chara.HideEyeHighlight(!info.highlight);
								}
								bool flag18 = !femaleData.lockBlink;
								if (flag18)
								{
									_chara.ChangeEyesBlinkFlag(info.eyesblink);
								}
								_chara.ChangeEyesShaking(info.yure);
								_chara.DisableShapeMouth(MathfEx.IsRange<int>(21, info.mouth, 22, true));
								result = true;
							}
						}
					}
				}
			}
			return result;
		}
		public bool ProcAllProgram(ExpressionControl.FemaleData fd, bool fix, FaceListCtrl.FaceInfo faceInfo = null)
		{
			this.SetProgramTimer(fd);
			return (!fd.isKiss || !fd.program.ignoreKiss) && this.ProcProgram(fd, fix, faceInfo);
		}
		private void SetProgramTimer(ExpressionControl.FemaleData fd)
		{
			fd.programTimerNow = UnityEngine.Random.Range(1f, fd.programTimer);
		}
		private bool ProcProgram(ExpressionControl.FemaleData fd, bool fix, FaceListCtrl.FaceInfo faceInfo = null)
		{
			List<ExpressionControl.Program.Unit> matchedUnits = this.GetMatchedUnits(fd);
			bool flag = matchedUnits.Count == 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				ExpressionControl.Expression expression = new ExpressionControl.Expression(fd.female, fd.lockEyeY ? fd.expression.eyeY : 0f, fd.lockEyeSmall ? fd.expression.eyeSmall : 0f);
				ExpressionControl.Expression expression2 = (faceInfo == null) ? null : new ExpressionControl.Expression(faceInfo);
				bool flag2 = false;
				string guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.morph);
				bool flag3 = string.IsNullOrEmpty(guid);
				ExpressionControl.Expression expression3;
				if (flag3)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag4 = expression3 != null;
				if (flag4)
				{
					bool flag5 = !fd.lockEyebrowPtn;
					if (flag5)
					{
						expression.eyebrowPtn = expression3.eyebrowPtn;
						flag2 = true;
					}
					bool flag6 = !fd.lockEyePtn;
					if (flag6)
					{
						expression.eyesPtn = expression3.eyesPtn;
						flag2 = true;
					}
					bool flag7 = !fd.lockMouthPtn && !fd.ignoreMouthLoad;
					if (flag7)
					{
						expression.mouthPtn = expression3.mouthPtn;
						flag2 = true;
					}
					bool flag8 = !fd.lockEyeOpen;
					if (flag8)
					{
						expression.eyeOpen = expression3.eyeOpen;
						flag2 = true;
					}
					bool flag9 = !fd.lockMouthOpen && !fd.ignoreMouthLoad && !fd.ignoreMouthOpen;
					if (flag9)
					{
						expression.mouthOpen = expression3.mouthOpen;
						flag2 = true;
					}
				}
				guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.tear);
				bool flag10 = string.IsNullOrEmpty(guid);
				if (flag10)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag11 = expression3 != null && !fd.lockTear;
				if (flag11)
				{
					expression.tearLv = expression3.tearLv;
					flag2 = true;
				}
				guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.hoho);
				bool flag12 = string.IsNullOrEmpty(guid);
				if (flag12)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag13 = expression3 != null && !fd.lockHohoAka;
				if (flag13)
				{
					expression.hohoAka = expression3.hohoAka;
					flag2 = true;
				}
				guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.eyeY);
				bool flag14 = string.IsNullOrEmpty(guid);
				if (flag14)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag15 = expression3 != null && !fd.lockEyeY;
				if (flag15)
				{
					expression.eyeY = expression3.eyeY;
					flag2 = true;
				}
				guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.eyeSmall);
				bool flag16 = string.IsNullOrEmpty(guid);
				if (flag16)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag17 = expression3 != null && !fd.lockEyeSmall;
				if (flag17)
				{
					expression.eyeSmall = expression3.eyeSmall;
					flag2 = true;
				}
				guid = this.GetRandomExpressionGuid(matchedUnits, ExpressionControl.ExpressionPart.highlight);
				bool flag18 = string.IsNullOrEmpty(guid);
				if (flag18)
				{
					expression3 = expression2;
				}
				else
				{
					expression3 = this.xml.expressions.FirstOrDefault((ExpressionControl.Expression x) => x.guid == guid);
				}
				bool flag19 = expression3 != null && !fd.lockHighlight;
				if (flag19)
				{
					expression.hideHighlight = expression3.hideHighlight;
					flag2 = true;
				}
				bool flag20 = flag2 || (!fd.lockEyeY && !Utl.Equals(fd.expression.eyeY, expression.eyeY)) || (!fd.lockEyeSmall && !Utl.Equals(fd.expression.eyeSmall, expression.eyeSmall));
				if (flag20)
				{
					fd.expressionPrev = fd.expression;
					fd.expression = expression;
					fd.ApplyExpression(expression.eyeY, expression.eyeSmall, fix);
					result = true;
				}
				else
				{
					result = false;
				}
			}
			return result;
		}
		private string GetRandomExpressionGuid(List<ExpressionControl.Program.Unit> units, ExpressionControl.ExpressionPart part)
		{
			return ExpressionRules.ChooseExpressionGuid(units, part, this.xml == null ? null : this.xml.expressions,
				count => UnityEngine.Random.Range(0, count));
		}
		private bool UnitMatchesMode(ExpressionControl.Program.Unit unit, ExpressionControl.FemaleData fd)
		{
			bool flag = unit == null || ExpressionControl.hFrag == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				HFlag.EMode mode = ExpressionControl.hFrag.mode;
				bool flag2 = mode == HFlag.EMode.aibu;
				if (flag2)
				{
					result = unit.aibu;
				}
				else
				{
					bool flag3 = mode == HFlag.EMode.houshi;
					if (flag3)
					{
						result = ((fd != null && fd.isFeraMode) ? unit.fera : unit.houshi);
					}
					else
					{
						bool flag4 = mode == HFlag.EMode.sonyu;
						if (flag4)
						{
							result = unit.sounyuu;
						}
						else
						{
							bool flag5 = mode == HFlag.EMode.masturbation;
							if (flag5)
							{
								result = unit.masturbation;
							}
							else
							{
								bool flag6 = mode == HFlag.EMode.lesbian;
								if (flag6)
								{
									result = unit.lesbian;
								}
								else
								{
									result = true;
								}
							}
						}
					}
				}
			}
			return result;
		}
		private List<ExpressionControl.Program.Unit> GetMatchedUnits(ExpressionControl.FemaleData fd)
		{
			List<ExpressionControl.Program.Unit> matched = new List<ExpressionControl.Program.Unit>();
			if (fd == null || fd.program == null || fd.program.units == null) return matched;
			foreach (ExpressionControl.Program.Unit unit in fd.program.units)
			{
				if (unit == null) continue;
				if (ExpressionControl.hFrag == null)
				{
					if (unit.idle) matched.Add(unit);
					continue;
				}
				if (!this.UnitMatchesMode(unit, fd) || !ExpressionRules.MatchesState(unit, fd.state)) continue;
				if (this.orgazumCount >= unit.orgazumNumL && this.orgazumCount <= unit.orgazumNumH &&
					ExpressionControl.hFrag.gaugeFemale >= unit.feelingL && ExpressionControl.hFrag.gaugeFemale <= unit.feelingH)
				{
					matched.Add(unit);
				}
			}
			return matched;
		}
		public void UpdateFemale()
		{
			List<ExpressionControl.FemaleData> list = new List<ExpressionControl.FemaleData>();
			bool flag = ExpressionControl.isStudio;
			if (flag)
			{
				List<OCIChar> ocicharFemaleAll = Utl.GetOCICharFemaleAll();
				for (int i = 0; i < ocicharFemaleAll.Count; i++)
				{
					bool flag2 = false;
					for (int j = 0; j < this.femaleList.Count; j++)
					{
						if (ocicharFemaleAll[i] is OCICharFemale ociF) {
							bool flag3 = ociF.female == this.femaleList[j].female;
							if (flag3) {
								list.Add(this.femaleList[j]);
								flag2 = true;
								break;
							}
						}
                        if (ocicharFemaleAll[i] is OCICharMale ociM) {
                            bool flag3 = ociM.male == this.femaleList[j].female;
                            if (flag3) {
                                list.Add(this.femaleList[j]);
                                flag2 = true;
                                break;
                            }
                        }
                    }
					bool flag4 = !flag2;
					if (flag4)
					{
                        if (ocicharFemaleAll[i] is OCICharFemale ociF) {
                            list.Add(new ExpressionControl.FemaleData(ociF, this));
                        }
                        if (ocicharFemaleAll[i] is OCICharMale ociM) {
                            list.Add(new ExpressionControl.FemaleData(ociM, this));
                        }
                    }
				}
				this.femaleList = list;
				bool flag5 = this.currentFemaleNum >= this.femaleList.Count;
				if (flag5)
				{
					this.currentFemaleNum = 0;
				}
			}
			else
			{
				List<ChaControl> femaleAll = Utl.GetFemaleAll();
				for (int k = 0; k < femaleAll.Count; k++)
				{
					bool flag6 = false;
					for (int l = 0; l < this.femaleList.Count; l++)
					{
						bool flag7 = femaleAll[k] == this.femaleList[l].female;
						if (flag7)
						{
							list.Add(this.femaleList[l]);
							flag6 = true;
							break;
						}
					}
					bool flag8 = !flag6;
					if (flag8)
					{
						list.Add(new ExpressionControl.FemaleData(femaleAll[k], this, ExpressionControl.hsceneProc));
					}
				}
				this.femaleList = list;
				bool flag9 = this.currentFemaleNum >= this.femaleList.Count;
				if (flag9)
				{
					this.currentFemaleNum = 0;
				}
			}
		}
		public ChaControl currentFemale
		{
			get
			{
				bool flag = this.femaleList.Count == 0;
				ChaControl result;
				if (flag)
				{
					result = null;
				}
				else
				{
					result = this.femaleList[this.currentFemaleNum].female;
				}
				return result;
			}
		}
		public ExpressionControl.FemaleData currentFemaleData
		{
			get
			{
				bool flag = this.femaleList.Count == 0;
				ExpressionControl.FemaleData result;
				if (flag)
				{
					result = null;
				}
				else
				{
					result = this.femaleList[this.currentFemaleNum];
				}
				return result;
			}
		}
		private void PrevFemale()
		{
			int num = this.currentFemaleNum - 1;
			this.currentFemaleNum = num;
			bool flag = num < 0;
			if (flag)
			{
				this.currentFemaleNum = this.femaleList.Count - 1;
			}
		}
		private void NextFemale()
		{
			int num = this.currentFemaleNum + 1;
			this.currentFemaleNum = num;
			bool flag = num >= this.femaleList.Count;
			if (flag)
			{
				this.currentFemaleNum = 0;
			}
		}
		private void PrevProgram()
		{
			bool flag = this.xml.programs.Count == 0;
			if (!flag)
			{
				int num = this.currentProgramNum - 1;
				this.currentProgramNum = num;
				bool flag2 = num < 0;
				if (flag2)
				{
					this.currentProgramNum = this.xml.programs.Count - 1;
				}
				this.currentProgram = this.xml.programs[this.currentProgramNum].DeepClone<ExpressionControl.Program>();
				this.selectedProgramUnit = 0;
			}
		}
		private void NextProgram()
		{
			bool flag = this.xml.programs.Count == 0;
			if (!flag)
			{
				int num = this.currentProgramNum + 1;
				this.currentProgramNum = num;
				bool flag2 = num >= this.xml.programs.Count;
				if (flag2)
				{
					this.currentProgramNum = 0;
				}
				this.currentProgram = this.xml.programs[this.currentProgramNum].DeepClone<ExpressionControl.Program>();
				this.selectedProgramUnit = 0;
			}
		}
		private void SelectProgram(int number)
		{
			bool flag = this.xml.programs.Count == 0;
			if (!flag)
			{
				bool flag2 = number < 0 || number >= this.xml.programs.Count;
				if (flag2)
				{
					number = 0;
				}
				this.currentProgramNum = number;
				this.currentProgram = this.xml.programs[this.currentProgramNum].DeepClone<ExpressionControl.Program>();
				this.selectedProgramUnit = 0;
			}
		}
		private void AddUnit()
		{
			this.currentProgram.units.Add(new ExpressionControl.Program.Unit());
			this.selectedProgramUnit = this.currentProgram.units.Count - 1;
		}
		private void CopyUnit()
		{
			bool flag = this.currentProgram.units.Count == 0;
			if (!flag)
			{
				this.currentProgram.units.Add(this.currentProgram.units[this.selectedProgramUnit].DeepClone<ExpressionControl.Program.Unit>());
				this.selectedProgramUnit = this.currentProgram.units.Count - 1;
			}
		}
		private void CopyUnit(bool addBelow)
		{
			bool flag = this.currentProgram.units.Count == 0;
			if (!flag)
			{
				if (addBelow)
				{
					this.currentProgram.units.Insert(this.selectedProgramUnit + 1, this.currentProgram.units[this.selectedProgramUnit].DeepClone<ExpressionControl.Program.Unit>());
					this.selectedProgramUnit++;
				}
				else
				{
					this.currentProgram.units.Add(this.currentProgram.units[this.selectedProgramUnit].DeepClone<ExpressionControl.Program.Unit>());
					this.selectedProgramUnit = this.currentProgram.units.Count - 1;
				}
			}
		}
		private void DeleteUnit()
		{
			bool flag = this.currentProgram.units.Count == 0;
			if (!flag)
			{
				this.currentProgram.units.RemoveAt(this.selectedProgramUnit);
				int num = this.selectedProgramUnit - 1;
				this.selectedProgramUnit = num;
				bool flag2 = num < 0;
				if (flag2)
				{
					this.selectedProgramUnit = 0;
				}
			}
		}
		private void MoveUnit(bool up)
		{
			this.MoveUnit(this.selectedProgramUnit, up);
		}
		private void MoveUnit(int number, bool up)
		{
			if (up)
			{
				bool flag = number == 0;
				if (!flag)
				{
					ExpressionControl.Program.Unit value = this.currentProgram.units[number - 1];
					this.currentProgram.units[number - 1] = this.currentProgram.units[number];
					this.currentProgram.units[number] = value;
					bool flag2 = number == this.selectedProgramUnit;
					if (flag2)
					{
						this.selectedProgramUnit--;
					}
					else
					{
						bool flag3 = number - 1 == this.selectedProgramUnit;
						if (flag3)
						{
							this.selectedProgramUnit++;
						}
					}
				}
			}
			else
			{
				bool flag4 = number >= this.currentProgram.units.Count - 1;
				if (!flag4)
				{
					ExpressionControl.Program.Unit value2 = this.currentProgram.units[number + 1];
					this.currentProgram.units[number + 1] = this.currentProgram.units[number];
					this.currentProgram.units[number] = value2;
					bool flag5 = number == this.selectedProgramUnit;
					if (flag5)
					{
						this.selectedProgramUnit++;
					}
					else
					{
						bool flag6 = number + 1 == this.selectedProgramUnit;
						if (flag6)
						{
							this.selectedProgramUnit--;
						}
					}
				}
			}
		}
		private void MoveExpression(int number, bool up)
		{
			this.isSortExpressionModify = true;
			if (up)
			{
				bool flag = number == 0;
				if (!flag)
				{
					ExpressionControl.Expression value = this.xml.expressions[number - 1];
					this.xml.expressions[number - 1] = this.xml.expressions[number];
					this.xml.expressions[number] = value;
				}
			}
			else
			{
				bool flag2 = number >= this.xml.expressions.Count - 1;
				if (!flag2)
				{
					ExpressionControl.Expression value2 = this.xml.expressions[number + 1];
					this.xml.expressions[number + 1] = this.xml.expressions[number];
					this.xml.expressions[number] = value2;
				}
			}
		}
		private bool XML_SaveNewExpression(string name)
		{
			bool flag = string.IsNullOrEmpty(name) || this.currentFemaleData == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				ExpressionControl.Expression expression = new ExpressionControl.Expression(Guid.NewGuid().ToString("N"), name, this.currentFemaleData.expression);
				this.xml.expressions.Add(expression);
				this.xml.Save(true);
				this.currentFemaleData.SetExpression(expression);
				result = true;
			}
			return result;
		}
		private bool XML_UpdateExpression(string guid, string name)
		{
			bool flag = string.IsNullOrEmpty(name);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = string.IsNullOrEmpty(guid);
				if (flag2)
				{
					result = this.XML_SaveNewExpression(name);
				}
				else
				{
					bool flag3 = this.currentFemaleData == null;
					if (flag3)
					{
						result = false;
					}
					else
					{
						bool flag4 = false;
						for (int i = 0; i < this.xml.expressions.Count; i++)
						{
							bool flag5 = this.xml.expressions[i].guid == guid;
							if (flag5)
							{
								flag4 = true;
								ExpressionControl.Expression expression = new ExpressionControl.Expression(this.xml.expressions[i].guid, name, this.currentFemaleData.expression);
								this.xml.expressions[i] = expression;
								this.currentFemaleData.SetExpression(expression);
								break;
							}
						}
						bool flag6 = !flag4;
						if (flag6)
						{
							result = this.XML_SaveNewExpression(name);
						}
						else
						{
							this.xml.Save(true);
							result = true;
						}
					}
				}
			}
			return result;
		}
		private bool XML_DeleteExpression(string guid)
		{
			bool flag = string.IsNullOrEmpty(guid);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = false;
				for (int i = this.xml.expressions.Count - 1; i >= 0; i--)
				{
					bool flag3 = this.xml.expressions[i].guid == guid;
					if (flag3)
					{
						this.xml.expressions.RemoveAt(i);
						this.currentFemaleData.expression.guid = string.Empty;
						this.currentFemaleData.expression.name = "- - -";
						flag2 = true;
						break;
					}
				}
				bool flag4 = !flag2;
				if (flag4)
				{
					result = false;
				}
				else
				{
					foreach (ExpressionControl.Program program in this.xml.programs)
					{
						foreach (ExpressionControl.Program.Unit unit in program.units)
						{
							bool flag5 = unit.expressionGuids.Contains(guid);
							if (flag5)
							{
								unit.expressionGuids.Remove(guid);
								break;
							}
						}
					}
					this.xml.Save(true);
					result = true;
				}
			}
			return result;
		}
		private bool XML_SaveNewProgram(string name)
		{
			bool flag = string.IsNullOrEmpty(name);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				this.currentProgram.guid = Guid.NewGuid().ToString("N");
				this.currentProgram.name = name;
				ExpressionControl.Program item = this.currentProgram.DeepClone<ExpressionControl.Program>();
				this.xml.programs.Add(item);
				this.xml.Save(true);
				result = true;
			}
			return result;
		}
		private bool XML_UpdateProgram(string name)
		{
			bool flag = string.IsNullOrEmpty(name);
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				bool flag2 = false;
				for (int i = 0; i < this.xml.programs.Count; i++)
				{
					bool flag3 = this.xml.programs[i].guid == this.currentProgram.guid;
					if (flag3)
					{
						flag2 = true;
						this.currentProgram.name = name;
						this.xml.programs[i].name = this.currentProgram.name;
						this.xml.programs[i].guid = this.currentProgram.guid;
						this.xml.programs[i].applyMouthOpen = this.currentProgram.applyMouthOpen;
						this.xml.programs[i].ignoreMouthInFera = this.currentProgram.ignoreMouthInFera;
						this.xml.programs[i].ignoreKiss = this.currentProgram.ignoreKiss;
						this.xml.programs[i].units = this.currentProgram.units.DeepClone<List<ExpressionControl.Program.Unit>>();
						break;
					}
				}
				bool flag4 = !flag2;
				if (flag4)
				{
					result = this.XML_SaveNewProgram(name);
				}
				else
				{
					this.xml.Save(true);
					result = true;
				}
			}
			return result;
		}
		private string GetSceneRole(int index)
		{
			if (index < 0 || index >= this.femaleList.Count || this.femaleList[index].female == null)
			{
				return string.Empty;
			}
			int sex = this.femaleList[index].female.chaFile.parameter.sex;
			int ordinal = 1;
			for (int i = 0; i < index; i++)
			{
				if (this.femaleList[i].female != null && this.femaleList[i].female.chaFile.parameter.sex == sex)
				{
					ordinal++;
				}
			}
			return (sex == 0 ? "M" : "F") + ordinal.ToString();
		}
		private ExpressionControl.FemaleData FindSceneFemale(string role)
		{
			for (int i = 0; i < this.femaleList.Count; i++)
			{
				if (this.GetSceneRole(i) == role)
				{
					return this.femaleList[i];
				}
			}
			return null;
		}
		private ExpressionControl.ScenePreset CurrentScenePreset()
		{
			return this.selectedScenePreset >= 0 && this.selectedScenePreset < this.xml.scenePresets.Count ? this.xml.scenePresets[this.selectedScenePreset] : null;
		}
		private void SelectScenePreset(int index)
		{
			if (this.xml.scenePresets.Count == 0)
			{
				this.selectedScenePreset = -1;
				return;
			}
			if (index < 0) index = this.xml.scenePresets.Count - 1;
			if (index >= this.xml.scenePresets.Count) index = 0;
			this.selectedScenePreset = index;
		}
		private void SaveSceneRoles(ExpressionControl.ScenePreset preset)
		{
			preset.roles.Clear();
			for (int i = 0; i < this.femaleList.Count; i++)
			{
				string role = this.GetSceneRole(i);
				ExpressionControl.FemaleData femaleData = this.femaleList[i];
				preset.roles.Add(new ExpressionControl.SceneRolePreset
				{
					role = role,
					displayName = femaleData.fullName,
					programGuid = femaleData.program == null ? string.Empty : femaleData.program.guid,
					programTimer = Mathf.Clamp(femaleData.programTimer, 1f, 20f)
				});
			}
			this.xml.Save(true);
		}
		private bool XML_SaveNewScenePreset(string name)
		{
			name = name == null ? string.Empty : name.Trim();
			if (name.Length == 0 || this.xml.scenePresets.Any((ExpressionControl.ScenePreset x) => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}
			ExpressionControl.ScenePreset preset = new ExpressionControl.ScenePreset { guid = Guid.NewGuid().ToString("N"), name = name };
			this.xml.scenePresets.Add(preset);
			this.selectedScenePreset = this.xml.scenePresets.Count - 1;
			this.SaveSceneRoles(preset);
			return true;
		}
		private bool XML_UpdateScenePreset(string name)
		{
			ExpressionControl.ScenePreset preset = this.CurrentScenePreset();
			name = name == null ? string.Empty : name.Trim();
			if (preset == null || name.Length == 0 || this.xml.scenePresets.Any((ExpressionControl.ScenePreset x) => x != preset && string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}
			preset.name = name;
			this.SaveSceneRoles(preset);
			return true;
		}
		private bool XML_DeleteScenePreset()
		{
			ExpressionControl.ScenePreset preset = this.CurrentScenePreset();
			if (preset == null) return false;
			this.xml.scenePresets.Remove(preset);
			this.xml.Save(true);
			this.SelectScenePreset(Mathf.Min(this.selectedScenePreset, this.xml.scenePresets.Count - 1));
			return true;
		}
		private void ApplyCurrentScenePreset()
		{
			ExpressionControl.ScenePreset preset = this.CurrentScenePreset();
			if (preset == null) return;
			foreach (ExpressionControl.SceneRolePreset role in preset.roles)
			{
				ExpressionControl.FemaleData femaleData = this.FindSceneFemale(role.role);
				ExpressionControl.Program program = this.xml.programs.FirstOrDefault((ExpressionControl.Program x) => x.guid == role.programGuid);
				if (femaleData == null || program == null) continue;
				bool wasValid = femaleData.programValid;
				femaleData.SetProgram(program);
				femaleData.programTimer = Mathf.Clamp(role.programTimer, 1f, 20f);
				this.SetProgramTimer(femaleData);
				if (wasValid != femaleData.programValid)
				{
					femaleData.UpdateState(false, this.nowMotion, this.isFeraAction);
				}
			}
		}
		public void GUIFuncSave(int winID)
		{
			GUI.contentColor = Color.black;
			Rect rect = new Rect((float)(this.gui.fontSize / 2), (float)(this.gui.fontSize * 2), this.gui.rectSave.width - (float)this.gui.fontSize, this.gui.rectSave.height - (float)(this.gui.fontSize * 3));
			Rect rect2 = new Rect(rect.x, rect.y, rect.width, this.gui.itemHeight);
			bool sceneWindow = this.guiStateScene > ExpressionControl.GUIState.normal;
			bool mainWindow = !sceneWindow && this.guiStateMain > ExpressionControl.GUIState.normal;
			ExpressionControl.GUIState guistate = sceneWindow ? this.guiStateScene : (mainWindow ? this.guiStateMain : this.guiStateProgram);
			bool flag = guistate == ExpressionControl.GUIState.saveNew;
			if (flag)
			{
				Action action = delegate()
				{
					bool flag14 = string.IsNullOrEmpty(this.gui.sTmp);
					if (!flag14)
					{
						bool saved;
						if (sceneWindow)
						{
							saved = this.XML_SaveNewScenePreset(this.gui.sTmp);
						}
						else if (mainWindow)
						{
							saved = this.XML_SaveNewExpression(this.gui.sTmp);
						}
						else
						{
							saved = this.XML_SaveNewProgram(this.gui.sTmp);
						}
						if (saved) this.CloseSaveWindow();
					}
				};
				GUI.Label(rect2, sceneWindow ? "请输入场景预设名" : (mainWindow ? "请输入表情名" : "请输入程序名"), this.gui.gsLabel);
				rect2.y += this.gui.itemHeight;
				GUI.SetNextControlName("input");
				this.gui.sTmp = GUIMgr.TextField(rect2, this.gui.sTmp, this.gui.gsTextField);
				bool flag2 = this.setInputFocus;
				if (flag2)
				{
					GUI.FocusControl("input");
					this.setInputFocus = false;
				}
				bool flag3 = Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "input";
				if (flag3)
				{
					action();
				}
				rect2.y += this.gui.itemHeightWithMargin;
				rect2.width /= 2f;
				bool flag4 = GUIMgr.Button(rect2, "确定", this.gui.gsButton);
				if (flag4)
				{
					action();
				}
				rect2.x += rect2.width;
				bool flag5 = GUIMgr.Button(rect2, "取消", this.gui.gsButton);
				if (flag5)
				{
					this.CloseSaveWindow();
				}
			}
			else
			{
				bool flag6 = guistate == ExpressionControl.GUIState.update;
				if (flag6)
				{
					Action action2 = delegate()
					{
						bool flag14 = string.IsNullOrEmpty(this.gui.sTmp);
						if (!flag14)
						{
							bool saved;
							if (sceneWindow)
							{
								saved = this.XML_UpdateScenePreset(this.gui.sTmp);
							}
							else if (mainWindow)
							{
								saved = this.XML_UpdateExpression(this.currentFemaleData.expression.guid, this.gui.sTmp);
							}
							else
							{
								saved = this.XML_UpdateProgram(this.gui.sTmp);
							}
							if (saved) this.CloseSaveWindow();
						}
					};
					GUI.Label(rect2, "要覆盖（并重命名）吗？", this.gui.gsLabel);
					rect2.y += this.gui.itemHeight;
					GUI.SetNextControlName("input");
					this.gui.sTmp = GUIMgr.TextField(rect2, this.gui.sTmp, this.gui.gsTextField);
					bool flag7 = this.setInputFocus;
					if (flag7)
					{
						GUI.FocusControl("input");
						this.setInputFocus = false;
					}
					bool flag8 = Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "input";
					if (flag8)
					{
						action2();
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.width /= 2f;
					bool flag9 = GUIMgr.Button(rect2, "确定", this.gui.gsButton);
					if (flag9)
					{
						action2();
					}
					rect2.x += rect2.width;
					bool flag10 = GUIMgr.Button(rect2, "取消", this.gui.gsButton);
					if (flag10)
					{
						this.CloseSaveWindow();
					}
				}
				else
				{
					bool flag11 = guistate == ExpressionControl.GUIState.delete;
					if (flag11)
					{
						GUI.Label(rect2, "确定要删除吗？", this.gui.gsLabel);
						rect2.y += this.gui.itemHeight;
						GUI.Label(rect2, this.gui.sTmp, this.gui.gsLabel);
						rect2.y += this.gui.itemHeightWithMargin;
						rect2.width /= 2f;
						bool flag12 = GUIMgr.Button(rect2, "确定", this.gui.gsButton);
						if (flag12)
						{
							if (sceneWindow)
							{
								this.XML_DeleteScenePreset();
							}
							else if (mainWindow)
							{
								this.XML_DeleteExpression(this.currentFemaleData.expression.guid);
							}
							else
							{
								this.XML_DeleteProgram();
							}
							this.CloseSaveWindow();
						}
						rect2.x += rect2.width;
						bool flag13 = GUIMgr.Button(rect2, "取消", this.gui.gsButton);
						if (flag13)
						{
							this.CloseSaveWindow();
						}
					}
				}
			}
			GUI.DragWindow();
		}
		public void GUIFunc(int winID)
		{
			GUI.contentColor = Color.black;
			Rect rect = new Rect((float)(this.gui.fontSize / 2), (float)(this.gui.fontSize * 2), this.gui.rectMainWin.width - (float)this.gui.fontSize, this.gui.rectMainWin.height - (float)(this.gui.fontSize * 3));
			Rect rect2 = new Rect(this.gui.rectMainWin.width - (float)this.gui.gsWindow.fontSize * 1.4f, 0f, (float)this.gui.gsWindow.fontSize * 1.4f, (float)this.gui.gsWindow.fontSize * 1.4f);
			GUI.enabled = !this.gui.showSave;
			bool flag = GUIMgr.Button(rect2, "×", this.gui.gsButton);
			if (flag)
			{
				this.gui.show = false;
			}
			rect2.x -= rect2.width;
			bool flag2 = GUIMgr.Button(rect2, "-", this.gui.gsButton);
			if (flag2)
			{
				this.gui.mainWinFold = !this.gui.mainWinFold;
				this.gui.rectMainWin.height = (this.gui.mainWinFold ? 20f : this.gui.mainWinHeight);
			}
			bool mainWinFold = this.gui.mainWinFold;
			if (mainWinFold)
			{
				GUI.DragWindow();
			}
			else
			{
				bool flag3 = this.currentFemaleData == null;
				if (flag3)
				{
					GUI.Label(new Rect(rect.x, rect.y, rect.width, this.gui.itemHeight), "没有角色", this.gui.gsLabel);
					bool flag4 = GUIMgr.Button(new Rect(rect.x, rect.y + this.gui.itemHeightWithMargin, rect.width, this.gui.itemHeight), "编辑表情程序", this.gui.gsButton);
					if (flag4)
					{
						this.gui.showProgram = !this.gui.showProgram;
						bool showProgram = this.gui.showProgram;
						if (showProgram)
						{
							this.gui.rectProgram = GUIUtl.MoveScreenCenter(this.gui.rectProgram);
						}
					}
					GUI.DragWindow();
				}
				else
				{
					rect2.Set(rect.x, rect.y, (float)(this.gui.fontSize * 2), this.gui.itemHeight);
					bool flag5 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag5)
					{
						this.PrevFemale();
					}
					rect2.x += rect2.width;
					bool flag6 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag6)
					{
						this.NextFemale();
					}
					rect2.x += rect2.width + (float)this.gui.fontSize;
					rect2.width = rect.width - rect2.width * 2f + (float)this.gui.fontSize;
					Rect rect3 = rect2;
					ExpressionControl.FemaleData currentFemaleData = this.currentFemaleData;
					GUI.Label(rect3, ((currentFemaleData != null) ? currentFemaleData.fullName : null) ?? string.Empty, this.gui.gsLabel);
					rect2.y = this.gui.DrawLine(new Vector2(rect.x, rect2.y + this.gui.itemHeightWithMargin), rect.width);
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 10);
					bool flag7 = GUIMgr.Button(rect2, "获取当前表情", this.gui.gsButton);
					if (flag7)
					{
						this.currentFemaleData.Update();
					}
					rect2.x += rect2.width + (float)this.gui.fontSize;
					rect2.width = (float)(this.gui.fontSize * 6);
					this.currentFemaleData.lockExpression = GUI.Toggle(rect2, this.currentFemaleData.lockExpression, "固定表情", this.gui.gsToggle);
					rect2.x = rect.x;
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "眉", this.gui.gsLabel);
					rect2.x += rect2.width;
					bool flag8 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag8)
					{
						this.currentFemaleData.PrevPart(ExpressionControl.PartType.eyebrow);
					}
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 7);
					bool flag9 = GUIMgr.Button(rect2, this.eyeBrowPtnNameDict.ContainsKey(this.currentFemaleData.expression.eyebrowPtn) ? this.eyeBrowPtnNameDict[this.currentFemaleData.expression.eyebrowPtn] : this.currentFemaleData.expression.eyebrowPtn.ToString(), this.gui.gsButton);
					if (flag9)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, Enumerable.Range(0, this.currentFemaleData.maxEyebrowPtn).Select(delegate(int x)
						{
							bool flag48 = !this.eyeBrowPtnNameDict.ContainsKey(x);
							string result;
							if (flag48)
							{
								result = x.ToString();
							}
							else
							{
								result = this.eyeBrowPtnNameDict[x];
							}
							return result;
						}).ToArray<string>(), delegate(int x)
						{
							this.currentFemaleData.SetPart(ExpressionControl.PartType.eyebrow, x);
						});
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag10 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag10)
					{
						this.currentFemaleData.NextPart(ExpressionControl.PartType.eyebrow);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockEyebrowPtn = GUI.Toggle(rect2, this.currentFemaleData.lockEyebrowPtn, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "眼", this.gui.gsLabel);
					rect2.x += rect2.width;
					bool flag11 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag11)
					{
						this.currentFemaleData.PrevPart(ExpressionControl.PartType.eyes);
					}
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 7);
					bool flag12 = GUIMgr.Button(rect2, this.eyesPtnNameDict.ContainsKey(this.currentFemaleData.expression.eyesPtn) ? this.eyesPtnNameDict[this.currentFemaleData.expression.eyesPtn] : this.currentFemaleData.expression.eyesPtn.ToString(), this.gui.gsButton);
					if (flag12)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, Enumerable.Range(0, this.currentFemaleData.maxEyesPtn).Select(delegate(int x)
						{
							bool flag48 = !this.eyesPtnNameDict.ContainsKey(x);
							string result;
							if (flag48)
							{
								result = x.ToString();
							}
							else
							{
								result = this.eyesPtnNameDict[x];
							}
							return result;
						}).ToArray<string>(), delegate(int x)
						{
							this.currentFemaleData.SetPart(ExpressionControl.PartType.eyes, x);
						});
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag13 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag13)
					{
						this.currentFemaleData.NextPart(ExpressionControl.PartType.eyes);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockEyePtn = GUI.Toggle(rect2, this.currentFemaleData.lockEyePtn, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "开合", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					float num = GUI.HorizontalSlider(rect2, this.currentFemaleData.expression.eyeOpen, 0f, 1f);
					bool flag14 = num != this.currentFemaleData.expression.eyeOpen;
					if (flag14)
					{
						this.currentFemaleData.ChangeOpen(ExpressionControl.PartType.eyes, num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockEyeOpen = GUI.Toggle(rect2, this.currentFemaleData.lockEyeOpen, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "瞳孔↑", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.expression.eyeY, 0f, 0.5f);
					bool flag15 = num != this.currentFemaleData.expression.eyeY;
					if (flag15)
					{
						this.currentFemaleData.ChangeEyeY(num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockEyeY = GUI.Toggle(rect2, this.currentFemaleData.lockEyeY, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "缩瞳", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.expression.eyeSmall, 0f, 1f);
					bool flag16 = num != this.currentFemaleData.expression.eyeSmall;
					if (flag16)
					{
						this.currentFemaleData.ChangeEyeSmall(num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockEyeSmall = GUI.Toggle(rect2, this.currentFemaleData.lockEyeSmall, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeight;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 9);
					bool flag17 = GUI.Toggle(rect2, this.currentFemaleData.expression.hideHighlight, "隐藏高光", this.gui.gsToggle);
					bool flag18 = flag17 != this.currentFemaleData.expression.hideHighlight;
					if (flag18)
					{
						this.currentFemaleData.ChangeHighlight(flag17);
					}
					rect2.x = rect.x + rect.width - (float)this.gui.fontSize;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockHighlight = GUI.Toggle(rect2, this.currentFemaleData.lockHighlight, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeight;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 8);
					flag17 = GUI.Toggle(rect2, !this.currentFemaleData.expression.blink, "不眨眼", this.gui.gsToggle);
					bool flag19 = flag17 != !this.currentFemaleData.expression.blink;
					if (flag19)
					{
						this.currentFemaleData.ChangeBlink(!flag17);
					}
					rect2.x = rect.x + rect.width - (float)this.gui.fontSize;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockBlink = GUI.Toggle(rect2, this.currentFemaleData.lockBlink, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 4);
					GUI.Label(rect2, "眨眼频率", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 6);
					num = GUI.HorizontalSlider(rect2, (float)this.currentFemaleData.female.fbsCtrl.BlinkCtrl.BlinkFrequency, 0f, 100f);
					bool flag20 = num != (float)this.currentFemaleData.female.fbsCtrl.BlinkCtrl.BlinkFrequency;
					if (flag20)
					{
						this.currentFemaleData.ChangeBlinkRate(num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag21 = GUI.Button(rect2, "|", this.gui.gsButton);
					if (flag21)
					{
						this.currentFemaleData.ChangeBlinkRate(30f);
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 4);
					GUI.Label(rect2, "眨眼速度", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 6);
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.female.fbsCtrl.BlinkCtrl.BaseSpeed, 0f, 0.3f);
					bool flag22 = num != this.currentFemaleData.female.fbsCtrl.BlinkCtrl.BaseSpeed;
					if (flag22)
					{
						this.currentFemaleData.ChangeBlinkSpeed(num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag23 = GUI.Button(rect2, "|", this.gui.gsButton);
					if (flag23)
					{
						this.currentFemaleData.ChangeBlinkSpeed(0.15f);
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "口", this.gui.gsLabel);
					rect2.x += rect2.width;
					bool flag24 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag24)
					{
						this.currentFemaleData.PrevPart(ExpressionControl.PartType.mouth);
					}
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 7);
					bool flag25 = GUIMgr.Button(rect2, this.mouthPtnNameDict.ContainsKey(this.currentFemaleData.expression.mouthPtn) ? this.mouthPtnNameDict[this.currentFemaleData.expression.mouthPtn] : this.currentFemaleData.expression.mouthPtn.ToString(), this.gui.gsButton);
					if (flag25)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, Enumerable.Range(0, this.currentFemaleData.maxMouthPtn).Select(delegate(int x)
						{
							bool flag48 = !this.mouthPtnNameDict.ContainsKey(x);
							string result;
							if (flag48)
							{
								result = x.ToString();
							}
							else
							{
								result = this.mouthPtnNameDict[x];
							}
							return result;
						}).ToArray<string>(), delegate(int x)
						{
							this.currentFemaleData.SetPart(ExpressionControl.PartType.mouth, x);
						});
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag26 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag26)
					{
						this.currentFemaleData.NextPart(ExpressionControl.PartType.mouth);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockMouthPtn = GUI.Toggle(rect2, this.currentFemaleData.lockMouthPtn, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "开合", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.expression.mouthOpen, 0f, 1f);
					bool flag27 = num != this.currentFemaleData.expression.mouthOpen;
					if (flag27)
					{
						this.currentFemaleData.ChangeOpen(ExpressionControl.PartType.mouth, num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockMouthOpen = GUI.Toggle(rect2, this.currentFemaleData.lockMouthOpen, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "泪", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					byte b = (byte)GUI.HorizontalSlider(rect2, (float)this.currentFemaleData.expression.tearLv, 0f, 3f);
					bool flag28 = b != this.currentFemaleData.expression.tearLv;
					if (flag28)
					{
						this.currentFemaleData.ChangeTear(b);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockTear = GUI.Toggle(rect2, this.currentFemaleData.lockTear, string.Empty, this.gui.gsToggle);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "脸红", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 3);
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.expression.hohoAka, 0f, 1f);
					bool flag29 = num != this.currentFemaleData.expression.hohoAka;
					if (flag29)
					{
						this.currentFemaleData.ChangeHohoAka(num);
					}
					rect2.x += rect2.width;
					rect2.width = (float)this.gui.fontSize;
					this.currentFemaleData.lockHohoAka = GUI.Toggle(rect2, this.currentFemaleData.lockHohoAka, string.Empty, this.gui.gsToggle);
					rect2.y = this.gui.DrawLine(new Vector2(rect.x, rect2.y + this.gui.itemHeightWithMargin), rect.width);
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 2);
					GUI.Label(rect2, "表情", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag30 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag30)
					{
						this.currentFemaleData.PrevExpression(this.xml.expressions);
					}
					rect2.x += rect2.width;
					rect2.width = rect.width - rect2.width * 3f;
					bool flag31 = GUIMgr.Button(rect2, this.currentFemaleData.expression.name, this.gui.gsButton);
					if (flag31)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, (from x in this.xml.expressions
						select x.name).ToArray<string>(), delegate(int x)
						{
							this.currentFemaleData.SetExpression(this.xml.expressions[x]);
						});
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag32 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag32)
					{
						this.currentFemaleData.NextExpression(this.xml.expressions);
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = rect.width / 3f;
					bool flag33 = GUIMgr.Button(rect2, "新建保存", this.gui.gsButton);
					if (flag33)
					{
						this.SaveWindow(true, ExpressionControl.GUIState.saveNew);
					}
					GUI.enabled = !string.IsNullOrEmpty(this.currentFemaleData.expression.guid);
					rect2.x += rect2.width;
					bool flag34 = GUIMgr.Button(rect2, "覆盖保存", this.gui.gsButton);
					if (flag34)
					{
						this.SaveWindow(true, ExpressionControl.GUIState.update);
					}
					rect2.x += rect2.width;
					bool flag35 = GUIMgr.Button(rect2, "删除", this.gui.gsButton);
					if (flag35)
					{
						this.SaveWindow(true, ExpressionControl.GUIState.delete);
					}
					GUI.enabled = !this.gui.showSave;
					rect2.y = this.gui.DrawLine(new Vector2(rect.x, rect2.y + this.gui.itemHeightWithMargin), rect.width);
					rect2.x = rect.x;
					rect2.width = (float)(this.gui.fontSize * 5);
					GUI.Label(rect2, "场景预设", this.gui.gsLabel);
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					if (GUIMgr.Button(rect2, "<", this.gui.gsButton)) this.SelectScenePreset(this.selectedScenePreset - 1);
					rect2.x += rect2.width;
					rect2.width = rect.width - (float)(this.gui.fontSize * 9);
					ExpressionControl.ScenePreset scenePreset = this.CurrentScenePreset();
					if (GUIMgr.Button(rect2, scenePreset == null ? "（无场景预设）" : scenePreset.name, this.gui.gsButton) && this.xml.scenePresets.Count > 0)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, this.xml.scenePresets.Select((ExpressionControl.ScenePreset x) => x.name).ToArray<string>(), delegate(int x) { this.SelectScenePreset(x); });
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					if (GUIMgr.Button(rect2, ">", this.gui.gsButton)) this.SelectScenePreset(this.selectedScenePreset + 1);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = rect.width / 4f;
					GUI.enabled = !this.gui.showSave && scenePreset != null;
					if (GUIMgr.Button(rect2, "应用选中", this.gui.gsButton)) this.ApplyCurrentScenePreset();
					GUI.enabled = !this.gui.showSave && this.femaleList.Count > 0;
					rect2.x += rect2.width;
					if (GUIMgr.Button(rect2, "新建保存", this.gui.gsButton)) this.SceneSaveWindow(ExpressionControl.GUIState.saveNew);
					GUI.enabled = !this.gui.showSave && scenePreset != null && this.femaleList.Count > 0;
					rect2.x += rect2.width;
					if (GUIMgr.Button(rect2, "覆盖保存", this.gui.gsButton)) this.SceneSaveWindow(ExpressionControl.GUIState.update);
					GUI.enabled = !this.gui.showSave && scenePreset != null;
					rect2.x += rect2.width;
					if (GUIMgr.Button(rect2, "删除选中", this.gui.gsButton)) this.SceneSaveWindow(ExpressionControl.GUIState.delete);
					GUI.enabled = !this.gui.showSave;
					bool programValid = this.currentFemaleData.programValid;
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = rect.width;
					GUI.Label(rect2, "表情程序", this.gui.gsLabel);
					rect2.y += this.gui.itemHeight;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag36 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag36)
					{
						this.currentFemaleData.PrevProgram(this.xml);
						bool flag37 = programValid != this.currentFemaleData.programValid;
						if (flag37)
						{
							this.currentFemaleData.UpdateState(false, this.nowMotion, this.isFeraAction);
							programValid = !programValid;
						}
					}
					rect2.x += rect2.width;
					rect2.width = rect.width - rect2.width * 2f;
					bool flag38 = GUIMgr.Button(rect2, this.currentFemaleData.program.name, this.gui.gsButton);
					if (flag38)
					{
						this.gui.combo.Set(this.gui.rectMainWin, rect2, (from x in this.xml.programs
						select x.name).ToArray<string>(), delegate(int x)
						{
							this.currentFemaleData.SetProgram(this.xml.programs[x]);
							bool flag48 = programValid != this.currentFemaleData.programValid;
							if (flag48)
							{
								this.currentFemaleData.UpdateState(false, this.nowMotion, this.isFeraAction);
								programValid = !programValid;
							}
						});
					}
					rect2.x += rect2.width;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag39 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag39)
					{
						this.currentFemaleData.NextProgram(this.xml);
						bool flag40 = programValid != this.currentFemaleData.programValid;
						if (flag40)
						{
							this.currentFemaleData.UpdateState(false, this.nowMotion, this.isFeraAction);
							programValid = !programValid;
						}
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = rect.width;
					GUI.Label(rect2, "倒计时： " + ((this.currentFemaleData.programTimer >= 20f) ? "-/-" : (this.currentFemaleData.programTimerNow.ToString("F2") + "/" + this.currentFemaleData.programTimer.ToString("F2"))), this.gui.gsLabel);
					rect2.y += this.gui.itemHeight;
					num = GUI.HorizontalSlider(rect2, this.currentFemaleData.programTimer, 1f, 20f);
					bool flag41 = num != this.currentFemaleData.programTimer;
					if (flag41)
					{
						this.currentFemaleData.programTimer = num;
						this.SetProgramTimer(this.currentFemaleData);
						bool flag42 = programValid != this.currentFemaleData.programValid;
						if (flag42)
						{
							this.currentFemaleData.UpdateState(false, this.nowMotion, this.isFeraAction);
							programValid = !programValid;
						}
					}
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.width = (float)(this.gui.fontSize * 2);
					bool flag43 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
					if (flag43)
					{
						int num2 = this.orgazumCount - 1;
						this.orgazumCount = num2;
						bool flag44 = num2 < 0;
						if (flag44)
						{
							this.orgazumCount = 0;
						}
					}
					rect2.x += rect2.width;
					bool flag45 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
					if (flag45)
					{
						int num3 = this.orgazumCount + 1;
						this.orgazumCount = num3;
						bool flag46 = num3 > 10;
						if (flag46)
						{
							this.orgazumCount = 10;
						}
					}
					rect2.x += rect2.width + (float)this.gui.fontSize;
					rect2.width = (float)(this.gui.fontSize * 7);
					GUI.Label(rect2, "绝顶次数: " + this.orgazumCount.ToString(), this.gui.gsLabel);
					rect2.y += this.gui.itemHeightWithMargin;
					rect2.x = rect.x;
					rect2.width = rect.width;
					bool flag47 = GUIMgr.Button(rect2, "编辑表情程序", this.gui.gsButton);
					if (flag47)
					{
						this.gui.showProgram = !this.gui.showProgram;
						bool showProgram2 = this.gui.showProgram;
						if (showProgram2)
						{
							this.gui.rectProgram = GUIUtl.MoveScreenCenter(this.gui.rectProgram);
						}
					}
					GUI.enabled = true;
					GUI.DragWindow();
					this.gui.rectMainWin.height = rect2.y + this.gui.itemHeightWithMargin;
				}
			}
		}
		public void GUIFuncProgram(int winID)
		{
			GUI.contentColor = Color.black;
			Rect rect = new Rect((float)(this.gui.fontSize / 2), (float)(this.gui.fontSize * 2), this.gui.rectProgram.width - (float)this.gui.fontSize, this.gui.rectProgram.height - (float)(this.gui.fontSize * 3));
			Rect rect2 = new Rect(this.gui.rectProgram.width - (float)this.gui.fontSize * 1.5f, 0f, (float)this.gui.fontSize * 1.5f, (float)this.gui.fontSize * 1.5f);
			GUI.enabled = !this.gui.showSave;
			bool flag = GUIMgr.Button(rect2, "×", this.gui.gsButton);
			if (flag)
			{
				this.gui.showProgram = false;
			}
			rect2.x -= rect2.width;
			bool flag2 = GUIMgr.Button(rect2, "-", this.gui.gsButton);
			if (flag2)
			{
				this.gui.programWinFold = !this.gui.programWinFold;
				this.gui.rectProgram.height = (this.gui.programWinFold ? 20f : this.gui.programWinHeight);
			}
			bool programWinFold = this.gui.programWinFold;
			if (programWinFold)
			{
				GUI.DragWindow();
			}
			else
			{
				rect2.Set(rect.x, rect.y, (float)(this.gui.fontSize * 2), this.gui.itemHeight);
				bool flag3 = GUIMgr.Button(rect2, "<", this.gui.gsButton);
				if (flag3)
				{
					this.PrevProgram();
				}
				rect2.x += rect2.width;
				rect2.width = (float)(this.gui.fontSize * 10);
				bool flag4 = GUIMgr.Button(rect2, this.currentProgram.name, this.gui.gsButton);
				if (flag4)
				{
					this.gui.combo.Set(this.gui.rectProgram, rect2, (from x in this.xml.programs
					select x.name).ToArray<string>(), delegate(int x)
					{
						this.SelectProgram(x);
					});
				}
				rect2.x += rect2.width;
				rect2.width = (float)(this.gui.fontSize * 2);
				bool flag5 = GUIMgr.Button(rect2, ">", this.gui.gsButton);
				if (flag5)
				{
					this.NextProgram();
				}
				rect2.x += rect2.width + (float)this.gui.fontSize;
				rect2.width = (float)(this.gui.fontSize * 5);
				GUI.enabled = (this.currentProgram.units.Count > 0);
				bool flag6 = GUIMgr.Button(rect2, "新建保存", this.gui.gsButton);
				if (flag6)
				{
					this.SaveWindow(false, ExpressionControl.GUIState.saveNew);
				}
				rect2.x += rect2.width;
				GUI.enabled = !string.IsNullOrEmpty(this.currentProgram.guid);
				bool flag7 = GUIMgr.Button(rect2, "覆盖保存", this.gui.gsButton);
				if (flag7)
				{
					this.SaveWindow(false, ExpressionControl.GUIState.update);
				}
				rect2.x += rect2.width;
				bool flag8 = GUIMgr.Button(rect2, "删除", this.gui.gsButton);
				if (flag8)
				{
					this.SaveWindow(false, ExpressionControl.GUIState.delete);
				}
				GUI.enabled = !this.gui.showSave;
				Rect rect3 = rect2;
				rect3.x = rect.x;
				rect3.width = rect.width - (float)(this.gui.fontSize * 10);
				rect3.y += this.gui.itemHeightWithMargin;
				rect3.height = rect.height - this.gui.itemHeightWithMargin * 3f;
				float num = this.gui.itemHeight * 7f;
				Rect rect4 = new Rect(0f, 0f, rect3.width - (float)this.gui.fontSize * 1.5f, num * (float)this.currentProgram.units.Count);
				this.gui.v2ScrollPos1 = GUI.BeginScrollView(rect3, this.gui.v2ScrollPos1, rect4, false, true);
				Rect r = new Rect(0f, 0f, rect4.width, num);
				for (int i = 0; i < this.currentProgram.units.Count; i++)
				{
					this.AddUnitPanel(r, i);
					r.y += r.height;
				}
				GUI.EndScrollView();
				rect3.x += rect3.width;
				rect3.width = (float)(this.gui.fontSize * 10);
				Rect rect5 = new Rect(0f, 0f, rect3.width - (float)this.gui.fontSize * 1.5f, this.gui.itemHeight * (float)this.xml.expressions.Count);
				this.gui.v2ScrollPos2 = GUI.BeginScrollView(rect3, this.gui.v2ScrollPos2, rect5, false, true);
				bool flag9 = this.isSortExpression;
				if (flag9)
				{
					Rect rect6 = new Rect(0f, 0f, (float)this.gui.fontSize * 1.5f, this.gui.itemHeight);
					for (int j = 0; j < this.xml.expressions.Count; j++)
					{
						bool flag10 = GUIMgr.Button(rect6, "△", this.gui.gsButton);
						if (flag10)
						{
							this.MoveExpression(j, true);
						}
						rect6.x += rect6.width;
						bool flag11 = GUIMgr.Button(rect6, "▽", this.gui.gsButton);
						if (flag11)
						{
							this.MoveExpression(j, false);
						}
						rect6.x += rect6.width;
						rect6.width = rect5.width - rect6.width * 2f;
						GUI.Label(rect6, this.xml.expressions[j].name, this.gui.gsLabel);
						rect6.y += rect6.height;
						rect6.x = 0f;
						rect6.width = (float)this.gui.fontSize * 1.5f;
					}
				}
				else
				{
					Rect rect7 = new Rect(0f, 0f, rect5.width, this.gui.itemHeight);
					for (int k = 0; k < this.xml.expressions.Count; k++)
					{
						bool flag12 = this.currentProgram.units.Count == 0;
						if (flag12)
						{
							GUI.Toggle(rect7, false, this.xml.expressions[k].name, this.gui.gsToggle);
						}
						else
						{
							bool flag13 = this.currentProgram.units[this.selectedProgramUnit].expressionGuids.Contains(this.xml.expressions[k].guid);
							bool flag14 = GUI.Toggle(rect7, flag13, this.xml.expressions[k].name, this.gui.gsToggle);
							bool flag15 = flag14 != flag13;
							if (flag15)
							{
								bool flag16 = flag14;
								if (flag16)
								{
									this.currentProgram.units[this.selectedProgramUnit].expressionGuids.Add(this.xml.expressions[k].guid);
								}
								else
								{
									this.currentProgram.units[this.selectedProgramUnit].expressionGuids.Remove(this.xml.expressions[k].guid);
								}
							}
						}
						rect7.y += rect7.height;
					}
				}
				GUI.EndScrollView();
				rect2.x = rect3.x;
				rect2.width = rect3.width;
				rect2.y = rect.y;
				bool flag17 = GUIMgr.Button(rect2, "表情排序", this.gui.gsButton);
				if (flag17)
				{
					this.isSortExpression = !this.isSortExpression;
					bool flag18 = this.isSortExpression;
					if (flag18)
					{
						this.isSortExpressionModify = false;
					}
					else
					{
						bool flag19 = this.isSortExpressionModify;
						if (flag19)
						{
							this.xml.Save(true);
						}
					}
				}
				rect2.x = rect.x;
				rect2.width = (float)(this.gui.fontSize * 3);
				rect2.y = rect3.y + rect3.height + this.gui.margin;
				bool flag20 = GUIMgr.Button(rect2, "△", this.gui.gsButton);
				if (flag20)
				{
					this.MoveUnit(true);
				}
				rect2.x += rect2.width;
				bool flag21 = GUIMgr.Button(rect2, "▽", this.gui.gsButton);
				if (flag21)
				{
					this.MoveUnit(false);
				}
				rect2.x += rect2.width + (float)this.gui.fontSize;
				bool flag22 = GUIMgr.Button(rect2, "添加", this.gui.gsButton);
				if (flag22)
				{
					this.AddUnit();
				}
				rect2.x += rect2.width;
				bool flag23 = GUIMgr.Button(rect2, "复制", this.gui.gsButton);
				if (flag23)
				{
					this.CopyUnit(false);
				}
				rect2.x += rect2.width;
				rect2.width = (float)(this.gui.fontSize * 6);
				bool flag24 = GUIMgr.Button(rect2, "复制到下方", this.gui.gsButton);
				if (flag24)
				{
					this.CopyUnit(true);
				}
				rect2.x += rect2.width;
				rect2.width = (float)(this.gui.fontSize * 3);
				bool flag25 = GUIMgr.Button(rect2, "删除", this.gui.gsButton);
				if (flag25)
				{
					this.DeleteUnit();
				}
				rect2.x += rect2.width + (float)this.gui.fontSize;
				rect2.width = (float)(this.gui.fontSize * 11);
				this.currentProgram.applyMouthOpen = GUI.Toggle(rect2, this.currentProgram.applyMouthOpen, "应用嘴巴开合", this.gui.gsToggle);
				rect2.x += rect2.width;
				rect2.width = (float)(this.gui.fontSize * 14);
				this.currentProgram.ignoreMouthInFera = GUI.Toggle(rect2, this.currentProgram.ignoreMouthInFera, "口交时不应用口型", this.gui.gsToggle);
				rect2.y += this.gui.itemHeightWithMargin;
				rect2.x -= (float)(this.gui.fontSize * 11);
				rect2.width = (float)(this.gui.fontSize * 13);
				this.currentProgram.ignoreKiss = GUI.Toggle(rect2, this.currentProgram.ignoreKiss, "接吻时禁用程序", this.gui.gsToggle);
				GUI.enabled = true;
				GUI.DragWindow();
			}
		}
		private void AddUnitPanel(Rect r, int number)
		{
			bool flag = number == this.selectedProgramUnit;
			bool flag2 = flag;
			if (flag2)
			{
				GUI.BeginGroup(r, this.gui.gsPanelBorderRed);
			}
			else
			{
				GUI.backgroundColor = new Color(1f, 1f, 1f, 0.5f);
				GUI.BeginGroup(r, this.gui.gsPanelNormal);
				GUI.backgroundColor = Color.white;
			}
			Rect rect = new Rect(0f, 0f, (float)this.gui.fontSize * 1.5f, r.height);
			bool flag3 = GUIMgr.Button(rect, flag ? "○" : string.Empty, this.gui.gsButton);
			if (flag3)
			{
				this.selectedProgramUnit = number;
			}
			rect.x += rect.width;
			rect.height /= 2f;
			bool flag4 = GUIMgr.Button(rect, "△", this.gui.gsButton);
			if (flag4)
			{
				this.MoveUnit(number, true);
			}
			rect.y += rect.height;
			bool flag5 = GUIMgr.Button(rect, "▽", this.gui.gsButton);
			if (flag5)
			{
				this.MoveUnit(number, false);
			}
			rect.x = (float)(this.gui.fontSize * 4);
			rect.y = (float)this.gui.fontSize * 0.5f;
			rect.width = (float)(this.gui.fontSize * 5);
			rect.height = this.gui.itemHeight;
			this.currentProgram.units[number].aibu = GUI.Toggle(rect, this.currentProgram.units[number].aibu, "爱抚", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].houshi = GUI.Toggle(rect, this.currentProgram.units[number].houshi, "侍奉", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].sounyuu = GUI.Toggle(rect, this.currentProgram.units[number].sounyuu, "插入", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].fera = GUI.Toggle(rect, this.currentProgram.units[number].fera, "口交", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].masturbation = GUI.Toggle(rect, this.currentProgram.units[number].masturbation, "自慰", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].lesbian = GUI.Toggle(rect, this.currentProgram.units[number].lesbian, "百合", this.gui.gsToggle);
			rect.x = this.gui.DrawLineV(new Vector2(rect.x + rect.width + this.gui.margin, (float)this.gui.fontSize * 0.5f), r.height - (float)this.gui.fontSize);
			rect.width = (float)(this.gui.fontSize * 6);
			rect.y = (float)this.gui.fontSize * 0.5f;
			this.currentProgram.units[number].idle = GUI.Toggle(rect, this.currentProgram.units[number].idle, "待机", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].kiss = GUI.Toggle(rect, this.currentProgram.units[number].kiss, "接吻", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].loopW = GUI.Toggle(rect, this.currentProgram.units[number].loopW, "弱循环", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].loopS = GUI.Toggle(rect, this.currentProgram.units[number].loopS, "强循环", this.gui.gsToggle);
			rect.x += rect.width + this.gui.margin;
			rect.y = (float)this.gui.fontSize * 0.5f;
			this.currentProgram.units[number].spurtM = GUI.Toggle(rect, this.currentProgram.units[number].spurtM, "男冲刺", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].spurtF = GUI.Toggle(rect, this.currentProgram.units[number].spurtF, "女冲刺", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].orgazumM = GUI.Toggle(rect, this.currentProgram.units[number].orgazumM, "男绝顶", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].orgazumF = GUI.Toggle(rect, this.currentProgram.units[number].orgazumF, "女绝顶", this.gui.gsToggle);
			rect.x += rect.width + this.gui.margin;
			rect.y = (float)this.gui.fontSize * 0.5f;
			this.currentProgram.units[number].afterOrgazumM = GUI.Toggle(rect, this.currentProgram.units[number].afterOrgazumM, "男绝顶后", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].afterOrgazumF = GUI.Toggle(rect, this.currentProgram.units[number].afterOrgazumF, "女绝顶后", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].drinkVomitWait = GUI.Toggle(rect, this.currentProgram.units[number].drinkVomitWait, "精饮待机", this.gui.gsToggle);
			rect.x += rect.width + this.gui.margin;
			rect.width = (float)(this.gui.fontSize * 7);
			rect.y = (float)this.gui.fontSize * 0.5f;
			GUI.Label(rect, "敏感度:", this.gui.gsLabel);
			int num = 0;
			string text = string.Format("programWindow_{0}_", number);
			rect.y += rect.height;
			rect.width = (float)(this.gui.fontSize * 3);
			Rect rect2 = rect;
			string text2 = this.currentProgram.units[number].feelingL.ToString();
			GUIStyle gsTextField = this.gui.gsTextField;
			string str = text;
			int num2 = num;
			num = num2 + 1;
			GUIMgr.TextField_FocusCtrl(rect2, text2, gsTextField, str + num2.ToString(), delegate(string x)
			{
				int feelingL = Mathf.Clamp(GUIMgr.StringToInt(x, this.currentProgram.units[number].feelingL), 0, this.currentProgram.units[number].feelingH);
				this.currentProgram.units[number].feelingL = feelingL;
				return feelingL.ToString();
			});
			rect.x += rect.width;
			rect.width = (float)this.gui.fontSize;
			GUI.Label(rect, "～", this.gui.gsLabel);
			rect.x += rect.width;
			rect.width = (float)(this.gui.fontSize * 3);
			Rect rect3 = rect;
			string text3 = this.currentProgram.units[number].feelingH.ToString();
			GUIStyle gsTextField2 = this.gui.gsTextField;
			string str2 = text;
			num2 = num;
			num = num2 + 1;
			GUIMgr.TextField_FocusCtrl(rect3, text3, gsTextField2, str2 + num2.ToString(), delegate(string x)
			{
				int feelingH = Mathf.Clamp(GUIMgr.StringToInt(x, this.currentProgram.units[number].feelingH), this.currentProgram.units[number].feelingL, 100);
				this.currentProgram.units[number].feelingH = feelingH;
				return feelingH.ToString();
			});
			rect.x -= (float)(this.gui.fontSize * 4);
			rect.width = (float)(this.gui.fontSize * 7);
			rect.y += rect.height;
			GUI.Label(rect, "绝顶次数:", this.gui.gsLabel);
			rect.y += rect.height;
			rect.width = (float)(this.gui.fontSize * 3);
			Rect rect4 = rect;
			string text4 = this.currentProgram.units[number].orgazumNumL.ToString();
			GUIStyle gsTextField3 = this.gui.gsTextField;
			string str3 = text;
			num2 = num;
			num = num2 + 1;
			GUIMgr.TextField_FocusCtrl(rect4, text4, gsTextField3, str3 + num2.ToString(), delegate(string x)
			{
				int orgazumNumL = Mathf.Clamp(GUIMgr.StringToInt(x, this.currentProgram.units[number].orgazumNumL), 0, this.currentProgram.units[number].orgazumNumH);
				this.currentProgram.units[number].orgazumNumL = orgazumNumL;
				return orgazumNumL.ToString();
			});
			rect.x += rect.width;
			rect.width = (float)this.gui.fontSize;
			GUI.Label(rect, "～", this.gui.gsLabel);
			rect.x += rect.width;
			rect.width = (float)(this.gui.fontSize * 3);
			Rect rect5 = rect;
			string text5 = this.currentProgram.units[number].orgazumNumH.ToString();
			GUIStyle gsTextField4 = this.gui.gsTextField;
			string str4 = text;
			num2 = num;
			num = num2 + 1;
			GUIMgr.TextField_FocusCtrl(rect5, text5, gsTextField4, str4 + num2.ToString(), delegate(string x)
			{
				int orgazumNumH = Mathf.Clamp(GUIMgr.StringToInt(x, this.currentProgram.units[number].orgazumNumH), this.currentProgram.units[number].orgazumNumL, 10);
				this.currentProgram.units[number].orgazumNumH = orgazumNumH;
				return orgazumNumH.ToString();
			});
			rect.x = this.gui.DrawLineV(new Vector2(rect.x + rect.width + this.gui.margin, (float)this.gui.fontSize * 0.5f), r.height - (float)this.gui.fontSize);
			rect.width = (float)(this.gui.fontSize * 7);
			rect.y = (float)this.gui.fontSize * 0.5f;
			this.currentProgram.units[number].morph = GUI.Toggle(rect, this.currentProgram.units[number].morph, "形态", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].tear = GUI.Toggle(rect, this.currentProgram.units[number].tear, "泪", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].hoho = GUI.Toggle(rect, this.currentProgram.units[number].hoho, "脸红", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].highlight = GUI.Toggle(rect, this.currentProgram.units[number].highlight, "高光", this.gui.gsToggle);
			rect.x += rect.width;
			rect.y = (float)this.gui.fontSize * 0.5f;
			rect.width = (float)(this.gui.fontSize * 4);
			this.currentProgram.units[number].eyeY = GUI.Toggle(rect, this.currentProgram.units[number].eyeY, "瞳孔Y", this.gui.gsToggle);
			rect.y += rect.height;
			this.currentProgram.units[number].eyeSmall = GUI.Toggle(rect, this.currentProgram.units[number].eyeSmall, "缩瞳", this.gui.gsToggle);
			GUI.EndGroup();
		}
		private void SaveWindow(bool mainWindow, ExpressionControl.GUIState state)
		{
			this.guiStateScene = ExpressionControl.GUIState.normal;
			if (mainWindow)
			{
				this.guiStateMain = state;
				this.guiStateProgram = ExpressionControl.GUIState.normal;
			}
			else
			{
				this.guiStateProgram = state;
				this.guiStateMain = ExpressionControl.GUIState.normal;
			}
			bool flag = state == ExpressionControl.GUIState.normal;
			if (flag)
			{
				this.gui.showSave = false;
			}
			else
			{
				bool showSave = this.gui.showSave;
				if (!showSave)
				{
					this.gui.showSave = true;
					this.gui.rectSave.height = (float)(this.gui.fontSize * 10);
					bool flag2 = state == ExpressionControl.GUIState.saveNew;
					if (flag2)
					{
						this.gui.sTmp = string.Empty;
						this.setInputFocus = true;
					}
					else
					{
						bool flag3 = state == ExpressionControl.GUIState.update;
						if (flag3)
						{
							this.gui.sTmp = (mainWindow ? this.currentFemaleData.expression.name : this.currentProgram.name);
							this.setInputFocus = true;
						}
						else
						{
							this.gui.sTmp = (mainWindow ? this.currentFemaleData.expression.name : this.currentProgram.name);
						}
					}
					this.gui.rectSave = GUIUtl.MoveScreenCenter(this.gui.rectSave);
				}
			}
		}
		private void SceneSaveWindow(ExpressionControl.GUIState state)
		{
			this.guiStateMain = ExpressionControl.GUIState.normal;
			this.guiStateProgram = ExpressionControl.GUIState.normal;
			this.guiStateScene = state;
			if (state == ExpressionControl.GUIState.normal)
			{
				this.gui.showSave = false;
				return;
			}
			this.gui.showSave = true;
			this.gui.rectSave.height = (float)(this.gui.fontSize * 10);
			ExpressionControl.ScenePreset preset = this.CurrentScenePreset();
			this.gui.sTmp = state == ExpressionControl.GUIState.saveNew ? string.Empty : (preset == null ? string.Empty : preset.name);
			this.setInputFocus = state != ExpressionControl.GUIState.delete;
			this.gui.rectSave = GUIUtl.MoveScreenCenter(this.gui.rectSave);
		}
		private void CloseSaveWindow()
		{
			this.guiStateMain = ExpressionControl.GUIState.normal;
			this.guiStateProgram = ExpressionControl.GUIState.normal;
			this.guiStateScene = ExpressionControl.GUIState.normal;
			this.gui.showSave = false;
		}
		public void GUIFuncDummy(int winID)
		{
			GUIMgr.GUIFuncDummy();
		}
		private static readonly int WINDOW_ID = 1120;
		private ShortCutKeyMgr hotkey;
		private static readonly string hotkeyShowGUI = "showGUI";
		private GUIMgr gui;
		private XmlMgr xml;
		private static StudioScene studioScene;
		private static bool isStudio;
		internal List<ExpressionControl.FemaleData> femaleList = new List<ExpressionControl.FemaleData>();
		internal int currentFemaleNum;
		internal int currentProgramNum;
		private ExpressionControl.Program currentProgram;
		private string nowMotion;
		private string nowHAction;
		private int orgazumCount;
		private Dictionary<int, string> eyeBrowPtnNameDict = new Dictionary<int, string>
		{
			{
				0,
				"0 默认"
			},
			{
				1,
				"1 生气"
			},
			{
				2,
				"2 困扰"
			},
			{
				3,
				"3 无聊"
			},
			{
				4,
				"4 疑问左"
			},
			{
				5,
				"5 疑问右"
			},
			{
				6,
				"6 思考左"
			},
			{
				7,
				"7 思考右"
			},
			{
				8,
				"8 生气2左"
			},
			{
				9,
				"9 生气2右"
			},
			{
				10,
				"10 认真"
			},
			{
				11,
				"11 不安"
			},
			{
				12,
				"12 惊讶"
			},
			{
				13,
				"13 沮丧"
			},
			{
				14,
				"14 得意"
			},
			{
				15,
				"15 眨眼左"
			},
			{
				16,
				"16 眨眼右"
			}
		};
		private Dictionary<int, string> eyesPtnNameDict = new Dictionary<int, string>
		{
			{
				0,
				"0 默认"
			},
			{
				1,
				"1 双眼闭合"
			},
			{
				2,
				"2 笑眼"
			},
			{
				3,
				"3 笑眼 双眼闭合"
			},
			{
				4,
				"4 微笑"
			},
			{
				5,
				"5 左眨眼"
			},
			{
				6,
				"6 右眨眼"
			},
			{
				7,
				"7 难过"
			},
			{
				8,
				"8 害羞"
			},
			{
				9,
				"9 生气"
			},
			{
				10,
				"10 认真"
			},
			{
				11,
				"11 无聊"
			},
			{
				12,
				"12 痛苦"
			},
			{
				13,
				"13 厌恶"
			},
			{
				14,
				"14 思考"
			},
			{
				15,
				"15 悲伤"
			},
			{
				16,
				"16 哭泣"
			},
			{
				17,
				"17 焦急"
			},
			{
				18,
				"18 沮丧"
			},
			{
				19,
				"19 困扰"
			},
			{
				20,
				"20 得意"
			},
			{
				21,
				"21 螺旋眼1"
			},
			{
				22,
				"22 螺旋眼2"
			},
			{
				23,
				"23 螺旋眼3"
			},
			{
				24,
				"24 星星瞳"
			},
			{
				25,
				"25 爱心瞳"
			},
			{
				26,
				"26 爱心眼"
			},
			{
				27,
				"27 火焰眼"
			},
			{
				28,
				"28 Q版眨眼"
			},
			{
				29,
				"29 竖一字"
			},
			{
				30,
				"30 Q版闭眼"
			},
			{
				31,
				"31 横一字"
			},
			{
				32,
				"32 Q版哭泣"
			}
		};
		private Dictionary<int, string> mouthPtnNameDict = new Dictionary<int, string>
		{
			{
				0,
				"0 默认"
			},
			{
				1,
				"1 笑脸"
			},
			{
				2,
				"2 开心"
			},
			{
				3,
				"3 开心s"
			},
			{
				4,
				"4 开心ss"
			},
			{
				5,
				"5 心动"
			},
			{
				6,
				"6 心动s"
			},
			{
				7,
				"7 心动ss"
			},
			{
				8,
				"8 生气"
			},
			{
				9,
				"9 生气2"
			},
			{
				10,
				"10 认真1"
			},
			{
				11,
				"11 认真2"
			},
			{
				12,
				"12 厌恶"
			},
			{
				13,
				"13 寂寞"
			},
			{
				14,
				"14 焦急"
			},
			{
				15,
				"15 不满"
			},
			{
				16,
				"16 无语"
			},
			{
				17,
				"17 惊讶"
			},
			{
				18,
				"18 惊讶s"
			},
			{
				19,
				"19 得意"
			},
			{
				20,
				"20 吐舌舔唇"
			},
			{
				21,
				"21 舔"
			},
			{
				22,
				"22 含住"
			},
			{
				23,
				"23 接吻"
			},
			{
				24,
				"24 吐舌张口"
			},
			{
				25,
				"25 啊 小"
			},
			{
				26,
				"26 啊 大"
			},
			{
				27,
				"27 咿 小"
			},
			{
				28,
				"28 咿 大"
			},
			{
				29,
				"29 呜 小"
			},
			{
				30,
				"30 呜 大"
			},
			{
				31,
				"31 诶 小"
			},
			{
				32,
				"32 诶 大"
			},
			{
				33,
				"33 哦 小"
			},
			{
				34,
				"34 哦 大"
			},
			{
				35,
				"35 嗯 小"
			},
			{
				36,
				"36 嗯 大"
			},
			{
				37,
				"37 猫嘴 闭合"
			},
			{
				38,
				"38 三角"
			},
			{
				39,
				"39 Q版微笑"
			}
		};
		private static bool isHScene;
		private static HSceneProc hsceneProc;
		private static HFlag hFrag;
		private bool isFeraAction;
		private bool updateState;
		private static readonly ExpressionControl.FemaleState[] inActions = new ExpressionControl.FemaleState[]
		{
			ExpressionControl.FemaleState.kiss,
			ExpressionControl.FemaleState.loopW,
			ExpressionControl.FemaleState.loopS,
			ExpressionControl.FemaleState.spurtF,
			ExpressionControl.FemaleState.spurtM,
			ExpressionControl.FemaleState.orgazumF,
			ExpressionControl.FemaleState.orgazumM,
			ExpressionControl.FemaleState.drinkVomitWait
		};
		private ExpressionControl.GUIState guiStateMain;
		private ExpressionControl.GUIState guiStateProgram;
		private ExpressionControl.GUIState guiStateScene;
		private int selectedProgramUnit;
		private bool isSortExpression;
		private bool isSortExpressionModify;
		private bool setInputFocus;
		private int selectedScenePreset = -1;
		public partial class FemaleData
		{
			public MonoBehaviour instMono { get; private set; }
			public FaceListCtrl faceListCtrl { get; private set; }
			public HVoiceCtrl.Voice nowVoice { get; private set; }
			public ExpressionControl.FemaleState state { get; private set; }
			public bool isFeraMode { get; private set; }
			public bool isKiss { get; private set; }
			public bool ignoreMouthOpen { get; private set; }
			public bool ignoreMouthLoad { get; private set; }
			public bool programValid
			{
				get
				{
					return !string.IsNullOrEmpty(this.program.guid) && this.programTimer > 1f;
				}
			}
			public string fullName
			{
				get
				{
					return this.female.chaFile.parameter.fullname;
				}
			}
			public int maxEyebrowPtn
			{
				get
				{
					return this.female.eyebrowCtrl.GetMaxPtn();
				}
			}
			public int maxEyesPtn
			{
				get
				{
					return this.female.GetEyesPtnNum();
				}
			}
			public int maxMouthPtn
			{
				get
				{
					return this.female.mouthCtrl.GetMaxPtn();
				}
			}
			public FemaleData(ChaControl female, MonoBehaviour inst, HSceneProc hsceneProc)
			{
				this.instMono = inst;
				this.female = female;
				this.expression = new ExpressionControl.Expression(female, 0f, 0f);
				this.expressionRealtime = new ExpressionControl.Expression(this.expression);
				this.program = new ExpressionControl.Program();
				bool flag = hsceneProc != null;
				if (flag)
				{
					List<ChaControl> fieldValue = Utl.GetFieldValue<HSceneProc, List<ChaControl>>(hsceneProc, "lstFemale");
					bool flag2 = fieldValue != null;
					if (flag2)
					{
						bool flag3 = fieldValue.Count > 1 && fieldValue[1] == female;
						if (flag3)
						{
							this.faceListCtrl = hsceneProc.face1;
							this.nowVoice = hsceneProc.voice.nowVoices[1];
							this.handCtrl = hsceneProc.hand1;
							this.voiceIndex = 1;
						}
						bool flag4 = this.faceListCtrl == null && fieldValue.Count > 0 && fieldValue[0] == female;
						if (flag4)
						{
							this.faceListCtrl = hsceneProc.face;
							this.nowVoice = hsceneProc.voice.nowVoices[0];
							this.handCtrl = hsceneProc.hand;
							this.voiceIndex = 0;
						}
					}
				}
			}
			public FemaleData(OCIChar ociFemale, MonoBehaviour inst)
			{
				this.instMono = inst;
				if (ociFemale is OCICharFemale ociF) {
					this.ociFemale = ociF;
					this.female = ociF.female;
				}
                if (ociFemale is OCICharMale ociM) {
                    this.ociFemale = ociM;
                    this.female = ociM.male;
                }
                this.expression = new ExpressionControl.Expression(this.female, 0f, 0f);
				this.expressionRealtime = new ExpressionControl.Expression(this.expression);
				this.program = new ExpressionControl.Program();
			}
			public void UpdateState(bool updateState, string nowMotion, bool isFeraAction)
			{
				bool kissAction = this.IsKissAction();
				bool flag = this.isKiss != kissAction;
				if (flag)
				{
					this.isKiss = kissAction;
					this.state = this.GetFemaleState(nowMotion);
				}
				bool flag2 = !updateState;
				if (!flag2)
				{
					this.state = this.GetFemaleState(nowMotion);
					this.isFeraMode = isFeraAction;
					this.ignoreMouthOpen = (this.programValid && !this.program.applyMouthOpen);
					this.ignoreMouthLoad = (this.programValid && isFeraAction && this.program.ignoreMouthInFera && ExpressionControl.inActions.Contains(this.state));
				}
			}
			private ExpressionControl.FemaleState GetFemaleState(string nowMotion)
			{
				bool flag = ExpressionControl.hsceneProc == null;
				ExpressionControl.FemaleState result;
				if (flag)
				{
					result = ExpressionControl.FemaleState.other;
				}
				else
				{
					bool flag2 = ExpressionControl.hFrag.mode == HFlag.EMode.aibu || ExpressionControl.hFrag.mode == HFlag.EMode.masturbation || ExpressionControl.hFrag.mode == HFlag.EMode.lesbian;
					if (flag2)
					{
						bool flag3 = nowMotion.Contains("_Idle") || nowMotion.Contains("WLoop");
						if (flag3)
						{
							result = ExpressionControl.FemaleState.loopW;
						}
						else
						{
							bool flag4 = nowMotion.Contains("MLoop") || nowMotion.Contains("SLoop");
							if (flag4)
							{
								result = ExpressionControl.FemaleState.loopS;
							}
							else
							{
								bool flag5 = nowMotion.Contains("OLoop");
								if (flag5)
								{
									result = ExpressionControl.FemaleState.spurtF;
								}
								else
								{
									bool flag6 = nowMotion == "Orgasm" || nowMotion.Contains("Orgasm_Start") || nowMotion.Contains("Orgasm_Loop") || nowMotion.Contains("S_IN_Start") || nowMotion.Contains("S_IN_Loop") || nowMotion.Contains("F_IN_Start") || nowMotion.Contains("F_IN_Loop") || nowMotion.Contains("IN_Start") || nowMotion.Contains("IN_Loop");
									if (flag6)
									{
										result = this.IsMaleCharacter() && nowMotion.Contains("S_IN_")
											? ExpressionControl.FemaleState.orgazumM
											: ExpressionControl.FemaleState.orgazumF;
									}
									else
									{
										bool flag7 = nowMotion.Contains("Orgasm_A") || nowMotion.Contains("Orgasm_B") || nowMotion.Contains("S_IN_A") || nowMotion.Contains("OUT_A") || nowMotion.Contains("IN_A");
										if (flag7)
										{
											result = this.IsMaleCharacter() && nowMotion.Contains("S_IN_A")
												? ExpressionControl.FemaleState.afterOrgazumM
												: ExpressionControl.FemaleState.afterOrgazumF;
										}
										else
										{
											bool flag21 = nowMotion.Contains("K_Touch") || nowMotion.Contains("K_Loop") || this.IsKissAction();
											if (flag21)
											{
												result = ExpressionControl.FemaleState.kiss;
											}
											else
											{
												bool flag22 = nowMotion.Contains("Idle");
												if (flag22)
												{
													result = ExpressionControl.FemaleState.idle;
												}
												else
												{
													result = ExpressionControl.FemaleState.other;
												}
											}
										}
									}
								}
							}
						}
					}
					else
					{
						bool flag8 = this.IsKissAction();
						if (flag8)
						{
							result = ExpressionControl.FemaleState.kiss;
						}
						else
						{
							bool flag9 = nowMotion.Contains("WLoop");
							if (flag9)
							{
								result = ExpressionControl.FemaleState.loopW;
							}
							else
							{
								bool flag10 = nowMotion.Contains("MLoop") || nowMotion.Contains("SLoop");
								if (flag10)
								{
									result = ExpressionControl.FemaleState.loopS;
								}
								else
								{
									bool flag11 = nowMotion.Contains("OLoop");
									if (flag11)
									{
										bool flag12 = this.IsFemaleSpurtVoice();
										if (flag12)
										{
											result = ExpressionControl.FemaleState.spurtF;
										}
										else
										{
											result = ExpressionControl.FemaleState.spurtM;
										}
									}
									else
									{
						bool flag13 = nowMotion.Contains("S_IN_A") || nowMotion.Contains("Orgasm_B");
						if (flag13)
						{
							result = this.IsMaleCharacter() && nowMotion.Contains("S_IN_A")
								? ExpressionControl.FemaleState.afterOrgazumM
								: ExpressionControl.FemaleState.afterOrgazumF;
										}
										else
										{
							bool flag14 = nowMotion.Contains("S_IN_Start") || nowMotion.Contains("S_IN_Loop") || nowMotion.Contains("F_IN_Start") || nowMotion.Contains("F_IN_Loop");
							if (flag14)
							{
								result = this.IsMaleCharacter() && nowMotion.Contains("S_IN_")
									? ExpressionControl.FemaleState.orgazumM
									: ExpressionControl.FemaleState.orgazumF;
											}
											else
											{
												bool flag15 = nowMotion.Contains("M_OUT_Start") || nowMotion.Contains("M_OUT_Loop") || nowMotion.Contains("M_IN_Start") || nowMotion.Contains("M_IN_Loop");
												if (flag15)
												{
													result = ExpressionControl.FemaleState.orgazumM;
												}
												else
												{
													bool flag16 = nowMotion.Contains("OUT_A") || nowMotion.Contains("IN_A");
													if (flag16)
													{
														result = ExpressionControl.FemaleState.afterOrgazumM;
													}
													else
													{
														bool flag17 = nowMotion.Contains("IN_Start") || nowMotion.Contains("IN_Loop");
														if (flag17)
														{
															result = ExpressionControl.FemaleState.orgazumM;
														}
														else
														{
															bool flag18 = nowMotion.Contains("Vomit_A") || nowMotion.Contains("Drink_A");
															if (flag18)
															{
																result = ExpressionControl.FemaleState.afterOrgazumM;
															}
															else
															{
																bool flag19 = nowMotion.Contains("Oral_Idle");
																if (flag19)
																{
																	result = ExpressionControl.FemaleState.drinkVomitWait;
																}
																else
																{
																	bool flag20 = nowMotion.Contains("Idle");
																	if (flag20)
																	{
																		result = ExpressionControl.FemaleState.idle;
																	}
																	else
																	{
																		result = ExpressionControl.FemaleState.other;
																	}
																}
															}
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
				return result;
			}
			private bool IsKissAction()
			{
				return this.nowVoice != null && this.handCtrl != null && this.handCtrl.IsKissAction() && this.nowVoice.state != HVoiceCtrl.VoiceKind.voice;
			}
			private bool IsMaleCharacter()
			{
				return this.female != null && this.female.chaFile != null && this.female.chaFile.parameter.sex == 0;
			}
			private bool IsFemaleSpurtVoice()
			{
				bool flag = ExpressionControl.hFrag == null || ExpressionControl.hFrag.voice == null || ExpressionControl.hFrag.voice.playVoices == null || ExpressionControl.hFrag.voice.playVoices.Length == 0;
				bool result;
				if (flag)
				{
					result = false;
				}
				else
				{
					int num = this.voiceIndex;
					bool flag2 = num < 0 || num >= ExpressionControl.hFrag.voice.playVoices.Length;
					if (flag2)
					{
						num = 0;
					}
					int num2 = ExpressionControl.hFrag.voice.playVoices[num];
					result = num2 == 318 || num2 == 319;
				}
				return result;
			}
			public void Update()
			{
				ExpressionControl.Expression expression = new ExpressionControl.Expression(this.female, 0f, 0f);
				expression.eyeY = this.expression.eyeY;
				expression.eyeSmall = this.expression.eyeSmall;
				this.expression = new ExpressionControl.Expression(expression);
				this.expressionRealtime = new ExpressionControl.Expression(expression);
			}
			public void PrevPart(ExpressionControl.PartType type)
			{
				switch (type)
				{
				case ExpressionControl.PartType.eyebrow:
				{
					ExpressionControl.Expression expression = this.expression;
					int num = expression.eyebrowPtn - 1;
					expression.eyebrowPtn = num;
					bool flag = num < 0;
					if (flag)
					{
						this.expression.eyebrowPtn = this.maxEyebrowPtn - 1;
					}
					this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, true);
					break;
				}
				case ExpressionControl.PartType.eyes:
				{
					ExpressionControl.Expression expression2 = this.expression;
					int num2 = expression2.eyesPtn - 1;
					expression2.eyesPtn = num2;
					bool flag2 = num2 < 0;
					if (flag2)
					{
						this.expression.eyesPtn = this.maxEyesPtn - 1;
					}
					this.female.ChangeEyesPtn(this.expression.eyesPtn, true);
					break;
				}
				case ExpressionControl.PartType.mouth:
				{
					ExpressionControl.Expression expression3 = this.expression;
					int num3 = expression3.mouthPtn - 1;
					expression3.mouthPtn = num3;
					bool flag3 = num3 < 0;
					if (flag3)
					{
						this.expression.mouthPtn = this.maxMouthPtn - 1;
					}
					this.female.ChangeMouthPtn(this.expression.mouthPtn, true);
					break;
				}
				}
				this.expressionRealtime = new ExpressionControl.Expression(this.expression);
			}
			public void NextPart(ExpressionControl.PartType type)
			{
				switch (type)
				{
				case ExpressionControl.PartType.eyebrow:
				{
					ExpressionControl.Expression expression = this.expression;
					int num = expression.eyebrowPtn + 1;
					expression.eyebrowPtn = num;
					bool flag = num >= this.maxEyebrowPtn;
					if (flag)
					{
						this.expression.eyebrowPtn = 0;
					}
					this.expressionRealtime.eyebrowPtn = this.expression.eyebrowPtn;
					this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, true);
					break;
				}
				case ExpressionControl.PartType.eyes:
				{
					ExpressionControl.Expression expression2 = this.expression;
					int num2 = expression2.eyesPtn + 1;
					expression2.eyesPtn = num2;
					bool flag2 = num2 >= this.maxEyesPtn;
					if (flag2)
					{
						this.expression.eyesPtn = 0;
					}
					this.expressionRealtime.eyesPtn = this.expression.eyesPtn;
					this.female.ChangeEyesPtn(this.expression.eyesPtn, true);
					break;
				}
				case ExpressionControl.PartType.mouth:
				{
					ExpressionControl.Expression expression3 = this.expression;
					int num3 = expression3.mouthPtn + 1;
					expression3.mouthPtn = num3;
					bool flag3 = num3 >= this.maxMouthPtn;
					if (flag3)
					{
						this.expression.mouthPtn = 0;
					}
					this.expressionRealtime.mouthPtn = this.expression.mouthPtn;
					this.female.ChangeMouthPtn(this.expression.mouthPtn, true);
					break;
				}
				}
			}
			public void SetPart(ExpressionControl.PartType type, int number)
			{
				bool flag = number < 0;
				if (flag)
				{
					number = 0;
				}
				switch (type)
				{
				case ExpressionControl.PartType.eyebrow:
					this.expression.eyebrowPtn = ((number >= this.maxEyebrowPtn) ? 0 : number);
					this.expressionRealtime.eyebrowPtn = this.expression.eyebrowPtn;
					this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, true);
					break;
				case ExpressionControl.PartType.eyes:
					this.expression.eyesPtn = ((number >= this.maxEyesPtn) ? 0 : number);
					this.expressionRealtime.eyesPtn = this.expression.eyesPtn;
					this.female.ChangeEyesPtn(this.expression.eyesPtn, true);
					break;
				case ExpressionControl.PartType.mouth:
					this.expression.mouthPtn = ((number >= this.maxMouthPtn) ? 0 : number);
					this.expressionRealtime.mouthPtn = this.expression.mouthPtn;
					this.female.ChangeMouthPtn(this.expression.mouthPtn, true);
					break;
				}
			}
			public void PrevExpression(List<ExpressionControl.Expression> list)
			{
				bool flag = list.Count == 0;
				if (!flag)
				{
					bool flag2 = string.IsNullOrEmpty(this.expression.guid);
					if (flag2)
					{
						this.SetExpression(list.Last<ExpressionControl.Expression>());
					}
					else
					{
						int i = list.Count - 1;
						while (i >= 0)
						{
							bool flag3 = this.expression.guid == list[i].guid;
							if (flag3)
							{
								bool flag4 = i > 0;
								if (flag4)
								{
									this.SetExpression(list[i - 1]);
									return;
								}
								break;
							}
							else
							{
								i--;
							}
						}
						this.SetExpression(list.Last<ExpressionControl.Expression>());
					}
				}
			}
			public void NextExpression(List<ExpressionControl.Expression> list)
			{
				bool flag = list.Count == 0;
				if (!flag)
				{
					bool flag2 = string.IsNullOrEmpty(this.expression.guid);
					if (flag2)
					{
						this.SetExpression(list.Last<ExpressionControl.Expression>());
					}
					else
					{
						int i = 0;
						while (i < list.Count)
						{
							bool flag3 = this.expression.guid == list[i].guid;
							if (flag3)
							{
								bool flag4 = i < list.Count - 1;
								if (flag4)
								{
									this.SetExpression(list[i + 1]);
									return;
								}
								break;
							}
							else
							{
								i++;
							}
						}
						this.SetExpression(list[0]);
					}
				}
			}
			public void ApplyExpression(float nowEyeY, float nowEyeSmall, bool fix)
			{
				if (fix)
				{
					this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, false);
					this.female.ChangeEyesPtn(this.expression.eyesPtn, false);
					this.female.fbsCtrl.EyebrowCtrl.CalculateBlendShape();
					this.female.fbsCtrl.EyesCtrl.CalculateBlendShape();
					bool flag = !this.ignoreMouthLoad;
					if (flag)
					{
						this.female.ChangeMouthPtn(this.expression.mouthPtn, false);
						this.female.fbsCtrl.MouthCtrl.CalculateBlendShape();
					}
				}
				this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, true);
				this.female.ChangeEyesPtn(this.expression.eyesPtn, true);
				bool flag2 = !this.ignoreMouthLoad;
				if (flag2)
				{
					this.female.ChangeMouthPtn(this.expression.mouthPtn, true);
				}
				this.female.HideEyeHighlight(this.expression.hideHighlight);
				this.female.ChangeEyesBlinkFlag(this.expression.blink);
				this.female.tearsLv = this.expression.tearLv;
				bool flag3 = !Utl.Equals(this.expression.eyeOpen, this.expressionRealtime.eyeOpen);
				if (flag3)
				{
					bool flag4 = this.faceListCtrl != null;
					if (flag4)
					{
						GlobalMethod.FloatBlend fieldValue = Utl.GetFieldValue<FaceListCtrl, GlobalMethod.FloatBlend>(this.faceListCtrl, "blendEye");
						fieldValue.Start(this.expressionPrev.eyeOpen, this.expressionPrev.eyeOpen, 0f);
						float num = 0f;
						fieldValue.Proc(ref num);
					}
					bool flag5 = this.coEyeOpenSlide != null;
					if (flag5)
					{
						this.instMono.StopCoroutine(this.coEyeOpenSlide);
						this.coEyeOpenSlide = null;
					}
					this.coEyeOpenSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.eyeOpen = val;
						this.female.ChangeEyesOpenMax(this.expression.eyeOpen);
						if (isEnd)
						{
							this.coEyeOpenSlide = null;
							this.expressionRealtime.eyeOpen = val;
						}
					}, this.expressionPrev.eyeOpen, this.expression.eyeOpen, 0.2f));
				}
				bool flag6 = !this.ignoreMouthLoad && !this.ignoreMouthOpen && !Utl.Equals(this.expression.mouthOpen, this.expressionRealtime.mouthOpen);
				if (flag6)
				{
					bool flag7 = this.faceListCtrl != null;
					if (flag7)
					{
						GlobalMethod.FloatBlend fieldValue2 = Utl.GetFieldValue<FaceListCtrl, GlobalMethod.FloatBlend>(this.faceListCtrl, "blendMouth");
						fieldValue2.Start(this.expressionPrev.mouthOpen, this.expressionPrev.mouthOpen, 0f);
						float num2 = 0f;
						fieldValue2.Proc(ref num2);
					}
					bool flag8 = this.coMouthOpenSlide != null;
					if (flag8)
					{
						this.instMono.StopCoroutine(this.coMouthOpenSlide);
						this.coMouthOpenSlide = null;
					}
					this.coMouthOpenSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.mouthOpen = val;
						this.female.ChangeMouthOpenMax(this.expression.mouthOpen);
						bool isStudio = ExpressionControl.isStudio;
						if (isStudio)
						{
							this.ociFemale.ChangeMouthOpen(this.expression.mouthOpen);
						}
						if (isEnd)
						{
							this.coMouthOpenSlide = null;
							this.expressionRealtime.mouthOpen = val;
						}
					}, this.expressionPrev.mouthOpen, this.expression.mouthOpen, 0.2f));
				}
				bool flag9 = !Utl.Equals(this.expression.hohoAka, this.expressionRealtime.hohoAka);
				if (flag9)
				{
					bool flag10 = this.coHohoAkaSlide != null;
					if (flag10)
					{
						this.instMono.StopCoroutine(this.coHohoAkaSlide);
						this.coHohoAkaSlide = null;
					}
					this.coHohoAkaSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.hohoAka = val;
						this.female.ChangeHohoAkaRate(this.expression.hohoAka);
						if (isEnd)
						{
							this.coHohoAkaSlide = null;
							this.expressionRealtime.hohoAka = val;
						}
					}, this.expressionPrev.hohoAka, this.expression.hohoAka, 0.2f));
				}
				bool flag11 = !Utl.Equals(this.expression.eyeY, nowEyeY);
				if (flag11)
				{
					bool flag12 = this.coEyeYSlide != null;
					if (flag12)
					{
						this.instMono.StopCoroutine(this.coEyeYSlide);
						this.coEyeYSlide = null;
					}
					this.coEyeYSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.eyeY = val;
						if (isEnd)
						{
							this.coEyeYSlide = null;
							this.expressionRealtime.eyeY = val;
						}
					}, nowEyeY, this.expression.eyeY, 0.2f));
				}
				bool flag13 = !Utl.Equals(this.expression.eyeSmall, nowEyeSmall);
				if (flag13)
				{
					bool flag14 = this.coEyeSmallSlide != null;
					if (flag14)
					{
						this.instMono.StopCoroutine(this.coEyeSmallSlide);
						this.coEyeSmallSlide = null;
					}
					this.coEyeSmallSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.eyeSmall = val;
						if (isEnd)
						{
							this.coEyeSmallSlide = null;
							this.expressionRealtime.eyeSmall = val;
						}
					}, nowEyeSmall, this.expression.eyeSmall, 0.1f));
				}
				this.expressionRealtime = new ExpressionControl.Expression(this.female, 0f, 0f);
			}
			public void SetExpression(ExpressionControl.Expression expression)
			{
				bool flag = expression == null;
				if (!flag)
				{
					ExpressionControl.Expression expression2 = new ExpressionControl.Expression(expression);
					bool flag2 = this.lockEyebrowPtn;
					if (flag2)
					{
						expression2.eyebrowPtn = this.expression.eyebrowPtn;
					}
					bool flag3 = this.lockEyePtn;
					if (flag3)
					{
						expression2.eyesPtn = this.expression.eyesPtn;
					}
					bool flag4 = this.lockMouthPtn;
					if (flag4)
					{
						expression2.mouthPtn = this.expression.mouthPtn;
					}
					bool flag5 = this.lockEyeOpen;
					if (flag5)
					{
						expression2.eyeOpen = this.expression.eyeOpen;
					}
					bool flag6 = this.lockMouthOpen;
					if (flag6)
					{
						expression2.mouthOpen = this.expression.mouthOpen;
					}
					bool flag7 = this.lockBlink;
					if (flag7)
					{
						expression2.blink = this.expression.blink;
					}
					bool flag8 = this.lockHighlight;
					if (flag8)
					{
						expression2.hideHighlight = this.expression.hideHighlight;
					}
					bool flag9 = this.lockTear;
					if (flag9)
					{
						expression2.tearLv = this.expression.tearLv;
					}
					bool flag10 = this.lockHohoAka;
					if (flag10)
					{
						expression2.hohoAka = this.expression.hohoAka;
					}
					bool flag11 = this.lockEyeY;
					if (flag11)
					{
						expression2.eyeY = this.expression.eyeY;
					}
					bool flag12 = this.lockEyeSmall;
					if (flag12)
					{
						expression2.eyeSmall = this.expression.eyeSmall;
					}
					float eyeY = this.expression.eyeY;
					float eyeSmall = this.expression.eyeSmall;
					this.expressionPrev = this.expression;
					this.expression = expression2;
					this.ApplyExpression(eyeY, eyeSmall, false);
				}
			}
			public void ChangeOpen(ExpressionControl.PartType type, float val)
			{
				bool flag = type == ExpressionControl.PartType.eyes;
				if (flag)
				{
					this.expression.eyeOpen = val;
					this.expressionRealtime.eyeOpen = val;
					this.female.ChangeEyesOpenMax(this.expression.eyeOpen);
				}
				else
				{
					bool flag2 = type != ExpressionControl.PartType.mouth;
					if (!flag2)
					{
						this.expression.mouthOpen = val;
						this.expressionRealtime.mouthOpen = val;
						this.female.ChangeMouthOpenMax(this.expression.mouthOpen);
						bool isStudio = ExpressionControl.isStudio;
						if (isStudio)
						{
							this.ociFemale.ChangeMouthOpen(val);
						}
					}
				}
			}
			public void ChangeEyeY(float val)
			{
				this.expression.eyeY = val;
				this.expressionRealtime.eyeY = val;
			}
			public void ChangeEyeSmall(float val)
			{
				this.expression.eyeSmall = val;
				this.expressionRealtime.eyeSmall = val;
			}
			public void ChangeHighlight(bool hide)
			{
				this.expression.hideHighlight = hide;
				this.expressionRealtime.hideHighlight = hide;
				this.female.HideEyeHighlight(hide);
			}
			public void ChangeBlink(bool enable)
			{
				this.expression.blink = enable;
				this.expressionRealtime.blink = enable;
				this.female.ChangeEyesBlinkFlag(enable);
			}
			public void ChangeBlinkRate(float val = 30f)
			{
				this.female.fbsCtrl.BlinkCtrl.SetFrequency((byte)Mathf.Clamp(val, 0f, 255f));
			}
			public void ChangeBlinkSpeed(float val = 0.15f)
			{
				this.female.fbsCtrl.BlinkCtrl.BaseSpeed = Mathf.Clamp(val, 0f, 0.5f);
			}
			public void ChangeTear(byte tearLv)
			{
				bool flag = tearLv > 3;
				if (flag)
				{
					tearLv = 3;
				}
				this.expression.tearLv = tearLv;
				this.expressionRealtime.tearLv = tearLv;
				this.female.tearsLv = tearLv;
			}
			public void ChangeHohoAka(float val)
			{
				this.expression.hohoAka = val;
				this.expressionRealtime.hohoAka = val;
				this.female.ChangeHohoAkaRate(val);
			}
			public void PrevProgram(XmlMgr xml)
			{
				bool flag = xml.programs.Count == 0;
				if (!flag)
				{
					bool flag2 = this.program == null || string.IsNullOrEmpty(this.program.guid);
					if (flag2)
					{
						this.program = xml.programs[0];
					}
					int i = xml.programs.Count - 1;
					while (i >= 0)
					{
						bool flag3 = xml.programs[i].guid == this.program.guid;
						if (flag3)
						{
							bool flag4 = i == 0;
							if (flag4)
							{
								this.program = xml.programs.Last<ExpressionControl.Program>();
								break;
							}
							this.program = xml.programs[i - 1];
							break;
						}
						else
						{
							i--;
						}
					}
				}
			}
			public void NextProgram(XmlMgr xml)
			{
				bool flag = xml.programs.Count == 0;
				if (!flag)
				{
					bool flag2 = this.program == null || string.IsNullOrEmpty(this.program.guid);
					if (flag2)
					{
						this.program = xml.programs.Last<ExpressionControl.Program>();
					}
					int i = 0;
					while (i < xml.programs.Count)
					{
						bool flag3 = xml.programs[i].guid == this.program.guid;
						if (flag3)
						{
							bool flag4 = i == xml.programs.Count - 1;
							if (flag4)
							{
								this.program = xml.programs[0];
								break;
							}
							this.program = xml.programs[i + 1];
							break;
						}
						else
						{
							i++;
						}
					}
				}
			}
			public void SetProgram(ExpressionControl.Program program)
			{
				bool flag = program == null;
				if (!flag)
				{
					this.program = program;
				}
			}
			public void RestoreEyeSmall()
			{
				bool flag = !Utl.Equals(this.expressionRealtime.eyeSmall, 0f);
				if (flag)
				{
					bool flag2 = this.coEyeSmallSlide != null;
					if (flag2)
					{
						this.instMono.StopCoroutine(this.coEyeSmallSlide);
						this.coEyeSmallSlide = null;
					}
					this.coEyeSmallSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.eyeSmall = val;
						this.expressionRealtime.eyeSmall = val;
						if (isEnd)
						{
							this.coEyeSmallSlide = null;
						}
					}, this.expression.eyeSmall, 0f, 0.1f));
				}
			}
			public void RestoreEyeY()
			{
				bool flag = !Utl.Equals(this.expressionRealtime.eyeY, 0f);
				if (flag)
				{
					bool flag2 = this.coEyeYSlide != null;
					if (flag2)
					{
						this.instMono.StopCoroutine(this.coEyeYSlide);
						this.coEyeYSlide = null;
					}
					this.coEyeYSlide = this.instMono.StartCoroutine(Utl.CoSmoothUpdate(delegate(float val, bool isEnd)
					{
						this.expression.eyeY = val;
						this.expressionRealtime.eyeY = val;
						if (isEnd)
						{
							this.coEyeYSlide = null;
						}
					}, this.expression.eyeY, 0f, 0.2f));
				}
			}
			public void ProcEyeSmall()
			{
				bool flag = this.expression.eyeSmall > 0f;
				if (flag)
				{
					this.isEyeSmallZeroProc = true;
				}
				bool flag2 = this.isEyeSmallZeroProc;
				if (flag2)
				{
					bool flag3 = this.expression.eyeSmall <= 0f;
					if (flag3)
					{
						this.isEyeSmallZeroProc = false;
					}
					foreach (EyeLookMaterialControll eyeLookMaterialControll in this.female.eyeLookMatCtrl)
					{
						foreach (EyeLookMaterialControll.TexState texState in eyeLookMaterialControll.texStates)
						{
							Vector2 vector = eyeLookMaterialControll._renderer.material.GetTextureScale(texState.texID);
							vector.x += this.expression.eyeSmall;
							vector.y += this.expression.eyeSmall;
							eyeLookMaterialControll._renderer.material.SetTextureScale(texState.texID, vector);
							vector = eyeLookMaterialControll._renderer.material.GetTextureOffset(texState.texID);
							vector.x -= this.expression.eyeSmall / 2f;
							vector.y -= this.expression.eyeSmall / 2f;
							eyeLookMaterialControll._renderer.material.SetTextureOffset(texState.texID, vector);
						}
					}
				}
			}
			public void ProcEyeY()
			{
				bool flag = this.expression.eyeY > 0f;
				if (flag)
				{
					this.isEyeYZeroProc = true;
				}
				bool flag2 = this.isEyeYZeroProc;
				if (flag2)
				{
					bool flag3 = this.expression.eyeY <= 0f;
					if (flag3)
					{
						this.isEyeYZeroProc = false;
					}
					foreach (EyeLookMaterialControll eyeLookMaterialControll in this.female.eyeLookMatCtrl)
					{
						foreach (EyeLookMaterialControll.TexState texState in eyeLookMaterialControll.texStates)
						{
							Vector2 textureOffset = eyeLookMaterialControll._renderer.material.GetTextureOffset(texState.texID);
							textureOffset.y -= this.expression.eyeY;
							eyeLookMaterialControll._renderer.material.SetTextureOffset(texState.texID, textureOffset);
						}
					}
				}
			}
			public OCIChar ociFemale;
			public ChaControl female;
			public ExpressionControl.Expression expression;
			public ExpressionControl.Expression expressionRealtime;
			public ExpressionControl.Expression expressionPrev;
			public ExpressionControl.Program program;
			private HandCtrl handCtrl;
			private int voiceIndex;
			public float programTimer;
			public float programTimerNow;
			public bool lockExpression;
			public bool lockEyebrowPtn;
			public bool lockEyePtn;
			public bool lockEyeOpen;
			public bool lockMouthPtn;
			public bool lockMouthOpen;
			public bool lockBlink;
			public bool lockHighlight;
			public bool lockEyeY;
			public bool lockEyeSmall;
			public bool lockTear;
			public bool lockHohoAka;
			public bool isEyeYZeroProc;
			public bool isEyeSmallZeroProc;
			public Coroutine coEyeOpenSlide;
			public Coroutine coMouthOpenSlide;
			public Coroutine coHohoAkaSlide;
			public Coroutine coEyeYSlide;
			public Coroutine coEyeSmallSlide;
		}
		public enum GUIState
		{
			normal,
			saveNew,
			update,
			delete
		}
	}
}
