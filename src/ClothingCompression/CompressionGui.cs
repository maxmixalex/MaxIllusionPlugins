using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;
using UnityEngine;

namespace ClothingCompression {
    internal sealed class CompressionGui {
        private const int WindowId = 94733120;
        private const int ComboWindowId = WindowId + 1;
        private Rect rect = new Rect(70f, 55f, 700f, 600f);
        private Vector2 listScroll, editorScroll;
        private PocGuiSkin skin;
        private PopupCombo combo;
        private bool mouseCaptured;
        private int importCoordinate = -1;
        private Rect bindingDropdownAnchor, coordinateDropdownAnchor;
        internal bool Visible;

        internal void Draw() {
            if (!Visible) return;
            if (skin == null) { skin = new PocGuiSkin(); combo = new PopupCombo(ComboWindowId, skin); }
            Color oldColor = GUI.color, oldContent = GUI.contentColor, oldBackground = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            try {
                GUI.color = Color.white; GUI.contentColor = Color.black; GUI.backgroundColor = new Color(1f, 1f, 1f, 0.8f);
                rect = GUI.Window(WindowId, rect, DrawWindow, ClothingCompressionPlugin.Name + " " + ClothingCompressionPlugin.Version, skin.Window);
                rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width)); rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - 24f));
                combo.Follow(rect);
                combo.Draw();
            }
            finally {
                GUI.color = oldColor; GUI.contentColor = oldContent; GUI.backgroundColor = oldBackground;
                GUI.enabled = oldEnabled;
            }
            HandleMouse();
        }

        private void DrawWindow(int id) {
            if (GUI.Button(new Rect(rect.width - 22f, 1f, 21f, 20f), "×", skin.Button)) { combo.Hide(); Visible = false; return; }
            CompressionController controller = FindController();
            GUILayout.Space(3f);
            if (controller == null || controller.ChaControl == null) {
                GUILayout.Label("请在 Maker 中编辑角色，或在 Studio 中选中角色。", skin.Label);
                GUI.DragWindow(new Rect(0f, 0f, rect.width - 24f, 22f)); return;
            }
            GUILayout.Label("角色：" + controller.ChaFileControl.parameter.fullname + "　形变：" + controller.Entries.Count + "　" + (controller.Dirty ? "（未保存）" : string.Empty), skin.Label);
            GUILayout.BeginHorizontal();
            DrawList(controller);
            DrawEditor(controller);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("保存形变设置", skin.Button, GUILayout.Width(130f), GUILayout.Height(26f))) controller.Save();
            if (GUILayout.Button("从 XML 重载", skin.Button, GUILayout.Width(110f), GUILayout.Height(26f))) controller.ReloadFromDisk();
            GUILayout.EndHorizontal();
            GUILayout.Label(controller.Status, skin.Hint, GUILayout.Height(20f));
            GUI.DragWindow(new Rect(0f, 0f, rect.width - 24f, 22f));
        }

        private void DrawList(CompressionController controller) {
            GUILayout.BeginVertical(skin.Group, GUILayout.Width(218f), GUILayout.ExpandHeight(true));
            if (GUILayout.Button("+ 新增形变", skin.Button, GUILayout.Height(24f))) controller.Add(DeformationKind.Thigh);
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < controller.Entries.Count; i++) {
                DeformationEntry entry = controller.Entries[i];
                Color old = GUI.backgroundColor;
                if (i == controller.SelectedIndex) GUI.backgroundColor = new Color(1f, 0.65f, 0.25f, 1f);
                string prefix = entry.Enabled ? "● " : "○ ";
                if (GUILayout.Button(prefix + ClothingCompressionPlugin.FormatEntryLabel(i, entry), skin.Button, GUILayout.Height(24f))) { controller.SelectedIndex = i; ClothingCompressionPlugin.Instance.RefreshMakerControls(controller); combo.Hide(); }
                GUI.backgroundColor = old;
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制", skin.Button)) controller.DuplicateSelected();
            if (GUILayout.Button("删除", skin.Button)) controller.DeleteSelected();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↑", skin.Button)) controller.MoveSelected(-1);
            if (GUILayout.Button("↓", skin.Button)) controller.MoveSelected(1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawEditor(CompressionController controller) {
            GUILayout.BeginVertical(skin.Group, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DeformationEntry value = controller.Selected;
            if (value == null) { GUILayout.Label("当前搭配没有形变。可以新增形变，或从其他服装搭配复制。", skin.Hint); DrawCoordinateImport(controller); GUILayout.EndVertical(); return; }
            GUI.changed = false;
            editorScroll = GUILayout.BeginScrollView(editorScroll, GUILayout.ExpandHeight(true));
            value.Enabled = GUILayout.Toggle(value.Enabled, "启用这个形变", skin.Toggle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("种类", skin.Label, GUILayout.Width(72f));
            if (GUILayout.Toggle(value.Kind == DeformationKind.Thigh, "大腿", skin.Toggle, GUILayout.Width(64f))) ClothingCompressionPlugin.SetKind(value, DeformationKind.Thigh);
            if (GUILayout.Toggle(value.Kind == DeformationKind.LowerThigh, "整腿", skin.Toggle, GUILayout.Width(72f))) ClothingCompressionPlugin.SetKind(value, DeformationKind.LowerThigh);
            if (GUILayout.Toggle(value.Kind == DeformationKind.Knee, "膝部", skin.Toggle, GUILayout.Width(64f))) ClothingCompressionPlugin.SetKind(value, DeformationKind.Knee);
            if (GUILayout.Toggle(value.Kind == DeformationKind.UpperArm, "大臂", skin.Toggle, GUILayout.Width(64f))) ClothingCompressionPlugin.SetKind(value, DeformationKind.UpperArm);
            if (GUILayout.Toggle(value.Kind == DeformationKind.Arm, "前臂", skin.Toggle, GUILayout.Width(64f))) ClothingCompressionPlugin.SetKind(value, DeformationKind.Arm);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("应用侧", skin.Label, GUILayout.Width(72f));
            value.Left = GUILayout.Toggle(value.Left, IsArmKind(value.Kind) ? "左臂" : "左腿", skin.Toggle, GUILayout.Width(90f));
            value.Right = GUILayout.Toggle(value.Right, IsArmKind(value.Kind) ? "右臂" : "右腿", skin.Toggle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
            DrawBinding(controller, value);
            if (value.BindingKind == BoundItemKind.None)
                GUILayout.Label("未绑定：当前只形变身体，不会处理服装/饰品网格。", skin.Warning);
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存级别", skin.Label, GUILayout.Width(72f));
            if (GUILayout.Button((value.SaveScope == SaveScope.CharacterAndItem ? "● " : "○ ") + "角色 + 服装/饰品", skin.Button, GUILayout.Width(155f)) && value.SaveScope != SaveScope.CharacterAndItem) { value.SaveScope = SaveScope.CharacterAndItem; GUI.changed = true; }
            if (GUILayout.Button((value.SaveScope == SaveScope.ItemOnly ? "● " : "○ ") + "仅服装/饰品", skin.Button, GUILayout.Width(120f)) && value.SaveScope != SaveScope.ItemOnly) { value.SaveScope = SaveScope.ItemOnly; GUI.changed = true; }
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
            GUILayout.Label(value.Kind == DeformationKind.Thigh ? "大腿参数" : value.Kind == DeformationKind.LowerThigh ? "整腿定位参数" : value.Kind == DeformationKind.Knee ? "膝部参数" : value.Kind == DeformationKind.UpperArm ? "大臂参数" : "前臂参数", skin.Header);
            value.Position = Slider("束口位置", value.Position, -0.5f, 1.5f, "F3");
            value.Width = Slider("影响宽度", value.Width, 0.005f, 0.5f, "F3");
            value.Depth = ScaledSlider("内收深度", value.Depth, 0f, IsArmKind(value.Kind) ? 0.06f : 0.10f, 100f);
            if (!IsArmKind(value.Kind)) {
                value.ProximalBulge = ScaledSlider("上侧隆起", value.ProximalBulge, 0f, 0.08f, 100f);
                value.DistalBulge = ScaledSlider("下侧隆起", value.DistalBulge, 0f, 0.08f, 100f);
            } else {
                value.ProximalBulge = ScaledSlider("手臂侧隆起", value.ProximalBulge, 0f, 0.05f, 100f);
                value.DistalBulge = 0f;
            }
            value.GarmentClearanceX = ScaledSlider("服装外扩 X", value.GarmentClearanceX, 0f, 0.10f, 100f);
            value.GarmentClearanceY = ScaledSlider("服装外扩 Y", value.GarmentClearanceY, 0f, 0.10f, 100f);
            value.GarmentClearanceZ = ScaledSlider("服装外扩 Z", value.GarmentClearanceZ, 0f, 0.10f, 100f);
            DrawCoordinateImport(controller);
            GUILayout.Label("主绑定对象关闭时形变停用；启用时会自动同步所有含对应骨骼的已穿着蒙皮服饰，以避免多层服装穿模。普通 MeshRenderer 暂时不能形变。", skin.Hint);
            GUILayout.EndScrollView();
            bool changed = GUI.changed;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("立即重算", skin.Button, GUILayout.Height(26f))) controller.ApplyNow();
            if (GUILayout.Button("取消绑定", skin.Button, GUILayout.Height(26f))) { ItemCatalog.Bind(value, null); controller.Changed(true); }
            GUILayout.EndHorizontal();
            if (changed) controller.Changed(true);
            GUILayout.EndVertical();
        }

        private void DrawBinding(CompressionController controller, DeformationEntry entry) {
            GUILayout.BeginHorizontal(); GUILayout.Label("绑定对象", skin.Label, GUILayout.Width(72f));
            string label = entry.BindingKind == BoundItemKind.None ? "未绑定（始终应用） ▼" : Shorten((string.IsNullOrEmpty(entry.BindingName) ? entry.ItemKey : entry.BindingName) + " ▼", 30);
            bool clicked = GUILayout.Button(label, skin.Button, GUILayout.Height(24f)); Rect local = GUILayoutUtility.GetLastRect();
            GUILayout.EndHorizontal();
            if (Event.current != null && Event.current.type == EventType.Repaint && local.width > 1f) {
                Vector2 screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(local.x, local.y + local.height));
                bindingDropdownAnchor = new Rect(screenPoint.x, screenPoint.y, local.width, 24f);
            }
            if (!clicked) return;
            if (combo.Show) { combo.Hide(); return; }
            List<ItemDescriptor> items = ItemCatalog.Get(controller.ChaControl);
            string[] labels = new[] { "未绑定（始终应用）" }.Concat(items.Select(x => x.Display)).ToArray();
            combo.Open(rect, bindingDropdownAnchor, labels, delegate(int index) { ItemCatalog.Bind(entry, index == 0 ? null : items[index - 1]); controller.Changed(true); }, true);
        }

        private static string Shorten(string value, int maxCharacters) {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters) return value;
            return value.Substring(0, Math.Max(1, maxCharacters - 1)) + "…";
        }

        private static bool IsArmKind(DeformationKind kind) { return kind == DeformationKind.Arm || kind == DeformationKind.UpperArm; }

        private void DrawCoordinateImport(CompressionController controller) {
            int current = controller.CoordinateIndex;
            if (importCoordinate < 0 || importCoordinate >= ClothingCompressionPlugin.CoordinateNames.Length || importCoordinate == current)
                importCoordinate = current >= 0 && current < ClothingCompressionPlugin.CoordinateNames.Length ? (current + 1) % ClothingCompressionPlugin.CoordinateNames.Length : 0;
            GUILayout.Space(6f);
            GUILayout.Label("从其他服装搭配复制", skin.Header);
            GUILayout.BeginHorizontal(); GUILayout.Label("来源搭配", skin.Label, GUILayout.Width(72f));
            bool clicked = GUILayout.Button(ClothingCompressionPlugin.CoordinateNames[importCoordinate] + " ▼", skin.Button, GUILayout.Height(24f));
            Rect local = GUILayoutUtility.GetLastRect();
            if (GUILayout.Button("复制形变", skin.Button, GUILayout.Width(85f), GUILayout.Height(24f))) controller.ImportFromCoordinate(importCoordinate);
            GUILayout.EndHorizontal();
            if (Event.current != null && Event.current.type == EventType.Repaint && local.width > 1f) {
                Vector2 screenPoint = GUIUtility.GUIToScreenPoint(new Vector2(local.x, local.y + local.height));
                coordinateDropdownAnchor = new Rect(screenPoint.x, screenPoint.y, local.width, 24f);
            }
            if (!clicked) return;
            if (combo.Show) { combo.Hide(); return; }
            combo.Open(rect, coordinateDropdownAnchor, ClothingCompressionPlugin.CoordinateNames,
                delegate(int index) { if (index >= 0 && index < ClothingCompressionPlugin.CoordinateNames.Length && index != current) importCoordinate = index; });
        }

        private float Slider(string label, float value, float min, float max, string format) {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, skin.Label, GUILayout.Width(95f));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(result.ToString(format), skin.Label, GUILayout.Width(50f)); GUILayout.EndHorizontal(); return result;
        }

        private float ScaledSlider(string label, float value, float min, float max, float scale) {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, skin.Label, GUILayout.Width(125f));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label((result * scale).ToString("0.###"), skin.Label, GUILayout.Width(50f)); GUILayout.EndHorizontal(); return result;
        }

        private static CompressionController FindController() {
            if (MakerAPI.InsideMaker) { ChaControl maker = MakerAPI.GetCharacterControl(); return maker == null ? null : maker.GetComponent<CompressionController>(); }
            if (StudioAPI.InsideStudio) {
                CompressionController selected = StudioAPI.GetSelectedCharacters().Select(x => x.GetChaControl()).Where(x => x != null).Select(x => x.GetComponent<CompressionController>()).FirstOrDefault(x => x != null);
                if (selected != null) return selected;
            }
            CharacterApi.ControllerRegistration registration = CharacterApi.GetRegisteredBehaviour(ClothingCompressionPlugin.Guid);
            return registration == null ? null : registration.Instances.OfType<CompressionController>().FirstOrDefault(x => x.ChaControl != null);
        }

        private void HandleMouse() {
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) {
                mouseCaptured = rect.Contains(mouse) || (combo.Show && combo.Rect.Contains(mouse));
                if (combo.Show && !combo.OpenedThisFrame && !combo.Rect.Contains(mouse)) combo.Hide();
            }
            if (mouseCaptured && (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))) Input.ResetInputAxes(); else if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2)) mouseCaptured = false;
        }
    }

    internal sealed class PopupCombo {
        private readonly int id; private readonly PocGuiSkin skin; private readonly GUIStyle selectionGrid;
        private string[] items = new string[0]; private Action<int> callback; private Vector2 scroll, mainWindowOffset; private Rect itemRect; private bool scrolling;
        internal PopupCombo(int id, PocGuiSkin skin) {
            this.id = id; this.skin = skin;
            GUIStyleState selected = new GUIStyleState { textColor = Color.white, background = skin.GrayTexture };
            selectionGrid = new GUIStyle { fontSize = PocGuiSkin.Pixel(12), padding = new RectOffset(4, 4, 4, 4), richText = true };
            selectionGrid.hover = selected; selectionGrid.active = selected; selectionGrid.focused = selected;
        }
        private int openedFrame = -1;
        internal Rect Rect; internal bool Show { get; private set; }
        internal bool OpenedThisFrame { get { return openedFrame == Time.frameCount; } }
        internal void Open(Rect mainWindow, Rect anchor, string[] values, Action<int> action, bool warningFirst = false) {
            items = values == null ? new string[0] : (string[])values.Clone(); callback = action;
            if (warningFirst && items.Length > 0) items[0] = "<color=#D52B1E>" + items[0] + "</color>";
            float fullHeight = Math.Max(24f, items.Length * 24f); scrolling = fullHeight > Screen.height * 0.4f;
            Rect = new Rect(anchor.x, anchor.y, anchor.width, scrolling ? Screen.height * 0.4f : fullHeight);
            if (Rect.y + Rect.height > Screen.height) Rect.y = anchor.y - Rect.height - anchor.height;
            itemRect = new Rect(0f, 0f, scrolling ? anchor.width - 16f : anchor.width, fullHeight);
            mainWindowOffset = new Vector2(Rect.x - mainWindow.x, Rect.y - mainWindow.y);
            openedFrame = Time.frameCount; Show = true;
        }
        internal void Hide() { Show = false; }
        internal void Follow(Rect mainWindow) {
            if (Show) Rect = new Rect(mainWindow.x + mainWindowOffset.x, mainWindow.y + mainWindowOffset.y, Rect.width, Rect.height);
        }
        internal void Draw() {
            if (!Show) return;
            Rect = GUI.Window(id, Rect, DrawWindow, string.Empty, skin.Group);
            GUI.BringWindowToFront(id);
        }
        private void DrawWindow(int windowId) {
            if (scrolling) scroll = GUI.BeginScrollView(new Rect(0f, 0f, Rect.width, Rect.height), scroll, itemRect, false, false);
            int selected = GUI.SelectionGrid(itemRect, -1, items, 1, selectionGrid);
            if (scrolling) GUI.EndScrollView();
            if (selected >= 0) { Action<int> action = callback; Show = false; if (action != null) action(selected); Input.ResetInputAxes(); }
        }
    }

    internal sealed class PocGuiSkin {
        internal readonly GUIStyle Window, Label, Header, Hint, Warning, Button, Toggle, TextField, Group;
        private static Texture2D windowTexture, windowOnTexture, buttonTexture, buttonHoverTexture, buttonActiveTexture, inputTexture, inputActiveTexture, backgroundTexture, grayTexture;
        internal Texture2D BackgroundTexture { get { return backgroundTexture; } }
        internal Texture2D GrayTexture { get { return grayTexture; } }
        internal PocGuiSkin() {
            if (windowTexture == null) {
                windowTexture = Load("iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAxklEQVR42uyX0Q3DIAxETcQsLMCAhf1gAZahoYIILqWirdOPynxFinPPJ5STrXLOhCfGeKuPjtbOo85a6/GFQkAVXxU+gRAyAFDcGLOkmlKaQg5AL74q/AJ0QDYucfjWtXtUIQQW8ZmTjS4+xUHm7B5dXO5AAAIQAJGeRC4/YI9XVuE9TeWSBSCAvwK4/tfmjImfzEUF4Dld9N2X+bQ5YIGg+BDXFdIG17ciHJoaxncNtb7fWD5wc1pA9JMij6vRNyvUXYABAFQxZQThBU+WAAAAAElFTkSuQmCC");
                windowOnTexture = Load("iVBORw0KGgoAAAANSUhEUgAAABgAAAAkCAYAAACTz/ouAAAAtElEQVR42uyX0QqAIAxFnfjsL6e/7A+YlsayMsvZQ+yCIFT3bBPTgfdelAKAKU2NaFN+zx6eRAAeQdHcvxxT6a9OIt+ids41ha+13mUSArXBa/XMJcLmrcY3oKVcksq8+NbkckGadJtfZSLFYEFafbLoyyyGZ8AABjAA7eRRUvjgIY08nQe8yAxgwF8ABm9tyt/EJ/eirO1G3avaTbsbUjM/QJ6A7nqEKqS3AYEKhKSFmgUYAGTwBp/Tq57RAAAAAElFTkSuQmCC");
                buttonTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAoklEQVR42uyXMQ7DIAxFbZSFlROFk6cnYmV0cIQrRJsRs3xLjoAM72GEkElEyLLF2VIWpzK+TH4+zLp46Y9aK62MGKMNc8uPCchq8IsIh777XXGyHof37scqBNocEIAABCAAgWNecHyOfyvg8SjNjOAJ/8cK3vBZArcAAhCAgArklJI7uDMz97mUUnYIsB3BUwWPSgycTNYmj72aV3tucQswAKPmcSH9pvfQAAAAAElFTkSuQmCC");
                buttonHoverTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmElEQVR42uyXMQrAIAxFE+mip9L730fHVCVSETsal/8htLXDe/zSISQiNKYm1pHD0xgfcwWfzirC7YyZ9Z1dKrNfXL2JdC+RtfYr9NaCo8uBAAQgAAEIPOtBKeUo0Hv/38Bp+I7hLOE7lrOGrxL4CyAAAQg0gRRCMAcrM7E+S875hgCPT9BbsGhi4iQaa/K8q1mt5yOvAAMAfwPPqY0XyRgAAAAASUVORK5CYII=");
                buttonActiveTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAmUlEQVR42uyXwQqAIAyGl3RoVx/Ioz5z1x7Ia8da4UKM6OQWtMEPMoT/24YMAQCmSpG0dVasPYdyCKSZBDln6Bneez4m0sIAa2/jBxB0pXqtCEcHNunq6y44UA4DMAAD+AYAIoobs6drE5LmtxFIQLQe49sFewUGYAAGYAC/AEiK6zhdXzMKDQDkEZxdkFrHXD3nVL/nuwADAAyFRZEqr0SGAAAAAElFTkSuQmCC");
                inputTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkUlEQVR42uyXQQrAIAwEE+nD6svUl9mfpaYYkUKPmkN3QSTksJO9LYsImUopZ/sqrVVs70opPQMrwMuYbblCzUtmkKMPas60R+ajIMw5Z7t+6eUfScTgYa5qfppEDeSs0Gl8AdwTAAAAAAAAAAAAAAAAwO8B4lSXXIrJ5RjA6IYjhd5YdpXT0Y5tv62e23ALMABi1TXAc3c4+wAAAABJRU5ErkJggg==");
                inputActiveTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAkElEQVR42uyXSw7AIAhEofFgev9DUWjxE+OyYhczCQuJybzMiiERoTqqrCObxzy658KYRqivZwZhWzKzLZhc/nGL1KvZ2DPpIkcYzx4OkrmSRJgv0pCLDovfVISPmP8hAQAAAAAAAAAAAAAAAADAAIo3o/CL+PHuhSX2NK91MPm7pbAbZEi7rPpgWD2vugUYAKxOtLWwqwjPAAAAAElFTkSuQmCC");
                backgroundTexture = Load("iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAjElEQVR42uyXUQrAIAxDm13c7eTdHArDdd2PrT8JFAuCeRSEBqoqowCU1u4yR/2d43VTAZ51qZprUBUXYDSfJRfCMo+SCZFl7kGkmVsQmywWGolY3zHUGLjP5RMgAAEIQAACEIAABNh6aOgbSuY2VL2XTyB9M/ZS0ppg8gWRFs3+IKLDKRyIlHh+CjAAfuTkaxIqAb4AAAAASUVORK5CYII=");
                grayTexture = Solid(new Color(0.8f, 0.8f, 0.8f, 1f));
            }
            Window = new GUIStyle("window") { alignment = TextAnchor.UpperCenter, fontSize = 12, border = new RectOffset(8, 8, 20, 8) }; SetBackground(Window, windowTexture); SetOnBackground(Window, windowOnTexture); SetText(Window, Color.black);
            Label = new GUIStyle("label") { fontSize = Pixel(12), alignment = TextAnchor.MiddleLeft }; SetText(Label, Color.black);
            Header = new GUIStyle(Label) { fontStyle = FontStyle.Bold, fontSize = Pixel(13) }; Hint = new GUIStyle(Label) { wordWrap = true }; SetText(Hint, new Color(0.25f, 0.25f, 0.25f));
            Warning = new GUIStyle(Hint) { fontStyle = FontStyle.Bold }; SetText(Warning, new Color(0.85f, 0.18f, 0.04f));
            Button = new GUIStyle("button") { fontSize = Pixel(12), alignment = TextAnchor.MiddleCenter, border = new RectOffset(6, 6, 6, 6) }; Button.normal.background = buttonTexture; Button.hover.background = buttonHoverTexture; Button.focused.background = buttonHoverTexture; Button.active.background = buttonActiveTexture; SetText(Button, Color.black);
            Toggle = new GUIStyle("toggle") { fontSize = Pixel(12), alignment = TextAnchor.MiddleLeft }; SetText(Toggle, Color.black);
            TextField = new GUIStyle("textfield") { fontSize = Pixel(12), alignment = TextAnchor.MiddleLeft, border = new RectOffset(6, 6, 6, 6) }; TextField.normal.background = inputTexture; TextField.hover.background = inputActiveTexture; TextField.focused.background = inputActiveTexture; TextField.active.background = inputActiveTexture; SetText(TextField, Color.black);
            Group = new GUIStyle("box") { fontSize = Pixel(12), alignment = TextAnchor.UpperLeft, padding = new RectOffset(7, 7, 7, 7), border = new RectOffset(12, 12, 12, 12) }; SetBackground(Group, backgroundTexture); SetOnBackground(Group, backgroundTexture); SetText(Group, Color.black);
        }
        internal static int Pixel(int value) { return (int)((1f + (Screen.width / 1280f - 1f) * 0.6f) * value); }
        private static Texture2D Load(string value) { Texture2D texture = new Texture2D(32, 32); texture.LoadImage(Convert.FromBase64String(value)); return texture; }
        private static Texture2D Solid(Color color) { Texture2D texture = new Texture2D(4, 4); Color32[] pixels = new Color32[16]; for (int i = 0; i < pixels.Length; i++) pixels[i] = color; texture.SetPixels32(pixels); texture.Apply(); return texture; }
        private static void SetBackground(GUIStyle s, Texture2D t) { s.normal.background = t; s.hover.background = t; s.active.background = t; s.focused.background = t; }
        private static void SetOnBackground(GUIStyle s, Texture2D t) { s.onNormal.background = t; s.onHover.background = t; s.onActive.background = t; s.onFocused.background = t; }
        private static void SetText(GUIStyle s, Color c) { s.normal.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.focused.textColor = c; s.onNormal.textColor = c; s.onHover.textColor = c; s.onActive.textColor = c; s.onFocused.textColor = c; }
    }
}
