using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HSControl
{
    /// <summary>
    /// HCtrl-compatible single-update synthetic Aibu pulses. The original
    /// AibuAuto activates HandCtrl.action for ONE update, releases it on the
    /// next call, then waits 0.7–3 seconds. The earlier multi-second hold
    /// made body parts unclickable and the standalone "auto caress" toggle
    /// did not exist in the original HCtrl.
    ///
    /// Unlike HCtrl's unconditional gauge injection on every idle frame,
    /// FemaleGaugeUp is called ONLY on a successfully bound native contact.
    /// This prevents idle-runaway while respecting native touch ownership.
    /// </summary>
    internal sealed class HSControlAibuDriver
    {
        private const BindingFlags Flags = BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo ActionField =
            typeof(HandCtrl).GetField("action", Flags);
        private static readonly FieldInfo ActionUseField =
            typeof(HandCtrl).GetField("actionUseItem", Flags);
        private static readonly FieldInfo AreasField =
            typeof(HandCtrl).GetField("useAreaItems", Flags);
        private static readonly FieldInfo UsedItemsField =
            typeof(HandCtrl).GetField("useItems", Flags);
        private HandCtrl ownedHand;
        private HFlag ownedFlags;
        private int previousUseId;
        private bool restoreUseId;
        private float retryIn;
        private bool completed;

        internal bool Running { get { return ownedHand != null && ownedFlags != null; } }
        internal bool TakeCompleted() {
            bool result = completed;
            completed = false;
            return result;
        }

        private static int Action(HandCtrl hand) {
            if (hand == null) return -1;
            if (ActionField == null) return -1;
            try { return Convert.ToInt32(ActionField.GetValue(hand)); }
            catch { return -1; }
        }

        private static int ReadInt(FieldInfo field, object target) {
            if (field == null || target == null) return -1;
            try { return Convert.ToInt32(field.GetValue(target)); }
            catch { return -1; }
        }

        private static bool Assign(FieldInfo field, object target, int value) {
            if (field == null || target == null) return false;
            try {
                object converted = field.FieldType.IsEnum
                    ? Enum.ToObject(field.FieldType, value)
                    : Convert.ChangeType(value, field.FieldType);
                field.SetValue(target, converted);
                return ReadInt(field, target) == value;
            } catch { return false; }
        }

        private static int MemberInt(object owner, string name) {
            if (owner == null) return -1;
            try {
                Type type = owner.GetType();
                FieldInfo field = type.GetField(name, Flags);
                if (field != null) return Convert.ToInt32(field.GetValue(owner));
                PropertyInfo prop = type.GetProperty(name, Flags);
                if (prop != null) return Convert.ToInt32(prop.GetValue(owner, null));
            } catch { }
            return -1;
        }

        private static int NativeUseArea(HandCtrl hand) {
            if (hand == null || ActionUseField == null || AreasField == null)
                return -1;
            try {
                IEnumerable areas = AreasField.GetValue(hand) as IEnumerable;
                if (areas == null) return -1;
                Array items = UsedItemsField == null ? null :
                    UsedItemsField.GetValue(hand) as Array;
                foreach (object area in areas) {
                    if (area == null) continue;
                    int kind = MemberInt(area, "kindTouch");
                    int id = MemberInt(area, "idUse");
                    if (kind < 2 || kind > 7 || id < 0) continue;
                    if (items != null && (id >= items.Length || items.GetValue(id) == null))
                        continue;
                    return id;
                }
            } catch { }
            return -1;
        }

        internal void Stop(HSceneProc scene) {
            if (Running) {
                // Release only a drag we still own. Never clear the second
                // hand's active drag or overwrite a fresh manual action.
                bool stillOwned = Action(ownedHand) == 1;
                bool secondBusy = scene != null && Action(scene.hand1) != 0;
                if (stillOwned) {
                    Assign(ActionField, ownedHand, 0);
                    if (!secondBusy) ownedFlags.FinishDrag();
                }
                if (restoreUseId && stillOwned)
                    Assign(ActionUseField, ownedHand, previousUseId);
            }
            ownedHand = null;
            ownedFlags = null;
            restoreUseId = false;
        }

        internal void Clear(HSceneProc scene) {
            Stop(scene);
            completed = false;
            retryIn = 0f;
        }

        /// <summary>
        /// Called only when the existing HCtrl automatic speed mode is on.
        /// No independent auto-caress setting: mode 0 disables all synthesis.
        /// </summary>
        internal bool Tick(HSceneProc scene, HFlag flags, float speed, float dt) {
            if (scene == null || flags == null || scene.hand == null ||
                !HSControlActionRules.IsAutoCaressState((int)flags.mode,
                    flags.nowAnimStateName, flags.rely)) {
                Clear(scene);
                return false;
            }

            // HCtrl sends an action only for a single game update. Release
            // our prior pulse BEFORE checking new mouse input.
            if (Running) {
                Stop(scene);
                return false;
            }
            retryIn = Mathf.Max(0f, retryIn - Mathf.Clamp(dt, 0f, .2f));
            if (retryIn > 0f || flags.gaugeFemale >= 99.5f)
                return false;
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                Action(scene.hand) != 0 || Action(scene.hand1) != 0)
                return false;

            int id = NativeUseArea(scene.hand);
            if (id < 0) return false;

            int previous = ReadInt(ActionUseField, scene.hand);
            if (!Assign(ActionUseField, scene.hand, id)) return false;
            if (!flags.DragStart()) {
                Assign(ActionUseField, scene.hand, previous);
                return false;
            }
            if (!Assign(ActionField, scene.hand, 1)) {
                flags.FinishDrag();
                Assign(ActionUseField, scene.hand, previous);
                return false;
            }
            ownedHand = scene.hand;
            ownedFlags = flags;
            previousUseId = previous;
            // -1 is the original native "no selected item" value too.
            restoreUseId = ActionUseField != null;

            flags.speed = Mathf.Clamp01(speed);
            if (scene.hand.nowMES != null && flags.gaugeFemale < 100f)
                flags.FemaleGaugeUp(flags.speed * 5f * flags.rateDragGauge,
                    false, scene.hand.nowMES.stateMotion == 0);

            // 0.7–3 s is the interval to the NEXT one-frame pulse,
            // not how long to hold the hand.action field.
            retryIn = UnityEngine.Random.Range(.7f, 3f);
            if (flags.gaugeFemale >= 100f) {
                Stop(scene);
                completed = true;
            }
            return true;
        }
    }
}
