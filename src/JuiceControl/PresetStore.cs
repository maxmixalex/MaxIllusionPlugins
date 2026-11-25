using System;

namespace JuiceControl {
    /// <summary>Keeps the loaded document and its writable destination together.</summary>
    internal sealed class PresetStore {
        private PresetXml data = new PresetXml();
        private string path;
        private bool canSave;

        public PresetXml Data { get { return data; } }
        public string Path { get { return path; } }
        public bool CanSave { get { return canSave; } }

        public void Invalidate() {
            canSave = false;
        }

        public void Load(string requestedPath) {
            // An unsuccessful switch must never leave the old path writable.
            // Keep the old in-memory presets available for applying to characters.
            Invalidate();
            PresetXml loaded = PresetXml.Load(requestedPath);
            data = loaded;
            path = requestedPath;
            canSave = true;
        }

        public void Save() {
            if (!canSave) throw new InvalidOperationException("请成功重载当前配置的预设 XML 后再保存。");
            data.Save(path);
        }
    }
}
