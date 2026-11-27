# Femboy Mode for Koikatsu — v0.6.7

Independent per-female-character **伪娘标志 (Femboy)** and scoped Maker / Free H extensions for original Koikatsu and Koikatsu Party. This is **not** a port of [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap), which instead marks the *male school player*. We never overwrite a female card's vanilla `sex` field.

## 0.6.7 — restrained clothing clearance and native Shift/Hang restoration

- Active fitting is restricted to native clothing state **On (0)** across relevant top/bottom/underwear slots. **Shift (1)**, **Hang (2)** and **Off (3)** immediately restore the original mesh: adjusted panty bulges must never persist after underwear is moved aside.
- Maximum local relief is now 18 mm rather than 70 mm, multiplied by a new Maker `Underwear mesh correction strength` slider defaulting to 0.4 (at most 7.2 mm nominal local displacement before vertex weights). The influence radius and target depth were also reduced to avoid stretching the whole crotch panel.
- In-game screenshots of stocked, shifted, hanging, off, and modded one-piece outfits remain necessary for acceptance. No original game/third-party mesh is saved or overwritten.

## 0.6.6 — actual genital visibility and expanded garment fitting

- Physical fluid trajectories now use **`ChaFileStatus.visibleSon`**, the live game-level genital-visibility boolean, rather than guessing visibility from underwear slot 3 or `notShorts`. This covers cases where a swimsuit, bodysuit, top/bottom or modded costume conceals the anatomy even if the separate underwear slot is missing.
- Mesh fitting now scans `ChaControl.objClothes` slots **0 (tops, including one-piece outfits), 1 (bottoms) and 3 (underwear)** if worn. It evaluates the character's current skinned garment with `SkinnedMeshRenderer.BakeMesh`, finds inadequate front clearance near the shaft, and maps a softly bounded displacement back through the dominant skinning bone. This replaces the previous invalid assumption that raw bind-pose vertices were already in the current animated position.
- Uses a scoped mesh-read mechanism compatible with optimized game meshes, borrowed from the approach in ClothingCompression, with cleanup in `finally`. Changes live on an owned clone only, automatically restoring the original source mesh on clothing change, reload or disabling. Debug logging reports which garment renderers were actually changed.
- Still experimental for heavily custom-weighted garment meshes. No claim of universal fit or actual visual correctness until representative bodysuit, swimsuit, stock underwear and modded underwear models have been inspected in the game.

## 0.6.5 — underwear-sensitive projectiles and reversible mesh clearance

- Historical 0.6.5 physical-fluid routing used the live Koikatsu underwear state `ChaControl.fileStatus.clothesState[3]` (On 0 / Shift 1 / Hang 2 / Off 3) and `notShorts` for the model absence check. A covered source uses reduced native MetaballShoot launch velocity with a locally lowered trajectory; Shift/Hang use a partially exposed profile; Off returns to the original shaft-relative parabola. Changes during emission affect subsequent droplets without changing game-owned presets.
- **Maker / Automatically fit unadapted underwear meshes** (on by default) optionally probes only the current underwear slot, estimates its front silhouette clearance relative to the male geometry bone and shaft tip, and creates a private, limited-distortion `SkinnedMeshRenderer.sharedMesh` copy if clearance is insufficient. Models already leaving room are skipped. The original mesh is restored on outfit change, feature disable, character reload and destruction. Shared AssetBundle meshes and cards are never edited.
- The automatic fit is conservative, heuristic and **not yet visually validated across different imported underwear meshes**. Disable the new Maker option if a particular model looks distorted; include garment/mod name and screenshots in a QA report.

## 0.6.4 — align scrollbar pointer target with its actual outer position

**RX18 acceptance gate:** v0.6.3 fixed external positioning and native text clipping, but the user reported that dragging works at an offset from the *visible* scrollbar. The copied scrollbar was outside `AllTop` **visually** but was still a child of `tglAll`, where its ancestor UI/Toggle raycast filters could reject input outside the tab rectangle.

- The actual cloned **native Scrollbar GameObject** now lives directly under the verified Maker `CvsMenuTree`, outside the entire `tglAll` raycast/layout/mask hierarchy. `AllTop`, its clipping mask, Femboy and vanilla rows, scroll range and native wheel sensitivity are unchanged.
- Its root **RectTransform**, used by both Unity rendering and the built-in Scrollbar hit-test, is positioned in `CvsMenuTree` *anchored coordinates*: convert the original page's rendered top-right world point into the menu's local coordinates, then subtract the correct parent-rect anchor corner. This avoids visual/input displacement due to different parent pivots, scales and layout updates. The cloned bar is placed last in menu drawing/raycast order and its track/handle native Graphics remain raycast targets.
- After the UI settles, a **one-shot real Unity EventSystem raycast** is performed at the rendered native scrollbar handle's screen center. The result, or the name of the occluding hit target, is written to the BepInEx log so a misleading purely-visual success cannot silently pass. This does not synthesize a custom input overlay or new scrollbar behavior.
- Regression assertions cover parent-anchor coordinates with noncentered rectangles. A Windows RX18 test is still required before accepting the pointer hit area; if it fails, inspect the `Maker scrollbar hit-test mismatch` log, not unrelated plugin features. The original two already-accepted checks (bar outside content, stencil text clipping) must remain passing.

## 0.6.3 — external stock scrollbar + stencil clipping for native text

**RX18 screenshot regression:** the in-place Body > All scrollbar in 0.6.2 overlapped the original content rather than standing outside the viewport, and native text/numeric values leaked into the area above and outside the clipping rectangle after scrolling.

- **Native scrollbar moved outside:** clone only the game's `SocksTop/Scroll View/Scrollbar Vertical`, with the same rendered width and source-derived viewport-to-bar gap. Parent it as a **sibling** of `AllTop` under `tglAll`, *not* as a child of the clipped page. Its world position and height track the page's viewport, but the page's own right padding remains unchanged. The original tab toggle, CanvasGroup visibility, and page enable/disable control external-bar visibility.
- **Native text clipping:** keep the existing `RectMask2D` for compatible UI graphics and install a real stencil `UnityEngine.UI.Mask` with a non-raycast `Image` on the **same original AllTop**. This clips original text and numeric fields whose shader did not honor `UnityUIClipRect`. Existing mask/image settings are preserved; any disabled `MaskableGraphic.maskable` values are enabled while scrolling and restored on uninstall. No native row or KKAPI widget changes parent.
- **Fail-open / restore:** missing viewport, missing stencil mask or moved scrollbar parent disables the scroll wrapper. The original page size, layout, mask state and graphics maskability are restored when the feature is disabled or Maker exits.
- **This is a Windows in-game acceptance blocker:** headless tests cover external gap bounds, not actual TMPro/native stencil rendering. The check must show **no text above the viewport** when scrolling and the **bar outside the content** without overlap before this issue is accepted; other plugins remain blocked.

## 0.6.2 — inherit native ScrollRect scrolling sensitivity

- Removed the manually invented `Maker / Whole-page wheel step` option entirely. The in-place Body > All scroller reads the game's `03_ClothesTop/tglSocks/SocksTop/Scroll View` `ScrollRect.scrollSensitivity` at installation, and applies incoming wheel deltas using that exact value, without hardcoded detent or speed bounds.
- The captured native sensitivity is logged together with stock scrollbar width. A former `Whole-page wheel step` entry may remain as an unused historical line in an existing BepInEx config file; the plugin no longer binds or reads it.
- The same-frame application and narrow native-slider wheel fallback from 0.6.1 remain in place, with no artificial smoothing. Managed tests now explicitly verify exact native sensitivity multiplication (including zero, fractional deltas, and large valid values), not a forced 112-unit scroll.
- This corrects a configuration design error; actual RX18 wheel responsiveness and the true Maker axis still require the user\'s blocked in-game checkpoint to pass.

## 0.6.1 — original Maker scrollbar sizing, direct wheel, actual Maker gizmo

**Acceptance blocker**: Body > All scrolling was present in 0.6.0, but the scrollbar was thinner than stock, the mouse wheel was sluggish, and the "显示轴" option still created hand-drawn RawImage/Text axes rather than using the real game gizmo.

- **Scrollbar** copies the native SocksTop Scrollbar GameObject, reading the stock scrollbar's rendered RectTransform width (and actual canvas scale) rather than forcing a 14-unit rail. The chosen width is logged on installation.
- **Wheel (in 0.6.1)** used an adjustable, synthetic 112-unit scroll step; it was replaced by native `ScrollRect.scrollSensitivity` readback in 0.6.2. A narrowly scoped raw-wheel fallback catches sliders or other controls that consume Unity pointer-scroll events. Periodic content measurement no longer resets native padding to zero and causes jumps.
- **Original Maker 3D guide**: `CustomBase.customCtrl.cmpGuid[0]` is the source of a separate `CustomUtility.CustomGuideObject` instance with the actual game `CustomGuideMove` and `CustomGuideRotation` components and original arrow/ring models. The cloned guide is **not** stored in `cmpGuid[]`, so native accessory editing remains attached to its own unchanged guide and CvsAccessory does not write Femboy edits into an accessory slot.
- **Three edit modes**: translation uses the native move arrows; rotation uses the native rotation rings; scale applies native move-arrow displacement to the card's scale parameters, since the original Maker only supplies native move and rotation guide models (not a separate Maker scale guide). No IMGUI, RawImage or custom color-line axes are drawn.
- Native guide activation requires the marked card and the actually visible Body > All tab. It is destroyed on Maker exit, does not take over the accessory guide, and writes only the FemboyCharacterController card-local values. Modifier keys remain Shift=fine, Ctrl=coarse.
- Managed tests cover stock scrollbar width fallback, immediate wheel distance, native move/rotation-to-card mapping and bounds. **These tests do not prove that the native guide receives pointer events in a live Windows game**; the acceptance checkpoint remains blocked until an actual screenshot, mouse drag and roll test passes.

## 0.6.0 — unified native Body > All scroll, without moving native controls

The user correctly rejected 0.5.9's return to a previously completed independent Femboy-only scroll. This version restores the original requirement: **native Body > All controls and all FemboyMode extensions share one scrollbar and one clipped viewport.**

Implementation: a `RectMask2D` clips the *original* `AllTop` rectangle, its existing `VerticalLayoutGroup` adjusts **top padding** to scroll all original children together, and only a native **Scrollbar artwork** child is cloned from the game's SocksTop template. The original native controls, added Femboy controls, event handlers, and direct parent-child relationships are never moved or replaced. The original `ContentSizeFitter` is temporarily disabled while using the fixed screen-relative viewport, then restored on uninstall.

- One scrollbar controls every visible native row and every additional Femboy row. Scroll-wheel and blank-area drag both alter the same clamped content offset. A mouse-wheel over native sliders scrolls the page; normal slider drag and keyboard input are preserved.
- Geometry is based on actual screen height, page location and root canvas scale, **not** a page height derived from the content itself. The scroll range is computed from the native layout's total preferred height, with bounds checks and regression tests for top/middle/bottom and unusual resolutions.
- A runtime watchdog checks that original rows retain the same parent and at least one active native row intersects the viewport; if geometry or visibility is invalid, it automatically restores the unmodified page. The existing F1 Maker scrolling toggle remains a live fail-open escape hatch.
- This design is significantly different from 0.5.4 and 0.5.9's *independent extension-only* scrolling and 0.5.6–0.5.8's *reparent-everything* clones. It must still be checked in the real Windows Maker for scrollbar appearance, text clipping, row interaction, card reload, and tab switching; CI cannot substitute for actual Unity 5.6 graphical testing.

## 0.5.9 — isolate Femboy extensions from native Maker layout

The second RX18 screenshot proved the invasive 0.5.8 wrapper could hide the *entire* Body > All content, despite logging successful installation. This release **stops moving any original Maker or third-party rows**: the owned scroller may reparent only the 24 FemboyMode geometry widgets. The persistent Femboy flag remains directly under the native Body > All parent.

- Scrollbar uses a bounded 240–620 local-unit viewport derived from the native Socks template and is explicitly kept visible even when most additional controls are inactive.
- The original Maker page's layout settings and other plugins' widgets are not modified. Incompatible or partially initialized controls cause a safe no-op; invalid viewport dimensions trigger rollback of extension rows.
- Existing F1 > Femboy Mode > Maker > `Enable Body All native scrolling` can still disable/re-enable the *owned* extension scrollbar at runtime.
- **Scope:** this is now a scrollable *FemboyMode extension section* inside the existing Body > All tab, not the former full-page native-rows scrolling. Whole-page scrolling will only be reconsidered after verifying the actual Maker hierarchy. This version needs graphical in-game validation and must not be considered E2E complete based on compilation.

## 0.5.8 Native Maker scroll layout repair

The RX18 report on 2026-11-26 showed an almost invisible scrollbar and misplaced Maker controls while the BepInEx log still said `Maker Body > All: native whole-page scroll installed.` The previous resize logic measured the content-driven `Body > All` host itself as a viewport, then subtracted its margin every LateUpdate until the host collapsed to roughly one pixel.

- Viewport sizing now considers only substantial, enabled clipping ancestors **outside** the content-sized host. Layout-driven ancestors are deliberately excluded.
- If no stable mask can be found, the wrapper uses the stock native SocksTop scroll-template height instead of repeatedly shrinking the entire Maker page. Implausibly small measured viewports are rejected.
- `Maker / Enable Body All native scrolling` (default on) is a **live emergency rollback switch** in BepInEx Configuration Manager (F1): switching it off uninstalls the wrapper and restores the original page's rows and scroll handlers without disabling FemboyMode's other features. Switching on reinstalls the wrapper when Maker is loaded.
- Regression tests explicitly reject the previous 1-pixel viewport and model 120 update frames without height erosion. Actual input/layout behavior still needs Windows Maker E2E.

## 0.5.7 Native transform handles

- Replaced the IMGUI screen-space axis drawing with an actual Unity UI canvas and EventSystem-enabled X/Y/Z drag handles. The handles are raycast targets, with lightweight non-interactive axis lines and labels; dragging updates the per-card transforms immediately and synchronizes the Maker sliders.
- Corrected screen-space Y projection to UGUI coordinates and added a guarded raw-input fallback for Maker variants without a working EventSystem. Native GUI hidden state also hides the gizmo.
- Shift enables fine movement and Ctrl enables coarse movement. Dragging is clamped to the same numeric limits as the saved model and gracefully stops if Maker, the active card or the selected mode changes. A missing configured bone is surfaced as a visible warning.
- New headless drag-math assertions cover scale/move/rotate, clamps, fine/coarse precision and invalid numbers. Real Maker pointer interaction still requires in-game testing.

## 0.5.6 Maker page layout

- The Body > All page uses a clone of the game's own `03_ClothesTop/tglSocks/SocksTop/Scroll View`, the same source used by KKAPI's subcategory creation. All existing page rows share one vertical viewport; visibility of individual rows does not hide the original page.
- The installation waits for Maker loading to finish, preserves sibling order and restores the page and original wheel handlers on uninstall or initialization failure. If the page already has a native scroll container, it is left unchanged. If a compatible template or layout is missing, no partial replacement is installed.
- Numeric row headings omit redundant unit suffixes. Stored values and numeric conversions are unchanged.
- The headless suite checks viewport height calculation. Native theme, raycasts, scroll interaction and other Maker mods still require in-game testing.

## Implemented

- **Two synchronized Maker switches:** 「伪娘角色标志 (Femboy)」in both Parameter > Character and Body > All of the female Maker.
- **Card-local state:** stores `MaxMixAlex.FemboyMode/IsFemboy` and optional male-bone X/Y/Z scaling (extended data version 2; compatible with version 1). Switching cards and state-preserving reloads are handled independently.
- **External API:** `FemboyModePlugin.IsFemboy(ChaControl)`, `IsFemboy(ChaFileControl)`, `GetController(ChaControl)`.
- **Maker asset unlock:** while a marked card is active, item-list sex filters are permitted to expose male-listed resources. Optional `KK_UncensorSelector` compatibility limits its male-body preset list availability to the marked female Maker, instead of globally enabling GenderBender. Reopen Maker after the first toggle to rebuild precomputed lists.
- **Body model controls:** X, Y, Z sliders under Body > All (0.5–2.0 scale, 1.0 neutral) are shown only when the character is marked; they act on the separately configurable `Male geometry bone` (`cm_j_dan100` by default) when it exists in the installed male geometry; individual values persist in the character card. These are **new model-specific controls**, not a transplant of all vanilla male Maker panels. Absent bones are safely ignored.
- **Free H filtering:** the two native non-anal insertion action buttons are hidden for the marked leading heroine, on **both** seven-button UI pages (normal and female-initiative). Their click handlers are guarded, without forcing unavailable positions or changing the unmarked heroine's behavior.
- **Kokan touch relocation:** action anchors are created *after* optional body objects load, immediately before the game's `HandCtrl.InitHitReactionCollider`; a configurable male-bone hit sphere is added. A `GetOnMouseAibuCollider` postfix favors this sphere over the old female-model Kokan collider and rejects the old region for marked characters. The original actor's touch-area restrictions remain intact. The fallback is `cf_d_kokan` if the configured target bone is absent.
- **Actor-local climax animation (v0.4.0):** in native two-person Free H Sonyu, only the marked heroine's finish states are intercepted. If the source male and target female both use Unity Humanoid avatars and the matching state exists, her Animator can temporarily use the male controller, restoring the original controller on the next non-finish state or scene/pose change. On Generic rigs, the fallback uses the animated male actor's `cm_J_dan100_00` to `cm_J_dan109_00` local-rotation *deltas* to move only matching male appendage bones on the marked heroine. It does **not** transfer the partner's entire skeleton, vertex shapes, voice clips, or object positions. If no matching geometry exists, it leaves native animation untouched. Both integrations have independent configuration switches; this is functional code but not yet gameplay-tested.
- **Actor-local climax voice routing (v0.4.0):** `HVoiceCtrl.VoiceProc` is guarded only for the marked female channel and native Aibu/Sonyu female climax voice codes (`142/143`, `318/319`, `325–330`). The game does not provide a separate corresponding male voice channel, so it suppresses these female-exclusive speech events instead of fabricating male voice assets. Normal dialogue and the partner's voice remain unchanged.
- **Male-style finishing visual:** on a marked heroine's climax in Free H, the native male outside-finish particle (`HParticleCtrl` particle ID 0, also used in `MetaballCtrl.Proc`) is cloned at her chosen body bone. The original Aibu squirt particle **and its associated sound** are suppressed only if the replacement effect was successfully started; otherwise vanilla effects remain. The original `HAibu.randSio` instance is captured once at construction and released when the scene closes, avoiding Unity scene scans during unrelated random calls. Effects are selected using the leading heroine's scene particle controller (never the first arbitrary HParticleCtrl in the scene). This **does not yet replace female animation/voice clips or the vanilla climax counters**; it only adds the correctly targeted male-style *visual effect*.

## RX18 reclining-trajectory and Sonyu trigger fixes (v0.4.4)

This version addresses two follow-up behaviors confirmed by the 0.4.3
in-game report and `LogOutput(2).log`.

1. **Shorter, anatomically relative launch.** The old
   `FemboyFluidAxisFollower` projected the shaft onto the world's
   horizontal X/Z plane and then added a **world-space +18°**
   elevation. Although it produced a visible parabola, this forced
   essentially the same world-up trajectory when the heroine lay on
   her back or rotated. The new follower uses the **full three-dimensional
   root-to-tip vector** from the current animated SoS bones and an
   orthonormal frame derived from the shaft root's own rotated
   up/right axes. The optional pitch is rotated in *that local frame*,
   never against world-up. The initial launch speed has been reduced
   from 2.5 to **0.85** Unity units/s, and the extra local pitch defaults
   to **0°**. These are conservative, model-relative defaults; the
   real projectile still falls naturally under gravity.
2. **Sonyu emission fallback on the verified native voice event.** The
   uploaded log recorded successful physical droplets in Aibu but no
   Sonyu emission at all: the marked heroine reached
   `A_SF_IN_Loop` and native voice code `326`, yet the emitter
   diagnostic still said `mode=aibu`. Existing HFlag counter and
   HActionBase.SetPlay callbacks were evidently insufficient in this
   installed mod combination. A new actor-specific
   `HVoiceCtrl.VoiceProc` prefix watches the *native, unconsumed*
   female Sonyu climax codes **325–330** (and the +38 female-initiative
   equivalents 363–368) and triggers the physical emitter from that
   real event. It verifies the selected heroine and active Free H scene,
   retains rate-limit and pose-reload protection, does not rewrite
   `HFlag.finish` or voice playback, and catches fluid errors so the
   game's native climax continues normally.

**Configuration migration:** In BepInEx Configuration Manager under
`Male fluid`, use the newly named **`Model-relative launch speed`**
(default **0.85**, range 0.2–4.0) and **`Local pitch relative to shaft
degrees`** (default **0**, range -30–30). The old keys `Launch speed`
and `Launch elevation degrees` are intentionally **not read** so a
saved 0.4.3 value of 2.5/18° cannot silently undo this fix. `Launch
spread degrees` and `Amount multiplier` are unchanged.

To test: use the same marked SoS character and try Aibu first, then
Sonyu (especially a lying/anal pose) in one Free H session. Compare
two different character orientations: the stream should rotate with
the actual shaft rather than maintaining a fixed horizon. The log
should include `Sonyu climax voice fallback fired` (when Verbose
logging is enabled) followed by `Physical fluid started mode=sonyu`
and ideally `First physical droplet spawned on sonyu`. If no
emission occurs, the new guarded hook or existing emitter warning
should identify whether the voice event was observed but the
projectile/preset could not initialize.

These are implemented source and CI-level fixes, **not a claim of
successful new RX18 gameplay validation**.

## RX18 screenshot + H scene log fixes (v0.4.3)

The user reported a hand hovering far away from a SoS model, the native-UI
symbol still invisible, no physical fluid in Sonyu after Aibu, and fluid
droplets falling vertically. These four issues have separate root causes:

1. **Preserve native hand animation coordinates.** Previously every
   `HandCtrl.LayerInfo.transParents` was assigned to
   `cm_J_dan100_00`. That changes not just the contact position, but
   the hand animation's parent **rotation and coordinate space**, causing
   the detached hand visible in the screenshot. Now each original hand
   action parent gets a lightweight offset proxy *under the original
   parent*. It maintains the original scale/orientation and translates
   by `new Kokan contact point - vanilla cf_d_kokan_REF` in world space.
   Each proxy is restored safely on scene exit, heroine switch or
   plugin unload. Configurable `Hand correction world X/Y/Z` is an
   additional fine-tune; the defaults are zero.
2. **Marker stays within the native UI, outside clipping masks.** The
   previous Unity Text was parented under the gauge fill's masked
   hierarchy, which can hide it completely. The updated marker is a
   Unity UI Text element in the **outermost native H Canvas**, drawn
   next to the current heroine's gauge using its screen-space corners.
   It follows the native UI scale and visibility, uses the existing
   gauge color plus a small native `Outline` for contrast, and never
   draws via IMGUI. The first successful binding is logged.
3. **Aibu -> Sonyu physical fluid emitter reload.** The old emitter
   cached an Aibu `MetaballShoot` and could continue using that
   obsolete projectile/source after `HSceneProc.ChangeAnimator`
   switched modes or poses. It now invalidates on **either mode or
   animation ID change** and builds a new current-pose shooter/seed.
   This retains the anal/regular index distinction and actor-specific
   cleanup. Ctrl+Shift+F10 now prints mode, pose, source child count
   and launch direction. The log also distinguishes an emitter being
   armed from an actual physics droplet appearing in the source.
4. **Ballistic, nonvertical launch.** Native `MetaballShoot` applies
   force along `ShootAxis.transform.forward`. The old axis simply
   inherited the genital bone rotation; on this rig that caused a
   nearly vertical path. The updated axis follows the real base-to-tip
   direction projected horizontally, with configurable upward
   elevation (default 18 degrees), speed (2.5 Unity units/second) and
   dispersion (6 degrees). Its own `MetaballShoot` uses
   `ForceMode.VelocityChange`, so Rigidbody mass cannot eliminate
   initial forward speed. Gravity and drag then create the natural
   parabolic trajectory. The projectile settings are *not* changed
   globally for the real male partner.

**New configuration:** `Male fluid / Launch speed` (0.2–8.0),
`Launch elevation degrees` (-10–60) and
`Launch spread degrees` (0–30), alongside the existing
`Amount multiplier` (0.25–5). The hand controls are
`Aibu alignment / Hand correction world X/Y/Z`.

These fixes compile and have deterministic rule tests, but the actual
hand alignment, Canvas position and Rigidbody motion must be checked in
RX18 before claiming live correctness. Preserve `LogOutput.log` if
there is a remaining mismatch.

## RX18 follow-up fixes (v0.4.2)

This release targets the **five specific follow-up observations** from the
RX18 test, using the supplied `LogOutput.log` and the game DLL/Unity list
resources.

- **Marker integrated into the original game HUD:** replaced the standalone
  IMGUI rectangle with a Unity `Text` object following the female
  `HSprite.gauge[0].imgBar` position. In v0.4.3 the element is parented
  under the outer H Canvas instead of the gauge's masked inner hierarchy,
  so it cannot be clipped away. It has no raycast target and only
  appears for the selected marked heroine. There is no `OnGUI` overlay.
- **Kokan slightly too high:** added a separate `Aibu alignment / World Y
  correction` (-0.012 Unity units by default) applied after scaled bone
  root-to-tip interpolation. The offset can be calibrated in Configuration
  Manager without editing the saved card's shape scale.
- **Fake phallus still selectable:** corrected the original resource
  identification. The base game's fifth Kokan item `p_item_vibe`
  (icon 25, IDs 7/8) **is the unwanted fake phallus**; the fourth item
  `p_item_denma_L/R` (IDs 5/6) is the vibrator to preserve.
  The earlier patch mistakenly kept the unwanted fifth item while
  searching for a nonexistent sixth item. The fixed code filters the
  exact IDs 7/8 and checks the runtime wheel again if another H-control
  mod reconstructs its list. Ctrl+Shift+F10 logs the actual Kokan rows.
- **Sonyu climax animation truncated:** fixed the critical voice-lifetime
  issue. Older releases discarded the heroine's climax speech command by
  setting `playVoices[_main] = -1`; `HSonyu` then saw
  `Manager.Voice.IsVoiceCheck == false` and advanced to the recovery
  animation prematurely. The new `HVoiceCtrl.VoiceProc` bridge preserves
  the original voice clip and its **full timing**, but mutes only that
  clip's `AudioSource`, including female-initiative (+38) code variants.
  The experimental full Animator-controller replacement stays off by
  default.
- **Configurable physical fluid in insertion:** added
  `Male fluid / Amount multiplier` (0.25–5.0, default 1.0), scaling the
  **number of native Metaball physical droplets**, not only the sprite
  particle. Sonyu triggers are now patched separately for vaginal/anal
  paths and at the real `WF/WS/SF/SS_IN_Start` transitions. In anal
  positions (the supplied log showed `isAnalPlay=True`), native
  Metaball ID **4** is used instead of ID 1, with a current-pose preset
  first and a standard preset fallback. The native fluid source belongs
  to the marked heroine; no other actor's stream is modified.

Note: the game log from v0.4.1 confirmed
`X=0.75, Y=0.75, Z=0.5, scaleApplied=True` with
UncensorSelector `PenisGUID=SoS`. That proves our own multiplier reached
the rig bone, not that the full replacement mesh aligned perfectly.
The added world-height correction and runtime wheel dump address what
the log could not establish. These visual changes still require RX18
in-game verification.

## RX18 Free H user-feedback fixes (v0.4.1)

These changes address the six specific gameplay issues reported after v0.4.0,
without modifying the real male participant's HFlag.finish.

1. **Saved geometry scale in Free H.** The KKAPI card-local X/Y/Z
   multipliers are reapplied in LateUpdate after animation and third-party
   body updates. If KKABMX or a scene reload changes the baseline scale,
   the modifier rebases to that new baseline instead of silently giving up
   (and restores only its own last-written value). Ctrl+Shift+F10 now
   prints the stored X/Y/Z values and whether scaling is currently applied.
2. **Scale-aware Kokan position.** The contact anchor aims between the
   actual bone root and shaft tip. Defaults: base `cm_j_dan100`
   (resolves `cm_J_dan100_00`), tip `cm_J_dan109_00`, interpolation
   `0.50`. Both bone world positions already include model scaling, and
   the configured hit radius stays constant in world units. The existing
   local X/Y/Z offsets still allow fine calibration for custom meshes.
3. **Remove the fifth Kokan phallus toy.** The base game's
   `aibulayer_02` final row 4 / icon 25 / `p_item_vibe`
   (IDs 7/8) is the unwanted fake phallus. It is removed for
   marked heroines only, preserving the preceding `denma` vibrator
   (IDs 5/6), as well as hand/finger/tongue items.
   The remaining wheel entries stay contiguous. Normal heroines
   retain all native items.
4. **Free H character marker.** The currently selected marked heroine
   displays an upper-left **⚥ FEMBOY** marker; it is hidden for
   ordinary characters, in other game modes and when disabled via config.
5. **Do not truncate Sonyu climax.** A live report showed that
   swapping the marked heroine's whole AnimatorController can interrupt
   the original climax state machine. That experimental swap is now
   **OFF by default**, controlled by a *new* explicit opt-in key even
   if the old v0.4.0 key was already enabled. The marked male
   appendage's per-bone motion follower remains independently available;
   the original female full-body climax animation stays in control.
6. **Actual native liquid physics in Aibu/Sonyu.** The marked actor's
   climax now attempts to start a separate `MetaballShoot` emitter
   backed by the game's own `UpdateMeta` / `StaticMetaballSeed`
   mesh renderer and outside-fluid projectile. Aibu can load the native
   `siru_khs_00` projectile preset if no male Sonyu emitter is active.
   Its source root is isolated from the real male actor, so scene
   cleanup cannot delete the other participant's fluid objects.
   If these assets are absent or the game has disabled semen effects,
   the original particle is preserved.

**Status:** the source changes above compile, but they require a fresh
RX18 gameplay run before their visual outcomes can be certified.
In particular, a custom UncensorSelector body may have no
`cm_J_dan109_00` tip bone or the desired physics projectile.

## RX18 rig compatibility (verified from local installation inventory)

The inspected RX18 installation contains the actual Unity transform
`cm_J_dan100_00`, whereas older default config used `cm_j_dan100`.

**Native ending effect uses a different bone.** From the user-supplied
`abdata/h/list/00_00.unity3d` TextAsset `h_particle`, particle ID 0
(`LiquidSiru`) belongs to `cm_J_dan109_00`. It is distinct from
the `cm_J_dan100_00` interaction/geometry root. Version 0.3.4 uses
its own `Male finish FX / Target bone` setting with the native
particle parent as default, retaining Kokan and `cf_d_kokan` fallbacks
for alternate body rigs.
Before v0.3.3 this strict name mismatch silently disabled the
model-specific bone scale and forced Aibu / finish FX back to the
female Kokan fallback.

v0.3.3 uses a shared resolver: exact name, case-insensitive name, exact
base plus `_00`, and then another numeric suffix. It deliberately
does **not** accept nearby bone families (for example, 100 never matches
101). **Existing config values can remain `cm_j_dan100`.**

After installing the new DLL, press Ctrl+Shift+F10 in Maker or Free H:
diagnostics now prints the configured name, resolved bone, parent,
local position/scale, and the character's male-geometry visibility flag.
This confirms **which bone is selected**, not whether the target
animation/IK is visually aligned in every pose.

## Configuration

The file `BepInEx/config/MaxMixAlex.FemboyMode.cfg` is created by BepInEx. Most values can be changed through BepInEx ConfigurationManager:

| Section | Setting | Default |
| --- | --- | --- |
| Maker | Unlock male-listed assets | true |
| Maker | Enable male geometry bone scaling | true |
| Maker | Male geometry bone | `cm_j_dan100` |
| Free H | Restrict front insertion | true |
| Free H | Show top-left Femboy marker (in native female gauge) | true |
| Free H | Retarget compatible male climax animation | true (legacy feature switch) |
| Free H | Allow experimental full-controller swap | **false** (safety gate) |
| Free H | Copy native male geometry climax motion | true (Generic bone fallback) |
| Free H | Suppress female climax voice | true |
| Free H | Use male-style finish FX | true |
| Free H | Generate native physical male fluid | true |
| Male fluid | Amount multiplier | 1.0 (0.25–5.0) |
| Male fluid | Model-relative launch speed | **0.85** (0.2–4.0) |
| Male fluid | Local pitch relative to shaft degrees | **0°** (-30–30) |
| Male fluid | Launch spread degrees | 6 (0–30) |
| Free H | Suppress female Aibu squirt FX | true |
| Aibu alignment | Enable Kokan action anchor | true |
| Aibu alignment | Enable target hit collider | true |
| Aibu alignment | Hide phallus toy after vibrator | true |
| Aibu alignment | Target bone | `cm_j_dan100` |
| Aibu alignment | Shaft tip bone | `cm_J_dan109_00` |
| Aibu alignment | Aim along shaft (0 root, 1 tip) | `0.50` |
| Aibu alignment | World Y correction | `-0.012` |
| Aibu alignment | Hand correction world X/Y/Z | 0 |
| Aibu alignment | Target hit radius | `0.05` |
| Aibu alignment | Offset X/Y/Z, Rotation X/Y/Z | 0 |
| Male finish FX | Target bone | `cm_J_dan109_00` |
| Male finish FX | Offset X/Y/Z, Rotation X/Y/Z | 0 |
| Debug | Verbose logs | false |

The anchor local offsets and rotations and the new hit sphere radius are refreshed during an active scene. Disabling the Kokan integration during a scene also disables the added hit collider; re-enter Free H to fully rebuild item-parent attachments. Changing target bone name requires re-entering Free H. Model geometry scaling is based on that bone's initial local scale. When the flag is disabled or the card is replaced, the controller restores its own last-written scale only if another plugin has not modified it in the meantime. Unrecognized bones leave the native model untouched. Model-specific compatibility with KKABMX, UncensorSelector and other runtime bone modifiers still requires game testing.

**Important:** the female Maker already shares many base body-shape fields with the male Maker; enabling cross-sex items and adding bone scaling does not mean every male-only native menu control is available. UI sections that exist only in the male Maker need separate adapters.

## Build / install

Targets .NET Framework 3.5 / BepInEx 5. Requires KKAPI and ExtendedSave, and uses optional integrations when installed. Build via:

```sh
dotnet restore src/FemboyMode/FemboyMode.csproj
dotnet build src/FemboyMode/FemboyMode.csproj -c Release --no-restore
```

Copy `bin/Release/FemboyMode/KK_FemboyMode.dll` to `Koikatu/BepInEx/plugins/`. The dedicated GitHub Actions workflow produces a downloadable `KK_FemboyMode` artifact.

For a local headless regression run (requires .NET SDK 9):

```sh
dotnet run --project tests/FemboyMode.Tests/FemboyMode.Tests.csproj -c Release
```

This checks the normal/female-initiative action-button offsets, numeric scale validation and distinct FX keys for two heroines. GitHub Actions also runs these assertions before uploading the DLL. The same job validates the collector's PowerShell syntax and executes a synthetic archive smoke test to ensure it includes the intended DLL and metadata but not the original asset bundle.

To smoke-test the plugin: install the DLL, open female Maker, check **Parameter → Character** and **Body → All**, enable the marker, save the character card, then reopen Maker before checking UncensorSelector body options. The first game log is `BepInEx/LogOutput.log`. For model-specific contact reports, include the selected UncensorSelector body/part and configured bone names.

## Collect local assemblies and diagnose missing animations

The project includes [Collect-KoikatsuBinaries.ps1](../../scripts/Collect-KoikatsuBinaries.ps1).
Run it on Windows from PowerShell 5.1+ and supply the installed game root:

```powershell
.\Collect-KoikatsuBinaries.ps1 -GameRoot "D:\Games\Koikatsu" -IncludeExecutables
```

It creates `Koikatsu-debug-binaries.zip` in the current directory, containing
the original game/Unity assemblies, BepInEx/Harmony DLLs and a *filtered* set
of relevant plugin DLLs plus a version manifest. By default the collector does
**not** include character cards, game asset bundles, save files, configs or logs.

**Output path:** The script always places the generated ZIP **beside the `.ps1` file**, not under the terminal's home directory. A relative `-OutputZip` is resolved beside the script; an absolute `-OutputZip` is honored unchanged. CI runs a regression test from a different working directory to verify both the default and explicit relative paths.
It performs no network requests. Review the archive before sharing it privately.

In either female Maker or a vanilla Free H scene, press **Ctrl + Shift + F10**
to log a character-independent diagnostic inventory: selected body/part GUIDs,
candidate rig bone names, end/finish clip names and game voice-channel states.
The inventory deliberately omits character names and card filenames.

To also collect the new diagnostic log tail and this plugin's config, run:

```powershell
.\Collect-KoikatsuBinaries.ps1 -GameRoot "D:\Games\Koikatsu" -IncludeExecutables -IncludeDiagnostics -IncludeAssetInventory
```

The optional `-IncludeAssetInventory` writes `_asset_inventory.csv` with candidate H/voice bundle names and sizes **without copying bundle bytes**. This helps determine which files are relevant before requesting any larger assets.

Use `-ExtraRelativeFiles @('abdata/h/list/specific-file.unity3d')` only
when a precise animation/voice asset path has been confirmed. The collector
enforces per-file and total-size limits and never copies the whole `abdata`.

These local references are needed to determine the **actual installed**
voice/animation assets for model-compatible male-style finish retargeting.
The original `Assembly-CSharp.dll` metadata alone does not contain those
Unity animation clips or voice recordings.

From the supplied RX18 asset filename inventory, the first two useful,
small list bundles are `abdata/h/list/00_00.unity3d` (~3.39 MiB)
and `abdata/h/list/20_00.unity3d` (~3.37 MiB). To collect those
**specific** bundles rather than multi-gigabyte animation data:

```powershell
.\Collect-KoikatsuBinaries.ps1 -GameRoot "D:\Games\Koikatsu" `
  -OnlyExtraFiles `
  -ExtraRelativeFiles @("abdata/h/list/00_00.unity3d", "abdata/h/list/20_00.unity3d") `
  -OutputZip ".\Koikatsu-animation-lists.zip"
```

The `-OnlyExtraFiles` flag produces a much smaller incremental archive:
it omits the same DLL/EXE files already provided in the initial debug ZIP.
It requires at least one `-ExtraRelativeFiles` path.

Only privately share bundles from your own installation. Review the
resulting ZIP before sharing, and never add these proprietary bundles
to the public GitHub repository.

## Validation and known limitations

Passing CI proves **C# compilation only**, not the actual motion or visual results inside the game. Manual checks are still required:

1. Save a marked card and reload it; switch to an unmarked female card, then a male card; ensure no state leakage.
2. Toggle the flag in both Maker pages; verify the controls synchronize and male list entries become visible on reopening Maker.
3. Test bone scaling on installed male geometry, restore when flag is removed, and combination with KKABMX / UncensorSelector.
4. Test touch alignment for the actual model and several poses, including source hit collider position and new hit sphere.
5. Test both available and unavailable anal-only positions in 2-person Free H; verify direct action handlers and active-character isolation.
6. Test climax effect when the male particle resource is present, original female particle suppression, and compatibility with other effects mods.
7. In a Sonyu finish sequence, verify the marked actor's Generic male-geometry bones move, and that removing the marker or leaving the finish returns their rotations to the previous pose without affecting the actual male actor.
8. Confirm that female climax dialogue is suppressed only for the marked actor; pre-climax and unrelated voice events still play. For a Humanoid replacement model, test controller switching and immediate return to normal pose; for non-Humanoid models the controller is not swapped.

**Remaining functional gap / runtime validation:** v0.4.4 now contains actual actor-local animation and voice routing, but **full male-controller retargeting works only for verified compatible Humanoid rigs**. Generic rigs use limited add-on bone motion, not full-body or full-pose replacement. The actor-specific voice behavior silences the audio signal for female climax lines without discarding their native playback/finish timing; it does not replace them with a new male voice sample (the vanilla male actor does not have an equivalent voice table). These features and the alternative male-style FX must be tested in the installed game, including poses with no matching bone or animation state, UncensorSelector replacements, and interactions with BetterSquirt/KKABMX/MultiTeamH. The shared `HFlag.finish` of the actual male participant is never modified. This repository does not bundle proprietary game assets or replacement models.

## v0.4.5 — Sonyu cycle deduplication and reversed Houshi actions

- Sonyu native HFlag, SetPlay and consumed voice callbacks now share one
  per-character, per-pose climax gate. Recovery voice codes do not create a
  second physical emission; the gate re-arms on an ordinary insertion loop.
- Each genuinely new Sonyu climax gets a fresh MetaballShoot state instead of
  reusing a depleted projectile controller. Old private droplets are cleared
  when the next native shooter is initialized.
- In marked-character Free H, the native Houshi menu receives an additional
  `[Reversed] ` entry for each existing service animation. The original motion
  and its identifiers/assets remain unchanged. Selecting the reversed entry
  exchanges the male and marked heroine's compatible body-joint poses at the
  end of animation evaluation; an early-frame restorer prevents feedback.
  Only the rendered body poses are reversed: the game's character sex, card
  metadata, H voice queues, and underlying gameplay counters are not swapped.
- Reversal requires at least 20 compatible shared body bones. If the installed
  rig lacks them, the action injection is skipped instead of showing broken
  controls. The in-game Free H poses and IK alignment still need validation.

## v0.4.6 — recovered Sonyu climax and native-clip Houshi reversal

- Real RX18 0.4.5 log: a second `A_SF_IN_Start` can immediately follow `A_SS_IN_A`, without observing ordinary `*_M_IN_Loop`. The Sonyu gate now re-arms on **recovery -> new Start** as well as ordinary insertion movement, while still suppressing multiple callbacks inside the same climax.
- The v0.4.5 raw rotation/position exchange between 278 male/female body bones has been **completely removed**. Reversed Houshi now creates per-actor `AnimatorOverrideController` instances and exchanges only **matching native Houshi motion clips**, retaining each actor's original controller, avatar and separate facial states. A pose without at least three paired clips is explicitly skipped (not silently forced into a deformed rig). Switching to a normal pose or exiting restores the exact original controller.
- The Houshi menu is synchronized from its live use-list, UI list, all three categorized native submenus and the Animation Loader import mapping (the same import mapping inspected by MultiTeamH), capturing late kPlug-compatible additions. Each original action remains untouched; generated duplicates have stable internal `[Reversed]` names.
- Visible motion-row labels are localized like MultiTeamH: KK_Chinese `GetText` first, XUnity AutoTranslator fallback; Chinese UI displays **[反转]**. Rebuilding the native menu reapplies the labels to pooled UI rows.
- **Runtime validation required:** motion-clips may still have authored poses and contact/IK/camera targets specific to the original actors, so evaluate actual Houshi actions (including kPlug) before considering this feature finished. CI validates compile and rules, not on-screen alignment.


## 0.4.7 — RX18 repeated/combined climax and existing animator-override compatibility

### What the v0.4.6 runtime log established

- LogOutput(5).log: 104 Reversed menu items were added, but selecting them repeatedly logged that an existing AnimatorOverrideController prevented all role overrides. In RX18, an existing override controller is normal and must not be treated as a fatal conflict.
- The Sonyu animation can enter subsequent SS/SF/WS climax Start states without visiting the ordinary M_IN_Loop or recovery state. At the end of the test, HFlag.finish was sameS while the old emitter gate was still fired=True.
- Combined finishes can consume female climax voice 318/319 before a later 325–330 cue. Not every later cue is a new orgasm, and a recovery *_A cue must never cause a second discharge.

### Corrected implementation

- HFlag.AddSonyuOrg / AddSonyuAnalOrg now record a per-marked-character native orgasm serial; they do *not* discharge fluid. A consumed native climax voice 325–330 (plus female-initiative 363–368), or combined-finish-only 318/319 (plus 356/357), starts native MetaballShoot only during a native Start/Loop emission phase. A new native orgasm serial can re-arm when a previous climax did not pass through the ordinary loop; 0.9 s protects against same-event callback duplication.
- Houshi role reversal now preserves the existing animator-override chain from kPlug/game/other plugins, resolves its effective animation clips, then creates an independent override layer rooted in the original state machine. Any third-party clip mappings unrelated to the chosen Houshi motion remain intact. Unity 5.6 ApplyOverrides batches changes to avoid repeatedly reallocating animation clip bindings.
- Unlike 0.4.6, an actor already using AnimatorOverrideController is not automatically rejected. When another plugin replaces the temporary controller during playback, the plugin releases its own layer safely and makes at most two timed retry attempts during the same selected Reversed action.
- Names are localized in the cloned native AnimationListInfo itself (Chinese [反转] with KK_Chinese, otherwise [Reversed]), and both prefixes continue to be identifiable when HSprite rebinds a copied row.
- The old direct 278-bone / facial-transform swap remains removed.

### Acceptance tests still requiring real Koikatsu

1. Select vanilla and imported kPlug Houshi [反转] actions. There should be one correctly named clone per donor, and the log should report an installed paired-motion clip layer rather than a blanket AnimatorOverrideController skip.
2. Observe actual actor service/receiver motion, facial expressions, IK contact, and return to the original action. Matching clip names and passing CI cannot prove the animation curves target the required bones.
3. In a single Sonyu pose, perform ordinary and combined (sameW/sameS) orgasms repeatedly, including immediate recovery-to-new-Start. Check precisely one physical-fluid burst for each *distinct* climax and no duplicate on recovery.
4. Repeat with kPlug imported motions and female-initiative variations. Capture verbose logs only if a runtime issue persists.

Build and 155 headless rules assertions passed on the development branch. The pull request stays draft until real in-game acceptance.

## 0.4.8 — HCtrl pre-combined climax and native/kPlug reverse-menu binding

Based on real Koikatsu RX18 LogOutput(6).log. This supersedes the ineffective 0.4.7 cross-gender clip-name swap.

### HCtrl sequence
- HCtrl can enter `A_OLoop` (before simultaneous climax) and subsequently request `A_SS_IN_Start` without advancing `HFlag.AddSonyuOrg`. Previously the Sonyu gate stayed `fired=true`, suppressing physical emission.
- The cycle gate now observes `A_OLoop`/`A_OStart` as a distinct pre-combined phase boundary; a later finish Start rearms emission even without a new native counter. It never emits during pre-climax or recovery and still waits for the original consumed voice signal.
- A semantic cycle transition no longer suffers from a global 0.9s Sonyu cooldown: fast HCtrl pre -> climax clicks are allowed. The Aibu cooldown is unchanged.

### kPlug / AnimationLoader integration
- IllusionMods/AnimationLoader (https://github.com/IllusionMods/AnimationLoader) reads its `swapAnimationMapping` both to label imported actions in `LoadMotionList` and to load imported controller bundles in `ChangeAnimator`. Merely cloning `AnimationListInfo` lost both.
- Clone and register corresponding `SwapAnimationInfo` metadata for each reverse import, changing only `AnimationName` on the copy; retain imported controller and asset paths and Studio ID. Imported list entries and compatible H-point/category constraints are now distinguished from native action entries.
- Deduplicate donors by stable native pose ID/category/name or imported GUID/StudioId/asset+controller identity, rather than temporary object reference. This prevents the previous large pile of repeated native menu rows.
- When the UI copies a selected action into a pooled button, resolve that copied row back to its canonical mapped reverse clone; call native `HSceneProc.ChangeAnimator` once instead of allowing the donor-ID equality check to discard the click.
- New menu logs report `sourceNative`, `sourceImported`, `importedEligible` and `mappingAvailable`. They must be evaluated separately; no longer assume that every 104-action count includes imported poses.

### Runtime role motion
- Native female `cf_j_*` and male `cm_j_*` curves address different rig paths. The old same-name AnimatorOverrideController clip swap could not produce true role reversal and has been removed.
- New `FemboyHoushiRoleRetargeter`: read both native pose streams, transfer rest-relative FK motion between matching body joints only, and restore original inputs before the next native animation update. Expressions, blendshapes, collider/cloth/penis add-ons, controllers and voice are left intact. The strict 12-joint minimum prevents applying a broken rig transfer.
- New optional setting: `Free H / Reversed Houshi exchange visible actor positions` (default ON). Turn OFF if a third-party IK or camera plugin needs the original actor visual-root positions.

### Acceptance still required
- CI: .NET Framework 3.5 build and **172** rules assertions pass. These assertions check HCtrl phase transitions, stable catalogue identifiers and the body-joint whitelist, not scene-rendered poses.
- Real Koikatsu RX18 tests still needed: native and kPlug reverse list counts/localization, correct imported motion bundles after click, actor contact/IK/face, clean return to ordinary pose, and one physical-fluid burst for each HCtrl pre-combined -> combined climax.
- Look for `Reversed Houshi body retarget active` and a nonzero `sourceImported`; if absent, the runtime did not deliver the requested behavior.


## 0.4.9 — Restore actual shared body-bone role retargeting

0.4.8's visible actor-role retargeter could never find sufficient joints in RX18: it incorrectly expected male human-body bones to start with `cm_j_`. **Both** male and female Koikatsu humanoid bodies have the same `cf_j_*` skeleton. `cm_J_dan*` belongs to the optional genital overlay rather than the human body. Earlier test assertions incorrectly encoded `cm_J_spine01` as a real male body joint.

### Corrections
- Match the same `cf_j_*` core FK joints on the marked heroine and male partner (including `cf_j_hips`, `cf_j_waist01/02`, `cf_j_spine01-03`, shoulders, arms, thighs/legs, neck and head). Keep every `cf_d_*`, facial, genital/`cm_J_dan*`, clothing, physical and IK helper bone excluded.
- Read BOTH native animated poses in LateUpdate, transfer **rest-relative** body-joint rotations between characters, and restore their native inputs before the next animation tick. Raising the retarget execution order puts this visual-only step after ordinary animator and IK updates.
- A raw `objBodyBone` root-position swap is not sufficient: in H scenes, the two roots can coincide while their `cf_j_hips` bones move independently. Record both **animated hip world positions**, exchange the original body-root orientations, and align the rendered actor hips to the opposite actor's previous world position. The male should occupy the original giver location and the heroine the receiver location.
- Always log `Reversed Houshi body retarget active` with the actual number of matched `cf_j_*` pairs on entry. Log first rendered frame, a 30-frame largest FK rotation change check and the number of frames affected when leaving the action. The original female and male game/controller/voice identities remain unchanged; retargeting affects visible character roles rather than reassigning the whole HHoushi state machine.
- Headless shared-skeleton regression assertions now cover actual `cf_j_*` male and female names, waist/hips/limbs and the exclusion of fake `cm_j_*` body joints. The HCtrl, physical fluid, and AnimationLoader/kPlug action menu changes from 0.4.8 have not been modified.

### Validation status
- Compiles against Unity 5.6 / Koikatsu net35; CI runs the current headless rules harness and produces an `KK_FemboyMode` artifact.
- **Not yet confirmed in the actual user's RX18 game.** Check that a selected `[反转]` action reports `real cf_j_* body joint pairs` and the 30-frame log; then inspect actual giver/receiver movement, world positions, hand/mouth IK and actor faces. At this stage native HHoushi dialogue/progression still uses the original game actor identities.

## 0.4.10 — original-style heroine badge, clothed Aibu contact, reversed mouth expressions

These changes follow verified RX18 0.4.9 feedback: the full body-role reversal now works; native/kPlug menus and the HCtrl physical-fluid cycle also work. This revision leaves their working control paths unchanged.

### Native H-scene UI badge
- The old Femboy gender glyph was logged as attached to `Canvas/Bar` but was not actually visible to the user. Its anchor was the gauge's clipping fill bar; it also depended on native font support for a little-used Unicode glyph.
- The new `FemboyMode_NativeHeroineBadge` borrows the CURRENT H scene's original image sprite and UI font, styles a small `♂ 伪娘` (or supported English fallback) panel, and places it on the top-level H Canvas, immediately beside the heroine portrait. It is a UI element, not a separately painted bitmap or added picture asset.
- The badge remains non-interactive, is clamped to the visible canvas rectangle, and is shown only in marked-character Free H. Runtime logs state actual visible/hidden status and display position rather than just successful object creation.

### Kokan Aibu while underwear is still worn
- `ChaControl.notShorts` is the native live indicator for whether the underwear surface is away. Previously the Kokan hand proxy ALWAYS shifted from native `cf_d_kokan_REF` to the configured penis bone, causing hands to float while panties remained in place.
- While `notShorts=false`, use the original HandCtrl panty/clothing contact and turn OFF the supplementary hidden penile hit collider, preserving native raycasting.
- When the character exposes their anatomy (`notShorts=true`), restore the existing male-geometry anchor, its config offsets and the physical hit collider. This switches live during the scene without rebuilding the scene, touching another actor's hand, or stripping clothing.

### Reversed service expressions
- 0.4.9 correctly exchanged 32 common human FK body joints, but the native `HHoushi`/voice face controller continued updating the original female card's mouth because the logical native heroine identity was unchanged.
- For selected reversed Houshi actions, sample BOTH characters' original `fileStatus.mouthPtn` and `fileStatus.mouthOpenMax` AFTER native face updates and exchange them using the game's own `ChangeMouthPtn` and `ChangeMouthOpenMax` APIs. The male performer thus receives the original heroine's oral mouth pattern/opening; the heroine receives the male's mouth pattern/opening.
- Restore each original channel before the next animation update, only if another face plugin has not replaced the written values. Preserve existing game controllers, actual character data, voices and card identity.
- New configuration: `Free H / Reversed Houshi swap mouth expressions` (default ON) lets users disable the facial role swap if third-party expression plugins conflict. Logs include native and applied mouth-channel readback.

### Testing
- CI: .NET Framework 3.5 plugin build, headless regression rules, PowerShell collector verification and packaging. **185** headless assertions include underwear-covered vs exposed contact and reversed oral marker.
- Runtime acceptance is still required for the badge's actual screen visibility, the hand's contact position across underwear states, and correct mouth motion during imported/native reversed oral animations. Runtime logs are not proof of final visual alignment.
- These improvements intentionally do not change the previously accepted Sonyu fluid cycle or kPlug action catalog.

## v0.4.11 — Compact H indicator, live underwear state, reversed Siru stain ownership

- User screenshot showed the previous 73x27 upper-left `♂ 伪娘` rectangle overlapping native pleasure HUD. The H flag is now a compact 22px teal, cream-bordered circle carrying a white sex glyph, anchored just after the native portrait status chip rather than across the portrait or gauge. Source is the portrait top-left, not a variable fill bar. Logs say whether the badge is displayed and where it was positioned.
- In v0.4.10 the code treated `ChaControl.notShorts` as the *current* underwear position. Existing Koikatsu clothing mods demonstrate that the dynamic state comes from `ChaControl.fileStatus.clothesState[3]`: 0=On, 1=Shift, 2=Hang, 3=Off. The Kokan hand proxy now follows **the current clothing state** each frame: use native contact at On; use the male anatomy contact at Shift/Hang/Off or when shorts are absent. Extra male collider follows the same exposure rule. Verbose logs include the actual status byte.
- In a reversed Houshi animation, native HHoushi still writes liquid stains to its original heroine object. `FemboyReversedSiruHooks` suppresses positive `ChaControl.SetSiruFlags` on the marked heroine only for active [反转] service actions, instead writing the requested Siru region/intensity to the original male character. Zero/cleanup operations are untouched. This path is compatible with the installed MaleJuice male material support if active.
- The working v0.4.9 body role swap, v0.4.8 HCtrl/Metaball fluid, and native/AnimationLoader imported action menu are unchanged.
- A mechanical FlatLock source pack was separately generated as original two-piece Koikatsu-scale OBJ/GLB/STL plus a Unity 5.6.2f1 ModdingTools accessory project. It targets Maker categories `ao_kokan (130)` and `ao_waist (126)` and is **not a playable zipmod until Unity compiles the assets**.

### Runtime verification
Confirm that the new circle is not covering the portrait, gauges or top-left status; cycle underwear through On -> Shift -> Hang -> Off and check hand movement; finish a reversed Houshi action with Siru and verify the male receives the overlay while the marked heroine remains unstained. CI compile and 188 headless rules assertions cannot verify these visual behaviors.

## 0.5.0: Penis and Balls transforms in Character Maker

Marked female cards now have two new Body subcategories: 男性器调整 (Penis) and 睾丸调整 (Balls).
Each tab provides XYZ scale (0.5–2), parent-local translation (-0.1 to +0.1 Unity units),
and bone-local XYZ rotation (-180 to +180 degrees). The old BodyBoneScaleX/Y/Z keys
and numeric values are preserved; new data is saved with the character card (ESF version 3).

Penis root: cm_j_dan100 (usually resolves to cm_J_dan100_00). Balls root:
cm_J_dan_f_top. Both names can be overridden in BepInEx config. A missing bone is
not replaced by the pelvis; this prevents whole-body distortion.

The per-tab 显示轴（预览） toggle shows colored XYZ axes at the selected bone.
This is a read-only preview. The stock cmpGuid draggable widget is bound to
accessory slots; taking it over could move the currently selected accessory.
Drag-to-edit is therefore not claimed, and all edits use native Maker sliders.

Changes are applied after native pose updates and rebased when external animation
or ABMX changes the bone baseline. Neutral values restore original transforms.
Unmarked characters remain untouched.

Suggested in-game test: change only Penis position, then only Balls rotation;
save/reload, switch UncensorSelector model, restore neutral; then switch to Accessories
and confirm vanilla gizmos work normally. Check H scenes with ABMX/BP enabled.
CI compilation and headless rules do not replace Koikatsu runtime testing.
