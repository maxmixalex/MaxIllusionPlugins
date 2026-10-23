using UnityEngine;

namespace MaximumUI {
    public static class Textures {
        public static Texture2D Solid(Color color) {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        internal static Texture2D Frame(Color fill, Color border) {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    pixels[y * size + x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? border : fill;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
