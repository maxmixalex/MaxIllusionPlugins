# MaxIllusionPlugins Windows RX18 — Actual game acceptance guide (2026-11-26)

This checklist describes **in-game** verification. Successful CI compilation, screenshot rendering and managed assertions do not replace a Windows Koikatsu run. Do not merge the integrated draft PR until the relevant game behavior has been checked.

## Keyboard controls (current defaults)

| Feature | Key / entry | Context |
| --- | --- | --- |
| BepInEx Configuration Manager | **F1** | Most game screens; see and edit plugin settings |
| JuiceControl | **top-row 0** (`Alpha0`, not numpad 0) | Effect editor |
| ClothingControl | **F7** | Maker, Studio, Free H |
| HSControl | **Ctrl+F8**, or native **HS** button beside Config | Free H |
| LoveJuice panel | **Ctrl+F9** | Enables/disables the configuration window, *not* the effect itself |
| LoveJuice sample | **F9** | Emit native Studio drops at selected character, when LoveJuice is enabled |
| FemboyMode bone/animation diagnosis | **Ctrl+Shift+F10** | Logs current Maker/Free H rig and animation info |
| FemboyMode XYZ gizmo | Body > All > "显示轴（可拖动）" | Maker; no standalone toggle hotkey |
| FemboyMode gizmo drag | **Shift** = fine, **Ctrl** = coarse | Maker, while dragging |

The shortcut entries can be changed with F1 or in their corresponding `BepInEx/config/*.cfg` file. Close any competing plugin that binds the same shortcut.

## Configurations and baseline (checked against the user's installed RX18 configuration)

- `BepInEx/config/MaxMixAlex.FemboyMode.cfg`: Maker > `Enable Body All native scrolling = true` is **new in 0.5.8**, and is a live rollback switch. Set it to `false` in F1 to restore the original Maker Body > All UI without disabling other Femboy features. The existing `Maker / Enable male genital bone transforms` and `Enable male geometry bone scaling` remain independent.
- FemboyMode > `Diagnostics / Dump rig and animation names` = `F10 + LeftShift + LeftControl`; `Debug / Verbose logs` is enabled on this RX18 installation. `Male fluid / Amount multiplier` is set to **2** (different from the default of 1). Current active physics fields are `Model-relative launch speed = 0.85`, `Local pitch relative to shaft degrees = 0`, and `Launch spread degrees = 6`. The old cfg lines `Launch speed = 2.5` and `Launch elevation degrees = 18` are **stale legacy keys**; do not mistake them for the current physics controls.
- `BepInEx/config/MaxMixAlex.LoveJuice.cfg`: `General / Enabled = false` on this installation. **Turn it on first** using F1, the Ctrl+F9 window, or JuiceControl's integration switch; otherwise the F9 sample and automatic drips cannot work. `Sources`: Face, Body, Intimate = true; Male pre-fluid, Milk, Shower, Marked geometry = false. This explains why those opt-in sources will not appear until enabled. `Male pre-fluid gauge threshold = 45`; `Milk gauge threshold = 65`.
- LoveJuice > `Performance / Max active drops = 48`, `Emission interval = 0.26`; `Physics / World collision = true`, `Gravity = 8`, `Air damping = 1`, `Drop lifetime = 1.6`, `Launch speed = 0.35`; `Visual / Prefab scale multiplier = 0.045`. For F9 tests set `General / Preview channel`: 0 Water, 1 Saliva, 2 Body, 3 Shower, 4 Milk, 5 Marked geometry, 6 Intimate, 7 Male pre-fluid.
- `BepInEx/config/MaxMixAlex.JuiceControl.cfg`: `General / Enabled = true`, `Show menu = Alpha0`; installed SkinEffects is reported as `已安装 (旧API)`. The preset file is `BepInEx/config/MaxMixAlex.JuiceControl.presets.xml`. **Open the UI with nonzero Ex values, close, and verify no reset.**
- `BepInEx/config/MaxMixAlex.HSControl.cfg`: `General / Enabled = true`; `Motion / Auto speed mode = 0` means automatic speed is **off** (1 = bands, 2 = randomized range, 3 = gauge linked). `Manual override = false`, `Auto strength = false`, `Strength timer minimum = 10`, `maximum = 30`. `Scene / Auto male finish = false`, `Auto female finish = true`, `Auto replay = false`. Turn a feature on before testing its automatic behavior.
- `BepInEx/config/MaxMixAlex.ClothingControl.cfg`: `General / Show menu = F7`, `Show in Maker = false` (remember visibility preference, not an unload or disable setting).

## Priority test cases

1. **Maker layout / FemboyMode 0.5.8**: Female Maker > Body > All. Original rows, extra Femboy controls and draggable axis settings must all be present in the same scroll viewport. The scrollbar must have meaningful track height; scroll wheel, dragging, and other Maker tabs must work. Repeat after changing tabs, resolution, card, and returning to Maker. The 2026-11-26 screenshot with a tiny scrollbar and misplaced text is a **failure**.
2. **Maker emergency rollback**: F1 > Femboy Mode > Maker > `Enable Body All native scrolling = false` *while the Maker is open*. The original Body > All layout and input should recover without reopening the game; turn it back on for a new check. If this fails, preserve `BepInEx/LogOutput.log` and a screenshot.
3. **Femboy XYZ handles**: Enable an active marked card, show one of the Penis/Balls axes, and drag X/Y/Z under scale, translation and rotation. Check sliders and character transform remain in sync and the gizmo disappears when hidden.
4. **JuiceControl integrity**: Set nonzero Ex/SkinEffects values, open/close JuiceControl, change character, and save/reload presets. Values must not silently clear or be overwritten by an unreadable optional field.
5. **LoveJuice**: Enable `General / Enabled` first. In Free H, Ctrl+F9 + F9 previews; confirm each channel, material, bone attachment, particles, particle cap, and pose-relative direction. Test optional sources one at a time. With SkinEffects uninstalled or unreadable, only the corresponding optional event should be skipped.
6. **HSControl**: Ctrl+F8 or native HS button. Test manual speed, then separately change mode to 1, 2 and 3; enable Auto strength / Auto replay when evaluating those, and test replay only in valid supported native states. Repeat in 3P/MMF.
7. **ClothingControl**: F7 in Maker, Studio and Free H; verify nine garment categories, accessories, multi-character selection, preset save/apply and mismatched-character safeguards.
8. **Lifetime and cross-plugin**: Switch between Maker, Free H and menu, reload cards, and alternate panels. No stale target, UI capture, particle pool leak or repeated exception.

## Evidence to provide for a failure

- For a visually wrong Maker pane: full-resolution screenshot, the active Maker tab, game resolution, and the fresh `BepInEx/LogOutput.log` **after the issue**.
- For the 0.5.8 fallback: whether changing `Enable Body All native scrolling` from true to false restored the page; attach the resulting log.
- For physics, animation or gizmo behaviors: a short clip showing the action and resulting state. Avoid sending full game assets unless necessary.
- For lost preset values: the affected keys and before/after numbers; do not replace or delete the original presets during diagnosis.

### Important evidence limits

The source log from 2026-11-26 contains `Femboy Mode 0.5.7: Harmony groups registered 10/10` and `Maker Body > All: native whole-page scroll installed.` but no Maker-scroll exception. The screen layout regression was visible despite successful installation. Version 0.5.8 fixes a code path that measured its own content height and supplies a rollback control, but the new UI behavior must still be checked on the real Windows game.
