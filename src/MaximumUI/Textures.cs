using UnityEngine;

namespace MaximumUI {
    public static class Textures {
        public static Texture2D Solid(Color color) {
            Texture2D texture = new Texture2D(4, 4);
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
