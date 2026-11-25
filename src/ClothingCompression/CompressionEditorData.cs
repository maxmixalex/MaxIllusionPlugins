using System.Collections.Generic;

namespace ClothingCompression {
    /// <summary>
    /// The editor only needs a new item catalog when equipment changes, and new
    /// import copies when their source or the saved library changes. Keep those
    /// snapshots independent of numeric edits and widget rebuilds.
    /// </summary>
    internal sealed class CompressionEditorData {
        private CompressionController controller;
        private CompressionLibraryStore library;
        private int coordinate = -1, catalogRevision = -1, sourceCoordinate = -1, libraryRevision = -1;
        private string characterName;
        private int characterSex = -1;

        internal List<ItemDescriptor> BindingItems { get; private set; } = new List<ItemDescriptor>();
        internal List<DeformationEntry> ImportEntries { get; private set; } = new List<DeformationEntry>();
        internal int CatalogRefreshCount { get; private set; }
        internal int ImportRefreshCount { get; private set; }

        internal bool Refresh(CompressionController next, int source) {
            if (next == null || next.ChaControl == null) {
                if (controller == null && BindingItems.Count == 0 && ImportEntries.Count == 0) return false;
                Clear();
                return true;
            }
            bool changedController = !ReferenceEquals(controller, next);
            bool changedCoordinate = coordinate != next.CoordinateIndex;
            string name = next.ChaFileControl == null || next.ChaFileControl.parameter == null ? null : next.ChaFileControl.parameter.fullname;
            int sex = next.ChaControl.sex;
            bool changedIdentity = characterName != name || characterSex != sex;
            bool changedCatalog = changedController || changedCoordinate || catalogRevision != next.CatalogRevision;
            CompressionLibraryStore currentLibrary = ClothingCompressionPlugin.Library;
            bool changedImports = changedController || changedCoordinate || changedIdentity || sourceCoordinate != source ||
                !ReferenceEquals(library, currentLibrary) || currentLibrary != null && libraryRevision != currentLibrary.Revision;

            if (changedCatalog) {
                BindingItems = ItemCatalog.Get(next.ChaControl);
                CatalogRefreshCount++;
            }
            if (changedImports) {
                ImportEntries = currentLibrary == null || source < 0 || source == next.CoordinateIndex
                    ? new List<DeformationEntry>()
                    : currentLibrary.LoadCoordinateEntries(next.MakeCoordinateKey(source));
                ImportRefreshCount++;
            }
            controller = next;
            coordinate = next.CoordinateIndex;
            catalogRevision = next.CatalogRevision;
            characterName = name;
            characterSex = sex;
            sourceCoordinate = source;
            library = currentLibrary;
            // LoadCoordinateEntries can cause the first lazy library load.
            libraryRevision = currentLibrary == null ? -1 : currentLibrary.Revision;
            return changedCatalog || changedImports;
        }

        internal void Clear() {
            controller = null;
            library = null;
            coordinate = catalogRevision = sourceCoordinate = libraryRevision = -1;
            characterName = null;
            characterSex = -1;
            BindingItems = new List<ItemDescriptor>();
            ImportEntries = new List<DeformationEntry>();
        }
    }
}
