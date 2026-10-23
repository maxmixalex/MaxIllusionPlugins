using UnityEngine;

namespace MaximumUI {
    public static class Textures {
        internal static Texture2D Disc() {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float alpha = Mathf.Clamp01(size * 0.5f - 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

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
            return Surface(fill, border, border, 0, 0);
        }

        /// <summary>A tiny nine-sliced surface; 1 selects the bottom edge, 2 the leading edge.</summary>
        internal static Texture2D Surface(Color fill, Color border, Color accent, int markedEdge, int markerWidth) {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    pixels[y * size + x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? border : fill;
                    if ((markedEdge == 1 && y < markerWidth) || (markedEdge == 2 && x < markerWidth))
                        pixels[y * size + x] = accent;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        internal static Texture2D WindowSurface(Color fill, Color title, Color border, int titleHeight) {
            const int width = 8;
            int height = titleHeight + 8;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    bool edge = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    pixels[y * width + x] = edge ? border : y >= 8 ? title : fill;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
