# Femboy Mode / 伪娘角色标志

Koikatsu / Koikatsu Party plugin. Adds a **female-card-only** Femboy marker to the Character tab of Maker. This is independent from [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap): that plugin targets male cards and the school player, whereas this plugin targets female cards and leaves the native sex value untouched.

## Implemented

- Maker checkbox **伪娘角色标志 (Femboy)**, visible only in the female Maker.
- Character-card extended data under `MaxMixAlex.FemboyMode`, key `IsFemboy`, data version 1.
- Independent per-card state, including card-load/UI refresh and no flag leakage when switching cards.
- Normal female and male cards remain unchanged; unmarked cards do not receive extra data.
- Public integration API: `FemboyModePlugin.IsFemboy(ChaControl)` and `GetController(ChaControl)`.
- Preserve unsaved Maker edits on state-preserving reloads.

## Not yet implemented

This first increment is an actual **flag/card-persistence plugin**, not a claim that the full requested gameplay conversion is finished. The male-only body editor options, model-dependent contact alignment/IK, Free H action restrictions, and ending-state changes require targeted integrations and live-game testing. None are patched in this increment.

For cross-sex clothing item visibility, the existing **KK_ClothingUnlocker** provides a separate option. Femboy Mode does not flip `ChaControl.sex`, globally unlock all clothing, or rewrite another plugin's settings.

## Build and install

From the repository root:

`dotnet restore src/FemboyMode/FemboyMode.csproj`

`dotnet build src/FemboyMode/FemboyMode.csproj -c Release`

Copy `bin/Release/FemboyMode/KK_FemboyMode.dll` into `BepInEx/plugins/` in a Koikatsu installation with BepInEx 5, KKAPI and ExtendedSave. This repository does not distribute game binaries.

**Testing status:** Code was reviewed against the repository's current KKAPI project conventions, but no compiler or running game was available for an end-to-end verification. Verify marked female card save/reload, switching to an unmarked card, and unaffected male cards.
