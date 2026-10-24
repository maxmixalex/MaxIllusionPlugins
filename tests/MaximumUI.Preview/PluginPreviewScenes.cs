using System;
using System.Collections.Generic;
using AccessoryBatchManager;
using JuiceControl;
using MaximumUI;

// Public examples only. These fixtures do not read cards, XML, configuration,
// the game, or the user's uploaded files. Composition is the shipped View code.
internal static class PluginPreviewScenes {
    internal static IEnumerable<PreviewScene> Create() {
        yield return Juice("main");
        yield return Juice("name");
        yield return Juice("xml-error");
        yield return Juice("empty");
        yield return Accessory(0);
        yield return Accessory(1);
        yield return Accessory(2);
        yield return Accessory(0, true);
    }

    private static PreviewScene Juice(string state) {
        JuiceEditorModel model = new JuiceEditorModel {
            Version = "0.2.1", Characters = new[] { "示例角色 A", "示例角色 B", "示例角色 C" },
            CharacterIndex = 0, HasCharacter = true, AllLocked = false,
            CharacterPresets = new[] { "自然效果", "雨后", "舞台妆容" },
            GroupPresets = new[] { "场景示例 A", "场景示例 B" },
            CharacterPresetIndex = 0, GroupPresetIndex = 0, CanSave = true,
            Error = string.Empty, Status = "已选择角色: 示例角色 A",
            Integration = "SkinEffects：示例扩展效果已连接。",
            NameDialogVisible = false, NamePrompt = string.Empty, Name = string.Empty, NameError = string.Empty,
            SelectCharacter = delegate { }, SelectCharacterPreset = delegate { }, SelectGroupPreset = delegate { },
            ChooseCharacterPreset = delegate { }, ChooseGroupPreset = delegate { },
            LockAll = delegate { }, ChangeName = delegate { }, Execute = delegate { }
        };
        string[] labels = { "脸部精液", "前上精液", "前下精液", "后上精液", "后下精液", "泪水", "脸红", "[Ex] 汗水", "[Ex] 水滴", "[Ex] 光泽", "[Ex] 红印", "[Ex] 眼影" };
        for (int i = 0; i < labels.Length; i++) {
            int maximum = i == 6 ? 100 : 3;
            int value = i == 6 ? 35 : i % 4;
            model.Effects.Add(new JuiceEffectRow {
                Id = "example-" + i, Name = labels[i], Maximum = maximum, Value = value,
                DisplayValue = value.ToString(), Locked = i == 2 || i == 5,
                Change = delegate { }, Lock = delegate { }
            });
        }
        if (state == "name") {
            model.NameDialogVisible = true; model.NamePrompt = "请输入汁液预设名"; model.Name = "自然效果";
        } else if (state == "xml-error") {
            model.CanSave = false;
            model.Error = "example.xml: Invalid data at line 8. Expected closing element.";
        } else if (state == "empty") {
            model.Characters = new string[0]; model.HasCharacter = false; model.Effects.Clear();
            model.CharacterPresets = new string[0]; model.GroupPresets = new string[0]; model.Status = "就绪。";
        }
        JuiceEditorView view = new JuiceEditorView { Model = model };
        return new PreviewScene {
            Slug = "juicecontrol-" + state + "-preview", Title = "JuiceControl",
            Source = "JuiceControl.JuiceEditorView.Build", Detail = "Anonymous fixture; restored pre-migration single window, " + state,
            Widget = view.Build(new BuildContext()), StateId = state == "main" ? "default" : state,
            RequiredKeys = state == "empty" ? new[] { "juice-no-character", "juice-character-preset", "juice-group-preset", "juice-command-ReloadXml" }
                : new[] { "juice-effects", "juice-lock-all", "juice-command-Fill", "juice-character-preset", "juice-group-preset", "juice-command-ReloadXml" }
        };
    }

    private static PreviewScene Accessory(int page, bool confirming = false) {
        bool copy = page == 1;
        string[] names = {
            "发夹 / 左", "发夹 / 右", "丝带", "耳环 / 左", "耳环 / 右", "项链", "胸针", "腕饰 / 左", "腕饰 / 右", "戒指", "腰带装饰", "肩饰"
        };
        AccessoryEditorModel model = new AccessoryEditorModel {
            Version = "0.7.1", Available = true, Page = page,
            Status = "就绪。",
            Summary = "饰品 12 / 槽位 48", Mapping = "按槽位编号从小到大一一对应，共 3 组。",
            Sources = new[] { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣", "服装卡（示例）" },
            Destinations = new[] { "学生服（校内）", "学生服（放学）", "体操服", "泳装", "社团", "私服", "睡衣" },
            SourceIndex = 0, DestinationIndex = 1, FollowCoordinate = false,
            ImmediateSync = true, SelectionHighlight = true, ClearAfter = false,
            ReverseParent = false, SyncScroll = true,
            CanCopy = true, CanMove = true, CanInsert = true, CanAdjust = true,
            Confirming = confirming, Confirmation = confirming ? "确定移除选中的 3 个槽位内容吗？" : string.Empty,
            ChangePage = delegate { }, ChangeSource = delegate { }, ChangeDestination = delegate { },
            SelectSlot = delegate { }, SelectionCommand = delegate { }, ChangeSetting = delegate { }, Execute = delegate { }
        };
        model.Batch = List(AccessoryListRole.Batch, 48, names, new[] { 1, 2, 5 });
        model.Source = List(AccessoryListRole.Source, 48, names, new[] { 1, 2, 5 });
        model.Destination = List(AccessoryListRole.Destination, 48, names, new[] { 3, 4, 5 });
        model.Destination.Scroll = model.Source.Scroll;
        return new PreviewScene {
            Slug = "accessory-batch-manager-" + (page == 2 ? "settings" : copy ? "copy" : "batch") + (confirming ? "-confirm" : "") + "-preview",
            Title = "AccessoryBatchManager / " + (page == 2 ? "设置" : copy ? "复制与移动" : "批量管理"),
            StateId = confirming ? "confirmation" : "default",
            Source = "AccessoryBatchManager.AccessoryEditorView.Build", Detail = "Anonymous fixture; production " + (page == 2 ? "settings" : copy ? "copy" : "batch") + " page",
            Widget = new AccessoryEditorView { Model = model }.Build(new BuildContext()),
            RequiredKeys = page == 2 ? new[] { "accessory-setting-ImmediateSync", "accessory-setting-SelectionHighlight" } : copy ? new[] { "accessory-list-Source", "accessory-list-Destination", "accessory-command-Copy", "accessory-command-Adjust2UpDown" }
                : new[] { "accessory-list-Batch", "accessory-command-Remove", "accessory-command-Compact" }
        };
    }

    private static AccessoryListModel List(AccessoryListRole role, int count, string[] names, int[] selected) {
        return new AccessoryListModel {
            Role = role, Count = count, Clothing = false, CanToggleKind = true,
            Selection = new HashSet<int>(selected),
            GetName = i => (i + 1) + "  " + (i < names.Length ? names[i] : "空")
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
    internal readonly Dictionary<string, float> HorizontalOffsets = new Dictionary<string, float>(StringComparer.Ordinal);
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
        foreach (KeyValuePair<string, float> pair in HorizontalOffsets) copy.HorizontalOffsets.Add(pair.Key, pair.Value);
        return copy;
    }
}
