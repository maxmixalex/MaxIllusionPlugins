using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace HSControl
{
    // A native HSprite menu button, placed next to the original Config button.
    // Unlike the original plugin's hierarchy surgery, we never move or
    // disable built-in buttons and never execute the old Config click handler.
    internal sealed class NativeSceneButton
    {
        private readonly HSceneProc owner;
        private GameObject node;
        private NativeSceneButton(HSceneProc scene, GameObject button) {
            owner = scene; node = button;
        }

        internal bool Matches(HSceneProc scene) {
            return node != null && ReferenceEquals(scene, owner);
        }

        internal static NativeSceneButton TryInstall(HSceneProc scene, Action toggle, Action<string> warn) {
            if (scene == null || scene.sprite == null || toggle == null) return null;
            Transform root = scene.sprite.transform.Find("MainMenu/Base");
            if (root == null) return null;
            Transform existing = root.Find("HStateCtrl");
            if (existing != null) {
                // Another installation owns this entry; do not intercept it.
                if (warn != null) warn("A pre-existing HStateCtrl button was detected; native entry not replaced.");
                return null;
            }
            Transform config = root.Find("Config");
            if (config == null) return null;
            RectTransform original = config as RectTransform;
            if (original == null) return null;
            GameObject go = null;
            try {
                go = new GameObject("HStateCtrl", typeof(RectTransform), typeof(Image), typeof(Button));
                go.SetActive(false);
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                rect.anchorMin = original.anchorMin;
                rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot;
                rect.localScale = Vector3.one;
                rect.anchoredPosition = original.anchoredPosition +
                    new Vector2(original.rect.width + 4f, 0f);
                rect.sizeDelta = original.sizeDelta;
                Image background = go.GetComponent<Image>();
                Image sample = config.GetComponent<Image>();
                if (sample != null) {
                    background.sprite = sample.sprite;
                    background.type = sample.type;
                    background.color = sample.color;
                    background.preserveAspect = sample.preserveAspect;
                } else background.color = new Color(.25f, .35f, .5f, .95f);
                Button button = go.GetComponent<Button>();
                Button originalButton = config.GetComponent<Button>();
                button.targetGraphic = background;
                if (originalButton != null) {
                    button.transition = originalButton.transition;
                    button.colors = originalButton.colors;
                    button.spriteState = originalButton.spriteState;
                    button.navigation = originalButton.navigation;
                }
                button.onClick.AddListener(() => toggle());
                GameObject labelGo = new GameObject("HS", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(rect, false);
                RectTransform labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                Text label = labelGo.GetComponent<Text>();
                label.text = "HS";
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 18;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                go.SetActive(true);
                return new NativeSceneButton(scene, go);
            } catch (Exception e) {
                if (go != null) UnityEngine.Object.Destroy(go);
                if (warn != null) warn("Unable to add native HSprite control button: " + e.Message);
                return null;
            }
        }

        internal void Dispose() {
            if (node != null) UnityEngine.Object.Destroy(node);
            node = null;
        }
    }
}
