using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MultiTeamH {
    internal static class DiagnosticProbe {
        private static ManualLogSource log;
        private static readonly Dictionary<int, float> LastAibuLog = new Dictionary<int, float>();
        private static readonly HashSet<string> Once = new HashSet<string>();

        internal static void Initialize(ManualLogSource logger) { log = logger; }
        internal static long Start() { return Stopwatch.GetTimestamp(); }
        internal static double ElapsedMs(long start) { return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; }
        internal static void Info(string message) { if (log != null) log.LogInfo("[probe] " + message); }
        internal static void Warning(string message) { if (log != null) log.LogWarning("[probe] " + message); }
        internal static void Error(string message) { if (log != null) log.LogError("[probe] " + message); }
        internal static void OnceInfo(string key, string message) { if (Once.Add(key)) Info(message); }

        internal static void AibuMiss(HandCtrl hand, ChaControl female, HandCtrl.AibuColliderKind repaired) {
            if (hand == null || female == null || female.gameObject.name != "MultiTeamH_Female2") return;
            float now = Time.realtimeSinceStartup;
            float last;
            int id = hand.GetInstanceID();
            if (LastAibuLog.TryGetValue(id, out last) && now - last < 0.75f) return;
            LastAibuLog[id] = now;
            Camera camera = hand.flags == null || hand.flags.ctrlCamera == null ? null : hand.flags.ctrlCamera.thisCmaera;
            if (camera == null) { Warning("aibu-miss camera=null"); return; }
            bool[] areas = hand.nowMES == null ? null : hand.nowMES.isTouchAreas;
            string areaText = areas == null ? "null" : string.Join("", Array.ConvertAll(areas, delegate(bool value) { return value ? "1" : "0"; }));
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, float.PositiveInfinity, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, delegate(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); });
            List<string> hitText = new List<string>();
            for (int i = 0; i < hits.Length && i < 12; i++) {
                Collider c = hits[i].collider;
                hitText.Add(c.name + "{tag=" + SafeTag(c) + ",layer=" + c.gameObject.layer + ",trigger=" + c.isTrigger +
                    ",enabled=" + c.enabled + ",child=" + c.transform.IsChildOf(female.transform) + ",d=" + hits[i].distance.ToString("0.000") + "}");
            }
            Info("aibu-miss mouse=" + Input.mousePosition + " repaired=" + repaired + " isSelect=" + hand.flags.isAibuSelect +
                " nowAnim=" + hand.flags.nowAnimStateName + " areas=" + areaText + " hits=" + hits.Length + " [" + string.Join(";", hitText.ToArray()) + "]");
            OnceInfo("hand-fields-" + id, DescribeHandFields(hand));
        }

        internal static void AibuGate(HandCtrl hand, ChaControl female, string reason) {
            if (hand == null || female == null || female.gameObject.name != "MultiTeamH_Female2") return;
            float now = Time.realtimeSinceStartup;
            float last;
            int id = hand.GetInstanceID();
            if (LastAibuLog.TryGetValue(id, out last) && now - last < 0.75f) return;
            LastAibuLog[id] = now;
            Info("aibu-gated reason=" + reason + " mouse=" + Input.mousePosition + " eventSystem=" +
                (UnityEngine.EventSystems.EventSystem.current == null ? "null" : UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null ? "no-selection" : UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name));
        }

        private static string DescribeHandFields(HandCtrl hand) {
            List<string> values = new List<string>();
            FieldInfo[] fields = hand.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++) {
                string name = fields[i].Name.ToLowerInvariant();
                if (!name.Contains("coll") && !name.Contains("hit") && !name.Contains("mouse") && !name.Contains("area")) continue;
                object value = null;
                try { value = fields[i].GetValue(hand); } catch { }
                Array array = value as Array;
                values.Add(fields[i].Name + "=" + (array == null ? (value == null ? "null" : value.ToString()) : array.Length.ToString()));
            }
            return "hand-fields " + string.Join(",", values.ToArray());
        }

        private static string SafeTag(Collider collider) {
            try { return collider.tag; } catch { return "<invalid>"; }
        }
    }
}
