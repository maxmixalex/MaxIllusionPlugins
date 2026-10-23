using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace MaxIllusionPlugins.Common {
    /// <summary>Serialize completely before replacing a user's existing file.</summary>
    public static class AtomicXmlFile {
        public static T Read<T>(string path) {
            XmlReaderSettings settings = new XmlReaderSettings {
                // ProhibitDtd is the .NET 3.5 equivalent of DtdProcessing.Prohibit.
                ProhibitDtd = true,
                XmlResolver = null
            };
            using (XmlReader reader = XmlReader.Create(path, settings)) {
                return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
            }
        }

        public static void Write<T>(string path, T value, string backupPath = null) {
            Write(path, stream => {
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) {
                    new XmlSerializer(typeof(T)).Serialize(writer, value);
                }
            }, backupPath);
        }

        // Also used by non-XML writers and by failure-injection regression tests.
        public static void Write(string path, Action<Stream> write, string backupPath = null) {
            if (write == null) throw new ArgumentNullException("write");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    write(stream);
                }
                if (File.Exists(fullPath)) {
                    // Never delete the old file to make room: if replacement fails,
                    // the error must leave the previous valid document intact.
                    File.Replace(temporary, fullPath, backupPath);
                } else {
                    File.Move(temporary, fullPath);
                }
            } finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
