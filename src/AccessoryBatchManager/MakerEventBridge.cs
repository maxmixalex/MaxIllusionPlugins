using System;
using System.Reflection;
using KKAPI.Maker;

namespace AccessoryBatchManager {
    internal static class MakerEventBridge {
        internal static void AccessoriesCopied(int[] slots, int sourceCoordinate, int destinationCoordinate) {
            Raise("AccessoriesCopied", new AccessoryCopyEventArgs(
                slots,
                (ChaFileDefine.CoordinateType)sourceCoordinate,
                (ChaFileDefine.CoordinateType)destinationCoordinate));
        }

        internal static void AccessoryTransferred(int sourceSlot, int destinationSlot) {
            Raise("AccessoryTransferred", new AccessoryTransferEventArgs(sourceSlot, destinationSlot));
        }

        internal static void AccessoryKindChanged(int slot) {
            Raise("AccessoryKindChanged", new AccessorySlotEventArgs(slot));
        }

        private static void Raise<T>(string fieldName, T eventArgs) where T : EventArgs {
            FieldInfo field = typeof(AccessoriesApi).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            var handlers = field == null ? null : field.GetValue(null) as EventHandler<T>;
            if (handlers == null) return;
            foreach (EventHandler<T> handler in handlers.GetInvocationList()) {
                try { handler(null, eventArgs); }
                catch { /* Match KKAPI's resilient event dispatch: one integration must not block the rest. */ }
            }
        }
    }
}
