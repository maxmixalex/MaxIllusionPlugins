using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace HSControl
{
    /// <summary>
    /// Native, scene-local automatic Aibu. HCtrl calls FemaleGaugeUp every
    /// update and briefly owns the main hand drag for an available contact.
    /// Unlike the old implementation, yielding to either manual hand never
    /// overwrites HandCtrl's action or clears another player's drag.
    /// </summary>
    internal sealed class HSControlAibuDriver
    {
        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo MainAction =
            typeof(HandCtrl).GetField("action", InstanceFields);
        private static readonly FieldInfo AvailableAreas =
            typeof(HandCtrl).GetField("useAreaItems", InstanceFields);
        private static readonly FieldInfo ActionUses =
            typeof(HandCtrl).GetField("actionUseItem", InstanceFields);
        private HandCtrl ownedHand;
        private HFlag ownedFlags;
        private float remaining;

        internal bool Running {
            get { return ownedHand != null && ownedFlags != null; }
        }

        private static int ReadAction(HandCtrl hand)
        {
            if (hand == null || MainAction == null) return 0;
            try { return Convert.ToInt32(MainAction.GetValue(hand)); }
            catch { return -1; }
        }

        private static bool WriteAction(HandCtrl hand, int code)
        {
            if (hand == null || MainAction == null) return false;
            try {
                object value = MainAction.FieldType.IsEnum
                    ? Enum.ToObject(MainAction.FieldType, code)
                    : Convert.ChangeType(code, MainAction.FieldType);
                MainAction.SetValue(hand, value);
                return ReadAction(hand) == code;
            } catch { return false; }
        }

        private static object GetMember(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();
            try {
                FieldInfo field = type.GetField(name, InstanceFields);
                if (field != null) return field.GetValue(source);
                PropertyInfo property = type.GetProperty(name, InstanceFields);
                if (property != null) return property.GetValue(source, null);
            } catch { }
            return null;
        }

        private static bool HasNativeCaressArea(HandCtrl hand)
        {
            // Match HCtrl's useAreaItems/kindTouch path when selecting a
            // synthetic hand drag. The original still invokes FemaleGaugeUp
            // even if none of these areas is currently selected.
            if (hand == null || AvailableAreas == null) return false;
            IEnumerable areas;
            try { areas = AvailableAreas.GetValue(hand) as IEnumerable; }
            catch { return false; }
            if (areas == null) return false;
            foreach (object area in areas)
            {
                if (area == null) continue;
                object kind = GetMember(area, "kindTouch");
                int kindId;
                try { kindId = Convert.ToInt32(kind); }
                catch { continue; }
                if (kindId < 2 || kindId > 7) continue;
                if (ActionUses == null) return true;
                try {
                    object id = GetMember(area, "idUse");
                    IDictionary dict = ActionUses.GetValue(hand) as IDictionary;
                    if (dict == null || id == null || dict.Contains(id))
                        return true;
                } catch { return true; }
            }
            return false;
        }

        internal void Stop(HSceneProc scene)
        {
            if (ownedHand != null && ownedFlags != null && ReadAction(ownedHand) == 1)
            {
                // If the secondary hand owns a drag, resetting global HFlag
                // would interrupt it. Only release our own HandCtrl.action.
                bool secondaryBusy = scene != null && ReadAction(scene.hand1) != 0;
                WriteAction(ownedHand, 0);
                if (!secondaryBusy) ownedFlags.FinishDrag();
            }
            ownedHand = null;
            ownedFlags = null;
            remaining = 0f;
        }

        internal bool Tick(HSceneProc scene, HFlag flags, float speed, float dt)
        {
            if (scene == null || flags == null || scene.hand == null ||
                !HSControlActionRules.IsAutoCaressState((int)flags.mode,
                    flags.nowAnimStateName, flags.rely))
            {
                Stop(scene);
                return false;
            }

            HandCtrl hand = scene.hand;
            if (ownedHand != null && (!ReferenceEquals(ownedHand, hand) ||
                !ReferenceEquals(ownedFlags, flags) || ReadAction(hand) != 1))
                Stop(scene);

            // Native player input has priority. Neither hand may be
            // overwritten to manufacture a fake automatic caress.
            if (ReadAction(scene.hand1) != 0) {
                // Release only our primary-hand action when the second hand
                // becomes manually controlled; do not clear global drag.
                if (Running) Stop(scene);
                return false;
            }
            if (!Running && ReadAction(hand) != 0) return false;

            if (!Running && HasNativeCaressArea(hand) && flags.DragStart())
            {
                if (!WriteAction(hand, 1)) flags.FinishDrag();
                else {
                    ownedHand = hand;
                    ownedFlags = flags;
                    remaining = UnityEngine.Random.Range(.7f, 3f);
                }
            }

            // The installed HCtrl's IL invokes FemaleGaugeUp on every Aibu
            // auto-speed frame, even if no eligible area started a new drag.
            // Guard both manual hands, not the automatic speed contribution.
            // Original HCtrl's essential missing path:
            // HFlag.speed -> FemaleGaugeUp(speed * 5 * rateDragGauge, false,
            //   hand.nowMES.stateMotion == 0).
            flags.speed = Mathf.Clamp01(speed);
            if (hand.nowMES != null)
                flags.FemaleGaugeUp(flags.speed * 5f * flags.rateDragGauge,
                    false, hand.nowMES.stateMotion == 0);

            if (Running) {
                remaining -= Mathf.Clamp(dt, 0f, .2f);
                if (remaining <= 0f) Stop(scene);
            }
            return true;
        }
    }
}
