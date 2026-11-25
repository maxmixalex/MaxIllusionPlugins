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

        internal WardrobePresetLibrary Copy() {
            WardrobePresetLibrary result = new WardrobePresetLibrary();
            foreach (WardrobePreset preset in Presets) result.Presets.Add(preset.Copy());
            return result;
        }

        internal void Validate() {
            if (Presets == null) throw new InvalidDataException("Missing preset list.");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WardrobePreset preset in Presets) {
                if (preset == null || !WardrobePreset.ValidName(preset.Name) || !names.Add(preset.Name))
                    throw new InvalidDataException("Invalid or duplicate preset name.");
                preset.Validate();
            }
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
                result.Garments.Add(new GarmentVisibility { Kind = entry.Kind, Item = entry.Item, Visible = entry.Visible });
            foreach (AccessoryVisibility entry in Accessories)
                result.Accessories.Add(new AccessoryVisibility { Slot = entry.Slot, Type = entry.Type, Item = entry.Item, Visible = entry.Visible });
            return result;
        }

        internal void Validate() {
            if (Coordinate < 0 || Accessories == null || Garments == null || ShoeMode < -1 || ShoeMode > 1)
                throw new InvalidDataException("Invalid outfit.");
            HashSet<string> garmentKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GarmentVisibility entry in Garments)
                if (entry == null || !GarmentVisibility.IsSupported(entry.Kind) || entry.Item <= 0 || !garmentKinds.Add(entry.Kind))
                    throw new InvalidDataException("Invalid or duplicate garment.");
            HashSet<int> slots = new HashSet<int>();
            foreach (AccessoryVisibility entry in Accessories)
                if (entry == null || entry.Slot < 0 || entry.Type < 0 || entry.Item < 0 || !slots.Add(entry.Slot))
                    throw new InvalidDataException("Invalid or duplicate accessory slot.");
        }
    }

    public sealed class GarmentVisibility {
        [XmlAttribute] public string Kind;
        [XmlAttribute] public int Item;
        [XmlAttribute] public bool Visible;

        internal static bool IsSupported(string kind) {
            return kind == "gloves" || kind == "socks" || kind == "shoes_inner" || kind == "shoes_outer";
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
                if (next == null) throw new InvalidDataException("Missing preset document.");
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
