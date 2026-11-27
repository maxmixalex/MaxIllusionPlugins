using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LoveJuice
{
    /// <summary>
    /// Read-only, in-game survey of the ACTUALLY INSTALLED Studio assets.
    /// This intentionally does not create any particle, modify material,
    /// activate a prefab or edit another plugin. Logs exact component,
    /// material and shader information before choosing a native rendering
    /// strategy. Never claim the bundle was verified if it cannot load.
    /// </summary>
    internal static class StudioAssetAudit
    {
        internal static string Inspect(Action<string> log)
        {
            var channels = new[] {
                LoveJuiceChannel.Saliva, LoveJuiceChannel.Body,
                LoveJuiceChannel.Intimate, LoveJuiceChannel.MalePre,
                LoveJuiceChannel.Milk, LoveJuiceChannel.Special,
                LoveJuiceChannel.Shower
            };
            var visited = new HashSet<string>();
            int loaded = 0, missing = 0, particleSystems = 0,
                meshRenderers = 0, trails = 0, lines = 0;
            foreach (LoveJuiceChannel channel in channels)
            {
                string name = StudioLiquidCatalog.Prefab(channel);
                if (string.IsNullOrEmpty(name) || !visited.Add(name))
                    continue;
                GameObject original = null;
                try {
                    original = CommonLib.LoadAsset<GameObject>(
                        StudioLiquidCatalog.Bundle, name, true,
                        StudioLiquidCatalog.Manifest);
                    if (original == null)
                        original = CommonLib.LoadAsset<GameObject>(
                            StudioLiquidCatalog.Bundle, name, true, string.Empty);
                } catch (Exception ex) {
                    if (log != null) log("Studio native asset lookup failed " +
                        name + ": " + ex.Message);
                }
                if (original == null) {
                    missing++;
                    if (log != null) log("Studio native asset MISSING: " + name);
                    continue;
                }

                loaded++;
                var info = new StringBuilder();
                info.Append("Studio native asset ").Append(name).Append(": ");
                Component[] components = original.GetComponentsInChildren<Component>(true);
                if (components != null)
                    foreach (Component c in components) {
                        if (c == null) continue;
                        ParticleSystem particle = c as ParticleSystem;
                        ParticleSystemRenderer pRenderer = c as ParticleSystemRenderer;
                        SkinnedMeshRenderer skinned = c as SkinnedMeshRenderer;
                        MeshRenderer mesh = c as MeshRenderer;
                        LineRenderer line = c as LineRenderer;
                        TrailRenderer trail = c as TrailRenderer;
                        if (particle != null) {
                            particleSystems++;
                            var main = particle.main;
                            info.Append(" ParticleSystem[loop=")
                                .Append(main.loop).Append(",max=")
                                .Append(main.maxParticles).Append("]");
                        } else if (pRenderer != null) {
                            info.Append(" ParticleRenderer[mode=")
                                .Append(pRenderer.renderMode).Append("]");
                        } else if (skinned != null) {
                            meshRenderers++;
                            info.Append(" SkinnedMesh[")
                                .Append(skinned.sharedMesh == null ? "none" :
                                    skinned.sharedMesh.name).Append("]");
                        } else if (mesh != null) {
                            meshRenderers++;
                            info.Append(" MeshRenderer[")
                                .Append(mesh.name).Append("]");
                        } else if (line != null) {
                            lines++;
                            info.Append(" LineRenderer");
                        } else if (trail != null) {
                            trails++;
                            info.Append(" TrailRenderer");
                        }
                        Renderer renderer = c as Renderer;
                        if (renderer != null && renderer.sharedMaterials != null)
                            foreach (Material mat in renderer.sharedMaterials)
                                if (mat != null) {
                                    info.Append(" Material[")
                                        .Append(mat.name).Append("; shader=")
                                        .Append(mat.shader == null ? "(missing)" :
                                            mat.shader.name).Append("]");
                                }
                    }
                if (log != null) log(info.ToString());
            }

            string result = "Studio 资源检查：已加载 " + loaded +
                "，缺失 " + missing + "；ParticleSystem " +
                particleSystems + "，MeshRenderer " + meshRenderers +
                "，Trail " + trails + "，Line " + lines +
                "。详见 BepInEx 日志（仅诊断，未修改素材）。";
            if (log != null) log(result);
            return result;
        }
    }
}
