# Femboy Mode (KK_FemboyMode) — v0.2.0

Independent female-card marker and scoped integrations for **Koikatsu / Koikatsu Party**.

This is **not a port of [KK_BecomeTrap](https://github.com/Totofer77/KK_BecomeTrap)**. BecomeTrap marks the male school player and modifies school animations and NPC interactions. Femboy Mode marks a **female** card without changing its built-in `sex` value or transferring male-player school hooks.

## Current features

- **Female Maker:** a **伪娘角色标志 (Femboy)** checkbox in the Character parameter tab.
- **Character-card persistence:** saves `IsFemboy=true` as extended card data under `MaxMixAlex.FemboyMode` (version 1). No extra data is written for unmarked cards.
- **Card load/update:** syncs the checkbox after switching/reloading cards, preserving temporary changes on state-preserving reloads.
- **Plugin APIs:** `FemboyModePlugin.IsFemboy(ChaControl)`, `IsFemboy(ChaFileControl)`, and `GetController(ChaControl)`.
- **Male-listed assets in female Maker:** a scoped `ListInfoBase.GetInfoInt(Sex)` override when editing a marked female card. This changes list filtering; it does **not** create the missing male-body editor panels or swap the female mesh.
- **Free H action restrictions:** when the active heroine is marked, hides the two standard front-insertion actions in `HSprite.SetSonyuStart` and blocks their click handlers. Only the vanilla two-character Free H flow is covered. A position that has no anal animation is not forced to offer one.
- **Kokan touch target:** in `HandCtrl.Init`, changes the local `aibulayer_02` item-parent transforms to a configurable bone. Adds a tagged `H/Aibu/Hit/kokan` sphere collider before the game's later `InitHitReactionCollider` pass, leaving original colliders in place. The new contact sphere and anchor offsets can be adjusted during an open Free H scene. If the configured male bone is missing, the plugin uses `cf_d_kokan` as a fallback.
- **Independent toggles:** Maker list filtering, Free H restrictions and Kokan anchoring can each be disabled through BepInEx configuration.

## Kokan anchor configuration

BepInEx writes the plugin's configuration under `BepInEx/config/MaxMixAlex.FemboyMode.cfg`. The setting names are:

| Section | Setting | Default |
| --- | --- | --- |
| Aibu alignment | Enable Kokan action anchor | true |
| Aibu alignment | Target bone | `cm_j_dan100` |
| Aibu alignment | Enable target hit collider | true |
| Aibu alignment | Target hit radius | `0.05` (Unity units) |
| Aibu alignment | Offset X / Y / Z | `0` |
| Aibu alignment | Rotation X / Y / Z | `0` degrees |
| Maker | Unlock male-listed assets | true |
| Free H | Restrict front insertion | true |
| Debug | Verbose logs | false |

The target bone must be an actual transform in the installed character's rig. It depends on the body/uncensor model; **do not assume every replacement body contains `cm_j_dan100`**. If it does not, a fallback offset can be calibrated relative to `cf_d_kokan`. The positional and rotational values update the proxy during a running scene if edited through ConfigurationManager, but changing the *bone name* requires re-entering the scene.

This approach repositions **item parents and mouse hit detection**. It does not retarget all vanilla animator clips or solve every IK configuration. Report the body model, target bone and offset when a posture is visibly incorrect.

## Build and install

Use the repository's centrally managed NuGet packages; the project targets `net35`.

```sh
dotnet restore src/FemboyMode/FemboyMode.csproj
dotnet build src/FemboyMode/FemboyMode.csproj -c Release --no-restore
```

Copy `bin/Release/FemboyMode/KK_FemboyMode.dll` to the game's `BepInEx/plugins/` directory. BepInEx 5, KKAPI and ExtendedSave are runtime prerequisites. The project also includes a dedicated GitHub Actions workflow that uploads the compiled DLL for successful runs.

## Remaining work and test matrix

**Not implemented:** a native male-body morph/settings panel inside female Maker, a model-independent full IK retargeting system, and male-style climax/finish state handling. Those should not be claimed as finished.

**Not yet game-tested:** compile passing is not proof of in-game compatibility. Verify a marked female card's saved flag and switch to an unmarked card, a male card, Maker list refresh, vanilla H action menus, positions with missing anal actions, click detection at the re-anchored Kokan target, and compatibility with UncensorSelector / KK_ClothingUnlocker / MultiTeamH. Do not bundle game assemblies or user card data in releases.
