# LoveJuice 0.4.0 — Free H fluid effects

LoveJuice was requested as an independent, scene-driven liquid effect for Free H: mobile saliva strands and falling body/intimate/pre-fluid/milk effects rather than static textures, a shower spray, or a second JuiceControl editor. CyuNoVR is a **visual reference only**. LoveJuice does not load it, inspect its types, or depend on its assets. The existing KK_JuiceControl UI, saved presets and settings must remain untouched.

**Status:** Compiles and has managed unit/UI tests. The visual quality and interaction with the user's installed Koikatsu H scenes are **not** yet confirmed by game E2E testing. Do not describe this as a finished realistic fluid simulation. Current default rendering uses the game's native MetaballShoot/UpdateMeta for non-saliva channels; where that native source is unavailable, the feature should report the limitation instead of silently showing the obsolete textured sphere effect.

## Quick start

1. Install LoveJuice and the matching MaximumUI library; do **not** install CyuNoVR on its behalf and do **not** alter JuiceControl. The two can coexist, but LoveJuice does not call CyuNoVR.
2. Enter an active Free H scene. Press **Ctrl+F9** to open LoveJuice, and check **启用 LoveJuice**. Existing installations may have it off; the default is off until explicitly enabled.
3. The main page shows the **actual automatic conditions** of each effect. Keep the panel closed while playing; it is a configuration/diagnostics panel, not a control that must be held open. For an immediate test, choose a real scene actor and channel in the bottom preview row, then press **测试** (or F9).
4. Expand **高级** only for non-native proxy sources, performance, visual tests and read-only Studio asset audit. The historical sphere/trail approximation remains an experimental OFF-by-default option.

## What triggers what (current code, not unverified game behavior)

| Channel | When enabled, automatic trigger | Source and limitations |
| --- | --- | --- |
| Saliva | A native kiss signal, or (if “其他口部场景也允许” is checked) an oral H action or a nonzero facial drool state; F9 saliva preview also works | Builds a separate 3D dynamically updated tube and gravity-falling tear meshes from the current mouth, **not** a CyuNoVR LineRenderer or particle/shower texture. |
| Body | When native `GetSiruFlags(SiruFrontUp/SiruFrontDown)` or optional SkinEffects `WetBody` becomes/continues nonzero | Emits an immediate bounded burst on a positive level change, then periodic native metaball drops while level remains > 0. This is **not** the same as intimate fluid. |
| Intimate | Optional SkinEffects `PussyJuiceBody` returns > 0 on an active female actor | A separate pelvic source; no SkinEffects data means **no automatic intimate event**, not a guessed substitute. |
| Male pre-fluid (optional) | An active loop, the primary male actor and male gauge >= configured threshold (default 45) | **Gauge proxy**, not an actual pre-fluid animation/event. Disabled by default; its inferred trigger may be inappropriate for some animations. |
| Milk (optional, Advanced) | An active loop and female gauge >= configured threshold (default 65) | **Gauge proxy**, not an anatomical lactation event. Disabled by default. |
| Shower (optional, Advanced) | The native animation name contains `shower` or `シャワー` | Optional scene-specific proxy, **not** a universal water simulation. Disabled by default. |
| FemboyMode (optional, Advanced) | FemboyMode reports a marked actor and the body liquid level changes/remains nonzero | Optional read-only integration; FemboyMode itself is not required for ordinary LoveJuice behavior. Disabled by default. |

Each non-saliva source uses the current live character's anatomy-related anchor and a separate native 3D metaball source. Do not treat a material, source name or a successful launch call as proof of good rendering. Unsupported pose/seed/prefab availability should be visible in the BepInEx log, not replaced with a fabricated shower or random particle.

The selected **测试角色 / 来源** pair controls **manual preview only**. It is not a target lock for automatic emissions, which inspect the live scene roster. Preview actions do not modify JuiceControl presets or game `Siru` levels. Ctrl+F9 opens/closes panel; F9 previews the selected source.

## What the panel does—and does not do

The top-level enable flag gates everything. Regular source checkboxes select **which** conditional effects can occur, not continuous emitters. Status labels report the scene and optional data sources. **高级** groups the extra proxy sources, gauge thresholds, source interval, native drop budget and native diagnostic inspection. Gravity, damping, scale, collision and lifetime sliders labelled **实验** only affect the **obsolete sphere renderer** when explicitly enabled; they do **not** tune the game's native Metaball seed or Koikatsu physics.

The **只读检查本机 Studio 液体资源** button examines installed native liquid asset components and material/shader names, without modifying the borrowed resource. The current non-saliva renderer uses the native Metaball pipeline, not the looping Studio `LiquidShower` prefab.

## Known limits / acceptance criteria

- Runtime conditions can depend on which optional SkinEffects version is installed and whether a Free H animation creates a compatible native Metaball host. A configured checkbox does not guarantee a visible result in every scene.
- Verify independent UI opening, native Source/H scene availability, actual 3D drop dynamics, collisions and attachment during character rotation and recline, 1- and multi-actor scenes, and repeated scene teardown/reentry.
- Saliva must visibly stretch, neck, separate and fall, with no duplicated CyuNoVR line and without a Cyu DLL. It must continue to work with CyuNoVR absent.
- Do not change JuiceControl UI or couple either plugin to the other's runtime state. Only bug fixes in JuiceControl are within scope.

This README describes the **implemented current behavior**, not a claim that all user-requested final visual fidelity has been achieved.
