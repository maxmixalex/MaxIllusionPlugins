using System;
using System.IO;
using System.Xml.Serialization;
using BepInEx;
using ExpressionControl;
using JuiceControl;

// Explicit local diagnostic mode. Personal presets are copied to a temporary
// directory; they are never embedded in the test assembly or written in place.
internal static class PresetFileValidation {
    internal static int Run(string[] args) {
        if (args.Length != 2 || args[0] != "--validate-presets") {
            Console.Error.WriteLine("Usage: PluginCore.Regression --validate-presets <config-directory>");
            return 2;
        }
        string temporary = Path.Combine(Path.GetTempPath(), "preset-validation-" + Guid.NewGuid().ToString("N"));
        string previousConfigPath = Paths.ConfigPath;
        Directory.CreateDirectory(temporary);
        try {
            string juice = Copy(args[1], temporary, "MaxMixAlex.JuiceControl.presets.xml");
            PresetStore store = new PresetStore();
            store.Load(juice);
            string original = Snapshot(store.Data);
            store.Save();
            store.Load(juice);
            Require(original == Snapshot(store.Data), "Juice preset values changed after a save/reload.");
            Console.WriteLine("PASS Juice: " + store.Data.CharacterPresets.Count + " character presets, " + store.Data.GroupPresets.Count + " groups; all serialized values preserved.");

            Copy(args[1], temporary, "MaxMixAlex.ExpressionControl.presets.xml");
            Paths.ConfigPath = temporary;
            XmlMgr expression = new XmlMgr();
            expression.Load();
            original = Snapshot(expression);
            expression.Save(true);
            expression.Load();
            Require(original == Snapshot(expression), "Expression preset values changed after a save/reload.");
            Console.WriteLine("PASS Expression: " + expression.expressions.Count + " expressions, " + expression.programs.Count + " programs, " + expression.scenePresets.Count + " scene presets; all serialized values preserved.");
            Console.WriteLine("Only temporary copies were saved. Original files were not opened for writing.");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        } finally {
            Paths.ConfigPath = previousConfigPath;
            Directory.Delete(temporary, true);
        }
    }

    private static string Copy(string source, string temporary, string name) {
        string path = Path.Combine(temporary, name);
        File.Copy(Path.Combine(source, name), path);
        return path;
    }
    private static string Snapshot<T>(T value) {
        using (StringWriter writer = new StringWriter()) {
            new XmlSerializer(typeof(T)).Serialize(writer, value);
            return writer.ToString();
        }
    }
    private static void Require(bool condition, string message) {
        if (!condition) throw new InvalidDataException(message);
    }
}
