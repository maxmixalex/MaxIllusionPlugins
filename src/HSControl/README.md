# HSControl for Koikatsu

A new, independently implemented controller for a running Koikatsu / Koikatsu Party H scene. The C# source uses the original game's HFlag and HSceneProc public members, not a wrapper around the original KK_HCtrl.dll.

## 0.1.6 — stop unintended motion on scene entry

- Existing HCtrl INI/BepInEx saved automatic-speed preferences no longer authorize active hand control at H-scene entry. Automatic motion, native caress, auto-finish and auto-replay stay idle until the player actively changes an HSControl speed control **within the current scene**. An explicit selector or slider change arms it; the arm is cleared on scene transition and plugin disable. No extra auto-caress toggle.
- Prevents spontaneous motion and repeated one-frame Aibu pulses solely because an old INI happened to select random speed. The native hand remains in control until the user acts, even when the UI displays a remembered speed mode.
- This is an intentional safety refinement of the HCtrl-style speed behavior. Actual motion/hand choreography still needs in-game E2E validation across animation packs.

## 0.1.5 — native one-update auto-caress and direct action dispatch

- Removed the separate auto-caress checkbox: original HCtrl automatically drives Aibu while an automatic speed mode is enabled. No new toggle is required.
- Fixed the multi-second hand lock. Auto Aibu now issues a single update of native `HandCtrl.action`, releases it on the next update, restores `actionUseItem` (including the original -1 no-selection value), and waits 0.7–3 s before the next eligible contact. Player mouse and second-hand input always take priority.
- The native speed setting does not add gauge every idle frame. `FemaleGaugeUp` only runs on a valid, owned short Aibu pulse; empty contact lists leave the gauge untouched.
- Removed invented "current action unavailable" banners. Weak/strong follow original `ChangeSW` (native `motionchange`) rather than forcing `SetPlay`; the native loop, pre-climax and climax operations now dispatch based on HCtrl's per-scene `CanProc` matrix. More detailed in-game checks remain necessary across non-Aibu modes.

## 0.1.4 — reconstructed from the installed HCtrl GUIFunc

- Restored the actual HCtrl single-window order: female/male gauges with native lock-button callbacks; four auto speed choices; optional range/band/link options; male/female auto climax and replay; and original three rows of loop, pre-climax and climax actions. No tabs and no vertically hidden actions.
- **Fixed an important regression:** auto-speed never implicitly raises the female gauge. The previous 0.1.3 driver called `HFlag.FemaleGaugeUp()` on every Aibu idle frame, causing a full gauge without interaction. Automatic caress is now a **separate option OFF by default**.
- Optional auto-caress requires a real `HandCtrl.useAreaItems` item and writes its scalar `actionUseItem` ID before starting the native drag. It yields to mouse and second-hand inputs, and raises gauge only while it owns a live action.
- Native Aibu female finish requests `HActionBase.SetPlay("Orgasm_Start")` plus `HFlag.AddAibuOrg()`, not merely setting gauge to 100. Other specialized original 3P/alternative animation transitions still require in-game validation.
- Tested the GUI against ILSpy-decompiled `KK_HCtrl.HCtrl.GUIFunc` of the installed original DLL. Test fixtures explicitly assert no passive gauge growth over 500 idle frames.

## Runtime features

- Default `Ctrl+F8` opens the MaximumUI window (unmodified F8 is already used by ClothingCompression). No native H-scene button is injected: the original lighting slot, its click handler and all other native buttons remain untouched. BepInEx configuration is under `MaxMixAlex.HSControl.cfg`. Change the hotkey if it conflicts with another installed plugin.
- Four speed modes: off; slow/full/fast bands; timed randomized range; gauge linkage. Speed dynamics follow the installed original's 2–10 second target timer, 0.05 minimum speed, 0.4999/0.5001 band split, 0.05–0.2 sinusoidal variation and 0.005–0.05 lerp factor (normalized to real time for stable 30/60 FPS). Manual slider can temporarily override speed. Live readback displays both current output and target speed.
- Each supported scene/animation mode is checked before writing values. Aibu uses native `HFlag.speed`; supported Houshi/Sonyu loops use `HFlag.speedCalc` after the game's `WaitSpeedProc`, via Harmony postfix. No speed writes in unsupported modes or transition states. An **explicitly opted-in** HCtrl-style Aibu cycle starts a native HandCtrl drag only when a usable Aibu contact exists and both player hands are idle; it forwards speed and FemaleGaugeUp, then releases only its own action. Existing manual hands take precedence.
- Optional automatic motion-strength switching, with a configurable randomized interval. Auto finish toggles, native gauge adjustments and optional replay use the game's existing HFlag action system.
- Replay follows the installed original KK_HCtrl IL rather than sending the same click in every mode. Houshi uses `again`; Sonyu `OUT_A` uses native insertion, then a delayed `speedup`, while `IN_A` goes straight to `speedup`. The gate waits at least 13 seconds and for idle voice, and avoids repeated requests or overwriting another user's pending click. A pure state-machine regression covers both game modes and 3P routes.
- Manual weak/strong loop controls route through the current native `HActionBase.SetPlay` (Houshi/Sonyu in two-person, 3P, and MMF/Dark modes 1/2/6/7/8/9 with an initialized action), not a fabricated animator. The same panel exposes native male/female gauge-threshold and finish requests with mode guards. The replay gate issues at most one request per completed animation before the next loop.
- Inherit defaults from an existing `BepInEx/plugins/KK_HCtrl.ini` when present, but always keep BepInEx-managed values authoritative. Legacy INI is never modified. Missing INI uses safe defaults.

**Uninstall the original `BepInEx/plugins/KK_HCtrl.dll` before installing HSControl.** Both plugins write to the same game fields, and simultaneously running them can cause conflicting state. Preserve `KK_HCtrl.ini` if you wish to import its defaults. Ensure that the shipped `MaximumUI.dll` comes from the same build. No original DLL, game binary, or user config is included in the package.

## 0.1.3 — eliminate the light-button regression and restore Aibu gauge progression

- Removed the entire `NativeSceneButton` class and its update loop. HSControl is **Ctrl+F8 only**, leaving the game's lighting/menu controls unchanged.
- Automatic Aibu now invokes the original `HFlag.FemaleGaugeUp` path along with `HFlag.speed` and a stateful native primary-hand drag. It detects game or compatible imported `Idle` animation names; requires usable native `useAreaItems` and does **not** replace manual actions of either hand.
- The former 650px scrolling form is divided into three compact tabs (speed, state, automation); the controls of inactive speed modes are omitted. User-entered speed and runtime speed remain separate readings.
- Managed regression tests verify the scene guards and UI structure, but real two-hand + CyuNoVR/modded animation E2E must still be performed in Windows before calling this a full HCtrl replacement.

## 0.1.2 — native interaction corrections

- Stop capping `HFlag.gaugeFemale` at 99 when automatic female finish is disabled. This legacy HCtrl behavior blocked native climax/near-climax controls. Its old config line can remain on disk but does not suppress the game's own action.
- Manual male/female/both pre-finish gauge adjustments can be used without an active loop, including Aibu, and never clear the ongoing hand-click event. Only manual weak/strong `SetPlay` actions require an initialized animation and idle native click.
- `TryStartLoop` no longer clears `flags.click`, `flags.finish`, `voiceWait`, `isInsideFinish`, resets male gauge to 35 or overwrites Houshi motion behind the game's back. Returning from the game's `SetPlay` is sufficient to signal a successful explicit command.
- New HS self-drawn entry replaces copying the Config icon and placing faint white text over it; the MinimumUI window is shorter and makes several controls horizontally aligned.
- **Compatibility caution:** do not keep `KK_HCtrl.dll` loaded alongside `KK_HSControl.dll`. The current authorized Koikatsu plugin folder still lists `KK_HCtrl.dll` but not `KK_HSControl.dll`; confirm the intended game copy before comparing live behavior. No installed file was removed or altered by this change.

## Scope and caveats

The original `KK_HCtrl.dll` was inspected using its CLI method/field metadata and IL; no original source was copied. The new speed controller and lifecycle are real runtime code, **not the original binary**. Version 0.1.3 supports manual native WLoop/SLoop, automated replay and finish guards in modes 1/2/6/7/8/9; specialized finish animations remain dependent on the installed game's native action state and are not all force-replicated. H-scene behavior, compatibility with other animation mods and the default hotkey still require in-game E2E validation. Until those checks pass, use a copy of your game install when evaluating experimental plugin builds.

## Build / tests

```sh
dotnet build src/HSControl/HSControl.csproj -c Release
dotnet run --project tests/HSControl.Regression/HSControl.Regression.csproj -c Release
```

Output: `bin/Release/HSControl/KK_HSControl.dll`, with the same-build `MaximumUI.dll` alongside. The independent regression host checks the motion scheduler, mode guards, clamps, manual override, gauge linkage and legacy config parsing. MaximumUI tests/preview exporter run the **actual production View** with anonymous fixture data.
