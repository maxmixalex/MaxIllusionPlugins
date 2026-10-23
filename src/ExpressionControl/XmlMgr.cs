using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using BepInEx;
namespace ExpressionControl
{
	[XmlRoot("root")]
	public class XmlMgr
	{
		public XmlMgr()
		{
			this.keyConfig = new XmlMgr.KeyConfig();
			this.expressions = new List<ExpressionControl.Expression>();
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
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(XmlMgr));
			string filePath = XmlMgr.GetFilePath();
			XmlMgr.EnsureDirectory(filePath);
			using (StreamWriter streamWriter = new StreamWriter(filePath, false, new UTF8Encoding(false)))
			{
				xmlSerializer.Serialize(streamWriter, this);
			}
			if (load)
			{
				this.LoadFinish();
			}
		}
		public void Load()
		{
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(XmlMgr));
			XmlMgr xmlMgr;
			using (StreamReader streamReader = new StreamReader(XmlMgr.GetFilePath(), new UTF8Encoding(false)))
			{
				xmlMgr = (XmlMgr)xmlSerializer.Deserialize(streamReader);
			}
			this.keyConfig = xmlMgr.keyConfig;
			this.expressions = xmlMgr.expressions;
			this.programs = xmlMgr.programs;
			this.scenePresets = xmlMgr.scenePresets;
			this.LoadFinish();
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
			foreach (ExpressionControl.ScenePreset scenePreset in this.scenePresets)
			{
				if (scenePreset.roles == null)
				{
					scenePreset.roles = new List<ExpressionControl.SceneRolePreset>();
				}
			}
			bool flag = this.expressions == null;
			if (flag)
			{
				this.expressions = new List<ExpressionControl.Expression>();
			}
			else
			{
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
				return p.guid.GetHashCode();
			}
		}
	}
}
