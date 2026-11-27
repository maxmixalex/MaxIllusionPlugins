# LoveJuice — 0.3.7 (saliva strings and native Studio asset diagnosis)

Unlike the 0.1 scaffold, this version executes native Studio asset visuals in active Koikatsu / Koikatsu Party scenes. Studio dripping effects borrow the **materials** of installed Studio item prefabs without invoking the original continuous emitters. Kissing saliva is different: it reuses the installed CyuNoVR `BeroIto` thread when present, or renders a reversible Bézier `LineRenderer` ribbon with an available native material/fallback. No game asset is packaged.

## 0.3.7 — stop shipping unverified drops as finished native effects

- Saliva strings now support kissing, oral scenes, ongoing mouth-drool/face state and an explicit F9 saliva preview; optional non-kissing saliva is enabled by default in LoveJuice's **own** settings, never JuiceControl.
- The previous `PrimitiveType.Sphere` + `TrailRenderer` approximation is **disabled by default** behind `Experimental / Enable unverified material spheres`. It is not verified original Studio liquid geometry and should not be presented as a completed effect.
- A new `检查实际 Studio 素材` command does a read-only survey at runtime on the user's installed assets (`CommonLib.LoadAsset`). It logs exact prefab, component (ParticleSystem, MeshRenderer, LineRenderer, TrailRenderer), material and shader details, without modifying or instantiating borrowed templates. The UI summarizes availability and counts.
- During this development round the smaller Studio info bundles and the locally installed MaleJuice plugin were inspected, but Bridge's transfer of `abdata/studio/info/00.unity3d` and the main `abdata/studio/00.unity3d` was blocked by unavailable Google Drive file delivery. The in-game inspector is necessary before selecting the final native visual implementation.
- This version is **not** a completed native droplet release; other non-saliva effects will not spawn unverified spheres unless the explicit experimental switch is enabled.

## 0.3.5 — replace continuous Studio spray with small viscous drops

- The pooled renderer no longer calls `ParticleSystem.Emit`, `LiquidShower` or an entire looping Studio spray prefab. Instead it clones a **material** from the installed Studio item and renders discrete small spherical drops with short `TrailRenderer` strings, updated by gravity, drag, collision and lifetime. All instances are scene-local, bounded by the configured drop limit and destroyed or recycled cleanly.
- Body, intimate fluid, male pre-fluid, milk and special sources use distinct model-relative origins and gentle launch velocities, not a common shower jet. Optional shower scenes now use individual water drops rather than `LiquidShower` particles.
- Saliva now checks the installed CyuVR/CyuNoVR `IsKiss` and `kissing` states and, when available, reads that mod's actual female performer and mouth target. The original `BeroIto` renderer takes precedence over our fallback; it is never taken over.
- Shader/material fidelity, droplet geometry and actor contact under imported animations need **real in-game inspection**. CI cannot certify appearance.

## 0.3.4 — CyuNoVR-compatible kissing filaments and isolation

- The installed `BepInEx/plugins/Sabakan/CyuNoVR.dll` is the existing French-kiss plugin. Saliva now uses a kiss-only, dynamically tapered and gravity-bent thread rather than treating `LiquidSio` as a miniature shower. If the original `BeroIto` `LineRenderer` exists, LoveJuice does not duplicate, delete, or recolor it. When absent, LoveJuice creates one scene-local fallback thread between the active actors' mouth anchors, and destroys its material and GameObject on scene exit.
- No saliva droplets are automatically emitted from an increased face stain level; the **Face** switch now means **kissing thread**. F9 with the saliva preview channel displays a message explaining it cannot produce a realistic standalone kiss.
- The external readback API remains optional, but JuiceControl has **no embedded LoveJuice controls, bridge, hard dependency, or UI modifications**. Configure LoveJuice only in its own Ctrl+F9 panel.
- Runtime acceptance still required: real CyuNoVR H-scene lipstick/tongue contact, male/female mouth point accuracy, close/open mouth transition, multi-actor behaviour, and shader consistency. Static CI is not a game visual check.

## Native asset ownership fix

Studio asset-bundle prefabs are borrowed references. LoveJuice now clones the verified liquid **material** into the scene's droplet pool; it does not reparent or mutate the borrowed template. This avoids mutating or destroying an asset shared with Studio or another mod. Partially initialized emitters and their material copies are cleaned up on failure.

## 0.3.3: opt-in male pre-fluid

An independent male source uses the actual `cm_J_dan100`-family anatomical bone and the native transparent Studio water emitter. It is **off by default**. Periodic droplets are conditionally allowed only for the current primary male performer while a loop animation is active and the male gauge reaches the user-selected threshold (45 by default). This is explicitly a gauge-based proxy rather than a dedicated pre-fluid animation event; ambiguous second-male scenes are not auto-triggered. A missing bone silently skips the channel instead of emitting from an unrelated body part.

## 0.3.2: independent intimate-fluid source

The optional SkinEffects `PussyJuiceBody` level drives its **own** switchable native Studio liquid emitter with pelvis-relative positioning, separate from sweat/body overlays. This source is restricted to active female characters. A missing optional plugin or unresolved pelvis bone does not produce invented drips or substitute an unrelated torso anchor. It is configurable in the independent LoveJuice panel and offers an explicit F9 preview channel. The emitter uses the same original Studio `LiquidSiru` asset (borrowed, instantiated and pooled) rather than copying game assets.

## Native source assets

Inspected the original game's `abdata/studio/info/00.unity3d` in the authorized Koikatsu installation. This inventory identifies bundle `studio/00.unity3d` and manifest `studio00` with the following exact names:

| Channel | Installed Studio prefab |
| --- | --- |
| Water / preview | `p_koi_syu_LiquidWater_00_00` |
| Saliva/light liquid | `p_koi_syu_LiquidSio_00_00` |
| Male pre-fluid (primary male source) | `p_koi_syu_LiquidWater_00_00` |
| Intimate fluid (pelvis source) | `p_koi_syu_LiquidSiru_00_00` |
| Body liquid | `p_koi_syu_LiquidSiru_00_00` |
| Shower | `p_koi_syu_LiquidShower_01_00` |
| Milk (optional, white-tinted only if supported by the native prefab) | `p_koi_syu_LiquidWater_00_00` |
| Marked geometry (optional) | `p_koi_syu_LiquidKancho_00_00` |

Runtime loads through the *same native* `CommonLib.LoadAsset<GameObject>(bundle, name, true, manifest)` API used by the game's Studio `AddObjectItem`, then pools the GameObjects.

**Important native asset inspection:** the installed `studio/00.unity3d` contains a Unity 5.6.2 `ParticleSystem` (class ID 198) and `ParticleSystemRenderer` (class ID 199) on each of these prefabs. All six examined liquid systems have `loop=true` and `playOnAwake=true`. Version 0.2.1 accordingly disables native loop flags, zeroes burst/time/distance auto-emission while keeping the EmissionModule enabled for explicit Emit, and calls `ParticleSystem.Emit(EmitParams, 1)` on a small pooled set of the **original emitters**; it no longer clones the complete stream once per drop. Unity's native particle physics retains the authored renderer and performs simulation/collision. No original assets are copied into the source tree or installer.

## 0.2.2: legacy SkinEffects integration

The installed `KK_SkinEffects.dll` exposes `DroolLevel`, `SalivaLevel`, and `SweatLevel` properties, not `SkinEffectKind`/`GetEffectLevel`. LoveJuice now reads **both** the legacy properties and newer enum-based API, with read failures distinguished from level 0. The optional SkinEffects BepInEx load-order dependency is declared, while no plugin DLL is shipped. No external level values are modified.

## 0.3.0 integrated in-game controls

**Ctrl+F9** opens the real MaximumUI control panel (normal **F9** still previews particles). It exposes enable/disable, all seven effect sources, native preview channel, gravity, air damping, emission interval, lifetime, scale, launch velocity, collision, maximum active particles and the milk trigger. The UI shows scene actor count, live particle count and SkinEffects connection status. It also works when the effect is disabled, so installation does not require editing configuration files to turn it on. The integrated installer includes the matching `MaximumUI.dll`. The window is included in headless render and layout tests, but its Unity input is not represented as a game E2E result. The preview target selector lists the **actual currently active H characters** rather than taking the first dictionary item; its selection remains attached to the same ChaControl instance across roster reordering, and is cleared when that actor leaves. Milk emission is explicitly limited to female characters even when the scene-wide gauge rises, avoiding accidental male torso emission.

## Working runtime behavior

- `General/Enabled` defaults to **false**. Toggle it to true to enable actual Studio liquid visuals. Default `F9` emits four native particles in the selected preview channel (water by default) from the current scene character; it is an active runtime test, not a mock image.
- When `HSceneProc` is active, the plugin tracks **the current HSceneProc heroine roster and the separate native male performer**, including multi-character scenes, and resolves per-character bones when the roster changes and rebinds them after asynchronous head/body rig initialization or replacement. Unresolved optional anchors are rechecked on a bounded interval. It never modifies a character card.
- Body and intimate emissions follow the read-only native/SkinEffects levels; rising values create bounded bursts and periodic drips. Face levels are still observed, but never used as a cue to spray water from the mouth: saliva threads require a native kiss signal and close mouth anchors. No other plugin's UI, presets, or effect levels are modified.
- FemboyMode integration calls its public `IsFemboy(ChaControl)` via an optional runtime adapter. The separately switchable marked-geometry source resolves the matching game bone if present.
- Separate optional milk/shower channels (disabled by default). Milk uses an independent female-gauge threshold during native loop states and reuses the water prefab with a white particle start color and owned material copies for `_Color` / legacy `_TintColor` shaders. A public `LoveJuicePlugin.Emit(character, channel, count)` API supports other mods.
- Real world-space liquid motion: gravity, damping, source direction, native `ParticleSystem` collision, lifetime, hard emitted-particle limit, and no cross-scene effect leaks. Same-frame reservations enforce the global particle cap even when Unity's native `particleCount` readback has not caught up with just-issued `Emit()` calls. Only one reusable native emitter is instantiated per visual channel; material copies for the optional milk tint are created once and explicitly disposed.
- Missing assets produce a warning and disable only that visual channel, rather than falling back to generated effects. `Enabled=false` tears down the active pool.

## 0.3.1: JuiceControl interface and MMF roster

JuiceControl can toggle the master effect and five independent sources and open the full settings window via an explicit optional interface. The public `TryGetSetting` / `TrySetSetting` / `ShowControlPanel` API accepts only six whitelisted keys. The active roster reads public or private `lstFemale`, `male`, and optional `male1` fields so the second male in supported MMF scenes is no longer omitted.

## Configuration

`BepInEx/config/MaxMixAlex.LoveJuice.cfg`: `Enabled`; `Preview key`; per-channel Face/Body/Milk/Shower/Marked-geometry toggles; max active particles (default 48), interval (default 0.26 sec), preview channel, milk trigger gauge, collision, gravity, damping, lifetime, visual prefab scale and launch speed.

## Verification boundaries

The C# compilation and deterministic trajectory/catalog tests check source compatibility and safety under missing data. **The exact game's Studio prefab may require further runtime adjustment of visual scale, material compatibility and attachment bones**; no Koikatsu graphical E2E was run. The built-in F9 preview is intended to make this check immediately possible on the installed game. Studio itself should not load this process-filtered plugin; only its asset bundles are read in the game scene.

```sh
dotnet build src/LoveJuice/LoveJuice.csproj -c Release
dotnet run --project tests/LoveJuice.Regression/LoveJuice.Regression.csproj -c Release
```

Output: `bin/Release/LoveJuice/KK_LoveJuice.dll`. The standalone GitHub Actions workflow publishes a distributable installer. Third-party game assets and configuration are not distributed.
