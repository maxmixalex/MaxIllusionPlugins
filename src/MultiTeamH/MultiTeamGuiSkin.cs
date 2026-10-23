using System;
using UnityEngine;

namespace MultiTeamH {
    // The original white IMGUI skin used by StarPlugins JuiceControl/ExpressionControl.
    // Kept local so this plugin does not depend on the unfinished MaximumUI project.
    internal static class MultiTeamGuiSkin {
        private static readonly Texture2D WindowOn = Load(Base64WindowOn);
        private static readonly Texture2D WindowOff = Load(Base64WindowOff);
        private static readonly Texture2D BackgroundOn = Load(Base64BackgroundOn);
        private static readonly Texture2D ButtonNormal = Load(Base64ButtonNormal);
        private static readonly Texture2D ButtonHover = Load(Base64ButtonHover);
        private static readonly Texture2D ButtonActive = Load(Base64ButtonActive);
        private static readonly Texture2D PanelNormal = Load(Base64PanelNormal);
        private static readonly Texture2D InputNormal = Load(Base64InputNormal);
        private static readonly Texture2D InputActive = Load(Base64InputActive);

        internal static GUISkin Create(GUISkin source) {
            GUISkin skin = UnityEngine.Object.Instantiate(source);
            int fontSize = GetPix(12);

            skin.window = new GUIStyle(source.window) { fontSize = 12, alignment = TextAnchor.UpperCenter };
            SetOffBackground(skin.window, WindowOff);
            SetOnBackground(skin.window, WindowOn);
            SetTextColor(skin.window, Color.black);
            skin.window.border = new RectOffset(8, 8, 20, 8);

            skin.label = new GUIStyle(source.label) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft };
            SetTextColor(skin.label, Color.black);

            skin.toggle = new GUIStyle(source.toggle) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft };
            SetTextColor(skin.toggle, Color.black);

            skin.button = new GUIStyle(source.button) { fontSize = fontSize, alignment = TextAnchor.MiddleCenter };
            skin.button.normal.background = ButtonNormal;
            skin.button.hover.background = ButtonHover;
            skin.button.focused.background = ButtonHover;
            skin.button.active.background = ButtonActive;
            skin.button.onNormal.background = ButtonNormal;
            skin.button.onHover.background = ButtonHover;
            skin.button.onFocused.background = ButtonHover;
            skin.button.onActive.background = ButtonActive;
            SetTextColor(skin.button, Color.black);
            skin.button.border = new RectOffset(6, 6, 6, 6);

            skin.box = new GUIStyle(source.box) { fontSize = fontSize, alignment = TextAnchor.UpperLeft };
            SetAllBackground(skin.box, BackgroundOn);
            SetTextColor(skin.box, Color.black);
            skin.box.border = new RectOffset(12, 12, 12, 12);

            skin.textField = new GUIStyle(source.textField) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft };
            skin.textField.normal.background = InputNormal;
            skin.textField.hover.background = InputActive;
            skin.textField.focused.background = InputActive;
            skin.textField.active.background = InputActive;
            SetTextColor(skin.textField, Color.black);
            skin.textField.border = new RectOffset(6, 6, 6, 6);
            return skin;
        }

        internal static GUIStyle CreatePanelStyle() {
            var style = new GUIStyle {
                fontSize = GetPix(12),
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(5, 5, 2, 2),
                margin = new RectOffset(1, 1, 1, 1)
            };
            SetAllBackground(style, PanelNormal);
            SetTextColor(style, Color.black);
            return style;
        }

        internal static GUIStyle CreateFoldedWindowStyle(GUIStyle source) {
            var style = new GUIStyle(source) {
                border = new RectOffset(8, 8, 0, 0)
            };
            style.padding.bottom = -20;
            SetOffBackground(style, Load(Base64WindowFoldOff));
            SetOnBackground(style, Load(Base64WindowFoldOn));
            return style;
        }

        private static int GetPix(int value) {
            return (int)((1f + (Screen.width / 1280f - 1f) * 0.6f) * value);
        }

        private static Texture2D Load(string base64) {
            var texture = new Texture2D(32, 32) { hideFlags = HideFlags.HideAndDontSave };
            texture.LoadImage(Convert.FromBase64String(base64));
            return texture;
        }

        private static void SetOffBackground(GUIStyle style, Texture2D texture) {
            style.normal.background = texture;
            style.hover.background = texture;
            style.focused.background = texture;
            style.active.background = texture;
        }

        private static void SetOnBackground(GUIStyle style, Texture2D texture) {
            style.onNormal.background = texture;
            style.onHover.background = texture;
            style.onFocused.background = texture;
            style.onActive.background = texture;
        }

        private static void SetAllBackground(GUIStyle style, Texture2D texture) {
            SetOffBackground(style, texture);
            SetOnBackground(style, texture);
        }

        private static void SetTextColor(GUIStyle style, Color color) {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.focused.textColor = color;
            style.active.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onFocused.textColor = color;
            style.onActive.textColor = color;
        }

        private const string Base64WindowOn = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAtElEQVR42uyX0QqAIAxFnfjsL6e/7A+YlsayMsvZQ+yCIFT3bBPTgfdelAKAKU2NaFN+zx6eRAAeQdHcvxxT6a9OIt+ids41ha+13mUSArXBa/XMJcLmrcY3oKVcksq8+NbkckGadJtfZSLFYEFafbLoyyyGZ8AABjAA7eRRUvjgIY08nQe8yAxgwF8ABm9tyt/EJ/eirO1G3avaTbsbUjM/QJ6A7nqEKqS3AYEKhKSFmgUYAGTwBp/Tq57RAAAAAElFTkSuQmCC";
        private const string Base64WindowOff = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAxklEQVR42uyX0Q3DIAxETcQsLMCAhf1gAZahoYIILqWirdOPynxFinPPJ5STrXLOhCfGeKuPjtbOo85a6/GFQkAVXxU+gRAyAFDcGLOkmlKaQg5AL74q/AJ0QDYucfjWtXtUIQQW8ZmTjS4+xUHm7B5dXO5AAAIQAJGeRC4/YI9XVuE9TeWSBSCAvwK4/tfmjImfzEUF4Dld9N2X+bQ5YIGg+BDXFdIG17ciHJoaxncNtb7fWD5wc1pA9JMij6vRNyvUXYABAFQxZQThBU+WAAAAAElFTkSuQmCC";
        private const string Base64WindowFoldOn = "iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAYAAABB7B6eAAAAfUlEQVR42uyT0QqAIAxFnfTsL6u/vB9YWlvYKpJyb14YCOq5dxOBiJwWAEReJtcnOZcvO9WgraIKp48VNX+5SX6kRsSu+CGEUyclaC6snSkjauG94BejbVx+FFzdTTIu4MVv+FMn3hkL+PWHpdddmHcwDabBNGh+spVWAQYAeaxBMZcK1SAAAAAASUVORK5CYII=";
        private const string Base64WindowFoldOff = "iVBORw0KGgoAAAANSUhEUgAAABgAAAASCAYAAABB7B6eAAAAeklEQVR42uyUwQnAIAxFo3QWF3DA2v3MAi6TUrHS/mLJId78J0F9Lx9BJyKEYea9LRPpUs/FGA/ccChocC34I0LJS4DwEIKKWkoZSrrgCdeCf0Rd4q3gcDfd7+hyzibwURNPk3M1EMvpscX0BkuwBEtAtA2+XLOcAgwAe189mW2MybwAAAAASUVORK5CYII=";
        private const string Base64BackgroundOn = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAjElEQVR42uyXUQrAIAxDm13c7eTdHArDdd2PrT8JFAuCeRSEBqoqowCU1u4yR/2d43VTAZ51qZprUBUXYDSfJRfCMo+SCZFl7kGkmVsQmywWGolY3zHUGLjP5RMgAAEIQAACEIAABNh6aOgbSuY2VL2XTyB9M/ZS0ppg8gWRFs3+IKLDKRyIlHh+CjAAfuTkaxIqAb4AAAAASUVORK5CYII=";
        private const string Base64ButtonNormal = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAoklEQVR42uyXMQ7DIAxFbZSFlROFk6cnYmV0cIQrRJsRs3xLjoAM72GEkElEyLLF2VIWpzK+TH4+zLp46Y9aK62MGKMNc8uPCchq8IsIh777XXGyHof37scqBNocEIAABCAAgWNecHyOfyvg8SjNjOAJ/8cK3vBZArcAAhCAgArklJI7uDMz97mUUnYIsB3BUwWPSgycTNYmj72aV3tucQswAKPmcSH9pvfQAAAAAElFTkSuQmCC";
        private const string Base64ButtonHover = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmElEQVR42uyXMQrAIAxFE+mip9L730fHVCVSETsal/8htLXDe/zSISQiNKYm1pHD0xgfcwWfzirC7YyZ9Z1dKrNfXL2JdC+RtfYr9NaCo8uBAAQgAAEIPOtBKeUo0Hv/38Bp+I7hLOE7lrOGrxL4CyAAAQg0gRRCMAcrM7E+S875hgCPT9BbsGhi4iQaa/K8q1mt5yOvAAMAfwPPqY0XyRgAAAAASUVORK5CYII=";
        private const string Base64ButtonActive = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmUlEQVR42uyXwQqAIAyGl3RoVx/Ioz5z1x7Ia8da4UKM6OQWtMEPMoT/24YMAQCmSpG0dVasPYdyCKSZBDln6Bneez4m0sIAa2/jBxB0pXqtCEcHNunq6y44UA4DMAAD+AYAIoobs6drE5LmtxFIQLQe49sFewUGYAAGYAC/AEiK6zhdXzMKDQDkEZxdkFrHXD3nVL/nuwADAAyFRZEqr0SGAAAAAElFTkSuQmCC";
        private const string Base64PanelNormal = "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAH0lEQVR42mJgwAEYgfj////oooyMTLh00EMCJwAIMAADRwMMiHBH2wAAAABJRU5ErkJggg==";
        private const string Base64InputNormal = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkUlEQVR42uyXQQrAIAwEE+nD6svUl9mfpaYYkUKPmkN3QSTksJO9LYsImUopZ/sqrVVs70opPQMrwMuYbblCzUtmkKMPas60R+ajIMw5Z7t+6eUfScTgYa5qfppEDeSs0Gl8AdwTAAAAAAAAAAAAAAAAwO8B4lSXXIrJ5RjA6IYjhd5YdpXT0Y5tv62e23ALMABi1TXAc3c4+wAAAABJRU5ErkJggg==";
        private const string Base64InputActive = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkElEQVR42uyXSw7AIAhEofFgev9DUWjxE+OyYhczCQuJybzMiiERoTqqrCObxzy658KYRqivZwZhWzKzLZhc/nGL1KvZ2DPpIkcYzx4OkrmSRJgv0pCLDovfVISPmP8hAQAAAAAAAAAAAAAAAADAAIo3o/CL+PHuhSX2NK91MPm7pbAbZEi7rPpgWD2vugUYAKxOtLWwqwjPAAAAAElFTkSuQmCC";
    }
}

