using System;
using System.Collections.Generic;
using AccessoryBatchManager;
using JuiceControl;
using MaximumUI;

// Public examples only. These fixtures do not read cards, XML, configuration,
// the game, or the user's uploaded files. Composition is the shipped View code.
internal static class PluginPreviewScenes {
    internal static IEnumerable<PreviewScene> Create() {
        yield return Juice(false);
        yield return Juice(true);
        yield return Accessory(false);
        yield return Accessory(true);
    }

    private static PreviewScene Juice(bool presets) {
        JuiceEditorModel model = new JuiceEditorModel {
            Version = "0.2.0", Characters = new[] { "示例角色 A", "示例角色 B", "示例角色 C" },
            CharacterIndex = 0, HasCharacter = true, AllLocked = false,
            CharacterPresets = new[] { "自然效果", "雨后", "舞台妆容" },
            GroupPresets = new[] { "场景示例 A", "场景示例 B" },
            CharacterPresetIndex = 0, GroupPresetIndex = 0, CanSave = true,
            Error = string.Empty, Status = "已选择示例角色 A。效果变化将保留到当前角色。",
            Integration = "SkinEffects：示例扩展效果已连接。",
            NameDialogVisible = false, NamePrompt = string.Empty, Name = string.Empty, NameError = string.Empty,
            SelectCharacter = delegate { }, SelectCharacterPreset = delegate { }, SelectGroupPreset = delegate { },
            LockAll = delegate { }, ChangeName = delegate { }, Execute = delegate { }
        };
        string[] labels = { "脸部", "胸部", "腹部", "背部", "下半身", "泪水", "脸红", "SkinEffects 汗水", "SkinEffects 水滴", "SkinEffects 光泽", "SkinEffects 红印", "SkinEffects 眼影" };
        for (int i = 0; i < labels.Length; i++) {
            int maximum = i == 6 ? 100 : 3;
            int value = i == 6 ? 35 : i % 4;
            model.Effects.Add(new JuiceEffectRow {
                Id = "example-" + i, Name = labels[i], Maximum = maximum, Value = value,
                DisplayValue = value.ToString(), Locked = i == 2 || i == 5,
                Change = delegate { }, Lock = delegate { }
            });
        }
        JuiceEditorView view = new JuiceEditorView { Model = model };
        if (presets) {
            Widget original = view.Build(new BuildContext());
            Selectable page = (Selectable)Find(original, "juice-page-1");
            page.OnChange(true);
        }
        return new PreviewScene {
            Slug = "juicecontrol-" + (presets ? "presets" : "effects") + "-preview",
            Title = "JuiceControl / " + (presets ? "角色与场景预设" : "效果与锁定"),
            Source = "JuiceControl.JuiceEditorView.Build", Detail = "Anonymous fixture; production " + (presets ? "presets" : "effects") + " page",
            Widget = view.Build(new BuildContext()),
            RequiredKeys = presets ? new[] { "juice-character-preset", "juice-group-preset", "juice-command-ReloadXml" }
                : new[] { "juice-effects", "juice-lock-all", "juice-command-Fill" }
        };
    }

    private static PreviewScene Accessory(bool copy) {
        string[] names = {
            "发夹 / 左", "发夹 / 右", "丝带", "耳环 / 左", "耳环 / 右", "项链", "胸针", "腕饰 / 左", "腕饰 / 右", "戒指", "腰带装饰", "肩饰"
        };
        AccessoryEditorModel model = new AccessoryEditorModel {
            Version = "0.7.0", Available = true, Page = copy ? 1 : 0,
            Status = copy ? "将来源的 3 个饰品按顺序映射到目标选区。Shift 可连续选择。" : "已选中 3 个饰品。移除前会显示确认；整理保持原有顺序。",
            Summary = "已选 3 / 48", Mapping = "来源 3 项 → 目标 3 项，按槽位顺序映射。",
            Sources = new[] { "校服", "便服", "运动服", "泳装", "坐标卡（示例）" },
            Destinations = new[] { "校服", "便服", "运动服", "泳装" },
            SourceIndex = 0, DestinationIndex = 1, FollowCoordinate = false,
            ImmediateSync = true, SelectionHighlight = true, ClearAfter = false,
            ReverseParent = false, SyncScroll = true,
            CanCopy = true, CanMove = true, CanInsert = true, CanAdjust = true,
            Confirming = false, Confirmation = string.Empty,
            ChangePage = delegate { }, ChangeSource = delegate { }, ChangeDestination = delegate { },
            SelectSlot = delegate { }, SelectionCommand = delegate { }, ChangeSetting = delegate { }, Execute = delegate { }
        };
        model.Batch = List(AccessoryListRole.Batch, 48, names, new[] { 1, 2, 5 });
        model.Source = List(AccessoryListRole.Source, 48, names, new[] { 1, 2, 5 });
        model.Destination = List(AccessoryListRole.Destination, 48, names, new[] { 3, 4, 5 });
        model.Destination.Scroll = model.Source.Scroll;
        return new PreviewScene {
            Slug = "accessory-batch-manager-" + (copy ? "copy" : "batch") + "-preview",
            Title = "AccessoryBatchManager / " + (copy ? "复制与移动" : "批量管理"),
            Source = "AccessoryBatchManager.AccessoryEditorView.Build", Detail = "Anonymous fixture; production " + (copy ? "copy" : "batch") + " page",
            Widget = new AccessoryEditorView { Model = model }.Build(new BuildContext()),
            RequiredKeys = copy ? new[] { "accessory-list-Source", "accessory-list-Destination", "accessory-command-Copy", "accessory-command-Adjust2UpDown" }
                : new[] { "accessory-list-Batch", "accessory-command-Remove", "accessory-command-Compact" }
        };
    }

    private static AccessoryListModel List(AccessoryListRole role, int count, string[] names, int[] selected) {
        return new AccessoryListModel {
            Role = role, Count = count, Clothing = false, CanToggleKind = true,
            Selection = new HashSet<int>(selected),
            GetName = i => (i + 1).ToString("00") + "  " + (i < names.Length ? names[i] : "（空槽位）")
        };
    }

    private static Widget Find(Widget root, string key) {
        if (Equals(root.Key, key)) return root;
        foreach (Widget child in root.Children) {
            Widget found = Find(child, key);
            if (found != null) return found;
        }
        return null;
    }
}

internal sealed class PreviewScene {
    internal string Slug;
    internal string Title;
    internal string Source;
    internal string Detail;
    internal Widget Widget;
    internal ThemeTokens Palette = ThemePalettes.MetroLight();
    internal string PaletteName = "Metro Light";
    internal float Width;
    internal float Height;
    internal string[] RequiredKeys = new string[0];
    internal bool Gallery;
    internal string PageId;
    internal string StateId = "default";
    internal string SizeName = "standard";
    internal Func<Widget> Build;
    internal string PaintState;
    internal object PaintKey;
    internal string PaintPath;
    internal string ScrollOf;
    internal string PopupKind;
    internal object PopupKey;
    internal string PopupFilter;
    internal int PopupMenuIndex;
    internal int PopupSubmenuIndex = -1;
    internal bool PopupBottom;
    internal readonly Dictionary<string, float> ScrollOffsets = new Dictionary<string, float>(StringComparer.Ordinal);

    internal PreviewScene Copy(string slug) {
        PreviewScene copy = new PreviewScene {
            Slug = slug, Title = Title, Source = Source, Detail = Detail, Widget = Widget,
            Palette = Palette, PaletteName = PaletteName, Width = Width, Height = Height,
            RequiredKeys = RequiredKeys, Gallery = Gallery, PageId = PageId,
            StateId = StateId, SizeName = SizeName, Build = Build,
            PaintState = PaintState, PaintKey = PaintKey, PaintPath = PaintPath,
            ScrollOf = ScrollOf, PopupKind = PopupKind, PopupKey = PopupKey,
            PopupFilter = PopupFilter, PopupMenuIndex = PopupMenuIndex,
            PopupSubmenuIndex = PopupSubmenuIndex, PopupBottom = PopupBottom
        };
        foreach (KeyValuePair<string, float> pair in ScrollOffsets) copy.ScrollOffsets.Add(pair.Key, pair.Value);
        return copy;
    }
}
