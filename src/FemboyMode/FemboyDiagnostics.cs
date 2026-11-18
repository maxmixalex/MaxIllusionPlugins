using System;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using ExtensibleSaveFormat;
using KKAPI.Maker;
using UnityEngine;

namespace FemboyMode
{
    /// <summary>
    /// Small on-demand inventory for diagnosing body replacements and
    /// missing animation/voice mappings. No clip/audio data is exported.
    /// No per-frame reflection: this only runs from an explicit hotkey.
    /// </summary>
    internal static class FemboyDiagnostics
    {
        private static readonly FieldInfo MaleField =
            typeof(HSceneProc).GetField("male", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void DumpCurrent()
        {
            ManualLogSource log = FemboyModePlugin.Log;
            if (log == null) return;
            log.LogInfo("[FemboyMode diagnostics] BEGIN — no character names or card paths are logged.");

            try
            {
                if (MakerAPI.InsideAndLoaded)
                {
                    log.LogInfo("[FemboyMode diagnostics] Context: female Maker");
                    DumpCharacter(log, MakerAPI.GetCharacterControl(), "Maker");
                }

                HSceneProc[] scenes = UnityEngine.Object.FindObjectsOfType<HSceneProc>();
                if (scenes.Length == 0 && !MakerAPI.InsideAndLoaded)
                    log.LogInfo("[FemboyMode diagnostics] Neither Maker nor vanilla HSceneProc is active.");

                for (int i = 0; i < scenes.Length; i++)
                {
                    HSceneProc proc = scenes[i];
                    if (proc == null || proc.flags == null) continue;
                    HFlag flags = proc.flags;
                    log.LogInfo("[FemboyMode diagnostics] HScene #" + i +
                        ": mode=" + flags.mode + " freeH=" + flags.isFreeH +
                        " animState=" + flags.nowAnimStateName +
                        " finish=" + flags.finish + " isAnalPlay=" + flags.isAnalPlay +
                        " heroineCount=" + (flags.lstHeroine == null ? 0 : flags.lstHeroine.Count));

                    if (flags.lstHeroine != null)
                        for (int index = 0; index < flags.lstHeroine.Count; index++)
                        {
                            var heroine = flags.lstHeroine[index];
                            DumpCharacter(log, heroine == null ? null : heroine.chaCtrl,
                                "HScene heroine " + index);
                        }

                    if (MaleField != null)
                        DumpCharacter(log, MaleField.GetValue(proc) as ChaControl, "HScene male");

                    log.LogInfo("[FemboyMode diagnostics] HScene particle=" +
                        (proc.particle != null) + " secondParticle=" + (proc.particle1 != null) +
                        " hand=" + (proc.hand != null) + " secondHand=" + (proc.hand1 != null));

                    if (proc.voice != null && proc.voice.nowVoices != null)
                        for (int index = 0; index < proc.voice.nowVoices.Length; index++)
                        {
                            var entry = proc.voice.nowVoices[index];
                            if (entry != null)
                                log.LogInfo("[FemboyMode diagnostics] voice channel " +
                                    index + " state=" + entry.state);
                        }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("[FemboyMode diagnostics] Failed to inspect a scene: " + e);
            }
            finally
            {
                log.LogInfo("[FemboyMode diagnostics] END.");
            }
        }

        private static void DumpCharacter(ManualLogSource log, ChaControl c, string label)
        {
            if (c == null)
            {
                log.LogInfo("[FemboyMode diagnostics] " + label + ": no ChaControl");
                return;
            }

            log.LogInfo("[FemboyMode diagnostics] " + label +
                ": sex=" + c.sex + " marked=" + FemboyModePlugin.IsFemboy(c) +
                " objBodyBone=" + (c.objBodyBone != null) +
                " animBody=" + (c.animBody != null));

            if (c.chaFile != null)
            {
                PluginData selector = ExtendedSave.GetExtendedDataById(
                    c.chaFile, "com.deathweasel.bepinex.uncensorselector");
                if (selector != null && selector.data != null)
                {
                    // Only plugin asset GUIDs and booleans, never personal names/paths.
                    foreach (string key in new[]
                    {
                        "BodyGUID", "PenisGUID", "BallsGUID",
                        "DisplayPenis", "DisplayBalls"
                    })
                    {
                        object value;
                        if (selector.data.TryGetValue(key, out value))
                            log.LogInfo("[FemboyMode diagnostics] " + label +
                                " selector." + key + "=" + value);
                    }
                }
            }

            if (c.objBodyBone != null)
            {
                Transform[] bones = c.objBodyBone.GetComponentsInChildren<Transform>(true);
                log.LogInfo("[FemboyMode diagnostics] " + label + " boneCount=" + bones.Length);
                StringBuilder selected = new StringBuilder();
                int kept = 0;
                for (int i = 0; i < bones.Length && kept < 45; i++)
                {
                    string name = bones[i].name;
                    string lower = name.ToLowerInvariant();
                    if (!(lower.Contains("dan") || lower.Contains("penis") ||
                          lower.Contains("ball") || lower.Contains("shaft") ||
                          lower.Contains("kokan") || lower.Contains("ana") ||
                          lower.Contains("genital") || lower.Contains("chinko")))
                        continue;

                    if (kept > 0) selected.Append(", ");
                    selected.Append(name);
                    kept++;
                }
                log.LogInfo("[FemboyMode diagnostics] " + label + " candidateBones=[" +
                    selected + "]" + (kept == 45 ? " (truncated)" : ""));
                if (FemboyModePlugin.IsFemboy(c))
                {
                    LogBoneResolution(log, label, c.objBodyBone.transform,
                        "male geometry", FemboyModePlugin.MaleGeometryBone);
                    LogBoneResolution(log, label, c.objBodyBone.transform,
                        "Kokan action anchor", FemboyModePlugin.KokanAnchorBone);
                }
            }

            if (c.animBody != null && c.animBody.runtimeAnimatorController != null)
            {
                var clips = c.animBody.runtimeAnimatorController.animationClips;
                log.LogInfo("[FemboyMode diagnostics] " + label +
                    " animatorController=" + c.animBody.runtimeAnimatorController.name +
                    " clipCount=" + clips.Length);

                StringBuilder selected = new StringBuilder();
                int kept = 0;
                for (int i = 0; i < clips.Length && kept < 45; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null) continue;
                    string lower = clip.name.ToLowerInvariant();
                    if (!(lower.Contains("orgasm") || lower.Contains("finish") ||
                          lower.Contains("shoot") || lower.Contains("in_start") ||
                          lower.Contains("out_start") || lower.Contains("wfin") ||
                          lower.Contains("m_out") || lower.Contains("m_in")))
                        continue;
                    if (kept > 0) selected.Append(", ");
                    selected.Append(clip.name);
                    kept++;
                }
                log.LogInfo("[FemboyMode diagnostics] " + label +
                    " finishCandidates=[" + selected + "]" +
                    (kept == 45 ? " (truncated)" : ""));
            }
        }

        private static void LogBoneResolution(
            ManualLogSource log, string label, Transform root, string purpose,
            BepInEx.Configuration.ConfigEntry<string> nameSetting)
        {
            if (nameSetting == null) return;
            Transform resolved = FemboyRigResolver.FindBone(root, nameSetting.Value);
            if (resolved == null)
            {
                log.LogInfo("[FemboyMode diagnostics] " + label + " " + purpose +
                    ": configured=" + nameSetting.Value + " MISSING");
                return;
            }

            Transform parent = resolved.parent;
            log.LogInfo("[FemboyMode diagnostics] " + label + " " + purpose +
                ": configured=" + nameSetting.Value + " resolved=" + resolved.name +
                " parent=" + (parent == null ? "(none)" : parent.name) +
                " localPosition=" + resolved.localPosition +
                " localScale=" + resolved.localScale);
        }
    }
}