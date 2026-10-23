using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace ClothingCompression {
    internal sealed class MeshAccessDetour {
        private readonly NativeDetour detour;

        public MeshAccessDetour() {
            detour = new NativeDetour(AccessTools.Property(typeof(Mesh), "canAccess").GetGetMethod(true),
                AccessTools.Method(typeof(MeshAccessDetour), nameof(CanAccess)));
            detour.Undo();
        }

        public bool CanAccess() { return true; }
        public void Apply() { detour.Apply(); }
        public void Undo() { detour.Undo(); }
        public void Dispose() { detour.Dispose(); }
    }
}
