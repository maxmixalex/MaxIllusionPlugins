using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ExpressionControl {
	public partial class ExpressionControl {
		[Serializable]
		public class Expression
		{
			public Expression()
			{
			}
			public Expression(string guid, string name)
			{
				this.guid = guid;
				this.name = name;
				this.blink = true;
			}
			public Expression(string guid, string name, ExpressionControl.Expression expression)
			{
				this.guid = guid;
				this.name = name;
				this.eyebrowPtn = expression.eyebrowPtn;
				this.eyesPtn = expression.eyesPtn;
				this.mouthPtn = expression.mouthPtn;
				this.hideHighlight = expression.hideHighlight;
				this.blink = expression.blink;
				this.eyeOpen = expression.eyeOpen;
				this.mouthOpen = expression.mouthOpen;
				this.hohoAka = expression.hohoAka;
				this.tearLv = expression.tearLv;
				this.eyeY = expression.eyeY;
				this.eyeSmall = expression.eyeSmall;
			}
			public Expression(ExpressionControl.Expression expression)
			{
				this.guid = expression.guid;
				this.name = expression.name;
				this.eyebrowPtn = expression.eyebrowPtn;
				this.eyesPtn = expression.eyesPtn;
				this.mouthPtn = expression.mouthPtn;
				this.hideHighlight = expression.hideHighlight;
				this.blink = expression.blink;
				this.eyeOpen = expression.eyeOpen;
				this.mouthOpen = expression.mouthOpen;
				this.hohoAka = expression.hohoAka;
				this.tearLv = expression.tearLv;
				this.eyeY = expression.eyeY;
				this.eyeSmall = expression.eyeSmall;
			}
			public Expression(ChaControl female, float eyeY = 0f, float eyeSmall = 0f)
			{
				this.guid = string.Empty;
				this.name = "- - -";
				this.eyebrowPtn = female.GetEyebrowPtn();
				this.eyesPtn = female.GetEyesPtn();
				this.mouthPtn = female.GetMouthPtn();
				this.hideHighlight = female.fileStatus.hideEyesHighlight;
				this.blink = female.GetEyesBlinkFlag();
				this.eyeOpen = female.GetEyesOpenMax();
				this.mouthOpen = female.GetMouthOpenMax();
				this.hohoAka = female.fileStatus.hohoAkaRate;
				this.tearLv = female.tearsLv;
				this.eyeY = eyeY;
				this.eyeSmall = eyeSmall;
			}
			public Expression(FaceListCtrl.FaceInfo faceInfo)
			{
				this.guid = string.Empty;
				this.name = "- - -";
				this.eyebrowPtn = faceInfo.eyebrow;
				this.eyesPtn = faceInfo.eye;
				this.mouthPtn = faceInfo.mouth;
				this.hideHighlight = !faceInfo.highlight;
				this.blink = !faceInfo.eyesblink;
				this.eyeOpen = faceInfo.openMaxEye;
				this.mouthOpen = faceInfo.openMinMouth;
				this.hohoAka = faceInfo.cheek;
				this.tearLv = (byte)faceInfo.tears;
			}
			[XmlAttribute("guid")]
			public string guid = string.Empty;
			[XmlAttribute("name")]
			public string name = string.Empty;
			[XmlAttribute("eyebrowPtn")]
			public int eyebrowPtn;
			[XmlAttribute("eyesPtn")]
			public int eyesPtn;
			[XmlAttribute("mouthPtn")]
			public int mouthPtn;
			[XmlAttribute("hideHighlight")]
			public bool hideHighlight;
			[XmlAttribute("blink")]
			public bool blink;
			[XmlAttribute("eyeOpen")]
			public float eyeOpen;
			[XmlAttribute("mouthOpen")]
			public float mouthOpen;
			[XmlAttribute("hohoAka")]
			public float hohoAka;
			[XmlAttribute("tearLv")]
			public byte tearLv;
			[XmlAttribute("eyeY")]
			public float eyeY;
			[XmlAttribute("eyeSmall")]
			public float eyeSmall;
		}
		[Serializable]
		public class Program
		{
			[XmlAttribute("guid")]
			public string guid = string.Empty;
			[XmlAttribute("name")]
			public string name = "- - -";
			[XmlAttribute("applyMouthOpen")]
			public bool applyMouthOpen;
			[XmlAttribute("ignoreMouthInFera")]
			public bool ignoreMouthInFera;
			[XmlAttribute("ignoreKiss")]
			public bool ignoreKiss;
			[XmlElement(Type = typeof(ExpressionControl.Program.Unit), ElementName = "unit")]
			public List<ExpressionControl.Program.Unit> units = new List<ExpressionControl.Program.Unit>();
			[Serializable]
			public class Unit
			{
				[XmlAttribute("aibu")]
				public bool aibu;
				[XmlAttribute("houshi")]
				public bool houshi;
				[XmlAttribute("sounyuu")]
				public bool sounyuu;
				[XmlAttribute("fera")]
				public bool fera;
				[XmlAttribute("masturbation")]
				public bool masturbation;
				[XmlAttribute("lesbian")]
				public bool lesbian;
				[XmlAttribute("idle")]
				public bool idle;
				[XmlAttribute("kiss")]
				public bool kiss;
				[XmlAttribute("loopW")]
				public bool loopW;
				[XmlAttribute("loopS")]
				public bool loopS;
				[XmlAttribute("spurtM")]
				public bool spurtM;
				[XmlAttribute("spurtF")]
				public bool spurtF;
				[XmlAttribute("orgazumM")]
				public bool orgazumM;
				[XmlAttribute("orgazumF")]
				public bool orgazumF;
				[XmlAttribute("afterOrgazumM")]
				public bool afterOrgazumM;
				[XmlAttribute("afterOrgazumF")]
				public bool afterOrgazumF;
				[XmlAttribute("drinkVomitWait")]
				public bool drinkVomitWait;
				[XmlAttribute("feelingL")]
				public int feelingL;
				[XmlAttribute("feelingH")]
				public int feelingH;
				[XmlAttribute("orgazumNumL")]
				public int orgazumNumL;
				[XmlAttribute("orgazumNumH")]
				public int orgazumNumH;
				[XmlAttribute("morph")]
				public bool morph;
				[XmlAttribute("tear")]
				public bool tear;
				[XmlAttribute("hoho")]
				public bool hoho;
				[XmlAttribute("highlight")]
				public bool highlight;
				[XmlAttribute("eyeY")]
				public bool eyeY;
				[XmlAttribute("eyeSmall")]
				public bool eyeSmall;
				[XmlElement(Type = typeof(string), ElementName = "guid")]
				public List<string> expressionGuids = new List<string>();
			}
		}
		public enum PartType
		{
			eyebrow,
			eyes,
			mouth
		}
		public enum ExpressionPart
		{
			morph,
			tear,
			hoho,
			highlight,
			eyeY,
			eyeSmall
		}
		public enum FemaleState
		{
			other,
			idle,
			kiss,
			loopW,
			loopS,
			spurtM,
			spurtF,
			orgazumM,
			orgazumF,
			afterOrgazumM,
			afterOrgazumF,
			drinkVomitWait
		}
		[Serializable]
		public class ScenePreset
		{
			[XmlAttribute("guid")]
			public string guid = string.Empty;
			[XmlAttribute("name")]
			public string name = string.Empty;
			[XmlElement("Role")]
			public List<ExpressionControl.SceneRolePreset> roles = new List<ExpressionControl.SceneRolePreset>();
		}
		[Serializable]
		public class SceneRolePreset
		{
			[XmlAttribute("role")]
			public string role = string.Empty;
			[XmlAttribute("displayName")]
			public string displayName = string.Empty;
			[XmlAttribute("programGuid")]
			public string programGuid = string.Empty;
			[XmlAttribute("programTimer")]
			public float programTimer = 20f;
		}
	}
}
