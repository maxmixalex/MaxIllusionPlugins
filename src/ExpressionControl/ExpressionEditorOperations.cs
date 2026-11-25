using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExpressionControl {
    public partial class ExpressionControl {
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
    }
}
