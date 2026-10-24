using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using BepInEx;
using ClothingCompression;
using MaxIllusionPlugins.Common;

// The explicit path may use any filename so old documents can be checked before
// the user renames them. This is a test utility, not a production fallback path.
internal static class PresetFileValidation {
    internal static int Run(string[] args) {
        if (args.Length != 2 || args[0] != "--validate-presets") {
            Console.Error.WriteLine("Usage: AccessoryClothing.Regression --validate-presets <clothing-xml-path>");
            return 2;
        }
        string temporary = Path.Combine(Path.GetTempPath(), "clothing-validation-" + Guid.NewGuid().ToString("N"));
        string previousConfigPath = Paths.ConfigPath;
        Directory.CreateDirectory(temporary);
        try {
            Paths.ConfigPath = temporary;
            CompressionLibraryStore store = new CompressionLibraryStore();
            File.Copy(args[1], store.Path);
            if (!store.Reload()) throw new InvalidDataException("The production clothing loader rejected the copied document.");
            string original = Snapshot(store.Data);
            AtomicXmlFile.Write(store.Path, store.Data, store.Path + ".bak");
            if (!store.Reload() || Snapshot(store.Data) != original)
                throw new InvalidDataException("Clothing preset values changed after a save/reload.");
            Console.WriteLine("PASS Clothing: " + store.Data.Profiles.Count + " profiles, " + store.Data.Profiles.Sum(profile => profile.Entries.Count) + " deformations; all serialized values preserved.");
            Console.WriteLine("Production filename: " + Path.GetFileName(store.Path));
            Console.WriteLine("Only a temporary copy was saved. The original file was not opened for writing.");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        } finally {
            Paths.ConfigPath = previousConfigPath;
            Directory.Delete(temporary, true);
        }
    }

    private static string Snapshot(CompressionLibrary value) {
        using (StringWriter writer = new StringWriter()) {
            new XmlSerializer(typeof(CompressionLibrary)).Serialize(writer, value);
            return writer.ToString();
        }
    }
}
