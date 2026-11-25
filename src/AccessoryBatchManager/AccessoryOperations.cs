using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChaCustom;
using UnityEngine;

namespace AccessoryBatchManager {
    internal static class AccessoryOperations {
        internal const int EmptyType = 120;
        internal const int VanillaSlotCount = 20;
        internal const int ClothingSlotCount = 9;
        internal static readonly ChaListDefine.CategoryNo[] ClothingCategories = {
            ChaListDefine.CategoryNo.co_top, ChaListDefine.CategoryNo.co_bot, ChaListDefine.CategoryNo.co_bra,
            ChaListDefine.CategoryNo.co_shorts, ChaListDefine.CategoryNo.co_gloves, ChaListDefine.CategoryNo.co_panst,
            ChaListDefine.CategoryNo.co_socks, ChaListDefine.CategoryNo.co_shoes, ChaListDefine.CategoryNo.co_shoes
        };

        private static readonly Type MoreAccessoriesType = Type.GetType("MoreAccessoriesKOI.MoreAccessories, MoreAccessories", false);
        private static readonly Type MoreAccessoriesWindowType = Type.GetType("MoreAccessoriesKOI.Accessories, MoreAccessories", false);
        private static readonly MethodInfo ArraySyncMethod = MoreAccessoriesType == null ? null : MoreAccessoriesType.GetMethod("ArraySync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ChaControl) }, null);
        private static readonly FieldInfo ShowSlotField = MoreAccessoriesWindowType == null ? null : MoreAccessoriesWindowType.GetField("ShowSlot", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo NoShakeProperty = typeof(ChaFileAccessory.PartsInfo).GetProperty("noShake", BindingFlags.Public | BindingFlags.Instance);

        internal static bool MoreAccessoriesAvailable { get { return ArraySyncMethod != null; } }

        internal static ChaControl GetCharacter() {
            if (!KKAPI.Maker.MakerAPI.InsideMaker) return null;
            return KKAPI.Maker.MakerAPI.GetCharacterControl();
        }

        internal static ChaFileAccessory.PartsInfo[] CurrentParts(ChaControl character) {
            if (character == null || character.nowCoordinate == null || character.nowCoordinate.accessory == null)
                return new ChaFileAccessory.PartsInfo[0];
            return character.nowCoordinate.accessory.parts ?? new ChaFileAccessory.PartsInfo[0];
        }

        internal static ChaFileCoordinate GetCoordinate(ChaControl character, int coordinate) {
            // ChaControl.ChangeCoordinateType deserializes a separate working copy.
            // Edits in Maker can therefore be newer than chaFile.coordinate[current].
            return coordinate == character.fileStatus.coordinateType
                ? character.nowCoordinate : character.chaFile.coordinate[coordinate];
        }

        internal static int EffectiveSlotCount(ChaFileAccessory.PartsInfo[] parts) {
            if (parts == null || parts.Length == 0) return 0;
            int last = Array.FindLastIndex(parts, delegate(ChaFileAccessory.PartsInfo part) { return !IsEmpty(part); });
            return Math.Min(parts.Length, Math.Max(VanillaSlotCount, last + 1));
        }

        internal static int FilledCount(ChaFileAccessory.PartsInfo[] parts) {
            return parts == null ? 0 : parts.Count(delegate(ChaFileAccessory.PartsInfo part) { return !IsEmpty(part); });
        }

        internal static string GetName(ChaControl character, ChaFileAccessory.PartsInfo part) {
            if (part == null || part.type == EmptyType) return "空";
            try {
                ListInfoBase info = character.lstCtrl.GetListInfo((ChaListDefine.CategoryNo)part.type, part.id);
                return info == null || string.IsNullOrEmpty(info.Name) ? "未知饰品 (" + part.type + ":" + part.id + ")" : AccessoryNameLocalizer.Localize(info.Name);
            }
            catch {
                return "未知饰品 (" + part.type + ":" + part.id + ")";
            }
        }

        internal static string GetClothingName(ChaControl character, ChaFileClothes.PartsInfo part, int slot) {
            if (part == null || part.id == 0) return "空";
            try {
                ListInfoBase info = character.lstCtrl.GetListInfo(ClothingCategories[slot], part.id);
                return info == null || string.IsNullOrEmpty(info.Name) ? "未知服装 (" + part.id + ")" : AccessoryNameLocalizer.Localize(info.Name);
            }
            catch { return "未知服装 (" + part.id + ")"; }
        }

        internal static ChaFileAccessory.PartsInfo[] ConvertClothingToAccessories(ChaControl character, ChaFileClothes.PartsInfo[] clothing) {
            var result = new ChaFileAccessory.PartsInfo[ClothingSlotCount];
            for (int i = 0; i < result.Length; i++) result[i] = ClothingToAccessory(character, clothing[i], i);
            return result;
        }

        internal static int RemoveSelected(ChaControl character, ICollection<int> selected) {
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            int removed = 0;
            foreach (int index in selected) {
                if (index < 0 || index >= parts.Length || IsEmpty(parts[index])) continue;
                parts[index] = NewEmpty();
                removed++;
            }
            if (removed > 0) {
                CommitCurrent(character, parts);
                foreach (int index in selected) MakerEventBridge.AccessoryKindChanged(index);
            }
            return removed;
        }

        internal static int Compact(ChaControl character) {
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            var filled = parts
                .Select(delegate(ChaFileAccessory.PartsInfo part, int index) { return new IndexedPart(index, part); })
                .Where(delegate(IndexedPart item) { return !IsEmpty(item.Part); })
                .ToList();
            int moved = 0;
            for (int i = 0; i < filled.Count; i++) {
                if (filled[i].Index != i) moved++;
            }
            var compacted = new ChaFileAccessory.PartsInfo[parts.Length];
            for (int i = 0; i < compacted.Length; i++)
                compacted[i] = i < filled.Count ? filled[i].Part : NewEmpty();
            CommitCurrent(character, compacted);
            for (int i = 0; i < filled.Count; i++) {
                if (filled[i].Index != i) MakerEventBridge.AccessoryTransferred(filled[i].Index, i);
            }
            for (int i = filled.Count; i < parts.Length; i++) MakerEventBridge.AccessoryKindChanged(i);
            return moved;
        }

        internal static void AddAtFront(ChaControl character, int count) {
            if (count <= 0) return;
            ChaFileAccessory.PartsInfo[] oldParts = CurrentParts(character);
            int oldVisibleCount = EffectiveSlotCount(oldParts);
            int newVisibleCount = oldVisibleCount + count;
            int newLength = Math.Max(oldParts.Length, newVisibleCount);
            var result = new ChaFileAccessory.PartsInfo[newLength];
            for (int i = 0; i < result.Length; i++) result[i] = NewEmpty();
            int copyCount = Math.Min(oldParts.Length, result.Length - count);
            Array.Copy(oldParts, 0, result, count, copyCount);
            CommitCurrent(character, result);
            SyncMoreAccessories(character, newVisibleCount);
            for (int i = copyCount - 1; i >= 0; i--) {
                if (!IsEmpty(oldParts[i])) MakerEventBridge.AccessoryTransferred(i, i + count);
            }
            for (int i = 0; i < count; i++) MakerEventBridge.AccessoryKindChanged(i);
        }

        internal static int CopyOrMoveSlots(ChaControl character, IList<int> sources, IList<int> destinations, bool reverseParent, bool move) {
            List<SlotPair> pairs = BuildPairs(sources, destinations, move);
            if (pairs.Count == 0) return 0;
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            if (!ValidPairs(pairs, parts.Length, parts.Length)) return 0;
            var copied = new List<ChaFileAccessory.PartsInfo>(pairs.Count);
            foreach (SlotPair pair in pairs) {
                ChaFileAccessory.PartsInfo value = Clone(parts[pair.Source]);
                if (reverseParent && value.type != EmptyType) {
                    string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                    if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
                }
                copied.Add(value);
            }
            for (int i = 0; i < pairs.Count; i++) parts[pairs[i].Destination] = copied[i];
            if (move) {
                var destinationSet = new HashSet<int>(pairs.Select(delegate(SlotPair pair) { return pair.Destination; }));
                foreach (SlotPair pair in pairs) {
                    if (!destinationSet.Contains(pair.Source)) parts[pair.Source] = NewEmpty();
                }
            }
            CommitCurrent(character, parts);
            foreach (SlotPair pair in pairs) MakerEventBridge.AccessoryTransferred(pair.Source, pair.Destination);
            if (move) {
                var destinationSet = new HashSet<int>(pairs.Select(delegate(SlotPair pair) { return pair.Destination; }));
                foreach (SlotPair pair in pairs) {
                    if (!destinationSet.Contains(pair.Source)) MakerEventBridge.AccessoryKindChanged(pair.Source);
                }
            }
            return pairs.Count;
        }

        internal static int CopyOrMoveBetweenCoordinates(ChaControl character, int sourceCoordinate, int destinationCoordinate, IList<int> sources, IList<int> destinations, bool reverseParent, bool move) {
            if (!ValidCoordinate(character, sourceCoordinate) || !ValidCoordinate(character, destinationCoordinate)) return 0;
            if (sourceCoordinate == destinationCoordinate && sourceCoordinate == character.fileStatus.coordinateType)
                return CopyOrMoveSlots(character, sources, destinations, reverseParent, move);

            List<SlotPair> pairs = BuildPairs(sources, destinations, move, sourceCoordinate != destinationCoordinate);
            if (pairs.Count == 0) return 0;
            ChaFileAccessory.PartsInfo[] source = GetCoordinate(character, sourceCoordinate).accessory.parts;
            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            if (source == null || destination == null || !ValidPairs(pairs, source.Length, int.MaxValue)) return 0;
            int required = Math.Max(destination.Length, pairs.Max(delegate(SlotPair pair) { return pair.Destination; }) + 1);
            if (required > destination.Length) {
                destination = ResizeWithEmpty(destination, required);
                if (sourceCoordinate == destinationCoordinate) source = destination;
            }

            var copied = new List<ChaFileAccessory.PartsInfo>(pairs.Count);
            foreach (SlotPair pair in pairs) {
                ChaFileAccessory.PartsInfo value = Clone(source[pair.Source]);
                if (reverseParent && value.type != EmptyType) {
                    string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                    if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
                }
                copied.Add(value);
            }
            for (int i = 0; i < pairs.Count; i++) destination[pairs[i].Destination] = copied[i];
            if (move) {
                var retained = new HashSet<int>(pairs.Select(pair => pair.Destination));
                foreach (SlotPair pair in pairs)
                    if (sourceCoordinate != destinationCoordinate || !retained.Contains(pair.Source)) source[pair.Source] = NewEmpty();
            }

            character.chaFile.coordinate[sourceCoordinate].accessory.parts = source;
            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            int current = character.fileStatus.coordinateType;
            if (destinationCoordinate == current) character.nowCoordinate.accessory.parts = destination;
            else if (sourceCoordinate == current) character.nowCoordinate.accessory.parts = source;
            MarkMakerDirty(character);

            int[] sameIndexes = pairs.Where(delegate(SlotPair pair) { return pair.Source == pair.Destination; }).Select(delegate(SlotPair pair) { return pair.Source; }).ToArray();
            if (sourceCoordinate != destinationCoordinate && sameIndexes.Length > 0)
                MakerEventBridge.AccessoriesCopied(sameIndexes, sourceCoordinate, destinationCoordinate);
            return pairs.Count;
        }

        internal static int CopyOrMoveClothing(ChaControl character, ChaFileClothes.PartsInfo[] source, int sourceCoordinate,
            int destinationCoordinate, IList<int> sources, IList<int> destinations, bool move) {
            List<SlotPair> pairs = BuildClothingPairs(sources, destinations, move);
            if (pairs.Any(pair => !IsAllowedClothingPair(pair.Source, pair.Destination))) return 0;
            if (!ValidCoordinate(character, destinationCoordinate) || source == null || pairs.Count == 0 ||
                (move && !ValidCoordinate(character, sourceCoordinate))) return 0;
            ChaFileClothes.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).clothes.parts;
            if (destination == null || !ValidPairs(pairs, source.Length, destination.Length)) return 0;
            var copied = pairs.Select(pair => CloneClothing(source[pair.Source])).ToArray();
            for (int i = 0; i < pairs.Count; i++) destination[pairs[i].Destination] = copied[i];
            if (move) {
                var destinationSet = new HashSet<int>(pairs.Where(pair => sourceCoordinate == destinationCoordinate)
                    .Select(pair => pair.Destination));
                foreach (SlotPair pair in pairs)
                    if (!destinationSet.Contains(pair.Source)) source[pair.Source] = new ChaFileClothes.PartsInfo();
            }
            if (move) character.chaFile.coordinate[sourceCoordinate].clothes.parts = source;
            character.chaFile.coordinate[destinationCoordinate].clothes.parts = destination;
            if (destinationCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.clothes.parts = destination;
            else if (move && sourceCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.clothes.parts = source;
            MarkMakerDirty(character);
            return pairs.Count;
        }

        internal static bool IsValidClothingMapping(IList<int> sources, IList<int> destinations, bool move,
            int sourceCoordinate, int destinationCoordinate) {
            List<SlotPair> pairs = BuildClothingPairs(sources, destinations, move);
            if (pairs.Count == 0 || pairs.Any(pair => !IsAllowedClothingPair(pair.Source, pair.Destination))) return false;
            return sourceCoordinate != destinationCoordinate || pairs.Any(pair => pair.Source != pair.Destination);
        }

        private static List<SlotPair> BuildClothingPairs(IList<int> sources, IList<int> destinations, bool move) {
            var pairs = new List<SlotPair>();
            if (sources == null || destinations == null || sources.Count == 0 || destinations.Count == 0) return pairs;
            if (sources.Count == destinations.Count) {
                for (int i = 0; i < sources.Count; i++) pairs.Add(new SlotPair(sources[i], destinations[i]));
            }
            else if (sources.Count == 1 && IsShoeSlot(sources[0]) && destinations.All(IsShoeSlot)) {
                for (int i = 0; i < destinations.Count; i++) pairs.Add(new SlotPair(sources[0], destinations[i]));
            }
            return pairs;
        }

        private static bool IsAllowedClothingPair(int source, int destination) {
            if (source < 0 || source >= ClothingSlotCount || destination < 0 || destination >= ClothingSlotCount) return false;
            return source == destination || IsShoeSlot(source) && IsShoeSlot(destination);
        }

        private static bool IsShoeSlot(int slot) { return slot == 7 || slot == 8; }

        internal static int CopyOrMoveClothingToAccessories(ChaControl character, ChaFileClothes.PartsInfo[] source,
            int sourceCoordinate, int destinationCoordinate, IList<int> sources, IList<int> destinations, bool reverseParent, bool move) {
            // Clothing and accessory slots are separate collections, even in one coordinate.
            List<SlotPair> pairs = BuildPairs(sources, destinations, move, true)
                .Where(pair => pair.Source >= 0 && pair.Source < ClothingSlotCount && pair.Destination >= 0)
                .ToList();
            if (!ValidCoordinate(character, destinationCoordinate) || source == null || pairs.Count == 0 ||
                (move && !ValidCoordinate(character, sourceCoordinate))) return 0;
            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            if (destination == null || !ValidPairs(pairs, source.Length, int.MaxValue)) return 0;
            int required = Math.Max(destination.Length, pairs.Max(pair => pair.Destination) + 1);
            if (required > destination.Length) destination = ResizeWithEmpty(destination, required);
            for (int i = 0; i < pairs.Count; i++) {
                ChaFileAccessory.PartsInfo value = ClothingToAccessory(character, source[pairs[i].Source], pairs[i].Source);
                if (reverseParent && value.type != EmptyType) {
                    string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                    if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
                }
                destination[pairs[i].Destination] = value;
            }
            if (move) foreach (SlotPair pair in pairs) source[pair.Source] = new ChaFileClothes.PartsInfo();
            if (move) character.chaFile.coordinate[sourceCoordinate].clothes.parts = source;
            if (move && sourceCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.clothes.parts = source;
            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            if (destinationCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.accessory.parts = destination;
            MarkMakerDirty(character);
            if (destinationCoordinate == character.fileStatus.coordinateType)
                SyncMoreAccessories(character, EffectiveSlotCount(destination));
            return pairs.Count;
        }

        internal static int InsertSlots(ChaControl character, ChaFileAccessory.PartsInfo[] source, int destinationCoordinate,
            IList<int> sources, bool atFront, bool reverseParent, out int insertIndex, out List<int> insertedSlots) {
            insertIndex = 0;
            insertedSlots = new List<int>();
            if (character == null || source == null || sources == null || destinationCoordinate < 0 ||
                destinationCoordinate >= character.chaFile.coordinate.Length) return 0;
            List<int> validSources = sources.Where(index => index >= 0 && index < source.Length).Distinct().OrderBy(index => index).ToList();
            if (validSources.Count == 0) return 0;

            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            int used = UsedSlotCount(destination);
            insertIndex = atFront ? 0 : used;
            int required = Math.Max(VanillaSlotCount, used + validSources.Count);
            int length = Math.Max(destination.Length, required);
            var result = new ChaFileAccessory.PartsInfo[length];
            for (int i = 0; i < result.Length; i++) result[i] = NewEmpty();

            int shift = atFront ? validSources.Count : 0;
            for (int i = 0; i < used && i + shift < result.Length; i++) result[i + shift] = destination[i];
            for (int i = 0; i < validSources.Count; i++) {
                ChaFileAccessory.PartsInfo value = Clone(source[validSources[i]]);
                if (reverseParent && value.type != EmptyType) {
                    string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                    if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
                }
                int destinationSlot = insertIndex + i;
                result[destinationSlot] = value;
                insertedSlots.Add(destinationSlot);
            }

            character.chaFile.coordinate[destinationCoordinate].accessory.parts = result;
            if (destinationCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.accessory.parts = result;
            MarkMakerDirty(character);
            if (destinationCoordinate == character.fileStatus.coordinateType)
                SyncMoreAccessories(character, Math.Max(VanillaSlotCount, used + validSources.Count));
            return validSources.Count;
        }

        internal static int CopyFromParts(ChaControl character, ChaFileAccessory.PartsInfo[] source, int destinationCoordinate, IList<int> sources, IList<int> destinations, bool reverseParent) {
            if (character == null || source == null || destinationCoordinate < 0 || destinationCoordinate >= character.chaFile.coordinate.Length) return 0;
            List<SlotPair> pairs = BuildPairs(sources, destinations, false, true)
                .Where(delegate(SlotPair pair) { return pair.Source >= 0 && pair.Source < source.Length && pair.Destination >= 0; })
                .ToList();
            if (pairs.Count == 0) return 0;

            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            int required = Math.Max(destination.Length, pairs.Max(delegate(SlotPair pair) { return pair.Destination; }) + 1);
            if (required > destination.Length) destination = ResizeWithEmpty(destination, required);
            foreach (SlotPair pair in pairs) {
                ChaFileAccessory.PartsInfo value = Clone(source[pair.Source]);
                if (reverseParent && value.type != EmptyType) {
                    string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                    if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
                }
                destination[pair.Destination] = value;
            }

            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            if (destinationCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.accessory.parts = destination;
            MarkMakerDirty(character);
            // KindChanged has no coordinate parameter; raising it for an inactive
            // coordinate makes integrations clear the visible coordinate's metadata.
            if (destinationCoordinate == character.fileStatus.coordinateType)
                foreach (int destinationSlot in pairs.Select(delegate(SlotPair pair) { return pair.Destination; }).Distinct())
                    MakerEventBridge.AccessoryKindChanged(destinationSlot);
            return pairs.Count;
        }

        internal static int CopyAdjustments(ChaControl character, IList<int> sources, IList<int> destinations, int adjustment, MirrorMode mirror) {
            List<SlotPair> pairs = BuildPairs(sources, destinations, false);
            if (pairs.Count == 0 || adjustment < 0 || adjustment > 1) return 0;
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            if (!ValidPairs(pairs, parts.Length, parts.Length) || pairs.Any(pair => parts[pair.Source] == null || parts[pair.Destination] == null)) return 0;
            var values = new Vector3[pairs.Count, 3];
            for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++) {
                for (int component = 0; component < 3; component++) {
                    Vector3 value = parts[pairs[pairIndex].Source].addMove[adjustment, component];
                    if (component == 1 && mirror == MirrorMode.LeftRight) value.y = WrapAngle(value.y + 180f);
                    if (component == 1 && mirror == MirrorMode.UpDown) value.x = WrapAngle(value.x + 180f);
                    values[pairIndex, component] = value;
                }
            }
            for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++)
                for (int component = 0; component < 3; component++)
                    parts[pairs[pairIndex].Destination].addMove[adjustment, component] = values[pairIndex, component];
            int coordinate = character.fileStatus.coordinateType;
            character.nowCoordinate.accessory.parts = parts;
            character.chaFile.coordinate[coordinate].accessory.parts = parts;
            MarkMakerDirty(character);
            return pairs.Count;
        }

        internal static int CopyAdjustmentsBetweenCoordinates(ChaControl character, int sourceCoordinate, int destinationCoordinate, IList<int> sources, IList<int> destinations, int adjustment, MirrorMode mirror) {
            if (!ValidCoordinate(character, sourceCoordinate) || !ValidCoordinate(character, destinationCoordinate) || adjustment < 0 || adjustment > 1) return 0;
            if (sourceCoordinate == destinationCoordinate && sourceCoordinate == character.fileStatus.coordinateType)
                return CopyAdjustments(character, sources, destinations, adjustment, mirror);

            List<SlotPair> pairs = BuildPairs(sources, destinations, false, sourceCoordinate != destinationCoordinate);
            if (pairs.Count == 0) return 0;
            ChaFileAccessory.PartsInfo[] source = GetCoordinate(character, sourceCoordinate).accessory.parts;
            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            if (source == null || destination == null || !ValidPairs(pairs, source.Length, int.MaxValue) || pairs.Any(pair => source[pair.Source] == null)) return 0;
            int required = Math.Max(destination.Length, pairs.Max(delegate(SlotPair pair) { return pair.Destination; }) + 1);
            if (required > destination.Length) destination = ResizeWithEmpty(destination, required);
            // Snapshot before writing: the selected coordinate need not be the visible one,
            // and overlapping copies must not read values written by an earlier pair.
            var values = new Vector3[pairs.Count, 3];
            for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++) {
                for (int component = 0; component < 3; component++) {
                    Vector3 value = source[pairs[pairIndex].Source].addMove[adjustment, component];
                    if (component == 1 && mirror == MirrorMode.LeftRight) value.y = WrapAngle(value.y + 180f);
                    if (component == 1 && mirror == MirrorMode.UpDown) value.x = WrapAngle(value.x + 180f);
                    values[pairIndex, component] = value;
                }
            }
            for (int pairIndex = 0; pairIndex < pairs.Count; pairIndex++) {
                int slot = pairs[pairIndex].Destination;
                if (destination[slot] == null) destination[slot] = NewEmpty();
                for (int component = 0; component < 3; component++)
                    destination[slot].addMove[adjustment, component] = values[pairIndex, component];
            }
            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            int current = character.fileStatus.coordinateType;
            if (destinationCoordinate == current) character.nowCoordinate.accessory.parts = destination;
            else if (sourceCoordinate == current) character.nowCoordinate.accessory.parts = source;
            MarkMakerDirty(character);
            return pairs.Count;
        }

        internal static int CopyAdjustmentsFromParts(ChaControl character, ChaFileAccessory.PartsInfo[] source, int destinationCoordinate, IList<int> sources, IList<int> destinations, int adjustment, MirrorMode mirror) {
            if (!ValidCoordinate(character, destinationCoordinate) || source == null || adjustment < 0 || adjustment > 1) return 0;
            List<SlotPair> pairs = BuildPairs(sources, destinations, false, true);
            if (pairs.Count == 0 || !ValidPairs(pairs, source.Length, int.MaxValue)) return 0;

            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            if (destination == null) return 0;
            // The source is an independent coordinate card. Its MessagePack data may
            // contain null entries (displayed as selectable empty slots) or malformed
            // adjustment arrays. Validate the entire mapping before changing any slot.
            foreach (SlotPair pair in pairs) {
                if (!HasAdjustment(source[pair.Source], adjustment)) return 0;
                if (pair.Destination < destination.Length && destination[pair.Destination] != null &&
                    !HasAdjustment(destination[pair.Destination], adjustment)) return 0;
            }
            int required = Math.Max(destination.Length, pairs.Max(delegate(SlotPair pair) { return pair.Destination; }) + 1);
            if (required > destination.Length) destination = ResizeWithEmpty(destination, required);
            foreach (SlotPair pair in pairs) {
                if (destination[pair.Destination] == null) destination[pair.Destination] = NewEmpty();
                for (int component = 0; component < 3; component++) {
                    Vector3 value = source[pair.Source].addMove[adjustment, component];
                    if (component == 1 && mirror == MirrorMode.LeftRight) value.y = WrapAngle(value.y + 180f);
                    if (component == 1 && mirror == MirrorMode.UpDown) value.x = WrapAngle(value.x + 180f);
                    destination[pair.Destination].addMove[adjustment, component] = value;
                }
            }

            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            if (destinationCoordinate == character.fileStatus.coordinateType) character.nowCoordinate.accessory.parts = destination;
            MarkMakerDirty(character);
            return pairs.Count;
        }

        private static bool HasAdjustment(ChaFileAccessory.PartsInfo part, int adjustment) {
            return part != null && part.addMove != null && part.addMove.GetLength(0) > adjustment && part.addMove.GetLength(1) >= 3;
        }

        internal static int CopyAcrossCoordinates(ChaControl character, int sourceCoordinate, int destinationCoordinate, ICollection<int> selected) {
            if (character == null || character.chaFile == null || character.chaFile.coordinate == null) return 0;
            if (sourceCoordinate < 0 || destinationCoordinate < 0 || sourceCoordinate >= character.chaFile.coordinate.Length || destinationCoordinate >= character.chaFile.coordinate.Length) return 0;
            if (sourceCoordinate == destinationCoordinate) return 0;

            ChaFileAccessory.PartsInfo[] source = GetCoordinate(character, sourceCoordinate).accessory.parts;
            ChaFileAccessory.PartsInfo[] destination = GetCoordinate(character, destinationCoordinate).accessory.parts;
            int required = selected.Count == 0 ? destination.Length : Math.Max(destination.Length, selected.Max() + 1);
            if (required > destination.Length) destination = ResizeWithEmpty(destination, required);

            int copied = 0;
            foreach (int index in selected) {
                if (index < 0 || index >= source.Length) continue;
                destination[index] = Clone(source[index]);
                copied++;
            }
            character.chaFile.coordinate[destinationCoordinate].accessory.parts = destination;
            if (destinationCoordinate == character.fileStatus.coordinateType)
                character.nowCoordinate.accessory.parts = destination;
            MarkMakerDirty(character);
            MakerEventBridge.AccessoriesCopied(selected.Where(delegate(int index) { return index >= 0 && index < source.Length; }).ToArray(), sourceCoordinate, destinationCoordinate);
            return copied;
        }

        internal static void CopyOrMoveSlot(ChaControl character, int source, int destination, bool reverseParent, bool move) {
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            if (source < 0 || destination < 0 || source >= parts.Length || destination >= parts.Length || source == destination) return;
            ChaFileAccessory.PartsInfo value = Clone(parts[source]);
            if (reverseParent && value.type != EmptyType) {
                string reverse = ChaAccessoryDefine.GetReverseParent(value.parentKey);
                if (!string.IsNullOrEmpty(reverse)) value.parentKey = reverse;
            }
            parts[destination] = value;
            if (move) parts[source] = NewEmpty();
            CommitCurrent(character, parts);
            MakerEventBridge.AccessoryTransferred(source, destination);
            if (move) MakerEventBridge.AccessoryKindChanged(source);
        }

        internal static void CopyAdjustment(ChaControl character, int source, int destination, int adjustment, MirrorMode mirror) {
            ChaFileAccessory.PartsInfo[] parts = CurrentParts(character);
            if (source < 0 || destination < 0 || source >= parts.Length || destination >= parts.Length || source == destination) return;
            for (int component = 0; component < 3; component++) {
                Vector3 value = parts[source].addMove[adjustment, component];
                if (component == 1 && mirror == MirrorMode.LeftRight) value.y = WrapAngle(value.y + 180f);
                if (component == 1 && mirror == MirrorMode.UpDown) value.x = WrapAngle(value.x + 180f);
                parts[destination].addMove[adjustment, component] = value;
            }
            int coordinate = character.fileStatus.coordinateType;
            character.nowCoordinate.accessory.parts = parts;
            character.chaFile.coordinate[coordinate].accessory.parts = parts;
            MarkMakerDirty(character);
        }

        private static void CommitCurrent(ChaControl character, ChaFileAccessory.PartsInfo[] parts) {
            int coordinate = character.fileStatus.coordinateType;
            character.nowCoordinate.accessory.parts = parts;
            character.chaFile.coordinate[coordinate].accessory.parts = parts;
            MarkMakerDirty(character);
        }

        private static bool ValidCoordinate(ChaControl character, int coordinate) {
            return character != null && character.chaFile != null && character.chaFile.coordinate != null &&
                character.fileStatus != null && character.nowCoordinate != null && coordinate >= 0 &&
                coordinate < character.chaFile.coordinate.Length && character.chaFile.coordinate[coordinate] != null;
        }

        private static bool ValidPairs(IList<SlotPair> pairs, int sourceLength, int destinationLength) {
            foreach (SlotPair pair in pairs)
                if (pair.Source < 0 || pair.Source >= sourceLength || pair.Destination < 0 || pair.Destination >= destinationLength) return false;
            return true;
        }

        private static int UsedSlotCount(ChaFileAccessory.PartsInfo[] parts) {
            if (parts == null) return 0;
            int last = Array.FindLastIndex(parts, delegate(ChaFileAccessory.PartsInfo part) { return !IsEmpty(part); });
            return last + 1;
        }

        internal static void RefreshCharacter(ChaControl character) {
            if (character == null) return;
            character.StartCoroutine(RefreshCharacterRoutine(character));
        }

        private static IEnumerator RefreshCharacterRoutine(ChaControl character) {
            var rendererStates = new Dictionary<Renderer, bool>();
            object materialEditorState = null;
            var routines = new Stack<IEnumerator>();
            try {
                materialEditorState = MaterialEditorBridge.CaptureState(character);
                HideCharacterRenderers(character, rendererStates);
                NormalizeCoordinateLengths(character);
                SyncMoreAccessories(character, EffectiveSlotCount(CurrentParts(character)));
                character.AssignCoordinate((ChaFileDefine.CoordinateType)character.fileStatus.coordinateType);
                routines.Push(character.ReloadAsync(false, false, false, false, false));
                while (routines.Count > 0) {
                    IEnumerator routine = routines.Peek();
                    bool more = routine.MoveNext();
                    if (!more) {
                        routines.Pop();
                        IDisposable completed = routine as IDisposable;
                        if (completed != null) completed.Dispose();
                        continue;
                    }
                    var nested = routine.Current as IEnumerator;
                    if (nested != null) {
                        routines.Push(nested);
                        continue;
                    }
                    HideCharacterRenderers(character, rendererStates);
                    yield return routine.Current;
                }
                HideCharacterRenderers(character, rendererStates);
            }
            finally {
                // A failing integration or a cancelled reload must never leave the
                // character invisible or drop the captured MaterialEditor state.
                try {
                    while (routines.Count > 0) {
                        IDisposable pending = routines.Pop() as IDisposable;
                        if (pending != null) pending.Dispose();
                    }
                }
                finally {
                    RestoreCharacterRenderers(rendererStates);
                    MaterialEditorBridge.RestoreState(materialEditorState);
                }
            }
            MaterialEditorBridge.ReapplyAllImmediately(character);
            MarkMakerDirty(character);
        }

        private static void HideCharacterRenderers(ChaControl character, IDictionary<Renderer, bool> states) {
            if (character == null) return;
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true)) {
                if (renderer == null) continue;
                if (!states.ContainsKey(renderer)) states[renderer] = renderer.enabled;
                else if (renderer.enabled) states[renderer] = true;
                renderer.enabled = false;
            }
        }

        private static void RestoreCharacterRenderers(IDictionary<Renderer, bool> states) {
            foreach (KeyValuePair<Renderer, bool> item in states) {
                if (item.Key != null) item.Key.enabled = item.Value;
            }
        }

        private static void MarkMakerDirty(ChaControl character) {
            if (CustomBase.Instance != null) {
                CustomBase.Instance.updateCustomUI = true;
            }
        }

        private static void SyncMoreAccessories(ChaControl character, int visibleCount) {
            if (character == null || ArraySyncMethod == null) return;
            try {
                if (ShowSlotField != null) ShowSlotField.SetValue(null, Math.Max(VanillaSlotCount, visibleCount));
                ArraySyncMethod.Invoke(null, new object[] { character });
            }
            catch { /* Keep the data operation valid if an optional MoreAccessories Maker window is not ready yet. */ }
        }

        private static void NormalizeCoordinateLengths(ChaControl character) {
            int length = VanillaSlotCount;
            int current = character.fileStatus.coordinateType;
            // Retain the current working copy before normalizing the saved coordinates.
            character.chaFile.coordinate[current].accessory.parts = CurrentParts(character);
            foreach (ChaFileCoordinate coordinate in character.chaFile.coordinate)
                if (coordinate != null && coordinate.accessory != null && coordinate.accessory.parts != null) length = Math.Max(length, coordinate.accessory.parts.Length);
            if (character.fileStatus.showAccessory != null) length = Math.Max(length, character.fileStatus.showAccessory.Length);
            if (character.objAccessory != null) length = Math.Max(length, character.objAccessory.Length);
            if (character.cusAcsCmp != null) length = Math.Max(length, character.cusAcsCmp.Length);
            if (character.infoAccessory != null) length = Math.Max(length, character.infoAccessory.Length);
            if (character.hideHairAcs != null) length = Math.Max(length, character.hideHairAcs.Length);
            foreach (ChaFileCoordinate coordinate in character.chaFile.coordinate) {
                if (coordinate == null || coordinate.accessory == null) continue;
                ChaFileAccessory.PartsInfo[] parts = coordinate.accessory.parts ?? new ChaFileAccessory.PartsInfo[0];
                if (parts.Length != length) coordinate.accessory.parts = ResizeWithEmpty(parts, length);
            }
            character.nowCoordinate.accessory.parts = character.chaFile.coordinate[current].accessory.parts;
        }

        private static List<SlotPair> BuildPairs(IList<int> sources, IList<int> destinations, bool move, bool allowSameIndex = false) {
            var pairs = new List<SlotPair>();
            if (sources == null || destinations == null || sources.Count == 0 || destinations.Count == 0) return pairs;
            if (sources.Count == destinations.Count) {
                for (int i = 0; i < sources.Count; i++) {
                    if (allowSameIndex || sources[i] != destinations[i]) pairs.Add(new SlotPair(sources[i], destinations[i]));
                }
            }
            else if (!move && sources.Count == 1) {
                for (int i = 0; i < destinations.Count; i++) {
                    if (allowSameIndex || sources[0] != destinations[i]) pairs.Add(new SlotPair(sources[0], destinations[i]));
                }
            }
            return pairs;
        }

        private static ChaFileAccessory.PartsInfo[] ResizeWithEmpty(ChaFileAccessory.PartsInfo[] source, int length) {
            var result = new ChaFileAccessory.PartsInfo[length];
            Array.Copy(source, result, source.Length);
            for (int i = source.Length; i < result.Length; i++) result[i] = NewEmpty();
            return result;
        }

        private static ChaFileAccessory.PartsInfo Clone(ChaFileAccessory.PartsInfo source) {
            if (source == null) return NewEmpty();
            var result = new ChaFileAccessory.PartsInfo {
                type = source.type,
                id = source.id,
                parentKey = source.parentKey,
                hideCategory = source.hideCategory,
                partsOfHead = source.partsOfHead
            };
            if (NoShakeProperty != null)
                NoShakeProperty.SetValue(result, NoShakeProperty.GetValue(source, null), null);
            for (int adjustment = 0; adjustment < 2; adjustment++)
                for (int component = 0; component < 3; component++)
                    result.addMove[adjustment, component] = source.addMove[adjustment, component];
            for (int i = 0; i < result.color.Length && i < source.color.Length; i++) result.color[i] = source.color[i];
            return result;
        }

#pragma warning disable KKANAL01, KKANAL03
        private static ChaFileClothes.PartsInfo CloneClothing(ChaFileClothes.PartsInfo source) {
            if (source == null) return new ChaFileClothes.PartsInfo();
            var result = new ChaFileClothes.PartsInfo {
                id = source.id,
                emblemeId = source.emblemeId,
                emblemeId2 = source.emblemeId2,
                sleevesType = source.sleevesType,
                hideOpt = source.hideOpt == null ? null : (bool[])source.hideOpt.Clone()
            };
            if (source.colorInfo != null) {
                result.colorInfo = new ChaFileClothes.PartsInfo.ColorInfo[source.colorInfo.Length];
                for (int i = 0; i < source.colorInfo.Length; i++) {
                    ChaFileClothes.PartsInfo.ColorInfo color = source.colorInfo[i];
                    result.colorInfo[i] = new ChaFileClothes.PartsInfo.ColorInfo {
                        baseColor = color.baseColor,
                        pattern = color.pattern,
                        tiling = color.tiling,
                        patternColor = color.patternColor
                    };
                }
            }
            return result;
        }
#pragma warning restore KKANAL01, KKANAL03

        private static ChaFileAccessory.PartsInfo ClothingToAccessory(ChaControl character, ChaFileClothes.PartsInfo clothing, int clothingSlot) {
            if (clothing == null || clothing.id == 0 || clothingSlot < 0 || clothingSlot >= ClothingCategories.Length) return NewEmpty();
            int type = (int)ClothingCategories[clothingSlot];
            var result = NewEmpty();
            result.type = type;
            result.id = clothing.id;
            try { result.parentKey = character.GetAccessoryDefaultParentStr(type, clothing.id); }
            catch { result.parentKey = "a_n_head"; }
            if (clothing.colorInfo != null && result.color != null) {
                for (int i = 0; i < clothing.colorInfo.Length && i < result.color.Length; i++)
                    result.color[i] = clothing.colorInfo[i].baseColor;
            }
            return result;
        }

        private static ChaFileAccessory.PartsInfo NewEmpty() { return new ChaFileAccessory.PartsInfo(); }
        internal static bool IsEmpty(ChaFileAccessory.PartsInfo part) { return part == null || part.type == EmptyType; }
        private static float WrapAngle(float value) { return value >= 360f ? value - 360f : value; }

        private sealed class IndexedPart {
            internal readonly int Index;
            internal readonly ChaFileAccessory.PartsInfo Part;

            internal IndexedPart(int index, ChaFileAccessory.PartsInfo part) {
                Index = index;
                Part = part;
            }
        }

        private sealed class SlotPair {
            internal readonly int Source;
            internal readonly int Destination;

            internal SlotPair(int source, int destination) {
                Source = source;
                Destination = destination;
            }
        }
    }

    internal enum MirrorMode {
        None,
        LeftRight,
        UpDown
    }
}
