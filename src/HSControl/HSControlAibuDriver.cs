using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HSControl
{
    /// <summary>
    /// A single continuous, non-restarting native Aibu interaction.
    ///
    /// Previous implementation issued DragStart/FinishDrag every 0.7–3s,
    /// repeatedly restarting the heroine animation and duplicating gauge
    /// increases. The current driver:
    /// - enters only after an explicit current-scene speed change;
    /// - calls DragStart ONCE per uninterrupted automatic segment;
    /// - yields immediately to either manual hand/mouse button;
    /// - never resets native playback every few frames;
    /// - allows the native engine to raise the gauge, using only a paced,
    ///   conservative fallback if its gauge has genuinely stalled.
    /// No new manual "auto-caress enabled" switch.
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
        private float manualCooldown;
        private float gaugeStalledFor, fallbackElapsed;
        private float gaugeLastSeen = -1f;
        private bool completed;

        internal bool Running { get { return ownedHand != null && ownedFlags != null; } }
        internal bool TakeCompleted() {
            bool result = completed;
            completed = false;
            return result;
        }

        private static int Action(HandCtrl hand) {
            if (hand == null || ActionField == null) return -1;
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
                if (prop != null)
                    return Convert.ToInt32(prop.GetValue(owner, null));
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
                    if (items != null &&
                        (id >= items.Length || items.GetValue(id) == null))
                        continue;
                    return id;
                }
            } catch { }
            return -1;
        }

        internal void Stop(HSceneProc scene) {
            if (Running) {
                bool stillOwned = Action(ownedHand) == 1;
                if (stillOwned)
                    Assign(ActionField, ownedHand, 0);
                if (restoreUseId && stillOwned)
                    Assign(ActionUseField, ownedHand, previousUseId);
                // An ongoing synthetic drag needs one native release.
                // No repeated FinishDrag calls during ordinary movement.
                if (stillOwned && ownedFlags != null)
                    ownedFlags.FinishDrag();
            }
            ownedHand = null;
            ownedFlags = null;
            restoreUseId = false;
            gaugeStalledFor = fallbackElapsed = 0f;
            gaugeLastSeen = -1f;
        }

        internal void Clear(HSceneProc scene) {
            Stop(scene);
            manualCooldown = 0f;
            completed = false;
        }

        private void PaceGauge(HFlag flags, HandCtrl hand, float speed, float dt)
        {
            float actual = flags.gaugeFemale;
            if (gaugeLastSeen < 0f || actual > gaugeLastSeen + .01f) {
                gaugeStalledFor = 0f;
                fallbackElapsed = 0f;
            } else {
                gaugeStalledFor += dt;
                fallbackElapsed += dt;
            }
            gaugeLastSeen = actual;
            // Native HandCtrl normally owns the gauge progression. Only if
            // it has remained flat > .35 seconds do we ask the original
            // FemaleGaugeUp method for a *bounded* supplementary increment,
            // never once per Update with an unscaled multiplier of five.
            if (gaugeStalledFor >= .35f && fallbackElapsed >= .18f &&
                hand != null && hand.nowMES != null &&
                flags.gaugeFemale < 99.5f) {
                fallbackElapsed = 0f;
                float nativeDelta = Mathf.Clamp01(speed) *
                    Mathf.Clamp(flags.rateDragGauge, .1f, 2f) * .45f;
                if (nativeDelta > 0f)
                    flags.FemaleGaugeUp(nativeDelta, false,
                        hand.nowMES.stateMotion == 0);
                gaugeLastSeen = flags.gaugeFemale;
            }
        }

        internal bool Tick(HSceneProc scene, HFlag flags, float speed, float dt)
        {
            float delta = Mathf.Clamp(dt, 0f, .1f);
            if (scene == null || flags == null || scene.hand == null ||
                !HSControlActionRules.IsAutoCaressState((int)flags.mode,
                    flags.nowAnimStateName, flags.rely)) {
                Clear(scene);
                return false;
            }
            // Changing body part, using either hand, or holding a mouse
            // button immediately cancels OUR interaction. The native hand
            // takes over; avoid re-acquiring while the player is still
            // choosing a different body region.
            if (Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                Action(scene.hand1) > 0) {
                Stop(scene);
                manualCooldown = .85f;
                return false;
            }

            manualCooldown = Mathf.Max(0f, manualCooldown - delta);
            if (Running && (!ReferenceEquals(ownedHand, scene.hand) ||
                !ReferenceEquals(ownedFlags, flags))) {
                Stop(scene);
                manualCooldown = .85f;
                return false;
            }

            if (!Running) {
                if (manualCooldown > 0f || Action(scene.hand) != 0 ||
                    flags.gaugeFemale >= 99.5f) return false;
                int id = NativeUseArea(scene.hand);
                if (id < 0) return false;
                int previous = ReadInt(ActionUseField, scene.hand);
                if (!Assign(ActionUseField, scene.hand, id))
                    return false;
                if (!flags.DragStart()) {
                    Assign(ActionUseField, scene.hand, previous);
                    manualCooldown = .5f;
                    return false;
                }
                if (!Assign(ActionField, scene.hand, 1)) {
                    flags.FinishDrag();
                    Assign(ActionUseField, scene.hand, previous);
                    manualCooldown = .5f;
                    return false;
                }
                ownedHand = scene.hand;
                ownedFlags = flags;
                previousUseId = previous;
                restoreUseId = ActionUseField != null;
                gaugeLastSeen = flags.gaugeFemale;
                gaugeStalledFor = fallbackElapsed = 0f;
            } else if (Action(scene.hand) != 1) {
                // The game's hand state changed externally: never fight it.
                // Repeating DragStart would restart the native animation.
                ownedHand = null;
                ownedFlags = null;
                restoreUseId = false;
                manualCooldown = .85f;
                return false;
            }

            flags.speed = Mathf.Clamp01(speed);
            PaceGauge(flags, scene.hand, speed, delta);
            if (flags.gaugeFemale >= 99.5f) {
                Stop(scene);
                completed = true;
            }
            return true;
        }
    }
}
