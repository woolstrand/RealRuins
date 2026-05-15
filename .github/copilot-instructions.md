# RealRuins — Copilot Instructions

## What This Project Is

RealRuins is a RimWorld mod (C#, .NET 4.7.2, Harmony patching) that:
1. Periodically uploads the player's base as an XML "blueprint" to a central server
2. Downloads blueprints from other players and places them as ruins on newly generated maps
3. Creates persistent Points of Interest (POIs) on the world map from other players' bases

**Game Version**: 1.6 (current build target). Source in `Source/Classes/`. Output: `1.6/Assemblies/RealRuins.dll`.

## Knowledge Base

Detailed documentation is in `docs/`. Read the relevant file before making changes to a subsystem:

| File | Covers |
|------|--------|
| [docs/01-architecture-overview.md](docs/01-architecture-overview.md) | Full system map, data flows, namespace layout, cross-cutting concerns |
| [docs/02-snapshotting-upload.md](docs/02-snapshotting-upload.md) | Blueprint capture (SnapshotGenerator), upload/download (SnapshotManager, APIService), local cache (SnapshotStoreManager), compression |
| [docs/03-blueprint-pipeline.md](docs/03-blueprint-pipeline.md) | Blueprint data model, XML loading, preprocessing, deterioration, scavenging, map placement (BlueprintTransferUtility), pawn restoration, defender forces |
| [docs/04-scattering.md](docs/04-scattering.md) | ScatterOptions, RuinsScatterer, GenStep_ScatterRealRuins, GenStep_ScatterPOIRuins, proximity system |
| [docs/05-world-objects.md](docs/05-world-objects.md) | AbandonedBaseWorldObject, SmallRuinsWorldObject, RealRuinsPOIWorldObject, incidents, caravan/pod arrival actions, RuinedBaseComp state machine, triggers, thoughts |
| [docs/06-planetary-ruins.md](docs/06-planetary-ruins.md) | Pre-game wizard (Page_PlanetaryRuinsLoader), blueprint classification (BlueprintAnalyzer), POI factory (RealRuinsPOIFactory), world component (MapRuinsStore) |
| [docs/07-settings-ui.md](docs/07-settings-ui.md) | Settings pages, RealRuins_ModSettings, import/export serialization — including known critical bugs |
| [docs/08-utility-debug.md](docs/08-utility-debug.md) | Extensions, CoroutineManager, FactionSelector, SimpleJSON, Debug logging system, startup sequence |

## Critical Known Bugs (Fix Before Touching These Areas)

1. **Settings text buffers erase saved blacklists** — `AdvancedSettingsPage` string buffer fields are never initialized from saved settings. Opening the "Advanced" tab silently clears `spawnBlacklist`, `materialBlacklist`, and `fallbackMaterial`. See [docs/07-settings-ui.md](docs/07-settings-ui.md).

2. **`extraGenStepDefs.Concat()` result discarded** — In `RealRuins.cs` `MapGenerator_GenerateMap_Patch`, the `Concat` call result is not assigned back, so POI extra gen steps are never applied.

3. **`snapshotsToLoad.Pop()` race condition** — In `SnapshotManager`, multiple concurrent download callbacks call `Pop()` on a shared `List<string>` without a lock.

4. **`SnapshotStoreManager` version dedup broken** — `StoreBinaryData()` splits the new filename on `'='` but the existing filename on `'-'` — the version comparison never works correctly.

5. **`ScatterOptions.asIs()` mutates shared Default** — `ScatterOptions.asIs()` directly modifies `ScatterOptions.Default` singleton before returning it. Never call `asIs()` in a context where `Default` might be read again.

6. **`MapRuinsStore` uses `LookMode.Undefined`** — `remoteMapIds` list will not serialize correctly. Should be `LookMode.Value`.

7. **`chronologicalAge` writes biological age** — `SnapshotGenerator.EncodePawn` writes `AgeBiologicalTicks` for both fields.

## Build

Never build unless explicitly asked. Build target: `Source/RealRuins.csproj` → `1.6/Assemblies/RealRuins.dll`.

## Important Rules

- All settings fields in `RealRuins_ModSettings` are `public static` — no instance needed
- `ScatterOptions.Default` is a mutable singleton — use `Copy()` before modifying
- `PlanetaryRuinsInitData.shared` is valid only during new-game initialization flow
- Harmony patches live in `RealRuins.cs` as nested inner classes — one class per patch
- When adding a new source file, add it to `Source/RealRuins.csproj` (the csproj uses `<Compile Include="Classes\**\*.cs" />` glob — new files in subdirectories are picked up automatically)
- Never use `DecompiledSources/` — read-only reference only; use Harmony patches to modify behavior
