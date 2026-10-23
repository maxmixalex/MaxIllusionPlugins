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
				// Keep the editor available so the user can see the error and reload
				// a repaired file without restarting the game. XmlMgr blocks writes.
				this.presetReadError = ex;
				Debug.LogError("ExpressionControl: failed to load presets. " + ex);
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
		public void ReStart()
		{
			this.pendingGuiToggle = false;
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
					this.HideMaximumUi();
				}
			}
			else
			{
				bool flag5 = !ExpressionControl.isHScene;
				if (flag5)
				{
					ExpressionControl.isHScene = true;
					this.HideMaximumUi();
				}
				this.UpdateFemale();
				ExpressionControl.hFrag = ExpressionControl.hsceneProc.flags ?? FindObjectOfType<HFlag>();
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
		private ShortCutKeyMgr hotkey;
		private static readonly string hotkeyShowGUI = "showGUI";
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
		private int selectedProgramUnit;
		private bool isSortExpression;
		private bool isSortExpressionModify;
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
