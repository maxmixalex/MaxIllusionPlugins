using System;
using UnityEngine;
using UnityEngine.UI;

namespace HSControl
{
    // Dedicated self-drawn H-scene entry. Do not clone the game's Config
    // sprite or overlay low-contrast text on an unrelated native icon.
    internal sealed class NativeSceneButton
    {
        private readonly HSceneProc owner;
        private GameObject node;
        private Sprite paintedSprite;
        private Texture2D paintedTexture;

        private NativeSceneButton(HSceneProc scene, GameObject button,
            Sprite sprite, Texture2D texture) {
            owner = scene;
            node = button;
            paintedSprite = sprite;
            paintedTexture = texture;
        }

        internal bool Matches(HSceneProc scene) {
            return node != null && ReferenceEquals(scene, owner);
        }

        // Draw a readable, high-contrast HS mark directly into a transient UI
        // texture; no dependence on game fonts, sprites, or local asset bundles.
        private static void Rect(Color32[] pixels, int x, int y, int w, int h, Color32 c) {
            for (int py = Math.Max(0, y); py < Math.Min(64, y + h); py++)
                for (int px = Math.Max(0, x); px < Math.Min(64, x + w); px++)
                    pixels[py * 64 + px] = c;
        }

        private static Texture2D PaintIcon() {
            const int size = 64;
            Color32[] pixels = new Color32[size * size];
            Color32 edge = new Color32(84, 207, 231, 255);
            Color32 fill = new Color32(25, 42, 60, 248);
            Color32 ink = new Color32(245, 250, 252, 255);
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    // Soft-cornered panel with a clear outline.
                    bool inside = x >= 2 && x <= 61 && y >= 2 && y <= 61;
                    bool corner = (x < 8 || x > 55) && (y < 8 || y > 55);
                    if (corner && ((x < 6 || x > 57) && (y < 6 || y > 57)))
                        inside = false;
                    if (!inside) continue;
                    bool border = x < 5 || x > 58 || y < 5 || y > 58;
                    pixels[y * size + x] = border ? edge : fill;
                }
            }
            // Geometric H
            Rect(pixels, 12, 19, 5, 28, ink);
            Rect(pixels, 25, 19, 5, 28, ink);
            Rect(pixels, 15, 30, 13, 5, ink);
            // Geometric S (no dynamic font; readable at small menu sizes)
            Rect(pixels, 37, 19, 16, 5, ink);
            Rect(pixels, 36, 22, 5, 12, ink);
            Rect(pixels, 37, 31, 15, 5, ink);
            Rect(pixels, 48, 34, 5, 12, ink);
            Rect(pixels, 36, 43, 17, 5, ink);
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.name = "HSControl.MenuIcon";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        internal static NativeSceneButton TryInstall(HSceneProc scene, Action toggle,
            Action<string> warn) {
            if (scene == null || scene.sprite == null || toggle == null) return null;
            Transform root = scene.sprite.transform.Find("MainMenu/Base");
            if (root == null) return null;
            if (root.Find("HStateCtrl") != null) {
                if (warn != null)
                    warn("A pre-existing HStateCtrl button was detected; entry not replaced.");
                return null;
            }
            RectTransform config = root.Find("Config") as RectTransform;
            if (config == null) return null;

            GameObject go = null;
            Sprite sprite = null;
            Texture2D texture = null;
            try {
                go = new GameObject("HStateCtrl",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.SetActive(false);
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                rect.anchorMin = config.anchorMin;
                rect.anchorMax = config.anchorMax;
                rect.pivot = config.pivot;
                rect.localScale = Vector3.one;
                float width = Mathf.Max(40f, config.rect.width);
                float height = Mathf.Max(32f, config.rect.height);
                rect.anchoredPosition = config.anchoredPosition +
                    new Vector2(-(width + config.rect.width) * .5f - 8f, 0f);
                rect.sizeDelta = new Vector2(width, height);

                texture = PaintIcon();
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f),
                    new Vector2(.5f, .5f), 100f);
                sprite.name = "HSControl.MenuSprite";
                Image background = go.GetComponent<Image>();
                background.sprite = sprite;
                background.type = Image.Type.Simple;
                background.preserveAspect = true;
                background.color = Color.white;

                Button button = go.GetComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(.83f, 1f, 1f, 1f);
                colors.pressedColor = new Color(.55f, .8f, .85f, 1f);
                colors.disabledColor = new Color(.4f, .4f, .4f, .65f);
                button.colors = colors;
                Navigation nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() => toggle());
                go.SetActive(true);
                return new NativeSceneButton(scene, go, sprite, texture);
            } catch (Exception ex) {
                if (go != null) UnityEngine.Object.Destroy(go);
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
                if (warn != null) warn("HSControl entry unavailable: " + ex.Message);
                return null;
            }
        }

        internal void Dispose() {
            if (node != null) UnityEngine.Object.Destroy(node);
            if (paintedSprite != null) UnityEngine.Object.Destroy(paintedSprite);
            if (paintedTexture != null) UnityEngine.Object.Destroy(paintedTexture);
            node = null;
            paintedSprite = null;
            paintedTexture = null;
        }
    }
}
