using System;

namespace MaxIllusionPlugins.Common {
    /// <summary>Shows the parser's actual error instead of only the serializer wrapper.</summary>
    public static class XmlFileDiagnostics {
        public static string Describe(Exception error) {
            if (error == null) return string.Empty;
            while (error.InnerException != null) error = error.InnerException;
            return error.GetType().Name + ": " + error.Message;
        }
    }
}
