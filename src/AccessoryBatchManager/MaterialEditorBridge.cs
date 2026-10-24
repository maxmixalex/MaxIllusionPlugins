using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AccessoryBatchManager {
    internal static class MaterialEditorBridge {
        private const string ControllerTypeName = "KK_Plugins.MaterialEditor.MaterialEditorCharaController, KK_MaterialEditor";
        private const string PluginTypeName = "KK_Plugins.MaterialEditor.MaterialEditorPlugin, KK_MaterialEditor";
        private static readonly string[] PropertyListNames = {
            "RendererPropertyList",
            "ProjectorPropertyList",
            "MaterialNamePropertyList",
            "MaterialFloatPropertyList",
            "MaterialKeywordPropertyList",
            "MaterialColorPropertyList",
            "MaterialTexturePropertyList",
            "MaterialShaderList",
            "MaterialCopyList"
        };
        private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static object CaptureState(ChaControl character) {
            return CaptureControllerState(GetController(character));
        }

        internal static object CaptureControllerState(object controller) {
            if (controller == null) return null;
            var snapshot = new StateSnapshot(controller);
            Type controllerType = controller.GetType();
            foreach (string name in PropertyListNames) {
                FieldInfo field = controllerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = field == null ? null : field.GetValue(controller) as IList;
                if (list != null) snapshot.Lists[field] = list.Cast<object>()
                    .Select(value => value == null ? null : MemberwiseCloneMethod.Invoke(value, null)).ToArray();
            }
            FieldInfo textureField = controllerType.GetField("TextureDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary textures = textureField == null ? null : textureField.GetValue(controller) as IDictionary;
            if (textures != null) {
                snapshot.TextureField = textureField;
                foreach (DictionaryEntry entry in textures) {
                    PropertyInfo dataProperty = entry.Value == null ? null : entry.Value.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public);
                    byte[] data = dataProperty == null ? null : dataProperty.GetValue(entry.Value, null) as byte[];
                    if (data != null) snapshot.Textures[Convert.ToInt32(entry.Key)] = (byte[])data.Clone();
                }
            }
            return snapshot;
        }

        internal static void RestoreState(object value) {
            var snapshot = value as StateSnapshot;
            if (snapshot == null || snapshot.Controller == null) return;
            foreach (KeyValuePair<FieldInfo, object[]> item in snapshot.Lists) {
                IList list = item.Key.GetValue(snapshot.Controller) as IList;
                if (list == null) continue;
                list.Clear();
                foreach (object property in item.Value) list.Add(property);
            }
            IDictionary textures = snapshot.TextureField == null ? null : snapshot.TextureField.GetValue(snapshot.Controller) as IDictionary;
            if (textures == null) return;
            Type containerType = textures.GetType().GetGenericArguments()[1];
            ConstructorInfo constructor = containerType.GetConstructor(new[] { typeof(byte[]) });
            if (constructor == null) return;
            foreach (DictionaryEntry entry in textures) {
                var disposable = entry.Value as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
            textures.Clear();
            foreach (KeyValuePair<int, byte[]> texture in snapshot.Textures)
                textures.Add(texture.Key, constructor.Invoke(new object[] { texture.Value }));
        }

        internal static void InsertAccessories(ChaControl character, int sourceCoordinate, int destinationCoordinate,
            IList<int> sources, IList<int> destinations, int insertIndex) {
            object controller = GetController(character);
            if (controller == null || sources == null || destinations == null || sources.Count != destinations.Count) return;
            Type controllerType = controller.GetType();
            int insertedCount = destinations.Count;
            foreach (string name in PropertyListNames) {
                FieldInfo field = controllerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = field == null ? null : field.GetValue(controller) as IList;
                if (list == null) continue;
                object[] snapshot = list.Cast<object>().ToArray();
                for (int i = list.Count - 1; i >= 0; i--) {
                    object value = list[i];
                    if (IsAccessory(value) && ReadInt(value, "CoordinateIndex") == destinationCoordinate) list.RemoveAt(i);
                }
                foreach (object value in snapshot) {
                    if (!IsAccessory(value) || ReadInt(value, "CoordinateIndex") != destinationCoordinate) continue;
                    object shifted = MemberwiseCloneMethod.Invoke(value, null);
                    int slot = ReadInt(shifted, "Slot");
                    if (slot >= insertIndex) WriteInt(shifted, "Slot", slot + insertedCount);
                    list.Add(shifted);
                }
                for (int pair = 0; pair < sources.Count; pair++) {
                    int sourceSlot = sources[pair];
                    foreach (object value in snapshot.Where(item => IsAccessory(item) &&
                        ReadInt(item, "CoordinateIndex") == sourceCoordinate && ReadInt(item, "Slot") == sourceSlot)) {
                        object copy = MemberwiseCloneMethod.Invoke(value, null);
                        WriteInt(copy, "CoordinateIndex", destinationCoordinate);
                        WriteInt(copy, "Slot", destinations[pair]);
                        list.Add(copy);
                    }
                }
            }
        }

        internal static void ShiftAccessoriesForInsert(ChaControl character, int destinationCoordinate, int insertIndex, int insertedCount) {
            object controller = GetController(character);
            if (controller == null || insertedCount <= 0) return;
            Type controllerType = controller.GetType();
            foreach (string name in PropertyListNames) {
                FieldInfo field = controllerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = field == null ? null : field.GetValue(controller) as IList;
                if (list == null) continue;
                foreach (object value in list.Cast<object>().Where(item => IsAccessory(item) &&
                    ReadInt(item, "CoordinateIndex") == destinationCoordinate && ReadInt(item, "Slot") >= insertIndex).ToArray())
                    WriteInt(value, "Slot", ReadInt(value, "Slot") + insertedCount);
            }
        }

        internal static void ReapplyAllImmediately(ChaControl character) {
            if (character == null) return;
            object controller = GetController(character);
            if (controller == null) return;
            MethodInfo loadData = controller.GetType().GetMethod("LoadData", BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) }, null);
            object[] arguments = { true, true, true, true };
            if (loadData == null) {
                loadData = controller.GetType().GetMethod("LoadData", BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
                arguments = new object[] { true, true, true };
            }
            if (loadData == null) return;

            IEnumerator routine = null;
            try { routine = loadData.Invoke(controller, arguments) as IEnumerator; }
            catch { return; }
            if (routine == null) return;

            var routines = new Stack<IEnumerator>();
            routines.Push(routine);
            int guard = 0;
            while (routines.Count > 0 && guard++ < 64) {
                IEnumerator current = routines.Peek();
                bool more;
                try { more = current.MoveNext(); }
                catch { return; }
                if (!more) {
                    routines.Pop();
                    continue;
                }
                var nested = current.Current as IEnumerator;
                if (nested != null) routines.Push(nested);
            }
        }

        internal static int CopyFromCoordinateCard(ChaControl character, ChaFileCoordinate sourceCoordinate,
            int destinationCoordinate, IList<int> sources, IList<int> destinations) {
            return CopyFromCoordinateCard(character, sourceCoordinate, destinationCoordinate, sources, destinations, "Accessory", "Accessory");
        }

        internal static int CopyClothingFromCoordinateCard(ChaControl character, ChaFileCoordinate sourceCoordinate,
            int destinationCoordinate, IList<int> sources, IList<int> destinations, bool toAccessories) {
            return CopyFromCoordinateCard(character, sourceCoordinate, destinationCoordinate, sources, destinations,
                "Clothing", toAccessories ? "Accessory" : "Clothing");
        }

        private static int CopyFromCoordinateCard(ChaControl character, ChaFileCoordinate sourceCoordinate,
            int destinationCoordinate, IList<int> sources, IList<int> destinations, string sourceType, string destinationType) {
            object controller = GetController(character);
            if (controller == null || sourceCoordinate == null) return 0;
            List<SlotPair> pairs = BuildPairs(sources, destinations);
            if (pairs.Count == 0) return 0;

            Type controllerType = controller.GetType();
            MethodInfo loadCoordinate = controllerType.GetMethod("LoadCoordinateExtSaveData", BindingFlags.Instance | BindingFlags.NonPublic);
            if (loadCoordinate == null) return 0;

            var lists = new Dictionary<FieldInfo, IList>();
            foreach (string name in PropertyListNames) {
                FieldInfo field = controllerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = field == null ? null : field.GetValue(controller) as IList;
                if (field == null || list == null) continue;
                lists[field] = list;
            }
            if (lists.Count == 0) return 0;

            StateSnapshot originalState = CaptureState(character) as StateSnapshot;
            var imported = new Dictionary<FieldInfo, object[]>();
            Dictionary<int, byte[]> importedTextures = null;
            bool loaded = false;
            try {
                loadCoordinate.Invoke(controller, new object[] { sourceCoordinate });
                foreach (KeyValuePair<FieldInfo, IList> item in lists) {
                    IList loadedList = item.Key.GetValue(controller) as IList;
                    imported[item.Key] = (loadedList == null ? new object[0] : loadedList.Cast<object>())
                        .Where(value => IsObjectType(value, sourceType) && pairs.Any(pair => pair.Source == ReadInt(value, "Slot")))
                        .ToArray();
                }
                importedTextures = CaptureTextureBytes(controller);
                loaded = true;
            }
            catch {
                loaded = false;
            }
            finally {
                RestoreState(originalState);
            }
            if (!loaded) return 0;

            int copiedProperties = 0;
            var textureIdMap = new Dictionary<int, int>();
            foreach (KeyValuePair<FieldInfo, IList> item in lists) {
                IList destinationList = item.Key.GetValue(controller) as IList;
                if (destinationList == null) continue;
                var destinationSlots = new HashSet<int>(pairs.Select(pair => pair.Destination));
                for (int i = destinationList.Count - 1; i >= 0; i--) {
                    object value = destinationList[i];
                    if (IsObjectType(value, destinationType) && ReadInt(value, "CoordinateIndex") == destinationCoordinate &&
                        destinationSlots.Contains(ReadInt(value, "Slot"))) destinationList.RemoveAt(i);
                }

                foreach (SlotPair pair in pairs) {
                    foreach (object sourceValue in imported[item.Key].Where(value => ReadInt(value, "Slot") == pair.Source)) {
                        object copy = MemberwiseCloneMethod.Invoke(sourceValue, null);
                        WriteObjectType(copy, destinationType);
                        WriteInt(copy, "CoordinateIndex", destinationCoordinate);
                        WriteInt(copy, "Slot", pair.Destination);
                        RemapImportedTexture(controller, copy, importedTextures, textureIdMap);
                        destinationList.Add(copy);
                        copiedProperties++;
                    }
                }
            }
            return copiedProperties;
        }

        private static Dictionary<int, byte[]> CaptureTextureBytes(object controller) {
            var result = new Dictionary<int, byte[]>();
            FieldInfo field = controller == null ? null : controller.GetType().GetField("TextureDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary textures = field == null ? null : field.GetValue(controller) as IDictionary;
            if (textures == null) return result;
            foreach (DictionaryEntry entry in textures) {
                PropertyInfo dataProperty = entry.Value == null ? null : entry.Value.GetType().GetProperty("Data", BindingFlags.Instance | BindingFlags.Public);
                byte[] data = dataProperty == null ? null : dataProperty.GetValue(entry.Value, null) as byte[];
                if (data != null) result[Convert.ToInt32(entry.Key)] = (byte[])data.Clone();
            }
            return result;
        }

        private static void RemapImportedTexture(object controller, object property, IDictionary<int, byte[]> importedTextures,
            IDictionary<int, int> textureIdMap) {
            FieldInfo textureIdField = property == null ? null : property.GetType().GetField("TexID", BindingFlags.Instance | BindingFlags.Public);
            if (textureIdField == null) return;
            object rawId = textureIdField.GetValue(property);
            if (rawId == null) return;
            int oldId = Convert.ToInt32(rawId);
            byte[] data;
            if (importedTextures == null || !importedTextures.TryGetValue(oldId, out data)) return;

            int newId;
            if (!textureIdMap.TryGetValue(oldId, out newId)) {
                FieldInfo dictionaryField = controller.GetType().GetField("TextureDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                IDictionary textures = dictionaryField == null ? null : dictionaryField.GetValue(controller) as IDictionary;
                if (textures == null) return;
                newId = 0;
                foreach (object key in textures.Keys) newId = Math.Max(newId, Convert.ToInt32(key) + 1);
                Type[] arguments = textures.GetType().GetGenericArguments();
                if (arguments.Length != 2) return;
                ConstructorInfo constructor = arguments[1].GetConstructor(new[] { typeof(byte[]) });
                if (constructor == null) return;
                textures.Add(newId, constructor.Invoke(new object[] { (byte[])data.Clone() }));
                textureIdMap[oldId] = newId;
            }
            textureIdField.SetValue(property, textureIdField.FieldType == typeof(int)
                ? (object)newId : Activator.CreateInstance(textureIdField.FieldType, new object[] { newId }));
        }

        internal static void CopyOrMoveClothingProperties(ChaControl character, int sourceCoordinate, int destinationCoordinate,
            IList<int> sources, IList<int> destinations, bool toAccessories, bool move) {
            CopyOrMoveProperties(character, sourceCoordinate, destinationCoordinate, sources, destinations,
                "Clothing", toAccessories ? "Accessory" : "Clothing", move);
        }

        internal static void CopyOrMoveAccessoryProperties(ChaControl character, int sourceCoordinate, int destinationCoordinate,
            IList<int> sources, IList<int> destinations, bool move) {
            CopyOrMoveProperties(character, sourceCoordinate, destinationCoordinate, sources, destinations,
                "Accessory", "Accessory", move);
        }

        private static void CopyOrMoveProperties(ChaControl character, int sourceCoordinate, int destinationCoordinate,
            IList<int> sources, IList<int> destinations, string sourceType, string destinationType, bool move) {
            object controller = GetController(character);
            if (controller == null) return;
            List<SlotPair> pairs = BuildPairs(sources, destinations);
            if (pairs.Count == 0) return;
            Type controllerType = controller.GetType();
            foreach (string name in PropertyListNames) {
                FieldInfo field = controllerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                IList list = field == null ? null : field.GetValue(controller) as IList;
                if (list == null) continue;
                object[] snapshot = list.Cast<object>().ToArray();
                var destinationSlots = new HashSet<int>(pairs.Select(pair => pair.Destination));
                for (int i = list.Count - 1; i >= 0; i--) {
                    object item = list[i];
                    bool replaceDestination = IsObjectType(item, destinationType) &&
                        ReadInt(item, "CoordinateIndex") == destinationCoordinate && destinationSlots.Contains(ReadInt(item, "Slot"));
                    bool clearMovedSource = move && IsObjectType(item, sourceType) &&
                        ReadInt(item, "CoordinateIndex") == sourceCoordinate && pairs.Any(pair => pair.Source == ReadInt(item, "Slot")) &&
                        !(sourceCoordinate == destinationCoordinate && string.Equals(sourceType, destinationType, StringComparison.Ordinal) &&
                            destinationSlots.Contains(ReadInt(item, "Slot")));
                    if (replaceDestination || clearMovedSource) list.RemoveAt(i);
                }
                foreach (SlotPair pair in pairs) {
                    foreach (object sourceValue in snapshot.Where(item => IsObjectType(item, sourceType) &&
                        ReadInt(item, "CoordinateIndex") == sourceCoordinate && ReadInt(item, "Slot") == pair.Source)) {
                        object copy = MemberwiseCloneMethod.Invoke(sourceValue, null);
                        WriteObjectType(copy, destinationType);
                        WriteInt(copy, "CoordinateIndex", destinationCoordinate);
                        WriteInt(copy, "Slot", pair.Destination);
                        list.Add(copy);
                    }
                }
            }
        }

        private static object GetController(ChaControl character) {
            if (character == null) return null;
            Type pluginType = Type.GetType(PluginTypeName, false);
            MethodInfo getter = pluginType == null ? null : pluginType.GetMethod("GetCharaController", BindingFlags.Public | BindingFlags.Static);
            if (getter != null) {
                try { return getter.Invoke(null, new object[] { character }); }
                catch { }
            }
            Type controllerType = Type.GetType(ControllerTypeName, false);
            return controllerType == null ? null : character.gameObject.GetComponent(controllerType);
        }

        private static bool IsAccessory(object value) {
            return IsObjectType(value, "Accessory");
        }

        private static bool IsObjectType(object value, string expected) {
            if (value == null) return false;
            FieldInfo field = value.GetType().GetField("ObjectType", BindingFlags.Instance | BindingFlags.Public);
            object objectType = field == null ? null : field.GetValue(value);
            return objectType != null && string.Equals(objectType.ToString(), expected, StringComparison.Ordinal);
        }

        private static void WriteObjectType(object value, string objectType) {
            FieldInfo field = value == null ? null : value.GetType().GetField("ObjectType", BindingFlags.Instance | BindingFlags.Public);
            if (field != null) field.SetValue(value, Enum.Parse(field.FieldType, objectType));
        }

        private static int ReadInt(object value, string name) {
            FieldInfo field = value == null ? null : value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            return field == null ? -1 : Convert.ToInt32(field.GetValue(value));
        }

        private static void WriteInt(object value, string name, int number) {
            FieldInfo field = value == null ? null : value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null) field.SetValue(value, number);
        }

        private static List<SlotPair> BuildPairs(IList<int> sources, IList<int> destinations) {
            var result = new List<SlotPair>();
            if (sources == null || destinations == null || sources.Count == 0 || destinations.Count == 0) return result;
            if (sources.Count == destinations.Count) {
                for (int i = 0; i < sources.Count; i++) result.Add(new SlotPair(sources[i], destinations[i]));
            }
            else if (sources.Count == 1) {
                for (int i = 0; i < destinations.Count; i++) result.Add(new SlotPair(sources[0], destinations[i]));
            }
            return result;
        }

        private sealed class SlotPair {
            internal SlotPair(int source, int destination) { Source = source; Destination = destination; }
            internal int Source { get; private set; }
            internal int Destination { get; private set; }
        }

        private sealed class StateSnapshot {
            internal StateSnapshot(object controller) { Controller = controller; }
            internal readonly object Controller;
            internal readonly Dictionary<FieldInfo, object[]> Lists = new Dictionary<FieldInfo, object[]>();
            internal readonly Dictionary<int, byte[]> Textures = new Dictionary<int, byte[]>();
            internal FieldInfo TextureField;
        }
    }
}
