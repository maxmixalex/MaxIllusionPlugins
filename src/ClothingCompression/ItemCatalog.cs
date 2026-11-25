using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sideloader.AutoResolver;
using UnityEngine;

namespace ClothingCompression {
    internal sealed class ItemDescriptor {
        internal BoundItemKind Kind;
        internal int Category;
        internal int Id;
        internal int Slot;
        internal string Name;
        internal string ModGuid;
        internal int OriginalId = -1;
        internal string Property;
        internal GameObject Root;
        internal bool Visible;
        internal string Key { get { return !string.IsNullOrEmpty(ModGuid) && OriginalId >= 0
            ? Kind + ":" + Category + ":uar:" + ModGuid + ":" + OriginalId + ":" + (Property ?? string.Empty)
            : Kind + ":" + Category + ":" + Id; } }
        internal string Display { get { return Kind == BoundItemKind.Clothing
            ? ItemCatalog.GetClothingSlotName(Slot) + " · " + Name
            : "饰品 " + (Slot + 1) + " · " + Name; } }
    }

    internal static class ItemCatalog {
        private static readonly ChaListDefine.CategoryNo[] ClothingCategories = {
            ChaListDefine.CategoryNo.co_top, ChaListDefine.CategoryNo.co_bot, ChaListDefine.CategoryNo.co_bra,
            ChaListDefine.CategoryNo.co_shorts, ChaListDefine.CategoryNo.co_gloves, ChaListDefine.CategoryNo.co_panst,
            ChaListDefine.CategoryNo.co_socks, ChaListDefine.CategoryNo.co_shoes, ChaListDefine.CategoryNo.co_shoes
        };
        private static readonly string[] ClothingFallback = { "上衣", "下衣", "胸罩", "内裤", "手套", "连裤袜", "袜子", "鞋子(室内)", "鞋子(室外)" };

        internal static string GetClothingSlotName(int slot) {
            return slot >= 0 && slot < ClothingFallback.Length ? ClothingFallback[slot] : "服装";
        }

        internal static List<ItemDescriptor> Get(ChaControl cha) {
            List<ItemDescriptor> result = new List<ItemDescriptor>();
            if (cha == null || cha.nowCoordinate == null) return result;
            ChaFileClothes.PartsInfo[] clothes = cha.nowCoordinate.clothes == null ? null : cha.nowCoordinate.clothes.parts;
            if (clothes != null) {
                for (int i = 0; i < Math.Min(clothes.Length, ClothingCategories.Length); i++) {
                    if (clothes[i] == null || clothes[i].id == 0) continue;
                    GameObject root = cha.objClothes != null && i < cha.objClothes.Length ? cha.objClothes[i] : null;
                    int state = cha.fileStatus != null && cha.fileStatus.clothesState != null && i < cha.fileStatus.clothesState.Length ? cha.fileStatus.clothesState[i] : 0;
                    ItemDescriptor item = new ItemDescriptor { Kind = BoundItemKind.Clothing, Category = (int)ClothingCategories[i], Id = clothes[i].id, Slot = i,
                        Name = GetName(cha, ClothingCategories[i], clothes[i].id, ClothingFallback[i]), Root = root, Visible = state < 2 && IsVisible(root) };
                    PopulateStableIdentity(item); result.Add(item);
                }
            }
            ChaFileAccessory.PartsInfo[] accessories = cha.nowCoordinate.accessory == null ? null : cha.nowCoordinate.accessory.parts;
            if (accessories != null) {
                for (int i = 0; i < accessories.Length; i++) {
                    ChaFileAccessory.PartsInfo part = accessories[i];
                    if (part == null || part.type == 120) continue;
                    GameObject root = cha.objAccessory != null && i < cha.objAccessory.Length ? cha.objAccessory[i] : null;
                    bool shown = cha.fileStatus == null || cha.fileStatus.showAccessory == null || i >= cha.fileStatus.showAccessory.Length || cha.fileStatus.showAccessory[i];
                    ItemDescriptor item = new ItemDescriptor { Kind = BoundItemKind.Accessory, Category = part.type, Id = part.id, Slot = i,
                        Name = GetName(cha, (ChaListDefine.CategoryNo)part.type, part.id, "未知饰品"), Root = root, Visible = shown && IsVisible(root) };
                    PopulateStableIdentity(item); result.Add(item);
                }
            }
            return result;
        }

        internal static ItemDescriptor Resolve(ChaControl cha, DeformationEntry entry) {
            return Resolve(Get(cha), entry);
        }

        internal static ItemDescriptor Resolve(IList<ItemDescriptor> items, DeformationEntry entry) {
            if (entry == null || entry.BindingKind == BoundItemKind.None) return null;
            if (items == null) return null;
            ItemDescriptor stable = items.FirstOrDefault(x => Matches(entry, x) && x.Slot == entry.PreferredSlot)
                ?? items.FirstOrDefault(x => Matches(entry, x));
            if (stable != null) return stable;
            return items.FirstOrDefault(x => x.Kind == entry.BindingKind && x.Slot == entry.PreferredSlot &&
                string.Equals(x.Name ?? string.Empty, entry.BindingName ?? string.Empty, StringComparison.Ordinal));
        }

        internal static bool Matches(DeformationEntry entry, ItemDescriptor item) {
            if (entry == null || item == null || entry.BindingKind != item.Kind || entry.BindingCategory != item.Category) return false;
            if (!string.IsNullOrEmpty(entry.BindingModGuid) && entry.BindingOriginalId >= 0)
                return string.Equals(entry.BindingModGuid, item.ModGuid, StringComparison.OrdinalIgnoreCase) &&
                    entry.BindingOriginalId == item.OriginalId &&
                    (string.IsNullOrEmpty(entry.BindingProperty) || string.Equals(entry.BindingProperty, item.Property, StringComparison.Ordinal));
            return entry.BindingId == item.Id;
        }

        internal static void Bind(DeformationEntry entry, ItemDescriptor item) {
            if (item == null) {
                entry.BindingKind = BoundItemKind.None; entry.BindingCategory = 0; entry.BindingId = 0;
                entry.BindingModGuid = string.Empty; entry.BindingOriginalId = -1; entry.BindingProperty = string.Empty;
                entry.PreferredSlot = -1; entry.BindingName = string.Empty; return;
            }
            entry.BindingKind = item.Kind; entry.BindingCategory = item.Category; entry.BindingId = item.Id;
            entry.BindingModGuid = item.ModGuid; entry.BindingOriginalId = item.OriginalId; entry.BindingProperty = item.Property;
            entry.PreferredSlot = item.Slot; entry.BindingName = item.Name;
        }

        private static void PopulateStableIdentity(ItemDescriptor item) {
            if (item == null) return;
            try {
                ResolveInfo info = UniversalAutoResolver.TryGetResolutionInfo((ChaListDefine.CategoryNo)item.Category, item.Id);
                if (info == null) return;
                item.ModGuid = info.GUID == null ? string.Empty : info.GUID.Trim();
                item.OriginalId = info.Slot;
                item.Property = info.Property ?? string.Empty;
            } catch (Exception ex) {
                ClothingCompressionPlugin.Log.LogWarning("Could not resolve stable Sideloader identity for " + item.Category + ":" + item.Id + ": " + ex.Message);
            }
        }

        private static bool IsVisible(GameObject root) {
            if (root == null || !root.activeInHierarchy) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            return renderers.Length == 0 || renderers.Any(x => x.enabled && x.gameObject.activeInHierarchy);
        }

        private static string GetName(ChaControl cha, ChaListDefine.CategoryNo category, int id, string fallback) {
            try { ListInfoBase info = cha.lstCtrl.GetListInfo(category, id); return info == null || string.IsNullOrEmpty(info.Name) ? fallback + " (" + id + ")" : ItemNameLocalizer.Localize(info.Name); }
            catch { return fallback + " (" + id + ")"; }
        }

    }

    internal static class ItemNameLocalizer {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Type AutoTranslatorType = Type.GetType("XUnity.AutoTranslator.Plugin.Core.AutoTranslator, XUnity.AutoTranslator.Plugin.Core", false);
        private static readonly PropertyInfo DefaultTranslatorProperty = AutoTranslatorType == null ? null : AutoTranslatorType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo TryTranslateMethod = FindTryTranslateMethod();
        private static readonly Type ChineseTextType = Type.GetType("KK_Chinese.Core.Patch.Text_Display, KK_Chinese", false);
        private static readonly MethodInfo ChineseGetTextMethod = ChineseTextType == null ? null : ChineseTextType.GetMethod("GetText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        internal static string Localize(string value) {
            if (string.IsNullOrEmpty(value)) return value;
            string cached; if (Cache.TryGetValue(value, out cached)) return cached;
            string source = value;
            try { if (ChineseGetTextMethod != null) { string translated = ChineseGetTextMethod.Invoke(null, new object[] { value }) as string; if (!string.IsNullOrEmpty(translated) && translated != value) return Cache[value] = translated; } } catch { }
            try { object translator = DefaultTranslatorProperty == null ? null : DefaultTranslatorProperty.GetValue(null, null); if (translator != null && TryTranslateMethod != null) { object[] args = { value, null }; if ((bool)TryTranslateMethod.Invoke(translator, args) && args[1] is string) value = (string)args[1]; } } catch { }
            return Cache[source] = value;
        }
        private static MethodInfo FindTryTranslateMethod() {
            if (DefaultTranslatorProperty == null) return null;
            return DefaultTranslatorProperty.PropertyType.GetMethods().FirstOrDefault(x => { ParameterInfo[] p = x.GetParameters(); return x.Name == "TryTranslate" && p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string).MakeByRefType(); });
        }
    }
}
