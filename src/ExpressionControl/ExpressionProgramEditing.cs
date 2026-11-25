using System;

namespace ExpressionControl {
	public partial class ExpressionControl {
		private bool XML_DeleteProgram()
		{
			for (int i = this.xml.programs.Count - 1; i >= 0; i--)
			{
				bool flag = this.xml.programs[i].guid == this.currentProgram.guid;
				if (flag)
				{
					string deletedGuid = this.currentProgram.guid;
					this.xml.programs.RemoveAt(i);
					// The editor may move to a program with fewer units. Reset both
					// selections before the next GUI pass indexes that new program.
					this.selectedProgramUnit = 0;
					this.currentProgramNum = Math.Max(0, i - 1);
					foreach (ExpressionControl.ScenePreset scenePreset in this.xml.scenePresets)
					{
						foreach (ExpressionControl.SceneRolePreset role in scenePreset.roles)
						{
							if (role.programGuid == deletedGuid) role.programGuid = string.Empty;
						}
					}
					bool flag2 = this.xml.programs.Count == 0;
					if (flag2)
					{
						this.currentProgram = new ExpressionControl.Program();
						this.currentProgram.name = "- - -";
					}
					else
					{
						this.currentProgram = this.xml.programs[this.currentProgramNum].DeepClone<ExpressionControl.Program>();
					}
					this.xml.Save(true);
					return true;
				}
			}
			return false;
		}
	}
}
