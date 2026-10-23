# Accessory / Clothing regression runner

Run with .NET SDK 9:

```sh
dotnet run --project tests/AccessoryClothing.Regression/AccessoryClothing.Regression.csproj -c Release
```

This executable links the production `AccessoryOperations`, `AccessoryHighlighter`,
`MaterialEditorBridge`, `CompressionModels`, `CompressionController`, `ItemCatalog`
and `AtomicXmlFile` sources. `ExternalStubs.cs` replaces external game, Unity and
logging services; it does not reimplement the transfer or persistence algorithms.
The test executable is excluded from the plugin artifact directory.

The assertions cover:

- Transfers between coordinates, including equal slot numbers, inactive coordinates,
  current working copies, overlapping moves, adjustments, invalid selections and
  clothing-to-accessory transfers. External-card adjustments cover normal empty
  slots, null sources/destinations and malformed adjustment arrays without partial
  writes.
- Coordinate-scoped event behavior; renderer restoration and nested iterator disposal
  after a reload exception or disposal.
- Private selection materials, external material edits, replaced accessory objects,
  and snapshots of mutable MaterialEditor properties.
- Saving deletions/unbindings, preservation of unrelated profiles, repeated equipment,
  character overrides, legacy keys/IDs, empty XML collections and `.bak` preservation.
- Failed XML reads retain the last valid library and current unsaved edits, block
  writes before mutation, and preserve both the unreadable file and the last backup.
  A successful reload after repairing the file enables saving again.
- The production controller's visibility/rebuild ordering, deletion-to-save path,
  settling after the final entry is removed, and per-frame equipment lookup reuse.
- Skipping irrelevant mesh clones and leaving garments unchanged during an unbound
  body-only preview.

The catalog work check performs 100 visibility queries with 20 equipped items.
The original controller performs 2,000 catalog lookups; the changed controller
performs 20 in the same frame and refreshes its snapshot in the next frame.
This counts managed work; it is not a game FPS benchmark.

The original `AccessoryOperations.cs` from `17f2d36` was substituted into the
earlier 28-case version of this runner during audit and failed 11 transfer/reload
assertions. Substituting the original `CompressionController.cs` failed five
visibility/persistence/cache assertions. The substitutions were isolated to those files, with the same
external doubles, so they demonstrate regressions in those components rather
than a complete baseline build of all projects.

The final runner has 38 cases. The eight external-card and corrupt-XML cases were
also executed before their targeted fixes: seven failed and the normal empty-slot
case passed. All 38 cases pass after the fixes.

Unity rendering, native coroutine scheduling, actual clothing deformation,
MoreAccessories hooks, MaterialEditor texture loading and character-card round
trips still require an installed game and the corresponding plugins. A passing
headless runner does not validate those engine integrations.
