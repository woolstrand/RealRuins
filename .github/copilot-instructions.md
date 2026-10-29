# RealRuins — Copilot Instructions

## What This Project Is

RealRuins is a RimWorld mod (C#, .NET 4.7.2, Harmony patching) that:
1. Periodically uploads the player's base as an XML "blueprint" to a central server
2. Downloads blueprints from other players and places them as ruins on newly generated maps
3. Creates persistent Points of Interest (POIs) on the world map from other players' bases

**Game Version**: 1.7 (current build target). Source in `Source/Classes/`. Output: `1.7/Assemblies/RealRuins.dll`.

## Knowledge Base

Detailed documentation is in `docs/`. Read the relevant file before making changes to a subsystem:

| File | Covers |
|------|--------|
| [docs/01-architecture-overview.md](docs/01-architecture-overview.md) | Full system map, data flows, namespace layout, cross-cutting concerns |
| [docs/02-snapshotting-upload.md](docs/02-snapshotting-upload.md) | Blueprint capture (SnapshotGenerator), upload/download (SnapshotManager, APIService), local cache (SnapshotStoreManager), compression |
| [docs/03-blueprint-pipeline.md](docs/03-blueprint-pipeline.md) | Blueprint data model, XML loading, preprocessing, deterioration, scavenging, map placement (BlueprintTransferUtility), pawn restoration, defender forces |
| [docs/04-scattering.md](docs/04-scattering.md) | ScatterOptions, RuinsScatterer, GenStep_ScatterRealRuins, GenStep_ScatterPOIRuins, proximity system |
| [docs/05-world-objects.md](docs/05-world-objects.md) | AbandonedBaseWorldObject, SmallRuinsWorldObject, RealRuinsPOIWorldObject, incidents, caravan/pod arrival actions, RuinedBaseComp state machine, triggers, thoughts |
| [docs/06-planetary-ruins.md](docs/06-planetary-ruins.md) | Pre-game wizard (Page_PlanetaryRuinsLoader), blueprint classification (BlueprintAnalyzer), POI factory (RealRuinsPOIFactory), blueprint pool management |
| [docs/07-settings-ui.md](docs/07-settings-ui.md) | Settings pages, RealRuins_ModSettings, import/export serialization — including known critical bugs |
| [docs/08-utility-debug.md](docs/08-utility-debug.md) | Extensions, CoroutineManager, FactionSelector, SimpleJSON, Debug logging system, startup sequence |

## Critical Known Bugs (Fix Before Touching These Areas)

1. **`SnapshotManager` thread safety (partly fixed)** — `Pop()` in `LoadNextSnapshot` is locked, but `Add` calls in `AggressiveLoadSnapshots`/`LoadSomeSnapshots`, `Reset()` replacing the list, and the `loadedSnapshotsCount++`/`failedSnapshotsCount++` counters are not synchronized.

2. **`SnapshotStoreManager.StoreBinaryData` dedup hazards** — the `'='` split is consistent now, but a date prefix that fails `int.TryParse` silently skips dedup, and nothing guards the raw background thread (an exception in `GetFiles`/`File.Delete` is unhandled).

3. **`chronologicalAge` writes biological age** — `SnapshotGenerator.EncodePawn` writes `AgeBiologicalTicks` for both fields.

4. **"No limit" cache setting wipes the cache** — `diskCacheLimit = -1` makes `CheckCacheSizeLimits()` delete every cached file (`totalSize > -1 * 1024 * 1024` is always true).

5. **`ScatterOptions.Copy()` is incomplete** — it does not copy `costCap`, `startingPartyPoints` or `overridePosition`; every `options.Copy()` handed downstream loses them.

6. **`RealRuins_ModSettings.Reset()` is incomplete** — it does not reset `diskCacheLimit`, and assigns `new ScatterOptions()` so `defaultScatterOptions` stops being the `ScatterOptions.Default` singleton.

7. **Double `.bp` extension** — `StoreBinaryData` always appends `.bp`, while `SnapshotManager` passes names already ending in `.bp` (e.g. `local-<id>.bp.bp`).

8. **`BlueprintRecoveryService.TryRecoverInPlace` throws without `</cell>`** — `LastIndexOf` returns -1 and `Substring(0, -1)` throws `ArgumentOutOfRangeException`.

9. **`SnapshotManager.LoadSomeSnapshots` retries forever** — `retries` is decremented but never checked.

10. **`BlueprintAnalyzer` integer division by zero** — `haulableStacksCount * 5 / internalArea` throws when `internalArea` is 0.

Reported but not yet verified: settings export may be culture-sensitive (`TryImport` can half-apply on failure); `APIService` may format `coverage` culture-sensitively; `Blueprint.Part` may drop the last row/column; some `Rand.Range` calls can have min > max; raid trigger loops may spin forever when `InBounds` keeps failing; `AdvancedSettingsPage` buffers may go stale after import or `Reset()`.

Fixed earlier (docs were stale, do not re-add): `AdvancedSettingsPage` buffers are seeded from settings in the constructor; `extraGenStepDefs.Concat()` result is assigned; `ScatterOptions.asIs()` uses `Default.Copy()`; the `StoreBinaryData` `'='`/`'-'` mismatch.

## Build

Never build unless explicitly asked. Build target: `Source/RealRuins.csproj` → `1.7/Assemblies/RealRuins.dll`.

## Important Rules

- All settings fields in `RealRuins_ModSettings` are `public static` — no instance needed
- `ScatterOptions.Default` is a mutable singleton — use `Copy()` before modifying
- `PlanetaryRuinsInitData.shared` is valid only during new-game initialization flow
- Harmony patches live in `RealRuins.cs` as nested inner classes — one class per patch
- When adding a new source file, add it to `Source/RealRuins.csproj` (the csproj uses `<Compile Include="Classes\**\*.cs" />` glob — new files in subdirectories are picked up automatically)
- Never use `DecompiledSources/` — read-only reference only; use Harmony patches to modify behavior
