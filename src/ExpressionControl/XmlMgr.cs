using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using BepInEx;
using MaxIllusionPlugins.Common;
namespace ExpressionControl
{
	[XmlRoot("root")]
	public class XmlMgr
	{
		public XmlMgr()
		{
			this.keyConfig = new XmlMgr.KeyConfig();
			this.expressions = new List<ExpressionControl.Expression>();
		}
		private void EnsureFile()
		{
			string filePath = XmlMgr.GetFilePath();
			bool flag = !File.Exists(filePath);
			if (flag)
			{
				string legacyPath = XmlMgr.GetLegacyFilePath();
				XmlMgr.EnsureDirectory(filePath);
				if (File.Exists(legacyPath))
				{
					File.Copy(legacyPath, filePath, false);
				}
				else
				{
					this.Save(false);
				}
			}
		}
		public void Save(bool load)
		{
			if (load)
			{
				this.LoadFinish();
			}
			string filePath = XmlMgr.GetFilePath();
			AtomicXmlFile.Write(filePath, this);
		}
		public void Load()
		{
			this.EnsureFile();
			XmlMgr xmlMgr = AtomicXmlFile.Read<XmlMgr>(XmlMgr.GetFilePath());
			if (xmlMgr == null) throw new InvalidDataException("Expression preset XML is empty.");
			// Normalize before publishing the new state. A failed reload leaves the
			// currently selected presets and the original file intact.
			xmlMgr.LoadFinish();
			this.keyConfig = xmlMgr.keyConfig;
			this.expressions = xmlMgr.expressions;
			this.programs = xmlMgr.programs;
			this.scenePresets = xmlMgr.scenePresets;
		}
		private void LoadFinish()
		{
			if (this.keyConfig == null)
			{
				this.keyConfig = new XmlMgr.KeyConfig();
			}
			if (this.programs == null)
			{
				this.programs = new List<ExpressionControl.Program>();
			}
			if (this.scenePresets == null)
			{
				this.scenePresets = new List<ExpressionControl.ScenePreset>();
			}
			this.programs.RemoveAll(program => program == null);
			foreach (ExpressionControl.Program program in this.programs)
			{
				if (program.units == null) program.units = new List<ExpressionControl.Program.Unit>();
				program.units.RemoveAll(unit => unit == null);
				foreach (ExpressionControl.Program.Unit unit in program.units)
				{
					if (unit.expressionGuids == null) unit.expressionGuids = new List<string>();
				}
			}
			this.scenePresets.RemoveAll(preset => preset == null);
			foreach (ExpressionControl.ScenePreset scenePreset in this.scenePresets)
			{
				if (scenePreset.roles == null)
				{
					scenePreset.roles = new List<ExpressionControl.SceneRolePreset>();
				}
				scenePreset.roles.RemoveAll(role => role == null);
			}
			bool flag = this.expressions == null;
			if (flag)
			{
				this.expressions = new List<ExpressionControl.Expression>();
			}
			else
			{
				this.expressions.RemoveAll(expression => expression == null);
				foreach (ExpressionControl.Expression expression in this.expressions)
				{
					// Missing IDs in hand-edited/legacy XML must not collapse unrelated
					// expressions into one entry during de-duplication.
					if (string.IsNullOrEmpty(expression.guid)) expression.guid = Guid.NewGuid().ToString();
				}
				this.expressions = this.expressions.Distinct(new XmlMgr.ExpressionComparer()).ToList<ExpressionControl.Expression>();
			}
		}
		private const string DefaultPresetFileName = "MaxMixAlex.ExpressionControl.presets.xml";
		private static string GetFilePath()
		{
			return Path.Combine(Paths.ConfigPath, XmlMgr.DefaultPresetFileName);
		}
		private static string GetLegacyFilePath()
		{
			return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ExpressionControlPlugin.Name + ".xml");
		}
		private static void EnsureDirectory(string filePath)
		{
			string directoryName = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
		}
		[XmlElement("KeyConfig")]
		public XmlMgr.KeyConfig keyConfig;
		[XmlArray("Expressions")]
		[XmlArrayItem("item")]
		public List<ExpressionControl.Expression> expressions;
		[XmlArray("Programs")]
		[XmlArrayItem("Program")]
		public List<ExpressionControl.Program> programs = new List<ExpressionControl.Program>();
		[XmlArray("ScenePresets")]
		[XmlArrayItem("ScenePreset")]
		public List<ExpressionControl.ScenePreset> scenePresets = new List<ExpressionControl.ScenePreset>();
		public class KeyConfig
		{
			public string showGUI { get; set; }
			public KeyConfig()
			{
				this.showGUI = "L";
			}
		}
		private class ExpressionComparer : IEqualityComparer<ExpressionControl.Expression>
		{
			public bool Equals(ExpressionControl.Expression x, ExpressionControl.Expression y)
			{
				return (x == null && y == null) || (x != null && y != null && x.guid == y.guid);
			}
			public int GetHashCode(ExpressionControl.Expression p)
			{
				return p == null || p.guid == null ? 0 : p.guid.GetHashCode();
			}
		}
	}
}
