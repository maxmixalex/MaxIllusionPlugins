using System;
using System.IO;
using System.Text;
using System.Xml;
using BepInEx;
using ExpressionControl;
using JuiceControl;
using MaxIllusionPlugins.Common;

internal static class XmlCompatibilityTests {
    private const string NilNamespace = " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\"";

    internal static void JuiceNilRoot() {
        InDirectory(delegate(string directory) {
            string path = Path.Combine(directory, "juice.xml");
            string xml = "<JuiceControlPresets" + NilNamespace + "/>";
            File.WriteAllText(path, xml);
            PresetStore store = new PresetStore();
            store.Load(path);
            Check(store.CanSave && store.Data.CharacterPresets.Count == 0 && store.Data.GroupPresets.Count == 0,
                "An explicitly nil root represents an empty preset document, as in the original loader.");
            Check(File.ReadAllText(path) == xml, "Loading an empty document must not rewrite it.");
            store.Data.CharacterPresets.Add(new CharacterPreset { Name = "First preset" });
            store.Save();
            Check(PresetXml.Load(path).CharacterPresets[0].Name == "First preset", "A nil document remains editable after loading.");
        });
    }

    internal static void ExpressionNilRoot() {
        InDirectory(delegate(string directory) {
            Paths.ConfigPath = directory;
            string path = Path.Combine(directory, "MaxMixAlex.ExpressionControl.presets.xml");
            string xml = "<root" + NilNamespace + "/>";
            File.WriteAllText(path, xml);
            XmlMgr model = new XmlMgr();
            model.Load();
            Check(model.keyConfig.showGUI == "L" && model.expressions.Count == 0 && model.programs.Count == 0 && model.scenePresets.Count == 0,
                "An explicitly nil document must initialize the shortcut and all lists.");
            Check(File.ReadAllText(path) == xml, "Loading must preserve the source until an explicit save.");
        });
    }

    internal static void EncodingsAndEmptyRoots() {
        InDirectory(delegate(string directory) {
            Encoding[] encodings = { new UTF8Encoding(false), new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode };
            for (int i = 0; i < encodings.Length; i++) {
                string path = Path.Combine(directory, "encoding-" + i + ".xml");
                string xml = "<?xml version=\"1.0\" encoding=\"" + encodings[i].WebName + "\"?><JuiceControlPresets><CharacterPresets><Preset name=\"雨の日 雪\"/></CharacterPresets></JuiceControlPresets>";
                File.WriteAllText(path, xml, encodings[i]);
                Check(PresetXml.Load(path).CharacterPresets[0].Name == "雨の日 雪", "BOM and declared encoding must agree: " + encodings[i].WebName);
            }
            string empty = Path.Combine(directory, "empty-root.xml");
            File.WriteAllText(empty, "<JuiceControlPresets /><!-- valid trailing comment -->\n");
            Check(PresetXml.Load(empty).CharacterPresets.Count == 0, "An empty element is a valid empty preset library.");
            Paths.ConfigPath = directory;
            File.WriteAllText(Path.Combine(directory, "MaxMixAlex.ExpressionControl.presets.xml"), "<root />");
            XmlMgr expression = new XmlMgr(); expression.Load();
            Check(expression.keyConfig.showGUI == "L", "An empty expression element keeps the default shortcut.");
        });
    }

    internal static void InvalidDocumentsPreserveStore() {
        InDirectory(delegate(string directory) {
            string path = Path.Combine(directory, "juice.xml");
            PresetStore store = new PresetStore(); store.Load(path);
            store.Data.CharacterPresets.Add(new CharacterPreset { Name = "Last good model" }); store.Save();
            PresetXml previous = store.Data;
            string[] invalid = {
                "", "\ufeff", "<?xml version=\"1.0\"?>", "<JuiceControlPresets>",
                "<JuiceControlPresets>\u0001</JuiceControlPresets>", "<root />",
                "<!DOCTYPE JuiceControlPresets [<!ENTITY value 'injected'>]><JuiceControlPresets>&value;</JuiceControlPresets>",
                "<JuiceControlPresets><CharacterPresets><Preset><State tear=\"invalid\"/></Preset></CharacterPresets></JuiceControlPresets>",
                "<JuiceControlPresets /><!-- trailing comment -->not XML",
                "<JuiceControlPresets /><!-- trailing comment --><second />"
            };
            for (int i = 0; i < invalid.Length; i++) {
                File.WriteAllText(path, invalid[i], new UTF8Encoding(false));
                byte[] rejectedBytes = File.ReadAllBytes(path);
                MustThrow(delegate { store.Load(path); });
                Check(!store.CanSave && ReferenceEquals(store.Data, previous), "Rejected input must retain the previous model and disable writes, case " + i);
                MustThrow(store.Save);
                Check(SameBytes(File.ReadAllBytes(path), rejectedBytes), "Rejected XML must remain byte-for-byte available for repair, case " + i);
            }
        });
    }

    internal static void FailedExpressionReloadBlocksSave() {
        InDirectory(delegate(string directory) {
            Paths.ConfigPath = directory;
            string path = Path.Combine(directory, "MaxMixAlex.ExpressionControl.presets.xml");
            XmlMgr model = new XmlMgr(); model.Load();
            model.expressions.Add(new ExpressionControl.ExpressionControl.Expression("saved", "keep")); model.Save(true);
            File.WriteAllText(path, "<root><Expressions>");
            MustThrow(model.Load);
            MustThrow(delegate { model.Save(true); });
            Check(File.ReadAllText(path) == "<root><Expressions>", "Saving after a failed reload must not replace the unreadable document.");
            File.WriteAllText(path, "<root />"); model.Load(); model.Save(true);
            Check(AtomicXmlFile.Read<XmlMgr>(path).keyConfig.showGUI == "L", "A successful reload restores writes.");
            File.WriteAllText(path, "<root>"); MustThrow(model.Load); File.Delete(path);
            model.Load(); model.Save(true);
            Check(model.expressions.Count == 0, "Explicit reload can recreate a removed broken file with defaults.");
        });
    }

    internal static void LiteralFileNames() {
        InDirectory(delegate(string directory) {
            string[] names = { "preset #1.xml", "preset%20.xml", "雨 & 雪.xml" };
            for (int i = 0; i < names.Length; i++) {
                string path = Path.Combine(directory, names[i]);
                AtomicXmlFile.Write(path, new Sample { Text = names[i] });
                Check(AtomicXmlFile.Read<Sample>(path).Text == names[i], "A filename must be read literally: " + names[i]);
            }
        });
    }

    internal static void DiagnosticCause() {
        XmlException cause = new XmlException("Invalid character in preset.", null, 3, 7);
        string details = XmlFileDiagnostics.Describe(new InvalidOperationException("There is an error in XML document.", cause));
        Check(details.Contains("XmlException") && details.Contains("Invalid character") && details.Contains("3") && details.Contains("7"),
            "The display must expose the parser's cause and location, not just the outer serializer message.");
        Check(XmlFileDiagnostics.Describe(null) == string.Empty, "No failure has no diagnostic text.");
    }

    private static void InDirectory(Action<string> test) {
        string directory = Path.Combine(Path.GetTempPath(), "MaxIllusionPlugins-xml-" + Guid.NewGuid().ToString("N"));
        string previous = Paths.ConfigPath;
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { Paths.ConfigPath = previous; Directory.Delete(directory, true); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool SameBytes(byte[] first, byte[] second) {
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }
    private static void MustThrow(Action action) {
        try { action(); } catch { return; }
        throw new Exception("Expected rejected input to remain protected.");
    }
}
