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

The initial runner had 38 cases. The eight external-card and corrupt-XML cases were
also executed before their targeted fixes: seven failed and the normal empty-slot
case passed. All 38 cases pass after the fixes.

The current runner additionally links the production `CompressionEditorView`,
`CompressionEditorSupport`, `CompressionEditorData` and `CompressionGui`,
referencing the real `MaximumUI.dll`. Twelve UI cases
exercise actual widget callbacks against the production controller: numeric unit
conversion, kind/side/enabled editing, stale selection/coordinate rejection,
virtual row selection, add/copy/reorder/delete, binding and XML save, importing
with a fresh identity, import-selection retention, layout at 680×460 and
900×720, and data-cache invalidation. With 20 equipped items, 200 actual slider
callbacks across simulated frames schedule 200 deformation updates with zero
additional editor catalog lookups, renderer scans or import-list copies. The
production advanced-window adapter and the cache shared with native Maker controls
are exercised; ordinary controller equipment polling is tested separately. Source
changes, successful save/reload, equipment changes with unsaved edits, coordinate
and character switches refresh the correct snapshot. Failed XML reload preserves
the last valid snapshot. These are managed work counts, not game FPS measurements.

Six further ABM boundary cases link the production `AccessoryEditorSession` and
`CoordinateCardSource` together with the actual transfer/deletion operations:

- A display-only model refresh preserves a valid pending command.
- Source, destination, selected slots, list kind and command-option changes reject
  old commands before or after the next model refresh. In-place accessory type/ID
  and clothing-ID changes are rejected too; ordinary transform edits retain the
  same target identity and are not frozen into a stale full-field copy.
- A replacement working coordinate at the same coordinate index cannot receive an
  old removal confirmation.
- Closing and reopening a session cannot reactivate its old commands or confirmations.
- The actual card adapter distinguishes two cards with identical display names and
  item IDs, and an explicitly reviewed command uses the new card's own parameters.
- Reset clears retained card data and invalidates the per-frame availability cache.

The card tests use anonymous temporary files. Only the external game's `LoadFile`
and Maker file-list services are doubled; card caching, identity guards and data
operations run their production implementation. The runner contains 56 cases:
38 original regression groups, 12 Clothing UI/cache groups and 6 ABM session groups.

The cross-framework test reference can report NU1702; the
.NET 9 host deliberately executes the managed part of the net35 library. It also
loads the compile-time Unity reference for MaximumUI runtime type signatures;
that reference stays inside test output and is not a shipped Unity runtime.

`CompressionEditorPreview.CreatePreview()` returns the actual production editor
tree with anonymous in-memory content for the preview exporter. It does not read
uploaded documents or the user's configuration directory.

To validate a local clothing XML without editing the original:

```sh
dotnet run --project tests/AccessoryClothing.Regression/AccessoryClothing.Regression.csproj -c Release -- --validate-presets /path/to/ClothingCompression.xml
```

This copies the named file into a temporary config directory under the production
filename `MaxMixAlex.ClothingCompression.xml`, runs the actual loader, saves and
reloads the copy, and compares all serialized model values. The original is never
opened for writing. The explicit path is only a diagnostic input; production has
no fallback to the old filename. The supplied real document passed with 35
profiles and 38 deformations. Personal preset content is not included in source
or CI artifacts.

Unity rendering, native coroutine scheduling, actual clothing deformation,
MoreAccessories hooks, MaterialEditor texture loading and character-card round
trips still require an installed game and the corresponding plugins. A passing
headless runner does not validate those engine integrations.
