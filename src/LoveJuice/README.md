# LoveJuice — 0.2.1 (Native Studio liquids)

Unlike the 0.1 scaffold, this version executes native Studio asset visuals in active Koikatsu / Koikatsu Party scenes. All rendering instances are **clones of installed Studio item prefabs**, not externally sourced particles, generated PNGs, or substituted shaders.

## Native source assets

Inspected the original game's `abdata/studio/info/00.unity3d` in the authorized Koikatsu installation. This inventory identifies bundle `studio/00.unity3d` and manifest `studio00` with the following exact names:

| Channel | Installed Studio prefab |
| --- | --- |
| Water / preview | `p_koi_syu_LiquidWater_00_00` |
| Saliva/light liquid | `p_koi_syu_LiquidSio_00_00` |
| Body liquid | `p_koi_syu_LiquidSiru_00_00` |
| Shower | `p_koi_syu_LiquidShower_01_00` |
| Milk (optional, white-tinted only if supported by the native prefab) | `p_koi_syu_LiquidWater_00_00` |
| Marked geometry (optional) | `p_koi_syu_LiquidKancho_00_00` |

Runtime loads through the *same native* `CommonLib.LoadAsset<GameObject>(bundle, name, true, manifest)` API used by the game's Studio `AddObjectItem`, then pools the GameObjects.

**Important native asset inspection:** the installed `studio/00.unity3d` contains a Unity 5.6.2 `ParticleSystem` (class ID 198) and `ParticleSystemRenderer` (class ID 199) on each of these prefabs. All six examined liquid systems have `loop=true` and `playOnAwake=true`. Version 0.2.1 accordingly disables automatic loops before activation and calls `ParticleSystem.Emit(EmitParams, 1)` on a small pooled set of the **original emitters**; it no longer clones the complete stream once per drop. Unity's native particle physics retains the authored renderer and performs simulation/collision. No original assets are copied into the source tree or installer.

## Working runtime behavior

- `General/Enabled` defaults to **false**. Toggle it to true to enable actual Studio liquid visuals. Default `F9` emits four native particles in the selected preview channel (water by default) from the current scene character; it is an active runtime test, not a mock image.
- When `HSceneProc` is active, the plugin tracks **its actual current `lstFemale` character roster**, including multi-character scenes, and resolves per-character bones only when the roster changes. It never modifies a character card.
- Face and body emission follows the current built-in `ChaControl.GetSiruFlags` and optional SkinEffects controller values, including changes made live through **JuiceControl**. This integration reads current values on the fly; neither optional mod is modified or required.
- FemboyMode integration calls its public `IsFemboy(ChaControl)` via an optional runtime adapter. The separately switchable marked-geometry source resolves the matching game bone if present.
- Separate optional milk/shower channels (disabled by default). Milk uses an independent female-gauge threshold during native loop states and reuses the water prefab with an owned, white-tinted material copy where the shader supports `_Color`. A public `LoveJuicePlugin.Emit(character, channel, count)` API supports other mods.
- Real world-space liquid motion: gravity, damping, source direction, native `ParticleSystem` collision, lifetime, hard emitted-particle limit, and no cross-scene effect leaks. Only one reusable native emitter is instantiated per visual channel; material copies for the optional milk tint are created once and explicitly disposed.
- Missing assets produce a warning and disable only that visual channel, rather than falling back to generated effects. `Enabled=false` tears down the active pool.

## Configuration

`BepInEx/config/MaxMixAlex.LoveJuice.cfg`: `Enabled`; `Preview key`; per-channel Face/Body/Milk/Shower/Marked-geometry toggles; max active particles (default 48), interval (default 0.26 sec), preview channel, milk trigger gauge, collision, gravity, damping, lifetime, visual prefab scale and launch speed.

## Verification boundaries

The C# compilation and deterministic trajectory/catalog tests check source compatibility and safety under missing data. **The exact game's Studio prefab may require further runtime adjustment of visual scale, material compatibility and attachment bones**; no Koikatsu graphical E2E was run. The built-in F9 preview is intended to make this check immediately possible on the installed game. Studio itself should not load this process-filtered plugin; only its asset bundles are read in the game scene.

```sh
dotnet build src/LoveJuice/LoveJuice.csproj -c Release
dotnet run --project tests/LoveJuice.Regression/LoveJuice.Regression.csproj -c Release
```

Output: `bin/Release/LoveJuice/KK_LoveJuice.dll`. The standalone GitHub Actions workflow publishes a distributable installer. Third-party game assets and configuration are not distributed.
