using System;
using System.IO;
using System.Reflection;
using ChaCustom;
using UnityEngine;

#pragma warning disable KKANAL03

namespace AccessoryBatchManager {
    internal sealed class CoordinateCardSource {
        private static readonly FieldInfo ListControllerField = typeof(CustomCoordinateFile).GetField("listCtrl", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo FileWindowField = typeof(CustomCoordinateFile).GetField("fileWindow", BindingFlags.Instance | BindingFlags.NonPublic);

        private string cachedPath;
        private DateTime cachedWriteTime;
        private ChaFileCoordinate cachedCoordinate;

        internal string DisplayName { get; private set; }
        internal ChaFileCoordinate Coordinate { get { return cachedCoordinate; } }
        internal ChaFileAccessory.PartsInfo[] Parts { get { return cachedCoordinate == null ? null : cachedCoordinate.accessory.parts; } }

        internal bool TryRefresh() {
            CustomCoordinateFile coordinateFile = UnityEngine.Object.FindObjectOfType<CustomCoordinateFile>();
            if (coordinateFile == null || !coordinateFile.gameObject.activeInHierarchy || ListControllerField == null || FileWindowField == null)
                return Clear();

            var fileWindow = FileWindowField.GetValue(coordinateFile) as CustomFileWindow;
            var listController = ListControllerField.GetValue(coordinateFile) as CustomFileListCtrl;
            if (fileWindow == null || listController == null || !fileWindow.gameObject.activeInHierarchy ||
                fileWindow.fwType != CustomFileWindow.FileWindowType.CoordinateLoad)
                return Clear();

            CustomFileInfoComponent selected = listController.GetSelectTopItem();
            CustomFileInfo info = selected == null ? null : selected.info;
            if (info == null || string.IsNullOrEmpty(info.FullPath) || !File.Exists(info.FullPath)) return Clear();

            DateTime writeTime = File.GetLastWriteTimeUtc(info.FullPath);
            if (cachedCoordinate == null || !string.Equals(cachedPath, info.FullPath, StringComparison.OrdinalIgnoreCase) || cachedWriteTime != writeTime) {
                var coordinate = new ChaFileCoordinate();
                if (!coordinate.LoadFile(info.FullPath)) return Clear();
                cachedPath = info.FullPath;
                cachedWriteTime = writeTime;
                cachedCoordinate = coordinate;
            }

            string name = cachedCoordinate.coordinateName;
            if (string.IsNullOrEmpty(name)) name = info.name;
            if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(info.FileName);
            if (string.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(info.FullPath);
            DisplayName = name + "（服装卡）";
            return Parts != null;
        }

        private bool Clear() {
            cachedPath = null;
            cachedWriteTime = default(DateTime);
            cachedCoordinate = null;
            DisplayName = null;
            return false;
        }
    }
}
#pragma warning restore KKANAL03
