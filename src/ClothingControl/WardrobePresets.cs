using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using MaxIllusionPlugins.Common;

namespace ClothingControl {
    [XmlRoot("ClothingPresets")]
    public sealed class WardrobePresetLibrary {
        [XmlElement("Preset")]
        public List<WardrobePreset> Presets = new List<WardrobePreset>();
        [XmlElement("ScenePreset")]
        public List<WardrobeScenePreset> Scenes = new List<WardrobeScenePreset>();

        internal WardrobePresetLibrary Copy() {
            WardrobePresetLibrary result = new WardrobePresetLibrary();
            foreach (WardrobePreset preset in Presets) result.Presets.Add(preset.Copy());
            foreach (WardrobeScenePreset scene in Scenes) result.Scenes.Add(scene.Copy());
            return result;
        }

        internal void Validate() {
            if (Presets == null) throw new IOException("Missing preset list.");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WardrobePreset preset in Presets) {
                if (preset == null || !WardrobePreset.ValidName(preset.Name) || !names.Add(preset.Name))
                    throw new IOException("Invalid or duplicate preset name.");
                preset.Validate();
            }
            if (Scenes == null) throw new IOException("Missing scene preset list.");
            names.Clear();
            foreach (WardrobeScenePreset scene in Scenes) {
                if (scene == null || !WardrobePreset.ValidName(scene.Name) || !names.Add(scene.Name))
                    throw new IOException("Invalid or duplicate scene preset name.");
                scene.Validate();
            }
        }
    }

    public sealed class WardrobeScenePreset {
        [XmlAttribute] public string Name;
        [XmlElement("Character")]
        public List<WardrobeSceneMember> Members = new List<WardrobeSceneMember>();

        internal WardrobeScenePreset Copy() {
            WardrobeScenePreset result = new WardrobeScenePreset { Name = Name };
            foreach (WardrobeSceneMember entry in Members) result.Members.Add(entry.Copy());
            return result;
        }

        internal void Validate() {
            if (Members == null || Members.Count == 0) throw new IOException("Empty scene preset.");
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (WardrobeSceneMember entry in Members) {
                if (entry == null || string.IsNullOrEmpty(entry.Key) || entry.Key.Length > 1024 ||
                    entry.State == null || !keys.Add(entry.Key))
                    throw new IOException("Invalid or ambiguous character identity.");
                entry.State.Validate();
            }
        }
    }

    public sealed class WardrobeSceneMember {
        [XmlAttribute] public string Key;
        [XmlAttribute] public string DisplayName;
        [XmlElement("State")] public WardrobePreset State;

        internal WardrobeSceneMember Copy() {
            return new WardrobeSceneMember { Key = Key, DisplayName = DisplayName, State = State.Copy() };
        }
    }

    internal sealed class WardrobeSceneMatch<T> where T : class {
        internal T Target;
        internal WardrobeSceneMember Member;
    }

    internal static class WardrobeSceneIdentity {
        // Studio object numbers change across scene loads. Use card name plus
        // basename where present, never the user's absolute card path.
        internal static string Key(string name, string cardFile) {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0) return null;
            string file = (cardFile ?? string.Empty).Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            if (slash >= 0) file = file.Substring(slash + 1);
            file = file.Trim().ToUpperInvariant();
            string key = name.Length + ":" + name + "|" + file;
            return key.Length <= 1024 ? key : null;
        }

        internal static T FindUnique<T>(IEnumerable<T> targets, string key, Func<T, string> identity) where T : class {
            if (string.IsNullOrEmpty(key)) return null;
            T result = null;
            foreach (T target in targets) {
                if (target == null || !string.Equals(identity(target), key, StringComparison.Ordinal)) continue;
                if (result != null && !ReferenceEquals(result, target)) return null;
                result = target;
            }
            return result;
        }

        internal static List<WardrobeSceneMatch<T>> Match<T>(WardrobeScenePreset preset,
            IEnumerable<T> targets, Func<T, string> identity) where T : class {
            List<WardrobeSceneMatch<T>> result = new List<WardrobeSceneMatch<T>>();
            if (preset == null) return result;
            foreach (WardrobeSceneMember member in preset.Members) {
                T target = FindUnique(targets, member.Key, identity);
                if (target != null) result.Add(new WardrobeSceneMatch<T> { Target = target, Member = member.Copy() });
            }
            return result;
        }
    }

    public sealed class WardrobePreset {
        [XmlAttribute] public string Name;
        [XmlAttribute] public int Coordinate;
        [XmlAttribute] public int ShoeMode = -1;
        [XmlElement("Garment")]
        public List<GarmentVisibility> Garments = new List<GarmentVisibility>();
        public bool ShouldSerializeShoeMode() { return ShoeMode >= 0; }

        [XmlElement("Accessory")]
        public List<AccessoryVisibility> Accessories = new List<AccessoryVisibility>();

        internal static bool ValidName(string name) {
            return !string.IsNullOrEmpty(name) && name.Trim() == name && name.Length <= 80;
        }

        internal WardrobePreset Copy() {
            WardrobePreset result = new WardrobePreset { Name = Name, Coordinate = Coordinate, ShoeMode = ShoeMode };
            foreach (GarmentVisibility entry in Garments)
                result.Garments.Add(new GarmentVisibility {
                    Kind = entry.Kind, Item = entry.Item, Visible = entry.Visible, State = entry.State
                });
            foreach (AccessoryVisibility entry in Accessories)
                result.Accessories.Add(new AccessoryVisibility { Slot = entry.Slot, Type = entry.Type, Item = entry.Item, Visible = entry.Visible });
            return result;
        }

        internal void Validate() {
            if (Coordinate < 0 || Accessories == null || Garments == null || ShoeMode < -1 || ShoeMode > 1)
                throw new IOException("Invalid outfit.");
            HashSet<string> garmentKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GarmentVisibility entry in Garments)
                if (entry == null || !GarmentVisibility.IsSupported(entry.Kind) || entry.Item <= 0 ||
                    entry.State < -1 || entry.State > 3 || !garmentKinds.Add(entry.Kind))
                    throw new IOException("Invalid or duplicate garment.");
            HashSet<int> slots = new HashSet<int>();
            foreach (AccessoryVisibility entry in Accessories)
                if (entry == null || entry.Slot < 0 || entry.Type < 0 || entry.Item < 0 || !slots.Add(entry.Slot))
                    throw new IOException("Invalid or duplicate accessory slot.");
        }
    }

    // One-piece tops (ChaControl.notBot) own both top/bottom clothing
    // state channels. The game itself builds dictStateType[1] from the
    // top model even when coordinate.clothes.parts[1].id is 0.
    // This logic mirrors ChaControl.AddClothesStateKind and
    // SetClothesState's notBot handling. It is NOT a synthetic garment.
    internal static class WardrobeGarmentIdentity {
        internal struct Binding {
            internal int Item;
            internal int StateSlot;
            internal bool Linked;
        }

        // A single garment can own multiple native state channels:
        // notBot: top(0) -> bot(1);
        // notBra: top(0) -> bra(2);
        // notShorts: bra(2) -> shorts(3).
        // When the child state channel is absent, only the real owner's
        // state may be cycled, never a fake state on an empty slot.
        internal static Binding Resolve(int slot, int ownItem, int topItem,
            int braItem, bool notBot, bool notBra, bool notShorts,
            bool hasOwnState, bool hasTopState, bool hasBraState) {
            Binding result = new Binding {
                Item = ownItem > 0 ? ownItem : 0,
                StateSlot = slot,
                Linked = false
            };
            if (slot == 1 && notBot && topItem > 0) {
                result.Linked = true;
                result.Item = topItem;
                result.StateSlot = hasOwnState ? 1 :
                    hasTopState ? 0 : -1;
            } else if (slot == 2 && notBra && topItem > 0) {
                result.Linked = true;
                result.Item = topItem;
                result.StateSlot = hasOwnState ? 2 :
                    hasTopState ? 0 : -1;
            } else if (slot == 3 && notShorts) {
                int ownerItem = notBra ? topItem : braItem;
                if (ownerItem > 0) {
                    result.Linked = true;
                    result.Item = ownerItem;
                    result.StateSlot = hasOwnState ? 3 :
                        !notBra && hasBraState ? 2 :
                        notBra && hasTopState ? 0 : -1;
                }
            }
            // Preserve the identity for display, but never write to a
            // nonexistent native channel: the caller checks StateSlot.
            return result;
        }

        internal static bool IsLinkedBottom(string kind, bool topOwnsBottom,
            int topItem, bool bottomStateChannelExists) {
            return kind == "bot" && topOwnsBottom && topItem > 0 &&
                bottomStateChannelExists;
        }

        internal static int EffectiveItem(string kind, int ownItem,
            int topItem, bool linkedBottom) {
            if (kind == "bot" && linkedBottom)
                return topItem > 0 ? topItem : 0;
            return ownItem > 0 ? ownItem : 0;
        }
    }

    public sealed class GarmentVisibility {
        [XmlAttribute] public string Kind;
        [XmlAttribute] public int Item;
        [XmlAttribute] public bool Visible;
        [XmlAttribute] public int State = -1;
        public bool ShouldSerializeState() { return State >= 0; }

        internal static bool IsSupported(string kind) {
            return kind == "top" || kind == "bot" || kind == "bra" ||
                kind == "shorts" || kind == "gloves" || kind == "panst" ||
                kind == "socks" || kind == "shoes_inner" || kind == "shoes_outer";
        }

        internal bool Matches(string kind, int item) {
            return IsSupported(Kind) && Kind == kind && Item == item;
        }
    }

    public sealed class AccessoryVisibility {
        [XmlAttribute] public int Slot;
        [XmlAttribute] public int Type;
        [XmlAttribute] public int Item;
        [XmlAttribute] public bool Visible;

        internal bool Matches(int slot, int type, int item) {
            return Slot == slot && Type == type && Item == item;
        }
    }

    internal sealed class WardrobePresetStore {
        private readonly string path;
        private WardrobePresetLibrary library = new WardrobePresetLibrary();
        internal string Error { get; private set; }
        internal bool CanWrite { get { return string.IsNullOrEmpty(Error); } }

        internal WardrobePresetStore(string path) { this.path = path; }

        internal bool Reload() {
            try {
                WardrobePresetLibrary next = File.Exists(path)
                    ? AtomicXmlFile.Read<WardrobePresetLibrary>(path) : new WardrobePresetLibrary();
                if (next == null) throw new IOException("Missing preset document.");
                next.Validate();
                library = next;
                Error = null;
                return true;
            } catch (Exception ex) {
                Error = ex.GetBaseException().Message;
                return false;
            }
        }

        internal string[] Names() {
            List<string> names = new List<string>();
            foreach (WardrobePreset entry in library.Presets) names.Add(entry.Name);
            return names.ToArray();
        }

        private static int IndexOf(WardrobePresetLibrary source, string name) {
            for (int i = 0; i < source.Presets.Count; i++)
                if (string.Equals(source.Presets[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        internal WardrobePreset Find(string name) {
            int index = IndexOf(library, name);
            return index < 0 ? null : library.Presets[index].Copy();
        }

        internal bool Save(WardrobePreset snapshot, string name, bool overwrite, out string message) {
            if (!CanWrite) { message = Error; return false; }
            name = (name ?? string.Empty).Trim();
            if (!WardrobePreset.ValidName(name) || snapshot == null) { message = "预设名称或数据无效。"; return false; }
            try {
                WardrobePresetLibrary next = library.Copy();
                int index = IndexOf(next, name);
                if (overwrite != (index >= 0)) {
                    message = overwrite ? "预设已不存在。" : "预设名称已存在。";
                    return false;
                }
                WardrobePreset preset = snapshot.Copy();
                preset.Name = name;
                if (index >= 0) next.Presets[index] = preset; else next.Presets.Add(preset);
                next.Validate();
                return Write(next, out message);
            } catch (Exception ex) { message = ex.GetBaseException().Message; return false; }
        }

        internal bool Delete(string name, out string message) {
            if (!CanWrite) { message = Error; return false; }
            WardrobePresetLibrary next = library.Copy();
            int index = IndexOf(next, name);
            if (index < 0) { message = "预设已不存在。"; return false; }
            next.Presets.RemoveAt(index);
            return Write(next, out message);
        }

        internal string[] SceneNames() {
            List<string> names = new List<string>();
            foreach (WardrobeScenePreset entry in library.Scenes) names.Add(entry.Name);
            return names.ToArray();
        }

        private static int SceneIndex(WardrobePresetLibrary source, string name) {
            for (int i = 0; i < source.Scenes.Count; i++)
                if (string.Equals(source.Scenes[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        internal WardrobeScenePreset FindScene(string name) {
            int index = SceneIndex(library, name);
            return index < 0 ? null : library.Scenes[index].Copy();
        }

        internal bool SaveScene(WardrobeScenePreset snapshot, string name, bool overwrite, out string message) {
            if (!CanWrite) { message = Error; return false; }
            name = (name ?? string.Empty).Trim();
            if (!WardrobePreset.ValidName(name) || snapshot == null) { message = "场景预设名称或数据无效。"; return false; }
            try {
                WardrobePresetLibrary next = library.Copy();
                int index = SceneIndex(next, name);
                if (overwrite != (index >= 0)) {
                    message = overwrite ? "场景预设已不存在。" : "场景预设名称已存在。";
                    return false;
                }
                snapshot.Validate();
                WardrobeScenePreset copy = snapshot.Copy();
                copy.Name = name;
                if (index >= 0) next.Scenes[index] = copy; else next.Scenes.Add(copy);
                next.Validate();
                return Write(next, out message);
            } catch (Exception ex) { message = ex.GetBaseException().Message; return false; }
        }

        internal bool DeleteScene(string name, out string message) {
            if (!CanWrite) { message = Error; return false; }
            WardrobePresetLibrary next = library.Copy();
            int index = SceneIndex(next, name);
            if (index < 0) { message = "场景预设已不存在。"; return false; }
            next.Scenes.RemoveAt(index);
            return Write(next, out message);
        }

        private bool Write(WardrobePresetLibrary next, out string message) {
            try {
                AtomicXmlFile.Write(path, next, path + ".bak");
                library = next;
                message = null;
                return true;
            } catch (Exception ex) {
                message = ex.GetBaseException().Message;
                return false;
            }
        }
    }
}
