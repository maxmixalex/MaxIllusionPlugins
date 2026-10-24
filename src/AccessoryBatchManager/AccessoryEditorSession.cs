using System;
using System.Collections.Generic;

namespace AccessoryBatchManager {
    /// <summary>Owns the lifetime of queued editor commands and their reviewed targets.</summary>
    internal sealed class AccessoryEditorSession {
        internal int ModelRevision { get; private set; }
        internal int LifetimeRevision { get; private set; }
        internal AccessoryEditorTarget Target { get; private set; }
        private bool active;

        internal bool Refresh(AccessoryEditorTarget target) {
            if (active && (Target == null ? target == null : Target.Matches(target))) return false;
            Target = target;
            active = true;
            unchecked { ModelRevision++; }
            return true;
        }

        internal void Clear() {
            Target = null;
            active = false;
            unchecked { ModelRevision++; LifetimeRevision++; }
        }

        internal bool TryInvoke(int modelRevision, AccessoryEditorTarget target, AccessoryEditorTarget current, Action action) {
            if (!active || modelRevision != ModelRevision || !ReferenceEquals(Target, target) ||
                (target == null ? current != null : !target.Matches(current))) return false;
            action();
            return true;
        }

        internal bool TryConfirm(int lifetimeRevision, AccessoryEditorTarget target, AccessoryEditorTarget current, Action action) {
            // Opening/updating the dialog rebuilds its model. Confirmation keeps
            // the reviewed target, but never survives a closed editor session.
            if (!active || lifetimeRevision != LifetimeRevision || Target == null || target == null || !target.Matches(current)) return false;
            action();
            return true;
        }
    }

    internal sealed class AccessoryEditorTarget {
        private readonly object[] identities;
        private readonly int[] choices;
        private readonly int[][] selections;
        private readonly object[][] selectedItems;
        private readonly int[][] selectedItemTypes;
        private readonly int[][] selectedItemIds;

        internal AccessoryEditorTarget(ChaControl character, int page, int sourceIndex, int destinationIndex,
            bool sourceCard, bool sourceClothing, bool destinationClothing, bool reverseParent,
            ChaFileCoordinate source, ChaFileCoordinate destination,
            ICollection<int> batch, ICollection<int> sourceSelection, ICollection<int> destinationSelection, int commandOptions = 0) {
            ChaFileCoordinate working = character == null ? null : character.nowCoordinate;
            identities = new object[] {
                character, working,
                working == null || working.accessory == null ? null : working.accessory.parts,
                working == null || working.clothes == null ? null : working.clothes.parts,
                source,
                source == null || source.accessory == null ? null : source.accessory.parts,
                source == null || source.clothes == null ? null : source.clothes.parts,
                destination,
                destination == null || destination.accessory == null ? null : destination.accessory.parts,
                destination == null || destination.clothes == null ? null : destination.clothes.parts
            };
            choices = new[] { page, character == null || character.fileStatus == null ? -1 : character.fileStatus.coordinateType,
                sourceIndex, destinationIndex, sourceCard ? 1 : 0, sourceClothing ? 1 : 0, destinationClothing ? 1 : 0,
                reverseParent ? 1 : 0, commandOptions };
            selections = new[] { Copy(batch), Copy(sourceSelection), Copy(destinationSelection) };
            selectedItems = new[] {
                SelectedItems(selections[0], identities[2] as Array),
                SelectedItems(selections[1], identities[sourceClothing ? 6 : 5] as Array),
                SelectedItems(selections[2], identities[destinationClothing ? 9 : 8] as Array)
            };
            selectedItemTypes = new int[selectedItems.Length][];
            selectedItemIds = new int[selectedItems.Length][];
            for (int i = 0; i < selectedItems.Length; i++) {
                selectedItemTypes[i] = new int[selectedItems[i].Length];
                selectedItemIds[i] = new int[selectedItems[i].Length];
                for (int j = 0; j < selectedItems[i].Length; j++) {
                    ChaFileAccessory.PartsInfo accessory = selectedItems[i][j] as ChaFileAccessory.PartsInfo;
                    ChaFileClothes.PartsInfo clothing = selectedItems[i][j] as ChaFileClothes.PartsInfo;
                    selectedItemTypes[i][j] = accessory == null ? -1 : accessory.type;
                    selectedItemIds[i][j] = accessory != null ? accessory.id : clothing == null ? -1 : clothing.id;
                }
            }
        }

        private static object[] SelectedItems(int[] selection, Array items) {
            object[] result = new object[selection.Length];
            for (int i = 0; i < selection.Length; i++)
                if (items != null && selection[i] >= 0 && selection[i] < items.Length) result[i] = items.GetValue(selection[i]);
            return result;
        }

        private static int[] Copy(ICollection<int> source) {
            if (source == null || source.Count == 0) return new int[0];
            int[] result = new int[source.Count];
            source.CopyTo(result, 0);
            Array.Sort(result);
            return result;
        }

        internal bool Matches(AccessoryEditorTarget other) {
            if (other == null) return false;
            for (int i = 0; i < identities.Length; i++) if (!ReferenceEquals(identities[i], other.identities[i])) return false;
            for (int i = 0; i < choices.Length; i++) if (choices[i] != other.choices[i]) return false;
            for (int i = 0; i < selections.Length; i++) {
                if (selections[i].Length != other.selections[i].Length) return false;
                for (int j = 0; j < selections[i].Length; j++)
                    if (selections[i][j] != other.selections[i][j] || !ReferenceEquals(selectedItems[i][j], other.selectedItems[i][j]) ||
                        selectedItemTypes[i][j] != other.selectedItemTypes[i][j] || selectedItemIds[i][j] != other.selectedItemIds[i][j]) return false;
            }
            return true;
        }
    }
}
