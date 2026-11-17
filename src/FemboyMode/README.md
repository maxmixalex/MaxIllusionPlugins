# Femboy Mode for Koikatsu — v0.3.0

Independent per-female-character **伪娘标志 (Femboy)** and scoped Maker / Free H extensions for original Koikatsu and Koikatsu Party. This is **not** a port of [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap), which instead marks the *male school player*. We never overwrite a female card's vanilla `sex` field.

## Implemented

- **Two synchronized Maker switches:** 「伪娘角色标志 (Femboy)」in both Parameter > Character and Body > All of the female Maker.
- **Card-local state:** stores `MaxMixAlex.FemboyMode/IsFemboy` and optional male-bone X/Y/Z scaling (extended data version 2; compatible with version 1). Switching cards and state-preserving reloads are handled independently.
- **External API:** `FemboyModePlugin.IsFemboy(ChaControl)`, `IsFemboy(ChaFileControl)`, `GetController(ChaControl)`.
- **Maker asset unlock:** while a marked card is active, item-list sex filters are permitted to expose male-listed resources. Optional `KK_UncensorSelector` compatibility limits its male-body preset list availability to the marked female Maker, instead of globally enabling GenderBender. Reopen Maker after the first toggle to rebuild precomputed lists.
- **Body model controls:** X, Y, Z sliders under Body > All (0.5–2.0 scale, 1.0 neutral) act on the specifically named bone `cm_j_dan100` when it exists in the installed male geometry; individual values persist in the character card. These are **new model-specific controls**, not a transplant of all vanilla male Maker panels. Absent bones are safely ignored.
- **Free H filtering:** the two native non-anal insertion action buttons are hidden for the marked leading heroine. Their click handlers are guarded, without forcing unavailable positions or changing the unmarked heroine's behavior.
- **Kokan touch relocation:** action anchors are created *after* optional body objects load, immediately before the game's `HandCtrl.InitHitReactionCollider`; a configurable male-bone hit sphere is added. A `GetOnMouseAibuCollider` postfix favors this sphere over the old female-model Kokan collider and rejects the old region for marked characters. The original actor's touch-area restrictions remain intact. The fallback is `cf_d_kokan` if the configured target bone is absent.
- **Male-style finishing visual:** on a marked heroine's climax in Free H, the native male outside-finish particle (`HParticleCtrl` particle ID 0, also used in `MetaballCtrl.Proc`) is cloned at her chosen body bone. The female Aibu squirt particle (ID 2) can optionally be suppressed. This **does not yet replace female animation/voice clips or the vanilla climax counters**; it only adds the correctly targeted male-style *visual effect*.

## Configuration

The file `BepInEx/config/MaxMixAlex.FemboyMode.cfg` is created by BepInEx. Most values can be changed through BepInEx ConfigurationManager:

| Section | Setting | Default |
| --- | --- | --- |
| Maker | Unlock male-listed assets | true |
| Maker | Enable male geometry bone scaling | true |
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

The anchor local offsets and rotations and the new hit sphere radius are refreshed during an active scene. Changing target bone name requires re-entering Free H. Model geometry scaling is based on that bone's initial local scale; when the flag is disabled or the card is replaced, the controller restores the original scale. Model-specific compatibility with KKABMX, UncensorSelector and other runtime bone modifiers still requires game testing.

**Important:** the female Maker already shares many base body-shape fields with the male Maker; enabling cross-sex items and adding bone scaling does not mean every male-only native menu control is available. UI sections that exist only in the male Maker need separate adapters.

## Build / install

Targets .NET Framework 3.5 / BepInEx 5. Requires KKAPI and ExtendedSave, and uses optional integrations when installed. Build via:

```sh
dotnet restore src/FemboyMode/FemboyMode.csproj
dotnet build src/FemboyMode/FemboyMode.csproj -c Release --no-restore
```

Copy `bin/Release/FemboyMode/KK_FemboyMode.dll` to `Koikatu/BepInEx/plugins/`. The dedicated GitHub Actions workflow produces a downloadable `KK_FemboyMode` artifact.

## Validation and known limitations

Passing CI proves **C# compilation only**, not the actual motion or visual results inside the game. Manual checks are still required:

1. Save a marked card and reload it; switch to an unmarked female card, then a male card; ensure no state leakage.
2. Toggle the flag in both Maker pages; verify the controls synchronize and male list entries become visible on reopening Maker.
3. Test bone scaling on installed male geometry, restore when flag is removed, and combination with KKABMX / UncensorSelector.
4. Test touch alignment for the actual model and several poses, including source hit collider position and new hit sphere.
5. Test both available and unavailable anal-only positions in 2-person Free H; verify direct action handlers and active-character isolation.
6. Test climax effect when the male particle resource is present, original female particle suppression, and compatibility with other effects mods.

**Remaining functional gap:** true replacement of the female climax **animation and voice** with a male-character animation/voice set has not been implemented. Replacing `HFlag.finish` is not a safe shortcut: it operates on the **actual male participant** and corrupts simultaneous finishes. A full animation rewrite needs appropriate actor clips and skeleton retargeting, plus runtime validation. This repository does not bundle game assets or replacement models.
