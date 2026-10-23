using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using MaxIllusionPlugins.Common;
using UnityEngine;

namespace JuiceControl {
    [XmlRoot("JuiceControlPresets")]
    public class PresetXml {
        [XmlArray("CharacterPresets")]
        [XmlArrayItem("Preset")]
        public List<CharacterPreset> CharacterPresets = new List<CharacterPreset>();

        [XmlArray("GroupPresets")]
        [XmlArrayItem("Preset")]
        public List<GroupPreset> GroupPresets = new List<GroupPreset>();

        public static PresetXml Load(string path) {
            if (!File.Exists(path)) {
                PresetXml created = new PresetXml();
                created.Save(path);
                return created;
            }

            PresetXml loaded = AtomicXmlFile.Read<PresetXml>(path);
            if (loaded == null) throw new InvalidDataException("Preset XML is empty.");
            loaded.Normalize();
            return loaded;
        }

        public void Save(string path) {
            AtomicXmlFile.Write(path, this);
        }

        private void Normalize() {
            if (CharacterPresets == null) CharacterPresets = new List<CharacterPreset>();
            if (GroupPresets == null) GroupPresets = new List<GroupPreset>();
            CharacterPresets.RemoveAll(preset => preset == null);
            GroupPresets.RemoveAll(preset => preset == null);
            for (int i = 0; i < CharacterPresets.Count; i++) {
                if (CharacterPresets[i].State == null) CharacterPresets[i].State = new CharaSkinState();
                CharacterPresets[i].State.Normalize();
            }

            for (int i = 0; i < GroupPresets.Count; i++) {
                if (GroupPresets[i].Characters == null) GroupPresets[i].Characters = new List<RolePreset>();
                // XmlElement collections on the legacy serializer can turn a nil
                // element into an empty object; it cannot map to an actual role.
                GroupPresets[i].Characters.RemoveAll(role => role == null || string.IsNullOrEmpty(role.Role));
                for (int j = 0; j < GroupPresets[i].Characters.Count; j++) {
                    if (GroupPresets[i].Characters[j].State == null) GroupPresets[i].Characters[j].State = new CharaSkinState();
                    GroupPresets[i].Characters[j].State.Normalize();
                }
            }
        }

        public CharacterPreset FindCharacterPreset(string name) {
            for (int i = 0; i < CharacterPresets.Count; i++) {
                if (string.Equals(CharacterPresets[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return CharacterPresets[i];
                }
            }

            return null;
        }

        public GroupPreset FindGroupPreset(string name) {
            for (int i = 0; i < GroupPresets.Count; i++) {
                if (string.Equals(GroupPresets[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return GroupPresets[i];
                }
            }

            return null;
        }
    }

    public class CharacterPreset {
        [XmlAttribute("name")]
        public string Name = string.Empty;

        [XmlElement("State")]
        public CharaSkinState State = new CharaSkinState();
    }

    public class GroupPreset {
        [XmlAttribute("name")]
        public string Name = string.Empty;

        [XmlElement("Character")]
        public List<RolePreset> Characters = new List<RolePreset>();

        public RolePreset FindRole(string role) {
            for (int i = 0; i < Characters.Count; i++) {
                if (string.Equals(Characters[i].Role, role, StringComparison.OrdinalIgnoreCase)) {
                    return Characters[i];
                }
            }

            return null;
        }
    }

    public class RolePreset {
        [XmlAttribute("role")]
        public string Role = string.Empty;

        [XmlAttribute("displayName")]
        public string DisplayName = string.Empty;

        [XmlElement("State")]
        public CharaSkinState State = new CharaSkinState();
    }

    public class CharaSkinState {
        [XmlAttribute("tear")]
        public byte TearLevel;

        [XmlAttribute("blush")]
        public int BlushLevel;

        [XmlArray("Siru")]
        [XmlArrayItem("Part")]
        public List<NamedLevel> Siru = new List<NamedLevel>();

        [XmlArray("SkinEffects")]
        [XmlArrayItem("Effect")]
        public List<NamedLevel> SkinEffects = new List<NamedLevel>();

        internal static CharaSkinState CreateMax(int siruMax, int tearMax, int blushMax, List<SkinEffectInfo> effects) {
            CharaSkinState state = new CharaSkinState {
                TearLevel = (byte)tearMax,
                BlushLevel = blushMax
            };

            foreach (ChaFileDefine.SiruParts part in Enum.GetValues(typeof(ChaFileDefine.SiruParts))) {
                state.SetSiruLevel(part, (byte)siruMax);
            }

            if (effects != null) {
                for (int i = 0; i < effects.Count; i++) {
                    state.SetSkinEffectLevel(effects[i].Name, effects[i].MaxLevel);
                }
            }

            return state;
        }

        public byte GetSiruLevel(ChaFileDefine.SiruParts part) {
            NamedLevel level = Find(Siru, part.ToString());
            return level == null ? (byte)0 : (byte)Mathf.Clamp(level.Level, 0, byte.MaxValue);
        }

        public void SetSiruLevel(ChaFileDefine.SiruParts part, byte level) {
            SetLevel(Siru, part.ToString(), level);
        }

        public int GetSkinEffectLevel(string name) {
            NamedLevel level = Find(SkinEffects, name);
            return level == null ? 0 : level.Level;
        }

        public void SetSkinEffectLevel(string name, int level) {
            SetLevel(SkinEffects, name, level);
        }

        public void Normalize() {
            if (Siru == null) Siru = new List<NamedLevel>();
            if (SkinEffects == null) SkinEffects = new List<NamedLevel>();
        }

        private static NamedLevel Find(List<NamedLevel> levels, string name) {
            if (levels == null) return null;

            for (int i = 0; i < levels.Count; i++) {
                if (levels[i] != null && string.Equals(levels[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return levels[i];
                }
            }

            return null;
        }

        private static void SetLevel(List<NamedLevel> levels, string name, int level) {
            NamedLevel namedLevel = Find(levels, name);
            if (namedLevel == null) {
                namedLevel = new NamedLevel { Name = name };
                levels.Add(namedLevel);
            }

            namedLevel.Level = level;
        }
    }

    public class NamedLevel {
        [XmlAttribute("name")]
        public string Name = string.Empty;

        [XmlAttribute("level")]
        public int Level;
    }
}
