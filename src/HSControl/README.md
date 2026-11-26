# HSControl for Koikatsu

A new, independently implemented controller for a running Koikatsu / Koikatsu Party H scene. The C# source uses the original game's HFlag and HSceneProc public members, not a wrapper around the original KK_HCtrl.dll.

## Runtime features

- Default `F8` opens the MaximumUI window. BepInEx configuration is under `MaxMixAlex.HSControl.cfg`. Change the hotkey if it conflicts with another installed plugin.
- Four speed modes: off; slow/full/fast bands; timed randomized range; gauge linkage. Manual slider can temporarily override speed. Live readback displays both current output and target speed.
- Each supported scene/animation mode is checked before writing values. Aibu uses native `HFlag.speed`; supported Houshi/Sonyu loops use `HFlag.speedCalc` after the game's `WaitSpeedProc`, via Harmony postfix. No speed writes in unsupported modes or transition states.
- Optional automatic motion-strength switching, with a configurable randomized interval. Auto finish toggles, native gauge adjustments and optional replay use the game's existing HFlag action system.
- Inherit defaults from an existing `BepInEx/plugins/KK_HCtrl.ini` when present, but always keep BepInEx-managed values authoritative. Legacy INI is never modified. Missing INI uses safe defaults.

**Uninstall the original `BepInEx/plugins/KK_HCtrl.dll` before installing HSControl.** Both plugins write to the same game fields, and simultaneously running them can cause conflicting state. Preserve `KK_HCtrl.ini` if you wish to import its defaults. Ensure that the shipped `MaximumUI.dll` comes from the same build. No original DLL, game binary, or user config is included in the package.

## Scope and caveats

The original `KK_HCtrl.dll` was inspected using its CLI method/field metadata and IL; no original source was copied. The new speed controller and lifecycle are real runtime code, **not the original binary**. Version 0.1.0 does not yet reproduce the original plugin's in-scene Sprite button injection, advanced per-mode restart coroutines or every 3P mode. H-scene behavior, compatibility with other animation mods and the default hotkey still require in-game E2E validation. Until those checks pass, use a copy of your game install when evaluating experimental plugin builds.

## Build / tests

```sh
dotnet build src/HSControl/HSControl.csproj -c Release
dotnet run --project tests/HSControl.Regression/HSControl.Regression.csproj -c Release
```

Output: `bin/Release/HSControl/KK_HSControl.dll`, with the same-build `MaximumUI.dll` alongside. The independent regression host checks the motion scheduler, mode guards, clamps, manual override, gauge linkage and legacy config parsing. MaximumUI tests/preview exporter run the **actual production View** with anonymous fixture data.
