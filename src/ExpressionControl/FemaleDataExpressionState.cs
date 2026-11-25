namespace ExpressionControl {
	public partial class ExpressionControl {
		public partial class FemaleData {
			public bool CheckExpression(bool useLock)
			{
				bool result = false;
				int num = this.female.GetEyebrowPtn();
				bool flag = this.expressionRealtime.eyebrowPtn != num;
				if (flag)
				{
					result = true;
					bool flag2 = useLock && (this.lockExpression || this.lockEyebrowPtn);
					if (flag2)
					{
						this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, false);
						this.female.fbsCtrl.EyebrowCtrl.CalculateBlendShape();
						this.female.ChangeEyebrowPtn(this.expression.eyebrowPtn, true);
						this.expressionRealtime.eyebrowPtn = this.expression.eyebrowPtn;
					}
					else
					{
						this.expressionRealtime.eyebrowPtn = num;
					}
				}
				num = this.female.GetEyesPtn();
				bool flag3 = this.expressionRealtime.eyesPtn != num;
				if (flag3)
				{
					result = true;
					bool flag4 = useLock && (this.lockExpression || this.lockEyePtn);
					if (flag4)
					{
						this.female.ChangeEyesPtn(this.expression.eyesPtn, false);
						this.female.fbsCtrl.EyesCtrl.CalculateBlendShape();
						this.female.ChangeEyesPtn(this.expression.eyesPtn, true);
						this.expressionRealtime.eyesPtn = this.expression.eyesPtn;
					}
					else
					{
						this.expressionRealtime.eyesPtn = num;
					}
				}
				num = this.female.GetMouthPtn();
				bool flag5 = this.expressionRealtime.mouthPtn != num;
				if (flag5)
				{
					result = true;
					bool flag6 = !this.ignoreMouthLoad && useLock && (this.lockExpression || this.lockMouthPtn);
					if (flag6)
					{
						this.female.ChangeMouthPtn(this.expression.mouthPtn, false);
						this.female.fbsCtrl.MouthCtrl.CalculateBlendShape();
						this.female.ChangeMouthPtn(this.expression.mouthPtn, true);
						this.expressionRealtime.mouthPtn = this.expression.mouthPtn;
					}
					else
					{
						this.expressionRealtime.mouthPtn = num;
					}
				}
				bool flag7 = this.coEyeOpenSlide == null;
				if (flag7)
				{
					float eyesOpenMax = this.female.GetEyesOpenMax();
					bool flag8 = !Utl.Equals(this.expressionRealtime.eyeOpen, eyesOpenMax);
					if (flag8)
					{
						result = true;
						bool flag9 = useLock && (this.lockExpression || this.lockEyeOpen);
						if (flag9)
						{
							bool flag10 = this.faceListCtrl != null;
							if (flag10)
							{
								Utl.GetFieldValue<FaceListCtrl, GlobalMethod.FloatBlend>(this.faceListCtrl, "blendEye").Start(this.expression.eyeOpen, this.expression.eyeOpen, 0f);
							}
							this.female.ChangeEyesOpenMax(this.expression.eyeOpen);
							this.expressionRealtime.eyeOpen = this.expression.eyeOpen;
						}
						else
						{
							this.expressionRealtime.eyeOpen = eyesOpenMax;
						}
					}
				}
				bool flag11 = this.coMouthOpenSlide == null;
				if (flag11)
				{
					float mouthOpenMax = this.female.GetMouthOpenMax();
					bool flag12 = !Utl.Equals(this.expressionRealtime.mouthOpen, mouthOpenMax);
					if (flag12)
					{
						result = true;
						bool flag13 = !this.ignoreMouthOpen && !this.ignoreMouthLoad && useLock && (this.lockExpression || this.lockMouthOpen);
						if (flag13)
						{
							bool flag14 = this.faceListCtrl != null;
							if (flag14)
							{
								Utl.GetFieldValue<FaceListCtrl, GlobalMethod.FloatBlend>(this.faceListCtrl, "blendMouth").Start(this.expression.mouthOpen, this.expression.mouthOpen, 0f);
							}
							this.female.ChangeMouthOpenMax(this.expression.mouthOpen);
							bool isStudio = ExpressionControl.isStudio;
							if (isStudio)
							{
								this.ociFemale.ChangeMouthOpen(this.expression.mouthOpen);
							}
							this.expressionRealtime.mouthOpen = this.expression.mouthOpen;
						}
						else
						{
							this.expressionRealtime.mouthOpen = mouthOpenMax;
						}
					}
				}
				bool flag15 = this.expressionRealtime.tearLv != this.female.tearsLv;
				if (flag15)
				{
					result = true;
					bool flag16 = useLock && (this.lockExpression || this.lockTear);
					if (flag16)
					{
						this.female.tearsLv = this.expression.tearLv;
						this.expressionRealtime.tearLv = this.expression.tearLv;
					}
					else
					{
						this.expressionRealtime.tearLv = this.female.tearsLv;
					}
				}
				bool flag17 = this.coHohoAkaSlide == null;
				if (flag17)
				{
					float hohoAkaRate = this.female.fileStatus.hohoAkaRate;
					bool flag18 = !Utl.Equals(this.expressionRealtime.hohoAka, hohoAkaRate);
					if (flag18)
					{
						result = true;
						bool flag19 = useLock && (this.lockExpression || this.lockHohoAka);
						if (flag19)
						{
							this.female.ChangeHohoAkaRate(this.expression.hohoAka);
							this.expressionRealtime.hohoAka = this.expression.hohoAka;
						}
						else
						{
							this.expressionRealtime.hohoAka = hohoAkaRate;
						}
					}
				}
				bool flag20 = this.expressionRealtime.hideHighlight != this.female.fileStatus.hideEyesHighlight;
				if (flag20)
				{
					result = true;
					bool flag21 = useLock && (this.lockExpression || this.lockHighlight);
					if (flag21)
					{
						this.female.HideEyeHighlight(this.expression.hideHighlight);
						this.expressionRealtime.hideHighlight = this.expression.hideHighlight;
					}
					else
					{
						this.expressionRealtime.hideHighlight = this.female.fileStatus.hideEyesHighlight;
					}
				}
				bool flag22 = this.expressionRealtime.blink != this.female.GetEyesBlinkFlag();
				if (flag22)
				{
					result = true;
					bool flag23 = useLock && (this.lockExpression || this.lockBlink);
					if (flag23)
					{
						this.female.ChangeEyesBlinkFlag(this.expression.blink);
						this.expressionRealtime.blink = this.expression.blink;
					}
					else
					{
						this.expressionRealtime.blink = this.female.GetEyesBlinkFlag();
					}
				}
				return result;
			}
		}
	}
}
