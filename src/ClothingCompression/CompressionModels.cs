using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using BepInEx;
using MaxIllusionPlugins.Common;

namespace ClothingCompression {
    // Keep existing numeric values stable for XML compatibility.
    public enum DeformationKind { Thigh = 0, Arm = 1, Knee = 2, LowerThigh = 3, UpperArm = 4 }
    public enum BoundItemKind { None, Clothing, Accessory }
    public enum SaveScope { CharacterAndItem, ItemOnly }

    [Serializable]
    public sealed class DeformationEntry {
        [XmlAttribute] public string Id;
        [XmlAttribute] public DeformationKind Kind;
        [XmlAttribute] public bool Enabled = true;
        [XmlAttribute] public bool Left = true;
        [XmlAttribute] public bool Right = true;
        [XmlAttribute] public float Position;
        [XmlAttribute] public float Width;
        [XmlAttribute] public float Depth;
        [XmlAttribute] public float ProximalBulge;
        [XmlAttribute] public float DistalBulge;
        [XmlAttribute] public float GarmentClearanceX = 0.0015f;
        [XmlAttribute] public float GarmentClearanceY = 0.0015f;
        [XmlAttribute] public float GarmentClearanceZ = 0.0015f;
        [XmlAttribute] public SaveScope SaveScope = SaveScope.CharacterAndItem;
        [XmlAttribute] public BoundItemKind BindingKind;
        [XmlAttribute] public int BindingCategory;
        [XmlAttribute] public int BindingId;
        [XmlAttribute] public string BindingModGuid;
        [XmlAttribute] public int BindingOriginalId = -1;
        [XmlAttribute] public string BindingProperty;
        [XmlAttribute] public int PreferredSlot = -1;
        [XmlAttribute] public string BindingName;

        public string ItemKey { get {
            if (BindingKind == BoundItemKind.None) return string.Empty;
            return !string.IsNullOrEmpty(BindingModGuid) && BindingOriginalId >= 0
                ? BindingKind + ":" + BindingCategory + ":uar:" + BindingModGuid + ":" + BindingOriginalId + ":" + (BindingProperty ?? string.Empty)
                : BindingKind + ":" + BindingCategory + ":" + BindingId;
        } }

        public static DeformationEntry Create(DeformationKind kind) {
            bool thigh = kind == DeformationKind.Thigh;
            bool knee = kind == DeformationKind.Knee;
            bool lowerThigh = kind == DeformationKind.LowerThigh;
            bool upperArm = kind == DeformationKind.UpperArm;
            return new DeformationEntry {
                Id = System.Guid.NewGuid().ToString("N"), Kind = kind,
                Position = thigh ? 0.32f : lowerThigh ? 0.58f : knee ? 0f : upperArm ? 0.55f : 0.68f,
                Width = knee ? 0.14f : lowerThigh ? 0.12f : thigh ? 0.09f : 0.10f,
                Depth = thigh || knee || lowerThigh ? 0.006f : 0.003f,
                ProximalBulge = thigh || knee || lowerThigh ? 0.003f : 0.0015f,
                DistalBulge = thigh || knee || lowerThigh ? 0.003f : 0f
            };
        }

        public DeformationEntry Clone(bool newIdentity = false) {
            DeformationEntry copy = (DeformationEntry)MemberwiseClone();
            if (newIdentity) copy.Id = System.Guid.NewGuid().ToString("N");
            return copy;
        }
    }

    [Serializable]
    public sealed class SavedProfile {
        [XmlAttribute] public SaveScope Scope;
        [XmlAttribute] public string CharacterKey;
        [XmlAttribute] public string ItemKey;
        [XmlArray("Deformations"), XmlArrayItem("Deformation")]
        public List<DeformationEntry> Entries = new List<DeformationEntry>();
    }

    [Serializable, XmlRoot("ClothingCompression")]
    public sealed class CompressionLibrary {
        [XmlAttribute] public int Version = 2;
        [XmlArray("Profiles"), XmlArrayItem("Profile")]
        public List<SavedProfile> Profiles = new List<SavedProfile>();
    }

    internal sealed class CompressionLibraryStore {
        private readonly string path;
        private CompressionLibrary data;
        private Exception readFailure;

        internal CompressionLibraryStore() : this(System.IO.Path.Combine(Paths.ConfigPath, "MaxMixAlex.ClothingCompression.xml")) { }

        internal CompressionLibraryStore(string path) {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A library path is required.", "path");
            this.path = path;
        }

        internal string Path { get { return path; } }

        internal CompressionLibrary Data {
            get { if (data == null) Reload(); return data; }
        }

        internal bool Reload() {
            try {
                CompressionLibrary loaded = File.Exists(path) ? AtomicXmlFile.Read<CompressionLibrary>(path) : new CompressionLibrary();
                if (loaded == null) loaded = new CompressionLibrary();
                if (loaded.Profiles == null) loaded.Profiles = new List<SavedProfile>();
                loaded.Profiles.RemoveAll(profile => profile == null);
                foreach (SavedProfile profile in loaded.Profiles)
                    if (profile.Entries == null) profile.Entries = new List<DeformationEntry>();
                loaded.Version = 2;
                data = loaded;
                readFailure = null;
                return true;
            } catch (Exception ex) {
                ClothingCompressionPlugin.Log.LogError("Could not read " + path + ": " + ex);
                // Keep the last valid model, and do not let a later save replace the
                // unreadable file or overwrite its last good .bak with corrupt bytes.
                readFailure = ex;
                if (data == null) data = new CompressionLibrary();
                return false;
            }
        }

        internal int SaveEntries(string characterKey, IEnumerable<DeformationEntry> source, IEnumerable<string> previousIds = null) {
            CompressionLibrary library = Data;
            if (readFailure != null)
                throw new InvalidOperationException("Cannot save " + path + " after a failed read. Repair or restore the XML file and reload it before saving.", readFailure);
            if (source == null) throw new ArgumentNullException("source");
            List<DeformationEntry> bound = source.Where(x => x != null && x.BindingKind != BoundItemKind.None && !string.IsNullOrEmpty(x.ItemKey)).ToList();
            HashSet<string> savedIds = new HashSet<string>(bound.Select(x => x.Id).Where(x => !string.IsNullOrEmpty(x)));
            // A deleted or unbound entry is absent from `bound`; retain the caller's
            // previously loaded IDs so saving can remove it from its former profile.
            if (previousIds != null) savedIds.UnionWith(previousIds.Where(id => !string.IsNullOrEmpty(id)));
            foreach (SavedProfile profile in library.Profiles) {
                bool currentCharacterProfile = profile.Scope == SaveScope.CharacterAndItem && CharacterKeysMatch(profile.CharacterKey, characterKey);
                if (profile.Entries != null && (profile.Scope == SaveScope.ItemOnly || currentCharacterProfile))
                    profile.Entries.RemoveAll(x => x != null && savedIds.Contains(x.Id));
            }
            data.Profiles.RemoveAll(x => x.Entries == null || x.Entries.Count == 0);
            foreach (IGrouping<string, DeformationEntry> group in bound.GroupBy(x => x.SaveScope + "|" + x.ItemKey)) {
                DeformationEntry first = group.First();
                SaveScope scope = first.SaveScope;
                string itemKey = first.ItemKey;
                data.Profiles.RemoveAll(x => x.Scope == scope && x.ItemKey == itemKey &&
                    (scope == SaveScope.ItemOnly || CharacterKeysMatch(x.CharacterKey, characterKey)));
                data.Profiles.Add(new SavedProfile {
                    Scope = scope, CharacterKey = scope == SaveScope.CharacterAndItem ? characterKey : string.Empty,
                    ItemKey = itemKey, Entries = group.Select(x => x.Clone()).ToList()
                });
            }
            Write();
            return bound.Count;
        }

        internal List<DeformationEntry> LoadEntries(string characterKey, IEnumerable<ItemDescriptor> equippedItems) {
            List<DeformationEntry> result = new List<DeformationEntry>();
            if (equippedItems == null) return result;
            ItemDescriptor[] items = equippedItems.Where(x => x != null && !string.IsNullOrEmpty(x.Key)).ToArray();
            // A repeated item in two accessory slots still refers to one saved profile.
            // Apply all character overrides after global profiles, independent of slot order.
            foreach (SaveScope scope in new[] { SaveScope.ItemOnly, SaveScope.CharacterAndItem }) {
                var seenProfiles = new HashSet<SavedProfile>();
                foreach (ItemDescriptor item in items) {
                    bool remapped;
                    SavedProfile profile = FindProfile(scope, characterKey, item, out remapped);
                    if (profile == null || profile.Entries == null || !seenProfiles.Add(profile)) continue;
                    foreach (DeformationEntry entry in profile.Entries.Where(x => x != null)) {
                        DeformationEntry copy = entry.Clone();
                        if (string.IsNullOrEmpty(copy.Id)) entry.Id = copy.Id = Guid.NewGuid().ToString("N");
                        if (remapped) ItemCatalog.Bind(copy, item);
                        int old = result.FindIndex(x => x.Id == copy.Id);
                        if (old >= 0) result[old] = copy; else result.Add(copy);
                    }
                }
            }
            return result;
        }

        private SavedProfile FindProfile(SaveScope scope, string characterKey, ItemDescriptor item, out bool remapped) {
            IEnumerable<SavedProfile> candidates = Data.Profiles.Where(x => x.Scope == scope &&
                (scope == SaveScope.ItemOnly || CharacterKeysMatch(x.CharacterKey, characterKey)));
            SavedProfile exact = candidates.LastOrDefault(x => x.ItemKey == item.Key);
            if (exact != null) { remapped = false; return exact; }
            SavedProfile fallback = candidates.LastOrDefault(x => x.Entries != null && x.Entries.Any(entry => entry != null &&
                entry.BindingKind == item.Kind && entry.PreferredSlot == item.Slot &&
                string.Equals(entry.BindingName ?? string.Empty, item.Name ?? string.Empty, StringComparison.Ordinal)));
            remapped = fallback != null;
            if (fallback != null)
                ClothingCompressionPlugin.Log.LogInfo("Remapped saved binding " + fallback.ItemKey + " to " + item.Key + " at slot " + item.Slot + ".");
            return fallback;
        }

        internal List<DeformationEntry> LoadCoordinateEntries(string characterKey) {
            List<DeformationEntry> result = new List<DeformationEntry>();
            foreach (SavedProfile profile in Data.Profiles.Where(x => x.Scope == SaveScope.CharacterAndItem && CharacterKeysMatch(x.CharacterKey, characterKey))) {
                if (profile.Entries == null) continue;
                result.AddRange(profile.Entries.Where(x => x != null).Select(x => x.Clone()));
            }
            return result.GroupBy(x => string.IsNullOrEmpty(x.Id) ? Guid.NewGuid().ToString("N") : x.Id).Select(x => x.Last()).ToList();
        }

        private static bool CharacterKeysMatch(string saved, string current) {
            if (string.Equals(saved, current, StringComparison.Ordinal)) return true;
            if (string.IsNullOrEmpty(saved) || string.IsNullOrEmpty(current)) return false;
            int currentCoordinateMarker = current.LastIndexOf(":coord:", StringComparison.Ordinal);
            string currentIdentity = currentCoordinateMarker >= 0 ? current.Substring(0, currentCoordinateMarker) : current;
            if (saved.IndexOf(":coord:", StringComparison.Ordinal) >= 0) return false;
            if (string.Equals(saved, currentIdentity, StringComparison.Ordinal)) return true;
            int separator = currentIdentity.IndexOf(':');
            if (separator <= 0) return false;
            string sex = currentIdentity.Substring(0, separator);
            string name = currentIdentity.Substring(separator + 1);
            // Legacy format: sex:card-filename:character-name
            return saved.StartsWith(sex + ":", StringComparison.Ordinal) && saved.EndsWith(":" + name, StringComparison.Ordinal);
        }

        private void Write() {
            AtomicXmlFile.Write(path, data, path + ".bak");
        }
    }
}
