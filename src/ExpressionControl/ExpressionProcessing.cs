using System.Collections.Generic;
using UnityEngine;

namespace ExpressionControl {
	public partial class ExpressionControl {
		internal void ProcessExpressions()
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
						femaleData2.UpdateState(this.updateState, this.nowMotion, this.isFeraAction);
						femaleData2.CheckExpression(false);
						// Native SetFace may already have refreshed FemaleData.state. The
						// pending scene transition still owns one program evaluation here,
						// including a new action whose coarse FemaleState is unchanged.
						bool flag10 = this.updateState;
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

	}
}
