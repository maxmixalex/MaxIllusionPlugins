# HSControl for Koikatsu

A new, independently implemented controller for a running Koikatsu / Koikatsu Party H scene. The C# source uses the original game's HFlag and HSceneProc public members, not a wrapper around the original KK_HCtrl.dll.

## Runtime features

- Default `Ctrl+F8` opens the MaximumUI window (unmodified F8 is already used by ClothingCompression). A separate **HS** button is also installed alongside the native HSprite MainMenu/Base > Config control (when that hierarchy exists). The entry uses an independently painted high-contrast HS sprite with the native Button/Image event handling; the original menu is left unchanged. BepInEx configuration is under `MaxMixAlex.HSControl.cfg`. Change the hotkey if it conflicts with another installed plugin.
- Four speed modes: off; slow/full/fast bands; timed randomized range; gauge linkage. Speed dynamics follow the installed original's 2–10 second target timer, 0.05 minimum speed, 0.4999/0.5001 band split, 0.05–0.2 sinusoidal variation and 0.005–0.05 lerp factor (normalized to real time for stable 30/60 FPS). Manual slider can temporarily override speed. Live readback displays both current output and target speed.
- Each supported scene/animation mode is checked before writing values. Aibu uses native `HFlag.speed`; supported Houshi/Sonyu loops use `HFlag.speedCalc` after the game's `WaitSpeedProc`, via Harmony postfix. No speed writes in unsupported modes or transition states. Aibu dual-hand handling remains owned by the original HandCtrl; manual-only speed override is opt-in.
- Optional automatic motion-strength switching, with a configurable randomized interval. Auto finish toggles, native gauge adjustments and optional replay use the game's existing HFlag action system.
- Replay follows the installed original KK_HCtrl IL rather than sending the same click in every mode. Houshi uses `again`; Sonyu `OUT_A` uses native insertion, then a delayed `speedup`, while `IN_A` goes straight to `speedup`. The gate waits at least 13 seconds and for idle voice, and avoids repeated requests or overwriting another user's pending click. A pure state-machine regression covers both game modes and 3P routes.
- Manual weak/strong loop controls route through the current native `HActionBase.SetPlay` (Houshi/Sonyu in two-person, 3P, and MMF/Dark modes 1/2/6/7/8/9 with an initialized action), not a fabricated animator. The same panel exposes native male/female gauge-threshold and finish requests with mode guards. The replay gate issues at most one request per completed animation before the next loop.
- Inherit defaults from an existing `BepInEx/plugins/KK_HCtrl.ini` when present, but always keep BepInEx-managed values authoritative. Legacy INI is never modified. Missing INI uses safe defaults.

**Uninstall the original `BepInEx/plugins/KK_HCtrl.dll` before installing HSControl.** Both plugins write to the same game fields, and simultaneously running them can cause conflicting state. Preserve `KK_HCtrl.ini` if you wish to import its defaults. Ensure that the shipped `MaximumUI.dll` comes from the same build. No original DLL, game binary, or user config is included in the package.

## 0.1.2 — native interaction corrections

- Stop capping `HFlag.gaugeFemale` at 99 when automatic female finish is disabled. This legacy HCtrl behavior blocked native climax/near-climax controls. Its old config line can remain on disk but does not suppress the game's own action.
- Manual male/female/both pre-finish gauge adjustments can be used without an active loop, including Aibu, and never clear the ongoing hand-click event. Only manual weak/strong `SetPlay` actions require an initialized animation and idle native click.
- `TryStartLoop` no longer clears `flags.click`, `flags.finish`, `voiceWait`, `isInsideFinish`, resets male gauge to 35 or overwrites Houshi motion behind the game's back. Returning from the game's `SetPlay` is sufficient to signal a successful explicit command.
- New HS self-drawn entry replaces copying the Config icon and placing faint white text over it; the MinimumUI window is shorter and makes several controls horizontally aligned.
- **Compatibility caution:** do not keep `KK_HCtrl.dll` loaded alongside `KK_HSControl.dll`. The current authorized Koikatsu plugin folder still lists `KK_HCtrl.dll` but not `KK_HSControl.dll`; confirm the intended game copy before comparing live behavior. No installed file was removed or altered by this change.

## Scope and caveats

The original `KK_HCtrl.dll` was inspected using its CLI method/field metadata and IL; no original source was copied. The new speed controller and lifecycle are real runtime code, **not the original binary**. Version 0.1.2 supports manual native WLoop/SLoop, automated replay and finish guards in modes 1/2/6/7/8/9; specialized finish animations remain dependent on the installed game's native action state and are not all force-replicated. H-scene behavior, compatibility with other animation mods and the default hotkey still require in-game E2E validation. Until those checks pass, use a copy of your game install when evaluating experimental plugin builds.

## Build / tests

```sh
dotnet build src/HSControl/HSControl.csproj -c Release
dotnet run --project tests/HSControl.Regression/HSControl.Regression.csproj -c Release
```

Output: `bin/Release/HSControl/KK_HSControl.dll`, with the same-build `MaximumUI.dll` alongside. The independent regression host checks the motion scheduler, mode guards, clamps, manual override, gauge linkage and legacy config parsing. MaximumUI tests/preview exporter run the **actual production View** with anonymous fixture data.
