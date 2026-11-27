using System;
using System.Collections.Generic;
using AccessoryBatchManager;
using ClothingControl;
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
        yield return Wardrobe("main");
        yield return Wardrobe("busy");
        yield return Wardrobe("empty");
        yield return Wardrobe("xml-error");
        yield return Wardrobe("garments");
        yield return Wardrobe("free-h");
        yield return Wardrobe("filtered");
        yield return Wardrobe("scene");
        yield return Wardrobe("scene-applying");
    }

    private static PreviewScene Wardrobe(string state) {
        var model = new WardrobeViewModel {
            Version = "0.5.5", Targets = new[] { "1. 示例角色 A", "2. 示例角色 B" },
            Coordinates = new[] { "日常配装", "旅行配装", "运动配装" },
            Presets = new[] { "日常配装", "旅行配装" }, TargetIndex = 0, CoordinateIndex = 1, PresetIndex = 0,
            Name = "旅行配装", Status = "就绪。", CanWrite = true, HasTarget = true,
            SelectTarget = delegate { }, SelectCoordinate = delegate { }, SelectPreset = delegate { },
            ChangeName = delegate { }, Apply = delegate { }, Create = delegate { },
            Overwrite = delegate { }, Delete = delegate { }, Reload = delegate { }, Close = delegate { },
            ChangePage = delegate { }, ChangeAccessoryGroup = delegate { }, ChangeAccessoryQuery = delegate { },
            ChangeShoeMode = delegate { }, RestoreClothing = delegate { }, SetFilteredAccessories = delegate { }
        };
        string[] names = { "帽子", "眼镜", "围巾", "发夹", "手表", "胸针", "耳饰", "背包" };
        for (int i = 0; i < 32; i++) model.Rows.Add(new WardrobeRow {
            Slot = i, Type = 121, Item = i + 1, Group = i % 2, Label = (i + 1) + ". " + names[i % names.Length],
            Visible = i % 3 != 0, Change = delegate { }
        });
        // Clothing is now tab 0; accessory-heavy previews use tab 1.
        model.Page = state == "main" || state == "garments" ||
            state == "free-h" ? 0 : 1;
        if (state == "free-h") {
            model.CanChangeCoordinate = false;
            model.Targets = new[] { "1. 活动角色 A", "2. 活动角色 B", "3. 活动角色 C" };
            model.HasTarget = true;
            model.Status = "Free H：当前配装的服装状态可编辑，配装重载已禁用。";
        }
        if (state == "main" || state == "garments" || state == "free-h") {
            model.ShoeMode = 0;
            string[] garmentLabels = {
                "上衣", "下装", "胸衣", "内裤", "手套", "连裤袜", "袜子", "室内鞋", "室外鞋"
            };
            for (int i = 0; i < garmentLabels.Length; i++) model.Garments.Add(new WardrobeRow {
                Slot = i, Item = i + 1, Label = garmentLabels[i],
                State = i < 4 ? i % 4 : i % 2 == 0 ? 0 : 3,
                AllowedStates = i < 4 ? new[] { 0, 1, 2, 3 } : new[] { 0, 3 },
                ChangeState = delegate { }
            });
        }
        if (state == "filtered") { model.AccessoryGroup = 2; model.AccessoryQuery = "围巾"; }
        if (state == "scene" || state == "scene-applying") {
            model.Scope = 1; model.CanScenePresets = true; model.HasSceneTargets = true;
            model.Presets = new[] { "旅行组合", "舞台配装" }; model.Name = "旅行组合";
            model.ChangeScope = delegate { }; model.Cancel = delegate { };
            if (state == "scene-applying") {
                model.Busy = true; model.Status = "场景预设 旅行组合：1/2（成功 1，部分 0，跳过 0）";
            }
        }
        if (state == "busy") { model.Busy = true; model.Status = "正在载入配装…"; }
        if (state == "empty") {
            model.Targets = new string[0]; model.Coordinates = new string[0];
            model.TargetIndex = -1; model.CoordinateIndex = -1; model.HasTarget = false;
            model.Rows.Clear(); model.Status = "当前场景中没有可用角色。";
        }
        if (state == "xml-error") {
            model.CanWrite = false; model.Error = "example.xml: Invalid XML at line 8. Expected closing element. Reload after repairing the source file.";
        }
        Window window = (Window)new WardrobeView { Model = model }.Build(new BuildContext());
        return new PreviewScene {
            Slug = "clothingcontrol-" + state + "-preview", Title = "ClothingControl",
            Source = "ClothingControl.WardrobeView.Build", Detail = "Synthetic wardrobe fixture; " + state,
            Widget = window, StateId = state == "main" ? "default" : state,
            Width = state == "xml-error" ? window.MinWidth : 0f,
            Height = state == "xml-error" ? window.MinHeight : 0f,
            RequiredKeys = state == "main" || state == "garments" || state == "free-h"
                ? new[] { "wardrobe-target-current", "wardrobe-coordinate-current",
                    "wardrobe-garment-rows", "garment-state-0-current", "wardrobe-shoe-mode",
                    "wardrobe-restore-clothing", "wardrobe-preset-current" }
                : new[] { "wardrobe-target-current", "wardrobe-coordinate-current", "wardrobe-accessories",
                    "wardrobe-preset-current", "wardrobe-name", "wardrobe-apply", "wardrobe-reload",
                    "wardrobe-accessory-search", "wardrobe-accessory-group-current", "wardrobe-hide-filtered" }
        };
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
