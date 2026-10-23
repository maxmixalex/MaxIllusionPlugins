using System.IO;
using System.Reflection;
using UnityEngine;

namespace MaximumUI {
    internal static class ThemeResources {
        public static Texture2D LoadTexture(string name, int width, int height) {
            Assembly assembly = typeof(ThemeResources).Assembly;
            string resourceName = "MaximumUI.Resources.ExpressionLight." + name;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) return Textures.Solid(Color.white);

                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length) {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }

                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.LoadImage(bytes);
                return texture;
            }
        }
    }
}
