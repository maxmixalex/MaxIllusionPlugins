# Femboy Mode for Koikatsu — v0.3.3

Independent per-female-character **伪娘标志 (Femboy)** and scoped Maker / Free H extensions for original Koikatsu and Koikatsu Party. This is **not** a port of [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap), which instead marks the *male school player*. We never overwrite a female card's vanilla `sex` field.

## Implemented

- **Two synchronized Maker switches:** 「伪娘角色标志 (Femboy)」in both Parameter > Character and Body > All of the female Maker.
- **Card-local state:** stores `MaxMixAlex.FemboyMode/IsFemboy` and optional male-bone X/Y/Z scaling (extended data version 2; compatible with version 1). Switching cards and state-preserving reloads are handled independently.
- **External API:** `FemboyModePlugin.IsFemboy(ChaControl)`, `IsFemboy(ChaFileControl)`, `GetController(ChaControl)`.
- **Maker asset unlock:** while a marked card is active, item-list sex filters are permitted to expose male-listed resources. Optional `KK_UncensorSelector` compatibility limits its male-body preset list availability to the marked female Maker, instead of globally enabling GenderBender. Reopen Maker after the first toggle to rebuild precomputed lists.
- **Body model controls:** X, Y, Z sliders under Body > All (0.5–2.0 scale, 1.0 neutral) are shown only when the character is marked; they act on the separately configurable `Male geometry bone` (`cm_j_dan100` by default) when it exists in the installed male geometry; individual values persist in the character card. These are **new model-specific controls**, not a transplant of all vanilla male Maker panels. Absent bones are safely ignored.
- **Free H filtering:** the two native non-anal insertion action buttons are hidden for the marked leading heroine, on **both** seven-button UI pages (normal and female-initiative). Their click handlers are guarded, without forcing unavailable positions or changing the unmarked heroine's behavior.
- **Kokan touch relocation:** action anchors are created *after* optional body objects load, immediately before the game's `HandCtrl.InitHitReactionCollider`; a configurable male-bone hit sphere is added. A `GetOnMouseAibuCollider` postfix favors this sphere over the old female-model Kokan collider and rejects the old region for marked characters. The original actor's touch-area restrictions remain intact. The fallback is `cf_d_kokan` if the configured target bone is absent.
- **Male-style finishing visual:** on a marked heroine's climax in Free H, the native male outside-finish particle (`HParticleCtrl` particle ID 0, also used in `MetaballCtrl.Proc`) is cloned at her chosen body bone. The original Aibu squirt particle **and its associated sound** are suppressed only if the replacement effect was successfully started; otherwise vanilla effects remain. The original `HAibu.randSio` instance is captured once at construction and released when the scene closes, avoiding Unity scene scans during unrelated random calls. Effects are selected using the leading heroine's scene particle controller (never the first arbitrary HParticleCtrl in the scene). This **does not yet replace female animation/voice clips or the vanilla climax counters**; it only adds the correctly targeted male-style *visual effect*.

## RX18 rig compatibility (verified from local installation inventory)

The inspected RX18 installation contains the actual Unity transform
`cm_J_dan100_00`, whereas older default config used `cm_j_dan100`.
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
| Free H | Use male-style finish FX | true |
| Free H | Suppress female Aibu squirt FX | true |
| Aibu alignment | Enable Kokan action anchor | true |
| Aibu alignment | Enable target hit collider | true |
| Aibu alignment | Target bone | `cm_j_dan100` |
| Aibu alignment | Target hit radius | `0.05` |
| Aibu alignment | Offset X/Y/Z, Rotation X/Y/Z | 0 |
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

**Remaining functional gap:** true replacement of the female climax **animation and voice** with a male-character animation/voice set has not been implemented. Replacing `HFlag.finish` is not a safe shortcut: it operates on the **actual male participant** and corrupts simultaneous finishes. A full animation rewrite needs appropriate actor clips and skeleton retargeting, plus runtime validation. The original gender-specific voice resources have not been verified in the installed game assets, so this project does not claim to replace them. This repository does not bundle game assets or replacement models.
