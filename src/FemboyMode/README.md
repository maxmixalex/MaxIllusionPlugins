# Femboy Mode / 伪娘角色标志

Koikatsu / Koikatsu Party plugin. Adds a **female-card-only** Femboy marker to the Character tab of Maker. This is independent from [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap): that plugin targets male cards and the school player, whereas this plugin targets female cards and leaves the native sex value untouched.

## Implemented

- Maker checkbox **伪娘角色标志 (Femboy)**, visible only in the female Maker.
- Character-card extended data under `MaxMixAlex.FemboyMode`, key `IsFemboy`, data version 1.
- Independent per-card state, including card-load/UI refresh and no flag leakage when switching cards.
- Normal female and male cards remain unchanged; unmarked cards do not receive extra data.
- Public integration API: `FemboyModePlugin.IsFemboy(ChaControl)` and `GetController(ChaControl)`.
- Preserve unsaved Maker edits on state-preserving reloads.
- In a marked female Maker, permit male-only asset entries that use the game's `ListInfoBase` sex filter. Some tabs may require reopening to refresh their cached lists.
- In 2-character Free H, hide the two non-anal insert buttons for the currently marked heroine and block their direct click handlers; unmarked characters and story H retain original handling.

## Not yet implemented

This first increment is an actual **flag/card-persistence plugin**, not a claim that the full requested gameplay conversion is finished. The dedicated male-body slider UI, model-dependent contact alignment/IK, and male-style finish behavior remain to be implemented and tested. Cross-sex list filtering is not equivalent to adding an entire male-body Maker panel. The Free H restriction hook only covers the two native front-insertion actions; no unsupported 3P or mod-added action is claimed.

For cross-sex clothing item visibility, the existing **KK_ClothingUnlocker** provides a separate option. Femboy Mode does not flip `ChaControl.sex`, globally unlock all clothing, or rewrite another plugin's settings. Two different mods may expose the same asset list entries; compatibility with those combinations still needs in-game checking.

## Build and install

From the repository root:

`dotnet restore src/FemboyMode/FemboyMode.csproj`

`dotnet build src/FemboyMode/FemboyMode.csproj -c Release`

Copy `bin/Release/FemboyMode/KK_FemboyMode.dll` into `BepInEx/plugins/` in a Koikatsu installation with BepInEx 5, KKAPI and ExtendedSave. This repository does not distribute game binaries.

**Testing status:** Code was reviewed against the repository's current KKAPI project conventions, but no compiler or running game was available for an end-to-end verification. Verify marked female card save/reload, switching to an unmarked card, unaffected male cards, male-filtered items in Maker, and 2-character Free H menu/click behavior. Check a position without an anal animation to ensure the game does not hang.
