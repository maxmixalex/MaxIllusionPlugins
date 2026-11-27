using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HSControl
{
    /// <summary>
    /// Explicitly opt-in automated native Aibu interaction.
    /// Auto-speed alone never increases gauge or starts a caress.
    /// A synthetic drag is allowed only after a real usable native area was
    /// resolved and bound to HandCtrl.actionUseItem, as in KK_HCtrl.AibuAuto.
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
        private HandCtrl ownedHand;
        private HFlag ownedFlags;
        private float remaining;
        private float retryIn;
        private bool completed;

        internal bool TakeCompleted() {
            bool wasCompleted = completed;
            completed = false;
            return wasCompleted;
        }

        internal void Clear(HSceneProc scene) {
            Stop(scene);
            completed = false;
            retryIn = 0f;
        }

        internal bool Running {
            get { return ownedHand != null && ownedFlags != null; }
        }

        private static int Action(HandCtrl hand)
        {
            if (hand == null) return 0;
            if (ActionField == null) return -1;
            try { return Convert.ToInt32(ActionField.GetValue(hand)); }
            catch { return -1; }
        }

        private static bool Assign(FieldInfo field, object target, int value)
        {
            if (field == null || target == null) return false;
            try {
                object converted = field.FieldType.IsEnum
                    ? Enum.ToObject(field.FieldType, value)
                    : Convert.ChangeType(value, field.FieldType);
                field.SetValue(target, converted);
                return Convert.ToInt32(field.GetValue(target)) == value;
            } catch { return false; }
        }

        private static int FieldInt(object owner, string name)
        {
            if (owner == null) return -1;
            try {
                Type type = owner.GetType();
                FieldInfo field = type.GetField(name, Flags);
                if (field != null) return Convert.ToInt32(field.GetValue(owner));
                PropertyInfo property = type.GetProperty(name, Flags);
                if (property != null)
                    return Convert.ToInt32(property.GetValue(owner, null));
            } catch { }
            return -1;
        }

        private static int NativeUseArea(HandCtrl hand)
        {
            if (hand == null || ActionUseField == null || AreasField == null)
                return -1;
            try {
                IEnumerable areas = AreasField.GetValue(hand) as IEnumerable;
                if (areas == null) return -1;
                foreach (object area in areas) {
                    if (area == null) continue;
                    int kind = FieldInt(area, "kindTouch");
                    int idUse = FieldInt(area, "idUse");
                    if (kind >= 2 && kind <= 7 && idUse >= 0)
                        return idUse;
                }
            } catch { }
            return -1;
        }

        internal void Stop(HSceneProc scene)
        {
            if (Running && Action(ownedHand) == 1) {
                bool otherHandBusy = scene != null && Action(scene.hand1) != 0;
                Assign(ActionField, ownedHand, 0);
                if (!otherHandBusy) ownedFlags.FinishDrag();
            }
            ownedHand = null;
            ownedFlags = null;
            remaining = 0f;
        }

        /// <summary>
        /// Called only in supported Aibu animation states. The 'autoCaress'
        /// switch is separate from automatic speed settings and defaults OFF.
        /// Returns true only when this driver actually owns a native drag.
        /// </summary>
        internal bool Tick(HSceneProc scene, HFlag flags, float speed, float delta,
            bool autoCaress)
        {
            if (scene == null || flags == null || scene.hand == null ||
                !HSControlActionRules.IsAutoCaressState((int)flags.mode,
                    flags.nowAnimStateName, flags.rely)) {
                Stop(scene);
                return false;
            }

            float dt = Mathf.Clamp(delta, 0f, .2f);
            if (!autoCaress) {
                Stop(scene);
                // Manual caress is owned by the game. Adjust speed only
                // when an actual native input is already in progress.
                if (Action(scene.hand) > 0 || Action(scene.hand1) > 0)
                    flags.speed = Mathf.Clamp01(speed);
                return false;
            }
            retryIn = Mathf.Max(0f, retryIn - dt);
            HandCtrl hand = scene.hand;
            if (Running && (!ReferenceEquals(ownedHand, hand) ||
                !ReferenceEquals(ownedFlags, flags) || Action(hand) != 1))
                Stop(scene);

            // Native mouse/second-hand activity always wins.
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                Action(scene.hand1) != 0) {
                Stop(scene);
                return false;
            }
            if (!Running) {
                if (Action(hand) != 0 || retryIn > 0f ||
                    flags.gaugeFemale >= 99.5f) return false;
                int id = NativeUseArea(hand);
                if (id < 0 || !Assign(ActionUseField, hand, id))
                    return false;
                if (!flags.DragStart()) return false;
                if (!Assign(ActionField, hand, 1)) {
                    flags.FinishDrag();
                    return false;
                }
                ownedHand = hand;
                ownedFlags = flags;
                remaining = UnityEngine.Random.Range(.7f, 3f);
            }

            if (!Running) return false;
            flags.speed = Mathf.Clamp01(speed);
            // Unlike the previous unconditional call, raise the gauge only
            // while our own native drag is active, and never during idle.
            if (hand.nowMES != null && flags.gaugeFemale < 100f)
                flags.FemaleGaugeUp(flags.speed * 5f * flags.rateDragGauge,
                    false, hand.nowMES.stateMotion == 0);
            remaining -= dt;
            if (flags.gaugeFemale >= 100f) {
                // Only an actual owned caress can request the native finish.
                Stop(scene);
                completed = true;
                retryIn = UnityEngine.Random.Range(.7f, 3f);
            } else if (remaining <= 0f) {
                Stop(scene);
                retryIn = UnityEngine.Random.Range(.7f, 3f);
            }
            return true;
        }
    }
}
