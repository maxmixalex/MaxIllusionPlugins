using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace FlatLockBand
{
    // The accessory is an ordinary Sideloader ao_waist part. This module
    // translates FOUR native Koikatsu accessory color swatches into the
    // fabric, button, BLACKED and Queen-of-Spades layers. No F8 window.
    [BepInPlugin("MaxMixAlex.FlatLockBand", "FlatLock Native Accessory Colors", "6.0.0")]
    public sealed class FlatLockBandPlugin : BaseUnityPlugin
    {
        private float _nextScan;
        private readonly Dictionary<int, Applied> _applied = new Dictionary<int, Applied>();
        private readonly Dictionary<string, Texture2D> _atlasCache = new Dictionary<string, Texture2D>();
        private byte[] _words, _spades;
        private int _w, _h;

        private sealed class Applied
        {
            internal Renderer Renderer;
            internal Material Material;
            internal string Key;
        }

        private void Awake()
        {
            int width2, height2;
            _words = LoadMask("FlatLock_WORD_Mask.png", out _w, out _h);
            _spades = LoadMask("FlatLock_QSPADE_Mask.png", out width2, out height2);
            if (_words == null || _spades == null || width2 != _w || height2 != _h)
            {
                Logger.LogError("FlatLock: native palette masks missing/incompatible; install PNG files beside KK_FlatLockBand.dll.");
                enabled = false;
                return;
            }
            Logger.LogInfo("FlatLock v6 ready: four native Maker accessory colors; no custom editor/hotkey.");
        }

        private byte[] LoadMask(string name, out int width, out int height)
        {
            width = height = 0;
            Texture2D tex = null;
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), name);
                if (!File.Exists(path)) return null;
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
                width = tex.width;
                height = tex.height;
                var pixels = tex.GetPixels32();
                var result = new byte[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) result[i] = pixels[i].r;
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("FlatLock mask " + name + ": " + ex.Message);
                return null;
            }
            finally { if (tex != null) Destroy(tex); }
        }

        private static bool TryGetBand(ChaAccessoryComponent component, out Renderer target, out bool printed)
        {
            target = null;
            printed = false;
            if (component == null) return false;
            foreach (var skinned in component.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned == null || skinned.sharedMesh == null) continue;
                var name = skinned.sharedMesh.name;
                if (String.IsNullOrEmpty(name) || name.Length < 14 ||
                    !name.StartsWith("fb", StringComparison.OrdinalIgnoreCase)) continue;
                // Generated 14-character names terminate in P or B.
                char kind = name[13];
                if (kind != 'P' && kind != 'B') continue;
                target = skinned;
                printed = kind == 'B';
                return true;
            }
            return false;
        }

        private static string Hex(Color c)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255).ToString("X2") +
                   Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255).ToString("X2") +
                   Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255).ToString("X2");
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.25f;
            try { Scan(); }
            catch (Exception ex) { Logger.LogWarning("FlatLock scan: " + ex.Message); }
        }

        private void Scan()
        {
            var present = new HashSet<int>();
            foreach (var character in FindObjectsOfType<ChaControl>())
            {
                if (character == null || character.cusAcsCmp == null || character.nowCoordinate == null ||
                    character.nowCoordinate.accessory == null ||
                    character.nowCoordinate.accessory.parts == null) continue;
                var parts = character.nowCoordinate.accessory.parts;
                for (int slot = 0; slot < character.cusAcsCmp.Length && slot < parts.Length; slot++)
                {
                    Renderer target;
                    bool printed;
                    if (!TryGetBand(character.cusAcsCmp[slot], out target, out printed)) continue;
                    var part = parts[slot];
                    if (part == null || part.color == null || part.color.Length < 4) continue;
                    var palette = part.color;
                    int id = target.GetInstanceID();
                    present.Add(id);
                    string key = (printed ? "B" : "P") + Hex(palette[0]) + Hex(palette[1]) +
                                 Hex(palette[2]) + Hex(palette[3]);
                    Applied previous;
                    if (_applied.TryGetValue(id, out previous) && previous.Renderer == target && previous.Key == key)
                    {
                        // The game reapplies native material tints when the user changes a color.
                        if (previous.Material != null) ResetMaterialTint(previous.Material);
                        continue;
                    }
                    var mat = target.material;
                    if (mat == null) continue;
                    mat.SetTexture("_MainTex", BuildAtlas(key, palette, printed));
                    ResetMaterialTint(mat);
                    _applied[id] = new Applied { Renderer = target, Material = mat, Key = key };
                }
            }
            var stale = new List<int>();
            foreach (var pair in _applied)
                if (!present.Contains(pair.Key) || pair.Value.Renderer == null) stale.Add(pair.Key);
            foreach (var id in stale) _applied.Remove(id);
        }

        private static void ResetMaterialTint(Material mat)
        {
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Color2")) mat.SetColor("_Color2", Color.white);
            if (mat.HasProperty("_Color3")) mat.SetColor("_Color3", Color.white);
            if (mat.HasProperty("_Color4")) mat.SetColor("_Color4", Color.white);
        }

        private Texture2D BuildAtlas(string key, Color[] palette, bool printed)
        {
            Texture2D existing;
            if (_atlasCache.TryGetValue(key, out existing) && existing != null) return existing;
            var pixels = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
            {
                bool buttonStripe = y < _h * 85 / 512;
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    float weave = .955f + (x % 9 == 0 ? .027f : 0f) - (x % 13 == 0 ? .011f : 0f);
                    var c = buttonStripe
                        ? palette[1] * (.87f + Mathf.Sin(x * .05f) * .025f)
                        : palette[0] * weave;
                    if (printed)
                    {
                        c = Color.Lerp(c, palette[2], _words[i] / 255f);
                        c = Color.Lerp(c, palette[3], _spades[i] / 255f);
                    }
                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), 255);
                }
            }
            var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            tex.name = "FlatLockNativeMaker_" + key;
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            _atlasCache[key] = tex;
            if (_atlasCache.Count > 24)
            {
                var names = new List<string>(_atlasCache.Keys);
                for (int j = 0; j < names.Count - 16; j++)
                {
                    if (names[j] == key) continue;
                    if (_atlasCache[names[j]] != null) Destroy(_atlasCache[names[j]]);
                    _atlasCache.Remove(names[j]);
                }
            }
            return tex;
        }

        private void OnDestroy()
        {
            foreach (var texture in _atlasCache.Values)
                if (texture != null) Destroy(texture);
            _atlasCache.Clear();
            _applied.Clear();
        }
    }
}
